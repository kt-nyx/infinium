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
    public const string CredentialReplacementBoundaryFileName =
        "credential-replacement-helper-boundary.v1.json";

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

    public string StageCredentialReplacementBoundary(string attemptId, ReadOnlySpan<byte> canonicalBoundary)
    {
        ValidateCredentialIdentity(attemptId, nameof(attemptId));
        if (canonicalBoundary.Length is 0 or > 1_048_576)
        {
            throw new InvalidDataException("The credential replacement boundary exceeds its staging bound.");
        }
        string relativePath = Path.Combine(attemptId, CredentialReplacementBoundaryFileName);
        using (AttemptStagingAuthority staging = Paths.CreateAttemptStagingDirectory(attemptId))
        using (FileStream stream = Paths.CreateNewFile(ProductWriteClass.AttemptStaging, relativePath))
        {
            stream.Write(canonicalBoundary);
            stream.Flush(flushToDisk: true);
        }
        return relativePath;
    }

    public byte[] ReadCredentialReplacementBoundary(string attemptId)
    {
        ValidateCredentialIdentity(attemptId, nameof(attemptId));
        string relativePath = Path.Combine(attemptId, CredentialReplacementBoundaryFileName);
        using WindowsHandleRelativeStorage.AdmissionSource source =
            Paths.OpenAdmissionSource(ProductWriteClass.AttemptStaging, relativePath);
        using MemoryStream destination = new();
        WindowsHandleRelativeStorage.AdmissionCopyResult copied =
            source.CopyToAndHash(destination, 1_048_576);
        if (copied.ByteLength <= 0)
        {
            throw new InvalidDataException("The staged credential replacement boundary is empty.");
        }
        return destination.ToArray();
    }

    public HelperStagingReceipt AdmitExistingHelperReceipt(
        string attemptId,
        ReadOnlySpan<byte> canonicalFrame,
        DateTimeOffset now)
    {
        ValidateCredentialIdentity(attemptId, nameof(attemptId));
        if (canonicalFrame.Length is 0 or > 1_048_576)
        {
            throw new InvalidDataException("The existing helper receipt exceeds its staging bound.");
        }
        string relativePath = Path.Combine(attemptId, "helper-receipt.v2.pb");
        using (WindowsHandleRelativeStorage.AdmissionSource source =
            Paths.OpenAdmissionSource(ProductWriteClass.AttemptStaging, relativePath))
        using (MemoryStream destination = new())
        {
            _ = source.CopyToAndHash(destination, 1_048_576);
            if (!destination.ToArray().AsSpan().SequenceEqual(canonicalFrame))
            {
                throw new InvalidDataException("The existing helper receipt is not the exact validated frame.");
            }
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            using SqliteCommand count = connection.CreateCommand();
            count.Transaction = transaction;
            count.CommandText =
                "SELECT COUNT(*) FROM audit_events WHERE event_kind='helper-receipt-admitted' "
                + "AND object_kind='helper-attempt' AND object_id=$attempt;";
            count.Parameters.AddWithValue("$attempt", attemptId);
            long admissions = Convert.ToInt64(
                count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (admissions > 1)
            {
                throw new InvalidDataException("The helper receipt has multiple durable admission events.");
            }
            if (admissions == 0)
            {
                InsertAuditEvent("helper-receipt-admitted", "helper-attempt", attemptId, now, transaction);
            }
            transaction.Commit();
        }
        return new(
            attemptId, relativePath, canonicalFrame.Length,
            Convert.ToHexString(SHA256.HashData(canonicalFrame)).ToLowerInvariant(),
            null, 0, null, true, true);
    }

    internal long HelperReceiptAdmissionCount(string attemptId)
    {
        ValidateCredentialIdentity(attemptId, nameof(attemptId));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM audit_events WHERE event_kind='helper-receipt-admitted' "
                + "AND object_kind='helper-attempt' AND object_id=$attempt;";
            command.Parameters.AddWithValue("$attempt", attemptId);
            return Convert.ToInt64(
                command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public bool TryAdmitCredentialReplacementHelperLaunch(string attemptId, DateTimeOffset now)
    {
        ValidateCredentialIdentity(attemptId, nameof(attemptId));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            using SqliteCommand count = connection.CreateCommand();
            count.Transaction = transaction;
            count.CommandText =
                "SELECT COUNT(*) FROM audit_events "
                + "WHERE event_kind='credential-replacement-helper-launch-admitted' "
                + "AND object_kind='helper-attempt' AND object_id=$attempt;";
            count.Parameters.AddWithValue("$attempt", attemptId);
            long admissions = Convert.ToInt64(
                count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (admissions > 1)
            {
                throw new InvalidDataException("The replacement helper has multiple launch admissions.");
            }
            if (admissions == 1)
            {
                transaction.Commit();
                return false;
            }
            InsertAuditEvent(
                "credential-replacement-helper-launch-admitted", "helper-attempt", attemptId, now, transaction);
            transaction.Commit();
            return true;
        }
    }

    public bool HasExactCredentialReplacementHelperLaunchAdmission(string attemptId)
    {
        ValidateCredentialIdentity(attemptId, nameof(attemptId));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM audit_events "
                + "WHERE event_kind='credential-replacement-helper-launch-admitted' "
                + "AND object_kind='helper-attempt' AND object_id=$attempt;";
            command.Parameters.AddWithValue("$attempt", attemptId);
            return Convert.ToInt64(
                command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
        }
    }
}
