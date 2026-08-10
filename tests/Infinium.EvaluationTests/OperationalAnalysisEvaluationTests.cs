using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Analysis;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class OperationalAnalysisEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void AnalysisOperationalFixtureRegistryIsFrozenClosedAndAnswerIsolated()
    {
        string root = FixtureRoot();
        using JsonDocument manifest = Parse(Path.Combine(root, "fixture-manifest.v1.json"));
        using JsonDocument projections = Parse(Path.Combine(root, "ordinary-product-projections.v1.json"));
        using JsonDocument safetyTopologies = Parse(Path.Combine(root, "safety-topologies.v1.json"));
        using JsonDocument envelope = Parse(Path.Combine(root, "harness-envelope.v1.json"));
        using JsonDocument expected = Parse(Path.Combine(root, "expected-results.v1.json"));

        JsonElement manifestRoot = manifest.RootElement;
        StringAssert.Contains(manifestRoot.GetProperty("status").GetString()!, "independent");
        Assert.IsFalse(expected.RootElement.GetProperty("product_output_used").GetBoolean());
        Assert.AreEqual(
            envelope.RootElement.GetProperty("registry_identity").GetString(),
            expected.RootElement.GetProperty("registry_identity").GetString());

        HashSet<string> inputCases = envelope.RootElement.GetProperty("case_bindings").EnumerateArray()
            .Select(item => item.GetProperty("case_id").GetString()!).ToHashSet(StringComparer.Ordinal);
        HashSet<string> expectedCases = CaseIds(expected.RootElement);
        Assert.HasCount(12, inputCases);
        Assert.IsTrue(inputCases.SetEquals(expectedCases));
        Assert.AreEqual(12, projections.RootElement.GetProperty("projections").GetArrayLength());

        HashSet<string> declaredPaths = manifestRoot.GetProperty("package_file_paths").EnumerateArray()
            .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        HashSet<string> physicalPaths = Directory.EnumerateFiles(root)
            .Select(path => Path.GetFileName(path)!).ToHashSet(StringComparer.Ordinal);
        Assert.HasCount(8, declaredPaths);
        Assert.IsTrue(declaredPaths.SetEquals(physicalPaths));
        HashSet<string> hashBoundPaths = manifestRoot.GetProperty("files").EnumerateArray()
            .Select(item => item.GetProperty("path").GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.HasCount(7, hashBoundPaths);
        Assert.IsTrue(hashBoundPaths.SetEquals(declaredPaths.Where(path => path != "fixture-manifest.v1.json")));

        HashSet<string> forbiddenNames = envelope.RootElement.GetProperty("product_projection_contract")
            .GetProperty("forbidden_recursive_property_names").EnumerateArray()
            .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (JsonElement projection in projections.RootElement.GetProperty("projections").EnumerateArray())
        {
            AssertProjectionNamesAreClosed(projection, forbiddenNames);
        }

        foreach (JsonElement file in manifestRoot.GetProperty("files").EnumerateArray())
        {
            string path = Path.Combine(root, file.GetProperty("path").GetString()!);
            Assert.AreEqual(file.GetProperty("bytes").GetInt64(), new FileInfo(path).Length);
            string actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.AreEqual(file.GetProperty("sha256").GetString(), actual);
        }

        List<string[]> safetyOrders = [];
        foreach (JsonElement binding in envelope.RootElement.GetProperty("case_bindings").EnumerateArray()
            .Where(item => item.GetProperty("behavior_family").GetString() == "write-and-nonmutation-safety"))
        {
            string caseId = binding.GetProperty("case_id").GetString()!;
            JsonElement safety = ExpectedCase(expected.RootElement, caseId).GetProperty("expected");
            Assert.AreEqual(0, safety.GetProperty("writes_outside_authorized_final_objects").GetInt32());
            Assert.AreEqual(0, safety.GetProperty("network_requests").GetInt32());
            Assert.AreEqual(0, safety.GetProperty("external_processes").GetInt32());
            Assert.AreEqual(0, safety.GetProperty("credential_operations").GetInt32());
            safetyOrders.Add(DeriveAndVerifySafety(
                projections.RootElement,
                safetyTopologies.RootElement,
                binding,
                safety));
        }
        Assert.HasCount(2, safetyOrders);
        Assert.IsFalse(safetyOrders[0].SequenceEqual(safetyOrders[1], StringComparer.Ordinal));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void CleanIncrementalReplayFixtureInvalidatesOnlyTheChangedDependencyClosure()
    {
        using JsonDocument projections = Parse(Path.Combine(FixtureRoot(), "ordinary-product-projections.v1.json"));
        using JsonDocument expected = Parse(Path.Combine(FixtureRoot(), "expected-results.v1.json"));
        JsonElement input = projections.RootElement.GetProperty("projections")[1];
        JsonElement expectation = ExpectedCase(expected.RootElement, "OPS-LANTERN-REPLAY-D02")
            .GetProperty("expected");

        HashSet<string> allNodes = input.GetProperty("entities").EnumerateArray()
            .Select(item => item.GetProperty("identity").GetString()!)
            .Concat(input.GetProperty("relations").EnumerateArray().SelectMany(relation => new[]
            {
                relation.GetProperty("from").GetString()!, relation.GetProperty("to").GetString()!,
            })).ToHashSet(StringComparer.Ordinal);
        IReadOnlySet<string> invalidated = ReplayInvalidationPlanner.InvalidatedClosure(
            input.GetProperty("relations").EnumerateArray().Select(relation => (
                relation.GetProperty("from").GetString()!, relation.GetProperty("to").GetString()!)),
            ["source-maple-r1"]);

        string[] expectedInvalidated = expectation.GetProperty("invalidated_nodes").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
        string[] expectedReused = expectation.GetProperty("reused_nodes").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
        Assert.IsTrue(invalidated.SetEquals(expectedInvalidated));
        Assert.IsTrue(allNodes.Except(invalidated).Where(node => node is "source-elm-r1" or "analyze-elm")
            .ToHashSet(StringComparer.Ordinal).SetEquals(expectedReused));
        Assert.AreEqual(3, expectation.GetProperty("equivalent_executions").GetArrayLength());
        Assert.AreEqual(0, expectation.GetProperty("network_calls").GetInt32());
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void FrozenBoundedQueryCasesExecuteThroughTheProductKeysetPaginator()
    {
        using JsonDocument projections = Parse(Path.Combine(FixtureRoot(), "ordinary-product-projections.v1.json"));
        using JsonDocument envelope = Parse(Path.Combine(FixtureRoot(), "harness-envelope.v1.json"));
        using JsonDocument expected = Parse(Path.Combine(FixtureRoot(), "expected-results.v1.json"));

        foreach (JsonElement binding in envelope.RootElement.GetProperty("case_bindings").EnumerateArray()
            .Where(item => item.GetProperty("behavior_family").GetString() == "bounded-query"))
        {
            int projectionIndex = binding.GetProperty("product_projection_index").GetInt32();
            JsonElement projection = projections.RootElement.GetProperty("projections")[projectionIndex];
            JsonElement command = projection.GetProperty("commands")[0];
            Dictionary<string, JsonElement> parameters = command.GetProperty("parameters").EnumerateArray()
                .ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("value"), StringComparer.Ordinal);
            string filterName = parameters.ContainsKey("filter-state") ? "state" : "group";
            string filterValue = parameters.Values.Single(value => value.ValueKind == JsonValueKind.String
                && (value.GetString() is "visible" or "open")).GetString()!;
            int pageSize = parameters["page-size"].GetInt32();
            AnalysisArtifactSortOrder sort = parameters["sort"][0].GetString() == "rank-descending"
                ? AnalysisArtifactSortOrder.RankDescendingIdentityAscending
                : AnalysisArtifactSortOrder.UpdatedTickDescendingIdentityDescending;
            AnalysisArtifactPersistenceRecord[] records = projection.GetProperty("entities").EnumerateArray()
                .Select(entity =>
                {
                    Dictionary<string, JsonElement> attributes = entity.GetProperty("attributes").EnumerateArray()
                        .ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("value"), StringComparer.Ordinal);
                    return new AnalysisArtifactPersistenceRecord(
                        entity.GetProperty("identity").GetString()!, "result", "fixture-result", "1.0.0", 1,
                        attributes[filterName].GetString()!, new string('a', 64), 1, "p", "c",
                        attributes.GetValueOrDefault("rank").ValueKind == JsonValueKind.Number ? attributes["rank"].GetInt64() : 0,
                        attributes.GetValueOrDefault("updated-tick").ValueKind == JsonValueKind.Number ? attributes["updated-tick"].GetInt64() : 0);
                }).ToArray();
            List<string[]> actualPages = PageAll(records, filterValue, pageSize, sort);
            JsonElement oracle = ExpectedCase(expected.RootElement, binding.GetProperty("case_id").GetString()!)
                .GetProperty("expected");
            string[][] expectedPages = oracle.GetProperty("pages").EnumerateArray()
                .Select(page => page.EnumerateArray().Select(item => item.GetString()!).ToArray()).ToArray();
            Assert.AreEqual(expectedPages.Length, actualPages.Count);
            for (int index = 0; index < expectedPages.Length; index++)
            {
                CollectionAssert.AreEqual(expectedPages[index], actualPages[index]);
            }
            List<string[]> permutedPages = PageAll(records.Reverse().ToArray(), filterValue, pageSize, sort);
            Assert.IsTrue(actualPages.SelectMany(item => item).SequenceEqual(permutedPages.SelectMany(item => item), StringComparer.Ordinal));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AnalysisArtifactKeysetPaginator.Page(
                records, new HashSet<string>(), new HashSet<string>(), 101, sort, null));
        }
    }

    private static HashSet<string> CaseIds(JsonElement root) => root.GetProperty("cases").EnumerateArray()
        .Select(item => item.GetProperty("case_id").GetString()!).ToHashSet(StringComparer.Ordinal);

    private static JsonElement ExpectedCase(JsonElement root, string id) => root.GetProperty("cases").EnumerateArray()
        .Single(item => item.GetProperty("case_id").GetString() == id);

    private static string[] DeriveAndVerifySafety(
        JsonElement projectionRegistry,
        JsonElement topologyRegistry,
        JsonElement binding,
        JsonElement expected)
    {
        int projectionIndex = binding.GetProperty("product_projection_index").GetInt32();
        JsonElement projection = projectionRegistry.GetProperty("projections")[projectionIndex];
        JsonElement topology = topologyRegistry.GetProperty("topologies").EnumerateArray()
            .Single(item => item.GetProperty("projection_index").GetInt32() == projectionIndex);
        Dictionary<string, string> rootAuthorities = topology.GetProperty("roots").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("identity").GetString()!,
                item => item.GetProperty("authority").GetString()!,
                StringComparer.Ordinal);
        Dictionary<string, string> objectRoots = topology.GetProperty("objects").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("identity").GetString()!,
                item => item.GetProperty("owner_root").GetString()!,
                StringComparer.Ordinal);
        Dictionary<string, JsonElement> targets = topology.GetProperty("targets").EnumerateArray()
            .ToDictionary(item => item.GetProperty("target").GetString()!, StringComparer.Ordinal);

        List<int> accepted = [];
        List<int> rejected = [];
        List<string> decisions = [];
        List<string> resolutionOrder = [];
        int index = 0;
        foreach (JsonElement command in projection.GetProperty("commands").EnumerateArray())
        {
            string targetId = command.GetProperty("parameters")[0].GetProperty("value").GetString()!;
            JsonElement target = targets[targetId];
            string finalObject = target.GetProperty("final_open_object").GetString()!;
            bool accepts = FinalObjectAuthorityPolicy.IsAuthorized(
                target.GetProperty("operation_supported").GetBoolean(),
                target.GetProperty("capability_at_use").GetString() == "fresh",
                finalObjectIdentityProven: true,
                rootAuthorities[objectRoots[finalObject]] == "authorized-write");
            (accepts ? accepted : rejected).Add(index++);
            decisions.Add(targetId + (accepts ? ":accept" : ":reject"));
            resolutionOrder.Add(target.GetProperty("resolution_kind").GetString()!);
        }

        CollectionAssert.AreEqual(
            expected.GetProperty("accepted_command_indices").EnumerateArray().Select(item => item.GetInt32()).ToArray(),
            accepted.ToArray());
        CollectionAssert.AreEqual(
            expected.GetProperty("rejected_command_indices").EnumerateArray().Select(item => item.GetInt32()).ToArray(),
            rejected.ToArray());
        CollectionAssert.AreEqual(
            expected.GetProperty("target_decisions").EnumerateArray().Select(item => item.GetString()!).ToArray(),
            decisions.ToArray());
        Assert.AreEqual(25, accepted.Count + rejected.Count);
        Assert.IsGreaterThan(0, accepted.Count);
        Assert.IsGreaterThan(0, rejected.Count);
        return resolutionOrder.ToArray();
    }

    private static List<string[]> PageAll(
        IReadOnlyList<AnalysisArtifactPersistenceRecord> records,
        string state,
        int pageSize,
        AnalysisArtifactSortOrder sort)
    {
        List<string[]> pages = [];
        AnalysisArtifactCursorKey? cursor = null;
        do
        {
            AnalysisArtifactPagePersistenceRecord page = AnalysisArtifactKeysetPaginator.Page(
                records, new HashSet<string>(), new HashSet<string>([state], StringComparer.Ordinal),
                pageSize, sort, cursor);
            pages.Add(page.Items.Select(item => item.ArtifactId).ToArray());
            cursor = page.NextKey;
        }
        while (cursor is not null);
        return pages;
    }

    private static void AssertProjectionNamesAreClosed(JsonElement value, IReadOnlySet<string> forbiddenNames)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.IsFalse(forbiddenNames.Contains(property.Name), $"Harness-only property leaked: {property.Name}");
                AssertProjectionNamesAreClosed(property.Value, forbiddenNames);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertProjectionNamesAreClosed(item, forbiddenNames);
            }
        }
    }

    private static JsonDocument Parse(string path) => JsonDocument.Parse(
        File.ReadAllBytes(path),
        new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });

    private static string FixtureRoot() => Path.Combine(
        FindRepositoryRoot(), "fixtures", "public", "operations", "analysis-lifecycle");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Infinium repository root was not found.");
    }
}
