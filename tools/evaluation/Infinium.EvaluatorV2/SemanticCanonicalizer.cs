using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infinium.EvaluatorV2;

/// <summary>
/// Projects only the explicitly accepted Slice 4 semantic surface. This class
/// must never recursively flatten the complete product result.
/// </summary>
internal static partial class SemanticCanonicalizer
{
    internal static IReadOnlyList<SemanticFact> Project(JsonElement result)
    {
        FactBuilder facts = new();
        if (result.TryGetProperty("failures", out JsonElement failures))
        {
            foreach (JsonElement failure in failures.EnumerateArray()
                         .OrderBy(item => Text(item, "code"), StringComparer.Ordinal))
            {
                string code = Text(failure, "code");
                facts.String($"failures/{Segment(code)}/code", "failure", code);
            }
        }

        if (!result.TryGetProperty("snapshot", out JsonElement snapshot)
            || snapshot.ValueKind == JsonValueKind.Null)
        {
            ProjectGaps(result.GetProperty("gaps"), "gaps", facts);
            return facts.Build();
        }

        ProjectPlugins(snapshot.GetProperty("plugins"), facts);
        ProjectOverrideChains(snapshot.GetProperty("override_chains"), facts);
        ProjectContributionFacts(snapshot.GetProperty("npc_contributions"), "npc_contributions", "npc", ProjectNpc, facts);
        ProjectContributionFacts(snapshot.GetProperty("race_contributions"), "race_contributions", "race", ProjectRace, facts);
        ProjectContributionFacts(snapshot.GetProperty("placed_reference_contributions"), "placed_reference_contributions", "reference", ProjectReference, facts);
        ProjectFields(snapshot.GetProperty("allowlisted_fields"), facts);
        ProjectResolvedParticipants(snapshot.GetProperty("resolved_participants"), facts);
        ProjectResolvedFacts(snapshot.GetProperty("npcs"), "npcs", "npc", ProjectNpc, facts);
        ProjectResolvedFacts(snapshot.GetProperty("races"), "races", "race", ProjectRace, facts);
        ProjectResolvedFacts(snapshot.GetProperty("placed_references"), "placed_references", "reference", ProjectReference, facts);
        ProjectLinks(snapshot.GetProperty("links"), "links", facts);
        ProjectFaceGen(snapshot.GetProperty("face_gen"), facts);
        ProjectTaxonomy(snapshot.GetProperty("taxonomy"), facts);
        ProjectCoverage(snapshot.GetProperty("coverage"), facts);
        ProjectGaps(snapshot.GetProperty("gaps"), "gaps", facts);
        ProjectGaps(result.GetProperty("gaps"), "result_gaps", facts);
        return facts.Build();
    }

    internal static string CanonicalFormKey(string value)
    {
        Match match = FormKeyPattern().Match(value);
        if (!match.Success)
        {
            throw new CandidateOutputException($"'{value}' is not an ID-first Slice 4 FormKey.");
        }

        return $"{match.Groups[1].Value.ToLowerInvariant()}:{match.Groups[2].Value.ToLowerInvariant()}";
    }

    internal static string CanonicalIdentity(string value) =>
        EmbeddedFormKeyPattern().Replace(value, match => CanonicalFormKey(match.Value)).ToLowerInvariant();

    private static void ProjectPlugins(JsonElement plugins, FactBuilder facts)
    {
        int index = 0;
        foreach (JsonElement plugin in plugins.EnumerateArray())
        {
            string root = $"plugins/{index:D4}";
            facts.String($"{root}/plugin_name", "plugin", Text(plugin, "plugin_name").ToLowerInvariant());
            facts.Integer($"{root}/load_order", "plugin", plugin.GetProperty("load_order").GetInt64());
            facts.String($"{root}/local_installed_entity_id", "plugin", Text(plugin, "local_installed_entity_id").ToLowerInvariant());
            facts.String($"{root}/master_style", "plugin", Text(plugin, "master_style"));
            ProjectStringSequence(plugin.GetProperty("masters"), $"{root}/masters", "plugin", value => value.ToLowerInvariant(), facts);
            index++;
        }
    }

    private static void ProjectOverrideChains(JsonElement chains, FactBuilder facts)
    {
        foreach (JsonProperty entry in chains.EnumerateObject().OrderBy(item => CanonicalFormKey(item.Name), StringComparer.Ordinal))
        {
            JsonElement chain = entry.Value;
            string identity = CanonicalFormKey(Text(chain.GetProperty("identity"), "form_key"));
            string root = $"override_chains/{Segment(identity)}";
            ProjectRecordIdentity(chain.GetProperty("identity"), $"{root}/identity", facts);
            int index = 0;
            foreach (JsonElement contribution in chain.GetProperty("contributions").EnumerateArray())
            {
                ProjectContribution(contribution, $"{root}/contributions/{index:D4}", facts);
                index++;
            }

            facts.String($"{root}/winner_contribution_id", "winner", CanonicalIdentity(Text(chain.GetProperty("winner"), "contribution_id")));
        }
    }

    private static void ProjectContributionFacts(
        JsonElement source,
        string collection,
        string type,
        Action<JsonElement, string, FactBuilder> projector,
        FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(
                     item => CanonicalIdentity(Text(item.GetProperty("contribution"), "contribution_id")),
                     StringComparer.Ordinal))
        {
            string id = CanonicalIdentity(Text(item.GetProperty("contribution"), "contribution_id"));
            projector(item, $"{collection}/{Segment(id)}", facts);
            facts.String($"{collection}/{Segment(id)}/kind", type, type);
        }
    }

    private static void ProjectResolvedFacts(
        JsonElement source,
        string collection,
        string type,
        Action<JsonElement, string, FactBuilder> projector,
        FactBuilder facts)
    {
        foreach (JsonProperty item in source.EnumerateObject().OrderBy(item => CanonicalFormKey(item.Name), StringComparer.Ordinal))
        {
            string id = CanonicalFormKey(item.Name);
            projector(item.Value, $"{collection}/{Segment(id)}", facts);
            facts.String($"{collection}/{Segment(id)}/kind", type, type);
        }
    }

    private static void ProjectNpc(JsonElement npc, string root, FactBuilder facts)
    {
        ProjectContribution(npc.GetProperty("contribution"), $"{root}/contribution", facts);
        facts.Integer($"{root}/configuration_flags", "npc", npc.GetProperty("configuration_flags").GetInt64());
        facts.Integer($"{root}/template_flags", "npc", npc.GetProperty("template_flags").GetInt64());
        facts.Boolean($"{root}/uses_template", "npc", npc.GetProperty("uses_template").GetBoolean());
        facts.Boolean($"{root}/templates_traits", "npc", npc.GetProperty("templates_traits").GetBoolean());
        ProjectOptionalLink(npc.GetProperty("template"), $"{root}/template", facts);
        ProjectOptionalLink(npc.GetProperty("race"), $"{root}/race", facts);
        ProjectOptionalLink(npc.GetProperty("hair_color"), $"{root}/hair_color", facts);
        if (npc.GetProperty("ai_data") is { ValueKind: not JsonValueKind.Null } ai)
        {
            foreach (string name in new[] { "aggression", "confidence", "energy_level", "responsibility", "mood", "assistance", "warn", "warn_or_attack", "attack" })
            {
                facts.Integer($"{root}/ai_data/{name}", "npc", ai.GetProperty(name).GetInt64());
            }

            facts.Boolean($"{root}/ai_data/aggro_radius_behavior", "npc", ai.GetProperty("aggro_radius_behavior").GetBoolean());
        }

        ProjectLinks(npc.GetProperty("packages"), $"{root}/packages", facts);
        ProjectLinks(npc.GetProperty("head_parts"), $"{root}/head_parts", facts);
    }

    private static void ProjectRace(JsonElement race, string root, FactBuilder facts)
    {
        ProjectContribution(race.GetProperty("contribution"), $"{root}/contribution", facts);
        facts.Boolean($"{root}/face_gen_head", "race", race.GetProperty("face_gen_head").GetBoolean());
    }

    private static void ProjectReference(JsonElement reference, string root, FactBuilder facts)
    {
        ProjectContribution(reference.GetProperty("contribution"), $"{root}/contribution", facts);
        ProjectOptionalLink(reference.GetProperty("base"), $"{root}/base", facts);
        ProjectLinks(reference.GetProperty("linked_references"), $"{root}/linked_references", facts);
        ProjectOptionalLink(reference.GetProperty("location_reference"), $"{root}/location_reference", facts);
        ProjectOptionalLink(reference.GetProperty("owner"), $"{root}/owner", facts, "ownership");
        JsonElement placement = reference.GetProperty("placement");
        if (placement.ValueKind != JsonValueKind.Null)
        {
            foreach (string vector in new[] { "position", "rotation" })
            {
                foreach (string axis in new[] { "x", "y", "z" })
                {
                    facts.Number($"{root}/placement/{vector}/{axis}", "placement", placement.GetProperty(vector).GetProperty(axis).GetDouble());
                }
            }
        }
    }

    private static void ProjectContribution(JsonElement contribution, string root, FactBuilder facts)
    {
        facts.String($"{root}/contribution_id", "contribution", CanonicalIdentity(Text(contribution, "contribution_id")));
        ProjectRecordIdentity(contribution.GetProperty("identity"), $"{root}/identity", facts);
        facts.String($"{root}/source_plugin", "contribution", Text(contribution, "source_plugin").ToLowerInvariant());
        facts.Integer($"{root}/load_order", "contribution", contribution.GetProperty("load_order").GetInt64());
        facts.Boolean($"{root}/deleted", "contribution", contribution.GetProperty("deleted").GetBoolean());
        facts.Boolean($"{root}/compressed", "contribution", contribution.GetProperty("compressed").GetBoolean());
        facts.Integer($"{root}/raw_flags", "contribution", contribution.GetProperty("raw_flags").GetInt64());
    }

    private static void ProjectRecordIdentity(JsonElement identity, string root, FactBuilder facts)
    {
        facts.String($"{root}/participant_id", "record_identity", CanonicalIdentity(Text(identity, "participant_id")));
        facts.String($"{root}/signature", "record_identity", Text(identity, "signature"));
        facts.String($"{root}/form_key", "form_key", CanonicalFormKey(Text(identity, "form_key")));
        facts.String($"{root}/origin_plugin", "record_identity", Text(identity, "origin_plugin").ToLowerInvariant());
        facts.Integer($"{root}/origin_local_id", "record_identity", identity.GetProperty("origin_local_id").GetInt64());
    }

    private static void ProjectLinks(JsonElement links, string root, FactBuilder facts)
    {
        foreach (JsonElement link in links.EnumerateArray().OrderBy(LinkIdentity, StringComparer.Ordinal))
        {
            ProjectLink(link, $"{root}/{Segment(LinkIdentity(link))}", facts);
        }
    }

    private static void ProjectOptionalLink(JsonElement link, string root, FactBuilder facts, string factType = "link")
    {
        if (link.ValueKind == JsonValueKind.Null)
        {
            facts.Null($"{root}/state", factType);
            return;
        }

        ProjectLink(link, root, facts, factType);
    }

    private static void ProjectLink(JsonElement link, string root, FactBuilder facts, string factType = "link")
    {
        facts.String($"{root}/source_participant_id", factType, CanonicalIdentity(Text(link, "source_participant_id")));
        facts.String($"{root}/source_contribution_id", factType, CanonicalIdentity(Text(link, "source_contribution_id")));
        facts.String($"{root}/field", factType, Text(link, "field"));
        StringOrNull(link.GetProperty("component"), $"{root}/component", factType, value => value, facts);
        facts.Integer($"{root}/ordinal", factType, link.GetProperty("ordinal").GetInt64());
        StringOrNull(link.GetProperty("target_form_key"), $"{root}/target_form_key", "form_key", CanonicalFormKey, facts);
        facts.String($"{root}/state", factType, Text(link, "state"));
        StringOrNull(link.GetProperty("target_participant_id"), $"{root}/target_participant_id", factType, CanonicalIdentity, facts);
    }

    private static string LinkIdentity(JsonElement link) => string.Join(':',
        CanonicalIdentity(Text(link, "source_contribution_id")),
        Text(link, "field").ToLowerInvariant(),
        link.GetProperty("component").ValueKind == JsonValueKind.Null ? "value" : Text(link, "component").ToLowerInvariant(),
        link.GetProperty("ordinal").GetInt32().ToString("D4", System.Globalization.CultureInfo.InvariantCulture));

    private static void ProjectFields(JsonElement fields, FactBuilder facts)
    {
        foreach (JsonElement field in fields.EnumerateArray().OrderBy(
                     item => $"{CanonicalIdentity(Text(item, "contribution_id"))}:{Text(item, "field")}", StringComparer.Ordinal))
        {
            string identity = $"{CanonicalIdentity(Text(field, "contribution_id"))}:{Text(field, "field").ToLowerInvariant()}";
            string root = $"allowlisted_fields/{Segment(identity)}";
            facts.String($"{root}/contribution_id", "field", CanonicalIdentity(Text(field, "contribution_id")));
            facts.String($"{root}/field", "field", Text(field, "field"));
            facts.Integer($"{root}/count", "field", field.GetProperty("count").GetInt64());
        }
    }

    private static void ProjectResolvedParticipants(JsonElement participants, FactBuilder facts)
    {
        foreach (JsonProperty entry in participants.EnumerateObject().OrderBy(item => CanonicalFormKey(item.Name), StringComparer.Ordinal))
        {
            string formKey = CanonicalFormKey(entry.Name);
            string root = $"resolved_participants/{Segment(formKey)}";
            facts.String($"{root}/participant_id", "record_identity", CanonicalIdentity(Text(entry.Value, "participant_id")));
            facts.String($"{root}/form_key", "form_key", CanonicalFormKey(Text(entry.Value, "form_key")));
        }
    }

    private static void ProjectFaceGen(JsonElement source, FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(item => CanonicalIdentity(Text(item, "npc_participant_id")), StringComparer.Ordinal))
        {
            string id = CanonicalIdentity(Text(item, "npc_participant_id"));
            string root = $"face_gen/{Segment(id)}";
            facts.String($"{root}/npc_participant_id", "face_gen", id);
            facts.String($"{root}/applicability", "face_gen", Text(item, "applicability"));
            facts.String($"{root}/origin_plugin", "face_gen", Text(item, "origin_plugin").ToLowerInvariant());
            facts.Integer($"{root}/origin_local_id", "face_gen", item.GetProperty("origin_local_id").GetInt64());
            ProjectLooseAsset(item.GetProperty("mesh"), $"{root}/mesh", facts);
            ProjectLooseAsset(item.GetProperty("tint"), $"{root}/tint", facts);
        }
    }

    private static void ProjectLooseAsset(JsonElement asset, string root, FactBuilder facts)
    {
        facts.String($"{root}/normalized_relative_path", "face_gen", Text(asset, "normalized_relative_path").Replace('\\', '/').ToLowerInvariant());
        ProjectStringSequence(asset.GetProperty("provider_participant_ids"), $"{root}/provider_participant_ids", "face_gen", value => value.ToLowerInvariant(), facts);
        StringOrNull(asset.GetProperty("winner_participant_id"), $"{root}/winner_participant_id", "face_gen", value => value.ToLowerInvariant(), facts);
        facts.Boolean($"{root}/present", "face_gen", asset.GetProperty("present").GetBoolean());
        facts.Boolean($"{root}/exact_absence_known", "face_gen", asset.GetProperty("exact_absence_known").GetBoolean());
    }

    private static void ProjectTaxonomy(JsonElement source, FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(TaxonomyIdentity, StringComparer.Ordinal))
        {
            string id = CanonicalIdentity(Text(item, "assignment_id"));
            string root = $"taxonomy/{Segment(TaxonomyIdentity(item))}";
            foreach (string name in new[] { "assignment_id", "taxonomy_id", "subject_participant_id", "subject_type", "axis", "facet", "applicability", "role", "analyzer_or_adjudicator_id" })
            {
                string value = Text(item, name);
                facts.String($"{root}/{name}", "taxonomy", name.Contains("participant", StringComparison.Ordinal) || name == "assignment_id" ? CanonicalIdentity(value) : value);
            }

            JsonElement version = item.GetProperty("taxonomy_version");
            facts.Integer($"{root}/taxonomy_version/major", "taxonomy", version.GetProperty("major").GetInt64());
            facts.Integer($"{root}/taxonomy_version/minor", "taxonomy", version.GetProperty("minor").GetInt64());
            facts.Integer($"{root}/taxonomy_version/patch", "taxonomy", version.GetProperty("patch").GetInt64());
            StringOrNull(item.GetProperty("code"), $"{root}/code", "taxonomy", value => value, facts);
            foreach (string evidence in item.GetProperty("evidence_fields").EnumerateArray().Select(value => CanonicalIdentity(value.GetString()!)).Order(StringComparer.Ordinal))
            {
                facts.String($"{root}/evidence_fields/{Segment(evidence)}", "taxonomy", evidence);
            }
        }
    }

    private static string TaxonomyIdentity(JsonElement item) => string.Join('|',
        CanonicalIdentity(Text(item, "assignment_id")),
        CanonicalIdentity(Text(item, "subject_participant_id")),
        Text(item, "axis"),
        Text(item, "facet"),
        item.GetProperty("code").ValueKind == JsonValueKind.Null ? "null" : Text(item, "code"),
        Text(item, "applicability"),
        Text(item, "role"));

    private static void ProjectCoverage(JsonElement source, FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(item => Text(item, "population"), StringComparer.Ordinal))
        {
            string population = Text(item, "population");
            string root = $"coverage/{Segment(population)}";
            facts.String($"{root}/population", "coverage", population);
            facts.String($"{root}/denominator_label", "coverage", Text(item, "denominator_label"));
            facts.Integer($"{root}/denominator", "coverage", item.GetProperty("denominator").GetInt64());
            facts.Integer($"{root}/completed", "coverage", item.GetProperty("completed").GetInt64());
            facts.String($"{root}/state", "coverage", Text(item, "state"));
            foreach (string gap in item.GetProperty("gap_ids").EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal))
            {
                facts.String($"{root}/gap_ids/{Segment(gap)}", "coverage", gap);
            }
        }
    }

    private static void ProjectGaps(JsonElement source, string collection, FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(item => Text(item, "gap_id"), StringComparer.Ordinal))
        {
            string id = Text(item, "gap_id");
            string root = $"{collection}/{Segment(id)}";
            facts.String($"{root}/gap_id", "gap", id);
            facts.String($"{root}/population", "gap", Text(item, "population"));
            facts.Integer($"{root}/denominator", "gap", item.GetProperty("denominator").GetInt64());
            facts.String($"{root}/missing_capability", "gap", Text(item, "missing_capability"));
        }
    }

    private static void ProjectStringSequence(JsonElement array, string root, string type, Func<string, string> normalize, FactBuilder facts)
    {
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            facts.String($"{root}/{index:D4}", type, normalize(item.GetString()!));
            index++;
        }
    }

    private static void StringOrNull(JsonElement value, string id, string type, Func<string, string> normalize, FactBuilder facts)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            facts.Null(id, type);
        }
        else
        {
            facts.String(id, type, normalize(value.GetString()!));
        }
    }

    private static string Text(JsonElement value, string property)
    {
        JsonElement field = value.GetProperty(property);
        if (field.ValueKind == JsonValueKind.Object && field.TryGetProperty("value", out JsonElement wrapped))
        {
            field = wrapped;
        }

        return field.GetString() ?? throw new CandidateOutputException($"Projection field '{property}' is null.");
    }

    private static string Segment(string value) => Uri.EscapeDataString(value);

    [GeneratedRegex(@"^([0-9a-fA-F]{8}):([^:/\\]+\.(?:esm|esp|esl))$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FormKeyPattern();

    [GeneratedRegex(@"[0-9a-fA-F]{8}:[^:/\\\s]+\.(?:esm|esp|esl)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EmbeddedFormKeyPattern();

    private sealed class FactBuilder
    {
        private readonly Dictionary<string, SemanticFact> facts = new(StringComparer.Ordinal);

        internal void String(string id, string type, string value) => Add(id, type, "string", EvaluatorProtocol.Primitive(value));
        internal void Integer(string id, string type, long value) => Add(id, type, "integer", EvaluatorProtocol.Primitive(value));
        internal void Number(string id, string type, double value) => Add(id, type, "number", EvaluatorProtocol.Primitive(value));
        internal void Boolean(string id, string type, bool value) => Add(id, type, "boolean", EvaluatorProtocol.Primitive(value));
        internal void Null(string id, string type) => Add(id, type, "null", EvaluatorProtocol.Null());

        internal SemanticFact[] Build() => facts.Values.OrderBy(item => item.FactId, StringComparer.Ordinal).ToArray();

        private void Add(string id, string type, string valueType, JsonElement value)
        {
            if (!facts.TryAdd(id, new SemanticFact(id, type, valueType, value)))
            {
                throw new CandidateOutputException($"Projection produced duplicate fact identity '{id}'.");
            }
        }
    }
}
