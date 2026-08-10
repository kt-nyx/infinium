namespace Infinium.Domain.Contracts;

public static partial class DomainContractInvariants
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
        RequireText(declaration.AnalyzerFamily, nameof(declaration.AnalyzerFamily));
        if (!StringComparer.Ordinal.Equals(declaration.TaxonomyId, ContractConstants.TaxonomyId)
            || declaration.TaxonomyVersion != ContractVersion.Parse(ContractConstants.TaxonomyVersion))
        {
            throw new InvalidOperationException("Analyzer declarations must bind the accepted taxonomy version.");
        }

        if (declaration.Maturity != AnalyzerMaturity.Experimental
            || !declaration.RawDevelopmentOutput
            || declaration.PresetOrMaturitySuppression)
        {
            throw new InvalidOperationException(
                "Bounded analyzers are Experimental, retain raw output, and cannot suppress by maturity or preset.");
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
        RequireNonEmpty(declaration.PayloadContracts, nameof(declaration.PayloadContracts));
        string[] requiredAnalysisSchemas =
        [
            ContractConstants.DocumentationEvidenceSchemaId,
            ContractConstants.CandidateAnalysisSchemaId,
            ContractConstants.FindingCaseSchemaId,
            ContractConstants.AnalysisReplaySchemaId,
            ContractConstants.AnalysisExecutionInputSchemaId,
        ];
        ExecutionBoundaryContractInvariants.ValidateProductCapabilities(
            declaration.NotUsedBoundaries,
            requireNotUsed: true);
        if (declaration.StateModelVersion.Major != 1
            || !requiredAnalysisSchemas.All(schemaId => declaration.PayloadContracts.Any(
                item => item.Required && StringComparer.Ordinal.Equals(item.SchemaId, schemaId))))
        {
            throw new InvalidOperationException("analysis pipeline analyzers must bind every required v1 payload, state model v1, and explicit not-used boundary.");
        }
    }

    public static void Validate(TaxonomyAssignmentContract assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (!StringComparer.Ordinal.Equals(assignment.TaxonomyId, ContractConstants.TaxonomyId)
            || assignment.TaxonomyVersion != ContractVersion.Parse(ContractConstants.TaxonomyVersion))
        {
            throw new InvalidOperationException("The taxonomy contract is not the accepted taxonomy version.");
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

}
