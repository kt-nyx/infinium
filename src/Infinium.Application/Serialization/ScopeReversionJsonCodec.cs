using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class ScopeReversionJsonCodec
{
    public static byte[] Serialize(ScopeReversionAnalysisContract value) =>
        SchemaValidatedJsonCodec.Serialize(
            value,
            "scope-reversion-analysis.v1.schema.json",
            static item => ScopeReversionContractInvariants.Validate(item));

    public static ScopeReversionAnalysisContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<ScopeReversionAnalysisContract>(
            bytes,
            "scope-reversion-analysis.v1.schema.json",
            static item => ScopeReversionContractInvariants.Validate(item));
}
