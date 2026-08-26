using Google.Protobuf;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Persistence;

namespace Infinium.Application.Runtime;

public static class ProtocolConstants
{
    public const uint Major = 1;
    public const uint Minor = 6;
    public const string ContractVersion = "1.6.0";
    public const string DomainContractVersion = "1.3.0";
    public static readonly string StorageContractVersion =
        AuthoritativeStore.CurrentStorageContractVersion;
    public const string RendererContractVersion = "1.0.0";
    public const uint MaximumBootstrapRecentRuns = 20;
    public const uint MaximumMessageBytes = 1_048_576;
    public const uint MaximumPageItems = 100;
    public const uint MaximumChunkBytes = 262_144;
    public const uint MaximumStreamQueueItems = 64;
    public const uint MaximumFilterTerms = 16;
    public const uint MaximumSortTerms = 4;
    public const uint MaximumCapabilityFlags = 16;
    public const uint MaximumStagedOutputs = 32;
    public const uint MaximumDiagnosticBytes = 65_536;

    public static ProtocolLimits Limits { get; } = new()
    {
        MaximumMessageBytes = MaximumMessageBytes,
        MaximumPageItems = MaximumPageItems,
        MaximumChunkBytes = MaximumChunkBytes,
        MaximumStreamQueueItems = MaximumStreamQueueItems,
        MaximumFilterTerms = MaximumFilterTerms,
        MaximumSortTerms = MaximumSortTerms,
        MaximumCapabilityFlags = MaximumCapabilityFlags,
        MaximumStagedOutputs = MaximumStagedOutputs,
        MaximumDiagnosticBytes = MaximumDiagnosticBytes,
        DefaultUnaryDeadline = new DurationMillis { Value = 15_000 },
        MaximumUnaryDeadline = new DurationMillis { Value = 60_000 },
    };

    public static ContractCompatibility Compatibility { get; } = new()
    {
        ApplicationContract = new SemanticVersion { Value = ContractVersion },
        DomainContract = new SemanticVersion { Value = DomainContractVersion },
        StorageContract = new SemanticVersion { Value = StorageContractVersion },
    };

    public static ProtocolVersion Version { get; } = new()
    {
        Major = Major,
        Minor = Minor,
        SchemaFingerprintSha256 = ByteString.CopyFrom(
            Convert.FromHexString("a95cf80fa175461c93aa0ad36c8d7dc02bd8dee55b97fd142c4d0d85ddd86353")),
    };
}
