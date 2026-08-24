using System.Text;
using System.Text.Json;
using Infinium.Application.Provider;

namespace Infinium.Coordinator;

internal enum M1Slice6CampaignEvidenceRootKind
{
    PersistedSourceClaimApplication,
    FrozenHostEvidence,
}

internal sealed record M1Slice6CampaignEvidenceRoot(
    M1Slice6CampaignEvidenceRootKind Kind,
    string ContextId,
    string CandidateId,
    string EvidenceId,
    string EvidenceApplicationLinkId,
    string SourceRevisionId,
    string PassageId,
    string ContentSha256,
    string AcquisitionRunId,
    string ProposalId,
    string SourceAdmissionId,
    string ApplicationDecisionId,
    string AdmittedArtifactId,
    string ApplicationLinkId,
    string EvidenceRootId,
    string ApplicabilityRecordId);

internal sealed record M1Slice6CampaignCandidateInput(
    CandidateInvestigationExecutionInput ProductInput,
    IReadOnlyDictionary<string, M1Slice6CampaignEvidenceRoot> RootsByContext,
    IReadOnlyDictionary<string, M1Slice6CampaignLocalObservation> LocalObservationsByContext,
    byte[] ExactV2Bytes);

internal sealed record M1Slice6CampaignApplicabilityFact(
    string FactId,
    string SourceRevisionId,
    string Statement,
    string StatementSha256);

internal sealed record M1Slice6CampaignSourceInput(
    SourceClaimExecutionInput ProductInput,
    IReadOnlyDictionary<string, M1Slice6CampaignApplicabilityFact> ApplicabilityFacts,
    byte[] ExactV2Bytes);

internal sealed record M1Slice6CampaignLocalObservation(
    string ObservationId,
    string Text,
    string TextSha256);

/// <summary>
/// Strict clean-break adapter from the frozen campaign v2 authority into the unchanged
/// product-facing source-claim and candidate-investigation v1 domain contracts. The exact
/// v2 bytes and root discriminator remain authoritative for persistence and replay.
/// </summary>
internal static class M1Slice6CampaignV2InputAdapter
{
    internal static SourceClaimExecutionInput ReadSourceClaim(string json) =>
        ReadSourceClaimAuthority(json).ProductInput;

    internal static M1Slice6CampaignSourceInput ReadSourceClaimAuthority(string json)
    {
        using JsonDocument document = Parse(json);
        JsonElement root = document.RootElement;
        Require(root, "schema_id", "infinium.llm.source-claim-execution-input/v2");
        Require(root, "schema_version", "2");
        RequireBoundedIdentity(root, "package_id");
        JsonElement[] passages = root.GetProperty("passages").EnumerateArray().ToArray();
        if (passages.Length == 0)
        {
            throw new InvalidDataException("WP10 v2 input has no bounded passages.");
        }

        SourceClaimPassageInput[] normalized = passages.Select(passage => new SourceClaimPassageInput(
            Text(passage, "passage_id"), Text(passage, "source_revision_id"),
            passage.TryGetProperty("text", out JsonElement text) ? text.GetString() ?? string.Empty : string.Empty,
            Sha(passage, "text_sha256"), passage.GetProperty("deleted").GetBoolean())
        {
            StartByte = passage.GetProperty("start_byte").GetInt64(),
            EndByte = passage.GetProperty("end_byte").GetInt64(),
        }).ToArray();
        Dictionary<string, M1Slice6CampaignApplicabilityFact> facts = new(StringComparer.Ordinal);
        foreach (JsonElement fact in root.GetProperty("applicability_facts").EnumerateArray())
        {
            M1Slice6CampaignApplicabilityFact value = new(Text(fact, "fact_id"),
                Text(fact, "source_revision_id"), Text(fact, "statement"), Sha(fact, "statement_sha256"));
            if (value.SourceRevisionId != Text(root, "source_revision_id")
                || Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(value.Statement))) != value.StatementSha256
                || !facts.TryAdd(value.FactId, value))
            {
                throw new InvalidDataException("WP10 v2 applicability facts are duplicated or drifted.");
            }
        }
        SourceClaimExecutionInput input = new(
            "infinium.llm.source-claim-execution-input/v1", "1", Text(root, "package_id"),
            Text(root, "acquisition_run_id"), Text(root, "operation_id"),
            Text(root, "host_authorization_id"), Text(root, "owner_kind"), Text(root, "owner_id"),
            Text(root, "parent_analysis_run_id"), Text(root, "application_scope_id"),
            Text(root, "cost_attribution_scope_id"), Text(root, "source_revision_id"),
            Text(root, "declared_purpose"), Text(root, "prompt_id"), Text(root, "prompt_fingerprint"),
            normalized);
        SourceClaimContextMinimizer.ValidateInput(input);
        return new(input, facts, Encoding.UTF8.GetBytes(json));
    }

    internal static M1Slice6CampaignCandidateInput ReadCandidate(string json)
    {
        using JsonDocument document = Parse(json);
        JsonElement root = document.RootElement;
        Require(root, "schema_id", "infinium.llm.candidate-investigation-execution-input/v2");
        Require(root, "schema_version", "2");
        RequireBoundedIdentity(root, "package_id");
        JsonElement[] contexts = root.GetProperty("contexts").EnumerateArray().ToArray();
        if (contexts.Length != 2)
        {
            throw new InvalidDataException("WP11 v2 input must contain exactly two closed contexts.");
        }

        Dictionary<string, M1Slice6CampaignEvidenceRoot> roots = new(StringComparer.Ordinal);
        Dictionary<string, M1Slice6CampaignLocalObservation> observations = new(StringComparer.Ordinal);
        List<CandidateInvestigationContextInput> normalizedContexts = [];
        foreach (JsonElement context in contexts)
        {
            JsonElement[] evidenceItems = context.GetProperty("evidence").EnumerateArray().ToArray();
            if (evidenceItems.Length != 1)
            {
                throw new InvalidDataException("Each WP11 v2 context must contain one exact evidence root.");
            }
            JsonElement evidence = evidenceItems[0];
            bool hasSource = evidence.TryGetProperty("host_bindings", out JsonElement source);
            bool hasHost = evidence.TryGetProperty("host_evidence", out JsonElement host);
            if (hasSource == hasHost)
            {
                throw new InvalidDataException("WP11 v2 evidence must select exactly one root discriminator.");
            }

            string contextId = Text(context, "context_id");
            string candidateId = Text(context, "candidate_id");
            string evidenceId = Text(evidence, "evidence_id");
            string evidenceApplication = Text(evidence, "evidence_application_link_id");
            string sourceRevision = Text(hasSource ? source : host, "source_revision_id");
            string passageId = Text(hasSource ? source : host, "passage_id");
            string contentSha = Sha(evidence, "content_sha256");
            M1Slice6CampaignEvidenceRoot metadata;
            CandidateEvidenceInput normalizedEvidence;
            if (hasSource)
            {
                string persistedSha = Sha(source, "persisted_payload_sha256");
                if (persistedSha != contentSha)
                {
                    throw new InvalidDataException("WP11 persisted source root changed its payload digest.");
                }
                metadata = new(M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication,
                    contextId, candidateId, evidenceId, evidenceApplication, sourceRevision, passageId,
                    contentSha, Text(source, "acquisition_run_id"), Text(source, "proposal_id"),
                    Text(source, "source_admission_id"),
                    Text(source, "application_decision_id"),
                    Text(source, "admitted_artifact_id"),
                    Text(source, "application_link_id"), string.Empty, string.Empty);
                normalizedEvidence = new(evidenceId, evidenceApplication, metadata.AcquisitionRunId,
                    metadata.SourceAdmissionId, metadata.ApplicationLinkId, sourceRevision, passageId,
                    Text(evidence, "relationship"), Text(evidence, "availability"), contentSha)
                {
                    SourceApplicationDecisionId = metadata.ApplicationDecisionId,
                };
            }
            else
            {
                metadata = new(M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence,
                    contextId, candidateId, evidenceId, evidenceApplication, sourceRevision, passageId,
                    contentSha, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    Text(host, "evidence_root_id"), Text(host, "applicability_record_id"));
                normalizedEvidence = new(evidenceId, evidenceApplication, string.Empty,
                    string.Empty, string.Empty, sourceRevision, passageId,
                    Text(evidence, "relationship"), Text(evidence, "availability"), contentSha)
                {
                    RootKind = "frozen-host-evidence",
                    EvidenceRootId = metadata.EvidenceRootId,
                    ApplicabilityRecordId = metadata.ApplicabilityRecordId,
                };
            }
            if (!roots.TryAdd(contextId, metadata))
            {
                throw new InvalidDataException("WP11 v2 context identity is duplicated.");
            }
            JsonElement[] localObservations = context.GetProperty("local_observations").EnumerateArray().ToArray();
            if (localObservations.Length != 1)
            {
                throw new InvalidDataException("Each WP11 v2 context must contain one exact local observation.");
            }
            JsonElement localObservation = localObservations[0];
            M1Slice6CampaignLocalObservation observation = new(Text(localObservation, "observation_id"),
                Text(localObservation, "text"), Sha(localObservation, "text_sha256"));
            if (Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(observation.Text))) != observation.TextSha256
                || !observations.TryAdd(contextId, observation))
            {
                throw new InvalidDataException("WP11 v2 local observation is duplicated or drifted.");
            }
            normalizedContexts.Add(new(contextId, candidateId, Text(context, "hypothesis_id"),
                Text(context, "hypothesis"), Strings(context, "participant_ids"),
                Strings(context, "participant_roles"), Strings(context, "causal_path_ids"),
                Text(context, "dependency_closure_id"), [normalizedEvidence]));
        }

        if (roots.Values.Count(item => item.Kind == M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication) != 1
            || roots.Values.Count(item => item.Kind == M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence) != 1)
        {
            throw new InvalidDataException("WP11 v2 requires one persisted WP10 root and one frozen host root.");
        }
        if (observations.Values.Select(item => (item.ObservationId, item.TextSha256)).Distinct().Count() != 1)
        {
            throw new InvalidDataException("WP11 v2 contexts do not retain the same exact local observation.");
        }
        CandidateInvestigationExecutionInput input = new(
            "infinium.llm.candidate-investigation-execution-input/v1", "1", Text(root, "package_id"),
            Text(root, "operation_id"), Text(root, "host_authorization_id"), Text(root, "owner_kind"),
            Text(root, "owner_id"), Text(root, "analysis_run_id"), Text(root, "application_scope_id"),
            Text(root, "cost_attribution_scope_id"), Text(root, "prompt_id"),
            Text(root, "prompt_fingerprint"), normalizedContexts);
        CandidateInvestigationContextMinimizer.ValidateInput(input);
        return new(input, roots, observations, Encoding.UTF8.GetBytes(json));
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Campaign v2 input is not strict JSON.", exception);
        }
    }

    private static string Text(JsonElement value, string property)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty(property, out JsonElement propertyValue)
            || propertyValue.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Campaign v2 input has a missing or non-text " + property + ".");
        }
        string result = propertyValue.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidDataException("Campaign v2 input has an empty " + property + ".");
        }
        return result;
    }

    private static string Sha(JsonElement value, string property)
    {
        string result = Text(value, property);
        if (result.Length != 64 || !result.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new InvalidDataException("Campaign v2 input has an invalid " + property + ".");
        }
        return result;
    }

    private static string[] Strings(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty(property, out JsonElement items)
            && items.ValueKind == JsonValueKind.Array
        ? items.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
            ? item.GetString()! : throw new InvalidDataException(
                "Campaign v2 input contains a non-text " + property + ".")).ToArray()
        : throw new InvalidDataException("Campaign v2 input has a missing or non-array " + property + ".");

    private static void Require(JsonElement value, string property, string expected)
    {
        if (Text(value, property) != expected)
        {
            throw new InvalidDataException("Campaign v2 input has a wrong " + property + " identity.");
        }
    }

    private static void RequireBoundedIdentity(JsonElement value, string property)
    {
        string identity = Text(value, property);
        if (identity.Length > 256 || identity.Any(char.IsControl))
        {
            throw new InvalidDataException("Campaign v2 input has an invalid " + property + " identity.");
        }
    }
}
