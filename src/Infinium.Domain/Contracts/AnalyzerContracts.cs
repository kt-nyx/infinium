namespace Infinium.Domain.Contracts;

public enum AnalyzerMaturity
{
    Unspecified,
    Experimental,
}

public enum ExecutionRequirement
{
    Unspecified,
    LocalOnly,
    CachedExternalEvidence,
    LiveNetwork,
    LlmProvider,
}

public enum CliExitCode
{
    Success = 0,
    InvalidInput = 2,
    Unsupported = 3,
    Failed = 4,
    Cancelled = 5,
    LimitReached = 6,
}

public enum CliOutcome
{
    Unspecified,
    Completed,
    CompletedWithGaps,
    Failed,
    Cancelled,
    LimitReached,
    InvalidInput,
    Unsupported,
}

public enum DiagnosticSensitivityLabel
{
    Unspecified,
    SensitiveDevelopmentDiagnostic,
}

public enum DiagnosticSharingClass
{
    Unspecified,
    PrivateDiagnostic,
}

public enum DiagnosticRedactionState
{
    Unspecified,
    VerifiedSecretFree,
    RedactedAndVerified,
}

public enum DiagnosticSeverity
{
    Unspecified,
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
}

public enum DiagnosticDataClass
{
    Unspecified,
    Operational,
    Identifier,
    Path,
    Fingerprint,
    Usage,
    Cost,
    Error,
}

public enum DiagnosticFieldRedaction
{
    Unspecified,
    NotSensitive,
    Redacted,
    Hashed,
}

public enum AnalyzerCostClass
{
    Unspecified,
    LocalLow,
    LocalModerate,
    LocalHigh,
    ProviderBounded,
}

public sealed record AnalyzerThresholdContract(
    string Name,
    string Version,
    string Rule);

public sealed record ReasonedAnalyzerScopeContract(
    string ScopeId,
    string Reason);

public sealed record AnalyzerScopeContract(
    IReadOnlyList<string> SupportedInputs,
    IReadOnlyList<ReasonedAnalyzerScopeContract> ExcludedInputs,
    IReadOnlyList<string> SupportedRecordFieldAssetShapes,
    IReadOnlyList<ReasonedAnalyzerScopeContract> ExcludedRecordFieldAssetShapes,
    IReadOnlyList<string> SupportedTaxonomyCodes,
    IReadOnlyList<ReasonedAnalyzerScopeContract> UnsupportedTaxonomyCodes,
    IReadOnlyList<string> SupportedExtentFacets,
    IReadOnlyList<ReasonedAnalyzerScopeContract> ExcludedExtentFacets);

public sealed record AnalyzerInputPopulationContract(
    string PopulationId,
    string Description,
    bool Required);

public sealed record AnalyzerDependencyContract(
    string DependencyId,
    ContractVersion MinimumVersion,
    bool Required,
    CoverageState MissingState);

public sealed record AnalyzerThresholdsContract(
    AnalyzerThresholdContract CandidateAdmission,
    AnalyzerThresholdContract Evidence,
    AnalyzerThresholdContract Abstention,
    AnalyzerThresholdContract FindingPromotion);

public sealed record AnalyzerCoverageDeclarationContract(
    IReadOnlyList<string> Populations,
    IReadOnlyList<CoverageState> PossibleStates,
    string UnsupportedBehavior);

public sealed record AnalyzerOperationRequirementsContract(
    ExecutionRequirement Mode,
    bool NetworkRequired,
    bool LlmRequired,
    bool ProviderRequired);

public sealed record AnalyzerScaleAndCostContract(
    string PopulationScale,
    AnalyzerCostClass CostClass,
    bool Billable);

public sealed record AnalyzerResourceBoundsContract(
    long MaximumInputItems,
    long MaximumOutputItems,
    long MaximumWallTimeMilliseconds);

public sealed record LinkedEvaluationCasesContract(
    IReadOnlyList<string> Positive,
    IReadOnlyList<string> Negative,
    IReadOnlyList<string> Boundary,
    IReadOnlyList<string> Malformed,
    IReadOnlyList<string> CrossCategory,
    IReadOnlyList<string> Gap);

public sealed record PayloadContractDeclarationContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    bool Required);

public sealed record AnalyzerDeclarationContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    string AnalyzerId,
    ContractVersion AnalyzerVersion,
    ContractVersion SemanticContractVersion,
    ContractVersion IdentityContractVersion,
    ContractVersion RulesetVersion,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    AnalyzerScopeContract Scope,
    IReadOnlyList<AnalyzerInputPopulationContract> InputPopulations,
    IReadOnlyList<AnalyzerDependencyContract> Dependencies,
    SnapshotAssuranceState MinimumSnapshotAssurance,
    AnalyzerThresholdsContract Thresholds,
    IReadOnlyList<string> PossibleOutputTypes,
    AnalyzerCoverageDeclarationContract Coverage,
    AnalyzerOperationRequirementsContract OperationRequirements,
    AnalyzerScaleAndCostContract ExpectedScaleAndCost,
    AnalyzerResourceBoundsContract ResourceBounds,
    AnalyzerMaturity Maturity,
    bool RawDevelopmentOutput,
    bool PresetOrMaturitySuppression,
    LinkedEvaluationCasesContract LinkedEvaluationCases,
    IReadOnlyList<PayloadContractDeclarationContract> PayloadContracts,
    ContractVersion StateModelVersion,
    IReadOnlyList<ExecutionBoundaryContract> NotUsedBoundaries)
{
    public string AnalyzerFamily { get; init; } = AnalyzerId;
}
