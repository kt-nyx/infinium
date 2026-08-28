import { GeneratedBridgeApplicationClient, bindRendererSession, type ClosedRendererBridge } from "./client.js";
import { BoundedResultPager, virtualRowWindow } from "./bounded-result-pager.js";
import type { BootstrapResponse, ProgressResponse, ResultDetailResponse, ResultItemKind } from "./generated/renderer-contract.generated.js";

declare const React: {
  createElement(type: string | ((properties: Readonly<Record<string, unknown>>) => unknown), properties?: Readonly<Record<string, unknown>> | null, ...children: readonly unknown[]): unknown;
  useEffect(effect: () => void | (() => void), dependencies: readonly unknown[]): void;
  useMemo<T>(factory: () => T, dependencies: readonly unknown[]): T;
  useRef<T>(initial: T): { current: T };
  useState<T>(initial: T): readonly [T, (value: T | ((current: T) => T)) => void];
};
declare const ReactDOM: { createRoot(element: Element): { render(value: unknown): void } };

interface WebViewTransport {
  postMessage(value: string): void;
  addEventListener(name: "message", listener: (event: MessageEvent<unknown>) => void, options?: AddEventListenerOptions): void;
}
declare global { interface Window { chrome: { webview: WebViewTransport }; } }

class WebViewClosedBridge implements ClosedRendererBridge {
  private readonly pending = new Map<string, (value: string) => void>();
  private readonly subscriptions = new Map<string, (value: string) => void>();
  private readonly subscriptionRequests = new Map<string, string>();
  private readonly closedSubscriptions = new Set<string>();

  public constructor(
    private readonly transport: WebViewTransport,
    private readonly acceptGestureGrant: (value: string) => void,
    private readonly acceptClosedSubscriptionEvent: (value: string) => void,
  ) {
    transport.addEventListener("message", (event) => this.receive(event.data));
  }

  public request(serializedEnvelope: string): Promise<string> {
    const requestId = readIdentity(serializedEnvelope, "request_id");
    if (this.pending.has(requestId)) throw new Error("A renderer request identifier is already pending.");
    return new Promise((resolve) => {
      this.pending.set(requestId, resolve);
      this.transport.postMessage(serializedEnvelope);
    });
  }

  public subscribe(serializedEnvelope: string, listener: (serializedEvent: string) => void): () => void {
    const subscriptionId = readPayloadIdentity(serializedEnvelope, "subscription_id");
    const requestId = readIdentity(serializedEnvelope, "request_id");
    if (this.subscriptions.has(subscriptionId) || this.subscriptionRequests.size >= 64) throw new Error("The renderer subscription mapping is duplicated or exceeds its finite bound.");
    this.subscriptions.set(subscriptionId, listener);
    this.subscriptionRequests.set(requestId, subscriptionId);
    this.transport.postMessage(serializedEnvelope);
    return () => {
      this.subscriptions.delete(subscriptionId);
      this.subscriptionRequests.delete(requestId);
      this.closedSubscriptions.add(subscriptionId);
      while (this.closedSubscriptions.size > 64) {
        const oldest = this.closedSubscriptions.values().next().value as string | undefined;
        if (oldest === undefined) break;
        this.closedSubscriptions.delete(oldest);
      }
    };
  }

  public closeSubscriptionRequest(requestId: string): void {
    const subscriptionId = this.subscriptionRequests.get(requestId);
    if (subscriptionId !== undefined) this.subscriptions.delete(subscriptionId);
    this.subscriptionRequests.delete(requestId);
  }

  private receive(value: unknown): void {
    if (typeof value !== "string" || new TextEncoder().encode(value).byteLength > 1_048_576) throw new Error("The host returned a malformed or oversized serialized message.");
    const parsed: unknown = JSON.parse(value);
    if (!isRecord(parsed)) throw new Error("The host returned a non-object message.");
    if (parsed.message_kind === "event" && parsed.operation === "transport.gesture.grant") {
      this.acceptGestureGrant(value);
      return;
    }
    if (parsed.message_kind === "response") {
      const requestId = recordString(parsed, "request_id");
      const resolve = this.pending.get(requestId);
      if (resolve === undefined) throw new Error("The host response has no pending request.");
      this.pending.delete(requestId);
      resolve(value);
      return;
    }
    if (parsed.message_kind === "event") {
      const subscriptionId = recordString(parsed, "subscription_id");
      const listener = this.subscriptions.get(subscriptionId);
      if (listener === undefined && this.closedSubscriptions.has(subscriptionId)) {
        this.acceptClosedSubscriptionEvent(value);
        return;
      }
      if (listener === undefined) throw new Error("The host event has no active subscription.");
      listener(value);
      return;
    }
    throw new Error("The host returned an unsupported message kind.");
  }
}

function readIdentity(serialized: string, field: string): string {
  const parsed: unknown = JSON.parse(serialized);
  if (!isRecord(parsed)) throw new Error("The renderer envelope is malformed.");
  return recordString(parsed, field);
}
function readPayloadIdentity(serialized: string, field: string): string {
  const parsed: unknown = JSON.parse(serialized);
  if (!isRecord(parsed) || !isRecord(parsed.payload)) throw new Error("The renderer payload is malformed.");
  return recordString(parsed.payload, field);
}
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === "object" && value !== null && !Array.isArray(value); }
function recordString(value: Record<string, unknown>, field: string): string {
  const selected = value[field];
  if (typeof selected !== "string") throw new Error(`The ${field} identity is missing.`);
  return selected;
}

const h = React.createElement;
const resultKinds: readonly ResultItemKind[] = ["supported-case", "lead-only-case", "finding", "abstention", "failure", "coverage-gap"];

function DiagnosticApplication(properties: Readonly<Record<string, unknown>>): unknown {
  const client = properties.client as GeneratedBridgeApplicationClient;
  const [bootstrap, setBootstrap] = React.useState<BootstrapResponse | null>(null);
  const [runId, setRunId] = React.useState("");
  const [progress, setProgress] = React.useState<ProgressResponse | null>(null);
  const [detail, setDetail] = React.useState<ResultDetailResponse | null>(null);
  const [subscriptionActive, setSubscriptionActive] = React.useState(false);
  const activeSubscription = React.useRef<{ readonly stop: () => void } | null>(null);
  const [firstVisibleRow, setFirstVisibleRow] = React.useState(0);
  const [pagerRevision, setPagerRevision] = React.useState(0);
  const [completedOperationSequence, setCompletedOperationSequence] = React.useState(0);
  const [status, setStatus] = React.useState("Connecting to the local application service.");
  const pager = React.useMemo(() => new BoundedResultPager(client), [client]);

  React.useEffect(() => {
    void client.bootstrap({ maximum_recent_runs: 20 }).then((value) => {
      setBootstrap(value);
      if (value.outcome === "accepted" && value.bootstrap.recent_runs[0] !== undefined) setRunId(value.bootstrap.recent_runs[0].run_id);
      setStatus(value.outcome === "accepted" ? "Local application service connected." : `Bootstrap state: ${value.outcome}.`);
      setCompletedOperationSequence((value) => value + 1);
    }).catch(() => setStatus("The local application service is unavailable; reload to reconnect."));
  }, [client]);
  React.useEffect(() => { document.getElementById("main")?.focus(); }, []);
  React.useEffect(() => {
    if (pagerRevision > 0) document.getElementById("result-viewport")?.focus();
  }, [pagerRevision]);
  React.useEffect(() => {
    const closed = (): void => stopActiveSubscription();
    window.addEventListener("infinium-subscription-closed", closed);
    return () => window.removeEventListener("infinium-subscription-closed", closed);
  }, []);

  void pagerRevision;
  const logicalItems = pager.current;
  const items = logicalItems.map(({ item }) => item);
  const visible = React.useMemo(() => virtualRowWindow(items, firstVisibleRow, 13), [items, firstVisibleRow]);
  const query = async (): Promise<void> => {
    if (runId.length === 0) { setStatus("Enter an opaque run identity."); return; }
    const response = await pager.reset({ run_id: runId, kinds: resultKinds, search_text: "", sort: "identity-ascending", requested_page_size: 100 });
    setPagerRevision((value) => value + 1); setFirstVisibleRow(0); setStatus(`Result query state: ${response.outcome}.`); setCompletedOperationSequence((value) => value + 1);
  };
  const nextPage = async (): Promise<void> => {
    const response = await pager.loadNext();
    if (response === null) return;
    setPagerRevision((value) => value + 1); setFirstVisibleRow(0); setStatus(`Result query state: ${response.outcome}.`); setCompletedOperationSequence((value) => value + 1);
  };
  const readProgress = async (): Promise<void> => {
    const response = await client.getProgress({ run_id: runId }); setProgress(response); setStatus(`Progress state: ${response.outcome}.`); setCompletedOperationSequence((value) => value + 1);
  };
  const readDetail = async (): Promise<void> => {
    const first = items[0];
    if (first === undefined) { setStatus("Query a result page before requesting detail."); return; }
    const response = await client.getResultDetail({ run_id: first.run_id, kind: first.kind, item_id: first.item_id });
    setDetail(response); setStatus(`Detail state: ${response.outcome}.`); setCompletedOperationSequence((value) => value + 1);
  };
  function stopActiveSubscription(): void {
    const active = activeSubscription.current;
    activeSubscription.current = null;
    active?.stop();
    setSubscriptionActive(false);
  }
  function startProgressSubscription(replaceExisting = false): void {
    const previous = activeSubscription.current;
    if (previous !== null && !replaceExisting) return;
    const subscriptionId = `subscription_${crypto.randomUUID().replaceAll("-", "")}`;
    let actualStop: (() => void) | null = null;
    let stopRequested = false;
    const safeStop = (): void => {
      if (actualStop === null) stopRequested = true;
      else actualStop();
    };
    actualStop = client.subscribeProgress({ subscription_id: subscriptionId, run_id: runId, requested_queue_items: 64 }, (event) => {
      if (event.outcome === "accepted" && event.event_kind === "progress") {
        setProgress({ outcome: "accepted", progress: event.progress });
      }
      if (event.outcome === "resync-required") {
        safeStop();
        if (activeSubscription.current?.stop === safeStop) activeSubscription.current = null;
        setSubscriptionActive(false);
        void authoritativeResync(true);
        return;
      }
      setStatus(`Event state: ${event.outcome}.`);
    });
    const replacement = { stop: safeStop };
    activeSubscription.current = replacement;
    setSubscriptionActive(true);
    previous?.stop();
    if (stopRequested) {
      actualStop();
      if (activeSubscription.current === replacement) activeSubscription.current = null;
      setSubscriptionActive(false);
    }
    setStatus("Progress subscription active.");
  }
  async function authoritativeResync(resubscribe: boolean): Promise<void> {
    let stage = "bootstrap";
    try {
      setStatus("Reading authoritative state after reconnect or stale projection.");
      const latestBootstrap = await client.bootstrap({ maximum_recent_runs: 20 });
      setBootstrap(latestBootstrap);
      if (latestBootstrap.outcome !== "accepted") {
        stage = `bootstrap ${latestBootstrap.outcome}`;
        throw new Error("Authoritative bootstrap was not accepted.");
      }
      if (runId.length > 0) {
        stage = "progress";
        const latestProgress = await client.getProgress({ run_id: runId });
        setProgress(latestProgress);
        if (latestProgress.outcome !== "accepted") throw new Error("Authoritative progress was not accepted.");
        stage = "result page";
        const latestResults = await pager.reset({ run_id: runId, kinds: resultKinds, search_text: "", sort: "identity-ascending", requested_page_size: 100 });
        if (latestResults.outcome !== "accepted") throw new Error("Authoritative results were not accepted.");
        setPagerRevision((value) => value + 1);
        setFirstVisibleRow(0);
      }
      setStatus("Authoritative bootstrap, progress, and first result page were refreshed.");
      if (resubscribe && runId.length > 0) startProgressSubscription(true);
    } catch {
      stopActiveSubscription();
      setStatus(`Authoritative ${stage} resynchronization did not produce accepted state; reconnect is required.`);
    } finally {
      setCompletedOperationSequence((value) => value + 1);
    }
  }

  return h("main", { id: "main", className: "shell", tabIndex: -1, "data-completed-operation-sequence": completedOperationSequence },
    h("header", null, h("h1", null, "Infinium desktop consumption proof"), h("p", { id: "status", role: "status", "aria-live": "polite" }, status)),
    h("section", { "aria-labelledby": "connection-heading" },
      h("h2", { id: "connection-heading" }, "Application connection"),
      h("p", null, bootstrap?.outcome === "accepted" ? `Coordinator: ${bootstrap.bootstrap.coordinator_health}` : "No authoritative bootstrap projection yet."),
      h("label", { htmlFor: "run-id" }, "Opaque run identity"),
      h("input", { id: "run-id", value: runId, autoComplete: "off", spellCheck: false, onChange: (event: Event) => setRunId((event.target as HTMLInputElement).value) }),
      h("div", { className: "actions", role: "group", "aria-label": "Diagnostic operations" },
        h("button", { type: "button", onClick: () => void query() }, "Query first page"),
        h("button", { type: "button", onClick: () => void readProgress() }, "Read progress"),
        h("button", { type: "button", onClick: () => void readDetail() }, "Read first result detail"),
        h("button", { type: "button", disabled: subscriptionActive, onClick: () => startProgressSubscription() }, "Subscribe to progress"),
        h("button", { type: "button", onClick: () => void authoritativeResync(activeSubscription.current !== null) }, "Authoritative resync"),
        h("button", { type: "button", onClick: () => location.reload() }, "Reload renderer"))),
    h("section", { "aria-labelledby": "results-heading" },
      h("h2", { id: "results-heading" }, "Bounded logical result window"),
      h("p", null, `Each query transfers at most 100 summaries, the cache retains at most 500, and ${visible.length} rows are mounted. ${pager.observedLogicalCount} logical summaries have been observed.`),
      h("div", { id: "result-viewport", className: "result-viewport", role: "list", tabIndex: 0, "aria-label": "Virtualized result summaries" },
        ...visible.map(({ item, index }) => h("article", {
          key: item.item_id,
          role: "listitem",
          className: "result-row",
          "aria-posinset": (logicalItems[index]?.logicalIndex ?? index) + 1,
          "aria-setsize": pager.accessibilitySetSize,
        }, h("strong", null, item.kind), " — ", item.inert_summary))),
      h("div", { className: "actions", role: "group", "aria-label": "Virtual result window" },
        h("button", { type: "button", disabled: firstVisibleRow === 0, onClick: () => setFirstVisibleRow(Math.max(0, firstVisibleRow - 12)) }, "Previous visible rows"),
        h("button", { type: "button", disabled: visible.length === 0 || visible[visible.length - 1]!.index >= items.length - 1, onClick: () => setFirstVisibleRow(firstVisibleRow + 12) }, "Next visible rows")),
      h("div", { className: "actions", role: "group", "aria-label": "Bounded result pages" },
        h("button", { type: "button", disabled: !pager.hasPrevious, onClick: () => { if (pager.movePrevious()) { setPagerRevision((value) => value + 1); setFirstVisibleRow(0); } } }, "Previous cached page"),
        h("button", { type: "button", disabled: !pager.hasNext, onClick: () => void nextPage() }, "Load next bounded page"))),
    h("section", { "aria-labelledby": "detail-heading" }, h("h2", { id: "detail-heading" }, "Result detail"), h("pre", { tabIndex: 0 }, detail === null ? "No detail projection." : JSON.stringify(detail, null, 2))),
    h("section", { "aria-labelledby": "progress-heading" }, h("h2", { id: "progress-heading" }, "Progress"), h("pre", { tabIndex: 0 }, progress === null ? "No progress projection." : JSON.stringify(progress, null, 2))));
}

let initialized = false;
window.chrome.webview.addEventListener("message", (event) => {
  if (initialized || typeof event.data !== "string") throw new Error("The host session initialization was replayed or malformed.");
  const binding = bindRendererSession(event.data);
  window.chrome.webview.postMessage(binding.acknowledgementEnvelope);
  let client: GeneratedBridgeApplicationClient;
  const bridge = new WebViewClosedBridge(
    window.chrome.webview,
    (grant) => {
      const target = readPayloadIdentity(grant, "target_request_id");
      void client.acceptHostGestureGrant(grant).then(() => {
        bridge.closeSubscriptionRequest(target);
        window.dispatchEvent(new Event("infinium-subscription-closed"));
      });
    },
    (event) => client.acceptClosedSubscriptionEvent(event));
  client = new GeneratedBridgeApplicationClient(bridge, binding);
  const root = document.getElementById("root");
  if (root === null) throw new Error("The diagnostic renderer root is missing.");
  initialized = true;
  root.removeAttribute("aria-busy");
  ReactDOM.createRoot(root).render(h(DiagnosticApplication, { client }));
}, { once: true });
