import { GeneratedBridgeApplicationClient, bindRendererSession } from "./client.js";
import { BoundedResultPager, virtualRowWindow } from "./bounded-result-pager.js";
class WebViewClosedBridge {
    transport;
    acceptGestureGrant;
    acceptClosedSubscriptionEvent;
    pending = new Map();
    subscriptions = new Map();
    subscriptionRequests = new Map();
    closedSubscriptions = new Set();
    constructor(transport, acceptGestureGrant, acceptClosedSubscriptionEvent) {
        this.transport = transport;
        this.acceptGestureGrant = acceptGestureGrant;
        this.acceptClosedSubscriptionEvent = acceptClosedSubscriptionEvent;
        transport.addEventListener("message", (event) => this.receive(event.data));
    }
    request(serializedEnvelope) {
        const requestId = readIdentity(serializedEnvelope, "request_id");
        if (this.pending.has(requestId))
            throw new Error("A renderer request identifier is already pending.");
        return new Promise((resolve) => {
            this.pending.set(requestId, resolve);
            this.transport.postMessage(serializedEnvelope);
        });
    }
    subscribe(serializedEnvelope, listener) {
        const subscriptionId = readPayloadIdentity(serializedEnvelope, "subscription_id");
        const requestId = readIdentity(serializedEnvelope, "request_id");
        if (this.subscriptions.has(subscriptionId) || this.subscriptionRequests.size >= 64)
            throw new Error("The renderer subscription mapping is duplicated or exceeds its finite bound.");
        this.subscriptions.set(subscriptionId, listener);
        this.subscriptionRequests.set(requestId, subscriptionId);
        this.transport.postMessage(serializedEnvelope);
        return () => {
            this.subscriptions.delete(subscriptionId);
            this.subscriptionRequests.delete(requestId);
            this.closedSubscriptions.add(subscriptionId);
            while (this.closedSubscriptions.size > 64) {
                const oldest = this.closedSubscriptions.values().next().value;
                if (oldest === undefined)
                    break;
                this.closedSubscriptions.delete(oldest);
            }
        };
    }
    closeSubscriptionRequest(requestId) {
        const subscriptionId = this.subscriptionRequests.get(requestId);
        if (subscriptionId !== undefined)
            this.subscriptions.delete(subscriptionId);
        this.subscriptionRequests.delete(requestId);
    }
    receive(value) {
        if (typeof value !== "string" || new TextEncoder().encode(value).byteLength > 1_048_576)
            throw new Error("The host returned a malformed or oversized serialized message.");
        const parsed = JSON.parse(value);
        if (!isRecord(parsed))
            throw new Error("The host returned a non-object message.");
        if (parsed.message_kind === "event" && parsed.operation === "transport.gesture.grant") {
            this.acceptGestureGrant(value);
            return;
        }
        if (parsed.message_kind === "response") {
            const requestId = recordString(parsed, "request_id");
            const resolve = this.pending.get(requestId);
            if (resolve === undefined)
                throw new Error("The host response has no pending request.");
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
            if (listener === undefined)
                throw new Error("The host event has no active subscription.");
            listener(value);
            return;
        }
        throw new Error("The host returned an unsupported message kind.");
    }
}
function readIdentity(serialized, field) {
    const parsed = JSON.parse(serialized);
    if (!isRecord(parsed))
        throw new Error("The renderer envelope is malformed.");
    return recordString(parsed, field);
}
function readPayloadIdentity(serialized, field) {
    const parsed = JSON.parse(serialized);
    if (!isRecord(parsed) || !isRecord(parsed.payload))
        throw new Error("The renderer payload is malformed.");
    return recordString(parsed.payload, field);
}
function isRecord(value) { return typeof value === "object" && value !== null && !Array.isArray(value); }
function recordString(value, field) {
    const selected = value[field];
    if (typeof selected !== "string")
        throw new Error(`The ${field} identity is missing.`);
    return selected;
}
const h = React.createElement;
const resultKinds = ["supported-case", "lead-only-case", "finding", "abstention", "failure", "coverage-gap"];
function DiagnosticApplication(properties) {
    const client = properties.client;
    const [bootstrap, setBootstrap] = React.useState(null);
    const [runId, setRunId] = React.useState("");
    const [progress, setProgress] = React.useState(null);
    const [detail, setDetail] = React.useState(null);
    const [subscriptionActive, setSubscriptionActive] = React.useState(false);
    const activeSubscription = React.useRef(null);
    const [firstVisibleRow, setFirstVisibleRow] = React.useState(0);
    const [pagerRevision, setPagerRevision] = React.useState(0);
    const [completedOperationSequence, setCompletedOperationSequence] = React.useState(0);
    const [status, setStatus] = React.useState("Connecting to the local application service.");
    const pager = React.useMemo(() => new BoundedResultPager(client), [client]);
    React.useEffect(() => {
        void client.bootstrap({ maximum_recent_runs: 20 }).then((value) => {
            setBootstrap(value);
            if (value.outcome === "accepted" && value.bootstrap.recent_runs[0] !== undefined)
                setRunId(value.bootstrap.recent_runs[0].run_id);
            setStatus(value.outcome === "accepted" ? "Local application service connected." : `Bootstrap state: ${value.outcome}.`);
            setCompletedOperationSequence((value) => value + 1);
        }).catch(() => setStatus("The local application service is unavailable; reload to reconnect."));
    }, [client]);
    React.useEffect(() => { document.getElementById("main")?.focus(); }, []);
    React.useEffect(() => {
        if (pagerRevision > 0)
            document.getElementById("result-viewport")?.focus();
    }, [pagerRevision]);
    React.useEffect(() => {
        const closed = () => stopActiveSubscription();
        window.addEventListener("infinium-subscription-closed", closed);
        return () => window.removeEventListener("infinium-subscription-closed", closed);
    }, []);
    void pagerRevision;
    const logicalItems = pager.current;
    const items = logicalItems.map(({ item }) => item);
    const visible = React.useMemo(() => virtualRowWindow(items, firstVisibleRow, 13), [items, firstVisibleRow]);
    const query = async () => {
        if (runId.length === 0) {
            setStatus("Enter an opaque run identity.");
            return;
        }
        const response = await pager.reset({ run_id: runId, kinds: resultKinds, search_text: "", sort: "identity-ascending", requested_page_size: 100 });
        setPagerRevision((value) => value + 1);
        setFirstVisibleRow(0);
        setStatus(`Result query state: ${response.outcome}.`);
        setCompletedOperationSequence((value) => value + 1);
    };
    const nextPage = async () => {
        const response = await pager.loadNext();
        if (response === null)
            return;
        setPagerRevision((value) => value + 1);
        setFirstVisibleRow(0);
        setStatus(`Result query state: ${response.outcome}.`);
        setCompletedOperationSequence((value) => value + 1);
    };
    const readProgress = async () => {
        const response = await client.getProgress({ run_id: runId });
        setProgress(response);
        setStatus(`Progress state: ${response.outcome}.`);
        setCompletedOperationSequence((value) => value + 1);
    };
    const readDetail = async () => {
        const first = items[0];
        if (first === undefined) {
            setStatus("Query a result page before requesting detail.");
            return;
        }
        const response = await client.getResultDetail({ run_id: first.run_id, kind: first.kind, item_id: first.item_id });
        setDetail(response);
        setStatus(`Detail state: ${response.outcome}.`);
        setCompletedOperationSequence((value) => value + 1);
    };
    function stopActiveSubscription() {
        const active = activeSubscription.current;
        activeSubscription.current = null;
        active?.stop();
        setSubscriptionActive(false);
    }
    function startProgressSubscription(replaceExisting = false) {
        const previous = activeSubscription.current;
        if (previous !== null && !replaceExisting)
            return;
        const subscriptionId = `subscription_${crypto.randomUUID().replaceAll("-", "")}`;
        let actualStop = null;
        let stopRequested = false;
        const safeStop = () => {
            if (actualStop === null)
                stopRequested = true;
            else
                actualStop();
        };
        actualStop = client.subscribeProgress({ subscription_id: subscriptionId, run_id: runId, requested_queue_items: 64 }, (event) => {
            if (event.outcome === "accepted" && event.event_kind === "progress") {
                setProgress({ outcome: "accepted", progress: event.progress });
            }
            if (event.outcome === "resync-required") {
                safeStop();
                if (activeSubscription.current?.stop === safeStop)
                    activeSubscription.current = null;
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
            if (activeSubscription.current === replacement)
                activeSubscription.current = null;
            setSubscriptionActive(false);
        }
        setStatus("Progress subscription active.");
    }
    async function authoritativeResync(resubscribe) {
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
                if (latestProgress.outcome !== "accepted")
                    throw new Error("Authoritative progress was not accepted.");
                stage = "result page";
                const latestResults = await pager.reset({ run_id: runId, kinds: resultKinds, search_text: "", sort: "identity-ascending", requested_page_size: 100 });
                if (latestResults.outcome !== "accepted")
                    throw new Error("Authoritative results were not accepted.");
                setPagerRevision((value) => value + 1);
                setFirstVisibleRow(0);
            }
            setStatus("Authoritative bootstrap, progress, and first result page were refreshed.");
            if (resubscribe && runId.length > 0)
                startProgressSubscription(true);
        }
        catch {
            stopActiveSubscription();
            setStatus(`Authoritative ${stage} resynchronization did not produce accepted state; reconnect is required.`);
        }
        finally {
            setCompletedOperationSequence((value) => value + 1);
        }
    }
    return h("main", { id: "main", className: "shell", tabIndex: -1, "data-completed-operation-sequence": completedOperationSequence }, h("header", null, h("h1", null, "Infinium desktop consumption proof"), h("p", { id: "status", role: "status", "aria-live": "polite" }, status)), h("section", { "aria-labelledby": "connection-heading" }, h("h2", { id: "connection-heading" }, "Application connection"), h("p", null, bootstrap?.outcome === "accepted" ? `Coordinator: ${bootstrap.bootstrap.coordinator_health}` : "No authoritative bootstrap projection yet."), h("label", { htmlFor: "run-id" }, "Opaque run identity"), h("input", { id: "run-id", value: runId, autoComplete: "off", spellCheck: false, onChange: (event) => setRunId(event.target.value) }), h("div", { className: "actions", role: "group", "aria-label": "Diagnostic operations" }, h("button", { type: "button", onClick: () => void query() }, "Query first page"), h("button", { type: "button", onClick: () => void readProgress() }, "Read progress"), h("button", { type: "button", onClick: () => void readDetail() }, "Read first result detail"), h("button", { type: "button", disabled: subscriptionActive, onClick: () => startProgressSubscription() }, "Subscribe to progress"), h("button", { type: "button", onClick: () => void authoritativeResync(activeSubscription.current !== null) }, "Authoritative resync"), h("button", { type: "button", onClick: () => location.reload() }, "Reload renderer"))), h("section", { "aria-labelledby": "results-heading" }, h("h2", { id: "results-heading" }, "Bounded logical result window"), h("p", null, `Each query transfers at most 100 summaries, the cache retains at most 500, and ${visible.length} rows are mounted. ${pager.observedLogicalCount} logical summaries have been observed.`), h("div", { id: "result-viewport", className: "result-viewport", role: "list", tabIndex: 0, "aria-label": "Virtualized result summaries" }, ...visible.map(({ item, index }) => h("article", {
        key: item.item_id,
        role: "listitem",
        className: "result-row",
        "aria-posinset": (logicalItems[index]?.logicalIndex ?? index) + 1,
        "aria-setsize": pager.accessibilitySetSize,
    }, h("strong", null, item.kind), " — ", item.inert_summary))), h("div", { className: "actions", role: "group", "aria-label": "Virtual result window" }, h("button", { type: "button", disabled: firstVisibleRow === 0, onClick: () => setFirstVisibleRow(Math.max(0, firstVisibleRow - 12)) }, "Previous visible rows"), h("button", { type: "button", disabled: visible.length === 0 || visible[visible.length - 1].index >= items.length - 1, onClick: () => setFirstVisibleRow(firstVisibleRow + 12) }, "Next visible rows")), h("div", { className: "actions", role: "group", "aria-label": "Bounded result pages" }, h("button", { type: "button", disabled: !pager.hasPrevious, onClick: () => { if (pager.movePrevious()) {
            setPagerRevision((value) => value + 1);
            setFirstVisibleRow(0);
        } } }, "Previous cached page"), h("button", { type: "button", disabled: !pager.hasNext, onClick: () => void nextPage() }, "Load next bounded page"))), h("section", { "aria-labelledby": "detail-heading" }, h("h2", { id: "detail-heading" }, "Result detail"), h("pre", { tabIndex: 0 }, detail === null ? "No detail projection." : JSON.stringify(detail, null, 2))), h("section", { "aria-labelledby": "progress-heading" }, h("h2", { id: "progress-heading" }, "Progress"), h("pre", { tabIndex: 0 }, progress === null ? "No progress projection." : JSON.stringify(progress, null, 2))));
}
let initialized = false;
window.chrome.webview.addEventListener("message", (event) => {
    if (initialized || typeof event.data !== "string")
        throw new Error("The host session initialization was replayed or malformed.");
    const binding = bindRendererSession(event.data);
    window.chrome.webview.postMessage(binding.acknowledgementEnvelope);
    let client;
    const bridge = new WebViewClosedBridge(window.chrome.webview, (grant) => {
        const target = readPayloadIdentity(grant, "target_request_id");
        void client.acceptHostGestureGrant(grant).then(() => {
            bridge.closeSubscriptionRequest(target);
            window.dispatchEvent(new Event("infinium-subscription-closed"));
        });
    }, (event) => client.acceptClosedSubscriptionEvent(event));
    client = new GeneratedBridgeApplicationClient(bridge, binding);
    const root = document.getElementById("root");
    if (root === null)
        throw new Error("The diagnostic renderer root is missing.");
    initialized = true;
    root.removeAttribute("aria-busy");
    ReactDOM.createRoot(root).render(h(DiagnosticApplication, { client }));
}, { once: true });
