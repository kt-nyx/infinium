using System.Text.Json;
using Infinium.Analysis.ScopeReversion;
using Infinium.Application.ScopeReversion;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

public static class ScopeReversionTestSupport
{
    public static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Infinium repository root.");
    }

    public static ScopeReversionFixturePackage Fixture() =>
        ScopeReversionFixtureReader.Read(RepositoryRoot());

    public static IReadOnlyDictionary<string, ReadOnlyMemory<byte>> RetainedArtifacts(
        ScopeReversionCompositionRequest request,
        ScopeReversionAnalysisContract analysis)
    {
        Dictionary<string, byte[]> sourceBytesByMember = request.ActorInputs.ToDictionary(
            item => item.MemberId.Value,
            item => JsonSerializer.SerializeToUtf8Bytes(item, ContractJsonSerializer.Options),
            StringComparer.Ordinal);
        foreach (PlacedReferenceScopeReversionInput item in request.ReferenceInputs)
        {
            sourceBytesByMember.Add(
                item.MemberId.Value,
                JsonSerializer.SerializeToUtf8Bytes(item, ContractJsonSerializer.Options));
        }
        Dictionary<string, string> memberByDecision = analysis.Decisions.ToDictionary(
            item => item.DecisionId.Value,
            item => item.MemberId.Value,
            StringComparer.Ordinal);
        Dictionary<string, ReadOnlyMemory<byte>> retained = new(StringComparer.Ordinal);
        foreach (ScopeReversionDependencyEdgeContract edge in analysis.DependencyEdges
            .Where(item => item.ToKind is "dependency" or "evidence"))
        {
            if (edge.FromKind != "decision"
                || !memberByDecision.TryGetValue(edge.FromId.Value, out string? memberId)
                || !sourceBytesByMember.TryGetValue(memberId, out byte[]? bytes))
            {
                throw new InvalidDataException("A scope-reversion edge cannot resolve its exact typed source fact.");
            }
            if (retained.TryGetValue(edge.ToId.Value, out ReadOnlyMemory<byte> existing)
                && !existing.Span.SequenceEqual(bytes))
            {
                throw new InvalidDataException("A scope-reversion artifact identity resolves to different typed source facts.");
            }
            retained[edge.ToId.Value] = bytes;
        }
        return retained;
    }

    public static string Kebab(Enum value) =>
        string.Concat(value.ToString().Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? "-" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));
}
