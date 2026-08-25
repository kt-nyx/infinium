using System.Security.Cryptography;

namespace Infinium.CredentialHelper;

public enum ProviderCredentialEnrollmentState
{
    EnrolledAndVerified,
    Verified,
    RotatedAndVerified,
    Disabled,
    Deleted,
}

public sealed record ProviderCredentialEnrollmentReceipt(
    ProviderCredentialEnrollmentState State,
    string ProfileId,
    string GenerationId,
    string TargetFingerprintSha256,
    bool CredentialRemoved);

/// <summary>
/// Functional credential lifecycle operations. Secret bytes are accepted only
/// as in-memory spans and are never returned in receipts.
/// </summary>
public static class ProviderCredentialEnrollment
{
    public static ProviderCredentialEnrollmentReceipt EnrollNew(
        ProviderCredentialReference reference,
        ReadOnlySpan<byte> secret)
    {
        ProviderCredentialStore.WriteNew(reference, secret);
        using ProviderCredentialLease lease = ProviderCredentialStore.ReadExact(reference);
        if (!CryptographicOperations.FixedTimeEquals(lease.Secret.Span, secret))
        {
            _ = ProviderCredentialStore.DeleteExact(reference);
            throw new InvalidDataException(
                "The newly enrolled provider credential did not verify exactly.");
        }
        return Receipt(
            ProviderCredentialEnrollmentState.EnrolledAndVerified,
            reference,
            credentialRemoved: false);
    }

    public static ProviderCredentialEnrollmentReceipt Verify(ProviderCredentialReference reference)
    {
        using ProviderCredentialLease _ = ProviderCredentialStore.ReadExact(reference);
        return Receipt(
            ProviderCredentialEnrollmentState.Verified,
            reference,
            credentialRemoved: false);
    }

    public static ProviderCredentialEnrollmentReceipt Rotate(
        ProviderCredentialReference predecessor,
        ProviderCredentialReference successor,
        ReadOnlySpan<byte> successorSecret)
    {
        if (predecessor.ProfileId != successor.ProfileId
            || predecessor.GenerationId == successor.GenerationId)
        {
            throw new InvalidDataException(
                "Rotation requires one profile and a fresh successor generation.");
        }
        using (ProviderCredentialLease _ = ProviderCredentialStore.ReadExact(predecessor))
        {
        }
        _ = EnrollNew(successor, successorSecret);
        bool predecessorDeleted = ProviderCredentialStore.DeleteExact(predecessor);
        if (!predecessorDeleted)
        {
            throw new InvalidOperationException(
                "The successor verified, but the exact predecessor could not be deleted.");
        }
        return Receipt(
            ProviderCredentialEnrollmentState.RotatedAndVerified,
            successor,
            credentialRemoved: predecessorDeleted);
    }

    public static ProviderCredentialEnrollmentReceipt Disable(ProviderCredentialReference reference)
    {
        bool removed = ProviderCredentialStore.DeleteExact(reference);
        if (!removed || ProviderCredentialStore.ExistsExact(reference))
        {
            throw new InvalidOperationException(
                "The exact provider credential could not be removed while disabling it.");
        }
        return Receipt(
            ProviderCredentialEnrollmentState.Disabled,
            reference,
            credentialRemoved: true);
    }

    public static ProviderCredentialEnrollmentReceipt Delete(ProviderCredentialReference reference)
    {
        _ = ProviderCredentialStore.DeleteExact(reference);
        if (ProviderCredentialStore.ExistsExact(reference))
        {
            throw new InvalidOperationException(
                "The exact provider credential remained after deletion.");
        }
        return Receipt(
            ProviderCredentialEnrollmentState.Deleted,
            reference,
            credentialRemoved: true);
    }

    private static ProviderCredentialEnrollmentReceipt Receipt(
        ProviderCredentialEnrollmentState state,
        ProviderCredentialReference reference,
        bool credentialRemoved) =>
        new(
            state,
            reference.ProfileId,
            reference.GenerationId,
            reference.TargetFingerprintSha256(),
            credentialRemoved);
}
