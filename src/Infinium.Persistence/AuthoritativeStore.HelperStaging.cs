using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed record HelperStagingReceipt(
    string AttemptId,
    string RelativePath,
    long ByteLength,
    string Sha256,
    string? ResponseRelativePath,
    long ResponseByteLength,
    string? ResponseSha256,
    bool StagedBeforeAdmission,
    bool CoordinatorOnlyAdmission);

public sealed partial class AuthoritativeStore
{
    public HelperStagingReceipt StageAndAdmitHelperReceipt(
        string attemptId,
        ReadOnlySpan<byte> canonicalFrame,
        DateTimeOffset now,
        ReadOnlySpan<byte> stagedResponse = default)
    {
        ValidateCredentialIdentity(attemptId, nameof(attemptId));
        if (canonicalFrame.Length is 0 or > 1_048_576)
        {
            throw new InvalidDataException("The helper receipt exceeds its staging bound.");
        }
        string relativePath = Path.Combine(attemptId, "helper-receipt.v2.pb");
        using (AttemptStagingAuthority staging = Paths.CreateAttemptStagingDirectory(attemptId))
        using (FileStream stream = Paths.CreateNewFile(ProductWriteClass.AttemptStaging, relativePath))
        {
            stream.Write(canonicalFrame);
            stream.Flush(flushToDisk: true);
        }
        string sha256 = Convert.ToHexString(SHA256.HashData(canonicalFrame)).ToLowerInvariant();
        string? responseRelativePath = null;
        string? responseSha256 = null;
        if (!stagedResponse.IsEmpty)
        {
            if (stagedResponse.Length > 1_048_576)
            {
                throw new InvalidDataException("The helper response exceeds its staging bound.");
            }
            responseRelativePath = Path.Combine(attemptId, "provider-response.v2.bin");
            using FileStream response = Paths.CreateNewFile(ProductWriteClass.AttemptStaging, responseRelativePath);
            response.Write(stagedResponse);
            response.Flush(flushToDisk: true);
            responseSha256 = Convert.ToHexString(SHA256.HashData(stagedResponse)).ToLowerInvariant();
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            InsertAuditEvent("helper-receipt-admitted", "helper-attempt", attemptId, now, transaction);
            transaction.Commit();
        }
        return new(
            attemptId, relativePath, canonicalFrame.Length, sha256,
            responseRelativePath, stagedResponse.Length, responseSha256, true, true);
    }
}
