namespace Infinium.Persistence;

public sealed record CredentialProfileProjection(
    string ProfileId,
    string GenerationId,
    long GenerationOrdinal,
    long RevocationEpoch,
    string LifecycleState,
    string VerificationState,
    string? CapabilitySnapshotId,
    string? AccountIdentityId,
    string? BillingScopeIdentityId,
    string? IntentId,
    string RecoveryDisposition,
    string CleanupDisposition,
    long ProjectionVersion,
    DateTimeOffset UpdatedAt);

public sealed record CredentialTransitionRequest(
    string RootId,
    string ProfileId,
    string GenerationId,
    string IntentKind,
    string FromState,
    string ToState,
    string TerminalState,
    string? CapabilitySnapshotId,
    string? AccountIdentityId,
    string? BillingScopeIdentityId,
    DateTimeOffset PendingAt,
    DateTimeOffset TerminalAt,
    bool SecureStoreUnavailable = false,
    bool Failed = false,
    bool Cancelled = false,
    bool IncrementRevocationEpoch = false);
