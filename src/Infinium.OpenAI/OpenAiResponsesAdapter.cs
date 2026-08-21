using System.Buffers;
using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.OpenAI;

public sealed record OpenAiResponsesRequest(
    ProviderOperationKind OperationKind,
    string Instructions,
    string UntrustedInput,
    JsonElement OutputSchema,
    long MaximumOutputTokens,
    string SafetyIdentifier);

public sealed record OpenAiRateHeader(string Name, long Value);

public static class OpenAiStagedResponseEnvelope
{
    private static ReadOnlySpan<byte> Magic => "INFWP5\0"u8;

    public static byte[] Create(OpenAiResponsesResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        byte[] raw = result.RawResponseBytes ?? [];
        if (raw.Length == 0 && result.State == ProviderResponseState.Completed)
        {
            throw new InvalidOperationException("A completed staged response requires retained raw bytes.");
        }
        IReadOnlyList<OpenAiRateHeader> sanitizedHeaders = OpenAiResponsesAdapter.SanitizeRetainedHeaders(result.RateHeaders);
        byte[] headers = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "infinium.openai.response-headers/v2",
            state = result.State,
            failure_stage = result.FailureStage,
            transport_disposition = result.TransportDisposition,
            http_status = result.HttpStatus,
            response_bytes_existed = result.ResponseBytesExisted,
            response_bytes_observed_lower_bound = result.ResponseBytesObservedLowerBound,
            retained_response_bytes = result.RawResponseBytes?.LongLength ?? 0,
            provider_response_id = OpenAiResponsesAdapter.SanitizeProviderRequestId(result.ProviderResponseId),
            provider_request_id = OpenAiResponsesAdapter.SanitizeProviderRequestId(result.ProviderRequestId),
            returned_model = OpenAiResponsesAdapter.SanitizeProviderErrorField(result.ReturnedModel),
            returned_service_tier = OpenAiResponsesAdapter.SanitizeProviderErrorField(result.ReturnedServiceTier),
            refusal_code = OpenAiResponsesAdapter.SanitizeProviderErrorField(result.RefusalCode),
            incomplete_reason = OpenAiResponsesAdapter.SanitizeProviderErrorField(result.IncompleteReason),
            provider_error_type = OpenAiResponsesAdapter.SanitizeProviderErrorField(result.ProviderErrorType),
            provider_error_code = result.RawResponseBytes is null ? null
                : OpenAiResponsesAdapter.SanitizeProviderErrorField(result.ErrorCode),
            local_failure_code = result.RawResponseBytes is null
                ? OpenAiResponsesAdapter.SanitizeProviderErrorField(result.ErrorCode) : null,
            requested_output_schema = result.RequestedOutputSchemaBytes,
            usage = result.Usage,
            dns_resolution_count = result.DnsResolutionCount,
            network_used = result.NetworkUsed,
            send_count = result.SendCount,
            headers = sanitizedHeaders.Select(item => new { name = item.Name, value = item.Value }).ToArray(),
        });
        byte[] envelope = new byte[checked(Magic.Length + 8 + raw.Length + headers.Length)];
        Magic.CopyTo(envelope);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(Magic.Length), raw.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(Magic.Length + 4), headers.Length);
        raw.CopyTo(envelope.AsSpan(Magic.Length + 8));
        headers.CopyTo(envelope.AsSpan(Magic.Length + 8 + raw.Length));
        return envelope;
    }

    public static bool TryRead(ReadOnlySpan<byte> envelope, out byte[] raw, out byte[] headers)
    {
        raw = [];
        headers = [];
        if (envelope.Length < Magic.Length + 8 || !envelope[..Magic.Length].SequenceEqual(Magic))
        {
            return false;
        }
        int rawLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(envelope[Magic.Length..]);
        int headerLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(envelope[(Magic.Length + 4)..]);
        if (rawLength < 0 || headerLength <= 0 || Magic.Length + 8L + rawLength + headerLength != envelope.Length)
        {
            throw new InvalidDataException("The staged Responses envelope length is invalid.");
        }
        raw = envelope.Slice(Magic.Length + 8, rawLength).ToArray();
        headers = envelope.Slice(Magic.Length + 8 + rawLength, headerLength).ToArray();
        return true;
    }

    public static string? ProviderRequestId(ReadOnlySpan<byte> headerReceipt)
    {
        using JsonDocument document = JsonDocument.Parse(headerReceipt.ToArray());
        return document.RootElement.TryGetProperty("provider_request_id", out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? OpenAiResponsesAdapter.SanitizeProviderRequestId(value.GetString())
            : null;
    }

    public static int? HttpStatus(ReadOnlySpan<byte> headerReceipt)
    {
        using JsonDocument document = JsonDocument.Parse(headerReceipt.ToArray());
        JsonElement value = document.RootElement.GetProperty("http_status");
        return value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();
    }

    public static IReadOnlyList<OpenAiRateHeader> RateHeaders(ReadOnlySpan<byte> headerReceipt)
    {
        using JsonDocument document = JsonDocument.Parse(headerReceipt.ToArray());
        OpenAiRateHeader[] parsed = document.RootElement.GetProperty("headers").EnumerateArray()
            .Select(item => new OpenAiRateHeader(
                item.GetProperty("name").GetString()!, item.GetProperty("value").GetInt64()))
            .ToArray();
        return OpenAiResponsesAdapter.SanitizeRetainedHeaders(parsed);
    }

    public static OpenAiResponsesResult Replay(ReadOnlySpan<byte> raw, ReadOnlySpan<byte> headerReceipt, string clientRequestId)
    {
        int? status = HttpStatus(headerReceipt);
        IReadOnlyList<OpenAiRateHeader> rateHeaders = RateHeaders(headerReceipt);
        using JsonDocument document = JsonDocument.Parse(headerReceipt.ToArray());
        JsonElement root = document.RootElement;
        bool networkUsed = root.TryGetProperty("network_used", out JsonElement networkValue)
            && networkValue.ValueKind == JsonValueKind.True;
        int sendCount = root.TryGetProperty("send_count", out JsonElement sendValue)
            && sendValue.ValueKind == JsonValueKind.Number ? sendValue.GetInt32() : 0;
        if (!raw.IsEmpty)
        {
            byte[] retainedSchema = root.TryGetProperty("requested_output_schema", out JsonElement schemaValue)
                && schemaValue.ValueKind == JsonValueKind.String
                ? schemaValue.GetBytesFromBase64()
                : [];
            if (status is null)
            {
                throw new InvalidDataException("A retained raw provider response requires an HTTP status.");
            }
            return OpenAiResponsesResponseCodec.Replay(
                raw, status.Value, clientRequestId, ProviderRequestId(headerReceipt), rateHeaders, retainedSchema) with
            {
                DnsResolutionCount = root.GetProperty("dns_resolution_count").GetInt32(),
                NetworkUsed = networkUsed,
                SendCount = sendCount,
            };
        }
        ProviderResponseState state = root.GetProperty("state").Deserialize<ProviderResponseState>();
        ProviderUsageContract usage = root.GetProperty("usage").Deserialize<ProviderUsageContract>()
            ?? throw new InvalidDataException("The response usage receipt is absent.");
        bool transportMayHaveStarted = root.GetProperty("transport_disposition").GetString()
            is "may-have-started-no-response" or "response-received";
        return new OpenAiResponsesResult(state, transportMayHaveStarted, false, status, null, String(root, "provider_response_id"), clientRequestId,
            String(root, "provider_request_id"), String(root, "returned_model"), String(root, "returned_service_tier"),
            String(root, "refusal_code"), String(root, "incomplete_reason"),
            String(root, "provider_error_code") ?? String(root, "local_failure_code"), usage,
            rateHeaders, false, String(root, "failure_stage") ?? "provider-transport", networkUsed, sendCount)
        {
            ProviderErrorType = String(root, "provider_error_type"),
            ResponseBytesExisted = root.GetProperty("response_bytes_existed").GetBoolean(),
            ResponseBytesObservedLowerBound = root.GetProperty("response_bytes_observed_lower_bound").GetInt64(),
            RequestedOutputSchemaBytes = root.TryGetProperty("requested_output_schema", out JsonElement schema)
                && schema.ValueKind == JsonValueKind.String ? schema.GetBytesFromBase64() : [],
            DnsResolutionCount = root.GetProperty("dns_resolution_count").GetInt32(),
        };
    }

    private static string? String(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

public sealed record OpenAiResponsesResult(
    ProviderResponseState State,
    bool TransportMayHaveStarted,
    bool RetryPermitted,
    int? HttpStatus,
    byte[]? RawResponseBytes,
    string? ProviderResponseId,
    string? ClientRequestId,
    string? ProviderRequestId,
    string? ReturnedModel,
    string? ReturnedServiceTier,
    string? RefusalCode,
    string? IncompleteReason,
    string? ErrorCode,
    ProviderUsageContract Usage,
    IReadOnlyList<OpenAiRateHeader> RateHeaders,
    bool Admitted,
    string AdmissionReason,
    bool NetworkUsed,
    int SendCount)
{
    public byte[]? RequestedOutputSchemaBytes { get; init; }
    public int DnsResolutionCount { get; init; }
    public string? ProviderErrorType { get; init; }
    public bool ResponseBytesExisted { get; init; } = RawResponseBytes is not null;
    public long ResponseBytesObservedLowerBound { get; init; } = RawResponseBytes?.LongLength ?? 0;
    public string FailureStage => State == ProviderResponseState.Completed && Admitted
        ? "none"
        : HttpStatus is null ? "provider-transport" : "provider-response";
    public string TransportDisposition => HttpStatus is not null
        ? "response-received"
        : TransportMayHaveStarted ? "may-have-started-no-response" : "pre-send-known";

    public byte[] ToSecretFreeDiagnosticBytes() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        state = State.ToString(),
        transport_may_have_started = TransportMayHaveStarted,
        retry_permitted = RetryPermitted,
        failure_stage = FailureStage,
        transport_disposition = TransportDisposition,
        http_status = HttpStatus,
        response_bytes_existed = ResponseBytesExisted,
        response_bytes_observed_lower_bound = ResponseBytesObservedLowerBound,
        raw_response_bytes = RawResponseBytes?.LongLength,
        provider_response_id = OpenAiResponsesAdapter.SanitizeProviderRequestId(ProviderResponseId),
        client_request_id = OpenAiResponsesAdapter.SanitizeProviderRequestId(ClientRequestId),
        provider_request_id = OpenAiResponsesAdapter.SanitizeProviderRequestId(ProviderRequestId),
        returned_model = OpenAiResponsesAdapter.SanitizeProviderErrorField(ReturnedModel),
        returned_service_tier = OpenAiResponsesAdapter.SanitizeProviderErrorField(ReturnedServiceTier),
        refusal_code = OpenAiResponsesAdapter.SanitizeProviderErrorField(RefusalCode),
        incomplete_reason = OpenAiResponsesAdapter.SanitizeProviderErrorField(IncompleteReason),
        provider_error_type = OpenAiResponsesAdapter.SanitizeProviderErrorField(ProviderErrorType),
        provider_error_code = RawResponseBytes is null ? null
            : OpenAiResponsesAdapter.SanitizeProviderErrorField(ErrorCode),
        local_failure_code = RawResponseBytes is null
            ? OpenAiResponsesAdapter.SanitizeProviderErrorField(ErrorCode) : null,
        admitted = Admitted,
        admission_reason = AdmissionReason,
        network_used = NetworkUsed,
        send_count = SendCount,
    });
}

public interface IOpenAiResponsesTransport
{
    public Task<OpenAiResponsesResult> SendOnceAsync(
        ReadOnlyMemory<byte> canonicalRequest,
        ReadOnlyMemory<byte> secret,
        ProviderFiniteLimitsContract limits,
        string clientRequestId,
        CancellationToken cancellationToken);
}

public static class OpenAiResponsesCanonicalSerializer
{
    public const string Model = "gpt-5.6-sol";
    public const string ServiceTier = "default";
    public const string EndpointPath = "/v1/responses";
    public const string InputBoundPolicyId = "openai-responses-o200k-byte-envelope";
    public const string InputBoundPolicyVersion = "v2";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = false,
    };

    public static byte[] Serialize(OpenAiResponsesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string operationName = request.OperationKind switch
        {
            ProviderOperationKind.TransportQualification => "transport_qualification",
            ProviderOperationKind.SourceClaimExtraction => "source_claim_extraction",
            ProviderOperationKind.CandidateInvestigation => "candidate_investigation",
            _ => throw new InvalidOperationException("The Responses operation is not part of the closed M1 profile."),
        };
        long maximumOutputTokens = request.OperationKind == ProviderOperationKind.TransportQualification ? 256 : 4_096;
        if (string.IsNullOrWhiteSpace(request.Instructions) || string.IsNullOrWhiteSpace(request.UntrustedInput)
            || request.Instructions.Length > 8_192 || request.UntrustedInput.Length > 48_000
            || request.MaximumOutputTokens <= 0 || request.MaximumOutputTokens > maximumOutputTokens
            || !ProductUserSafetyIdentifier.IsValidProjection(request.SafetyIdentifier)
            || request.OutputSchema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The Responses request exceeds its closed context or output bounds.");
        }
        RejectAnswerBearingSchemaNames(request.OutputSchema);
        if (!ClosedJsonSchemaValidator.ValidateSchema(request.OutputSchema))
        {
            throw new InvalidOperationException("The output schema is outside the exact supported strict subset.");
        }

        ArrayBufferWriter<byte> bytes = new();
        using (Utf8JsonWriter writer = new(bytes, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("model", Model);
            writer.WriteString("safety_identifier", request.SafetyIdentifier);
            writer.WritePropertyName("reasoning");
            writer.WriteStartObject();
            writer.WriteString("effort", "medium");
            writer.WriteString("context", "current_turn");
            writer.WriteString("mode", "standard");
            writer.WriteEndObject();
            writer.WritePropertyName("text");
            writer.WriteStartObject();
            writer.WritePropertyName("format");
            writer.WriteStartObject();
            writer.WriteString("type", "json_schema");
            writer.WriteString("name", operationName);
            writer.WriteBoolean("strict", true);
            writer.WritePropertyName("schema");
            WriteCanonicalJson(writer, request.OutputSchema);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteBoolean("store", false);
            writer.WriteString("service_tier", ServiceTier);
            writer.WriteBoolean("background", false);
            writer.WriteBoolean("stream", false);
            writer.WriteString("tool_choice", "none");
            writer.WritePropertyName("tools");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteString("truncation", "disabled");
            writer.WriteNumber("max_output_tokens", request.MaximumOutputTokens);
            writer.WritePropertyName("prompt_cache_options");
            writer.WriteStartObject();
            writer.WriteString("mode", "explicit");
            writer.WriteEndObject();
            writer.WritePropertyName("input");
            writer.WriteStartArray();
            WriteMessage(writer, "developer", request.Instructions);
            WriteMessage(writer, "user", "BEGIN_UNTRUSTED_EVIDENCE\n" + request.UntrustedInput + "\nEND_UNTRUSTED_EVIDENCE");
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return bytes.WrittenSpan.ToArray();
    }

    public static string Fingerprint(ReadOnlySpan<byte> canonicalRequest) =>
        Convert.ToHexStringLower(SHA256.HashData(canonicalRequest));

    public static void ValidateExactProfile(ReadOnlySpan<byte> requestBytes, long maximumOutputTokens)
    {
        using JsonDocument document = JsonDocument.Parse(requestBytes.ToArray());
        JsonElement root = document.RootElement;
        string[] exactNames = ["model", "safety_identifier", "reasoning", "text", "store", "service_tier", "background", "stream",
            "tool_choice", "tools", "truncation", "max_output_tokens", "prompt_cache_options", "input"];
        if (root.ValueKind != JsonValueKind.Object
            || !root.EnumerateObject().Select(property => property.Name).SequenceEqual(exactNames, StringComparer.Ordinal)
            || root.GetProperty("model").GetString() != Model
            || !ProductUserSafetyIdentifier.IsValidProjection(root.GetProperty("safety_identifier").GetString())
            || root.GetProperty("store").GetBoolean()
            || root.GetProperty("service_tier").GetString() != ServiceTier
            || root.GetProperty("background").GetBoolean() || root.GetProperty("stream").GetBoolean()
            || root.GetProperty("tool_choice").GetString() != "none"
            || root.GetProperty("tools").GetArrayLength() != 0
            || root.GetProperty("truncation").GetString() != "disabled"
            || root.GetProperty("max_output_tokens").GetInt64() != maximumOutputTokens)
        {
            throw new InvalidDataException("The canonical request does not match the exact M1 Responses profile.");
        }
        JsonElement reasoning = root.GetProperty("reasoning");
        JsonElement cache = root.GetProperty("prompt_cache_options");
        JsonElement text = root.GetProperty("text");
        JsonElement format = text.GetProperty("format");
        long operationCeiling = format.GetProperty("name").GetString() switch
        {
            "transport_qualification" => 256,
            "source_claim_extraction" or "candidate_investigation" => 4_096,
            _ => 0,
        };
        JsonElement outputSchema = format.GetProperty("schema");
        byte[] canonicalSchema = Canonicalize(outputSchema);
        if (!ExactNames(reasoning, ["effort", "context", "mode"])
            || !ExactNames(text, ["format"])
            || !ExactNames(format, ["type", "name", "strict", "schema"])
            || !ExactNames(cache, ["mode"])
            || reasoning.GetProperty("effort").GetString() != "medium"
            || reasoning.GetProperty("context").GetString() != "current_turn"
            || reasoning.GetProperty("mode").GetString() != "standard"
            || cache.EnumerateObject().Count() != 1 || cache.GetProperty("mode").GetString() != "explicit"
            || format.GetProperty("type").GetString() != "json_schema" || !format.GetProperty("strict").GetBoolean()
            || !ClosedJsonSchemaValidator.ValidateSchema(outputSchema)
            || !Encoding.UTF8.GetBytes(outputSchema.GetRawText()).AsSpan().SequenceEqual(canonicalSchema)
            || maximumOutputTokens <= 0 || maximumOutputTokens > operationCeiling)
        {
            throw new InvalidDataException("Stateless reasoning, cache-off, and strict output controls must be explicit.");
        }
    }

    private static bool ExactNames(JsonElement value, string[] names) =>
        value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal);

    public static byte[] OutputSchemaBytes(ReadOnlySpan<byte> requestBytes)
    {
        using JsonDocument document = JsonDocument.Parse(requestBytes.ToArray());
        return Encoding.UTF8.GetBytes(document.RootElement.GetProperty("text").GetProperty("format")
            .GetProperty("schema").GetRawText());
    }

    private static void WriteMessage(Utf8JsonWriter writer, string role, string text)
    {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WritePropertyName("content");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("type", "input_text");
        writer.WriteString("text", text);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static byte[] Canonicalize(JsonElement value)
    {
        ArrayBufferWriter<byte> bytes = new();
        using Utf8JsonWriter writer = new(bytes, WriterOptions);
        WriteCanonicalJson(writer, value);
        writer.Flush();
        return bytes.WrittenSpan.ToArray();
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidOperationException("The output schema contains an unsupported JSON token.");
        }
    }

    private static void RejectAnswerBearingSchemaNames(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (property.Name.Contains("oracle", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("expected", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("credential", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("authorization_header", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Output schemas cannot carry answer or credential authority.");
                }
                RejectAnswerBearingSchemaNames(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                RejectAnswerBearingSchemaNames(item);
            }
        }
    }
}

public sealed class OpenAiResponsesAdapter : IOpenAiResponsesTransport, IDisposable
{
    private static readonly Uri ProductionEndpoint = new("https://api.openai.com/v1/responses", UriKind.Absolute);
    private static readonly HashSet<string> NumericResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "x-ratelimit-limit-requests", "x-ratelimit-remaining-requests",
        "x-ratelimit-limit-input-tokens", "x-ratelimit-remaining-input-tokens",
        "x-ratelimit-limit-output-tokens", "x-ratelimit-remaining-output-tokens",
        "x-ratelimit-limit-tokens", "x-ratelimit-remaining-tokens",
        "openai-processing-ms",
    };
    private const long MaximumRetainedHeaderQuantity = 1_000_000_000_000;
    private const long MaximumRetainedProcessingMilliseconds = 120_000;
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly bool ownsClient;
    private readonly ProductionTransportObservation? productionObservation;
    private int consumed;
    public bool UsesPerOperationDeadlineOnly => client.Timeout == Timeout.InfiniteTimeSpan;
    public static bool ProxyFallbackEnabled => false;
    public static bool RedirectsEnabled => false;
    public static bool RetriesEnabled => false;
    public static bool ProviderToolsEnabled => false;

    private OpenAiResponsesAdapter(HttpClient client, Uri endpoint, bool ownsClient,
        ProductionTransportObservation? productionObservation = null)
    {
        this.client = client;
        this.endpoint = endpoint;
        this.ownsClient = ownsClient;
        this.productionObservation = productionObservation;
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public static OpenAiResponsesAdapter CreateProduction()
    {
        ProductionTransportObservation observation = new();
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
            MaxConnectionsPerServer = 1,
            PooledConnectionLifetime = TimeSpan.Zero,
            ConnectCallback = observation.ConnectOnceAsync,
        };
        return new(new HttpClient(handler, disposeHandler: true), ProductionEndpoint, ownsClient: true, observation);
    }

    public static OpenAiResponsesAdapter CreateDeterministicLoopback(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttp
            || endpoint.AbsolutePath != OpenAiResponsesCanonicalSerializer.EndpointPath
            || !IPAddress.TryParse(endpoint.Host, out IPAddress? address) || !IPAddress.IsLoopback(address)
            || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new InvalidOperationException("Offline transport accepts only a literal-IP loopback /v1/responses endpoint.");
        }
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
            MaxConnectionsPerServer = 1,
            PooledConnectionLifetime = TimeSpan.Zero,
        };
        return new(new HttpClient(handler, disposeHandler: true), endpoint, ownsClient: true);
    }

    public async Task<OpenAiResponsesResult> SendOnceAsync(
        ReadOnlyMemory<byte> canonicalRequest,
        ReadOnlyMemory<byte> secret,
        ProviderFiniteLimitsContract limits,
        string clientRequestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (Interlocked.Exchange(ref consumed, 1) != 0)
        {
            throw new InvalidOperationException("A Responses adapter instance is one-shot and cannot retry.");
        }

        if (canonicalRequest.IsEmpty || canonicalRequest.Length > limits.MaximumRequestBytes
            || secret.IsEmpty || secret.Length > 2_560 || string.IsNullOrWhiteSpace(clientRequestId)
            || clientRequestId.Length > 128 || limits.MaximumDispatchCount != 1)
        {
            throw new InvalidOperationException("The one-shot request, credential, or dispatch bound is invalid.");
        }

        OpenAiResponsesCanonicalSerializer.ValidateExactProfile(canonicalRequest.Span, limits.MaximumOutputTokens);

        using HttpRequestMessage request = new(HttpMethod.Post, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Client-Request-Id", clientRequestId);
        string bearer = Encoding.ASCII.GetString(secret.Span);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        request.Content = new ByteArrayContent(canonicalRequest.ToArray());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(limits.DeadlineMilliseconds));
        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            byte[]? raw = await ReadBoundedAsync(response.Content, limits.MaximumRawResponseBytes, deadline.Token)
                .ConfigureAwait(false);
            if (raw is null)
            {
                if (ContainsSecretEcho(response, [], secret.Span))
                {
                    return Failure(ProviderResponseState.Unknown, (int)response.StatusCode, true,
                        "security_secret_echo", [], clientRequestId, null, sendCount: 1) with
                    {
                        ResponseBytesExisted = true,
                        ResponseBytesObservedLowerBound = checked(limits.MaximumRawResponseBytes + 1),
                        DnsResolutionCount = DnsResolutionCount(),
                    };
                }
                IReadOnlyList<OpenAiRateHeader> oversizedRateHeaders = CaptureHeaders(response);
                return Failure(ProviderResponseState.Oversized, (int)response.StatusCode, true, "response_too_large",
                    oversizedRateHeaders, clientRequestId, ProviderRequestId(response), sendCount: 1) with
                {
                    ResponseBytesExisted = true,
                    ResponseBytesObservedLowerBound = checked(limits.MaximumRawResponseBytes + 1),
                    RequestedOutputSchemaBytes = OpenAiResponsesCanonicalSerializer.OutputSchemaBytes(canonicalRequest.Span),
                    DnsResolutionCount = DnsResolutionCount(),
                };
            }

            if (ContainsSecretEcho(response, raw, secret.Span))
            {
                long observedBytes = raw.LongLength;
                CryptographicOperations.ZeroMemory(raw);
                return Failure(ProviderResponseState.Unknown, (int)response.StatusCode, true,
                    "security_secret_echo", [], clientRequestId, null, sendCount: 1) with
                {
                    ResponseBytesExisted = true,
                    ResponseBytesObservedLowerBound = observedBytes,
                    DnsResolutionCount = DnsResolutionCount(),
                };
            }

            IReadOnlyList<OpenAiRateHeader> rateHeaders = CaptureHeaders(response);
            return OpenAiResponsesResponseCodec.Parse(raw, (int)response.StatusCode, clientRequestId,
                ProviderRequestId(response), rateHeaders,
                OpenAiResponsesCanonicalSerializer.OutputSchemaBytes(canonicalRequest.Span)) with
            { DnsResolutionCount = DnsResolutionCount() };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(ProviderResponseState.Unknown, null, true, "deadline_ambiguous", [], clientRequestId, null, 1)
                with
            { DnsResolutionCount = DnsResolutionCount() };
        }
        catch (HttpRequestException)
        {
            return Failure(ProviderResponseState.Unknown, null, true, "transport_ambiguous", [], clientRequestId, null, 1)
                with
            { DnsResolutionCount = DnsResolutionCount() };
        }
    }

    private int DnsResolutionCount() => productionObservation?.DnsResolutionCount ?? 0;

    private sealed class ProductionTransportObservation
    {
        private int dnsResolutionCount;
        internal int DnsResolutionCount => Volatile.Read(ref dnsResolutionCount);

        internal async ValueTask<Stream> ConnectOnceAsync(
            SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref dnsResolutionCount) != 1)
            {
                throw new HttpRequestException("The one-shot production transport attempted a second DNS resolution.");
            }
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(
                context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
            Exception? last = null;
            foreach (IPAddress address in addresses)
            {
                Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception error) when (error is SocketException or OperationCanceledException)
                {
                    last = error;
                    socket.Dispose();
                    if (error is OperationCanceledException) { throw; }
                }
            }
            throw new HttpRequestException("The exact production DNS resolution returned no reachable address.", last);
        }
    }

    public void Dispose()
    {
        if (ownsClient)
        {
            client.Dispose();
        }
    }

    private OpenAiResponsesResult Failure(ProviderResponseState state, int? status, bool started, string error,
        IReadOnlyList<OpenAiRateHeader> headers, string clientRequestId, string? providerRequestId, int sendCount) =>
        new(state, started, false, status, null, null, clientRequestId, providerRequestId, null, null, null, null,
            state == ProviderResponseState.Oversized ? null : error, state == ProviderResponseState.Oversized
                ? DispatchedUnknownUsage(UsageReceiptState.Partial, headers.Any(IsRateHeader))
                : UnavailableUsage(started ? UsageReceiptState.Ambiguous : UsageReceiptState.NotDispatched), headers,
            false, error, endpoint != ProductionEndpoint || started, sendCount);

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, long maximumBytes, CancellationToken token)
    {
        if (maximumBytes <= 0 || maximumBytes > int.MaxValue)
        {
            throw new InvalidOperationException("Invalid response bound.");
        }

        await using Stream stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using MemoryStream result = new(checked((int)Math.Min(maximumBytes, 64 * 1024)));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (result.Length + read > maximumBytes)
                {
                    return null;
                }

                result.Write(buffer, 0, read);
            }
            return result.ToArray();
        }
        finally
        {
            if (result.TryGetBuffer(out ArraySegment<byte> retainedBuffer) && retainedBuffer.Array is not null)
            {
                CryptographicOperations.ZeroMemory(retainedBuffer.Array.AsSpan(0, checked((int)result.Length)));
            }
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static bool ContainsSecretEcho(
        HttpResponseMessage response,
        ReadOnlySpan<byte> raw,
        ReadOnlySpan<byte> secret)
    {
        byte[] base64 = [];
        byte[] base64Url = [];
        try
        {
            base64 = EncodeBase64(secret);
            base64Url = base64.ToArray();
            ReplaceBase64UrlAlphabet(base64Url);
            if (ContainsNormalizedRepresentation(raw, secret, base64, base64Url)
                || ContainsDecodedJsonRepresentation(raw, secret, base64, base64Url))
            {
                return true;
            }

            foreach ((string _, IEnumerable<string> values) in response.Headers.Concat(response.Content.Headers))
            {
                foreach (string value in values)
                {
                    byte[] headerBytes = Encoding.UTF8.GetBytes(value);
                    try
                    {
                        if (ContainsNormalizedRepresentation(headerBytes, secret, base64, base64Url))
                        {
                            return true;
                        }
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(headerBytes);
                    }
                }
            }

            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(base64);
            CryptographicOperations.ZeroMemory(base64Url);
        }
    }

    private static bool ContainsNormalizedRepresentation(
        ReadOnlySpan<byte> value,
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> base64,
        ReadOnlySpan<byte> base64Url) =>
        value.IndexOf(secret) >= 0
        || ContainsBase64(value, base64)
        || ContainsBase64(value, base64Url)
        || ContainsPercentDecoded(value, secret)
        || ContainsJsonEscaped(value, secret);

    private static bool ContainsDecodedJsonRepresentation(
        ReadOnlySpan<byte> value,
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> base64,
        ReadOnlySpan<byte> base64Url)
    {
        try
        {
            Utf8JsonReader reader = new(value, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            while (reader.Read())
            {
                if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
                {
                    continue;
                }
                int maximum = reader.HasValueSequence
                    ? checked((int)reader.ValueSequence.Length)
                    : reader.ValueSpan.Length;
                byte[] decoded = new byte[maximum];
                try
                {
                    int written = reader.CopyString(decoded);
                    if (ContainsNormalizedRepresentation(decoded.AsSpan(0, written), secret, base64, base64Url))
                    {
                        return true;
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(decoded);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON is still scanned bytewise by ContainsJsonEscaped.
        }
        return false;
    }

    private static byte[] EncodeBase64(ReadOnlySpan<byte> value)
    {
        byte[] encoded = new byte[Base64.GetMaxEncodedToUtf8Length(value.Length)];
        OperationStatus status = Base64.EncodeToUtf8(value, encoded, out int consumed, out int written);
        if (status != OperationStatus.Done || consumed != value.Length || written != encoded.Length)
        {
            CryptographicOperations.ZeroMemory(encoded);
            throw new InvalidOperationException("The bounded secret Base64 normalization failed.");
        }
        return encoded;
    }

    private static void ReplaceBase64UrlAlphabet(Span<byte> value)
    {
        value.Replace((byte)'+', (byte)'-');
        value.Replace((byte)'/', (byte)'_');
    }

    private static bool ContainsBase64(ReadOnlySpan<byte> value, ReadOnlySpan<byte> encoded)
    {
        int unpaddedLength = encoded.Length;
        while (unpaddedLength > 0 && encoded[unpaddedLength - 1] == '=')
        {
            unpaddedLength--;
        }
        return value.IndexOf(encoded) >= 0
            || unpaddedLength != encoded.Length && value.IndexOf(encoded[..unpaddedLength]) >= 0;
    }

    private static bool ContainsPercentDecoded(ReadOnlySpan<byte> value, ReadOnlySpan<byte> secret)
    {
        for (int start = 0; start < value.Length; start++)
        {
            int position = start;
            bool matched = true;
            foreach (byte expected in secret)
            {
                if (position < value.Length && value[position] == expected)
                {
                    position++;
                }
                else if (position + 2 < value.Length && value[position] == '%'
                    && TryHex(value[position + 1], out int high)
                    && TryHex(value[position + 2], out int low)
                    && (byte)((high << 4) | low) == expected)
                {
                    position += 3;
                }
                else
                {
                    matched = false;
                    break;
                }
            }
            if (matched)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsJsonEscaped(ReadOnlySpan<byte> value, ReadOnlySpan<byte> secret)
    {
        for (int start = 0; start < value.Length; start++)
        {
            int position = start;
            bool matched = true;
            foreach (byte expected in secret)
            {
                if (position < value.Length && value[position] == expected)
                {
                    position++;
                    continue;
                }
                if (position + 1 >= value.Length || value[position++] != '\\')
                {
                    matched = false;
                    break;
                }
                byte escape = value[position++];
                byte? decoded = escape switch
                {
                    (byte)'"' => (byte)'"',
                    (byte)'\\' => (byte)'\\',
                    (byte)'/' => (byte)'/',
                    (byte)'b' => (byte)'\b',
                    (byte)'f' => (byte)'\f',
                    (byte)'n' => (byte)'\n',
                    (byte)'r' => (byte)'\r',
                    (byte)'t' => (byte)'\t',
                    _ => null,
                };
                if (decoded == expected)
                {
                    continue;
                }
                if (escape == 'u' && position + 3 < value.Length
                    && TryHex(value[position], out int a)
                    && TryHex(value[position + 1], out int b)
                    && TryHex(value[position + 2], out int c)
                    && TryHex(value[position + 3], out int d)
                    && a == 0 && b == 0 && (byte)((c << 4) | d) == expected)
                {
                    position += 4;
                    continue;
                }
                matched = false;
                break;
            }
            if (matched)
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryHex(byte value, out int decoded)
    {
        decoded = value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - '0',
            >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
            >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
            _ => -1,
        };
        return decoded >= 0;
    }

    private static OpenAiRateHeader[] CaptureHeaders(HttpResponseMessage response)
    {
        List<OpenAiRateHeader> result = [];
        foreach ((string name, IEnumerable<string> values) in response.Headers)
        {
            if (!NumericResponseHeaders.Contains(name))
            {
                continue;
            }
            string[] exactValues = values.ToArray();
            if (exactValues.Length == 1 && exactValues[0].Length is > 0 and <= 32
                && exactValues[0].All(char.IsAsciiDigit)
                && long.TryParse(exactValues[0], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out long quantity)
                && quantity >= 0 && quantity <= MaximumHeaderValue(name))
            {
                result.Add(new(name.ToLowerInvariant(), quantity));
            }
        }
        return SanitizeRetainedHeaders(result).ToArray();
    }

    internal static IReadOnlyList<OpenAiRateHeader> SanitizeRetainedHeaders(IEnumerable<OpenAiRateHeader> headers)
    {
        OpenAiRateHeader[] unique = headers.Where(header => NumericResponseHeaders.Contains(header.Name)
                && header.Value >= 0 && header.Value <= MaximumHeaderValue(header.Name))
            .GroupBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .Select(group => new OpenAiRateHeader(group.Key.ToLowerInvariant(), group.Single().Value))
            .ToArray();
        Dictionary<string, long> values = unique.ToDictionary(header => header.Name, header => header.Value,
            StringComparer.Ordinal);
        HashSet<string> inconsistent = new(StringComparer.Ordinal);
        foreach (string suffix in new[] { "requests", "input-tokens", "output-tokens", "tokens" })
        {
            string limitName = "x-ratelimit-limit-" + suffix;
            string remainingName = "x-ratelimit-remaining-" + suffix;
            if (values.TryGetValue(limitName, out long limit)
                && values.TryGetValue(remainingName, out long remaining)
                && remaining > limit)
            {
                inconsistent.Add(limitName);
                inconsistent.Add(remainingName);
            }
        }
        return unique.Where(header => !inconsistent.Contains(header.Name))
            .OrderBy(header => header.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static long MaximumHeaderValue(string name) =>
        name.Equals("openai-processing-ms", StringComparison.OrdinalIgnoreCase)
            ? MaximumRetainedProcessingMilliseconds
            : MaximumRetainedHeaderQuantity;

    private static string? ProviderRequestId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? values))
        {
            return null;
        }
        string[] exactValues = values.ToArray();
        if (exactValues.Length != 1)
        {
            return null;
        }
        return SanitizeProviderRequestId(exactValues[0]);
    }

    internal static string? SanitizeProviderRequestId(string? value) =>
        value is not null
        && value.Length is > 0 and <= 128
        && !value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '-')
            ? value
            : null;

    internal static string? SanitizeProviderErrorField(string? value) =>
        value is not null
        && value.Length is > 0 and <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '-')
            ? value
            : null;

    internal static ProviderUsageContract UnavailableUsage(UsageReceiptState state)
    {
        ProviderQuantityContract absent = new(ProviderAvailabilityState.Unavailable, null);
        return new(ProviderAvailabilityState.Unavailable, absent, absent, absent, absent, absent, absent, absent,
            absent, absent, ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, state);
    }

    internal static ProviderUsageContract DispatchedUnknownUsage(UsageReceiptState receiptState, bool rateAvailable)
    {
        ProviderQuantityContract absent = new(ProviderAvailabilityState.Unavailable, null);
        return new(ProviderAvailabilityState.Available,
            new(ProviderAvailabilityState.Available, 1), absent, absent, absent, absent, absent, absent, absent, absent,
            ProviderAvailabilityState.Unavailable,
            rateAvailable ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, receiptState);
    }

    internal static bool IsRateHeader(OpenAiRateHeader header) =>
        header.Name.StartsWith("x-ratelimit-", StringComparison.Ordinal);
}

public static class OpenAiResponsesResponseCodec
{
    public static OpenAiResponsesResult Replay(
        ReadOnlySpan<byte> retainedRawResponse,
        int httpStatus,
        string clientRequestId,
        string? providerRequestId,
        IReadOnlyList<OpenAiRateHeader>? retainedRateHeaders = null,
        ReadOnlyMemory<byte> requestedOutputSchema = default) =>
        Parse(retainedRawResponse, httpStatus, clientRequestId, providerRequestId, retainedRateHeaders ?? [], requestedOutputSchema) with
        { NetworkUsed = false, SendCount = 0, TransportMayHaveStarted = false };

    public static OpenAiResponsesResult Parse(
        ReadOnlySpan<byte> raw,
        int httpStatus,
        string clientRequestId,
        string? providerRequestId,
        IReadOnlyList<OpenAiRateHeader> rateHeaders,
        ReadOnlyMemory<byte> requestedOutputSchema = default)
    {
        byte[] retained = raw.ToArray();
        providerRequestId = OpenAiResponsesAdapter.SanitizeProviderRequestId(providerRequestId);
        rateHeaders = OpenAiResponsesAdapter.SanitizeRetainedHeaders(rateHeaders);
        try
        {
            using JsonDocument document = JsonDocument.Parse(raw.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            JsonElement root = document.RootElement;
            string? id = OpenAiResponsesAdapter.SanitizeProviderRequestId(String(root, "id"));
            string? status = String(root, "status");
            string? model = String(root, "model");
            string? tier = String(root, "service_tier");
            ProviderResponseState state = State(status, httpStatus);
            string? refusal = FindRefusal(root);
            if (refusal is not null)
            {
                state = ProviderResponseState.Refusal;
            }
            string? incomplete = root.TryGetProperty("incomplete_details", out JsonElement details)
                ? String(details, "reason") : null;
            string? error = root.TryGetProperty("error", out JsonElement errorValue)
                ? OpenAiResponsesAdapter.SanitizeProviderErrorField(String(errorValue, "code")) : null;
            string? errorType = root.TryGetProperty("error", out errorValue)
                ? OpenAiResponsesAdapter.SanitizeProviderErrorField(String(errorValue, "type")) : null;
            ProviderUsageContract usage = ParseUsage(root, state) with
            {
                RateAvailability = !rateHeaders.Any(OpenAiResponsesAdapter.IsRateHeader)
                    ? ProviderAvailabilityState.Unavailable
                    : ProviderAvailabilityState.Available,
            };
            bool exactProfile = model == OpenAiResponsesCanonicalSerializer.Model
                && tier == OpenAiResponsesCanonicalSerializer.ServiceTier;
            bool completed = state == ProviderResponseState.Completed && httpStatus is >= 200 and < 300;
            bool usageComplete = usage.Availability == ProviderAvailabilityState.Available
                && Available(usage.InputTokens) && Available(usage.OutputTokens) && Available(usage.TotalTokens)
                && Available(usage.ReasoningTokens) && Available(usage.CacheReadTokens)
                && Available(usage.CacheWriteTokens) && Available(usage.PricedToolCalls)
                && usage.CacheReadTokens.Value == 0 && usage.CacheWriteTokens.Value == 0;
            bool structuredOutput = completed && StrictOutputMatches(root, requestedOutputSchema.Span);
            bool admitted = completed && exactProfile && usageComplete && structuredOutput && refusal is null
                && incomplete is null && error is null;
            string reason = admitted ? "admitted"
                : !exactProfile && completed ? "profile_drift"
                : completed && !usageComplete ? "usage_or_cache_drift"
                : completed && !structuredOutput ? "strict_output_missing"
                : state.ToString().ToLowerInvariant();
            if (completed && !exactProfile)
            {
                state = ProviderResponseState.Mismatched;
            }
            else if (completed && (!usageComplete || !structuredOutput))
            {
                state = ProviderResponseState.Malformed;
            }

            return new OpenAiResponsesResult(state, true, false, httpStatus, retained, id, clientRequestId, providerRequestId, model, tier,
                refusal, incomplete, error, usage, rateHeaders, admitted, reason, true, 1)
            { ProviderErrorType = errorType, RequestedOutputSchemaBytes = requestedOutputSchema.ToArray() };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or OverflowException or FormatException)
        {
            return new OpenAiResponsesResult(ProviderResponseState.Malformed, true, false, httpStatus, retained, null, clientRequestId,
                providerRequestId, null, null, null, null, "malformed_json",
                OpenAiResponsesAdapter.DispatchedUnknownUsage(
                    UsageReceiptState.Partial, rateHeaders.Any(OpenAiResponsesAdapter.IsRateHeader)), rateHeaders,
                false, "malformed", true, 1)
            { RequestedOutputSchemaBytes = requestedOutputSchema.ToArray() };
        }
    }

    private static ProviderUsageContract ParseUsage(JsonElement root, ProviderResponseState state)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return OpenAiResponsesAdapter.DispatchedUnknownUsage(state switch
            {
                ProviderResponseState.Failed => UsageReceiptState.FailedKnown,
                ProviderResponseState.Unknown => UsageReceiptState.Ambiguous,
                _ => UsageReceiptState.Partial,
            }, rateAvailable: false);
        }

        long? input = Integer(usage, "input_tokens");
        long? output = Integer(usage, "output_tokens");
        long? total = Integer(usage, "total_tokens");
        long? reasoning = usage.TryGetProperty("output_tokens_details", out JsonElement outputDetails)
            ? Integer(outputDetails, "reasoning_tokens") : null;
        long? cached = usage.TryGetProperty("input_tokens_details", out JsonElement inputDetails)
            ? Integer(inputDetails, "cached_tokens") : null;
        long? cacheWrite = usage.TryGetProperty("input_tokens_details", out inputDetails)
            ? Integer(inputDetails, "cache_write_tokens") : null;
        ProviderQuantityContract Quantity(long? value) => value.HasValue
            ? new(ProviderAvailabilityState.Available, value.Value)
            : new(ProviderAvailabilityState.Unavailable, null);
        bool complete = input.HasValue && output.HasValue && total.HasValue && reasoning.HasValue
            && cached.HasValue && cacheWrite.HasValue && total == input + output;
        complete = complete && input <= 147_456 && output <= 8_192 && total <= 155_648
            && reasoning <= output && cached <= 147_456 && cacheWrite <= 147_456;
        long? calculatedNanoUsd = complete
            ? checked(checked(input!.Value * 5_000L) + checked(output!.Value * 30_000L))
            : null;
        return new(complete ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            Quantity(1), Quantity(input), Quantity(output), Quantity(total), Quantity(reasoning), Quantity(cached),
            Quantity(cacheWrite), Quantity(0), Quantity(calculatedNanoUsd),
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, complete ? UsageReceiptState.Complete : UsageReceiptState.Partial);
    }

    private static ProviderResponseState State(string? value, int httpStatus) => value switch
    {
        "completed" => ProviderResponseState.Completed,
        "incomplete" => ProviderResponseState.Incomplete,
        "failed" => ProviderResponseState.Failed,
        "queued" => ProviderResponseState.Queued,
        "in_progress" => ProviderResponseState.InProgress,
        "cancelled" => ProviderResponseState.Cancelled,
        null when httpStatus is < 200 or >= 300 => ProviderResponseState.Failed,
        _ => ProviderResponseState.Unknown,
    };

    private static string? FindRefusal(JsonElement root)
    {
        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement item in output.EnumerateArray())
        {
            if (item.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement part in content.EnumerateArray())
                {
                    if (String(part, "type") == "refusal")
                    {
                        return String(part, "refusal") ?? "refusal";
                    }
                }
            }
        }

        return null;
    }

    private static bool StrictOutputMatches(JsonElement root, ReadOnlySpan<byte> schemaBytes)
    {
        if (schemaBytes.IsEmpty)
        {
            return false;
        }
        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        if (output.GetArrayLength() != 1)
        {
            return false;
        }
        string? outputText = null;
        foreach (JsonElement item in output.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !ExactProperties(item, ["id", "type", "status", "role", "content"])
                || String(item, "type") != "message"
                || item.TryGetProperty("status", out JsonElement status)
                    && (status.ValueKind != JsonValueKind.String || status.GetString() != "completed")
                || item.TryGetProperty("role", out JsonElement role)
                    && (role.ValueKind != JsonValueKind.String || role.GetString() != "assistant")
                || !item.TryGetProperty("content", out JsonElement content)
                || content.ValueKind != JsonValueKind.Array || content.GetArrayLength() != 1)
            {
                return false;
            }
            foreach (JsonElement part in content.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object
                    || !ExactProperties(part, ["type", "text", "annotations", "logprobs"])
                    || String(part, "type") != "output_text"
                    || String(part, "text") is not string text
                    || part.TryGetProperty("annotations", out JsonElement annotations)
                        && (annotations.ValueKind != JsonValueKind.Array || annotations.GetArrayLength() != 0)
                    || part.TryGetProperty("logprobs", out JsonElement logprobs)
                        && logprobs.ValueKind is not JsonValueKind.Null
                        && (logprobs.ValueKind != JsonValueKind.Array || logprobs.GetArrayLength() != 0))
                {
                    return false;
                }
                outputText = text;
            }
        }
        if (outputText is null)
        {
            return false;
        }
        try
        {
            using JsonDocument value = JsonDocument.Parse(outputText);
            using JsonDocument schema = JsonDocument.Parse(schemaBytes.ToArray());
            return ClosedJsonSchemaValidator.Validate(value.RootElement, schema.RootElement);
        }
        catch (JsonException) { return false; }
    }

    private static bool ExactProperties(JsonElement value, string[] allowed)
    {
        HashSet<string> names = allowed.ToHashSet(StringComparer.Ordinal);
        string[] actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        return actual.Distinct(StringComparer.Ordinal).Count() == actual.Length
            && actual.All(names.Contains);
    }

    private static bool Available(ProviderQuantityContract value) =>
        value.Availability == ProviderAvailabilityState.Available && value.Value.HasValue;
    private static string? String(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static long? Integer(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long result) && result >= 0
            ? result : null;
}

internal static class ClosedJsonSchemaValidator
{
    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "$defs", "title", "description",
        "$ref", "type", "const", "enum", "oneOf", "anyOf", "allOf",
        "required", "properties", "additionalProperties", "items",
        "minItems", "maxItems", "minLength", "maxLength", "minimum", "maximum",
    };

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.Ordinal)
        { "object", "array", "string", "boolean", "integer", "number", "null" };

    internal static bool ValidateSchema(JsonElement schema) =>
        ValidateSchema(schema, schema, rootSchema: true, depth: 0, []);

    private static bool ValidateSchema(
        JsonElement schema,
        JsonElement root,
        bool rootSchema,
        int depth,
        HashSet<string> resolvingReferences)
    {
        string[] keywordNames = schema.ValueKind == JsonValueKind.Object
            ? schema.EnumerateObject().Select(property => property.Name).ToArray()
            : [];
        if (schema.ValueKind != JsonValueKind.Object || depth > 64
            || keywordNames.Distinct(StringComparer.Ordinal).Count() != keywordNames.Length
            || schema.EnumerateObject().Any(property => !SupportedKeywords.Contains(property.Name)))
        {
            return false;
        }
        foreach (string annotation in new[] { "$schema", "$id", "title", "description" })
        {
            if (schema.TryGetProperty(annotation, out JsonElement value) && value.ValueKind != JsonValueKind.String)
            {
                return false;
            }
        }
        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            if (reference.ValueKind != JsonValueKind.String || schema.EnumerateObject().Count() != 1)
            {
                return false;
            }
            string identity = reference.GetString()!;
            if (!ResolveLocalReference(root, identity, out JsonElement target))
            {
                return false;
            }
            if (!resolvingReferences.Add(identity))
            {
                return true;
            }
            bool validReference = ValidateSchema(target, root, rootSchema, depth + 1, resolvingReferences);
            resolvingReferences.Remove(identity);
            return validReference;
        }
        if (schema.TryGetProperty("type", out JsonElement type) && !ValidTypeDeclaration(type))
        {
            return false;
        }
        bool declaresObject = DeclaresType(schema, "object");
        bool hasObjectKeywords = schema.TryGetProperty("properties", out JsonElement properties)
            || schema.TryGetProperty("required", out _) || schema.TryGetProperty("additionalProperties", out _);
        if (rootSchema && !declaresObject || hasObjectKeywords && !declaresObject)
        {
            return false;
        }
        if (declaresObject)
        {
            if (properties.ValueKind != JsonValueKind.Object
                || !schema.TryGetProperty("required", out JsonElement required)
                || required.ValueKind != JsonValueKind.Array
                || required.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String)
                || !schema.TryGetProperty("additionalProperties", out JsonElement additional)
                || additional.ValueKind != JsonValueKind.False)
            {
                return false;
            }
            string[] propertyNames = properties.EnumerateObject().Select(item => item.Name).ToArray();
            string[] requiredNames = required.EnumerateArray().Select(item => item.GetString()!).ToArray();
            if (propertyNames.Distinct(StringComparer.Ordinal).Count() != propertyNames.Length
                || requiredNames.Distinct(StringComparer.Ordinal).Count() != requiredNames.Length
                || !propertyNames.Order(StringComparer.Ordinal).SequenceEqual(requiredNames.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                || properties.EnumerateObject().Any(property =>
                    !ValidateSchema(property.Value, root, rootSchema: false, depth + 1, resolvingReferences)))
            {
                return false;
            }
        }
        bool declaresArray = DeclaresType(schema, "array");
        if ((schema.TryGetProperty("minItems", out _) || schema.TryGetProperty("maxItems", out _)) && !declaresArray
            || (schema.TryGetProperty("minLength", out _) || schema.TryGetProperty("maxLength", out _))
                && !DeclaresType(schema, "string")
            || (schema.TryGetProperty("minimum", out _) || schema.TryGetProperty("maximum", out _))
                && !DeclaresType(schema, "integer") && !DeclaresType(schema, "number"))
        {
            return false;
        }
        if (schema.TryGetProperty("items", out JsonElement items))
        {
            if (!declaresArray || !ValidateSchema(items, root, rootSchema: false, depth + 1, resolvingReferences))
            {
                return false;
            }
        }
        if (declaresArray && !schema.TryGetProperty("items", out _))
        {
            return false;
        }
        if (!ValidNonnegativeBoundPair(schema, "minItems", "maxItems")
            || !ValidNonnegativeBoundPair(schema, "minLength", "maxLength")
            || !ValidNumberBoundPair(schema, "minimum", "maximum"))
        {
            return false;
        }
        if (schema.TryGetProperty("enum", out JsonElement choices)
            && (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() is 0 or > 1_000
                || choices.EnumerateArray().Select(item => item.GetRawText()).Distinct(StringComparer.Ordinal).Count()
                    != choices.GetArrayLength()))
        {
            return false;
        }
        foreach (string union in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (schema.TryGetProperty(union, out JsonElement members)
                && (members.ValueKind != JsonValueKind.Array || members.GetArrayLength() is 0 or > 64
                    || members.EnumerateArray().Any(member =>
                        !ValidateSchema(member, root, rootSchema: false, depth + 1, resolvingReferences))))
            {
                return false;
            }
        }
        if (schema.TryGetProperty("$defs", out JsonElement definitions)
            && (definitions.ValueKind != JsonValueKind.Object
                || definitions.EnumerateObject().Any(definition =>
                    !ValidateSchema(definition.Value, root, rootSchema: false, depth + 1, resolvingReferences))))
        {
            return false;
        }
        return schema.TryGetProperty("type", out _) || schema.TryGetProperty("const", out _)
            || schema.TryGetProperty("enum", out _) || schema.TryGetProperty("oneOf", out _)
            || schema.TryGetProperty("anyOf", out _) || schema.TryGetProperty("allOf", out _);
    }

    private static bool ValidTypeDeclaration(JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return SupportedTypes.Contains(type.GetString()!);
        }
        return type.ValueKind == JsonValueKind.Array && type.GetArrayLength() is > 0 and <= 7
            && type.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String
                && SupportedTypes.Contains(item.GetString()!))
            && type.EnumerateArray().Select(item => item.GetString()).Distinct(StringComparer.Ordinal).Count()
                == type.GetArrayLength();
    }

    private static bool DeclaresType(JsonElement schema, string type) =>
        schema.TryGetProperty("type", out JsonElement declaration)
        && (declaration.ValueKind == JsonValueKind.String && declaration.GetString() == type
            || declaration.ValueKind == JsonValueKind.Array
                && declaration.EnumerateArray().Any(item => item.GetString() == type));

    private static bool ValidNonnegativeBoundPair(JsonElement schema, string minimumName, string maximumName)
    {
        long minimum = 0;
        long maximum = long.MaxValue;
        bool hasMinimum = schema.TryGetProperty(minimumName, out JsonElement minimumValue);
        bool hasMaximum = schema.TryGetProperty(maximumName, out JsonElement maximumValue);
        return (!hasMinimum || minimumValue.ValueKind == JsonValueKind.Number
                && minimumValue.TryGetInt64(out minimum) && minimum >= 0)
            && (!hasMaximum || maximumValue.ValueKind == JsonValueKind.Number
                && maximumValue.TryGetInt64(out maximum) && maximum >= 0)
            && minimum <= maximum;
    }

    private static bool ValidNumberBoundPair(JsonElement schema, string minimumName, string maximumName)
    {
        decimal minimum = decimal.MinValue;
        decimal maximum = decimal.MaxValue;
        bool hasMinimum = schema.TryGetProperty(minimumName, out JsonElement minimumValue);
        bool hasMaximum = schema.TryGetProperty(maximumName, out JsonElement maximumValue);
        return (!hasMinimum || minimumValue.ValueKind == JsonValueKind.Number && minimumValue.TryGetDecimal(out minimum))
            && (!hasMaximum || maximumValue.ValueKind == JsonValueKind.Number && maximumValue.TryGetDecimal(out maximum))
            && minimum <= maximum;
    }

    internal static bool Validate(JsonElement value, JsonElement schema) =>
        Validate(value, schema, schema, depth: 0);

    private static bool Validate(JsonElement value, JsonElement schema, JsonElement root, int depth)
    {
        if (schema.ValueKind != JsonValueKind.Object || depth > 64
            || schema.EnumerateObject().Any(property => !SupportedKeywords.Contains(property.Name)))
        {
            return false;
        }
        if (schema.TryGetProperty("required", out JsonElement requiredSchema)
            && (requiredSchema.ValueKind != JsonValueKind.Array
                || requiredSchema.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String)
                || requiredSchema.EnumerateArray().Select(item => item.GetString()).Distinct(StringComparer.Ordinal).Count()
                    != requiredSchema.GetArrayLength()))
        {
            return false;
        }
        if (schema.TryGetProperty("properties", out JsonElement propertiesSchema)
            && propertiesSchema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (schema.TryGetProperty("additionalProperties", out JsonElement additionalSchema)
            && additionalSchema.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Object))
        {
            return false;
        }
        if (schema.TryGetProperty("items", out JsonElement itemsSchema) && itemsSchema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            return reference.ValueKind == JsonValueKind.String
                && ResolveLocalReference(root, reference.GetString()!, out JsonElement target)
                && Validate(value, target, root, depth + 1);
        }

        if (schema.TryGetProperty("oneOf", out JsonElement oneOf)
            && (oneOf.ValueKind != JsonValueKind.Array
                || oneOf.EnumerateArray().Count(candidate => Validate(value, candidate, root, depth + 1)) != 1))
        {
            return false;
        }
        if (schema.TryGetProperty("anyOf", out JsonElement anyOf)
            && (anyOf.ValueKind != JsonValueKind.Array
                || !anyOf.EnumerateArray().Any(candidate => Validate(value, candidate, root, depth + 1))))
        {
            return false;
        }
        if (schema.TryGetProperty("allOf", out JsonElement allOf)
            && (allOf.ValueKind != JsonValueKind.Array
                || !allOf.EnumerateArray().All(candidate => Validate(value, candidate, root, depth + 1))))
        {
            return false;
        }

        if (schema.TryGetProperty("const", out JsonElement constant) && !JsonElement.DeepEquals(value, constant))
        {
            return false;
        }

        if (schema.TryGetProperty("enum", out JsonElement choices)
            && (choices.ValueKind != JsonValueKind.Array || !choices.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value))))
        {
            return false;
        }

        if (schema.TryGetProperty("type", out JsonElement type) && !MatchesDeclaredType(value, type))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> required = schema.TryGetProperty("required", out JsonElement requiredValue)
                && requiredValue.ValueKind == JsonValueKind.Array
                ? requiredValue.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal)
                : [];
            if (required.Any(name => !value.TryGetProperty(name, out _)))
            {
                return false;
            }

            JsonElement properties = schema.TryGetProperty("properties", out JsonElement propertyValue)
                && propertyValue.ValueKind == JsonValueKind.Object ? propertyValue : default;
            bool additional = !schema.TryGetProperty("additionalProperties", out JsonElement additionalValue)
                || additionalValue.ValueKind != JsonValueKind.False;
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out JsonElement child))
                {
                    if (!Validate(property.Value, child, root, depth + 1))
                    {
                        return false;
                    }
                }
                else if (schema.TryGetProperty("additionalProperties", out additionalValue)
                    && additionalValue.ValueKind == JsonValueKind.Object)
                {
                    if (!Validate(property.Value, additionalValue, root, depth + 1))
                    {
                        return false;
                    }
                }
                else if (!additional)
                {
                    return false;
                }
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            int length = value.GetArrayLength();
            if (!WithinIntegerBound(schema, "minItems", length, lowerBound: true)
                || !WithinIntegerBound(schema, "maxItems", length, lowerBound: false))
            {
                return false;
            }
            if (schema.TryGetProperty("items", out JsonElement items))
            {
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (!Validate(item, items, root, depth + 1))
                    {
                        return false;
                    }
                }
            }
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            int length = value.GetString()!.EnumerateRunes().Count();
            if (!WithinIntegerBound(schema, "minLength", length, lowerBound: true)
                || !WithinIntegerBound(schema, "maxLength", length, lowerBound: false))
            {
                return false;
            }
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!WithinNumberBound(schema, "minimum", value, lowerBound: true)
                || !WithinNumberBound(schema, "maximum", value, lowerBound: false))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ResolveLocalReference(JsonElement root, string reference, out JsonElement target)
    {
        target = root;
        if (reference == "#")
        {
            return true;
        }
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return false;
        }
        foreach (string rawSegment in reference[2..].Split('/'))
        {
            string segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty(segment, out target))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchesDeclaredType(JsonElement value, JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return MatchesType(value, type.GetString()!);
        }
        return type.ValueKind == JsonValueKind.Array && type.GetArrayLength() > 0
            && type.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
            && type.EnumerateArray().Any(item => MatchesType(value, item.GetString()!));
    }

    private static bool WithinIntegerBound(JsonElement schema, string name, int actual, bool lowerBound)
    {
        if (!schema.TryGetProperty(name, out JsonElement bound))
        {
            return true;
        }
        return bound.ValueKind == JsonValueKind.Number && bound.TryGetInt32(out int expected)
            && expected >= 0 && (lowerBound ? actual >= expected : actual <= expected);
    }

    private static bool WithinNumberBound(JsonElement schema, string name, JsonElement actual, bool lowerBound)
    {
        if (!schema.TryGetProperty(name, out JsonElement bound))
        {
            return true;
        }
        return bound.ValueKind == JsonValueKind.Number && bound.TryGetDecimal(out decimal expected)
            && actual.TryGetDecimal(out decimal number)
            && (lowerBound ? number >= expected : number <= expected);
    }

    private static bool MatchesType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => false,
    };
}
