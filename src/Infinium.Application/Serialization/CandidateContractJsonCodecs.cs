using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class CandidateAnalysisJsonCodec
{
    public static byte[] Serialize(CandidateAnalysisContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "candidate-analysis.v1.schema.json", static item => CandidateAnalysisContractInvariants.Validate(item));

    public static CandidateAnalysisContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<CandidateAnalysisContract>(bytes, "candidate-analysis.v1.schema.json", static item => CandidateAnalysisContractInvariants.Validate(item));
}

public static class CandidateDeliveredInputJsonCodec
{
    public static byte[] Serialize(CandidateDeliveredInputContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "candidate-delivered-input.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));

    public static CandidateDeliveredInputContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<CandidateDeliveredInputContract>(bytes, "candidate-delivered-input.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));
}

public static class CandidateDeliveredExpansionJsonCodec
{
    public static byte[] Serialize(CandidateDeliveredExpansionContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "candidate-delivered-expansion.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));

    public static CandidateDeliveredExpansionContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<CandidateDeliveredExpansionContract>(bytes, "candidate-delivered-expansion.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));
}
