using Google.Protobuf;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;

namespace Infinium.Application.Runtime;

public static class ProtocolConstants
{
    public const uint Major = 1;
    public const uint Minor = 2;
    public const string ContractVersion = "1.2.0";
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
        DomainContract = new SemanticVersion { Value = ContractVersion },
        StorageContract = new SemanticVersion { Value = ContractVersion },
    };

    public static ProtocolVersion Version { get; } = new()
    {
        Major = Major,
        Minor = Minor,
        SchemaFingerprintSha256 = ByteString.CopyFrom(
            Convert.FromHexString("676a0c655ca5f7a7ec70de386892b4142e11b73825b5289fdc465ecd0853f937")),
    };
}
