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
    LinkedEvaluationCasesContract LinkedEvaluationCases);

public sealed record DiagnosticTraceFieldContract(
    string Name,
    DiagnosticDataClass DataClass,
    DiagnosticFieldRedaction Redaction,
    System.Text.Json.JsonElement Value);

public sealed record DiagnosticTraceEventContract(
    long Sequence,
    UtcTimestamp Timestamp,
    DiagnosticSeverity Severity,
    OpaqueId ComponentId,
    string EventCode,
    string Message,
    IReadOnlyList<DiagnosticTraceFieldContract> Fields,
    IReadOnlyList<ArtifactReferenceContract> PayloadReferences);

public sealed record DiagnosticTraceContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId TraceId,
    OpaqueId RunId,
    DiagnosticSensitivityLabel SensitivityLabel,
    DiagnosticSharingClass SharingClass,
    bool CredentialMaterialPresent,
    DiagnosticRedactionState RedactionState,
    UtcTimestamp CreatedAt,
    IReadOnlyList<DiagnosticTraceEventContract> Events);

public sealed record TypedOutputCountsContract(
    long Observations,
    long DeterministicResults,
    long ExternalClaims,
    long ApplicationLinks,
    long DiscoveryLeads,
    long ModelProposals,
    long ProposalAdmissions,
    long Candidates,
    long Hypotheses,
    long Findings,
    long Recommendations,
    long SupportedCases,
    long LeadOnlyCases,
    long Abstentions,
    long InvalidInputs,
    long CoverageGaps,
    long Failures);

public sealed record CoverageStateCountsContract(
    long Completed,
    long CompletedWithGaps,
    long Failed,
    long SkippedByConfiguration,
    long SkippedByLimit,
    long Unsupported);

public sealed record CliSummaryContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId RunId,
    CliOutcome Outcome,
    CliExitCode ExitCode,
    TypedOutputCountsContract TypedCounts,
    CoverageStateCountsContract CoverageStateCounts,
    ReadinessScope ReadinessScope,
    bool NoSafetyGuarantee);

public sealed record RunOutputContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId RunId,
    OpaqueId SnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId EffectiveScanConfigurationId,
    OpaqueId ResolvedInputManifestId,
    Sha256Fingerprint CliSummaryFingerprint,
    IReadOnlyList<ObservationContract> Observations,
    IReadOnlyList<DeterministicResultContract> DeterministicResults,
    IReadOnlyList<ExternalClaimContract> ExternalClaims,
    IReadOnlyList<ExternalClaimApplicationLinkContract> ExternalClaimApplicationLinks,
    IReadOnlyList<DiscoveryLeadContract> DiscoveryLeads,
    IReadOnlyList<ModelProposalContract> ModelProposals,
    IReadOnlyList<ProposalAdmissionContract> ProposalAdmissions,
    IReadOnlyList<CandidateContract> Candidates,
    IReadOnlyList<HypothesisContract> Hypotheses,
    IReadOnlyList<FindingOccurrenceContract> Findings,
    IReadOnlyList<RecommendationContract> Recommendations,
    IReadOnlyList<CaseOccurrenceContract> SupportedCases,
    IReadOnlyList<CaseOccurrenceContract> LeadOnlyCases,
    IReadOnlyList<AbstentionContract> Abstentions,
    IReadOnlyList<InvalidInputContract> InvalidInputs,
    IReadOnlyList<CoverageGapContract> CoverageGaps,
    IReadOnlyList<FailureContract> Failures,
    IReadOnlyList<TypedCollectionStateContract> CollectionStates,
    IReadOnlyList<TaxonomyAssignmentContract> TaxonomyAssignments,
    IReadOnlyList<CoverageContract> Coverage,
    IReadOnlyList<AnalyzerDeclarationContract> AnalyzerDeclarations,
    ReadinessPlaceholderContract Readiness,
    ReplayabilityAssessmentContract Replayability,
    AuditabilityAssessmentContract Auditability,
    bool PotentiallySensitive,
    IReadOnlyList<string> UnsupportedCapabilities);

public static class DomainContractInvariants
{
    private static readonly HashSet<string> RequiredRunOutputCollectionNames = new(StringComparer.Ordinal)
    {
        "observations",
        "deterministic_results",
        "external_claims",
        "application_links",
        "discovery_leads",
        "model_proposals",
        "proposal_admissions",
        "candidates",
        "hypotheses",
        "findings",
        "recommendations",
        "supported_cases",
        "lead_only_cases",
        "abstentions",
        "invalid_inputs",
        "coverage_gaps",
        "failures",
    };

    public static void Validate(AnalyzerDeclarationContract declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (!StringComparer.Ordinal.Equals(
                declaration.SchemaId,
                ContractConstants.AnalyzerDeclarationSchemaId)
            || declaration.SchemaVersion.Major != 1)
        {
            throw new InvalidOperationException("Analyzer declaration uses an unsupported schema contract.");
        }

        RequireText(declaration.AnalyzerId, nameof(declaration.AnalyzerId));
        if (!StringComparer.Ordinal.Equals(declaration.TaxonomyId, ContractConstants.TaxonomyId)
            || declaration.TaxonomyVersion != ContractVersion.Parse(ContractConstants.TaxonomyVersion))
        {
            throw new InvalidOperationException("M1 analyzer declarations must bind the accepted taxonomy version.");
        }

        if (declaration.Maturity != AnalyzerMaturity.Experimental
            || !declaration.RawDevelopmentOutput
            || declaration.PresetOrMaturitySuppression)
        {
            throw new InvalidOperationException(
                "M1 analyzers are Experimental, retain raw output, and cannot suppress by maturity or preset.");
        }
        if (declaration.OperationRequirements.Mode == ExecutionRequirement.Unspecified
            || declaration.Coverage.PossibleStates.Contains(CoverageState.Unspecified))
        {
            throw new InvalidOperationException("Analyzer declarations cannot contain unspecified contract states.");
        }

        RequireNonEmpty(declaration.Scope.SupportedInputs, nameof(declaration.Scope.SupportedInputs));
        RequireNonEmpty(declaration.Scope.ExcludedInputs, nameof(declaration.Scope.ExcludedInputs));
        RequireNonEmpty(
            declaration.Scope.SupportedRecordFieldAssetShapes,
            nameof(declaration.Scope.SupportedRecordFieldAssetShapes));
        RequireNonEmpty(
            declaration.Scope.ExcludedRecordFieldAssetShapes,
            nameof(declaration.Scope.ExcludedRecordFieldAssetShapes));
        RequireNonEmpty(
            declaration.Scope.SupportedTaxonomyCodes,
            nameof(declaration.Scope.SupportedTaxonomyCodes));
        RequireNonEmpty(
            declaration.Scope.UnsupportedTaxonomyCodes,
            nameof(declaration.Scope.UnsupportedTaxonomyCodes));
        RequireNonEmpty(
            declaration.Scope.SupportedExtentFacets,
            nameof(declaration.Scope.SupportedExtentFacets));
        RequireNonEmpty(
            declaration.Scope.ExcludedExtentFacets,
            nameof(declaration.Scope.ExcludedExtentFacets));
        RequireNonEmpty(declaration.InputPopulations, nameof(declaration.InputPopulations));
        RequireNonEmpty(declaration.PossibleOutputTypes, nameof(declaration.PossibleOutputTypes));
        RequireNonEmpty(declaration.Coverage.Populations, nameof(declaration.Coverage.Populations));
        RequireNonEmpty(declaration.Coverage.PossibleStates, nameof(declaration.Coverage.PossibleStates));
        RequireText(declaration.Coverage.UnsupportedBehavior, nameof(declaration.Coverage.UnsupportedBehavior));
        RequireText(
            declaration.ExpectedScaleAndCost.PopulationScale,
            nameof(declaration.ExpectedScaleAndCost.PopulationScale));
        if (declaration.ExpectedScaleAndCost.CostClass == AnalyzerCostClass.Unspecified)
        {
            throw new InvalidOperationException("Analyzer cost class must be explicit.");
        }

        bool operationFlagsValid = declaration.OperationRequirements.Mode switch
        {
            ExecutionRequirement.LocalOnly => !declaration.OperationRequirements.NetworkRequired
                && !declaration.OperationRequirements.LlmRequired
                && !declaration.OperationRequirements.ProviderRequired,
            ExecutionRequirement.CachedExternalEvidence => !declaration.OperationRequirements.NetworkRequired
                && !declaration.OperationRequirements.LlmRequired
                && !declaration.OperationRequirements.ProviderRequired,
            ExecutionRequirement.LiveNetwork => declaration.OperationRequirements.NetworkRequired
                && !declaration.OperationRequirements.LlmRequired
                && !declaration.OperationRequirements.ProviderRequired,
            ExecutionRequirement.LlmProvider => declaration.OperationRequirements.NetworkRequired
                && declaration.OperationRequirements.LlmRequired
                && declaration.OperationRequirements.ProviderRequired,
            _ => false,
        };
        if (!operationFlagsValid)
        {
            throw new InvalidOperationException("Analyzer operation mode and requirements must agree.");
        }
        if (declaration.ResourceBounds.MaximumInputItems < 1
            || declaration.ResourceBounds.MaximumOutputItems < 1
            || declaration.ResourceBounds.MaximumWallTimeMilliseconds < 1)
        {
            throw new InvalidOperationException("Analyzer resource bounds must be finite and positive.");
        }

        string[] evaluationCases =
        [
            .. declaration.LinkedEvaluationCases.Positive,
            .. declaration.LinkedEvaluationCases.Negative,
            .. declaration.LinkedEvaluationCases.Boundary,
            .. declaration.LinkedEvaluationCases.Malformed,
            .. declaration.LinkedEvaluationCases.CrossCategory,
            .. declaration.LinkedEvaluationCases.Gap,
        ];
        RequireNonEmpty(declaration.LinkedEvaluationCases.Positive, "positive evaluation cases");
        RequireNonEmpty(declaration.LinkedEvaluationCases.Negative, "negative evaluation cases");
        RequireNonEmpty(declaration.LinkedEvaluationCases.Boundary, "boundary evaluation cases");
        RequireNonEmpty(declaration.LinkedEvaluationCases.Malformed, "malformed evaluation cases");
        RequireNonEmpty(declaration.LinkedEvaluationCases.CrossCategory, "cross-category evaluation cases");
        RequireNonEmpty(declaration.LinkedEvaluationCases.Gap, "gap evaluation cases");
        RequireNonEmpty(evaluationCases, nameof(declaration.LinkedEvaluationCases));
        RequireUnique(evaluationCases, nameof(declaration.LinkedEvaluationCases));
    }

    public static void Validate(TaxonomyAssignmentContract assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (!StringComparer.Ordinal.Equals(assignment.TaxonomyId, ContractConstants.TaxonomyId)
            || assignment.TaxonomyVersion != ContractVersion.Parse(ContractConstants.TaxonomyVersion))
        {
            throw new InvalidOperationException("The taxonomy contract is not the accepted M1 taxonomy version.");
        }

        bool hasCode = !string.IsNullOrWhiteSpace(assignment.Code);
        RequireText(assignment.Axis, nameof(assignment.Axis));
        RequireText(assignment.Facet, nameof(assignment.Facet));
        if (assignment.Applicability == TaxonomyApplicability.Unspecified
            || assignment.Role == ClassificationRole.Unspecified)
        {
            throw new InvalidOperationException("Taxonomy assignment state and role must be explicit.");
        }
        if ((assignment.Applicability == TaxonomyApplicability.Assigned) != hasCode)
        {
            throw new InvalidOperationException("Only assigned taxonomy classifications have a code.");
        }
    }

    public static void Validate(EffectiveScanConfigurationContract configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!StringComparer.Ordinal.Equals(
                configuration.SchemaId,
                ContractConstants.EffectiveScanConfigurationSchemaId)
            || configuration.SchemaVersion.Major != 1)
        {
            throw new InvalidOperationException("Effective configuration uses an unsupported schema contract.");
        }

        RequireNonEmpty(configuration.Analyzers, nameof(configuration.Analyzers));
        RequireUnique(
            configuration.Analyzers.Select(value => value.AnalyzerId.Value),
            nameof(configuration.Analyzers));
        RequireUnique(
            configuration.Sources.Select(value => value.SourceId.Value),
            nameof(configuration.Sources));
        RequireUnique(
            configuration.Thresholds.Select(value => $"{value.AnalyzerId.Value}/{value.ThresholdId.Value}"),
            nameof(configuration.Thresholds));
        RequireUnique(
            configuration.SemanticContextOverrides.Select(value => value.OverrideId.Value),
            nameof(configuration.SemanticContextOverrides));
        if (configuration.Analyzers.Any(value => value.Origin == SettingOrigin.Unspecified)
            || configuration.Sources.Any(value =>
                value.Origin == SettingOrigin.Unspecified || value.Mode == SourceMode.Unspecified)
            || configuration.Budgets.Origin == SettingOrigin.Unspecified
            || configuration.CachePolicy.Origin == SettingOrigin.Unspecified
            || configuration.Tracing.Origin == SettingOrigin.Unspecified
            || configuration.Tracing.SensitivityLabel
                != DiagnosticSensitivityLabel.SensitiveDevelopmentDiagnostic
            || configuration.CandidateBreadth.Origin == SettingOrigin.Unspecified
            || configuration.Provider.Origin == SettingOrigin.Unspecified
            || configuration.Provider.Mode == ProviderMode.Unspecified
            || configuration.Resources.Origin == SettingOrigin.Unspecified
            || configuration.Thresholds.Any(value => value.Origin == SettingOrigin.Unspecified)
            || configuration.SemanticContextOverrides.Any(
                value => value.Origin != SettingOrigin.SemanticContextOverride))
        {
            throw new InvalidOperationException("Every effective control must retain an explicit valid origin.");
        }

        if (configuration.Budgets.MaximumDispatchCount < 0
            || configuration.Budgets.MaximumInputTokens < 0
            || configuration.Budgets.MaximumOutputTokens < 0
            || configuration.Budgets.MaximumHostedSearchCalls != 0
            || configuration.Budgets.MaximumNanoUsd < 0
            || configuration.CandidateBreadth.MaximumCandidates < 1
            || configuration.CandidateBreadth.AllPairsLlmComparison
            || configuration.Resources.MaximumGeneralWorkers < 1
            || configuration.Resources.MaximumMemoryBytes < 1
            || configuration.Resources.MaximumOutputBytes < 1)
        {
            throw new InvalidOperationException("Effective controls must retain finite M1 bounds.");
        }
        if (configuration.CachePolicy.AnalyticalMode is not ("reuse-valid" or "force-clean-recomputation")
            || configuration.CachePolicy.SourceMode is not ("reuse-resolved-source" or "force-clean-extraction")
            || !StringComparer.Ordinal.Equals(configuration.CachePolicy.ProviderCacheMode, "disabled")
            || configuration.Tracing.Level is not ("off" or "errors" or "operations" or "development")
            || configuration.CandidateBreadth.Mode is not (
                "declared-mandatory-and-causal-lanes" or "expanded-deterministic-lanes"))
        {
            throw new InvalidOperationException("Effective cache and candidate controls use a closed M1 vocabulary.");
        }

        bool providerDisabled = configuration.Provider.Mode == ProviderMode.Disabled;
        bool hasProviderDetails = configuration.Provider.Model is not null
            || configuration.Provider.ReasoningEffort is not null
            || configuration.Provider.Store is not null
            || configuration.Provider.ServiceTier is not null
            || configuration.Provider.Streaming is not null
            || configuration.Provider.MaximumConcurrentLiveDispatches is not null;
        bool hasAllProviderDetails = configuration.Provider.Model is not null
            && configuration.Provider.ReasoningEffort is not null
            && configuration.Provider.Store is not null
            && configuration.Provider.ServiceTier is not null
            && configuration.Provider.Streaming is not null
            && configuration.Provider.MaximumConcurrentLiveDispatches is not null;
        if ((providerDisabled && hasProviderDetails)
            || (!providerDisabled && (!hasAllProviderDetails
                || !StringComparer.Ordinal.Equals(configuration.Provider.Model, "gpt-5.6-sol")
                || !StringComparer.Ordinal.Equals(configuration.Provider.ReasoningEffort, "medium")
                || configuration.Provider.Store != false
                || !StringComparer.Ordinal.Equals(configuration.Provider.ServiceTier, "default")
                || configuration.Provider.Streaming != false
                || configuration.Provider.MaximumConcurrentLiveDispatches != 1)))
        {
            throw new InvalidOperationException(
                "Disabled provider controls omit live details; enabled controls retain every accepted detail.");
        }
    }

    public static void Validate(LifecycleTransitionContract transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        LifecycleState[] terminalStates =
        [
            LifecycleState.Cancelled,
            LifecycleState.Completed,
            LifecycleState.CompletedWithGaps,
            LifecycleState.Failed,
            LifecycleState.LimitReached,
            LifecycleState.InvalidatedByChangedInput,
        ];

        if (terminalStates.Contains(transition.From))
        {
            throw new InvalidOperationException("Terminal lifecycle states cannot transition.");
        }
        if (transition.From == LifecycleState.Unspecified || transition.To == LifecycleState.Unspecified)
        {
            throw new InvalidOperationException("Lifecycle transitions require explicit states.");
        }

        if (transition.NewGeneration != checked(transition.ExpectedGeneration + 1))
        {
            throw new InvalidOperationException("A lifecycle transition must advance its compare-and-swap generation once.");
        }

        if (transition.CoordinatorFencingEpoch < 1)
        {
            throw new InvalidOperationException("A lifecycle transition requires a positive coordinator fencing epoch.");
        }
    }

    public static void Validate(RunOutputContract output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!StringComparer.Ordinal.Equals(output.SchemaId, ContractConstants.RunOutputSchemaId))
        {
            throw new InvalidOperationException("Run output uses an unsupported schema ID.");
        }

        if (output.SchemaVersion.Major != 1)
        {
            throw new InvalidOperationException("Run output major version must be 1.");
        }

        RequireUnique(output.Findings.Select(value => value.OccurrenceId.Value), nameof(output.Findings));
        RequireUnique(output.SupportedCases.Select(value => value.OccurrenceId.Value), nameof(output.SupportedCases));
        RequireUnique(output.LeadOnlyCases.Select(value => value.OccurrenceId.Value), nameof(output.LeadOnlyCases));
        RequireUnique(
            output.SupportedCases.Concat(output.LeadOnlyCases).Select(value => value.OccurrenceId.Value),
            "all case occurrences");
        foreach (CaseOccurrenceContract supportedCase in output.SupportedCases)
        {
            Validate(supportedCase);
            if (supportedCase.Kind != CaseOccurrenceKind.Supported)
            {
                throw new InvalidOperationException("Supported-case output contains a non-supported case.");
            }
        }
        foreach (CaseOccurrenceContract leadOnlyCase in output.LeadOnlyCases)
        {
            Validate(leadOnlyCase);
            if (leadOnlyCase.Kind != CaseOccurrenceKind.LeadOnly)
            {
                throw new InvalidOperationException("Lead-only output contains a non-lead-only case.");
            }
        }

        HashSet<string> findingIds = output.Findings
            .Select(value => value.OccurrenceId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.SupportedCases.Concat(output.LeadOnlyCases)
            .SelectMany(value => value.FindingOccurrenceIds)
            .Any(value => !findingIds.Contains(value.Value)))
        {
            throw new InvalidOperationException("Case output references a finding outside the run output.");
        }

        HashSet<string> externalClaimIds = output.ExternalClaims
            .Select(value => value.ClaimId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.DiscoveryLeads.Any(value => value.AdmittedExternalClaimId is not null
                && !externalClaimIds.Contains(value.AdmittedExternalClaimId.Value))
            || output.ExternalClaimApplicationLinks.Any(value =>
                !externalClaimIds.Contains(value.ExternalClaimId.Value)
                || value.ConsumingAnalysisRunId != output.RunId))
        {
            throw new InvalidOperationException(
                "Discovery-lead admission and external-claim application links must reference retained typed claims and the consuming run.");
        }

        if (output.Observations.Any(value =>
                value.Provenance.LlmInvolvement.State != LlmInvolvementState.None)
            || output.DeterministicResults.Any(value =>
                value.Provenance.LlmInvolvement.State != LlmInvolvementState.None))
        {
            throw new InvalidOperationException(
                "LLM output cannot become a local observation or deterministic result.");
        }

        IEnumerable<ArtifactProvenanceContract> provenances = output.Observations.Select(value => value.Provenance)
            .Concat(output.DeterministicResults.Select(value => value.Provenance))
            .Concat(output.ExternalClaims.Select(value => value.Provenance))
            .Concat(output.ExternalClaimApplicationLinks.Select(value => value.Provenance))
            .Concat(output.DiscoveryLeads.Select(value => value.Provenance))
            .Concat(output.ModelProposals.Select(value => value.Provenance))
            .Concat(output.Candidates.Select(value => value.Provenance))
            .Concat(output.Hypotheses.Select(value => value.Provenance))
            .Concat(output.Findings.Select(value => value.Conclusion.Provenance))
            .Concat(output.Recommendations.Select(value => value.Provenance))
            .Concat(output.Abstentions.Select(value => value.Provenance))
            .Concat(output.InvalidInputs.Select(value => value.Provenance))
            .Concat(output.CoverageGaps.Select(value => value.Provenance))
            .Concat(output.Failures.Select(value => value.Provenance));
        foreach (ArtifactProvenanceContract provenance in provenances)
        {
            Validate(provenance.LlmInvolvement);
        }

        HashSet<string> proposalIds = output.ModelProposals
            .Select(value => value.ProposalId.Value)
            .ToHashSet(StringComparer.Ordinal);
        RequireUnique(output.ModelProposals.Select(value => value.ProposalId.Value), nameof(output.ModelProposals));
        RequireUnique(output.ProposalAdmissions.Select(value => value.AdmissionId.Value), nameof(output.ProposalAdmissions));
        RequireUnique(output.ProposalAdmissions.Select(value => value.ProposalId.Value), "admitted proposal IDs");
        RequireUnique(
            output.ProposalAdmissions.Select(value => value.AdmittedArtifactId.Value),
            "proposal-admitted artifact IDs");
        if (output.ModelProposals.Any(value =>
            value.ValidationState == ProposalValidationState.Unspecified
                || value.Operation is LlmOperation.Unspecified or LlmOperation.None))
        {
            throw new InvalidOperationException(
                "Model proposals require an explicit operation and validation state.");
        }

        Dictionary<string, ModelProposalContract> proposalsById = output.ModelProposals
            .ToDictionary(value => value.ProposalId.Value, StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> admissibleArtifacts = new(StringComparer.Ordinal)
        {
            ["external-claim"] = output.ExternalClaims
                .Select(value => value.ClaimId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["candidate"] = output.Candidates
                .Select(value => value.CandidateId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["hypothesis"] = output.Hypotheses
                .Select(value => value.HypothesisId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["finding"] = output.Findings
                .Select(value => value.OccurrenceId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["recommendation"] = output.Recommendations
                .Select(value => value.RecommendationId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["abstention"] = output.Abstentions
                .Select(value => value.AbstentionId.Value)
                .ToHashSet(StringComparer.Ordinal),
        };
        Dictionary<string, IReadOnlyDictionary<string, ArtifactProvenanceContract>> admissibleProvenance =
            new(StringComparer.Ordinal)
            {
                ["external-claim"] = output.ExternalClaims.ToDictionary(
                    value => value.ClaimId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["candidate"] = output.Candidates.ToDictionary(
                    value => value.CandidateId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["hypothesis"] = output.Hypotheses.ToDictionary(
                    value => value.HypothesisId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["finding"] = output.Findings.ToDictionary(
                    value => value.OccurrenceId.Value,
                    value => value.Conclusion.Provenance,
                    StringComparer.Ordinal),
                ["recommendation"] = output.Recommendations.ToDictionary(
                    value => value.RecommendationId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["abstention"] = output.Abstentions.ToDictionary(
                    value => value.AbstentionId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
            };
        if (output.ModelProposals.Any(value =>
                !admissibleArtifacts.ContainsKey(value.ProposedArtifactType)
                || value.Provenance.OriginatingRunId != output.RunId
                || value.Provenance.LlmInvolvement.Operation != value.Operation
                || value.Provenance.LlmInvolvement.State
                    != (value.ValidationState == ProposalValidationState.Rejected
                        ? LlmInvolvementState.ProposalRejected
                        : LlmInvolvementState.ProposalRetained)))
        {
            throw new InvalidOperationException(
                "Model proposal provenance must retain its run, operation, validation disposition, "
                + "and an allowed proposed artifact type.");
        }

        if (output.ProposalAdmissions.Any(value =>
                !proposalIds.Contains(value.ProposalId.Value)
                || proposalsById[value.ProposalId.Value].ValidationState != ProposalValidationState.Validated
                || !StringComparer.Ordinal.Equals(
                    proposalsById[value.ProposalId.Value].ProposedArtifactType,
                    value.AdmittedArtifactType)
                || value.OriginatingRunId != output.RunId
                || !admissibleArtifacts.TryGetValue(value.AdmittedArtifactType, out HashSet<string>? artifacts)
                || !artifacts.Contains(value.AdmittedArtifactId.Value)
                || !admissibleProvenance.TryGetValue(
                    value.AdmittedArtifactType,
                    out IReadOnlyDictionary<string, ArtifactProvenanceContract>? provenanceById)
                || !provenanceById.TryGetValue(
                    value.AdmittedArtifactId.Value,
                    out ArtifactProvenanceContract? admittedProvenance)
                || admittedProvenance.LlmInvolvement.State != LlmInvolvementState.ProposalAdmitted
                || admittedProvenance.LlmInvolvement.Operation
                    != proposalsById[value.ProposalId.Value].Operation
                || admittedProvenance.LlmInvolvement.InvocationId
                    != proposalsById[value.ProposalId.Value].Provenance.LlmInvolvement.InvocationId))
        {
            throw new InvalidOperationException(
                "Every admitted model proposal must be validated and point to an existing allowed typed artifact "
                + "whose type, invocation, and proposal-admitted operation match the retained proposal.");
        }

        RequireUnique(output.CollectionStates.Select(value => value.CollectionName), nameof(output.CollectionStates));
        if (!RequiredRunOutputCollectionNames.SetEquals(
                output.CollectionStates.Select(value => value.CollectionName)))
        {
            throw new InvalidOperationException("Run output must state production status for every typed collection.");
        }
        if (output.CollectionStates.Any(value => value.State == CollectionProductionState.Unspecified))
        {
            throw new InvalidOperationException("Run output collection states must be explicit.");
        }
    }

    public static void Validate(LlmInvolvementContract involvement)
    {
        ArgumentNullException.ThrowIfNull(involvement);
        if (involvement.State == LlmInvolvementState.Unspecified
            || involvement.Operation == LlmOperation.Unspecified)
        {
            throw new InvalidOperationException("LLM involvement state and operation must be explicit.");
        }

        bool isNone = involvement.State == LlmInvolvementState.None;
        if ((isNone
                && (involvement.Operation != LlmOperation.None || involvement.InvocationId is not null))
            || (!isNone
                && (involvement.Operation == LlmOperation.None || involvement.InvocationId is null)))
        {
            throw new InvalidOperationException(
                "LLM absence has no invocation; rejected or admitted proposals retain operation and invocation.");
        }
    }

    public static void Validate(DiagnosticTraceContract trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        if (!StringComparer.Ordinal.Equals(trace.SchemaId, ContractConstants.DiagnosticTraceSchemaId)
            || trace.SchemaVersion.Major != 1
            || trace.SensitivityLabel != DiagnosticSensitivityLabel.SensitiveDevelopmentDiagnostic
            || trace.SharingClass != DiagnosticSharingClass.PrivateDiagnostic
            || trace.CredentialMaterialPresent
            || trace.RedactionState == DiagnosticRedactionState.Unspecified)
        {
            throw new InvalidOperationException(
                "Diagnostic traces must remain sensitive PrivateDiagnostic artifacts verified free of credentials.");
        }

        if (trace.Events.Any(value =>
                value.Sequence < 0
                || value.Severity == DiagnosticSeverity.Unspecified
                || value.Fields.Any(field =>
                    field.DataClass == DiagnosticDataClass.Unspecified
                    || field.Redaction == DiagnosticFieldRedaction.Unspecified))
            || trace.Events.Select(value => value.Sequence).Distinct().Count() != trace.Events.Count)
        {
            throw new InvalidOperationException("Diagnostic trace events require explicit unique sequences and labels.");
        }
    }

    public static void Validate(CaseOccurrenceContract occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.Kind == CaseOccurrenceKind.Unspecified)
        {
            throw new InvalidOperationException("Case occurrence kind must be explicit.");
        }

        if (occurrence.RevisionNumber < 1)
        {
            throw new InvalidOperationException("Case occurrence revision must be positive.");
        }

        if (occurrence.Kind == CaseOccurrenceKind.Supported
            && occurrence.FindingOccurrenceIds.Count == 0)
        {
            throw new InvalidOperationException("A supported case requires at least one finding.");
        }

        if (occurrence.Kind == CaseOccurrenceKind.LeadOnly
            && occurrence.FindingOccurrenceIds.Count != 0)
        {
            throw new InvalidOperationException(
                "A lead-only case cannot contain a finding or affect readiness.");
        }
    }

    public static void Validate(CliSummaryContract summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!StringComparer.Ordinal.Equals(summary.SchemaId, ContractConstants.CliSummarySchemaId)
            || summary.SchemaVersion.Major != 1)
        {
            throw new InvalidOperationException("CLI summary uses an unsupported schema contract.");
        }

        CliExitCode expected = summary.Outcome switch
        {
            CliOutcome.Completed => CliExitCode.Success,
            CliOutcome.CompletedWithGaps => CliExitCode.Success,
            CliOutcome.InvalidInput => CliExitCode.InvalidInput,
            CliOutcome.Unsupported => CliExitCode.Unsupported,
            CliOutcome.Failed => CliExitCode.Failed,
            CliOutcome.Cancelled => CliExitCode.Cancelled,
            CliOutcome.LimitReached => CliExitCode.LimitReached,
            _ => throw new InvalidOperationException("CLI outcome is unknown."),
        };
        if (summary.ExitCode != expected || !summary.NoSafetyGuarantee)
        {
            throw new InvalidOperationException("CLI outcome, exit code, and safety qualification must agree.");
        }

        if (summary.TypedCounts.Observations < 0
            || summary.TypedCounts.DeterministicResults < 0
            || summary.TypedCounts.ExternalClaims < 0
            || summary.TypedCounts.ApplicationLinks < 0
            || summary.TypedCounts.DiscoveryLeads < 0
            || summary.TypedCounts.ModelProposals < 0
            || summary.TypedCounts.ProposalAdmissions < 0
            || summary.TypedCounts.Candidates < 0
            || summary.TypedCounts.Hypotheses < 0
            || summary.TypedCounts.Findings < 0
            || summary.TypedCounts.Recommendations < 0
            || summary.TypedCounts.SupportedCases < 0
            || summary.TypedCounts.LeadOnlyCases < 0
            || summary.TypedCounts.Abstentions < 0
            || summary.TypedCounts.InvalidInputs < 0
            || summary.TypedCounts.CoverageGaps < 0
            || summary.TypedCounts.Failures < 0
            || summary.CoverageStateCounts.Completed < 0
            || summary.CoverageStateCounts.CompletedWithGaps < 0
            || summary.CoverageStateCounts.Failed < 0
            || summary.CoverageStateCounts.SkippedByConfiguration < 0
            || summary.CoverageStateCounts.SkippedByLimit < 0
            || summary.CoverageStateCounts.Unsupported < 0)
        {
            throw new InvalidOperationException("CLI summary counts cannot be negative.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }
    }

    private static void RequireNonEmpty<T>(IReadOnlyCollection<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new InvalidOperationException($"{name} must contain at least one value.");
        }
    }

    private static void RequireUnique(IEnumerable<string> values, string name)
    {
        string[] materialized = values.ToArray();
        if (materialized.Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new InvalidOperationException($"{name} must not contain duplicate values.");
        }
    }
}
