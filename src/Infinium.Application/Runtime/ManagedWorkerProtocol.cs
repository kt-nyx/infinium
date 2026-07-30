using System.Security.Cryptography;
using System.Text;

namespace Infinium.Application.Runtime;

public sealed record ManagedWorkerBootstrap(
    int SchemaVersion,
    string BootstrapId,
    string CoordinatorInstanceId,
    long CoordinatorFencingEpoch,
    string RunId,
    string AttemptId,
    long AttemptFencingToken,
    string WorkerPipe,
    int ExpectedProcessId,
    string StagingAreaId,
    string StagedArtifactId,
    long InheritedStagingDirectoryHandle,
    string OutputRelativeName,
    long MaximumOutputBytes,
    string OneUseNonceBase64,
    DateTimeOffset ExpiresAt,
    ManagedWorkerOperationKind OperationKind = ManagedWorkerOperationKind.SubstrateValidation,
    string OutputSchemaVersion = ManagedWorkerManifest.OutputSchemaVersion,
    ManagedMo2SnapshotCaptureAssignment? Mo2SnapshotCapture = null);

public enum ManagedWorkerOperationKind
{
    SubstrateValidation,
    Mo2SnapshotCapture,
}

public sealed record ManagedMo2SnapshotCaptureAssignment(
    string Mo2ExecutablePath,
    string InstanceRoot,
    string InstanceIniPath,
    string ProfilesRoot,
    string ModsRoot,
    string OverwriteRoot,
    string GameDataRoot,
    string SkyrimExecutablePath,
    string SelectedProfileName,
    string Platform,
    string DistributionChannel,
    string ApplicationId,
    IReadOnlyList<ManagedQualifiedMappingAssignment> QualifiedMappings,
    IReadOnlyList<string> EnabledMapperSha256s);

public sealed record ManagedQualifiedMappingAssignment(
    string MappingId,
    string SourceRoot,
    string VirtualPrefix,
    string MapperSha256);

public sealed record ManagedWorkerResult(
    int SchemaVersion,
    string BootstrapId,
    string AttemptId,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    string OutputRelativeName,
    string Sha256,
    long ByteLength,
    string ManifestSha256);

public static class ManagedWorkerManifest
{
    public const string OutputSchemaVersion = "1.0.0";

    public static byte[] GetCanonicalBytes(
        string stagedArtifactId,
        string typedRelativeName,
        string contentSha256,
        long byteLength,
        string outputSchemaVersion = OutputSchemaVersion)
    {
        string canonical = string.Join(
            '\n',
            "infinium-worker-manifest-v1",
            stagedArtifactId,
            typedRelativeName,
            "typed-result",
            contentSha256,
            byteLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            outputSchemaVersion);
        return Encoding.UTF8.GetBytes(canonical);
    }

    public static byte[] ComputeDigest(
        string stagedArtifactId,
        string typedRelativeName,
        string contentSha256,
        long byteLength,
        string outputSchemaVersion = OutputSchemaVersion) =>
        SHA256.HashData(GetCanonicalBytes(
            stagedArtifactId,
            typedRelativeName,
            contentSha256,
            byteLength,
            outputSchemaVersion));
}
