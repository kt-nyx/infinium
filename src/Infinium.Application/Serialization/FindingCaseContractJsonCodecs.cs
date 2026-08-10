using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class FindingCaseJsonCodec
{
    public static byte[] Serialize(FindingCaseContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "finding-case.v1.schema.json", static item => FindingCaseContractInvariants.Validate(item));

    public static FindingCaseContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<FindingCaseContract>(bytes, "finding-case.v1.schema.json", static item => FindingCaseContractInvariants.Validate(item));
}

public static class FindingCaseInputJsonCodec
{
    public static byte[] Serialize(FindingCaseInputContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "finding-case-input.v1.schema.json", static item => FindingCaseContractInvariants.Validate(item));

    public static FindingCaseInputContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<FindingCaseInputContract>(bytes, "finding-case-input.v1.schema.json", static item => FindingCaseContractInvariants.Validate(item));
}
