using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.ML.Tokenizers;

namespace Infinium.Application.Provider;

public sealed record ProviderInputBoundEvidence(
    ProviderInputBoundProofContract Proof,
    long CanonicalUtf8Bytes,
    Sha256Fingerprint CanonicalRequestFingerprint,
    IReadOnlyList<int> O200kTokenIds,
    long O200kTokenCount,
    Sha256Fingerprint TokenIdsFingerprint,
    long StructuralAllowanceTokens,
    long ConservativeInputTokenUpperBound);

/// <summary>
/// Offline, versioned proof for the closed M1 OpenAI Responses request profile.
/// The tokenizer count is retained evidence. Admission uses the stricter byte
/// envelope B + A: every ordinary o200k token consumes at least one canonical
/// UTF-8 byte, while A conservatively covers provider-only structural framing.
/// </summary>
public static class OpenAiResponsesInputBoundPolicy
{
    public const string PolicyId = "openai-responses-o200k-byte-envelope";
    public const string PolicyVersion = "v1";
    public const string PolicyIdentity = PolicyId + "/" + PolicyVersion;
    public const string Model = "gpt-5.6-sol";
    public const string EncodingName = "o200k_base";
    public const string TokenizerPackageIdentity = "Microsoft.ML.Tokenizers/2.0.0";
    public const string VocabularyPackageIdentity = "Microsoft.ML.Tokenizers.Data.O200kBase/2.0.0";
    public const string TokenizerPackageContentHash = "+b8lT4cLLO/sBR2hjvE/qG6qrZG15h7/PBvnIrzTh4xDaAxdHUY6449rC+1pHzQUsBiCHZVbj+VMn+xS0sL7TA==";
    public const string VocabularyPackageContentHash = "19G0KWrRnUZmc8vGdPNuBJqTruhAjzPLRY2nn6a/HiBXbEnE/Lx9L223jGlDzg1oAcCggo/8GlWw3ZLVuS76Ow==";
    public const long QualificationStructuralAllowanceTokens = 4_096;
    public const long SemanticStructuralAllowanceTokens = 8_192;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Lazy<TiktokenTokenizer> Tokenizer = new(
        () => TiktokenTokenizer.CreateForEncoding(EncodingName),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static ProviderInputBoundEvidence Prove(
        ProviderOperationKind operationKind,
        ReadOnlyMemory<byte> canonicalRequest,
        ProviderFiniteLimitsContract limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ProviderOperationContractInvariants.Validate(operationKind, limits);
        if (canonicalRequest.IsEmpty || canonicalRequest.Length > limits.MaximumRequestBytes)
        {
            throw new InvalidDataException("Canonical request bytes exceed the operation's admitted request bound.");
        }

        string requestText;
        try
        {
            requestText = StrictUtf8.GetString(canonicalRequest.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Canonical request must be well-formed UTF-8 without replacement decoding.", exception);
        }

        ValidateClosedRequestShape(canonicalRequest, operationKind, limits.MaximumOutputTokens);
        IReadOnlyList<int> tokenIds = Tokenizer.Value.EncodeToIds(requestText, false, false);
        long byteCount = canonicalRequest.Length;
        long tokenCount = tokenIds.Count;
        if (tokenCount > byteCount)
        {
            throw new InvalidDataException("The pinned ordinary o200k token count exceeds canonical UTF-8 bytes; the byte-envelope premise is false.");
        }

        long allowance = operationKind == ProviderOperationKind.TransportQualification
            ? QualificationStructuralAllowanceTokens
            : SemanticStructuralAllowanceTokens;
        long conservativeUpper = checked(byteCount + allowance);
        if (conservativeUpper > limits.MaximumInputTokens)
        {
            throw new InvalidDataException("Canonical request plus the fixed provider-structural allowance exceeds the admitted input-token upper bound.");
        }

        byte[] requestHash = SHA256.HashData(canonicalRequest.Span);
        byte[] tokenBytes = new byte[checked(tokenIds.Count * sizeof(int))];
        for (int index = 0; index < tokenIds.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(tokenBytes.AsSpan(index * sizeof(int), sizeof(int)), tokenIds[index]);
        }

        return new ProviderInputBoundEvidence(
            new ProviderInputBoundProofContract(PolicyId, PolicyVersion, ProviderInputBoundProofState.Proved),
            byteCount,
            new Sha256Fingerprint(Convert.ToHexStringLower(requestHash)),
            tokenIds,
            tokenCount,
            new Sha256Fingerprint(Convert.ToHexStringLower(SHA256.HashData(tokenBytes))),
            allowance,
            conservativeUpper);
    }

    public static void ValidateProofIdentity(ProviderInputBoundProofContract proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (proof.Status != ProviderInputBoundProofState.Proved
            || proof.PolicyId != PolicyId
            || proof.PolicyVersion != PolicyVersion)
        {
            throw new InvalidDataException($"Input-bound proof must use the pinned {PolicyIdentity} policy identity.");
        }
    }

    private static void ValidateClosedRequestShape(
        ReadOnlyMemory<byte> canonicalRequest,
        ProviderOperationKind operationKind,
        long maximumOutputTokens)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(canonicalRequest, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Canonical request must be one strict JSON document.", exception);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            RequireObjectProperties(root, "request",
            [
                "model", "reasoning", "text", "store", "service_tier", "background", "stream",
                "tool_choice", "tools", "truncation", "max_output_tokens", "prompt_cache_options",
                "instructions", "input",
            ]);
            RequireString(root, "model", Model);
            RequireBoolean(root, "store", false);
            RequireString(root, "service_tier", "default");
            RequireBoolean(root, "background", false);
            RequireBoolean(root, "stream", false);
            RequireString(root, "tool_choice", "none");
            JsonElement tools = root.GetProperty("tools");
            if (tools.ValueKind != JsonValueKind.Array || tools.GetArrayLength() != 0)
            {
                throw new InvalidDataException("The M1 byte-envelope policy rejects provider tools.");
            }
            RequireString(root, "truncation", "disabled");
            if (!root.GetProperty("max_output_tokens").TryGetInt64(out long outputTokens)
                || outputTokens != maximumOutputTokens)
            {
                throw new InvalidDataException("Canonical request max_output_tokens must equal the admitted operation limit.");
            }
            RequireStringValue(root.GetProperty("instructions"), "instructions");
            RequireStringValue(root.GetProperty("input"), "input");

            JsonElement reasoning = root.GetProperty("reasoning");
            RequireObjectProperties(reasoning, "reasoning", ["effort", "context", "mode"]);
            RequireString(reasoning, "effort", "medium");
            RequireString(reasoning, "context", "current_turn");
            RequireString(reasoning, "mode", "standard");

            JsonElement cache = root.GetProperty("prompt_cache_options");
            RequireObjectProperties(cache, "prompt_cache_options", ["mode"]);
            RequireString(cache, "mode", "explicit");

            JsonElement text = root.GetProperty("text");
            RequireObjectProperties(text, "text", ["format"]);
            JsonElement format = text.GetProperty("format");
            RequireObjectProperties(format, "text.format", ["type", "name", "strict", "schema"]);
            RequireString(format, "type", "json_schema");
            RequireStringValue(format.GetProperty("name"), "text.format.name");
            RequireBoolean(format, "strict", true);
            if (format.GetProperty("schema").ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("The closed request requires an inline JSON output schema.");
            }
            AssertNoDuplicateProperties(format.GetProperty("schema"), "text.format.schema");

            string expectedName = operationKind switch
            {
                ProviderOperationKind.TransportQualification => "transport_qualification",
                ProviderOperationKind.SourceClaimExtraction => "source_claim_extraction",
                ProviderOperationKind.CandidateInvestigation => "candidate_investigation",
                _ => throw new InvalidDataException("Input-bound policy operation kind is not supported."),
            };
            RequireString(format, "name", expectedName);
        }
    }

    private static void RequireObjectProperties(JsonElement value, string label, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{label} must be an object.");
        }
        HashSet<string> actual = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!actual.Add(property.Name))
            {
                throw new InvalidDataException($"{label} contains duplicate property {property.Name}.");
            }
        }
        if (!actual.SetEquals(expected))
        {
            throw new InvalidDataException($"{label} is not the closed M1 provider request shape.");
        }
    }

    private static void AssertNoDuplicateProperties(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"{path} contains duplicate property {property.Name}.");
                }
                AssertNoDuplicateProperties(property.Value, path + "." + property.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertNoDuplicateProperties(item, $"{path}[{index++}]");
            }
        }
    }

    private static void RequireString(JsonElement parent, string propertyName, string expected)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.String || value.GetString() != expected)
        {
            throw new InvalidDataException($"{propertyName} must be {expected}.");
        }
    }

    private static void RequireStringValue(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
        {
            throw new InvalidDataException($"{label} must be one non-empty string; multi-turn and out-of-band input are unsupported.");
        }
    }

    private static void RequireBoolean(JsonElement parent, string propertyName, bool expected)
    {
        JsonElement value = parent.GetProperty(propertyName);
        bool valid = expected ? value.ValueKind == JsonValueKind.True : value.ValueKind == JsonValueKind.False;
        if (!valid)
        {
            throw new InvalidDataException($"{propertyName} must be {expected.ToString().ToLowerInvariant()}.");
        }
    }
}
