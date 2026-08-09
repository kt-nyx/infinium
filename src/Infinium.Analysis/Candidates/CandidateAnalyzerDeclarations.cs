using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Candidates;

public static class CandidateAnalyzerDeclarations
{
    public static AnalyzerDeclarationContract Create(
        OpaqueId analyzerId,
        long maximumInputItems = 1_000_000,
        long maximumOutputItems = 1_000_000,
        string supportedInput = "typed-causal-population",
        IReadOnlyList<string>? supportedShapes = null,
        IReadOnlyList<AnalyzerInputPopulationContract>? inputPopulations = null,
        IReadOnlyList<AnalyzerDependencyContract>? dependencies = null) => new(
        ContractConstants.AnalyzerDeclarationSchemaId,
        new ContractVersion(1, 0, 0),
        analyzerId.Value,
        new ContractVersion(1, 0, 0),
        new ContractVersion(1, 0, 0),
        new ContractVersion(1, 0, 0),
        new ContractVersion(1, 0, 0),
        ContractConstants.TaxonomyId,
        ContractVersion.Parse(ContractConstants.TaxonomyVersion),
        new AnalyzerScopeContract(
            [supportedInput],
            [new("untyped-or-unbounded-input", "outside the bounded WP3 causal population")],
            supportedShapes ?? ["canonical-participant-join"],
            [new("whole-profile-or-all-pairs", "explicitly excluded from WP3")],
            [
                "surface.plugin-data",
                "surface.asset",
                "extent.subject.bounded-set",
                "extent.propagation.bounded-dependents",
            ],
            [
                new("purpose-target.*", "WP3 does not infer declared purpose from local causal inputs"),
                new("consequence.*", "candidate generation establishes no consequence or finding"),
            ],
            ["extent.subject.bounded-set", "extent.propagation.bounded-dependents"],
            [new("extent.*.runtime-or-installation-wide", "WP3 establishes no runtime-wide effect extent")]),
        inputPopulations ?? [new("eligible-causal-population", "one declared row per bounded relationship member", true)],
        dependencies ?? [new(supportedInput, new ContractVersion(1, 0, 0), true, CoverageState.Unsupported)],
        SnapshotAssuranceState.SelectivelyContentSealed,
        new AnalyzerThresholdsContract(
            new("candidate-admission", "1", "closed lane and disposition rules"),
            new("evidence", "1", "retained supporting evidence is required for non-invalid work"),
            new("abstention", "1", "missing required information retains an explicit abstention"),
            new("finding-promotion", "1", "not performed by WP3")),
        ["candidate-decision", "candidate", "hypothesis", "abstention", "coverage-gap", "failure"],
        new AnalyzerCoverageDeclarationContract(
            ["eligible-causal-population"],
            [CoverageState.Completed, CoverageState.CompletedWithGaps, CoverageState.Failed, CoverageState.SkippedByLimit, CoverageState.Unsupported],
            "Unsupported or incomplete inputs retain explicit decisions and gaps."),
        new AnalyzerOperationRequirementsContract(ExecutionRequirement.LocalOnly, false, false, false),
        new AnalyzerScaleAndCostContract("bounded at one million population members", AnalyzerCostClass.LocalModerate, false),
        new AnalyzerResourceBoundsContract(maximumInputItems, maximumOutputItems, 120_000),
        AnalyzerMaturity.Experimental,
        true,
        false,
        new LinkedEvaluationCasesContract(
            ["EVAL-0001"], ["EVAL-0002"], ["EVAL-0016"],
            ["EVAL-0017"], ["EVAL-0032"], ["EVAL-0065"]),
        [
            new(ContractConstants.DocumentationEvidenceSchemaId, new ContractVersion(1, 0, 0), true),
            new(ContractConstants.CandidateAnalysisSchemaId, new ContractVersion(1, 0, 0), true),
            new(ContractConstants.FindingCaseSchemaId, new ContractVersion(1, 0, 0), true),
            new(ContractConstants.AnalysisReplaySchemaId, new ContractVersion(1, 0, 0), true),
            new(ContractConstants.AnalysisExecutionInputSchemaId, new ContractVersion(1, 0, 0), true),
        ],
        new ContractVersion(1, 0, 0),
        [
            new("provider", BoundaryUseState.NotUsed, "deterministic local analyzer"),
            new("hosted-search", BoundaryUseState.NotUsed, "deterministic local analyzer"),
            new("nexus", BoundaryUseState.NotUsed, "deterministic local analyzer"),
            new("loot", BoundaryUseState.NotUsed, "deterministic local analyzer"),
        ]);
}
