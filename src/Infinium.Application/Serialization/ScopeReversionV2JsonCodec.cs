using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class ScopeReversionV2JsonCodec
{
    public static byte[] Serialize(ScopeReversionV2AnalysisContract value) =>
        SchemaValidatedJsonCodec.Serialize(
            value,
            "scope-reversion-analysis.v2.schema.json",
            static item => ScopeReversionV2Contract.Validate(item));

    public static ScopeReversionV2AnalysisContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<ScopeReversionV2AnalysisContract>(
            bytes,
            "scope-reversion-analysis.v2.schema.json",
            static item => ScopeReversionV2Contract.Validate(item));
}
