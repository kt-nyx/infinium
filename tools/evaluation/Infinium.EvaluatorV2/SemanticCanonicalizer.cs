using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infinium.EvaluatorV2;

/// <summary>
/// Projects only the explicitly accepted Slice 4 semantic surface. This class
/// must never recursively flatten the complete product result.
/// </summary>
internal static partial class SemanticCanonicalizer
{
    internal static IReadOnlyList<string> IncludedFactFamilies { get; } =
    [
        "result",
        "plugins",
        "override_chains",
        "npc_contributions",
        "race_contributions",
        "placed_reference_contributions",
        "allowlisted_fields",
        "npcs",
        "races",
        "placed_references",
        "face_gen",
        "taxonomy",
        "coverage",
        "gaps",
        "result_gaps",
    ];

    internal static IReadOnlyList<SemanticFact> Project(JsonElement result)
    {
        FactBuilder facts = new();
        bool snapshotPresent = result.TryGetProperty("snapshot", out JsonElement snapshot)
            && snapshot.ValueKind != JsonValueKind.Null;
        bool failurePresent = result.TryGetProperty("failures", out JsonElement failures)
            && failures.ValueKind == JsonValueKind.Array
            && failures.GetArrayLength() > 0;
        facts.Boolean("result/snapshot_present", "state", snapshotPresent);
        facts.Boolean("result/failure_present", "state", failurePresent);

        if (!snapshotPresent)
        {
            ProjectGaps(result.GetProperty("gaps"), "result_gaps", facts);
            return facts.Build();
        }

        ProjectionContext context = ProjectionContext.Create(snapshot);
        ProjectPlugins(snapshot.GetProperty("plugins"), facts);
        ProjectOverrideChains(snapshot.GetProperty("override_chains"), context, facts);
        ProjectContributionFacts(snapshot.GetProperty("npc_contributions"), "npc_contributions", "npc", ProjectNpc, context, facts);
        ProjectContributionFacts(snapshot.GetProperty("race_contributions"), "race_contributions", "race", ProjectRace, context, facts);
        ProjectContributionFacts(snapshot.GetProperty("placed_reference_contributions"), "placed_reference_contributions", "reference", ProjectReference, context, facts);
        ProjectFields(snapshot.GetProperty("allowlisted_fields"), context, facts);
        ProjectResolvedFacts(snapshot.GetProperty("npcs"), "npcs", "npc", ProjectNpc, facts);
        ProjectResolvedFacts(snapshot.GetProperty("races"), "races", "race", ProjectRace, facts);
        ProjectResolvedFacts(snapshot.GetProperty("placed_references"), "placed_references", "reference", ProjectReference, facts);
        ProjectFaceGen(snapshot.GetProperty("face_gen"), context, facts);
        ProjectTaxonomy(snapshot.GetProperty("taxonomy"), context, facts);
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
            facts.String($"{root}/provider_id", "plugin", Text(plugin, "local_installed_entity_id").ToLowerInvariant());
            facts.String($"{root}/master_style", "plugin", Text(plugin, "master_style"));
            ProjectStringSequence(plugin.GetProperty("masters"), $"{root}/masters", "plugin", value => value.ToLowerInvariant(), facts);
            index++;
        }
    }

    private static void ProjectOverrideChains(JsonElement chains, ProjectionContext context, FactBuilder facts)
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

            string winnerId = Text(chain.GetProperty("winner"), "contribution_id");
            ProjectWinner(context.Contribution(winnerId), $"{root}/winner", facts);
        }
    }

    private static void ProjectContributionFacts(
        JsonElement source,
        string collection,
        string type,
        Action<JsonElement, string, FactBuilder> projector,
        ProjectionContext context,
        FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(
                     item => context.ContributionIdentity(Text(item.GetProperty("contribution"), "contribution_id")),
                     StringComparer.Ordinal))
        {
            string id = context.ContributionIdentity(Text(item.GetProperty("contribution"), "contribution_id"));
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
        facts.Boolean($"{root}/ai_data_present", "npc", npc.GetProperty("ai_data").ValueKind != JsonValueKind.Null);

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
        ProjectRecordIdentity(contribution.GetProperty("identity"), $"{root}/identity", facts);
        facts.String($"{root}/source_plugin", "contribution", Text(contribution, "source_plugin").ToLowerInvariant());
        facts.Integer($"{root}/load_order", "contribution", contribution.GetProperty("load_order").GetInt64());
        facts.Boolean($"{root}/deleted", "contribution", contribution.GetProperty("deleted").GetBoolean());
        facts.Boolean($"{root}/compressed", "contribution", contribution.GetProperty("compressed").GetBoolean());
        facts.Integer($"{root}/raw_flags", "contribution", contribution.GetProperty("raw_flags").GetInt64());
    }

    private static void ProjectRecordIdentity(JsonElement identity, string root, FactBuilder facts)
    {
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
        facts.String($"{root}/field", factType, Text(link, "field"));
        StringOrNull(link.GetProperty("component"), $"{root}/component", factType, value => value, facts);
        facts.Integer($"{root}/ordinal", factType, link.GetProperty("ordinal").GetInt64());
        StringOrNull(link.GetProperty("target_form_key"), $"{root}/target_form_key", "form_key", CanonicalFormKey, facts);
        facts.String($"{root}/state", factType, Text(link, "state"));
    }

    private static string LinkIdentity(JsonElement link) => string.Join(':',
        Text(link, "field").ToLowerInvariant(),
        link.GetProperty("component").ValueKind == JsonValueKind.Null ? "value" : Text(link, "component").ToLowerInvariant(),
        link.GetProperty("ordinal").GetInt32().ToString("D4", System.Globalization.CultureInfo.InvariantCulture));

    private static void ProjectFields(JsonElement fields, ProjectionContext context, FactBuilder facts)
    {
        foreach (JsonElement field in fields.EnumerateArray().OrderBy(
                     item => $"{context.ContributionIdentity(Text(item, "contribution_id"))}:{Text(item, "field")}", StringComparer.Ordinal))
        {
            string identity = $"{context.ContributionIdentity(Text(field, "contribution_id"))}:{Text(field, "field").ToLowerInvariant()}";
            string root = $"allowlisted_fields/{Segment(identity)}";
            facts.String($"{root}/field", "field", Text(field, "field"));
            facts.Integer($"{root}/count", "field", field.GetProperty("count").GetInt64());
        }
    }

    private static void ProjectFaceGen(JsonElement source, ProjectionContext context, FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(item => context.ParticipantFormKey(Text(item, "npc_participant_id")), StringComparer.Ordinal))
        {
            string id = context.ParticipantFormKey(Text(item, "npc_participant_id"));
            string root = $"face_gen/{Segment(id)}";
            facts.String($"{root}/npc_form_key", "face_gen", id);
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
        ProjectStringSequence(asset.GetProperty("provider_participant_ids"), $"{root}/provider_ids", "face_gen", value => value.ToLowerInvariant(), facts);
        StringOrNull(asset.GetProperty("winner_participant_id"), $"{root}/winner_provider_id", "face_gen", value => value.ToLowerInvariant(), facts);
        facts.Boolean($"{root}/present", "face_gen", asset.GetProperty("present").GetBoolean());
        facts.Boolean($"{root}/exact_absence_known", "face_gen", asset.GetProperty("exact_absence_known").GetBoolean());
    }

    private static void ProjectTaxonomy(JsonElement source, ProjectionContext context, FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(item => TaxonomyIdentity(item, context), StringComparer.Ordinal))
        {
            string subject = context.TaxonomySubject(item);
            string subjectType = Text(item, "subject_type");
            string axis = Text(item, "axis");
            string facet = Text(item, "facet");
            string code = item.GetProperty("code").ValueKind == JsonValueKind.Null ? "null" : Text(item, "code");
            string applicability = Text(item, "applicability");
            string role = Text(item, "role");
            string root = $"taxonomy/{Segment(subject)}/{Segment(subjectType)}/{Segment(axis)}/{Segment(facet)}/{Segment(code)}/{Segment(applicability)}/{Segment(role)}";
            facts.String($"{root}/taxonomy_id", "taxonomy", Text(item, "taxonomy_id"));
            facts.String($"{root}/canonical_subject", "taxonomy", subject);
            facts.String($"{root}/subject_type", "taxonomy", subjectType);
            facts.String($"{root}/axis", "taxonomy", axis);
            facts.String($"{root}/facet", "taxonomy", facet);
            facts.String($"{root}/applicability", "taxonomy", applicability);
            facts.String($"{root}/role", "taxonomy", role);

            JsonElement version = item.GetProperty("taxonomy_version");
            facts.Integer($"{root}/taxonomy_version/major", "taxonomy", version.GetProperty("major").GetInt64());
            facts.Integer($"{root}/taxonomy_version/minor", "taxonomy", version.GetProperty("minor").GetInt64());
            facts.Integer($"{root}/taxonomy_version/patch", "taxonomy", version.GetProperty("patch").GetInt64());
            StringOrNull(item.GetProperty("code"), $"{root}/code", "taxonomy", value => value, facts);
        }
    }

    private static string TaxonomyIdentity(JsonElement item, ProjectionContext context) => string.Join('|',
        context.TaxonomySubject(item),
        Text(item, "subject_type"),
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
            facts.Integer($"{root}/denominator", "coverage", item.GetProperty("denominator").GetInt64());
            facts.Integer($"{root}/completed", "coverage", item.GetProperty("completed").GetInt64());
            facts.String($"{root}/state", "coverage", Text(item, "state"));
        }
    }

    private static void ProjectGaps(JsonElement source, string collection, FactBuilder facts)
    {
        foreach (JsonElement item in source.EnumerateArray().OrderBy(
                     item => $"{Text(item, "population")}|{Text(item, "missing_capability")}",
                     StringComparer.Ordinal))
        {
            string population = Text(item, "population");
            string missingCapability = Text(item, "missing_capability");
            string root = $"{collection}/{Segment(population)}/{Segment(missingCapability)}";
            facts.String($"{root}/population", "gap", population);
            facts.Integer($"{root}/denominator", "gap", item.GetProperty("denominator").GetInt64());
            facts.String($"{root}/missing_capability", "gap", missingCapability);
        }
    }

    private static void ProjectWinner(JsonElement contribution, string root, FactBuilder facts)
    {
        facts.String($"{root}/source_plugin", "winner", Text(contribution, "source_plugin").ToLowerInvariant());
        facts.Integer($"{root}/load_order", "winner", contribution.GetProperty("load_order").GetInt64());
        facts.String($"{root}/form_key", "winner", CanonicalFormKey(Text(contribution.GetProperty("identity"), "form_key")));
        facts.Boolean($"{root}/deleted", "winner", contribution.GetProperty("deleted").GetBoolean());
        facts.Boolean($"{root}/compressed", "winner", contribution.GetProperty("compressed").GetBoolean());
        facts.Integer($"{root}/raw_flags", "winner", contribution.GetProperty("raw_flags").GetInt64());
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

    [GeneratedRegex(@"^unsupported-record:(?<plugin>[^:]+):(?<signature>[^:]+):(?<formkey>[0-9a-fA-F]{8}:[^:]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UnsupportedSubjectPattern();

    private sealed class ProjectionContext
    {
        private readonly Dictionary<string, JsonElement> contributions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> participantFormKeys = new(StringComparer.Ordinal);
        private readonly string providerTopologySubject;

        private ProjectionContext(JsonElement snapshot)
        {
            foreach (JsonProperty chain in snapshot.GetProperty("override_chains").EnumerateObject())
            {
                foreach (JsonElement contribution in chain.Value.GetProperty("contributions").EnumerateArray())
                {
                    AddContribution(contribution);
                }
            }

            foreach (string collection in new[] { "npc_contributions", "race_contributions", "placed_reference_contributions" })
            {
                foreach (JsonElement item in snapshot.GetProperty(collection).EnumerateArray())
                {
                    AddContribution(item.GetProperty("contribution"));
                }
            }

            foreach (JsonProperty participant in snapshot.GetProperty("resolved_participants").EnumerateObject())
            {
                string participantId = Text(participant.Value, "participant_id");
                string formKey = CanonicalFormKey(Text(participant.Value, "form_key"));
                AddParticipant(participantId, formKey);
            }

            string topology = string.Join('|', snapshot.GetProperty("plugins").EnumerateArray().Select(plugin =>
                string.Join(':',
                    plugin.GetProperty("load_order").GetInt32().ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                    Text(plugin, "plugin_name").ToLowerInvariant(),
                    Text(plugin, "local_installed_entity_id").ToLowerInvariant())));
            providerTopologySubject = $"provider-topology|{topology}";
        }

        internal static ProjectionContext Create(JsonElement snapshot) => new(snapshot);

        internal JsonElement Contribution(string productId) =>
            contributions.TryGetValue(productId, out JsonElement contribution)
                ? contribution
                : throw new CandidateOutputException($"Projection could not resolve contribution identity '{productId}'.");

        internal string ContributionIdentity(string productId) => SemanticContributionIdentity(Contribution(productId));

        internal string ParticipantFormKey(string productId) =>
            participantFormKeys.TryGetValue(productId, out string? formKey)
                ? formKey
                : throw new CandidateOutputException($"Projection could not resolve participant identity '{productId}'.");

        internal string TaxonomySubject(JsonElement item)
        {
            string productSubject = Text(item, "subject_participant_id");
            return Text(item, "subject_type") switch
            {
                "record-contribution" => ContributionIdentity(productSubject),
                "record-semantic-subject" => SemanticRecordSubject(productSubject),
                "unsupported-record" => UnsupportedRecordSubject(productSubject),
                "provider-topology" => providerTopologySubject,
                string subjectType => throw new CandidateOutputException($"Projection encountered unsupported taxonomy subject type '{subjectType}'."),
            };
        }

        private void AddContribution(JsonElement contribution)
        {
            string productId = Text(contribution, "contribution_id");
            string semanticId = SemanticContributionIdentity(contribution);
            if (contributions.TryGetValue(productId, out JsonElement existing))
            {
                if (!StringComparer.Ordinal.Equals(SemanticContributionIdentity(existing), semanticId))
                {
                    throw new CandidateOutputException($"Product contribution identity '{productId}' maps to multiple semantic contributions.");
                }

                return;
            }

            contributions.Add(productId, contribution);
            AddParticipant(Text(contribution.GetProperty("identity"), "participant_id"),
                CanonicalFormKey(Text(contribution.GetProperty("identity"), "form_key")));
        }

        private void AddParticipant(string productId, string formKey)
        {
            if (participantFormKeys.TryGetValue(productId, out string? existing) && !StringComparer.Ordinal.Equals(existing, formKey))
            {
                throw new CandidateOutputException($"Product participant identity '{productId}' maps to multiple FormKeys.");
            }

            participantFormKeys[productId] = formKey;
        }

        private string SemanticRecordSubject(string productSubject)
        {
            foreach (string contributionId in contributions.Keys.OrderByDescending(value => value.Length))
            {
                string prefix = $"{contributionId}:semantic:";
                if (productSubject.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string semanticArea = productSubject[prefix.Length..].ToLowerInvariant();
                    return $"{ContributionIdentity(contributionId)}|semantic={semanticArea}";
                }
            }

            throw new CandidateOutputException($"Projection could not resolve semantic taxonomy subject '{productSubject}'.");
        }

        private static string UnsupportedRecordSubject(string productSubject)
        {
            Match match = UnsupportedSubjectPattern().Match(productSubject);
            if (!match.Success)
            {
                throw new CandidateOutputException($"Projection could not canonicalize unsupported-record subject '{productSubject}'.");
            }

            return string.Join('|',
                "unsupported-record",
                $"source={match.Groups["plugin"].Value.ToLowerInvariant()}",
                $"signature={match.Groups["signature"].Value.ToLowerInvariant()}",
                $"record={CanonicalFormKey(match.Groups["formkey"].Value)}");
        }

        private static string SemanticContributionIdentity(JsonElement contribution)
        {
            JsonElement identity = contribution.GetProperty("identity");
            return string.Join('|',
                $"source={Text(contribution, "source_plugin").ToLowerInvariant()}",
                $"order={contribution.GetProperty("load_order").GetInt32().ToString("D4", System.Globalization.CultureInfo.InvariantCulture)}",
                $"record={CanonicalFormKey(Text(identity, "form_key"))}",
                $"signature={Text(identity, "signature").ToLowerInvariant()}",
                $"flags={contribution.GetProperty("raw_flags").GetInt64():x8}",
                $"deleted={contribution.GetProperty("deleted").GetBoolean().ToString().ToLowerInvariant()}",
                $"compressed={contribution.GetProperty("compressed").GetBoolean().ToString().ToLowerInvariant()}");
        }
    }

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
