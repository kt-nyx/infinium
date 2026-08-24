using Infinium.Analysis.Candidates;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.ScopeReversion;

public static class ScopeReversionAnalyzerDeclaration
{
    public const string AnalyzerFamily = "infinium.scope-reversion";
    public const string AnalyzerId = "infinium.scope-reversion.local";

    public static AnalyzerDeclarationContract Create()
    {
        AnalyzerDeclarationContract declaration = CandidateAnalyzerDeclarations.Create(
            new OpaqueId(AnalyzerId),
            maximumInputItems: 100_000,
            maximumOutputItems: 1_000_000,
            supportedInput: "typed-neutral-scope-transition",
            supportedShapes:
            [
                "actor-ai-package-loss-after-appearance-change",
                "placed-reference-link-loss-after-position-change",
            ],
            inputPopulations:
            [
                new("actor-transition", "qualified actor contribution transitions", true),
                new("actor-purpose-applicability", "actor purpose and applicability decisions", true),
                new("actor-conclusion-taxonomy", "actor conclusion and taxonomy projections", true),
                new("reference-transition", "qualified placed-reference contribution transitions", true),
                new("reference-purpose-applicability", "placed-reference purpose and applicability decisions", true),
                new("reference-conclusion-taxonomy", "placed-reference conclusion and taxonomy projections", true),
                new("publication-replay", "scope-reversion publication and replay members", true),
            ],
            dependencies:
            [
                new("bethesda-semantic-substrate", new ContractVersion(1, 0, 0), true, CoverageState.Unsupported),
                new("admitted-purpose-applicability", new ContractVersion(1, 0, 0), true, CoverageState.Unsupported),
            ]);
        return declaration with
        {
            AnalyzerFamily = AnalyzerFamily,
            AnalyzerVersion = new ContractVersion(1, 0, 0),
            SemanticContractVersion = new ContractVersion(1, 0, 0),
            IdentityContractVersion = new ContractVersion(1, 0, 0),
            RulesetVersion = new ContractVersion(1, 0, 0),
            Scope = declaration.Scope with
            {
                UnsupportedTaxonomyCodes =
                [
                    new("purpose-target.unsupported", "the local analyzer does not infer unsupported purpose dimensions"),
                    new("consequence.unsupported", "the local analyzer does not invent unsupported consequences"),
                ],
                ExcludedExtentFacets =
                [
                    new("extent.runtime-or-installation-wide", "the local analyzer establishes no runtime-wide effect extent"),
                ],
            },
            PossibleOutputTypes =
            [
                "deterministic-result", "candidate", "hypothesis", "finding", "recommendation",
                "supported-case", "abstention", "invalid-input", "coverage-gap", "failure",
            ],
            Thresholds = new AnalyzerThresholdsContract(
                new("candidate-admission", "1", "one admitted neutral member produces one closed disposition"),
                new("evidence", "1", "retained evidence and dependency identities are required for semantic publication"),
                new("abstention", "1", "any unresolved required axis retains an explicit abstention without promotion"),
                new("finding-promotion", "1", "only one closed supported scope-incongruent transition promotes one finding and one causal case")),
            Coverage = new AnalyzerCoverageDeclarationContract(
                [
                    "actor-transition", "actor-purpose-applicability", "actor-conclusion-taxonomy",
                    "reference-transition", "reference-purpose-applicability", "reference-conclusion-taxonomy",
                    "publication-replay",
                ],
                [
                    CoverageState.Completed, CoverageState.CompletedWithGaps, CoverageState.Failed,
                    CoverageState.SkippedByConfiguration, CoverageState.SkippedByLimit, CoverageState.Unsupported,
                ],
                "Disabled, limited, unsupported, failed, and incomplete populations retain their exact non-completed state and never become safety percentages."),
            ExpectedScaleAndCost = new AnalyzerScaleAndCostContract(
                "bounded at one hundred thousand admitted members and one million output items",
                AnalyzerCostClass.LocalModerate,
                false),
            Maturity = AnalyzerMaturity.Experimental,
            LinkedEvaluationCases = new LinkedEvaluationCasesContract(
                ["EVAL-0001", "EVAL-0084"],
                ["EVAL-0002", "EVAL-0086"],
                ["EVAL-0065", "EVAL-0067", "EVAL-0083", "EVAL-0085"],
                ["EVAL-0032"],
                ["EVAL-0016"],
                ["EVAL-0017"]),
            PayloadContracts =
            [
                .. declaration.PayloadContracts,
                new(ContractConstants.ScopeReversionSchemaId, new ContractVersion(1, 0, 0), true),
            ],
            NotUsedBoundaries =
            [
                new("provider", BoundaryUseState.NotUsed, "deterministic local analyzer"),
                new("hosted-search", BoundaryUseState.NotUsed, "deterministic local analyzer"),
                new("nexus", BoundaryUseState.NotUsed, "deterministic local analyzer"),
                new("loot", BoundaryUseState.NotUsed, "deterministic local analyzer"),
            ],
        };
    }
}
