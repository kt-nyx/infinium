using Infinium.Analysis.Candidates;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal sealed class TestCandidatePopulationSource(
    OpaqueId analyzerId,
    IReadOnlyList<CausalJoinPopulationMember> members,
    AnalyzerDeclarationContract? declaration = null) : ICandidatePopulationSource
{
    public OpaqueId AnalyzerId => analyzerId;
    public AnalyzerDeclarationContract Declaration { get; } = declaration ?? CandidateAnalyzerDeclarations.Create(
        analyzerId, Math.Max(1, members.Count), 1_000_000,
        supportedShapes: members.Select(item => item.JoinKind).Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal).DefaultIfEmpty("invalid-causal-join").ToArray());
    public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
        CandidatePopulationContext context,
        CancellationToken cancellationToken = default) => members;
    public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
        CandidatePopulationContext context,
        CancellationToken cancellationToken = default) => members;
}
