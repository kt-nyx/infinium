using System.Globalization;
using System.Text.Json;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaOracleAgreementEvaluationTests
{
    private static readonly string[] CoreFixtures =
        ["BETH-NPC-DEV", "BETH-REFR-DEV", "BETH-LIGHT-VAL"];
    private static readonly Dictionary<string, HashSet<string>> AllowedFields =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["NPC_"] = ["EDID", "ACBS", "TPLT", "RNAM", "AIDT", "PKID", "PNAM", "HCLF"],
            ["RACE"] = ["EDID", "DATA"],
            ["REFR"] = ["EDID", "NAME", "XLKR", "XLRL", "XOWN", "DATA"],
        };
    private static readonly HashSet<string> ParticipantRecordFamilies =
    [
        "NPC_", "RACE", "REFR", "CELL", "CLAS", "PACK", "CLFM", "FACT", "HDPT", "KYWD", "LCTN", "STAT",
    ];

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0052CoreRecordsLinksChainsAndWinnersMatchIndependentOracle()
    {
        foreach (string fixture in CoreFixtures)
        {
            using JsonDocument oracle = TestRepository.ReadJson(
                "test-data", "evaluation", "m1-semantic", fixture,
                "oracle", "independent-reader-report.json");
            foreach (JsonElement scenario in oracle.RootElement.GetProperty("scenario_semantics").EnumerateArray())
            {
                string scenarioId = scenario.GetProperty("scenario_id").GetString()!;
                BethesdaSemanticRequest request = ScenarioRequest(fixture, scenario);
                BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(request);
                JsonElement[] scenarioFiles = scenario.GetProperty("plugin_paths").EnumerateArray()
                    .Select(path => oracle.RootElement.GetProperty("files").EnumerateArray()
                        .Single(file => file.GetProperty("path").GetString() == path.GetString()))
                    .ToArray();
                bool rejectedByOracle = scenarioFiles.Any(OracleRequiresRejection);
                if (rejectedByOracle)
                {
                    Assert.AreEqual(BethesdaExtractionState.InvalidInput, result.State, scenarioId);
                    Assert.IsNull(result.Snapshot, scenarioId);
                    continue;
                }
                Assert.IsNotNull(
                    result.Snapshot,
                    $"{scenarioId}: {string.Join("; ", result.Failures.Select(failure => failure.Message))}");
                BethesdaSemanticSnapshot snapshot = result.Snapshot;
                HashSet<string> independentlyResolvedParticipants = IndependentlyResolvedParticipants(
                    oracle.RootElement,
                    request);

                foreach (JsonElement expectedRecord in scenario.GetProperty("records").EnumerateArray())
                {
                    string formKey = expectedRecord.GetProperty("form_key").GetString()!;
                    Assert.IsTrue(snapshot.ResolvedParticipants.ContainsKey(formKey), $"{scenarioId}: {formKey}");
                    string locator = expectedRecord.GetProperty("locator").GetString()!;
                    string sourcePlugin = Path.GetFileName(locator.Split('#')[0]);
                    BethesdaRecordContribution? contribution = FindContribution(snapshot, sourcePlugin, formKey);
                    JsonElement oracleRecord = OracleRecord(oracle.RootElement, locator);
                    string recordSignature = oracleRecord.GetProperty("signature").GetString()!;
                    JsonElement[] expectedLinks = expectedRecord.GetProperty("links").EnumerateArray().ToArray();
                    if (contribution is null)
                    {
                        Assert.IsFalse(AllowedFields.ContainsKey(recordSignature), $"{scenarioId}: allowlisted record {locator} has no typed contribution.");
                        Assert.AreEqual(0, expectedLinks.Length, $"{scenarioId}: unsupported identity record {locator} unexpectedly has oracle links.");
                        continue;
                    }

                    Assert.AreEqual(
                        expectedRecord.GetProperty("deleted").GetBoolean(),
                        contribution.Deleted,
                        $"{scenarioId}: {locator}");
                    Assert.AreEqual(
                        oracleRecord.GetProperty("compressed").GetBoolean(),
                        contribution.Compressed,
                        $"{scenarioId}: {locator} compression");
                    Assert.AreEqual(
                        uint.Parse(oracleRecord.GetProperty("flags_hex").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        contribution.RawFlags,
                        $"{scenarioId}: {locator} flags");
                    AssertTypedPayload(snapshot, contribution, oracleRecord, scenarioId, locator);
                    AssertFieldPresence(snapshot, contribution, oracleRecord, scenarioId, locator);
                    BethesdaLinkFact[] actualLinks = snapshot.Links
                        .Where(link => link.SourceContributionId == contribution.ContributionId)
                        .ToArray();
                    CollectionAssert.AreEquivalent(
                        expectedLinks.Select(link => OracleLinkKey(link, independentlyResolvedParticipants)).ToArray(),
                        actualLinks.Select(ProductLinkKey).ToArray(),
                        $"{scenarioId}: {locator}");
                }

                foreach (JsonElement expectedChain in scenario.GetProperty("chains").EnumerateArray())
                {
                    string formKey = expectedChain.GetProperty("form_key").GetString()!;
                    bool isSemanticFamily = snapshot.NpcContributions.Any(item => item.Contribution.Identity.FormKey == formKey)
                        || snapshot.RaceContributions.Any(item => item.Contribution.Identity.FormKey == formKey)
                        || snapshot.PlacedReferenceContributions.Any(item => item.Contribution.Identity.FormKey == formKey);
                    if (!isSemanticFamily)
                    {
                        Assert.IsTrue(snapshot.ResolvedParticipants.ContainsKey(formKey), $"{scenarioId}: {formKey}");
                        continue;
                    }

                    Assert.IsTrue(snapshot.OverrideChains.TryGetValue(formKey, out BethesdaOverrideChain? chain), $"{scenarioId}: {formKey}");
                    string[] expectedOrder = expectedChain.GetProperty("ordered_locators")
                        .EnumerateArray()
                        .Select(item => Path.GetFileName(item.GetString()!.Split('#')[0]))
                        .ToArray();
                    CollectionAssert.AreEqual(
                        expectedOrder,
                        chain!.Contributions.Select(item => item.SourcePlugin).ToArray(),
                        $"{scenarioId}: {formKey}");
                    Assert.AreEqual(
                        Path.GetFileName(expectedChain.GetProperty("winner_locator").GetString()!.Split('#')[0]),
                        chain.Winner.SourcePlugin,
                        $"{scenarioId}: {formKey}");
                }
                AssertPluginReceipts(snapshot, oracle.RootElement, scenario, scenarioId);
            }
        }
    }

    private static bool OracleRequiresRejection(JsonElement file)
    {
        if (file.GetProperty("malformed").ValueKind != JsonValueKind.Null
            || file.GetProperty("extension_header_mismatch").GetBoolean())
        {
            return true;
        }

        foreach (JsonElement record in file.GetProperty("records").EnumerateArray())
        {
            if (file.GetProperty("tes4").GetProperty("esl_flag").GetBoolean()
                && record.GetProperty("signature").GetString() != "TES4")
            {
                uint raw = uint.Parse(record.GetProperty("raw_form_id_hex").GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if ((raw & 0x00FF_FFFF) is < 0x800 or > 0xFFF)
                {
                    return true;
                }
            }

            if (record.TryGetProperty("identity", out JsonElement identity)
                && IsOutOfRangeLightIdentity(identity))
            {
                return true;
            }

            if (record.TryGetProperty("links", out JsonElement links)
                && links.EnumerateArray().Any(IsOutOfRangeLightIdentity))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOutOfRangeLightIdentity(JsonElement identity) =>
        (identity.TryGetProperty("resolution_state", out JsonElement state)
         && state.ValueKind == JsonValueKind.String
         && state.GetString() == "invalid")
        || (identity.TryGetProperty("origin_kind", out JsonElement kind)
        && kind.ValueKind == JsonValueKind.String
        && kind.GetString() == "light"
        && identity.TryGetProperty("local_id_hex", out JsonElement id)
        && id.ValueKind == JsonValueKind.String
        && uint.Parse(id.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture) is < 0x800 or > 0xFFF);

    private static void AssertTypedPayload(
        BethesdaSemanticSnapshot snapshot,
        BethesdaRecordContribution contribution,
        JsonElement oracleRecord,
        string scenarioId,
        string locator)
    {
        if (!oracleRecord.TryGetProperty("allowlisted_payload", out JsonElement payload))
        {
            return;
        }

        BethesdaNpcFact? npc = snapshot.NpcContributions.SingleOrDefault(item =>
            item.Contribution.ContributionId == contribution.ContributionId);
        if (npc is not null)
        {
            if (payload.TryGetProperty("configuration_flags_hex", out JsonElement configuration))
            {
                Assert.AreEqual(
                    uint.Parse(configuration.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    npc.ConfigurationFlags,
                    $"{scenarioId}: {locator} configuration flags");
            }

            if (payload.TryGetProperty("template_flags_hex", out JsonElement template))
            {
                Assert.AreEqual(
                    uint.Parse(template.GetString()!, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    npc.TemplateFlags,
                    $"{scenarioId}: {locator} template flags");
            }

            if (payload.TryGetProperty("AIDT", out JsonElement aiData))
            {
                Assert.IsNotNull(npc.AiData, $"{scenarioId}: {locator} AIDT");
                byte[] bytes = Convert.FromHexString(aiData.EnumerateArray().Single().GetString()!);
                Assert.AreEqual(bytes[0], npc.AiData.Aggression, $"{scenarioId}: {locator} aggression");
                Assert.AreEqual(bytes[1], npc.AiData.Confidence, $"{scenarioId}: {locator} confidence");
                Assert.AreEqual(bytes[2], npc.AiData.EnergyLevel, $"{scenarioId}: {locator} energy");
                Assert.AreEqual(bytes[3], npc.AiData.Responsibility, $"{scenarioId}: {locator} responsibility");
                Assert.AreEqual(bytes[4], npc.AiData.Mood, $"{scenarioId}: {locator} mood");
                Assert.AreEqual(bytes[5], npc.AiData.Assistance, $"{scenarioId}: {locator} assistance");
                Assert.AreEqual(BitConverter.ToUInt32(bytes, 8), npc.AiData.Warn, $"{scenarioId}: {locator} warn");
                Assert.AreEqual(BitConverter.ToUInt32(bytes, 12), npc.AiData.WarnOrAttack, $"{scenarioId}: {locator} warn-or-attack");
                Assert.AreEqual(BitConverter.ToUInt32(bytes, 16), npc.AiData.Attack, $"{scenarioId}: {locator} attack");
                Assert.AreEqual(bytes[7] != 0, npc.AiData.AggroRadiusBehavior, $"{scenarioId}: {locator} aggro-radius behavior");
            }
            else
            {
                Assert.IsNull(npc.AiData, $"{scenarioId}: {locator} lacks AIDT");
            }
        }

        BethesdaRaceFact? race = snapshot.RaceContributions.SingleOrDefault(item =>
            item.Contribution.ContributionId == contribution.ContributionId);
        if (race is not null && payload.TryGetProperty("face_gen_head", out JsonElement faceGenHead))
        {
            Assert.AreEqual(faceGenHead.GetBoolean(), race.FaceGenHead, $"{scenarioId}: {locator} FaceGenHead");
        }

        BethesdaPlacedReferenceFact? reference = snapshot.PlacedReferenceContributions.SingleOrDefault(item =>
            item.Contribution.ContributionId == contribution.ContributionId);
        if (reference is not null && payload.TryGetProperty("DATA", out JsonElement data))
        {
            Assert.IsNotNull(reference.Placement, $"{scenarioId}: {locator} DATA");
            byte[] bytes = Convert.FromHexString(data.EnumerateArray().Single().GetString()!);
            float[] expected = Enumerable.Range(0, 6)
                .Select(index => BitConverter.ToSingle(bytes, index * sizeof(float)))
                .ToArray();
            float[] actual =
            [
                reference.Placement.Position.X,
                reference.Placement.Position.Y,
                reference.Placement.Position.Z,
                reference.Placement.Rotation.X,
                reference.Placement.Rotation.Y,
                reference.Placement.Rotation.Z,
            ];
            CollectionAssert.AreEqual(expected, actual, $"{scenarioId}: {locator} placement");
        }
        else if (reference is not null)
        {
            Assert.IsNull(reference.Placement, $"{scenarioId}: {locator} lacks DATA");
        }
    }

    private static void AssertFieldPresence(
        BethesdaSemanticSnapshot snapshot,
        BethesdaRecordContribution contribution,
        JsonElement oracleRecord,
        string scenarioId,
        string locator)
    {
        string signature = oracleRecord.GetProperty("signature").GetString()!;
        Dictionary<string, int> expected = oracleRecord.GetProperty("subrecords")
            .EnumerateArray()
            .Select(subrecord => subrecord.GetProperty("signature").GetString()!)
            .Where(AllowedFields[signature].Contains)
            .GroupBy(field => field, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Dictionary<string, int> actual = snapshot.AllowlistedFields
            .Where(field => field.ContributionId == contribution.ContributionId)
            .ToDictionary(field => field.Field, field => field.Count, StringComparer.Ordinal);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray(), $"{scenarioId}: {locator} field set");
        foreach ((string field, int count) in expected)
        {
            Assert.AreEqual(count, actual[field], $"{scenarioId}: {locator} {field} count");
        }
    }

    private static void AssertPluginReceipts(
        BethesdaSemanticSnapshot snapshot,
        JsonElement root,
        JsonElement scenario,
        string scenarioId)
    {
        Dictionary<string, JsonElement> expectedFiles = scenario.GetProperty("plugin_paths")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToDictionary(
                path => Path.GetFileName(path),
                path => root.GetProperty("files").EnumerateArray().Single(file => file.GetProperty("path").GetString() == path),
                StringComparer.OrdinalIgnoreCase);
        foreach (BethesdaPluginReceipt receipt in snapshot.Plugins)
        {
            JsonElement file = expectedFiles.TryGetValue(receipt.PluginName, out JsonElement selected)
                ? selected
                : root.GetProperty("files").EnumerateArray().Single(candidate =>
                    candidate.GetProperty("path").GetString() == $"plugins/{receipt.PluginName}");
            Assert.AreEqual(file.GetProperty("byte_length").GetInt64(), receipt.ByteLength, $"{scenarioId}: {receipt.PluginName} length");
            Assert.AreEqual(file.GetProperty("sha256").GetString(), receipt.Sha256.Value, $"{scenarioId}: {receipt.PluginName} sha");
            CollectionAssert.AreEqual(
                file.GetProperty("tes4").GetProperty("masters").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                receipt.Masters.ToArray(),
                $"{scenarioId}: {receipt.PluginName} masters");
            Assert.AreEqual(
                file.GetProperty("tes4").GetProperty("esl_flag").GetBoolean()
                    ? BethesdaMasterStyle.Light
                    : BethesdaMasterStyle.Full,
                receipt.MasterStyle,
                $"{scenarioId}: {receipt.PluginName} style");
        }
    }

    private static JsonElement OracleRecord(JsonElement root, string locator)
    {
        string[] parts = locator.Split('#');
        int recordIndex = int.Parse(parts[1], CultureInfo.InvariantCulture) - 1;
        return root.GetProperty("files")
            .EnumerateArray()
            .Single(file => file.GetProperty("path").GetString() == parts[0])
            .GetProperty("records")
            .EnumerateArray()
            .Where(record => record.GetProperty("signature").GetString() != "TES4")
            .ElementAt(recordIndex);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0086Slice4TaxonomyProjectionsKeepAxesIndependent()
    {
        foreach (string fixture in new[] { "BETH-NPC-DEV", "BETH-REFR-DEV", "BETH-UNSUPPORTED-VAL" })
        {
            BethesdaSemanticSnapshot snapshot = new BethesdaSemanticExtractor().Extract(
                BethesdaSemanticTestSnapshot.Create(fixture)).Snapshot!;
            using JsonDocument oracle = TestRepository.ReadJson(
                "test-data", "evaluation", "m1-semantic", fixture,
                "oracle", "taxonomy-projections.json");
            using JsonDocument bindings = TestRepository.ReadJson(
                "test-data", "evaluation", "m1-semantic", fixture,
                "inputs", "taxonomy-subject-bindings.json");
            Dictionary<string, string> productionSubjects = bindings.RootElement.GetProperty("bindings")
                .EnumerateArray()
                .ToDictionary(
                    item => item.GetProperty("sealed_subject_id").GetString()!,
                    item => item.GetProperty("production_subject_participant_id").GetString()!,
                    StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(
                productionSubjects.Values.Order(StringComparer.Ordinal).ToArray(),
                snapshot.Taxonomy.Select(item => item.SubjectParticipantId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                $"{fixture}: exhaustive taxonomy subject closure drifted");
            foreach (JsonElement subject in oracle.RootElement.GetProperty("subjects").EnumerateArray())
            {
                JsonElement canonical = subject.GetProperty("canonical_value");
                string sealedSubjectId = subject.GetProperty("subject_id").GetString()!;
                string productionSubject = productionSubjects[sealedSubjectId];
                BethesdaTaxonomyProjection[] actual = snapshot.Taxonomy
                    .Where(item => item.SubjectParticipantId == productionSubject)
                    .ToArray();
                JsonElement[] expected = canonical.GetProperty("expected_assignments").EnumerateArray().ToArray();
                CollectionAssert.AreEquivalent(
                    expected.Select(TaxonomyTuple).ToArray(),
                    actual.Select(TaxonomyTuple).ToArray(),
                    $"{fixture}: {subject.GetProperty("subject_id").GetString()}; expected={string.Join(',', expected.Select(TaxonomyTuple))}; actual={string.Join(',', actual.Select(TaxonomyTuple))}");
                foreach (JsonElement assignment in expected)
                {
                    string tuple = TaxonomyTuple(assignment);
                    BethesdaTaxonomyProjection projection = actual.Single(item => TaxonomyTuple(item) == tuple);
                    Assert.AreEqual(assignment.GetProperty("reason").GetString(), projection.Reason, $"{fixture}: unaccepted oracle reason for {tuple}: {projection.Reason}");
                    Assert.IsNotEmpty(projection.EvidenceFields, $"{fixture}: {tuple} lacks evidence provenance");
                    Assert.IsTrue(projection.EvidenceFields.All(evidence =>
                        evidence.StartsWith("evidence:", StringComparison.Ordinal)
                        || evidence.StartsWith("provider:", StringComparison.Ordinal)));
                }

                foreach (JsonElement forbidden in canonical.GetProperty("forbidden_assignments").EnumerateArray())
                {
                    string axis = forbidden.GetProperty("axis").GetString()!;
                    string? code = forbidden.TryGetProperty("code", out JsonElement codeElement)
                        ? codeElement.GetString()
                        : null;
                    bool axisHasExpectedAssignment = expected.Any(item =>
                        item.GetProperty("axis").GetString() == axis);
                    Assert.IsFalse(actual.Any(item =>
                        item.Axis == axis
                        && (code is not null
                            ? item.Code == code
                            : !axisHasExpectedAssignment || item.Applicability == TaxonomyApplicability.Assigned)),
                        $"{fixture}: {productionSubject} violates forbidden {axis}/{code ?? "*"}");
                }

                Assert.IsTrue(actual.All(projection => projection.EvidenceFields.Count > 0));
            }

            Assert.IsTrue(snapshot.Taxonomy.All(item =>
                item.TaxonomyId == oracle.RootElement.GetProperty("taxonomy_id").GetString()
                && item.TaxonomyVersion.ToString() == oracle.RootElement.GetProperty("taxonomy_version").GetString()));
            Assert.IsTrue(snapshot.Taxonomy.Where(item => item.SubjectType == "provider-topology").All(item => item.Axis != "technical-modification-surface"));
            Assert.IsFalse(snapshot.Taxonomy.Any(item => item.SubjectType == "unsupported-record" && item.Code is
                "area.actors.ai-packages" or "area.actors.appearance-identity" or "area.world.placed-objects-activation"));
        }
    }

    private static string TaxonomyTuple(JsonElement item)
    {
        string? code = item.TryGetProperty("code", out JsonElement codeElement)
                       && codeElement.ValueKind != JsonValueKind.Null
            ? codeElement.GetString()
            : null;
        return string.Join('|',
            item.GetProperty("axis").GetString(),
            item.GetProperty("facet").GetString(),
            code ?? "null",
            item.GetProperty("applicability_state").GetString(),
            item.GetProperty("classification_role").GetString());
    }

    private static string TaxonomyTuple(BethesdaTaxonomyProjection item) => string.Join('|',
        item.Axis,
        item.Facet,
        item.Code ?? "null",
        item.Applicability switch
        {
            TaxonomyApplicability.NotApplicable => "not-applicable",
            _ => item.Applicability.ToString().ToLowerInvariant(),
        },
        item.Role.ToString().ToLowerInvariant());

    private static BethesdaSemanticRequest ScenarioRequest(string fixture, JsonElement scenario)
    {
        string fixtureRoot = TestRepository.PathFromRoot(
            "test-data", "evaluation", "m1-semantic", fixture, "inputs");
        using JsonDocument reader = TestRepository.ReadJson(
            "test-data", "evaluation", "m1-semantic", fixture,
            "oracle", "independent-reader-report.json");
        Dictionary<string, string[]> mastersByPath = reader.RootElement.GetProperty("files")
            .EnumerateArray()
            .ToDictionary(
                file => file.GetProperty("path").GetString()!,
                file => file.GetProperty("tes4").GetProperty("masters").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, string> selectedPaths = scenario.GetProperty("plugin_paths")
            .EnumerateArray()
            .ToDictionary(
                item => Path.GetFileName(item.GetString()!),
                item => item.GetString()!,
                StringComparer.OrdinalIgnoreCase);
        List<string> ordered = [];
        HashSet<string> visiting = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        foreach (string plugin in selectedPaths.Keys.ToArray())
        {
            Visit(plugin);
        }

        void Visit(string plugin)
        {
            if (visited.Contains(plugin))
            {
                return;
            }

            if (!visiting.Add(plugin))
            {
                throw new InvalidOperationException("The oracle scenario master graph contains a cycle.");
            }
            foreach (string master in mastersByPath[selectedPaths[plugin]])
            {
                selectedPaths.TryAdd(master, $"plugins/{master}");
                Visit(master);
            }
            visiting.Remove(plugin);
            visited.Add(plugin);
            ordered.Add(plugin);
        }

        (string Name, int Order, string Path, OpaqueId Entity)[] plugins = ordered
            .Select((name, order) =>
            {
                string path = Path.Combine([fixtureRoot, .. selectedPaths[name].Split('/')]);
                return (name, order, Path.GetFullPath(path), new OpaqueId($"oracle-provider-{order:D3}"));
            })
            .ToArray();
        return BethesdaSemanticTestSnapshot.Create(plugins);
    }

    private static BethesdaRecordContribution? FindContribution(
        BethesdaSemanticSnapshot snapshot,
        string sourcePlugin,
        string formKey)
    {
        return snapshot.NpcContributions.Select(item => item.Contribution)
            .Concat(snapshot.RaceContributions.Select(item => item.Contribution))
            .Concat(snapshot.PlacedReferenceContributions.Select(item => item.Contribution))
            .SingleOrDefault(item =>
                item.Identity.FormKey == formKey
                && item.SourcePlugin == sourcePlugin);
    }

    private static string OracleLinkKey(JsonElement link, HashSet<string> independentlyResolvedParticipants)
    {
        string state = link.GetProperty("resolution_state").GetString()!;
        string target = link.GetProperty("form_key").ValueKind == JsonValueKind.Null
            ? "null"
            : link.GetProperty("form_key").GetString()!;
        string component = link.GetProperty("component").ValueKind == JsonValueKind.Null
            ? ""
            : link.GetProperty("component").GetString()!;
        if (state == "unresolved" && target != "null" && independentlyResolvedParticipants.Contains(target))
        {
            state = "resolved";
        }
        return string.Join('|', link.GetProperty("field").GetString(), component, link.GetProperty("occurrence").GetInt32(), target, state);
    }

    private static string ProductLinkKey(BethesdaLinkFact link) =>
        string.Join(
            '|',
            link.Field,
            link.Component ?? string.Empty,
            link.Ordinal,
            link.TargetFormKey ?? "null",
            link.State.ToString().ToLowerInvariant());

    private static HashSet<string> IndependentlyResolvedParticipants(
        JsonElement oracle,
        BethesdaSemanticRequest request)
    {
        HashSet<string> selected = request.AcceptedSnapshot.Snapshot!.Plugins
            .Where(plugin => plugin.Enabled)
            .Select(plugin => plugin.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return oracle.GetProperty("files").EnumerateArray()
            .Where(file => selected.Contains(Path.GetFileName(file.GetProperty("path").GetString()!)))
            .SelectMany(file => file.GetProperty("records").EnumerateArray())
            .Where(record => ParticipantRecordFamilies.Contains(record.GetProperty("signature").GetString()!))
            .Where(record => record.TryGetProperty("identity", out JsonElement identity)
                && identity.GetProperty("resolution_state").GetString() == "resolved"
                && identity.GetProperty("form_key").ValueKind == JsonValueKind.String)
            .Select(record => record.GetProperty("identity").GetProperty("form_key").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

}
