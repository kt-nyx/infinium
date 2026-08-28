import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { createHash } from "node:crypto";

const root = resolve(import.meta.dirname, "..");
const sourcePath = resolve(root, "contracts/json-schema/renderer-envelope.v1.schema.json");
const source = JSON.parse(readFileSync(sourcePath, "utf8"));
const registry = source["x-infinium-registry"];
if (!registry || registry.renderer_contract_version !== source.properties.contract_version.const) {
  throw new Error("The renderer schema must own matching x-infinium-registry metadata.");
}

const messages = registry.operations.flatMap((operation) => operation.messages.map((message) => ({
  operation: operation.operation,
  nativeTarget: operation.native_target,
  messageKind: message.message_kind,
  direction: message.direction ?? (message.message_kind === "request" ? "renderer-to-host" : "host-to-renderer"),
  payloadShape: message.payload_shape,
  schemaDefinition: message.schema_definition,
  gesture: message.gesture,
  outcomes: message.outcomes ?? [],
})));
const envelopeKeys = source.oneOf.map((variant) => `${variant.properties.operation.const}:${variant.properties.message_kind.const}`);
const registryKeys = messages.map((message) => `${message.operation}:${message.messageKind}`);
if (new Set(registryKeys).size !== registryKeys.length || JSON.stringify(envelopeKeys) !== JSON.stringify(registryKeys)) {
  throw new Error("The schema envelope branches and renderer registry metadata are not exhaustive and ordered alike.");
}
for (const message of messages) {
  if (!source.$defs[message.schemaDefinition]) {
    throw new Error(`Missing schema definition ${message.schemaDefinition}.`);
  }
}

const literal = (value) => JSON.stringify(value);
const pascal = (value) => value.slice(0, 1).toUpperCase() + value.slice(1);
const refName = (value) => pascal(value.replace("#/$defs/", ""));
const forbiddenRequiredProperties = (node) => {
  const clauses = node?.not?.anyOf ?? (node?.not?.required ? [node.not] : []);
  return [...new Set(clauses.flatMap((clause) => clause.required ?? []))];
};
const typeFor = (node, indent = 0) => {
  if (node.$ref) return refName(node.$ref);
  if (Object.hasOwn(node, "const")) return literal(node.const);
  if (node.enum) return node.enum.map(literal).join(" | ");
  if (node.oneOf) return node.oneOf.map((item) => `(${typeFor(item, indent)})`).join(" | ");
  if (node.allOf) return node.allOf.map((item) => `(${typeFor(item, indent)})`).join(" & ");
  if (node.type === "array") return `readonly (${typeFor(node.items, indent)})[]`;
  if (node.type === "string") return "string";
  if (node.type === "integer" || node.type === "number") return "number";
  if (node.type === "boolean") return "boolean";
  if (node.type === "object" || node.properties) {
    const required = new Set(node.required ?? []);
    const forbidden = new Set(forbiddenRequiredProperties(node));
    const properties = new Map(Object.entries(node.properties ?? {}));
    for (const name of forbidden) {
      if (!properties.has(name)) properties.set(name, undefined);
    }
    const fields = [...properties].map(([name, shape]) => forbidden.has(name)
      ? `${" ".repeat(indent + 2)}readonly ${JSON.stringify(name)}?: never;`
      : `${" ".repeat(indent + 2)}readonly ${JSON.stringify(name)}${required.has(name) ? "" : "?"}: ${typeFor(shape, indent + 2)};`);
    return fields.length === 0 ? "Readonly<Record<string, never>>" : `{\n${fields.join("\n")}\n${" ".repeat(indent)}}`;
  }
  throw new Error(`Unsupported JSON Schema shape: ${JSON.stringify(node)}`);
};

const definitions = Object.entries(source.$defs)
  .map(([name, shape]) => `export type ${pascal(name)} = ${typeFor(shape)};`)
  .join("\n");
const operations = registry.operations.map((operation) => literal(operation.operation)).join(" | ");
const outcomeValues = [...new Set(messages.flatMap((message) => message.outcomes))];
const outcomes = outcomeValues.map(literal).join(" | ");
const generatedRegistry = messages.map((message) => `  { operation: ${literal(message.operation)}, nativeTarget: ${literal(message.nativeTarget)}, messageKind: ${literal(message.messageKind)}, direction: ${literal(message.direction)}, payloadShape: ${literal(message.payloadShape)}, schemaDefinition: ${literal(message.schemaDefinition)}, gesture: ${literal(message.gesture)}, outcomes: ${literal(message.outcomes)} }`).join(",\n");
const payloadMap = messages.map((message) => `  readonly ${literal(`${message.operation}:${message.messageKind}`)}: ${pascal(message.schemaDefinition)};`).join("\n");
const messagesFor = (kind) => messages.filter((message) => message.messageKind === kind);
const uniqueOperations = (selected) => [...new Set(selected.map((message) => message.operation))];
const requestMessages = messagesFor("request");
const responseMessages = messagesFor("response");
const eventMessages = messagesFor("event");
const completeEnvelopeVariant = (variant) => ({
  type: "object",
  properties: { ...source.properties, ...variant.properties },
  required: [...new Set([...(source.required ?? []), ...(variant.required ?? [])])],
  not: variant.not,
});
const envelopeTypeFor = (kind) => source.oneOf
  .filter((variant) => variant.properties.message_kind.const === kind)
  .map((variant) => `(${typeFor(completeEnvelopeVariant(variant))})`)
  .join(" | ");
const requestOperations = uniqueOperations(requestMessages);
const responseOperations = uniqueOperations(responseMessages);
const eventOperations = uniqueOperations(eventMessages);
const operationUnion = (selected) => selected.map(literal).join(" | ");
const keyedPayloadMap = (selected) => selected.map((message) => `  readonly ${literal(message.operation)}: ${pascal(message.schemaDefinition)};`).join("\n");
const messageKeys = (selected) => selected.map((message) => literal(`${message.operation}:${message.messageKind}`)).join(", ");
const requestByOperation = new Map(requestMessages.map((message) => [message.operation, message]));
const responseDispatchOperations = responseOperations.map((operation) => {
  const requestMessage = requestByOperation.get(operation);
  const responseMessage = responseMessages.find((message) => message.operation === operation);
  if (!requestMessage) throw new Error(`Response operation ${operation} has no matching request.`);
  if (!responseMessage) throw new Error(`Response operation ${operation} has no response message.`);
  return { operation, requestMessage, responseMessage };
});
const responseHandlers = responseDispatchOperations.map(({ operation, requestMessage, responseMessage }) =>
  `  readonly ${literal(operation)}: (payload: ${pascal(requestMessage.schemaDefinition)}, gestureId: string | undefined) => Promise<${pascal(responseMessage.schemaDefinition)}>;`).join("\n");
const responseDispatchRequests = responseDispatchOperations.map(({ operation, requestMessage }) =>
  `{ readonly operation: ${literal(operation)}; readonly payload: ${pascal(requestMessage.schemaDefinition)}; readonly gestureId?: string }`).join(" | ");
const responseDispatchCases = responseDispatchOperations.map(({ operation, requestMessage }) =>
  `    case ${literal(operation)}: return handlers[${literal(operation)}](payload as ${pascal(requestMessage.schemaDefinition)}, gestureId) as Promise<ResponsePayloads[TOperation]>;`).join("\n");
const responseBindingHandlers = responseDispatchOperations.map(({ operation, requestMessage, responseMessage }) =>
  `  readonly ${literal(operation)}: (request: ${pascal(requestMessage.schemaDefinition)}, response: ${pascal(responseMessage.schemaDefinition)}) => TResult;`).join("\n");
const responseBindingCases = responseDispatchOperations.map(({ operation, requestMessage, responseMessage }) =>
  `    case ${literal(operation)}: return handlers[${literal(operation)}](request as ${pascal(requestMessage.schemaDefinition)}, response as ${pascal(responseMessage.schemaDefinition)});`).join("\n");
const rendererSchema = { ...source };
delete rendererSchema["x-infinium-registry"];
const canonicalRegistry = {
  ...registry,
  operations: registry.operations.map((operation) => ({
    ...operation,
    messages: operation.messages.map((message) => ({
      ...message,
      direction: message.direction ?? (message.message_kind === "request" ? "renderer-to-host" : "host-to-renderer"),
    })),
  })),
};
const registryContent = `${JSON.stringify(canonicalRegistry, null, 2)}\n`;
const registrySha256 = createHash("sha256").update(registryContent, "utf8").digest("hex");

const typescript = `// <auto-generated />
// Sole source: contracts/json-schema/renderer-envelope.v1.schema.json.
// Regenerate with eng/invoke-frontend.ps1 -Task Generate.

export const rendererContractVersion = ${literal(registry.renderer_contract_version)} as const;
export const rendererRegistryVersion = ${literal(registry.schema_version)} as const;
export const rendererRegistrySha256 = ${literal(registrySha256)} as const;
export const rendererLimits = ${JSON.stringify(registry.limits, null, 2)} as const;
export type RendererOperation = ${operations};
export type MessageKind = "request" | "response" | "event";
export type OutcomeName = ${outcomes};
export const rendererOperations = [${registry.operations.map((operation) => literal(operation.operation)).join(", ")}] as const;
export const outcomeNames = [${outcomeValues.map(literal).join(", ")}] as const;
export function decodeRendererOperation(value: string): RendererOperation {
  if (!rendererOperations.some((operation) => operation === value)) throw new Error("The renderer operation is not registered.");
  return value as RendererOperation;
}
${definitions}
export type DecimalUInt64 = Uint64;
export type DecimalInt64 = string;
export type OpaqueIdentity = OpaqueProductIdentity;
export type ProjectionVersion = Revision;
export type FailurePayload = RejectedResponse | ConflictResponse | UnsupportedResponse | UnavailableResponse | CancelledResponse | IndeterminateResponse | ResyncRequiredResponse;
export const lifecycleStates = ${JSON.stringify(source.$defs.lifecycleState.enum)} as const;
export interface RendererPayloadMap {
${payloadMap}
}
export type RequestOperation = ${operationUnion(requestOperations)};
export type ResponseOperation = ${operationUnion(responseOperations)};
export type EventOperation = ${operationUnion(eventOperations)};
export interface RequestPayloads {
${keyedPayloadMap(requestMessages)}
}
export interface ResponsePayloads {
${keyedPayloadMap(responseMessages)}
}
export interface EventPayloads {
${keyedPayloadMap(eventMessages)}
}
export type RendererRequestEnvelope = ${envelopeTypeFor("request")};
export type RendererResponseEnvelope = ${envelopeTypeFor("response")};
export type RendererEventEnvelope = ${envelopeTypeFor("event")};
export type RequestEnvelopeFor<TOperation extends RequestOperation> = RendererRequestEnvelope & { readonly operation: TOperation; readonly payload: RequestPayloads[TOperation] };
export type ResponseEnvelopeFor<TOperation extends ResponseOperation> = RendererResponseEnvelope & { readonly operation: TOperation; readonly payload: ResponsePayloads[TOperation] };
export type EventEnvelopeFor<TOperation extends EventOperation> = RendererEventEnvelope & { readonly operation: TOperation; readonly payload: EventPayloads[TOperation] };
export const requestOperations = [${requestOperations.map(literal).join(", ")}] as const;
export const responseOperations = [${responseOperations.map(literal).join(", ")}] as const;
export const eventOperations = [${eventOperations.map(literal).join(", ")}] as const;
export const requestMessageKeys = [${messageKeys(requestMessages)}] as const;
export const responseMessageKeys = [${messageKeys(responseMessages)}] as const;
export const eventMessageKeys = [${messageKeys(eventMessages)}] as const;
export interface RendererResponseOperationHandlers {
${responseHandlers}
}
export type RendererResponseDispatchRequest = ${responseDispatchRequests};
export function dispatchRendererResponseOperation<TOperation extends ResponseOperation>(handlers: RendererResponseOperationHandlers, operation: TOperation, payload: RequestPayloads[TOperation], gestureId?: string): Promise<ResponsePayloads[TOperation]> {
  assertRendererResponseHandlerCoverage(handlers);
  switch (operation) {
${responseDispatchCases}
  }
}
export function assertRendererResponseHandlerCoverage(handlers: object): void {
  const actual = Object.keys(handlers).sort();
  const expected = [...responseOperations].sort();
  if (JSON.stringify(actual) !== JSON.stringify(expected)) throw new Error("The renderer response handler map is incomplete or contains an unregistered operation.");
}
export interface RendererResponseBindingHandlers<TResult> {
${responseBindingHandlers}
}
export function dispatchRendererResponseBinding<TResult>(handlers: RendererResponseBindingHandlers<TResult>, operation: ResponseOperation, request: RequestPayloads[ResponseOperation], response: ResponsePayloads[ResponseOperation]): TResult {
  switch (operation) {
${responseBindingCases}
  }
}
export function assertRendererOperationPartitions(entries: readonly { readonly operation: string; readonly messageKind: string }[]): void {
  const actual = {
    request: entries.filter((entry) => entry.messageKind === "request").map((entry) => entry.operation + ":" + entry.messageKind),
    response: entries.filter((entry) => entry.messageKind === "response").map((entry) => entry.operation + ":" + entry.messageKind),
    event: entries.filter((entry) => entry.messageKind === "event").map((entry) => entry.operation + ":" + entry.messageKind),
  };
  if (JSON.stringify(actual.request) !== JSON.stringify(requestMessageKeys)
      || JSON.stringify(actual.response) !== JSON.stringify(responseMessageKeys)
      || JSON.stringify(actual.event) !== JSON.stringify(eventMessageKeys)) {
    throw new Error("The renderer operation partitions omit, duplicate, reorder, or add a message.");
  }
}
export type RegisteredMessageKey = keyof RendererPayloadMap;
export const registeredMessages = [
${generatedRegistry}
] as const;
export const deniedAuthorityFields = ${JSON.stringify(registry.denied_authority_fields)} as const;
export const rendererEnvelopeSchema = ${JSON.stringify(rendererSchema, null, 2)} as const;
`;

const csharpEntries = messages.map((message) => `        new(${literal(message.operation)}, ${literal(message.nativeTarget)}, ${literal(message.messageKind)}, ${literal(message.direction)}, ${literal(message.payloadShape)}, ${literal(message.schemaDefinition)}),`).join("\n");
const unaryAdapters = registry.operations.filter((operation) => operation.native_adapter.kind === "unary");
const streamAdapters = registry.operations.filter((operation) => operation.native_adapter.kind === "stream");
const hostAdapters = registry.operations.filter((operation) => operation.native_adapter.kind === "host");
const clientMethods = [...unaryAdapters, ...streamAdapters].map((operation) => {
  const adapter = operation.native_adapter;
  return adapter.kind === "unary"
    ? `    Task<${adapter.response_type}> ${adapter.client_method}(${adapter.request_type} request, CancellationToken cancellationToken);`
    : `    IAsyncEnumerable<${adapter.event_type}> ${adapter.client_method}(${adapter.request_type} request, CancellationToken cancellationToken);`;
}).join("\n");
const codecMethods = [
  ...unaryAdapters.map((operation) => `    JsonElement Project(${operation.native_adapter.request_type} request, ${operation.native_adapter.response_type} response);`),
  ...streamAdapters.map((operation) => `    JsonElement Project(${operation.native_adapter.request_type} request, ${operation.native_adapter.event_type} applicationEvent);`),
].join("\n");
const unaryDispatch = unaryAdapters.map((operation) => `        (${literal(operation.operation)}, ${operation.native_adapter.request_type} typedRequest, ${operation.native_adapter.response_type} typedResponse) => codec.Project(typedRequest, typedResponse),`).join("\n");
const streamDispatch = streamAdapters.map((operation) => `        (${literal(operation.operation)}, ${operation.native_adapter.request_type} typedRequest, ${operation.native_adapter.event_type} typedEvent) => codec.Project(typedRequest, typedEvent),`).join("\n");
const hostMethods = hostAdapters.map((operation) => `    Task<${operation.native_adapter.response_type}> ${operation.native_adapter.client_method}(${operation.native_adapter.request_type} request, CancellationToken cancellationToken);`).join("\n");
const rendererHandlerName = (operation) => `${operation.split(/[.-]/u).map(pascal).join("")}Async`;
const rendererRequestHandlerMethods = requestMessages.map((message) => `    Task<JsonElement?> ${rendererHandlerName(message.operation)}(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken);`).join("\n");
const rendererRequestDispatchCases = requestMessages.map((message) => `        ${literal(message.operation)} => handler.${rendererHandlerName(message.operation)}(envelope, payload, cancellationToken),`).join("\n");
const csharp = `// <auto-generated />
#nullable enable
using System.Text.Json;
using Infinium.Contracts.Protobuf.Application.V1;

namespace Infinium.Application.Runtime;

public sealed record GeneratedRendererMessage(string Operation, string NativeTarget, string MessageKind, string Direction, string PayloadShape, string SchemaDefinition);

public static class GeneratedRendererOperationCatalog
{
    public const string RegistryVersion = ${literal(registry.schema_version)};
    public const string RendererContractVersion = ${literal(registry.renderer_contract_version)};
    public const string RegistrySha256 = ${literal(registrySha256)};
    public static IReadOnlyList<GeneratedRendererMessage> Messages { get; } =
    [
${csharpEntries}
    ];
}

public interface IGeneratedRendererApplicationClient
{
${clientMethods}
}

public interface IGeneratedRendererProjectionCodec
{
${codecMethods}
}

public sealed class GeneratedRendererProjectionAdapter(IGeneratedRendererProjectionCodec codec)
{
    public JsonElement Project(string operation, object request, object response) => (operation, request, response) switch
    {
${unaryDispatch}
        _ => throw new InvalidDataException("The native projection is not registered for this renderer operation."),
    };

    public JsonElement ProjectEvent(string operation, object request, object applicationEvent) => (operation, request, applicationEvent) switch
    {
${streamDispatch}
        _ => throw new InvalidDataException("The native event is not registered for this renderer operation."),
    };
}

public interface IGeneratedRendererRequestHandler
{
${rendererRequestHandlerMethods}
}

public static class GeneratedRendererRequestDispatcher
{
    public static Task<JsonElement?> DispatchAsync(IGeneratedRendererRequestHandler handler, RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
        => envelope.Operation switch
        {
${rendererRequestDispatchCases}
            _ => throw new InvalidDataException("The renderer request has no generated desktop adapter."),
        };
}

public interface IGeneratedRendererHostControl
{
${hostMethods}
}

public sealed record RendererCancellationRequest(string TargetRequestId);
public sealed record RendererResyncRequest(string SubscriptionId, string CurrentProjectionVersion);
public sealed record RendererHostControlReceipt(string Outcome, string? CurrentProjectionVersion);
`;

const outputs = new Map([
  [resolve(root, "contracts/renderer/renderer-operation-registry.v1.json"), registryContent],
  [resolve(root, "src/Infinium.Frontend/generated/renderer-contract.generated.ts"), typescript.replaceAll("\r\n", "\n")],
  [resolve(root, "src/Infinium.Application/Runtime/RendererOperationCatalog.Generated.cs"), csharp.replaceAll("\r\n", "\n")],
]);
if (process.argv.includes("--check")) {
  const stale = [...outputs].filter(([path, content]) => readFileSync(path, "utf8").replaceAll("\r\n", "\n") !== content).map(([path]) => path);
  if (stale.length > 0) throw new Error(`Generated renderer contracts are stale: ${stale.join(", ")}`);
} else {
  mkdirSync(resolve(root, "src/Infinium.Frontend/generated"), { recursive: true });
  for (const [path, content] of outputs) writeFileSync(path, content, "utf8");
}
