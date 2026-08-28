import { rendererContractVersion, rendererRegistrySha256, rendererRegistryVersion, decodeRendererOperation, dispatchRendererResponseBinding, outcomeNames, } from "./generated/renderer-contract.generated.js";
import { validateParsedRendererMessage } from "./decoders.js";
import { parseAndValidateRendererJson } from "./schema-validator.js";
const boundSessionIds = new Set();
export function bindRendererSession(serializedEnvelope) {
    const parsed = parseRendererEvent(serializedEnvelope);
    if (parsed.operation !== "transport.session.establish"
        || parsed.sequence !== "1"
        || parsed.payload.outcome !== "accepted"
        || parsed.payload.origin !== "https://app.infinium.invalid"
        || parsed.payload.renderer_contract_version !== rendererContractVersion
        || parsed.payload.renderer_registry_version !== rendererRegistryVersion
        || parsed.payload.renderer_registry_sha256 !== rendererRegistrySha256
        || boundSessionIds.has(parsed.session_id)) {
        throw new Error("The host session initialization was missing, replayed, or inconsistent.");
    }
    boundSessionIds.add(parsed.session_id);
    const acknowledgementEnvelope = serializeRendererEnvelope({
        contract_version: rendererContractVersion,
        message_kind: "request",
        session_id: parsed.session_id,
        sequence: "1",
        request_id: "transport_acknowledgement_0001",
        operation: "transport.session.establish",
        payload: { renderer_registry_version: rendererRegistryVersion, renderer_registry_sha256: rendererRegistrySha256 },
    });
    return Object.freeze({
        sessionId: parsed.session_id,
        initialRequestSequence: "1",
        initialHostEventSequence: parsed.sequence,
        acknowledgementEnvelope,
    });
}
export class GeneratedBridgeApplicationClient {
    bridge;
    requestSequence = 0n;
    lastHostEventSequence = 0n;
    requestCount = 0n;
    lastRequestId = null;
    get lastIssuedRequestId() { return this.lastRequestId; }
    constructor(bridge, binding) {
        this.bridge = bridge;
        this.sessionId = binding.sessionId;
        this.requestSequence = BigInt(binding.initialRequestSequence);
        this.lastHostEventSequence = BigInt(binding.initialHostEventSequence);
    }
    sessionId;
    async bootstrap(request) {
        validateBootstrapRequest(request);
        return (await this.invoke("application.bootstrap", request)).payload;
    }
    async listResultItems(request) {
        validateResultListRequest(request);
        return (await this.invoke("results.list", request)).payload;
    }
    async getResultDetail(request) {
        validateResultDetailRequest(request);
        return (await this.invoke("results.detail", request)).payload;
    }
    async getProgress(request) {
        validateProgressRequest(request);
        return (await this.invoke("progress.read", request)).payload;
    }
    subscribeProgress(request, listener) {
        validateProgressSubscriptionRequest(request);
        const envelope = this.requestEnvelope("progress.subscribe", request);
        let lastDurableSequence = -1n;
        let active = true;
        const stop = this.bridge.subscribe(serializeRendererEnvelope(envelope), (serializedEvent) => {
            if (!active)
                throw new Error("The renderer delivered an event after the subscription was cancelled.");
            const event = parseRendererEvent(serializedEvent);
            const sequences = validateEventBinding(envelope, event, this.lastHostEventSequence, lastDurableSequence);
            this.lastHostEventSequence = sequences.envelope;
            lastDurableSequence = sequences.durable;
            if (event.operation === "transport.session.establish")
                throw new Error("A host session initialization cannot be replayed as a subscription event.");
            if (event.operation === "transport.gesture.grant")
                throw new Error("A host gesture grant cannot be replayed as a subscription event.");
            listener(event.payload);
        });
        return () => { active = false; stop(); };
    }
    async cancel(targetRequestId, gestureId) {
        validateCancelRequest(targetRequestId, gestureId);
        return (await this.invoke("application.cancel", { target_request_id: targetRequestId }, gestureId)).payload;
    }
    async acceptHostGestureGrant(serializedEnvelope) {
        const event = parseRendererEvent(serializedEnvelope);
        if (event.operation !== "transport.gesture.grant"
            || event.session_id !== this.sessionId
            || BigInt(event.sequence) !== this.lastHostEventSequence + 1n
            || event.payload.operation !== "application.cancel") {
            throw new Error("The host gesture grant is stale, replayed, or belongs to another session.");
        }
        this.lastHostEventSequence = BigInt(event.sequence);
        return await this.cancel(event.payload.target_request_id, event.payload.gesture_id);
    }
    acceptClosedSubscriptionEvent(serializedEnvelope) {
        const event = parseRendererEvent(serializedEnvelope);
        if (event.operation === "transport.session.establish"
            || event.operation === "transport.gesture.grant"
            || event.session_id !== this.sessionId
            || BigInt(event.sequence) !== this.lastHostEventSequence + 1n) {
            throw new Error("The closed-subscription event is stale, replayed, or belongs to another session.");
        }
        this.lastHostEventSequence = BigInt(event.sequence);
    }
    async invoke(operation, payload, gestureId) {
        const envelope = this.requestEnvelope(operation, payload, gestureId);
        const response = parseRendererResponse(await this.bridge.request(serializeRendererEnvelope(envelope)));
        validateResponseEnvelopeBinding(envelope, response);
        return response;
    }
    requestEnvelope(operation, payload, gestureId) {
        if ((operation === "application.cancel") !== (gestureId !== undefined)) {
            throw new Error("Only an application cancellation request may carry the required host gesture proof.");
        }
        this.requestSequence += 1n;
        this.requestCount += 1n;
        const common = {
            contract_version: rendererContractVersion,
            message_kind: "request",
            session_id: this.sessionId,
            sequence: this.requestSequence.toString(),
            request_id: `renderer_request_${this.requestCount.toString().padStart(8, "0")}`,
            operation,
            payload,
        };
        this.lastRequestId = common.request_id;
        return (gestureId === undefined ? common : { ...common, gesture_proof: { gesture_id: gestureId } });
    }
}
const responseBindings = {
    "application.bootstrap": (_request, response) => response.outcome === "accepted" ? response.bootstrap.projection_version : failureProjectionVersion(response),
    "results.list": (request, response) => {
        if (response.outcome === "accepted" && response.page.items.some((item) => item.run_id !== request.run_id || !request.kinds.includes(item.kind))) {
            throw new Error("The result page contains an item outside the originating run or requested kinds.");
        }
        return response.outcome === "accepted" ? response.page.projection_version : failureProjectionVersion(response);
    },
    "results.detail": (request, response) => {
        if (response.outcome === "accepted" && (response.detail.summary.run_id !== request.run_id || response.detail.summary.item_id !== request.item_id || response.detail.summary.kind !== request.kind)) {
            throw new Error("The result detail does not match the originating request.");
        }
        return response.outcome === "accepted" ? response.detail.projection_version : failureProjectionVersion(response);
    },
    "progress.read": (request, response) => {
        if (response.outcome === "accepted" && response.progress.run_id !== request.run_id)
            throw new Error("The progress projection belongs to another run.");
        return response.outcome === "accepted" ? response.progress.projection_version : failureProjectionVersion(response);
    },
    "application.cancel": (_request, response) => failureProjectionVersion(response),
};
function failureProjectionVersion(response) {
    if (response.outcome === "conflict") {
        if (response.conflict === undefined)
            throw new Error("The renderer conflict omitted its current revision.");
        return response.conflict.current_revision;
    }
    if (response.outcome === "resync-required") {
        if (response.current_projection_version === undefined)
            throw new Error("The renderer resync omitted its current projection version.");
        return response.current_projection_version;
    }
    return undefined;
}
function serializeRendererEnvelope(value) {
    const serialized = JSON.stringify(value);
    const parsed = parseAndValidateRendererJson(serialized);
    validateParsedRendererMessage(parsed);
    return serialized;
}
function parseRendererResponse(value) {
    const parsed = parseAndValidateRendererJson(value);
    const header = validateParsedRendererMessage(parsed);
    if (header.messageKind !== "response")
        throw new Error("The renderer bridge returned a non-response message.");
    return parsed;
}
function parseRendererEvent(value) {
    const parsed = parseAndValidateRendererJson(value);
    const header = validateParsedRendererMessage(parsed);
    if (header.messageKind !== "event")
        throw new Error("The renderer bridge returned a non-event message.");
    return parsed;
}
function validateResponseEnvelopeBinding(origin, response) {
    if (response.contract_version !== origin.contract_version
        || response.session_id !== origin.session_id
        || response.sequence !== origin.sequence
        || response.request_id !== origin.request_id
        || response.operation !== origin.operation) {
        throw new Error("The renderer response outer envelope does not match its originating request.");
    }
    const expectedRevision = dispatchRendererResponseBinding(responseBindings, origin.operation, origin.payload, response.payload);
    if (response.revision !== expectedRevision)
        throw new Error("The renderer response revision does not match its closed payload projection.");
}
function validateEventBinding(origin, event, previousEnvelopeSequence, previousDurableSequence) {
    const envelopeSequence = BigInt(assertDecimalUInt64(event.sequence));
    if (event.contract_version !== origin.contract_version
        || event.session_id !== origin.session_id
        || event.subscription_id !== origin.payload.subscription_id
        || envelopeSequence !== previousEnvelopeSequence + 1n) {
        throw new Error("The renderer event outer envelope, projection, or sequence does not match the originating subscription.");
    }
    if (event.operation === "application.resync-required") {
        if (event.revision !== event.payload.current_projection_version)
            throw new Error("The renderer resync event revision does not match its projection.");
        return { envelope: envelopeSequence, durable: previousDurableSequence };
    }
    const metadata = event.payload.metadata;
    const durableSequence = BigInt(assertDecimalUInt64(metadata.durable_event_sequence));
    if (metadata.subscription_id !== origin.payload.subscription_id
        || metadata.run_scope !== origin.payload.run_id
        || event.revision !== metadata.projection_version
        || durableSequence <= previousDurableSequence)
        throw new Error("The renderer event metadata does not match its originating subscription.");
    if (event.payload.outcome === "accepted" && event.payload.event_kind === "progress" && event.payload.progress.run_id !== metadata.run_scope) {
        throw new Error("The renderer progress event contains a projection from another run.");
    }
    return { envelope: envelopeSequence, durable: durableSequence };
}
export function selectApplicationClient(mode, clients) {
    return mode === "fake" ? clients.fake : clients.generatedBridge;
}
export function decodeOutcome(value) {
    if (typeof value !== "string" || !outcomeNames.some((outcome) => outcome === value))
        throw new Error("The renderer returned an unknown operation outcome.");
    return value;
}
export function assertDecimalUInt64(value) {
    if (!/^(0|[1-9][0-9]{0,19})$/u.test(value) || BigInt(value) > 18446744073709551615n) {
        throw new Error("The renderer integer is not a canonical unsigned 64-bit decimal string.");
    }
    return value;
}
export function assertDecimalInt64(value) {
    if (!/^(0|-?[1-9][0-9]{0,18})$/u.test(value))
        throw new Error("The renderer integer is not a canonical signed 64-bit decimal string.");
    const parsed = BigInt(value);
    if (parsed < -9223372036854775808n || parsed > 9223372036854775807n)
        throw new Error("The renderer signed integer exceeds its 64-bit bound.");
    return value;
}
export function assertOpaqueProductIdentity(value) {
    if (value.trim().length === 0 || utf8ByteCount(value) > 160 || [...value].some((symbol) => {
        const codePoint = symbol.codePointAt(0) ?? 0;
        return codePoint < 0x20 || codePoint === 0x7f || (codePoint >= 0xd800 && codePoint <= 0xdfff);
    }))
        throw new Error("The product identity exceeds its opaque UTF-8 bound or contains a control value.");
    return value;
}
function utf8ByteCount(value) {
    let count = 0;
    for (const symbol of value) {
        const codePoint = symbol.codePointAt(0) ?? 0;
        count += codePoint <= 0x7f ? 1 : codePoint <= 0x7ff ? 2 : codePoint <= 0xffff ? 3 : 4;
    }
    return count;
}
export function assertOpaqueCursor(value) {
    if (!/^[A-Za-z0-9_-]+$/u.test(value))
        throw new Error("The authenticated cursor is not canonical base64url.");
    const decoded = decodeBase64Url(value);
    if (decoded.length > 8_192 || encodeBase64Url(decoded) !== value)
        throw new Error("The authenticated cursor is not canonical base64url or exceeds its 8,192-byte bound.");
    return value;
}
const base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
function decodeBase64Url(value) {
    if (value.length % 4 === 1)
        throw new Error("The authenticated cursor has an invalid base64url length.");
    const bytes = [];
    for (let index = 0; index < value.length; index += 4) {
        const a = base64UrlAlphabet.indexOf(value[index] ?? "");
        const b = base64UrlAlphabet.indexOf(value[index + 1] ?? "");
        const c = index + 2 < value.length ? base64UrlAlphabet.indexOf(value[index + 2] ?? "") : -1;
        const d = index + 3 < value.length ? base64UrlAlphabet.indexOf(value[index + 3] ?? "") : -1;
        if (a < 0 || b < 0)
            throw new Error("The authenticated cursor is malformed.");
        bytes.push((a << 2) | (b >> 4));
        if (c >= 0)
            bytes.push(((b & 15) << 4) | (c >> 2));
        if (d >= 0)
            bytes.push(((c & 3) << 6) | d);
    }
    return Uint8Array.from(bytes);
}
function encodeBase64Url(bytes) {
    let value = "";
    for (let index = 0; index < bytes.length; index += 3) {
        const a = bytes[index] ?? 0;
        const b = bytes[index + 1];
        const c = bytes[index + 2];
        value += base64UrlAlphabet[a >> 2];
        value += base64UrlAlphabet[((a & 3) << 4) | ((b ?? 0) >> 4)];
        if (b !== undefined)
            value += base64UrlAlphabet[((b & 15) << 2) | ((c ?? 0) >> 6)];
        if (c !== undefined)
            value += base64UrlAlphabet[c & 63];
    }
    return value;
}
export function validateBootstrapRequest(request) {
    assertBoundedInteger(request.maximum_recent_runs, 1, 20, "maximum_recent_runs");
}
export function validateResultListRequest(request) {
    assertOpaqueProductIdentity(request.run_id);
    assertBoundedInteger(request.requested_page_size, 1, 100, "requested_page_size");
    if (request.kinds.length < 1 || request.kinds.length > 6 || new Set(request.kinds).size !== request.kinds.length)
        throw new Error("The result kinds are empty, duplicated, or exceed their bound.");
    for (const kind of request.kinds)
        if (!["supported-case", "lead-only-case", "finding", "abstention", "failure", "coverage-gap"].includes(kind))
            throw new Error("The result kind is unknown.");
    if (!["identity-ascending", "severity-descending-identity-ascending"].includes(request.sort))
        throw new Error("The result sort is unknown.");
    assertInertText(request.search_text, 160, "result search");
    if (request.after_cursor !== undefined)
        assertOpaqueCursor(request.after_cursor);
}
export function validateResultDetailRequest(request) {
    assertOpaqueProductIdentity(request.run_id);
    assertOpaqueProductIdentity(request.item_id);
    if (!["supported-case", "lead-only-case", "finding", "abstention", "failure", "coverage-gap"].includes(request.kind))
        throw new Error("The result kind is unknown.");
}
export function validateProgressRequest(request) { assertOpaqueProductIdentity(request.run_id); }
export function validateProgressSubscriptionRequest(request) {
    assertRendererIdentifier(request.subscription_id);
    assertOpaqueProductIdentity(request.run_id);
    assertBoundedInteger(request.requested_queue_items, 1, 64, "requested_queue_items");
    if (request.after_cursor !== undefined)
        assertOpaqueCursor(request.after_cursor);
}
export function validateCancelRequest(targetRequestId, gestureId) {
    assertRendererIdentifier(targetRequestId);
    assertRendererIdentifier(gestureId);
}
function assertRendererIdentifier(value) {
    if (!/^[A-Za-z0-9_-]{16,128}$/u.test(value))
        throw new Error("The renderer identity is outside its closed grammar.");
}
function assertBoundedInteger(value, minimum, maximum, label) {
    if (!Number.isInteger(value) || value < minimum || value > maximum)
        throw new Error(`The ${label} is outside its closed bound.`);
}
function assertInertText(value, maximumBytes, label) {
    if (utf8ByteCount(value) > maximumBytes || value.includes("\0") || [...value].some((symbol) => {
        const codePoint = symbol.codePointAt(0) ?? 0;
        return codePoint >= 0xd800 && codePoint <= 0xdfff;
    }))
        throw new Error(`The ${label} is invalid or exceeds its UTF-8 bound.`);
}
export function assertClosedOperation(value) {
    return decodeRendererOperation(value);
}
