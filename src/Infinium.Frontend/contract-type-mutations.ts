import type {
  BootstrapResponse,
  RendererEventEnvelope,
  RendererRequestEnvelope,
  RendererResponseEnvelope,
} from "./generated/renderer-contract.generated.js";

declare function acceptRequest(envelope: RendererRequestEnvelope): void;
declare function acceptResponse(envelope: RendererResponseEnvelope): void;
declare function acceptEvent(envelope: RendererEventEnvelope): void;

const sessionId = "11111111111111111111111111111111";
const requestId = "22222222222222222222222222222222";
const subscriptionId = "33333333333333333333333333333333";
const gestureId = "44444444444444444444444444444444";
const registrySha = "411a9c05604c7664773aa62c36f62817273ecaff228f20e074063bed1414cfa9";
declare const bootstrapResponse: BootstrapResponse;

// @ts-expect-error A non-cancel request cannot carry gesture authority.
acceptRequest({ contract_version: "1.4.0", message_kind: "request", session_id: sessionId, sequence: "2", request_id: requestId, operation: "application.bootstrap", gesture_proof: { gesture_id: gestureId }, payload: { maximum_recent_runs: 10 } });

// @ts-expect-error Cancellation must carry a host-issued gesture proof.
acceptRequest({ contract_version: "1.4.0", message_kind: "request", session_id: sessionId, sequence: "2", request_id: requestId, operation: "application.cancel", payload: { target_request_id: requestId } });

// @ts-expect-error Session initialization cannot bind a renderer request.
acceptEvent({ contract_version: "1.4.0", message_kind: "event", session_id: sessionId, sequence: "1", request_id: requestId, operation: "transport.session.establish", payload: { outcome: "accepted", origin: "https://app.infinium.invalid", renderer_contract_version: "1.4.0", renderer_registry_version: "1.3.0", renderer_registry_sha256: registrySha } });

// @ts-expect-error Session initialization cannot bind a subscription.
acceptEvent({ contract_version: "1.4.0", message_kind: "event", session_id: sessionId, sequence: "1", subscription_id: subscriptionId, operation: "transport.session.establish", payload: { outcome: "accepted", origin: "https://app.infinium.invalid", renderer_contract_version: "1.4.0", renderer_registry_version: "1.3.0", renderer_registry_sha256: registrySha } });

// @ts-expect-error Session initialization cannot carry a projection revision.
acceptEvent({ contract_version: "1.4.0", message_kind: "event", session_id: sessionId, sequence: "1", revision: "1", operation: "transport.session.establish", payload: { outcome: "accepted", origin: "https://app.infinium.invalid", renderer_contract_version: "1.4.0", renderer_registry_version: "1.3.0", renderer_registry_sha256: registrySha } });

// @ts-expect-error A gesture grant cannot bind a renderer request.
acceptEvent({ contract_version: "1.4.0", message_kind: "event", session_id: sessionId, sequence: "2", request_id: requestId, operation: "transport.gesture.grant", payload: { outcome: "accepted", gesture_id: gestureId, target_request_id: requestId, operation: "application.cancel" } });

// @ts-expect-error A gesture grant cannot bind a subscription.
acceptEvent({ contract_version: "1.4.0", message_kind: "event", session_id: sessionId, sequence: "2", subscription_id: subscriptionId, operation: "transport.gesture.grant", payload: { outcome: "accepted", gesture_id: gestureId, target_request_id: requestId, operation: "application.cancel" } });

// @ts-expect-error A gesture grant cannot carry a projection revision.
acceptEvent({ contract_version: "1.4.0", message_kind: "event", session_id: sessionId, sequence: "2", revision: "1", operation: "transport.gesture.grant", payload: { outcome: "accepted", gesture_id: gestureId, target_request_id: requestId, operation: "application.cancel" } });

// @ts-expect-error Host responses never carry gesture authority.
acceptResponse({ contract_version: "1.4.0", message_kind: "response", session_id: sessionId, sequence: "2", request_id: requestId, operation: "application.bootstrap", gesture_proof: { gesture_id: gestureId }, payload: bootstrapResponse });
