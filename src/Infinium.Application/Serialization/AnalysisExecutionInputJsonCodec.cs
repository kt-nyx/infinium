using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class AnalysisExecutionInputJsonCodec
{
    public static byte[] Serialize(AnalysisExecutionInputContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "analysis-execution-input.v1.schema.json", static item => AnalysisExecutionContractInvariants.Validate(item));

    public static AnalysisExecutionInputContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<AnalysisExecutionInputContract>(bytes, "analysis-execution-input.v1.schema.json", static item => AnalysisExecutionContractInvariants.Validate(item));
}
