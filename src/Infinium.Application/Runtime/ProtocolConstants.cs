using Google.Protobuf;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Persistence;

namespace Infinium.Application.Runtime;

public static class ProtocolConstants
{
    public const uint Major = 1;
    public const uint Minor = 10;
    public const string ContractVersion = "1.10.0";
    public const string DomainContractVersion = "1.4.0";
    public static readonly string StorageContractVersion =
        AuthoritativeStore.CurrentStorageContractVersion;
    public const string RendererContractVersion = "1.1.0";
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
            Convert.FromHexString("c51f6c400547b948fd7f350ef5ac72f29d6032b2671cfba957a7be71cfc44e74")),
    };
}
