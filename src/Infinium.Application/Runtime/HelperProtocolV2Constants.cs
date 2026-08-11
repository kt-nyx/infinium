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
    public const string SchemaFingerprintSha256 = "2b447cc75a94be781cc22373303670fd4c8143afb26ef112baf8b8739cd129dd";
}
