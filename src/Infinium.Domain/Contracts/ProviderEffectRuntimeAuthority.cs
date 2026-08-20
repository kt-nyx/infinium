namespace Infinium.Domain.Contracts;

public enum ProviderEffectAuthorityScope
{
    EffectFreeRehearsal,
    ExternalEffect,
}

public enum ProviderEffectAuthorityKind
{
    CredentialEnrollment,
    CredentialEvidenceRecovery,
    TransportQualification,
    SourceClaimExtraction,
    CandidateInvestigation,
}

public sealed record ProviderEffectAuthorityLimits(
    int HelperLaunches,
    int CredentialNativeCalls,
    int ProviderStarts,
    int DnsResolutions,
    int BillableOperations,
    int LiteralLoopbackStarts,
    bool AutomaticRetry,
    bool FourthCallPermitted);

public sealed record ProviderEffectAuthorityExecution(
    string OutputRootRelative,
    string LedgerPathRelative,
    string ProductStateRootRelative,
    string CoordinatorPathRelative,
    string HelperPathRelative);

public sealed record ProviderEffectRuntimeAuthority(
    string AuthorityId,
    ProviderEffectAuthorityScope Scope,
    ProviderEffectAuthorityKind Kind,
    string SubjectManifestId,
    string SubjectManifestSha256,
    string CampaignId,
    string CampaignManifestSha256,
    string PredecessorLedgerEventHash,
    string PredecessorEvidenceId,
    string PredecessorEvidenceSha256,
    string ImplementationCommit,
    string CoordinatorSha256,
    string HelperSha256,
    string ReviewEvidenceId,
    string ReviewEvidenceSha256,
    string OwnerDecisionId,
    string OwnerDecisionSha256,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc,
    ProviderEffectAuthorityExecution Execution,
    ProviderEffectAuthorityLimits Limits,
    string ManifestSha256);
