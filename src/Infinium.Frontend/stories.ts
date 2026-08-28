import type {
  BootstrapRequest,
  BootstrapResponse,
  CancelResponse,
  ProgressEvent,
  ProgressRequest,
  ProgressResponse,
  ProgressSubscriptionRequest,
  ResultDetailRequest,
  ResultDetailResponse,
  ResultItemKind,
  ResultListRequest,
  ResultListResponse,
  ResultSummary,
  ResyncEvent,
  LifecycleState,
} from "./generated/renderer-contract.generated.js";
import { validateBootstrapRequest, validateCancelRequest, validateProgressRequest, validateProgressSubscriptionRequest, validateResultDetailRequest, validateResultListRequest, type ApplicationClient } from "./client.js";

export type StoryName = "setup" | "empty" | "active" | "completed" | "failed" | "gap" | "lead-only" | "stale" | "conflict" | "reconnect" | "large-pagination";

export const storyNames: readonly StoryName[] = [
  "setup", "empty", "active", "completed", "failed", "gap", "lead-only", "stale", "conflict", "reconnect", "large-pagination",
];

interface StoryDefinition {
  readonly name: StoryName;
  readonly itemCount: number;
  readonly lifecycle: LifecycleState;
  readonly kind: ResultItemKind;
  readonly progress: StoryProgressCounters;
  readonly forcedOutcome?: "conflict" | "resync-required";
  readonly emitsReconnect?: boolean;
}

interface StoryProgressCounters {
  readonly total: number;
  readonly completed: number;
  readonly reused: number;
  readonly queued: number;
  readonly running: number;
  readonly failed: number;
  readonly skipped: number;
  readonly unsupported: number;
  readonly limited: number;
  readonly invalidated: number;
  readonly gap: number;
}

export const storyRunId = "opaque_run_identity_0001";
const counters = (values: Partial<StoryProgressCounters> & Pick<StoryProgressCounters, "total">): StoryProgressCounters => ({
  completed: 0, reused: 0, queued: 0, running: 0, failed: 0, skipped: 0, unsupported: 0, limited: 0, invalidated: 0, gap: 0, ...values,
});

const definitions: Readonly<Record<StoryName, StoryDefinition>> = {
  setup: { name: "setup", itemCount: 0, lifecycle: "queued", kind: "abstention", progress: counters({ total: 1, queued: 1 }) },
  empty: { name: "empty", itemCount: 0, lifecycle: "completed", kind: "supported-case", progress: counters({ total: 0 }) },
  active: { name: "active", itemCount: 6, lifecycle: "running", kind: "finding", progress: counters({ total: 6, completed: 2, running: 1, queued: 3 }) },
  completed: { name: "completed", itemCount: 8, lifecycle: "completed", kind: "supported-case", progress: counters({ total: 8, completed: 8 }) },
  failed: { name: "failed", itemCount: 1, lifecycle: "failed", kind: "failure", progress: counters({ total: 1, failed: 1 }) },
  gap: { name: "gap", itemCount: 3, lifecycle: "completed-with-gaps", kind: "coverage-gap", progress: counters({ total: 3, gap: 3 }) },
  "lead-only": { name: "lead-only", itemCount: 4, lifecycle: "completed", kind: "lead-only-case", progress: counters({ total: 4, completed: 4 }) },
  stale: { name: "stale", itemCount: 2, lifecycle: "completed", kind: "finding", progress: counters({ total: 2, completed: 2 }), forcedOutcome: "resync-required" },
  conflict: { name: "conflict", itemCount: 2, lifecycle: "completed", kind: "finding", progress: counters({ total: 2, completed: 2 }), forcedOutcome: "conflict" },
  reconnect: { name: "reconnect", itemCount: 5, lifecycle: "running", kind: "finding", progress: counters({ total: 5, completed: 2, running: 1, queued: 2 }), emitsReconnect: true },
  "large-pagination": { name: "large-pagination", itemCount: 100_000, lifecycle: "completed", kind: "finding", progress: counters({ total: 100_000, completed: 100_000 }) },
};

const error = <TCode extends "conflict" | "resync-required">(code: TCode) => ({ code, inert_detail: "The diagnostic projection requires authoritative reconciliation.", retry_may_be_safe: false });
const storyRunNotFound = () => ({ outcome: "rejected" as const, error: { code: "not-found" as const, inert_detail: "The requested story run does not exist.", retry_may_be_safe: false } });

export class StoryApplicationClient implements ApplicationClient {
  public constructor(
    private readonly story: StoryName,
  ) {}

  public async bootstrap(request: BootstrapRequest): Promise<BootstrapResponse> {
    validateBootstrapRequest(request);
    return {
      outcome: "accepted",
      bootstrap: {
        application_contract_version: "1.13.0", domain_contract_version: "1.6.0", storage_contract_version: "1.16.0", renderer_contract_version: "1.4.0",
        coordinator_health: "healthy", configuration_availability: this.story === "setup" ? "unavailable" : "available",
        capabilities: [], recent_runs: [], projection_version: "projection_0001", coordinator_instance_id: "coordinator_identity_0001", coordinator_fencing_epoch: "18446744073709551615",
      },
    };
  }

  public async listResultItems(request: ResultListRequest): Promise<ResultListResponse> {
    validateResultListRequest(request);
    const definition = definitions[this.story];
    if (request.run_id !== storyRunId) return storyRunNotFound();
    if (definition.forcedOutcome === "resync-required") return { outcome: "resync-required", error: error("resync-required"), current_projection_version: "projection_0002" };
    if (definition.forcedOutcome === "conflict") return { outcome: "conflict", error: error("conflict"), conflict: { expected_revision: "projection_0001", current_revision: "projection_0002", disposition: "stale-revision" } };
    if (!request.kinds.includes(definition.kind)) {
      return { outcome: "accepted", page: { items: [], has_more: false, projection_version: "projection_0001" } };
    }
    const offset = decodeCursor(request.after_cursor);
    const search = request.search_text.toLowerCase();
    const items: ResultSummary[] = [];
    let scanOffset = offset;
    let resumeOffset = offset;
    let hasMore = false;
    while (scanOffset < definition.itemCount) {
      const candidate = item(definition, scanOffset, storyRunId);
      scanOffset++;
      if (!candidate.inert_summary.toLowerCase().includes(search)) continue;
      if (items.length === request.requested_page_size) {
        hasMore = true;
        break;
      }
      items.push(candidate);
      resumeOffset = scanOffset;
    }
    return { outcome: "accepted", page: { items, ...(hasMore ? { next_cursor: encodeCursor(resumeOffset) } : {}), has_more: hasMore, projection_version: "projection_0001" } };
  }

  public async getResultDetail(request: ResultDetailRequest): Promise<ResultDetailResponse> {
    validateResultDetailRequest(request);
    const definition = definitions[this.story];
    const match = /^result_item_([0-9]{12})$/u.exec(request.item_id);
    const index = match === null ? -1 : Number.parseInt(match[1]!, 10) - 1;
    if (request.run_id !== storyRunId || request.kind !== definition.kind || index < 0 || index >= definition.itemCount) {
      return request.run_id !== storyRunId
        ? storyRunNotFound()
        : { outcome: "rejected", error: { code: "not-found", inert_detail: "The requested story result does not exist.", retry_may_be_safe: false } };
    }
    const summary = item(definition, index, storyRunId);
    return { outcome: "accepted", detail: { summary, inert_conclusion: "Bounded diagnostic conclusion.", inert_cause: "Bounded diagnostic cause.", evidence_ids: ["evidence_identity_0001"], contradicting_evidence_ids: [], recommendation_ids: [], taxonomy_assignment_ids: [], finding_occurrence_ids: [], hypothesis_ids: [], inert_uncertainty: [], inert_gaps: [], source_payload_id: "payload_identity_0001", source_payload_sha256: "0".repeat(64), subject_ids: ["subject_identity_0001"], projection_version: "projection_0001" } };
  }

  public async getProgress(request: ProgressRequest): Promise<ProgressResponse> {
    validateProgressRequest(request);
    if (request.run_id !== storyRunId) return storyRunNotFound();
    const definition = definitions[this.story];
    return { outcome: "accepted", progress: progress(request.run_id, definition, "18446744073709551615") };
  }

  public subscribeProgress(request: ProgressSubscriptionRequest, listener: (event: ProgressEvent | ResyncEvent) => void): () => void {
    validateProgressSubscriptionRequest(request);
    if (request.run_id !== storyRunId) throw new Error("The requested story run does not exist.");
    const definition = definitions[this.story];
    const metadata = { coordinator_instance_id: "coordinator_identity_0001", coordinator_fencing_epoch: "1", subscription_id: request.subscription_id, durable_event_sequence: "1", projection_version: "projection_0001", run_scope: request.run_id, resume_cursor: "cmVzdW1l" } as const;
    if (definition.emitsReconnect === true) listener({ outcome: "resync-required", event_kind: "resync-required", metadata, reason: "coordinator-restart", error: error("resync-required"), current_projection_version: "projection_0002" });
    else listener({ outcome: "accepted", event_kind: "progress", metadata, progress: progress(request.run_id, definition, "1") });
    return () => undefined;
  }

  public async cancel(targetRequestId: string, gestureId: string): Promise<CancelResponse> {
    validateCancelRequest(targetRequestId, gestureId);
    return { outcome: "accepted" };
  }
}

function item(definition: StoryDefinition, index: number, runId: string): ResultSummary {
  const identity = String(index + 1).padStart(12, "0");
  return {
    item_id: `result_item_${identity}`,
    kind: definition.kind,
    run_id: runId,
    logical_id: `logical_result_${identity}`,
    ...(definition.kind === "finding" || definition.kind === "coverage-gap" || definition.kind === "failure" ? {} : { case_occurrence_id: `case_occurrence_${identity}` }),
    inert_summary: index === 0 ? "<img src=x onerror=alert(1)> remains inert" : `${definition.name} summary for ${runId}`,
    severity: definition.kind === "failure" ? "high" : "informational",
    confidence: definition.kind === "lead-only-case" ? "limited" : "supported",
    analyzer_id: "diagnostic-story",
    analyzer_version: "1.0.0",
  };
}

function progress(runId: string, definition: StoryDefinition, eventSequence: string) {
  const unavailable = { availability: "unavailable" as const };
  const value = definition.progress;
  return { run_id: runId, lifecycle_state: definition.lifecycle, progress: { denominator_state: "known" as const, population_revision: "1", total_units: { availability: "available" as const, value: String(value.total) }, completed_units: String(value.completed), reused_units: String(value.reused), queued_units: String(value.queued), running_units: String(value.running), failed_units: String(value.failed), skipped_units: String(value.skipped), unsupported_units: String(value.unsupported), limited_units: String(value.limited), invalidated_units: String(value.invalidated), gap_units: String(value.gap) }, cost: { reserved_nano_usd: unavailable, calculated_actual_nano_usd: unavailable, provider_input_tokens: unavailable, provider_output_tokens: unavailable, provider_reasoning_tokens: unavailable, provider_dispatch_count: unavailable, provider_tool_call_count: unavailable, has_unresolved_hold: false }, projection_version: "projection_0001", durable_event_sequence: eventSequence, observed_at: "2026-08-27T12:00:00.0000000Z" };
}

function encodeCursor(offset: number): string { return offset.toString(16).padStart(16, "0"); }

function decodeCursor(cursor: string | undefined): number {
  if (cursor === undefined) return 0;
  if (!/^[0-9a-f]{16}$/u.test(cursor)) throw new Error("The story cursor is malformed.");
  return Number.parseInt(cursor, 16);
}
