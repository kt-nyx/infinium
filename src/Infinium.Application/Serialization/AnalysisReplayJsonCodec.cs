using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class AnalysisReplayJsonCodec
{
    public static byte[] Serialize(AnalysisReplayContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "analysis-replay.v1.schema.json", static item => AnalysisReplayContractInvariants.Validate(item));

    public static AnalysisReplayContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<AnalysisReplayContract>(bytes, "analysis-replay.v1.schema.json", static item => AnalysisReplayContractInvariants.Validate(item));
}
