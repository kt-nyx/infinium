using Infinium.Analysis.Candidates;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.ScopeReversion;

public static class ScopeReversionV2AnalyzerDeclaration
{
    public static AnalyzerDeclarationContract Create()
    {
        AnalyzerDeclarationContract declaration = CandidateAnalyzerDeclarations.Create(
            new OpaqueId(ScopeReversionAnalyzerDeclaration.AnalyzerId),
            maximumInputItems: 256,
            maximumOutputItems: 4096,
            supportedInput: "typed-neutral-scope-transition-cohort",
            supportedShapes: ["actor-cohort", "placed-reference"],
            inputPopulations:
            [
                new("actor-positive", "admitted actor-cohort positive subjects", true),
                new("actor-control", "matched actor-cohort restored-relation controls", true),
                new("reference-positive", "admitted placed-reference positive subjects", true),
                new("reference-control", "matched placed-reference restored-relation controls", true),
                new("purpose-application", "source support and exact local application decisions", true),
                new("taxonomy", "upstream taxonomy assignments", true),
            ],
            dependencies:
            [
                new("bethesda-semantic-substrate", new ContractVersion(2, 0, 0), true, CoverageState.Unsupported),
                new("admitted-source-application", new ContractVersion(1, 0, 0), true, CoverageState.Unsupported),
                new(ScopeReversionV2Contract.TaxonomyId, ScopeReversionV2Contract.TaxonomyVersion, true, CoverageState.Unsupported),
            ]);
        return declaration with
        {
            AnalyzerFamily = ScopeReversionAnalyzerDeclaration.AnalyzerFamily,
            AnalyzerVersion = new ContractVersion(2, 0, 0),
            SemanticContractVersion = new ContractVersion(2, 0, 0),
            IdentityContractVersion = new ContractVersion(2, 0, 0),
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
                "deterministic-result", "candidate", "hypothesis", "finding", "supported-case", "recommendation",
                "abstention", "invalid-input", "coverage-gap", "failure",
            ],
            Coverage = new AnalyzerCoverageDeclarationContract(
            [
                "actor-positive", "actor-control", "actor-unresolved",
                "reference-positive", "reference-control", "reference-unresolved",
                "analyzer", "persistence", "projection", "purpose", "replay", "taxonomy",
            ],
            [
                CoverageState.Completed, CoverageState.CompletedWithGaps, CoverageState.Failed,
                CoverageState.SkippedByLimit, CoverageState.Unsupported,
            ],
            "Unsupported or incomplete inputs remain in their declared population denominators."),
            Maturity = AnalyzerMaturity.Experimental,
            LinkedEvaluationCases = new LinkedEvaluationCasesContract(
                ["EVAL-0016", "EVAL-0017"], ["EVAL-0002"], ["EVAL-0065"], ["EVAL-0032"], ["EVAL-0086"], ["EVAL-0085"]),
            PayloadContracts =
            [
                .. declaration.PayloadContracts,
                new(ScopeReversionV2Contract.SchemaId, ScopeReversionV2Contract.SchemaVersion, true),
            ],
        };
    }
}
