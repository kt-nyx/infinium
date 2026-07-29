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

public sealed record CliCostContract(
    long ProviderInputTokens,
    long ProviderOutputTokens,
    long ProviderReasoningTokens,
    long DispatchCount,
    long ToolCallCount,
    long? CalculatedActualNanoUsd,
    long ReservedNanoUsd,
    bool UnresolvedHold);

public sealed record CliSummaryAggregateContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId RunId,
    CliOutcome Outcome,
    CliExitCode ExitCode,
    TypedOutputCountsContract TypedCounts,
    CoverageStateCountsContract CoverageStateCounts,
    long DurationMs,
    CliCostContract Cost,
    ReadinessScope ReadinessScope,
    bool NoSafetyGuarantee);

public sealed record RunOutputAggregateContract(
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
        OperationalContractInvariants.Validate(transition);
    }

    public static void Validate(RunOutputAggregateContract output)
    {
        ArgumentNullException.ThrowIfNull(output);
        RequireNonEmpty(output.AnalyzerDeclarations, nameof(output.AnalyzerDeclarations));
        RequireUnique(
            output.AnalyzerDeclarations.Select(value => value.AnalyzerId),
            nameof(output.AnalyzerDeclarations));
        foreach (AnalyzerDeclarationContract declaration in output.AnalyzerDeclarations)
        {
            Validate(declaration);
        }
        RequireUnique(
            output.TaxonomyAssignments.Select(value => value.AssignmentId.Value),
            nameof(output.TaxonomyAssignments));
        foreach (TaxonomyAssignmentContract assignment in output.TaxonomyAssignments)
        {
            Validate(assignment);
        }
        RequireUnique(output.Coverage.Select(value => value.CoverageId.Value), nameof(output.Coverage));
        RequireUnique(
            output.Coverage.Select(value => $"{value.AnalyzerId.Value}/{value.PopulationId}"),
            "analyzer coverage populations");

        RequireUnique(output.Findings.Select(value => value.OccurrenceId.Value), nameof(output.Findings));
        RequireUnique(output.SupportedCases.Select(value => value.OccurrenceId.Value), nameof(output.SupportedCases));
        RequireUnique(output.LeadOnlyCases.Select(value => value.OccurrenceId.Value), nameof(output.LeadOnlyCases));
        RequireUnique(
            output.SupportedCases.Concat(output.LeadOnlyCases).Select(value => value.OccurrenceId.Value),
            "all case occurrences");
        foreach (CaseOccurrenceContract supportedCase in output.SupportedCases)
        {
            Validate(supportedCase);
            if (supportedCase.Kind != CaseOccurrenceKind.Supported
                || supportedCase.OriginatingRunId != output.RunId)
            {
                throw new InvalidOperationException(
                    "Supported-case output must be supported and owned by the current run.");
            }
        }
        foreach (CaseOccurrenceContract leadOnlyCase in output.LeadOnlyCases)
        {
            Validate(leadOnlyCase);
            if (leadOnlyCase.Kind != CaseOccurrenceKind.LeadOnly
                || leadOnlyCase.OriginatingRunId != output.RunId)
            {
                throw new InvalidOperationException(
                    "Lead-only output must be lead-only and owned by the current run.");
            }
        }
        if (output.Findings.Any(value => value.OriginatingRunId != output.RunId))
        {
            throw new InvalidOperationException(
                "Finding occurrences must retain the current producing run.");
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
        HashSet<string> hypothesisIds = output.Hypotheses
            .Select(value => value.HypothesisId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.SupportedCases.Concat(output.LeadOnlyCases)
            .SelectMany(value => value.HypothesisIds)
            .Any(value => !hypothesisIds.Contains(value.Value)))
        {
            throw new InvalidOperationException("Case output references a hypothesis outside the run output.");
        }

        HashSet<string> externalClaimIds = output.ExternalClaims
            .Select(value => value.ClaimId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.DiscoveryLeads.Any(value => value.AdmittedExternalClaimId is not null
                && !externalClaimIds.Contains(value.AdmittedExternalClaimId.Value))
            || output.ExternalClaimApplicationLinks.Any(value =>
                !externalClaimIds.Contains(value.ExternalClaimId.Value)
                || value.ConsumingAnalysisRunId != output.RunId
                || value.SemanticAnalysisContextId != output.AnalysisContextId)
            || output.ExternalClaims.Any(value =>
                value.AcquisitionRunId != value.Provenance.OriginatingRunId))
        {
            throw new InvalidOperationException(
                "External claims, discovery admissions, and application links must retain their producing acquisition run and consuming run context.");
        }
        foreach (ExternalClaimContract claim in output.ExternalClaims)
        {
            HashSet<string> declaredLinks = claim.ApplicationLinkIds
                .Select(value => value.Value)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> retainedLinks = output.ExternalClaimApplicationLinks
                .Where(value => value.ExternalClaimId == claim.ClaimId)
                .Select(value => value.ApplicationLinkId.Value)
                .ToHashSet(StringComparer.Ordinal);
            if (!declaredLinks.SetEquals(retainedLinks))
            {
                throw new InvalidOperationException(
                    "External claims and consuming-run application links must be bidirectionally complete.");
            }
        }
        if (output.ExternalClaims.Any(value => value.Authority is
                EvidenceAuthority.Unspecified
                or EvidenceAuthority.SnapshotBoundLocal
                or EvidenceAuthority.DeterministicDerived
                or EvidenceAuthority.HeuristicOrLlmInference)
            || output.Observations.Any(value => value.Authority != EvidenceAuthority.SnapshotBoundLocal)
            || output.DeterministicResults.Any(value =>
                value.Authority != EvidenceAuthority.DeterministicDerived))
        {
            throw new InvalidOperationException(
                "Observation, deterministic-result, and external-claim authority must remain claim-type-specific.");
        }

        string[] allArtifactIds =
        [
            .. output.Observations.Select(value => value.ObservationId.Value),
            .. output.DeterministicResults.Select(value => value.ResultId.Value),
            .. output.ExternalClaims.Select(value => value.ClaimId.Value),
            .. output.ExternalClaimApplicationLinks.Select(value => value.ApplicationLinkId.Value),
            .. output.DiscoveryLeads.Select(value => value.LeadId.Value),
            .. output.ModelProposals.Select(value => value.ProposalId.Value),
            .. output.ProposalAdmissions.Select(value => value.AdmissionId.Value),
            .. output.Candidates.Select(value => value.CandidateId.Value),
            .. output.Hypotheses.Select(value => value.HypothesisId.Value),
            .. output.Findings.Select(value => value.OccurrenceId.Value),
            .. output.Recommendations.Select(value => value.RecommendationId.Value),
            .. output.SupportedCases.Select(value => value.OccurrenceId.Value),
            .. output.LeadOnlyCases.Select(value => value.OccurrenceId.Value),
            .. output.Abstentions.Select(value => value.AbstentionId.Value),
            .. output.InvalidInputs.Select(value => value.InvalidInputId.Value),
            .. output.CoverageGaps.Select(value => value.GapId.Value),
            .. output.Failures.Select(value => value.FailureId.Value),
        ];
        RequireUnique(allArtifactIds, "all run-output artifact IDs");

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
            if (provenance.SupersedesRevisionId == provenance.RevisionId)
            {
                throw new InvalidOperationException("Artifact provenance cannot supersede itself.");
            }
            RequireUnique(
                provenance.SupportingEvidenceIds.Select(value => value.Value),
                "supporting evidence IDs");
            RequireUnique(
                provenance.ContradictingEvidenceIds.Select(value => value.Value),
                "contradicting evidence IDs");
            if (provenance.SupportingEvidenceIds
                .Select(value => value.Value)
                .Intersect(
                    provenance.ContradictingEvidenceIds.Select(value => value.Value),
                    StringComparer.Ordinal)
                .Any())
            {
                throw new InvalidOperationException(
                    "The same evidence cannot simultaneously support and contradict one artifact.");
            }
        }
        RequireUnique(provenances.Select(value => value.RevisionId.Value), "artifact provenance revision IDs");

        IEnumerable<ArtifactProvenanceContract> directlyProducedProvenances =
            output.Observations.Select(value => value.Provenance)
                .Concat(output.DeterministicResults.Select(value => value.Provenance))
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
        if (directlyProducedProvenances.Any(value => value.OriginatingRunId != output.RunId))
        {
            throw new InvalidOperationException(
                "Directly produced run output must retain the current run as its provenance owner.");
        }
        HashSet<string> appliedForeignClaimIds = output.ExternalClaimApplicationLinks
            .Select(value => value.ExternalClaimId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.ExternalClaims.Any(value =>
                value.Provenance.OriginatingRunId != output.RunId
                && !appliedForeignClaimIds.Contains(value.ClaimId.Value)))
        {
            throw new InvalidOperationException(
                "A reusable foreign-run external claim requires an explicit application link in the consuming run.");
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

        HashSet<string> admittedArtifactIds = admissibleProvenance
            .SelectMany(value => value.Value)
            .Where(value => value.Value.LlmInvolvement.State == LlmInvolvementState.ProposalAdmitted)
            .Select(value => value.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (!admittedArtifactIds.SetEquals(
                output.ProposalAdmissions.Select(value => value.AdmittedArtifactId.Value)))
        {
            throw new InvalidOperationException(
                "Proposal admissions and proposal-admitted artifacts must form a complete bidirectional record.");
        }

        HashSet<string> taxonomyAssignmentIds = output.TaxonomyAssignments
            .Select(value => value.AssignmentId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.Findings.Any(value =>
                value.TaxonomyAssignmentIds
                    .Concat(value.Conclusion.TaxonomyAssignmentIds)
                    .Any(id => !taxonomyAssignmentIds.Contains(id.Value))))
        {
            throw new InvalidOperationException(
                "Finding taxonomy references must resolve to retained assignments.");
        }
        HashSet<string> recommendationIds = output.Recommendations
            .Select(value => value.RecommendationId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.Findings.Any(value =>
                value.Conclusion.RecommendationId is not null
                && !recommendationIds.Contains(value.Conclusion.RecommendationId.Value)))
        {
            throw new InvalidOperationException(
                "Finding recommendation references must resolve within the run output.");
        }

        HashSet<string> analyzerIds = output.AnalyzerDeclarations
            .Select(value => value.AnalyzerId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> coverageGapIds = output.CoverageGaps
            .Select(value => value.GapId.Value)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> failureIds = output.Failures
            .Select(value => value.FailureId.Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CoverageContract coverage in output.Coverage)
        {
            Validate(coverage);
            if (!analyzerIds.Contains(coverage.AnalyzerId.Value)
                || coverage.OriginatingRunId != output.RunId
                || coverage.TaxonomyAssignmentIds.Any(
                    value => !taxonomyAssignmentIds.Contains(value.Value))
                || coverage.GapIds.Any(value => !coverageGapIds.Contains(value.Value))
                || coverage.FailureIds.Any(value => !failureIds.Contains(value.Value)))
            {
                throw new InvalidOperationException(
                    "Coverage must be run-bound and resolve its analyzer, taxonomy, gap, and failure references.");
            }
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
        Dictionary<string, int> collectionCounts = new(StringComparer.Ordinal)
        {
            ["observations"] = output.Observations.Count,
            ["deterministic_results"] = output.DeterministicResults.Count,
            ["external_claims"] = output.ExternalClaims.Count,
            ["application_links"] = output.ExternalClaimApplicationLinks.Count,
            ["discovery_leads"] = output.DiscoveryLeads.Count,
            ["model_proposals"] = output.ModelProposals.Count,
            ["proposal_admissions"] = output.ProposalAdmissions.Count,
            ["candidates"] = output.Candidates.Count,
            ["hypotheses"] = output.Hypotheses.Count,
            ["findings"] = output.Findings.Count,
            ["recommendations"] = output.Recommendations.Count,
            ["supported_cases"] = output.SupportedCases.Count,
            ["lead_only_cases"] = output.LeadOnlyCases.Count,
            ["abstentions"] = output.Abstentions.Count,
            ["invalid_inputs"] = output.InvalidInputs.Count,
            ["coverage_gaps"] = output.CoverageGaps.Count,
            ["failures"] = output.Failures.Count,
        };
        if (output.CollectionStates.Any(value =>
                string.IsNullOrWhiteSpace(value.Reason)
                || (collectionCounts[value.CollectionName] > 0
                    && value.State != CollectionProductionState.Populated)
                || (collectionCounts[value.CollectionName] == 0
                    && value.State == CollectionProductionState.Populated)))
        {
            throw new InvalidOperationException(
                "Typed collection state and reason must agree with the retained collection contents.");
        }
        if (output.Readiness.RunId != output.RunId
            || output.Readiness.Scope == ReadinessScope.Unspecified
            || output.Replayability.ReplayClass == ReplayClass.Unspecified
            || output.Auditability.State == AuditabilityState.Unspecified)
        {
            throw new InvalidOperationException(
                "Readiness, replayability, and auditability state must be explicit and run-bound.");
        }
        bool readinessAbsent = output.Readiness.Scope == ReadinessScope.None;
        if ((readinessAbsent
                && (output.Readiness.ReadinessPolicyId is not null
                    || output.Readiness.DispositionIds.Count != 0))
            || (!readinessAbsent && output.Readiness.ReadinessPolicyId is null))
        {
            throw new InvalidOperationException(
                "Readiness absence cannot carry dispositions; evaluated readiness requires an explicit policy.");
        }
        RequireUnique(
            output.Readiness.DispositionIds.Select(value => value.Value),
            "readiness disposition IDs");
        RequireUnique(
            output.Replayability.DependencyIds.Select(value => value.Value),
            "replay dependency IDs");
        RequireUnique(output.Replayability.MissingDependencies, "missing replay dependencies");
        RequireUnique(output.UnsupportedCapabilities, nameof(output.UnsupportedCapabilities));
        if (output.Replayability.MissingDependencies.Any(string.IsNullOrWhiteSpace)
            || output.UnsupportedCapabilities.Any(string.IsNullOrWhiteSpace)
            || output.Auditability.Gaps.Any(string.IsNullOrWhiteSpace)
            || (output.Replayability.ReplayClass == ReplayClass.CompleteClean
                && output.Replayability.MissingDependencies.Count != 0)
            || (output.Auditability.State == AuditabilityState.Complete
                && output.Auditability.Gaps.Count != 0)
            || (output.Auditability.State != AuditabilityState.Complete
                && output.Auditability.Gaps.Count == 0))
        {
            throw new InvalidOperationException(
                "Replay, audit, and unsupported-capability declarations must retain coherent explicit gaps.");
        }
    }

    public static void Validate(CoverageContract coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (coverage.Denominator < 0
            || coverage.CompletedCount < 0
            || coverage.CompletedCount > coverage.Denominator
            || coverage.State == CoverageState.Unspecified
            || !StringComparer.Ordinal.Equals(coverage.TaxonomyId, ContractConstants.TaxonomyId)
            || coverage.TaxonomyVersion != ContractVersion.Parse(ContractConstants.TaxonomyVersion)
            || string.IsNullOrWhiteSpace(coverage.PopulationId)
            || string.IsNullOrWhiteSpace(coverage.DenominatorLabel))
        {
            throw new InvalidOperationException(
                "Coverage requires bounded counts, explicit state and the accepted taxonomy contract.");
        }
        RequireUnique(
            coverage.TaxonomyAssignmentIds.Select(value => value.Value),
            "coverage taxonomy assignment IDs");
        RequireUnique(coverage.GapIds.Select(value => value.Value), "coverage gap IDs");
        RequireUnique(coverage.FailureIds.Select(value => value.Value), "coverage failure IDs");
        if (coverage.Exclusions.Any(string.IsNullOrWhiteSpace)
            || (coverage.State == CoverageState.Completed
                && (coverage.CompletedCount != coverage.Denominator
                    || coverage.GapIds.Count != 0
                    || coverage.FailureIds.Count != 0))
            || (coverage.State == CoverageState.CompletedWithGaps
                && coverage.GapIds.Count == 0
                && coverage.FailureIds.Count == 0
                && coverage.Exclusions.Count == 0)
            || (coverage.State == CoverageState.Failed && coverage.FailureIds.Count == 0)
            || (coverage.State is CoverageState.SkippedByConfiguration
                    or CoverageState.SkippedByLimit
                    or CoverageState.Unsupported
                && coverage.CompletedCount != 0))
        {
            throw new InvalidOperationException(
                "Coverage status must agree with completed work and explicit gaps, failures, or exclusions.");
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

    public static void Validate(CliSummaryAggregateContract summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(summary.TypedCounts);
        ArgumentNullException.ThrowIfNull(summary.CoverageStateCounts);
        ArgumentNullException.ThrowIfNull(summary.Cost);
        if (!StringComparer.Ordinal.Equals(summary.SchemaId, ContractConstants.CliSummarySchemaId)
            || summary.SchemaVersion.Major != 1
            || summary.ReadinessScope == ReadinessScope.Unspecified)
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
            || summary.DurationMs < 0
            || summary.Cost.ProviderInputTokens < 0
            || summary.Cost.ProviderOutputTokens < 0
            || summary.Cost.ProviderReasoningTokens < 0
            || summary.Cost.DispatchCount < 0
            || summary.Cost.ToolCallCount < 0
            || summary.Cost.CalculatedActualNanoUsd < 0
            || summary.Cost.ReservedNanoUsd < 0
            || (summary.Cost.CalculatedActualNanoUsd is null
                && !summary.Cost.UnresolvedHold)
            || (summary.Cost.CalculatedActualNanoUsd is not null
                && summary.Cost.UnresolvedHold)
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
