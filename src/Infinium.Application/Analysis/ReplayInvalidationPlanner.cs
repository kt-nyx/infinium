namespace Infinium.Application.Analysis;

public static class ReplayInvalidationPlanner
{
    public static IReadOnlyList<(string From, string To)> NormalizeEdges(
        IEnumerable<(string From, string To)> dependencyEdges)
    {
        ArgumentNullException.ThrowIfNull(dependencyEdges);
        (string From, string To)[] edges = dependencyEdges.ToArray();
        if (edges.Any(item => string.IsNullOrWhiteSpace(item.From) || string.IsNullOrWhiteSpace(item.To)))
        {
            throw new InvalidDataException("Replay dependency edges require closed identities.");
        }
        return edges.Distinct().OrderBy(item => item.From, StringComparer.Ordinal)
            .ThenBy(item => item.To, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlySet<string> InvalidatedClosure(
        IEnumerable<(string From, string To)> dependencyEdges,
        IEnumerable<string> changedDependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencyEdges);
        ArgumentNullException.ThrowIfNull(changedDependencies);
        IReadOnlyList<(string From, string To)> edges = NormalizeEdges(dependencyEdges);
        HashSet<string> invalidated = changedDependencies.ToHashSet(StringComparer.Ordinal);
        bool added;
        do
        {
            added = false;
            foreach ((string from, string to) in edges)
            {
                if (invalidated.Contains(to))
                {
                    added |= invalidated.Add(from);
                }
            }
        }
        while (added);
        return invalidated;
    }

    public static IReadOnlyList<string> ReusableClosure(
        IEnumerable<(string From, string To)> dependencyEdges,
        IReadOnlySet<string> invalidated)
    {
        IReadOnlyList<(string From, string To)> edges = NormalizeEdges(dependencyEdges);
        HashSet<string> reusable = edges.SelectMany(item => new[] { item.From, item.To })
            .Where(item => !invalidated.Contains(item)).ToHashSet(StringComparer.Ordinal);
        List<string> ordered = [];
        while (ordered.Count != reusable.Count)
        {
            string[] ready = reusable.Where(node => !ordered.Contains(node, StringComparer.Ordinal)
                    && edges.Where(edge => edge.From == node && reusable.Contains(edge.To))
                        .All(edge => ordered.Contains(edge.To, StringComparer.Ordinal)))
                .Order(StringComparer.Ordinal).ToArray();
            if (ready.Length == 0)
            {
                throw new InvalidDataException("Replay dependency graph contains a cycle.");
            }
            ordered.AddRange(ready);
        }
        return ordered;
    }
}
