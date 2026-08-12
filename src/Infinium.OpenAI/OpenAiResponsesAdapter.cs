using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
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
    long MaximumOutputTokens);

public sealed record OpenAiRateHeader(string Name, string Value);

public static class OpenAiStagedResponseEnvelope
{
    private static ReadOnlySpan<byte> Magic => "INFWP5\0"u8;

    public static byte[] Create(OpenAiResponsesResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        byte[] raw = result.RawResponseBytes ?? [];
        if (raw.Length == 0 && result.State != ProviderResponseState.Oversized)
        {
            throw new InvalidOperationException("A staged response envelope requires raw bytes or an exact oversized observation.");
        }
        byte[] headers = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "infinium.openai.response-headers/v1",
            state = result.State,
            http_status = result.HttpStatus,
            provider_response_id = result.ProviderResponseId,
            provider_request_id = result.ProviderRequestId,
            returned_model = result.ReturnedModel,
            returned_service_tier = result.ReturnedServiceTier,
            refusal_code = result.RefusalCode,
            incomplete_reason = result.IncompleteReason,
            error_code = result.ErrorCode,
            requested_output_schema = result.RequestedOutputSchemaBytes,
            usage = result.Usage,
            headers = result.RateHeaders.Select(item => new { name = item.Name, value = item.Value }).ToArray(),
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
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    public static int HttpStatus(ReadOnlySpan<byte> headerReceipt)
    {
        using JsonDocument document = JsonDocument.Parse(headerReceipt.ToArray());
        return document.RootElement.GetProperty("http_status").GetInt32();
    }

    public static IReadOnlyList<OpenAiRateHeader> RateHeaders(ReadOnlySpan<byte> headerReceipt)
    {
        using JsonDocument document = JsonDocument.Parse(headerReceipt.ToArray());
        return document.RootElement.GetProperty("headers").EnumerateArray()
            .Select(item => new OpenAiRateHeader(
                item.GetProperty("name").GetString()!, item.GetProperty("value").GetString()!))
            .ToArray();
    }

    public static OpenAiResponsesResult Replay(ReadOnlySpan<byte> raw, ReadOnlySpan<byte> headerReceipt, string clientRequestId)
    {
        int status = HttpStatus(headerReceipt);
        IReadOnlyList<OpenAiRateHeader> rateHeaders = RateHeaders(headerReceipt);
        if (!raw.IsEmpty)
        {
            using JsonDocument retained = JsonDocument.Parse(headerReceipt.ToArray());
            byte[] retainedSchema = retained.RootElement.TryGetProperty("requested_output_schema", out JsonElement schemaValue)
                && schemaValue.ValueKind == JsonValueKind.String
                ? schemaValue.GetBytesFromBase64()
                : [];
            return OpenAiResponsesResponseCodec.Replay(
                raw, status, clientRequestId, ProviderRequestId(headerReceipt), rateHeaders, retainedSchema);
        }
        using JsonDocument document = JsonDocument.Parse(headerReceipt.ToArray());
        JsonElement root = document.RootElement;
        ProviderResponseState state = root.GetProperty("state").Deserialize<ProviderResponseState>();
        if (state != ProviderResponseState.Oversized)
        {
            throw new InvalidDataException("Only an oversized response may omit retained raw bytes.");
        }
        ProviderUsageContract usage = root.GetProperty("usage").Deserialize<ProviderUsageContract>()
            ?? throw new InvalidDataException("The oversized response usage receipt is absent.");
        return new OpenAiResponsesResult(state, true, false, status, null, String(root, "provider_response_id"), clientRequestId,
            String(root, "provider_request_id"), String(root, "returned_model"), String(root, "returned_service_tier"),
            String(root, "refusal_code"), String(root, "incomplete_reason"), String(root, "error_code"), usage,
            rateHeaders, false, "oversized", false, 0)
        {
            RequestedOutputSchemaBytes = root.TryGetProperty("requested_output_schema", out JsonElement schema)
                && schema.ValueKind == JsonValueKind.String ? schema.GetBytesFromBase64() : [],
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

    public byte[] ToSecretFreeDiagnosticBytes() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        state = State.ToString(),
        transport_may_have_started = TransportMayHaveStarted,
        retry_permitted = RetryPermitted,
        http_status = HttpStatus,
        raw_response_bytes = RawResponseBytes?.LongLength,
        provider_response_id = ProviderResponseId,
        client_request_id = ClientRequestId,
        provider_request_id = ProviderRequestId,
        returned_model = ReturnedModel,
        returned_service_tier = ReturnedServiceTier,
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
    public const string InputBoundPolicyVersion = "v1";

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
            || request.OutputSchema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The Responses request exceeds its closed context or output bounds.");
        }
        RejectAnswerBearingSchemaNames(request.OutputSchema);

        ArrayBufferWriter<byte> bytes = new();
        using (Utf8JsonWriter writer = new(bytes, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("model", Model);
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
            request.OutputSchema.WriteTo(writer);
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
        string[] exactNames = ["model", "reasoning", "text", "store", "service_tier", "background", "stream",
            "tool_choice", "tools", "truncation", "max_output_tokens", "prompt_cache_options", "input"];
        if (root.ValueKind != JsonValueKind.Object
            || !root.EnumerateObject().Select(property => property.Name).SequenceEqual(exactNames, StringComparer.Ordinal)
            || root.GetProperty("model").GetString() != Model
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
        JsonElement format = root.GetProperty("text").GetProperty("format");
        long operationCeiling = format.GetProperty("name").GetString() switch
        {
            "transport_qualification" => 256,
            "source_claim_extraction" or "candidate_investigation" => 4_096,
            _ => 0,
        };
        if (reasoning.GetProperty("effort").GetString() != "medium"
            || reasoning.GetProperty("context").GetString() != "current_turn"
            || reasoning.GetProperty("mode").GetString() != "standard"
            || cache.EnumerateObject().Count() != 1 || cache.GetProperty("mode").GetString() != "explicit"
            || format.GetProperty("type").GetString() != "json_schema" || !format.GetProperty("strict").GetBoolean()
            || maximumOutputTokens <= 0 || maximumOutputTokens > operationCeiling)
        {
            throw new InvalidDataException("Stateless reasoning, cache-off, and strict output controls must be explicit.");
        }
    }

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
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly bool ownsClient;
    private int consumed;
    public bool UsesPerOperationDeadlineOnly => client.Timeout == Timeout.InfiniteTimeSpan;
    public static bool ProxyFallbackEnabled => false;
    public static bool RedirectsEnabled => false;
    public static bool RetriesEnabled => false;
    public static bool ProviderToolsEnabled => false;

    private OpenAiResponsesAdapter(HttpClient client, Uri endpoint, bool ownsClient)
    {
        this.client = client;
        this.endpoint = endpoint;
        this.ownsClient = ownsClient;
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public static OpenAiResponsesAdapter CreateProduction()
    {
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
            MaxConnectionsPerServer = 1,
            PooledConnectionLifetime = TimeSpan.Zero,
        };
        return new(new HttpClient(handler, disposeHandler: true), ProductionEndpoint, ownsClient: true);
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
            IReadOnlyList<OpenAiRateHeader> rateHeaders = CaptureHeaders(response);
            byte[]? raw = await ReadBoundedAsync(response.Content, limits.MaximumRawResponseBytes, deadline.Token)
                .ConfigureAwait(false);
            if (raw is null)
            {
                return Failure(ProviderResponseState.Oversized, (int)response.StatusCode, true, "response_too_large",
                    rateHeaders, clientRequestId, Header(response, "x-request-id"), sendCount: 1) with
                { RequestedOutputSchemaBytes = OpenAiResponsesCanonicalSerializer.OutputSchemaBytes(canonicalRequest.Span) };
            }

            return OpenAiResponsesResponseCodec.Parse(raw, (int)response.StatusCode, clientRequestId,
                Header(response, "x-request-id"), rateHeaders,
                OpenAiResponsesCanonicalSerializer.OutputSchemaBytes(canonicalRequest.Span));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(ProviderResponseState.Unknown, null, true, "deadline_ambiguous", [], clientRequestId, null, 1);
        }
        catch (HttpRequestException)
        {
            return Failure(ProviderResponseState.Unknown, null, true, "transport_ambiguous", [], clientRequestId, null, 1);
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
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }

    private static OpenAiRateHeader[] CaptureHeaders(HttpResponseMessage response)
    {
        List<OpenAiRateHeader> result = [];
        foreach ((string name, IEnumerable<string> values) in response.Headers)
        {
            if (name.StartsWith("x-ratelimit-", StringComparison.OrdinalIgnoreCase)
                || name.Equals("openai-processing-ms", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new(name.ToLowerInvariant(), string.Join(",", values)));
            }
        }
        return result.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.SingleOrDefault() : null;

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
        try
        {
            using JsonDocument document = JsonDocument.Parse(raw.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
            JsonElement root = document.RootElement;
            string? id = String(root, "id");
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
            string? error = root.TryGetProperty("error", out JsonElement errorValue) ? String(errorValue, "code") : null;
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
            { RequestedOutputSchemaBytes = requestedOutputSchema.ToArray() };
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

        string? outputText = null;
        foreach (JsonElement item in output.EnumerateArray())
        {
            if (item.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement part in content.EnumerateArray())
                {
                    if (String(part, "type") == "output_text")
                    {
                        if (outputText is not null || String(part, "text") is not string text)
                        {
                            return false;
                        }
                        outputText = text;
                    }
                }
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
