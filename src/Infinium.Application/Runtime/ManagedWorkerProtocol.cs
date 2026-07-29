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
    string StagingDirectory,
    string OutputRelativeName,
    long MaximumOutputBytes,
    string OneUseNonceBase64,
    DateTimeOffset ExpiresAt);

public sealed record ManagedWorkerResult(
    int SchemaVersion,
    string BootstrapId,
    string AttemptId,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    string OutputRelativeName,
    string Sha256,
    long ByteLength);

public static class ManagedWorkerManifest
{
    public static byte[] ComputeDigest(
        string stagedArtifactId,
        string typedRelativeName,
        string contentSha256,
        long byteLength)
    {
        string canonical = string.Join(
            '\n',
            "infinium-worker-manifest-v1",
            stagedArtifactId,
            typedRelativeName,
            "typed-result",
            contentSha256,
            byteLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "1.0.0");
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }
}
