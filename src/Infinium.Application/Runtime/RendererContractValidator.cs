using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Infinium.Application.Evaluation;
using Infinium.Contracts.Protobuf.Application.V1;

namespace Infinium.Application.Runtime;

public sealed record RendererEnvelope(
    string MessageKind,
    string SessionId,
    ulong Sequence,
    string Operation,
    string? RequestId,
    string? SubscriptionId,
    string? Revision);

/// <summary>
/// Validates the closed renderer transport and advances one host-session
/// sequence only after the complete envelope has passed validation.
/// </summary>
public sealed class RendererContractValidator(string expectedSessionId)
{
    private ulong lastSequence;
    private readonly HashSet<string> observedRequestIds = new(StringComparer.Ordinal);

    public RendererEnvelope ValidateAndAdvance(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 || bytes.Length > ProtocolConstants.MaximumMessageBytes)
        {
            throw new InvalidDataException("The renderer envelope exceeds its finite message bound.");
        }

        using BoundedJsonDocumentSnapshot snapshot = BoundedJsonDocumentReader.Parse(
            bytes,
            "renderer envelope",
            maximumDepth: 32);
        ActiveJsonSchemaValidator.Validate(snapshot.Document.RootElement, "renderer-envelope.v1.schema.json");
        JsonElement root = snapshot.Document.RootElement;
        string sessionId = root.GetProperty("session_id").GetString()!;
        if (!StringComparer.Ordinal.Equals(sessionId, expectedSessionId))
        {
            throw new InvalidDataException("The renderer envelope belongs to another host session; resync is required.");
        }

        ulong sequence = root.GetProperty("sequence").GetUInt64();
        if (sequence <= lastSequence)
        {
            throw new InvalidDataException("The renderer envelope was replayed.");
        }

        if (sequence != checked(lastSequence + 1))
        {
            throw new InvalidDataException("The renderer envelope is out of order; resync is required.");
        }

        string? requestId = root.TryGetProperty("request_id", out JsonElement request)
            ? request.GetString()
            : null;
        if (requestId is not null && !observedRequestIds.Add(requestId))
        {
            throw new InvalidDataException("The renderer request identifier was replayed.");
        }

        if (observedRequestIds.Count > ProtocolConstants.MaximumStreamQueueItems)
        {
            throw new InvalidDataException("The renderer replay window is exhausted; resync is required.");
        }

        lastSequence = sequence;
        return new RendererEnvelope(
            root.GetProperty("message_kind").GetString()!,
            sessionId,
            sequence,
            root.GetProperty("operation").GetString()!,
            requestId,
            root.TryGetProperty("subscription_id", out JsonElement subscription) ? subscription.GetString() : null,
            root.TryGetProperty("revision", out JsonElement revision) ? revision.GetString() : null);
    }
}

public static class RendererOperationRegistry
{
    public const string ResourceName =
        "Infinium.Contracts.Renderer.renderer-operation-registry.v1.json";

    public static byte[] GetCanonicalInput()
    {
        using Stream stream = typeof(RendererOperationRegistry).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The renderer operation registry resource is missing.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        using BoundedJsonDocumentSnapshot snapshot = BoundedJsonDocumentReader.Parse(
            bytes,
            "renderer operation registry",
            maximumDepth: 32);
        ActiveJsonSchemaValidator.Validate(
            snapshot.Document.RootElement,
            "renderer-operation-registry.v1.schema.json");
        return bytes;
    }

    public static string GetCanonicalSha256() =>
        Convert.ToHexString(SHA256.HashData(GetCanonicalInput())).ToLowerInvariant();
}

public static class RendererBootstrapAdapter
{
    public static byte[] BuildResponse(
        ApplicationBootstrap bootstrap,
        string sessionId,
        ulong sequence,
        string requestId)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        if (bootstrap.Compatibility?.ApplicationContract?.Value != ProtocolConstants.ContractVersion
            || bootstrap.Compatibility.DomainContract?.Value != ProtocolConstants.DomainContractVersion
            || bootstrap.Compatibility.StorageContract?.Value != ProtocolConstants.StorageContractVersion
            || bootstrap.RendererContractVersion?.Value != ProtocolConstants.RendererContractVersion
            || bootstrap.Capabilities.Count > ProtocolConstants.MaximumCapabilityFlags
            || bootstrap.RecentRuns.Count > ProtocolConstants.MaximumBootstrapRecentRuns)
        {
            throw new InvalidDataException("The bootstrap projection is incompatible or exceeds its bounds.");
        }

        JsonArray capabilities = [];
        foreach (ApplicationCapabilityState item in bootstrap.Capabilities)
        {
            capabilities.Add(new JsonObject
            {
                ["capability"] = CapabilityName(item.Capability),
                ["availability"] = AvailabilityName(item.Availability),
                ["inert_reason"] = item.InertReason,
            });
        }

        JsonArray recentRuns = [];
        foreach (RunSummary item in bootstrap.RecentRuns)
        {
            if (string.IsNullOrWhiteSpace(item.RunId?.Value))
            {
                throw new InvalidDataException("A bootstrap run identity is missing.");
            }

            recentRuns.Add(new JsonObject
            {
                ["run_id"] = item.RunId.Value,
                ["lifecycle_state"] = ClosedEnumName(item.LifecycleState),
                ["lifecycle_generation"] = item.LifecycleGeneration,
            });
        }

        JsonObject document = new()
        {
            ["contract_version"] = ProtocolConstants.RendererContractVersion,
            ["message_kind"] = "response",
            ["session_id"] = sessionId,
            ["sequence"] = sequence,
            ["request_id"] = requestId,
            ["revision"] = bootstrap.ProjectionVersion?.Value,
            ["operation"] = "application.bootstrap",
            ["payload"] = new JsonObject
            {
                ["outcome"] = "accepted",
                ["bootstrap"] = new JsonObject
                {
                    ["application_contract_version"] = bootstrap.Compatibility.ApplicationContract.Value,
                    ["domain_contract_version"] = bootstrap.Compatibility.DomainContract.Value,
                    ["storage_contract_version"] = bootstrap.Compatibility.StorageContract.Value,
                    ["renderer_contract_version"] = bootstrap.RendererContractVersion.Value,
                    ["coordinator_health"] = HealthName(bootstrap.CoordinatorHealth),
                    ["configuration_availability"] = AvailabilityName(bootstrap.Configuration?.Availability ?? Availability.Unspecified),
                    ["capabilities"] = capabilities,
                    ["recent_runs"] = recentRuns,
                    ["projection_version"] = bootstrap.ProjectionVersion?.Value,
                    ["coordinator_instance_id"] = bootstrap.CoordinatorInstanceId?.Value,
                    ["coordinator_fencing_epoch"] = bootstrap.CoordinatorFencingEpoch,
                },
            },
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document);
        _ = new RendererContractValidator(sessionId).ValidateAndAdvance(bytes);
        return bytes;
    }

    private static string CapabilityName(ApplicationCapability value) => value switch
    {
        ApplicationCapability.Bootstrap => "bootstrap",
        ApplicationCapability.RunQuery => "run-query",
        ApplicationCapability.EventResync => "event-resync",
        ApplicationCapability.Configuration => "configuration",
        ApplicationCapability.ProviderEnrollment => "provider-enrollment",
        _ => throw new InvalidDataException("The bootstrap contains an unknown or unsupported capability."),
    };

    private static string AvailabilityName(Availability value) => value switch
    {
        Availability.Available => "available",
        Availability.Partial => "partial",
        Availability.Unavailable => "unavailable",
        _ => throw new InvalidDataException("The bootstrap contains an unknown or unsupported availability."),
    };

    private static string HealthName(HealthState value) => value switch
    {
        HealthState.Healthy => "healthy",
        HealthState.Degraded => "degraded",
        HealthState.Unavailable => "unavailable",
        _ => throw new InvalidDataException("The bootstrap contains an unknown or unsupported health state."),
    };

    private static string ClosedEnumName<T>(T value) where T : struct, Enum
    {
        string name = value.ToString();
        if (!Enum.IsDefined(value)
            || name.EndsWith("Unspecified", StringComparison.Ordinal)
            || name.EndsWith("Unknown", StringComparison.Ordinal)
            || name.EndsWith("Unsupported", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The bootstrap contains an unknown or unsupported enum value.");
        }

        StringBuilder result = new();
        foreach (char character in name)
        {
            if (char.IsUpper(character) && result.Length > 0)
            {
                result.Append('-');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }
}

public static class ApplicationContractValidator
{
    public static GetApplicationBootstrapRequest ParseBootstrapRequest(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is 0 || bytes.Length > ProtocolConstants.MaximumMessageBytes)
        {
            throw new InvalidDataException("The bootstrap request exceeds its finite message bound.");
        }

        GetApplicationBootstrapRequest request;
        try
        {
            request = GetApplicationBootstrapRequest.Parser.ParseFrom(bytes.ToArray());
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException("The bootstrap request is malformed.", exception);
        }

        RejectUnknownFields(request, "$bootstrap");
        Validate(request);
        return request;
    }

    public static void Validate(GetApplicationBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RejectUnknownFields(request, "$bootstrap");
        if (request.RendererContractVersion?.Value != ProtocolConstants.RendererContractVersion)
        {
            throw new InvalidDataException("The renderer contract version is incompatible.");
        }

        if (request.MaximumRecentRuns is 0 or > ProtocolConstants.MaximumBootstrapRecentRuns)
        {
            throw new InvalidDataException("The bootstrap recent-run count exceeds its finite bound.");
        }

        if (request.ExpectedProjectionVersion is not null
            && request.ExpectedProjectionVersion.Value is not ("" or "1"))
        {
            throw new InvalidDataException("The bootstrap projection is stale; resync is required.");
        }
    }

    public static void Validate(UserOperationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        RequireId(receipt.RequestId, "request ID");
        RequireId(receipt.ReceiptId, "receipt ID");
        if (!Enum.IsDefined(receipt.Disposition)
            || receipt.Disposition is OperationDisposition.Unspecified
                or OperationDisposition.Unknown
                or OperationDisposition.Unsupported)
        {
            throw new InvalidDataException("The operation receipt disposition is not usable.");
        }

        if (receipt.Disposition == OperationDisposition.Conflict
            && (receipt.Conflict is null
                || string.IsNullOrWhiteSpace(receipt.Conflict.Current?.OpaqueValue)))
        {
            throw new InvalidDataException("A conflict receipt requires the current revision.");
        }
    }

    public static void Validate(CancellationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireId(request.RequestId, "request ID");
        RequireId(request.TargetRequestId, "target request ID");
        if (StringComparer.Ordinal.Equals(request.RequestId, request.TargetRequestId))
        {
            throw new InvalidDataException("A cancellation request cannot target itself.");
        }
    }

    private static void RequireId(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 16 or > 128
            || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new InvalidDataException($"The {name} is invalid.");
        }
    }

    private static void RejectUnknownFields(IMessage message, string path)
    {
        FieldInfo? unknown = message.GetType().GetField(
            "_unknownFields",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (unknown?.GetValue(message) is not null)
        {
            throw new InvalidDataException($"Unknown application field at {path}.");
        }

        foreach (PropertyInfo property in message.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0 || property.Name is "Descriptor" or "Parser")
            {
                continue;
            }

            object? item = property.GetValue(message);
            if (item is IMessage nested)
            {
                RejectUnknownFields(nested, path + "." + property.Name);
            }
            else if (item is IEnumerable sequence and not string and not ByteString)
            {
                foreach (object? element in sequence)
                {
                    if (element is IMessage nestedElement)
                    {
                        RejectUnknownFields(nestedElement, path + "." + property.Name);
                    }
                }
            }
        }
    }
}
