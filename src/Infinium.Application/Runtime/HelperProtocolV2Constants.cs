namespace Infinium.Application.Runtime;

public static class HelperProtocolV2Constants
{
    public const uint Major = 2;
    public const uint Minor = 0;
    public const string ContractVersion = "2.0.0";
    public const uint MaximumFrameBytes = 1_048_576;
    public const uint MaximumRequestBytes = 65_536;
    public const uint MaximumResponseBytes = 1_048_576;
    public const uint MaximumStatusFrames = 16;

    // Canonical SHA-256 of helper v2 and its direct transitive common/identity contracts.
    // It is deliberately independent from the versioned application contract set.
    public const string SchemaFingerprintSha256 = "edd9f428df33a5c8f1b9aa8145799be99afbd5c9c98c9b7572d903865e026ca3";
}
