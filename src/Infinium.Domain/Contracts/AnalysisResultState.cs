namespace Infinium.Domain.Contracts;

public enum AnalysisResultState
{
    Unspecified,
    Present,
    ResolvedNegative,
    Missing,
    InvalidInput,
    Unsupported,
    Ambiguous,
    Partial,
    Abstained,
    NotApplicable,
    NotUsed,
    Failed,
    Cancelled,
    LimitReached,
    Unavailable,
    Unknown,
}

public enum EvidenceLayer
{
    Unspecified,
    Structural,
    Observed,
    Decoded,
    Resolved,
    Semantic,
}

public enum ClaimKind
{
    Unspecified,
    DeclaredPurpose,
    Requirement,
    Incompatibility,
    InstallationInstruction,
    PriorityInstruction,
    LifecycleInstruction,
    ConfigurationInstruction,
    PatchInstruction,
    KnownIssue,
}

public enum ClaimApplicabilityState
{
    Unspecified,
    Applicable,
    NotApplicable,
    Unknown,
    Unsupported,
    Contradicted,
}

public enum DocumentationSourceKind
{
    Unspecified,
    ProjectAuthoredLocal,
    Fixture,
}

public enum DocumentationSourceAvailability
{
    Unspecified,
    Present,
    Deleted,
    Unavailable,
}

public enum DocumentationImportMode
{
    Unspecified,
    CleanImport,
    RetainedReuse,
}

public enum DocumentationGapKind
{
    Unspecified,
    Contradiction,
    Deletion,
    UnavailableSource,
    Replay,
}

public enum CandidateLane
{
    Unspecified,
    DeterministicRequired,
    MandatoryEvidence,
    OptionalRanked,
}

public enum CandidateDecisionDisposition
{
    Unspecified,
    CandidateAdmitted,
    ResolvedNegative,
    Unsupported,
    Ambiguous,
    InvalidInput,
    Limited,
    Deferred,
    Unprocessed,
    Abstained,
    Failed,
}

public enum AnalysisConfidence
{
    Unspecified,
    SpeculativeLead,
    Plausible,
    StronglySupported,
    Confirmed,
}

public enum FindingSeverity
{
    Unspecified,
    Advisory,
    Minor,
    Moderate,
    Major,
    Blocker,
}

public enum RecommendationKind
{
    Unspecified,
    Remediation,
    AlternativeRemediation,
    Validation,
    FurtherInvestigation,
    Abstention,
}

public enum FindingPromotionOutcome
{
    Unspecified,
    SupportedFinding,
    LeadOnly,
    Abstained,
}

public enum WorstCredibleConsequence
{
    Unspecified,
    MaintenanceOnly,
    LocalizedLowImpact,
    MeaningfulBoundedLoss,
    ImportantRequirementFailure,
    UsefulPlaythroughBlocked,
}

public enum CoverageMemberState
{
    Unspecified,
    Completed,
    CompletedWithGaps,
    Failed,
    SkippedByConfiguration,
    SkippedByLimit,
    Unsupported,
}

public enum FindingGapState
{
    Unspecified,
    MissingInformation,
    MissingCapability,
    MissingDependency,
    Unsupported,
    Failed,
    Limited,
    Unavailable,
    Deleted,
    AuditGap,
}

public enum GapReplayEffect
{
    Unspecified,
    None,
    Partial,
    AuditOnly,
    Unavailable,
}

public enum GapConclusionEffect
{
    Unspecified,
    None,
    Bounded,
    Abstain,
    Unavailable,
}

public enum ReconciliationGateState
{
    Unspecified,
    ProvenEquivalent,
    ProvenDifferent,
    Ambiguous,
    Unknown,
    NotEvaluated,
}

public enum LineageKind
{
    Unspecified,
    Supersedes,
    AnalyticalRevision,
    RelatedFollowUp,
    PromotesLead,
    MergeSuccessor,
    SplitSuccessor,
    CorrectionSuccessor,
}

public enum ReplayMode
{
    Unspecified,
    Clean,
    Incremental,
    RetainedDownstreamReplay,
}

public enum ReplayState
{
    Unspecified,
    CompleteClean,
    Partial,
    AuditOnly,
    Unavailable,
    FailedIdentityDrift,
}

public enum BoundaryUseState
{
    Unspecified,
    Used,
    NotUsed,
    Unsupported,
}
