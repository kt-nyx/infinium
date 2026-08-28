import { assertDecimalInt64, assertDecimalUInt64, assertOpaqueCursor, assertOpaqueProductIdentity, decodeOutcome } from "./client.js";
import {
  lifecycleStates,
  registeredMessages,
  type LifecycleState,
  type MessageKind,
  type OutcomeName,
  type RendererOperation,
  type ResultItemKind,
} from "./generated/renderer-contract.generated.js";
import { validateRendererSchema } from "./schema-validator.js";

const capabilities = ["bootstrap", "run-query", "event-resync", "configuration", "provider-enrollment", "result-exploration", "durable-user-review"] as const;
const availabilities = ["available", "partial", "unavailable"] as const;
const scalarAvailabilities = ["available", "unavailable", "unsupported", "unknown"] as const;
const resultKinds = ["supported-case", "lead-only-case", "finding", "abstention", "failure", "coverage-gap"] as const;
const resultSorts = ["identity-ascending", "severity-descending-identity-ascending"] as const;
const denominatorStates = ["known", "enumerating", "unavailable", "unknown", "unsupported"] as const;
const eventKinds = ["progress", "lifecycle-changed", "projection-invalidated", "resync-required"] as const;
const resyncReasons = ["slow-client", "queue-overflow", "sequence-gap", "coordinator-restart", "replay-window-expired", "projection-rebuilt", "cursor-invalid"] as const;
const messageKinds = ["request", "response", "event"] as const;

type ClosedValueCatalog = {
  readonly capability: (typeof capabilities)[number];
  readonly availability: (typeof availabilities)[number];
  readonly scalarAvailability: (typeof scalarAvailabilities)[number];
  readonly lifecycle: LifecycleState;
  readonly resultKind: ResultItemKind;
  readonly resultSort: (typeof resultSorts)[number];
  readonly denominator: (typeof denominatorStates)[number];
  readonly eventKind: (typeof eventKinds)[number];
  readonly resyncReason: (typeof resyncReasons)[number];
  readonly messageKind: MessageKind;
  readonly outcome: OutcomeName;
};

const catalogs: { readonly [K in Exclude<keyof ClosedValueCatalog, "outcome">]: readonly ClosedValueCatalog[K][] } = {
  capability: capabilities,
  availability: availabilities,
  scalarAvailability: scalarAvailabilities,
  lifecycle: lifecycleStates,
  resultKind: resultKinds,
  resultSort: resultSorts,
  denominator: denominatorStates,
  eventKind: eventKinds,
  resyncReason: resyncReasons,
  messageKind: messageKinds,
};

export function decodeClosedValue<TKey extends keyof ClosedValueCatalog>(key: TKey, value: unknown): ClosedValueCatalog[TKey] {
  if (key === "outcome") return decodeOutcome(value) as ClosedValueCatalog[TKey];
  const catalog = (catalogs as Readonly<Record<string, readonly string[]>>)[key] ?? [];
  if (typeof value !== "string" || !catalog.includes(value)) throw new Error(`The renderer returned an unknown ${key} value.`);
  return value as ClosedValueCatalog[TKey];
}

export interface DecodedMessageHeader {
  readonly operation: RendererOperation;
  readonly messageKind: MessageKind;
}

export function validateParsedRendererMessage(value: unknown): DecodedMessageHeader {
  validateRendererSchema(value);
  const envelope = object(value, "renderer envelope");
  const operation = string(envelope.operation, "operation") as RendererOperation;
  const messageKind = decodeClosedValue("messageKind", envelope.message_kind);
  if (!registeredMessages.some((entry) => entry.operation === operation && entry.messageKind === messageKind)) throw new Error("The renderer operation/message pair is not registered.");
  assertDecimalUInt64(string(envelope.sequence, "sequence"));
  const payload = object(envelope.payload, "payload");
  if (messageKind !== "request") {
    const outcome = decodeClosedValue("outcome", payload.outcome);
    if (outcome !== "accepted") {
      const error = object(payload.error, "error");
      const code = string(error.code, "error.code");
      string(error.inert_detail, "error.inert_detail");
      boolean(error.retry_may_be_safe, "error.retry_may_be_safe");
      const expectedCode = outcome === "rejected" ? null : outcome;
      if (expectedCode === null) {
        if (["conflict", "unsupported", "unavailable", "cancelled", "indeterminate", "resync-required"].includes(code)) throw new Error("A rejected response carries a mismatched error code.");
      } else if (code !== expectedCode) throw new Error("A renderer failure carries a mismatched error code.");
      if (outcome === "conflict") {
        const conflict = object(payload.conflict, "conflict");
        string(conflict.expected_revision, "conflict.expected_revision");
        string(conflict.current_revision, "conflict.current_revision");
        const disposition = string(conflict.disposition, "conflict.disposition");
        if (!["stale-revision", "already-applied", "resync-required"].includes(disposition)) throw new Error("The conflict disposition is unknown.");
      }
      if (outcome === "resync-required") string(payload.current_projection_version, "current_projection_version");
    }
  }

  if (operation === "application.bootstrap" && messageKind === "response" && payload.outcome === "accepted") {
    const bootstrap = object(payload.bootstrap, "bootstrap");
    for (const item of array(bootstrap.capabilities, "capabilities")) {
      const capability = object(item, "capability");
      decodeClosedValue("capability", capability.capability);
      decodeClosedValue("availability", capability.availability);
    }
    for (const item of array(bootstrap.recent_runs, "recent_runs")) {
      const run = object(item, "run summary");
      decodeClosedValue("lifecycle", run.lifecycle_state);
      assertDecimalUInt64(string(run.lifecycle_generation, "lifecycle_generation"));
    }
    assertDecimalUInt64(string(bootstrap.coordinator_fencing_epoch, "coordinator_fencing_epoch"));
  }
  if (operation === "results.list" && messageKind === "request") {
    assertOpaqueProductIdentity(string(payload.run_id, "run_id"));
    if (payload.after_cursor !== undefined) assertOpaqueCursor(string(payload.after_cursor, "after_cursor"));
    for (const kind of array(payload.kinds, "kinds")) decodeClosedValue("resultKind", kind);
    decodeClosedValue("resultSort", payload.sort);
  }
  if (operation === "results.list" && messageKind === "response" && payload.outcome === "accepted") {
    const page = object(payload.page, "result page");
    const items = array(page.items, "result items");
    if (items.length > 100) throw new Error("The result page exceeds its item bound.");
    for (const item of items) validateResultSummary(item);
    const hasMore = boolean(page.has_more, "has_more");
    if (hasMore !== (page.next_cursor !== undefined)) throw new Error("The result page cursor and has-more state are inconsistent.");
    if (page.next_cursor !== undefined) assertOpaqueCursor(string(page.next_cursor, "next_cursor"));
  }
  if (operation === "results.detail" && messageKind === "response" && payload.outcome === "accepted") {
    const detail = object(payload.detail, "result detail");
    validateResultSummary(detail.summary);
    assertOpaqueProductIdentity(string(detail.source_payload_id, "source_payload_id"));
    const digest = string(detail.source_payload_sha256, "source_payload_sha256");
    if (!/^[0-9a-f]{64}$/u.test(digest)) throw new Error("The source payload fingerprint is invalid.");
  }
  if (operation === "progress.read" && payload.outcome === "accepted") validateProgressProjection(payload.progress);
  if (operation === "progress.subscribe" && messageKind === "event") {
    const eventKind = decodeClosedValue("eventKind", payload.event_kind);
    validateEventMetadata(payload.metadata);
    if (eventKind === "progress") validateProgressProjection(payload.progress);
    else if (eventKind === "lifecycle-changed") {
      const transition = object(payload.lifecycle_changed, "lifecycle_changed");
      decodeClosedValue("lifecycle", transition.previous_state);
      decodeClosedValue("lifecycle", transition.current_state);
      assertDecimalUInt64(string(transition.lifecycle_generation, "lifecycle_generation"));
      const recordKind = string(transition.transition_record_kind, "transition_record_kind");
      if (recordKind !== "requested" && recordKind !== "observed") throw new Error("The lifecycle transition record kind is unknown.");
    } else {
      decodeClosedValue("resyncReason", payload.reason);
      string(payload.current_projection_version, "current_projection_version");
    }
  }
  return { operation, messageKind };
}

function validateProgressProjection(value: unknown): void {
  const progress = object(value, "progress");
  decodeClosedValue("lifecycle", progress.lifecycle_state);
  assertDecimalUInt64(string(progress.durable_event_sequence, "durable_event_sequence"));
  const summary = object(progress.progress, "progress summary");
  decodeClosedValue("denominator", summary.denominator_state);
  assertDecimalUInt64(string(summary.population_revision, "population_revision"));
  const total = object(summary.total_units, "total_units");
  const availability = decodeClosedValue("scalarAvailability", total.availability);
  if (availability === "available") assertDecimalUInt64(string(total.value, "total_units.value"));
  for (const field of ["completed_units", "reused_units", "queued_units", "running_units", "failed_units", "skipped_units", "unsupported_units", "limited_units", "invalidated_units", "gap_units"] as const) assertDecimalUInt64(string(summary[field], field));
  const cost = object(progress.cost, "cost summary");
  for (const field of ["reserved_nano_usd", "calculated_actual_nano_usd", "provider_input_tokens", "provider_output_tokens", "provider_reasoning_tokens", "provider_dispatch_count", "provider_tool_call_count"] as const) {
    const scalar = object(cost[field], field);
    const scalarAvailability = decodeClosedValue("scalarAvailability", scalar.availability);
    if (scalarAvailability === "available") {
      const decimal = string(scalar.value, `${field}.value`);
      if (field === "reserved_nano_usd" || field === "calculated_actual_nano_usd") assertDecimalInt64(decimal);
      else assertDecimalUInt64(decimal);
    }
  }
}

function validateResultSummary(value: unknown): void {
  const item = object(value, "result item");
  decodeClosedValue("resultKind", item.kind);
  for (const field of ["item_id", "run_id", "logical_id"] as const) assertOpaqueProductIdentity(string(item[field], field));
  if (item.case_occurrence_id !== undefined) assertOpaqueProductIdentity(string(item.case_occurrence_id, "case_occurrence_id"));
}

function validateEventMetadata(value: unknown): void {
  const metadata = object(value, "event metadata");
  assertOpaqueProductIdentity(string(metadata.coordinator_instance_id, "coordinator_instance_id"));
  assertDecimalUInt64(string(metadata.coordinator_fencing_epoch, "coordinator_fencing_epoch"));
  assertDecimalUInt64(string(metadata.durable_event_sequence, "durable_event_sequence"));
  assertOpaqueProductIdentity(string(metadata.run_scope, "run_scope"));
  assertOpaqueCursor(string(metadata.resume_cursor, "resume_cursor"));
}

function object(value: unknown, label: string): Readonly<Record<string, unknown>> {
  if (typeof value !== "object" || value === null || Array.isArray(value)) throw new Error(`The renderer ${label} is not an object.`);
  return value as Readonly<Record<string, unknown>>;
}

function array(value: unknown, label: string): readonly unknown[] {
  if (!Array.isArray(value)) throw new Error(`The renderer ${label} is not an array.`);
  return value;
}

function string(value: unknown, label: string): string {
  if (typeof value !== "string") throw new Error(`The renderer ${label} is not a string.`);
  return value;
}

function boolean(value: unknown, label: string): boolean {
  if (typeof value !== "boolean") throw new Error(`The renderer ${label} is not a boolean.`);
  return value;
}
