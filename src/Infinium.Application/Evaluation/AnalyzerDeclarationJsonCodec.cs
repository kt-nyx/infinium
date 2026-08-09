using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Evaluation;

public static class AnalyzerDeclarationJsonCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Serialize(AnalyzerDeclarationContract declaration)
    {
        DomainContractInvariants.Validate(declaration);
        AnalyzerDeclarationDto dto = ToDto(declaration);
        string json = JsonSerializer.Serialize(dto, Options);
        using JsonDocument document = JsonDocument.Parse(json);
        ActiveJsonSchemaValidator.Validate(document.RootElement, "analyzer-declaration.v1.schema.json");
        return json;
    }

    public static AnalyzerDeclarationContract Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        ActiveJsonSchemaValidator.Validate(document.RootElement, "analyzer-declaration.v1.schema.json");
        AnalyzerDeclarationDto dto = JsonSerializer.Deserialize<AnalyzerDeclarationDto>(json, Options)
            ?? throw new InvalidDataException("Analyzer declaration is empty.");
        AnalyzerDeclarationContract declaration = FromDto(dto);
        DomainContractInvariants.Validate(declaration);
        return declaration;
    }

    private static AnalyzerDeclarationDto ToDto(AnalyzerDeclarationContract value)
    {
        return new AnalyzerDeclarationDto(
            value.SchemaId,
            "1",
            value.AnalyzerId,
            value.AnalyzerFamily,
            value.AnalyzerVersion.ToString(),
            value.SemanticContractVersion.ToString(),
            value.IdentityContractVersion.ToString(),
            value.RulesetVersion.ToString(),
            value.TaxonomyId,
            value.TaxonomyVersion.ToString(),
            new AnalyzerScopeDto(
                value.Scope.SupportedInputs,
                ToReasoned(value.Scope.ExcludedInputs),
                value.Scope.SupportedRecordFieldAssetShapes,
                ToReasoned(value.Scope.ExcludedRecordFieldAssetShapes),
                value.Scope.SupportedTaxonomyCodes,
                ToReasoned(value.Scope.UnsupportedTaxonomyCodes),
                value.Scope.SupportedExtentFacets,
                ToReasoned(value.Scope.ExcludedExtentFacets)),
            value.InputPopulations.Select(
                item => new InputPopulationDto(item.PopulationId, item.Description, item.Required)).ToArray(),
            value.Dependencies.Select(
                item => new DependencyDto(
                    item.DependencyId,
                    item.MinimumVersion.ToString(),
                    item.Required,
                    CoverageStateToWire(item.MissingState))).ToArray(),
            SnapshotAssuranceToWire(value.MinimumSnapshotAssurance),
            new ThresholdsDto(
                ToThreshold(value.Thresholds.CandidateAdmission),
                ToThreshold(value.Thresholds.Evidence),
                ToThreshold(value.Thresholds.Abstention),
                ToThreshold(value.Thresholds.FindingPromotion)),
            value.PossibleOutputTypes,
            new CoverageDto(
                value.Coverage.Populations,
                value.Coverage.PossibleStates.Select(CoverageStateToWire).ToArray(),
                value.Coverage.UnsupportedBehavior),
            new OperationRequirementsDto(
                ExecutionRequirementToWire(value.OperationRequirements.Mode),
                value.OperationRequirements.NetworkRequired,
                value.OperationRequirements.LlmRequired,
                value.OperationRequirements.ProviderRequired),
            new ScaleAndCostDto(
                value.ExpectedScaleAndCost.PopulationScale,
                CostClassToWire(value.ExpectedScaleAndCost.CostClass),
                value.ExpectedScaleAndCost.Billable),
            new ResourceBoundsDto(
                value.ResourceBounds.MaximumInputItems,
                value.ResourceBounds.MaximumOutputItems,
                value.ResourceBounds.MaximumWallTimeMilliseconds),
            "Experimental",
            value.RawDevelopmentOutput,
            value.PresetOrMaturitySuppression,
            new LinkedCasesDto(
                value.LinkedEvaluationCases.Positive,
                value.LinkedEvaluationCases.Negative,
                value.LinkedEvaluationCases.Boundary,
                value.LinkedEvaluationCases.Malformed,
                value.LinkedEvaluationCases.CrossCategory,
                value.LinkedEvaluationCases.Gap),
            value.PayloadContracts.Select(item => new PayloadContractDto(
                item.SchemaId,
                item.SchemaVersion.ToString(),
                item.Required)).ToArray(),
            value.StateModelVersion.ToString(),
            value.NotUsedBoundaries.Select(item => new BoundaryDto(
                item.BoundaryId,
                "not-used",
                item.Reason)).ToArray());
    }

    private static AnalyzerDeclarationContract FromDto(AnalyzerDeclarationDto value)
    {
        return new AnalyzerDeclarationContract(
            value.SchemaId,
            new ContractVersion(1, 0, 0),
            value.AnalyzerId,
            ContractVersion.Parse(value.AnalyzerVersion),
            ContractVersion.Parse(value.SemanticContractVersion),
            ContractVersion.Parse(value.IdentityContractVersion),
            ContractVersion.Parse(value.RulesetVersion),
            value.TaxonomyId,
            ContractVersion.Parse(value.TaxonomyVersion),
            new AnalyzerScopeContract(
                value.Scope.SupportedInputs,
                FromReasoned(value.Scope.ExcludedInputs),
                value.Scope.SupportedRecordFieldAssetShapes,
                FromReasoned(value.Scope.ExcludedRecordFieldAssetShapes),
                value.Scope.SupportedTaxonomyCodes,
                FromReasoned(value.Scope.UnsupportedTaxonomyCodes),
                value.Scope.SupportedExtentFacets,
                FromReasoned(value.Scope.ExcludedExtentFacets)),
            value.InputPopulations.Select(
                item => new AnalyzerInputPopulationContract(
                    item.PopulationId,
                    item.Description,
                    item.Required)).ToArray(),
            value.Dependencies.Select(
                item => new AnalyzerDependencyContract(
                    item.DependencyId,
                    ContractVersion.Parse(item.MinimumVersion),
                    item.Required,
                    ParseCoverageState(item.MissingState))).ToArray(),
            ParseSnapshotAssurance(value.MinimumSnapshotAssurance),
            new AnalyzerThresholdsContract(
                FromThreshold(value.Thresholds.CandidateAdmission),
                FromThreshold(value.Thresholds.Evidence),
                FromThreshold(value.Thresholds.Abstention),
                FromThreshold(value.Thresholds.FindingPromotion)),
            value.PossibleOutputs,
            new AnalyzerCoverageDeclarationContract(
                value.Coverage.Populations,
                value.Coverage.PossibleStates.Select(ParseCoverageState).ToArray(),
                value.Coverage.UnsupportedBehavior),
            new AnalyzerOperationRequirementsContract(
                ParseExecutionRequirement(value.OperationRequirements.Mode),
                value.OperationRequirements.NetworkRequired,
                value.OperationRequirements.LlmRequired,
                value.OperationRequirements.ProviderRequired),
            new AnalyzerScaleAndCostContract(
                value.ExpectedScaleAndCost.PopulationScale,
                ParseCostClass(value.ExpectedScaleAndCost.CostClass),
                value.ExpectedScaleAndCost.Billable),
            new AnalyzerResourceBoundsContract(
                value.ResourceBounds.MaxInputItems,
                value.ResourceBounds.MaxOutputItems,
                value.ResourceBounds.MaxWallTimeMs),
            ParseMaturity(value.Maturity),
            value.RawDevelopmentOutput,
            value.PresetOrMaturitySuppression,
            new LinkedEvaluationCasesContract(
                value.LinkedEvaluationCases.Positive,
                value.LinkedEvaluationCases.Negative,
                value.LinkedEvaluationCases.Boundary,
                value.LinkedEvaluationCases.Malformed,
                value.LinkedEvaluationCases.CrossCategory,
                value.LinkedEvaluationCases.Gap),
            value.PayloadContracts.Select(item => new PayloadContractDeclarationContract(
                item.SchemaId,
                ContractVersion.Parse(item.SchemaVersion),
                item.Required)).ToArray(),
            ContractVersion.Parse(value.StateModelVersion),
            value.NotUsedBoundaries.Select(item =>
                StringComparer.Ordinal.Equals(item.State, "not-used")
                    ? new ExecutionBoundaryContract(item.BoundaryId, BoundaryUseState.NotUsed, item.Reason)
                    : throw new InvalidDataException($"Unknown boundary state '{item.State}'.")).ToArray())
        {
            AnalyzerFamily = value.AnalyzerFamily,
        };
    }

    private static ReasonedScopeDto[] ToReasoned(IReadOnlyList<ReasonedAnalyzerScopeContract> values)
    {
        return values.Select(item => new ReasonedScopeDto(item.ScopeId, item.Reason)).ToArray();
    }

    private static ReasonedAnalyzerScopeContract[] FromReasoned(IReadOnlyList<ReasonedScopeDto> values)
    {
        return values.Select(item => new ReasonedAnalyzerScopeContract(item.ScopeId, item.Reason)).ToArray();
    }

    private static ThresholdDto ToThreshold(AnalyzerThresholdContract value)
    {
        return new ThresholdDto(value.Name, value.Version, value.Rule);
    }

    private static AnalyzerThresholdContract FromThreshold(ThresholdDto value)
    {
        return new AnalyzerThresholdContract(value.RuleId, value.RulesetVersion, value.Description);
    }

    private static string SnapshotAssuranceToWire(SnapshotAssuranceState value)
    {
        return value switch
        {
            SnapshotAssuranceState.Structural => "structural",
            SnapshotAssuranceState.SelectivelyContentSealed => "selectively-content-sealed",
            SnapshotAssuranceState.FullyByteSealed => "fully-byte-sealed",
            _ => throw new InvalidOperationException("Analyzer minimum snapshot assurance is unsupported."),
        };
    }

    private static SnapshotAssuranceState ParseSnapshotAssurance(string value)
    {
        return value switch
        {
            "structural" => SnapshotAssuranceState.Structural,
            "selectively-content-sealed" => SnapshotAssuranceState.SelectivelyContentSealed,
            "fully-byte-sealed" => SnapshotAssuranceState.FullyByteSealed,
            _ => throw new InvalidDataException($"Unknown snapshot assurance '{value}'."),
        };
    }

    private static string CoverageStateToWire(CoverageState value)
    {
        return value switch
        {
            CoverageState.Completed => "completed",
            CoverageState.CompletedWithGaps => "completed-with-gaps",
            CoverageState.Failed => "failed",
            CoverageState.SkippedByConfiguration => "skipped-by-configuration",
            CoverageState.SkippedByLimit => "skipped-by-limit",
            CoverageState.Unsupported => "unsupported",
            _ => throw new InvalidOperationException("Analyzer coverage state is unsupported."),
        };
    }

    private static CoverageState ParseCoverageState(string value)
    {
        return value switch
        {
            "completed" => CoverageState.Completed,
            "completed-with-gaps" => CoverageState.CompletedWithGaps,
            "failed" => CoverageState.Failed,
            "skipped-by-configuration" => CoverageState.SkippedByConfiguration,
            "skipped-by-limit" => CoverageState.SkippedByLimit,
            "unsupported" => CoverageState.Unsupported,
            _ => throw new InvalidDataException($"Unknown analyzer coverage state '{value}'."),
        };
    }

    private static string ExecutionRequirementToWire(ExecutionRequirement value)
    {
        return value switch
        {
            ExecutionRequirement.LocalOnly => "local-only",
            ExecutionRequirement.CachedExternalEvidence => "cached-external-evidence",
            ExecutionRequirement.LiveNetwork => "live-network",
            ExecutionRequirement.LlmProvider => "llm-provider",
            _ => throw new InvalidOperationException("Analyzer operation requirement is unsupported."),
        };
    }

    private static ExecutionRequirement ParseExecutionRequirement(string value)
    {
        return value switch
        {
            "local-only" => ExecutionRequirement.LocalOnly,
            "cached-external-evidence" => ExecutionRequirement.CachedExternalEvidence,
            "live-network" => ExecutionRequirement.LiveNetwork,
            "llm-provider" => ExecutionRequirement.LlmProvider,
            _ => throw new InvalidDataException($"Unknown operation requirement '{value}'."),
        };
    }

    private static string CostClassToWire(AnalyzerCostClass value)
    {
        return value switch
        {
            AnalyzerCostClass.LocalLow => "local-low",
            AnalyzerCostClass.LocalModerate => "local-moderate",
            AnalyzerCostClass.LocalHigh => "local-high",
            AnalyzerCostClass.ProviderBounded => "provider-bounded",
            _ => throw new InvalidOperationException("Analyzer cost class is unsupported."),
        };
    }

    private static AnalyzerCostClass ParseCostClass(string value)
    {
        return value switch
        {
            "local-low" => AnalyzerCostClass.LocalLow,
            "local-moderate" => AnalyzerCostClass.LocalModerate,
            "local-high" => AnalyzerCostClass.LocalHigh,
            "provider-bounded" => AnalyzerCostClass.ProviderBounded,
            _ => throw new InvalidDataException($"Unknown analyzer cost class '{value}'."),
        };
    }

    private static AnalyzerMaturity ParseMaturity(string value)
    {
        return StringComparer.Ordinal.Equals(value, "Experimental")
            ? AnalyzerMaturity.Experimental
            : throw new InvalidDataException($"Unknown analyzer maturity '{value}'.");
    }

    private sealed record AnalyzerDeclarationDto(
        string SchemaId,
        string SchemaVersion,
        string AnalyzerId,
        string AnalyzerFamily,
        string AnalyzerVersion,
        string SemanticContractVersion,
        string IdentityContractVersion,
        string RulesetVersion,
        string TaxonomyId,
        string TaxonomyVersion,
        AnalyzerScopeDto Scope,
        IReadOnlyList<InputPopulationDto> InputPopulations,
        IReadOnlyList<DependencyDto> Dependencies,
        string MinimumSnapshotAssurance,
        ThresholdsDto Thresholds,
        IReadOnlyList<string> PossibleOutputs,
        CoverageDto Coverage,
        OperationRequirementsDto OperationRequirements,
        ScaleAndCostDto ExpectedScaleAndCost,
        ResourceBoundsDto ResourceBounds,
        string Maturity,
        bool RawDevelopmentOutput,
        bool PresetOrMaturitySuppression,
        LinkedCasesDto LinkedEvaluationCases,
        IReadOnlyList<PayloadContractDto> PayloadContracts,
        string StateModelVersion,
        IReadOnlyList<BoundaryDto> NotUsedBoundaries);

    private sealed record PayloadContractDto(string SchemaId, string SchemaVersion, bool Required);

    private sealed record BoundaryDto(string BoundaryId, string State, string Reason);

    private sealed record AnalyzerScopeDto(
        IReadOnlyList<string> SupportedInputs,
        IReadOnlyList<ReasonedScopeDto> ExcludedInputs,
        IReadOnlyList<string> SupportedRecordFieldAssetShapes,
        IReadOnlyList<ReasonedScopeDto> ExcludedRecordFieldAssetShapes,
        IReadOnlyList<string> SupportedTaxonomyCodes,
        IReadOnlyList<ReasonedScopeDto> UnsupportedTaxonomyCodes,
        IReadOnlyList<string> SupportedExtentFacets,
        IReadOnlyList<ReasonedScopeDto> ExcludedExtentFacets);

    private sealed record ReasonedScopeDto(string ScopeId, string Reason);

    private sealed record InputPopulationDto(string PopulationId, string Description, bool Required);

    private sealed record DependencyDto(
        string DependencyId,
        string MinimumVersion,
        bool Required,
        string MissingState);

    private sealed record ThresholdsDto(
        ThresholdDto CandidateAdmission,
        ThresholdDto Evidence,
        ThresholdDto Abstention,
        ThresholdDto FindingPromotion);

    private sealed record ThresholdDto(string RuleId, string RulesetVersion, string Description);

    private sealed record CoverageDto(
        IReadOnlyList<string> Populations,
        IReadOnlyList<string> PossibleStates,
        string UnsupportedBehavior);

    private sealed record OperationRequirementsDto(
        string Mode,
        bool NetworkRequired,
        bool LlmRequired,
        bool ProviderRequired);

    private sealed record ScaleAndCostDto(string PopulationScale, string CostClass, bool Billable);

    private sealed record ResourceBoundsDto(
        long MaxInputItems,
        long MaxOutputItems,
        long MaxWallTimeMs);

    private sealed record LinkedCasesDto(
        IReadOnlyList<string> Positive,
        IReadOnlyList<string> Negative,
        IReadOnlyList<string> Boundary,
        IReadOnlyList<string> Malformed,
        IReadOnlyList<string> CrossCategory,
        IReadOnlyList<string> Gap);
}
