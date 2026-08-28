import { assertClosedOperation, assertDecimalInt64, assertDecimalUInt64, assertOpaqueCursor, assertOpaqueProductIdentity, bindRendererSession, decodeOutcome, GeneratedBridgeApplicationClient, selectApplicationClient, validateBootstrapRequest, validateCancelRequest, validateProgressRequest, validateProgressSubscriptionRequest, validateResultDetailRequest, validateResultListRequest, type ApplicationClient, type ClosedRendererBridge, type EventEnvelope, type RequestEnvelope, type ResponseEnvelope } from "./client.js";
import { assertRendererOperationPartitions, assertRendererResponseHandlerCoverage, deniedAuthorityFields, dispatchRendererResponseBinding, dispatchRendererResponseOperation, lifecycleStates, registeredMessages, rendererContractVersion, rendererRegistrySha256, rendererRegistryVersion, responseOperations, type BootstrapRequest, type BootstrapResponse, type CancelResponse, type FailurePayload, type LifecycleState, type ProgressEvent, type ProgressProjection, type ProgressRequest, type ProgressResponse, type ProgressSubscriptionRequest, type RendererResponseBindingHandlers, type RendererResponseOperationHandlers, type ResponseOperation, type ResultDetailRequest, type ResultDetailResponse, type ResultItemKind, type ResultListRequest, type ResultListResponse, type ResyncEvent } from "./generated/renderer-contract.generated.js";
import { StoryApplicationClient, storyNames, storyRunId, type StoryName } from "./stories.js";
import { decodeClosedValue, validateParsedRendererMessage } from "./decoders.js";
import { parseAndValidateRendererJson } from "./schema-validator.js";
import { BoundedResultPager, virtualRowWindow } from "./bounded-result-pager.js";

function assert(condition: boolean, message: string): asserts condition { if (!condition) throw new Error(message); }
async function rejects(action: () => unknown | Promise<unknown>): Promise<void> { try { await action(); } catch { return; } throw new Error("Expected rejection."); }
function captureFailure(action: () => unknown): string {
  try { action(); } catch (failure) { return failure instanceof Error ? failure.message : String(failure); }
  throw new Error("Expected a synchronous failure.");
}

let hostSessionCounter = 0;
function hostBinding() {
  hostSessionCounter += 1;
  const sessionId = `host_session_${hostSessionCounter.toString().padStart(8, "0")}`;
  return bindRendererSession(JSON.stringify({
    contract_version: rendererContractVersion,
    message_kind: "event",
    session_id: sessionId,
    sequence: "1",
    operation: "transport.session.establish",
    payload: { outcome: "accepted", origin: "https://app.infinium.invalid", renderer_contract_version: rendererContractVersion, renderer_registry_version: rendererRegistryVersion, renderer_registry_sha256: rendererRegistrySha256 },
  }));
}

type BridgeFault = "response-session" | "response-version" | "response-sequence" | "response-request" | "response-operation" | "response-revision" | "response-oversize" | "event-session" | "event-version" | "event-sequence" | "event-forward-gap" | "event-forward-gap-recovery" | "event-subscription" | "event-operation" | "event-revision" | "event-progress-run" | "event-oversize" | "event-replay" | "result-run" | "result-kind";

class StoryBridge implements ClosedRendererBridge {
  private readonly handlers: RendererResponseOperationHandlers;
  private hostEventSequence = 1n;
  public resultListRequestCount = 0;
  public maximumTransferredResultItems = 0;

  public constructor(private readonly source: ApplicationClient, private readonly fault?: BridgeFault) {
    this.handlers = {
      "application.bootstrap": (payload) => this.source.bootstrap(payload),
      "results.list": (payload) => this.source.listResultItems(payload),
      "results.detail": (payload) => this.source.getResultDetail(payload),
      "progress.read": (payload) => this.source.getProgress(payload),
      "application.cancel": (payload, gestureId) => this.source.cancel(payload.target_request_id, gestureId ?? ""),
    };
  }

  public async request(serializedEnvelope: string): Promise<string> {
    const parsed = parseAndValidateRendererJson(serializedEnvelope);
    const header = validateParsedRendererMessage(parsed);
    if (header.messageKind !== "request" || !responseOperations.some((operation) => operation === header.operation)) throw new Error("The bridge received an operation without a response mapping.");
    const envelope = parsed as RequestEnvelope<ResponseOperation>;
    const payload = await dispatchRendererResponseOperation(this.handlers, envelope.operation, envelope.payload, envelope.gesture_proof?.gesture_id);
    const resultPayload = payload as ResultListResponse;
    if (envelope.operation === "results.list" && resultPayload.outcome === "accepted") {
      this.resultListRequestCount += 1;
      this.maximumTransferredResultItems = Math.max(this.maximumTransferredResultItems, resultPayload.page.items.length);
    }
    const revision = dispatchRendererResponseBinding(bridgeRevisionBindings, envelope.operation, envelope.payload, payload);
    const response = {
      contract_version: rendererContractVersion,
      message_kind: "response",
      session_id: envelope.session_id,
      sequence: envelope.sequence,
      request_id: envelope.request_id,
      ...(revision === undefined ? {} : { revision }),
      operation: envelope.operation,
      payload,
    } as unknown as ResponseEnvelope<ResponseOperation>;
    return mutateSerializedEnvelope(JSON.stringify(response), this.fault, false);
  }

  public subscribe(serializedEnvelope: string, listener: (serializedEvent: string) => void): () => void {
    const parsed = parseAndValidateRendererJson(serializedEnvelope);
    const header = validateParsedRendererMessage(parsed);
    if (header.messageKind !== "request" || header.operation !== "progress.subscribe") throw new Error("The bridge received an invalid subscription request.");
    const envelope = parsed as RequestEnvelope<"progress.subscribe">;
    return this.source.subscribeProgress(envelope.payload, (payload) => {
      this.hostEventSequence += 1n;
      const common = { contract_version: rendererContractVersion, message_kind: "event" as const, session_id: envelope.session_id, sequence: this.hostEventSequence.toString(), subscription_id: envelope.payload.subscription_id };
      const event: EventEnvelope<"progress.subscribe"> | EventEnvelope<"application.resync-required"> = "metadata" in payload
        ? { ...common, revision: payload.metadata.projection_version, operation: "progress.subscribe", payload }
        : { ...common, revision: payload.current_projection_version, operation: "application.resync-required", payload };
      const wire = mutateSerializedEnvelope(JSON.stringify(event), this.fault, true);
      if (this.fault === "event-forward-gap-recovery") {
        try { listener(wire); } catch { listener(JSON.stringify(event)); }
        return;
      }
      listener(wire);
      if (this.fault === "event-replay") listener(wire);
    });
  }
}

const bridgeRevisionBindings: RendererResponseBindingHandlers<string | undefined> = {
  "application.bootstrap": (_request, response) => response.outcome === "accepted" ? response.bootstrap.projection_version : responseFailureRevision(response),
  "results.list": (_request, response) => response.outcome === "accepted" ? response.page.projection_version : responseFailureRevision(response),
  "results.detail": (_request, response) => response.outcome === "accepted" ? response.detail.projection_version : responseFailureRevision(response),
  "progress.read": (_request, response) => response.outcome === "accepted" ? response.progress.projection_version : responseFailureRevision(response),
  "application.cancel": (_request, response) => responseFailureRevision(response),
};

function responseFailureRevision(response: { readonly outcome: string; readonly conflict?: { readonly current_revision: string }; readonly current_projection_version?: string }): string | undefined {
  return response.outcome === "conflict" ? response.conflict?.current_revision
    : response.outcome === "resync-required" ? response.current_projection_version
      : undefined;
}

type MutableJson = Record<string, unknown>;
function mutableObject(value: unknown, label: string): MutableJson {
  if (typeof value !== "object" || value === null || Array.isArray(value)) throw new Error(`The ${label} is not an object.`);
  return value as MutableJson;
}

function mutateSerializedEnvelope(serialized: string, fault: BridgeFault | undefined, event: boolean): string {
  if (fault === (event ? "event-oversize" : "response-oversize")) return "x".repeat(1_048_577);
  if (fault === undefined || fault === "event-replay") return serialized;
  const value = mutableObject(JSON.parse(serialized), "wire envelope");
  if (!event) {
    if (fault === "response-session") value.session_id = "substituted_session";
    else if (fault === "response-version") value.contract_version = "9.9.9";
    else if (fault === "response-sequence") value.sequence = (BigInt(String(value.sequence)) + 1n).toString();
    else if (fault === "response-request") value.request_id = "substituted_request";
    else if (fault === "response-operation") value.operation = value.operation === "application.bootstrap" ? "progress.read" : "application.bootstrap";
    else if (fault === "response-revision") value.revision = "substituted_revision";
    else if (fault === "result-run" || fault === "result-kind") {
      const payload = mutableObject(value.payload, "result payload");
      const page = mutableObject(payload.page, "result page");
      const items = page.items;
      if (!Array.isArray(items) || items.length === 0) throw new Error("The result mutation requires an item.");
      const item = mutableObject(items[0], "result item");
      if (fault === "result-run") item.run_id = "different_run_identity";
      else item.kind = "failure";
    }
  } else {
    if (fault === "event-session") value.session_id = "substituted_session";
    else if (fault === "event-version") value.contract_version = "9.9.9";
    else if (fault === "event-sequence") value.sequence = "1";
    else if (fault === "event-forward-gap" || fault === "event-forward-gap-recovery") value.sequence = (BigInt(String(value.sequence)) + 1n).toString();
    else if (fault === "event-subscription") value.subscription_id = "substituted_subscription";
    else if (fault === "event-operation") value.operation = "application.resync-required";
    else if (fault === "event-revision") value.revision = "substituted_revision";
    else if (fault === "event-progress-run") {
      const payload = mutableObject(value.payload, "event payload");
      mutableObject(payload.progress, "event progress").run_id = "different_run_identity";
    }
  }
  return JSON.stringify(value);
}

interface StoryExpectation {
  readonly configuration: "available" | "unavailable";
  readonly listOutcome: "accepted" | "conflict" | "resync-required";
  readonly firstPageItems: number;
  readonly lifecycle: LifecycleState;
  readonly detailKind: ResultItemKind;
  readonly detailOutcome: "accepted" | "rejected";
  readonly eventOutcome: "accepted" | "resync-required";
  readonly progress: ProgressExpectation;
}

interface ProgressExpectation {
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

const expectedProgress = (values: Partial<ProgressExpectation> & Pick<ProgressExpectation, "total">): ProgressExpectation => ({
  completed: 0, reused: 0, queued: 0, running: 0, failed: 0, skipped: 0, unsupported: 0, limited: 0, invalidated: 0, gap: 0, ...values,
});

const storyExpectations: Readonly<Record<StoryName, StoryExpectation>> = {
  setup: { configuration: "unavailable", listOutcome: "accepted", firstPageItems: 0, lifecycle: "queued", detailKind: "abstention", detailOutcome: "rejected", eventOutcome: "accepted", progress: expectedProgress({ total: 1, queued: 1 }) },
  empty: { configuration: "available", listOutcome: "accepted", firstPageItems: 0, lifecycle: "completed", detailKind: "supported-case", detailOutcome: "rejected", eventOutcome: "accepted", progress: expectedProgress({ total: 0 }) },
  active: { configuration: "available", listOutcome: "accepted", firstPageItems: 6, lifecycle: "running", detailKind: "finding", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 6, completed: 2, running: 1, queued: 3 }) },
  completed: { configuration: "available", listOutcome: "accepted", firstPageItems: 8, lifecycle: "completed", detailKind: "supported-case", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 8, completed: 8 }) },
  failed: { configuration: "available", listOutcome: "accepted", firstPageItems: 1, lifecycle: "failed", detailKind: "failure", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 1, failed: 1 }) },
  gap: { configuration: "available", listOutcome: "accepted", firstPageItems: 3, lifecycle: "completed-with-gaps", detailKind: "coverage-gap", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 3, gap: 3 }) },
  "lead-only": { configuration: "available", listOutcome: "accepted", firstPageItems: 4, lifecycle: "completed", detailKind: "lead-only-case", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 4, completed: 4 }) },
  stale: { configuration: "available", listOutcome: "resync-required", firstPageItems: 0, lifecycle: "completed", detailKind: "finding", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 2, completed: 2 }) },
  conflict: { configuration: "available", listOutcome: "conflict", firstPageItems: 0, lifecycle: "completed", detailKind: "finding", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 2, completed: 2 }) },
  reconnect: { configuration: "available", listOutcome: "accepted", firstPageItems: 5, lifecycle: "running", detailKind: "finding", detailOutcome: "accepted", eventOutcome: "resync-required", progress: expectedProgress({ total: 5, completed: 2, running: 1, queued: 2 }) },
  "large-pagination": { configuration: "available", listOutcome: "accepted", firstPageItems: 100, lifecycle: "completed", detailKind: "finding", detailOutcome: "accepted", eventOutcome: "accepted", progress: expectedProgress({ total: 100_000, completed: 100_000 }) },
};

function assertProgress(projection: ProgressProjection, expectation: ProgressExpectation, story: StoryName): void {
  const value = projection.progress;
  assert(value.denominator_state === "known", `Progress denominator state changed for ${story}.`);
  assert(value.population_revision === "1", `Progress population revision changed for ${story}.`);
  assert(value.total_units.availability === "available", `Progress total availability changed for ${story}.`);
  const actual = {
    total: Number(value.total_units.value),
    completed: Number(value.completed_units),
    reused: Number(value.reused_units),
    queued: Number(value.queued_units),
    running: Number(value.running_units),
    failed: Number(value.failed_units),
    skipped: Number(value.skipped_units),
    unsupported: Number(value.unsupported_units),
    limited: Number(value.limited_units),
    invalidated: Number(value.invalidated_units),
    gap: Number(value.gap_units),
  };
  for (const key of ["total", "completed", "reused", "queued", "running", "failed", "skipped", "unsupported", "limited", "invalidated", "gap"] as const) {
    assert(actual[key] === expectation[key], `Progress ${key} counter changed for ${story}.`);
  }
  const mutuallyExclusiveSum = actual.completed + actual.reused + actual.queued + actual.running + actual.failed
    + actual.skipped + actual.unsupported + actual.limited + actual.invalidated + actual.gap;
  assert(mutuallyExclusiveSum === actual.total, `Progress counters do not sum to the declared total for ${story}.`);
}

type NonSuccessOutcome = FailurePayload["outcome"];
const nonSuccessPayloads: { readonly [K in NonSuccessOutcome]: Extract<FailurePayload, { readonly outcome: K }> } = {
  rejected: { outcome: "rejected", error: { code: "invalid-argument", inert_detail: "Rejected.", retry_may_be_safe: false } },
  conflict: { outcome: "conflict", error: { code: "conflict", inert_detail: "Conflict.", retry_may_be_safe: false }, conflict: { expected_revision: "projection_0001", current_revision: "projection_0002", disposition: "stale-revision" } },
  unsupported: { outcome: "unsupported", error: { code: "unsupported", inert_detail: "Unsupported.", retry_may_be_safe: false } },
  unavailable: { outcome: "unavailable", error: { code: "unavailable", inert_detail: "Unavailable.", retry_may_be_safe: true } },
  cancelled: { outcome: "cancelled", error: { code: "cancelled", inert_detail: "Cancelled.", retry_may_be_safe: true } },
  indeterminate: { outcome: "indeterminate", error: { code: "indeterminate", inert_detail: "Indeterminate.", retry_may_be_safe: false } },
  "resync-required": { outcome: "resync-required", error: { code: "resync-required", inert_detail: "Resync required.", retry_may_be_safe: false }, current_projection_version: "projection_0002" },
};

class NonSuccessApplicationClient implements ApplicationClient {
  public constructor(private readonly payload: FailurePayload) {}
  public async bootstrap(request: BootstrapRequest): Promise<BootstrapResponse> { validateBootstrapRequest(request); return this.payload; }
  public async listResultItems(request: ResultListRequest): Promise<ResultListResponse> { validateResultListRequest(request); return this.payload; }
  public async getResultDetail(request: ResultDetailRequest): Promise<ResultDetailResponse> { validateResultDetailRequest(request); return this.payload; }
  public async getProgress(request: ProgressRequest): Promise<ProgressResponse> { validateProgressRequest(request); return this.payload; }
  public subscribeProgress(request: ProgressSubscriptionRequest, listener: (event: ProgressEvent | ResyncEvent) => void): () => void {
    validateProgressSubscriptionRequest(request);
    if (this.payload.outcome === "resync-required") listener(this.payload);
    return () => undefined;
  }
  public async cancel(targetRequestId: string, gestureId: string): Promise<CancelResponse> { validateCancelRequest(targetRequestId, gestureId); return this.payload; }
}

async function run(): Promise<void> {
  assert(registeredMessages.length === 16, "Every closed message must be generated.");
  assert(new Set(registeredMessages.map((value) => `${value.operation}:${value.messageKind}`)).size === 16, "Generated operation/message keys must be unique.");
  assertRendererOperationPartitions(registeredMessages);
  await rejects(() => assertRendererOperationPartitions(registeredMessages.slice(1)));
  assertRendererResponseHandlerCoverage(bridgeRevisionBindings);
  const missingHandler = { ...bridgeRevisionBindings } as MutableJson;
  delete missingHandler["progress.read"];
  await rejects(() => assertRendererResponseHandlerCoverage(missingHandler));
  assert(deniedAuthorityFields.length === 9, "Denied authority fields drifted.");
  assert(JSON.stringify(lifecycleStates) === JSON.stringify(["queued", "running", "waiting", "retrying", "pausing", "paused", "cancelling", "cancelled", "completed", "completed-with-gaps", "failed", "limit-reached", "invalidated-by-changed-input"]), "Lifecycle enum diverged from the native closed projection.");
  for (const operation of new Set(registeredMessages.map((value) => value.operation))) assert(assertClosedOperation(operation) === operation, "Operation decode changed meaning.");
  await rejects(() => assertClosedOperation("generic.invoke"));
  for (const outcome of ["accepted", "rejected", "conflict", "unsupported", "unavailable", "cancelled", "indeterminate", "resync-required"]) assert(decodeOutcome(outcome) === outcome, "Outcome decode changed meaning.");
  await rejects(() => decodeOutcome("unknown"));
  const typedConflict: FailurePayload = { outcome: "conflict", error: { code: "conflict", inert_detail: "Conflict.", retry_may_be_safe: false }, conflict: { expected_revision: "1", current_revision: "2", disposition: "stale-revision" } };
  assert(typedConflict.conflict.current_revision === "2", "Typed conflict lost required metadata.");
  await rejects(() => validateParsedRendererMessage({ contract_version: rendererContractVersion, message_kind: "response", session_id: "host_session_0001", sequence: "1", request_id: "request_identity_0001", operation: "application.bootstrap", payload: { outcome: "conflict", error: { code: "unsupported", inert_detail: "Mismatched.", retry_may_be_safe: false } } }));
  for (const [key, known] of [
    ["capability", "result-exploration"], ["availability", "partial"], ["scalarAvailability", "unsupported"],
    ["lifecycle", "invalidated-by-changed-input"], ["resultKind", "coverage-gap"], ["resultSort", "severity-descending-identity-ascending"],
    ["denominator", "enumerating"], ["eventKind", "projection-invalidated"], ["messageKind", "event"],
  ] as const) {
    assert(decodeClosedValue(key, known) === known, `Closed ${key} decoder rejected a native value.`);
    await rejects(() => decodeClosedValue(key, "future-unknown-value"));
  }
  assert(assertDecimalUInt64("18446744073709551615") === "18446744073709551615", "UInt64 maximum lost precision.");
  await rejects(() => assertDecimalUInt64("18446744073709551616"));
  await rejects(() => assertDecimalUInt64("01"));
  assert(assertDecimalInt64("-9223372036854775808") === "-9223372036854775808", "Int64 minimum lost precision.");
  assert(assertDecimalInt64("9223372036854775807") === "9223372036854775807", "Int64 maximum lost precision.");
  await rejects(() => assertDecimalInt64("-9223372036854775809"));
  await rejects(() => assertDecimalInt64("9223372036854775808"));
  await rejects(() => assertDecimalInt64("-0"));
  assert(assertOpaqueProductIdentity("opaque product identity: 1") === "opaque product identity: 1", "Valid opaque product identity was rejected.");
  await rejects(() => assertOpaqueProductIdentity("x".repeat(161)));
  await rejects(() => assertOpaqueProductIdentity("🙂".repeat(41)));
  await rejects(() => assertOpaqueProductIdentity("invalid\0identity"));
  assert(assertOpaqueCursor("A".repeat(10_923)).length === 10_923, "Maximum cursor was rejected.");
  await rejects(() => assertOpaqueCursor("A".repeat(10_924)));
  await rejects(() => assertOpaqueCursor("A"));
  await rejects(() => assertOpaqueCursor("AB"));
  await rejects(() => parseAndValidateRendererJson("x".repeat(1_048_577)));

  assert(storyNames.length === 11 && new Set(storyNames).size === 11, "Story-state coverage is incomplete.");
  for (const name of storyNames) {
    const expectation = storyExpectations[name];
    const fake = new StoryApplicationClient(name);
    const generatedBridge = new GeneratedBridgeApplicationClient(new StoryBridge(new StoryApplicationClient(name)), hostBinding());
    const request: ResultListRequest = { run_id: storyRunId, requested_page_size: 100, kinds: ["supported-case", "lead-only-case", "finding", "abstention", "failure", "coverage-gap"], sort: "identity-ascending", search_text: "" };
    const bootstrapRequest: BootstrapRequest = { maximum_recent_runs: 20 };
    const fakeBootstrap = await fake.bootstrap(bootstrapRequest);
    const bridgeBootstrap = await generatedBridge.bootstrap(bootstrapRequest);
    assert(JSON.stringify(fakeBootstrap) === JSON.stringify(bridgeBootstrap), `Bootstrap meaning drifted for ${name}.`);
    assert(fakeBootstrap.outcome === "accepted" && fakeBootstrap.bootstrap.configuration_availability === expectation.configuration, `Bootstrap story semantics changed for ${name}.`);
    const fakeResult = await selectApplicationClient("fake", { fake, generatedBridge }).listResultItems(request);
    const bridgeResult = await selectApplicationClient("generated-bridge", { fake, generatedBridge }).listResultItems(request);
    assert(JSON.stringify(fakeResult) === JSON.stringify(bridgeResult), `Fake/generated-bridge list meaning drifted for ${name}.`);
    assert(fakeResult.outcome === expectation.listOutcome, `List outcome changed for ${name}.`);
    if (fakeResult.outcome === "accepted") {
      assert(fakeResult.page.items.length === expectation.firstPageItems, `List item count changed for ${name}.`);
      assert(fakeResult.page.items.every((item) => item.kind === expectation.detailKind), `List item kind changed for ${name}.`);
    }
    const detailRequest: ResultDetailRequest = { run_id: request.run_id, item_id: "result_item_000000000001", kind: expectation.detailKind };
    const fakeDetail = await fake.getResultDetail(detailRequest);
    const bridgeDetail = await generatedBridge.getResultDetail(detailRequest);
    assert(JSON.stringify(fakeDetail) === JSON.stringify(bridgeDetail), `Fake/generated-bridge detail meaning drifted for ${name}.`);
    assert(fakeDetail.outcome === expectation.detailOutcome, `Detail outcome changed for ${name}.`);
    if (fakeDetail.outcome === "accepted") {
      assert(fakeDetail.detail.summary.item_id === detailRequest.item_id
        && fakeDetail.detail.summary.run_id === detailRequest.run_id
        && fakeDetail.detail.summary.kind === detailRequest.kind, `Accepted detail identity changed for ${name}.`);
    } else {
      assert(fakeDetail.error.code === "not-found", `Absent story detail did not return typed not-found for ${name}.`);
    }
    const progressRequest: ProgressRequest = { run_id: request.run_id };
    const fakeProgress = await fake.getProgress(progressRequest);
    const bridgeProgress = await generatedBridge.getProgress(progressRequest);
    assert(JSON.stringify(fakeProgress) === JSON.stringify(bridgeProgress), `Fake/generated-bridge progress meaning drifted for ${name}.`);
    assert(fakeProgress.outcome === "accepted" && fakeProgress.progress.lifecycle_state === expectation.lifecycle, `Progress lifecycle changed for ${name}.`);
    assertProgress(fakeProgress.progress, expectation.progress, name);
    const fakeCancellation = await fake.cancel("renderer_request_00000001", "gesture_identity_0001");
    const bridgeCancellation = await generatedBridge.cancel("renderer_request_00000001", "gesture_identity_0001");
    assert(JSON.stringify(fakeCancellation) === JSON.stringify(bridgeCancellation) && fakeCancellation.outcome === "accepted", `Fake/generated-bridge cancellation meaning drifted for ${name}.`);
    const storySubscription: ProgressSubscriptionRequest = { subscription_id: "subscription_00001", run_id: request.run_id, requested_queue_items: 64 };
    let fakeEvent: unknown;
    let realEvent: unknown;
    fake.subscribeProgress(storySubscription, (event) => { fakeEvent = event; });
    generatedBridge.subscribeProgress(storySubscription, (event) => { realEvent = event; });
    assert(JSON.stringify(fakeEvent) === JSON.stringify(realEvent), `Fake/generated-bridge event meaning drifted for ${name}.`);
    assert(typeof fakeEvent === "object" && fakeEvent !== null && "outcome" in fakeEvent && fakeEvent.outcome === expectation.eventOutcome, `Event semantics changed for ${name}.`);
    if (typeof fakeEvent === "object" && fakeEvent !== null && "outcome" in fakeEvent && fakeEvent.outcome === "accepted" && "event_kind" in fakeEvent && fakeEvent.event_kind === "progress" && "progress" in fakeEvent) {
      assertProgress(fakeEvent.progress as ProgressProjection, expectation.progress, name);
    }
  }
  const validationFake = new StoryApplicationClient("active");
  const validationReal = new GeneratedBridgeApplicationClient(new StoryBridge(validationFake), hostBinding());
  for (const mode of ["fake", "generated-bridge"] as const) {
    const client = selectApplicationClient(mode, { fake: validationFake, generatedBridge: validationReal });
    await rejects(() => client.bootstrap({ maximum_recent_runs: 0 }));
    await rejects(() => client.listResultItems({ run_id: storyRunId, requested_page_size: 101, kinds: ["finding"], sort: "identity-ascending", search_text: "" }));
    await rejects(() => client.listResultItems({ run_id: storyRunId, requested_page_size: 1, kinds: ["finding", "finding"], sort: "identity-ascending", search_text: "" }));
    await rejects(() => client.listResultItems({ run_id: storyRunId, requested_page_size: 1, kinds: ["finding"], sort: "identity-ascending", search_text: "🙂".repeat(41) }));
    await rejects(() => client.listResultItems({ run_id: storyRunId, requested_page_size: 1, kinds: ["finding"], sort: "identity-ascending", search_text: "invalid\0search" }));
    await rejects(() => client.subscribeProgress({ subscription_id: "subscription_00001", run_id: storyRunId, requested_queue_items: 65 }, () => undefined));
  }
  const excludedKindRequest: ResultListRequest = { run_id: storyRunId, requested_page_size: 100, kinds: ["supported-case"], sort: "identity-ascending", search_text: "" };
  const matchingSearchRequest: ResultListRequest = { ...excludedKindRequest, kinds: ["finding"], search_text: "ACTIVE SUMMARY" };
  const missingSearchRequest: ResultListRequest = { ...matchingSearchRequest, search_text: "no story summary contains this" };
  const filteredPairs = [
    [await validationFake.listResultItems(excludedKindRequest), await validationReal.listResultItems(excludedKindRequest)],
    [await validationFake.listResultItems(matchingSearchRequest), await validationReal.listResultItems(matchingSearchRequest)],
    [await validationFake.listResultItems(missingSearchRequest), await validationReal.listResultItems(missingSearchRequest)],
  ] as const;
  for (const [fakeResult, bridgeResult] of filteredPairs) {
    assert(JSON.stringify(fakeResult) === JSON.stringify(bridgeResult), "Story list filtering diverged across fake and generated bridge modes.");
  }
  assert(filteredPairs[0][0].outcome === "accepted" && filteredPairs[0][0].page.items.length === 0, "Story list ignored the requested kind filter.");
  assert(filteredPairs[1][0].outcome === "accepted" && filteredPairs[1][0].page.items.length === 5, "Story list did not apply its case-insensitive inert-summary search.");
  assert(filteredPairs[2][0].outcome === "accepted" && filteredPairs[2][0].page.items.length === 0, "Story list returned items that did not match its search.");
  for (const detailRequest of [
    { run_id: storyRunId, item_id: "result_item_999999999999", kind: "finding" },
    { run_id: storyRunId, item_id: "result_item_000000000001", kind: "failure" },
    { run_id: "different_run_identity", item_id: "result_item_000000000001", kind: "finding" },
  ] as const) {
    const fakeDetail = await validationFake.getResultDetail(detailRequest);
    const bridgeDetail = await validationReal.getResultDetail(detailRequest);
    assert(JSON.stringify(fakeDetail) === JSON.stringify(bridgeDetail), "Absent story detail diverged across fake and generated bridge modes.");
    assert(fakeDetail.outcome === "rejected" && fakeDetail.error.code === "not-found", "Absent story detail did not return typed not-found.");
  }
  const unknownStoryRun = "unknown_story_run_identity";
  const unknownListRequest: ResultListRequest = { ...matchingSearchRequest, run_id: unknownStoryRun };
  const fakeUnknownList = await validationFake.listResultItems(unknownListRequest);
  const bridgeUnknownList = await validationReal.listResultItems(unknownListRequest);
  assert(JSON.stringify(fakeUnknownList) === JSON.stringify(bridgeUnknownList), "Unknown-run list failure diverged across fake and generated bridge modes.");
  assert(fakeUnknownList.outcome === "rejected" && fakeUnknownList.error.code === "not-found", "Unknown-run list did not return typed not-found.");
  const unknownProgressRequest: ProgressRequest = { run_id: unknownStoryRun };
  const fakeUnknownProgress = await validationFake.getProgress(unknownProgressRequest);
  const bridgeUnknownProgress = await validationReal.getProgress(unknownProgressRequest);
  assert(JSON.stringify(fakeUnknownProgress) === JSON.stringify(bridgeUnknownProgress), "Unknown-run progress failure diverged across fake and generated bridge modes.");
  assert(fakeUnknownProgress.outcome === "rejected" && fakeUnknownProgress.error.code === "not-found", "Unknown-run progress did not return typed not-found.");
  const unknownSubscription: ProgressSubscriptionRequest = { subscription_id: "subscription_unknown_run", run_id: unknownStoryRun, requested_queue_items: 64 };
  let fakeUnknownEvent = false;
  let bridgeUnknownEvent = false;
  const fakeUnknownSubscriptionFailure = captureFailure(() => validationFake.subscribeProgress(unknownSubscription, () => { fakeUnknownEvent = true; }));
  const bridgeUnknownSubscriptionFailure = captureFailure(() => validationReal.subscribeProgress(unknownSubscription, () => { bridgeUnknownEvent = true; }));
  assert(fakeUnknownSubscriptionFailure === bridgeUnknownSubscriptionFailure && fakeUnknownSubscriptionFailure === "The requested story run does not exist.", "Unknown-run subscription failure diverged across fake and generated bridge modes.");
  assert(!fakeUnknownEvent && !bridgeUnknownEvent, "Unknown-run subscription fabricated an event.");
  const outcomeListRequest: ResultListRequest = { run_id: storyRunId, requested_page_size: 1, kinds: ["finding"], sort: "identity-ascending", search_text: "" };
  const outcomeDetailRequest: ResultDetailRequest = { run_id: outcomeListRequest.run_id, item_id: "result_item_000000000001", kind: "finding" };
  const outcomeProgressRequest: ProgressRequest = { run_id: outcomeListRequest.run_id };
  for (const outcome of Object.keys(nonSuccessPayloads) as NonSuccessOutcome[]) {
    const fake = new NonSuccessApplicationClient(nonSuccessPayloads[outcome]);
    const generatedBridge = new GeneratedBridgeApplicationClient(new StoryBridge(fake), hostBinding());
    const pairs = [
      [await fake.bootstrap({ maximum_recent_runs: 20 }), await generatedBridge.bootstrap({ maximum_recent_runs: 20 })],
      [await fake.listResultItems(outcomeListRequest), await generatedBridge.listResultItems(outcomeListRequest)],
      [await fake.getResultDetail(outcomeDetailRequest), await generatedBridge.getResultDetail(outcomeDetailRequest)],
      [await fake.getProgress(outcomeProgressRequest), await generatedBridge.getProgress(outcomeProgressRequest)],
      [await fake.cancel("renderer_request_00000001", "gesture_identity_0001"), await generatedBridge.cancel("renderer_request_00000001", "gesture_identity_0001")],
    ] as const;
    for (const [fakeResult, bridgeResult] of pairs) {
      assert(fakeResult.outcome === outcome && bridgeResult.outcome === outcome, `Typed ${outcome} outcome changed meaning.`);
      assert(JSON.stringify(fakeResult) === JSON.stringify(bridgeResult), `Fake/generated-bridge ${outcome} outcome diverged.`);
    }
  }
  const crossRun = new GeneratedBridgeApplicationClient(new StoryBridge(new StoryApplicationClient("active"), "result-run"), hostBinding());
  await rejects(() => crossRun.listResultItems({ run_id: storyRunId, requested_page_size: 1, kinds: ["finding"], sort: "identity-ascending", search_text: "" }));
  const crossKind = new GeneratedBridgeApplicationClient(new StoryBridge(new StoryApplicationClient("active"), "result-kind"), hostBinding());
  await rejects(() => crossKind.listResultItems({ run_id: storyRunId, requested_page_size: 1, kinds: ["finding"], sort: "identity-ascending", search_text: "" }));

  for (const fault of ["response-session", "response-version", "response-sequence", "response-request", "response-operation", "response-revision", "response-oversize"] as const) {
    const hostile = new GeneratedBridgeApplicationClient(new StoryBridge(new StoryApplicationClient("active"), fault), hostBinding());
    await rejects(() => hostile.bootstrap({ maximum_recent_runs: 20 }));
  }
  const hostileCancel = new GeneratedBridgeApplicationClient(new StoryBridge(new StoryApplicationClient("active"), "response-session"), hostBinding());
  await rejects(() => hostileCancel.cancel("renderer_request_00000001", "gesture_identity_0001"));
  const hostileSubscription: ProgressSubscriptionRequest = { subscription_id: "subscription_00001", run_id: storyRunId, requested_queue_items: 64 };
  for (const fault of ["event-session", "event-version", "event-sequence", "event-forward-gap", "event-subscription", "event-operation", "event-revision", "event-progress-run", "event-oversize", "event-replay"] as const) {
    const hostile = new GeneratedBridgeApplicationClient(new StoryBridge(new StoryApplicationClient("active"), fault), hostBinding());
    await rejects(() => hostile.subscribeProgress(hostileSubscription, () => undefined));
  }
  let recoveredEvent = false;
  new GeneratedBridgeApplicationClient(new StoryBridge(new StoryApplicationClient("active"), "event-forward-gap-recovery"), hostBinding())
    .subscribeProgress(hostileSubscription, () => { recoveredEvent = true; });
  assert(recoveredEvent, "A rejected forward host-event gap incorrectly committed sequence state or prevented exact recovery.");
  await rejects(() => bindRendererSession(JSON.stringify({ contract_version: rendererContractVersion, message_kind: "event", session_id: "s".repeat(1_048_577), sequence: "1", operation: "transport.session.establish", payload: { outcome: "accepted", origin: "https://app.infinium.invalid", renderer_contract_version: rendererContractVersion, renderer_registry_version: rendererRegistryVersion, renderer_registry_sha256: rendererRegistrySha256 } })));

  const large = new StoryApplicationClient("large-pagination");
  const largeBridge = new StoryBridge(large);
  const pager = new BoundedResultPager(new GeneratedBridgeApplicationClient(largeBridge, hostBinding()));
  let result: ResultListResponse | null = await pager.reset({ run_id: storyRunId, requested_page_size: 100, kinds: ["finding"], sort: "identity-ascending", search_text: "" });
  while (result !== null && result.outcome === "accepted" && result.page.has_more) {
    const next = await pager.loadNext();
    if (next === null) throw new Error("Large story cursor ended before its declared final page.");
    result = next;
  }
  assert(result?.outcome === "accepted", "Large story unexpectedly failed.");
  assert(pager.observedLogicalCount === 100_000, "Large story did not page exactly 100,000 summaries.");
  assert(pager.cachedSummaryCount <= 500, "Large story exceeded the five-page renderer cache.");
  assert(pager.current[0]?.logicalIndex === 99_900, "Large story did not preserve logical indices after cache eviction.");
  assert(pager.accessibilitySetSize === 100_000, "Large story did not expose the completed logical count.");
  assert(largeBridge.resultListRequestCount === 1_000, "The generated bridge did not issue exactly 1,000 bounded pages for 100,000 summaries.");
  assert(largeBridge.maximumTransferredResultItems === 100, "The generated bridge transferred more than one bounded page.");
  assert(virtualRowWindow(pager.current.map((entry) => entry.item), 0, 13).length === 13, "The logical renderer window mounted more or fewer than 13 rows.");
  const partialSource = new StoryApplicationClient("large-pagination");
  const partialClient: ApplicationClient = {
    bootstrap: (request) => partialSource.bootstrap(request),
    listResultItems: (request) => partialSource.listResultItems({ ...request, requested_page_size: 37 }),
    getResultDetail: (request) => partialSource.getResultDetail(request),
    getProgress: (request) => partialSource.getProgress(request),
    subscribeProgress: (request, listener) => partialSource.subscribeProgress(request, listener),
    cancel: (target, gesture) => partialSource.cancel(target, gesture),
  };
  const partialPager = new BoundedResultPager(partialClient);
  await partialPager.reset({ run_id: storyRunId, requested_page_size: 100, kinds: ["finding"], sort: "identity-ascending", search_text: "" });
  await partialPager.loadNext();
  assert(partialPager.current[0]?.logicalIndex === 37, "A partial page used a fixed-size logical offset.");
  assert(partialPager.movePrevious(), "A cached prior partial page was not available.");
  const observedBeforeCachedMove = partialPager.observedLogicalCount;
  await partialPager.loadNext();
  assert(partialPager.current[0]?.logicalIndex === 37 && partialPager.observedLogicalCount === observedBeforeCachedMove, "Previous-to-next navigation refetched or misindexed a cached page.");
  for (let page = 0; page < 6; page++) await partialPager.loadNext();
  assert(partialPager.cachedSummaryCount <= 185, "Partial-page eviction exceeded the five-page cache.");
  const hostile = await new StoryApplicationClient("active").listResultItems({ run_id: storyRunId, requested_page_size: 1, kinds: ["finding"], sort: "identity-ascending", search_text: "" });
  assert(hostile.outcome === "accepted" && hostile.page.items[0]?.inert_summary.startsWith("<img") === true, "Hostile text was interpreted or rewritten.");
  await rejects(() => large.listResultItems({ run_id: storyRunId, requested_page_size: 101, kinds: ["finding"], sort: "identity-ascending", search_text: "" }));
  await rejects(() => large.listResultItems({ run_id: storyRunId, requested_page_size: 1, after_cursor: "malformed", kinds: ["finding"], sort: "identity-ascending", search_text: "" }));
  const original = await new StoryApplicationClient("completed").getProgress({ run_id: storyRunId });
  assert(original.outcome === "accepted" && original.progress.progress.completed_units === "8", "The accepted story run identity changed product meaning.");
  const subscription: ProgressSubscriptionRequest = { subscription_id: "subscription_00001", run_id: storyRunId, requested_queue_items: 64 };
  let reconnect = false;
  new StoryApplicationClient("reconnect").subscribeProgress(subscription, (event) => { reconnect = event.outcome === "resync-required"; });
  assert(reconnect, "Reconnect story did not require authoritative resync.");
  const progressResult = await new StoryApplicationClient("active").getProgress({ run_id: subscription.run_id });
  assert(progressResult.outcome === "accepted", "Progress fixture failed.");
  const metadata = { coordinator_instance_id: "coordinator 1", coordinator_fencing_epoch: "1", subscription_id: subscription.subscription_id, durable_event_sequence: "1", projection_version: "projection_0001", run_scope: subscription.run_id, resume_cursor: "cmVzdW1l" };
  const eventPayloads: readonly unknown[] = [
    { outcome: "accepted", event_kind: "progress", metadata, progress: progressResult.progress },
    { outcome: "accepted", event_kind: "lifecycle-changed", metadata, lifecycle_changed: { previous_state: "queued", current_state: "running", lifecycle_generation: "1", transition_id: "transition 1", transition_record_kind: "observed", lifecycle_policy_version: "1.0.0" } },
    { outcome: "resync-required", event_kind: "projection-invalidated", metadata, reason: "projection-rebuilt", error: { code: "resync-required", inert_detail: "Projection changed.", retry_may_be_safe: false }, current_projection_version: "projection_0002" },
    { outcome: "resync-required", event_kind: "resync-required", metadata, reason: "queue-overflow", error: { code: "resync-required", inert_detail: "Queue overflow.", retry_may_be_safe: false }, current_projection_version: "projection_0002" },
  ];
  for (const payload of eventPayloads) validateParsedRendererMessage({ contract_version: rendererContractVersion, message_kind: "event", session_id: "host_session_0001", sequence: "1", subscription_id: subscription.subscription_id, revision: "projection_0001", operation: "progress.subscribe", payload });
  await rejects(() => validateParsedRendererMessage({ contract_version: rendererContractVersion, message_kind: "response", session_id: "host_session_0001", sequence: "1", request_id: "request_identity_0001", operation: "results.list", payload: { outcome: "accepted", page: { items: [], has_more: true, projection_version: "1" } } }));
}

await run();
