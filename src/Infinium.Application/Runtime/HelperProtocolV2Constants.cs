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

    // Canonical SHA-256 of the complete generated protobuf contract set.
    public const string SchemaFingerprintSha256 = "80bb28272b9d514b6f0819d0f7532a3c9704fc3f4d543cdb803f88798fe4534c";
}
