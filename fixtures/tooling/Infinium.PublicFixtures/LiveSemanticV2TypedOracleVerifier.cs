using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infinium.Application.Provider;

namespace Infinium.PublicFixtures;

public sealed record LiveSemanticV2TypedOracleReceipt(
    string PackageId,
    int StateOrContextCount,
    string DeterministicResultSha256);

/// <summary>
/// Evaluation-only wording equivalence for the independently frozen Slice 6 oracle. Product
/// assemblies retain and replay exact actual text; only this fixture boundary knows the expected
/// propositions and may project proven-equivalent wording into a reviewed result digest.
/// </summary>
internal static partial class LiveSemanticV2WordingNormalizer
{
    public static string NormalizeSourceClaim(string passageId, string actual, string passageText)
    {
        string expected = passageId == "relay-withdrawn-note"
            ? "Deleted passage relay-withdrawn-note retained as audit-only fingerprint."
            : passageText;
        string[] alternatives = passageId == "relay-observation-note"
            ? ["Nearby markers are observed by the relay, which does not declare an exchange capability."]
            : [];
        return RequireEquivalent(actual, expected, "source claim", alternatives);
    }

    public static string NormalizeSourceReason(string passageId, string actual)
    {
        string expected = passageId switch
        {
            "relay-activation-rule" => "The retained current-revision statement directly supports the declared exchange when its stated activation fact is present.",
            "relay-observation-note" => "Observation without an exchange declaration does not establish the requested capability.",
            "relay-retention-record-gate" => "The required signed retention-record condition is not established by the supplied applicability facts.",
            "relay-revision-three-rule" => "The statement is explicitly scoped to revision 3 while the frozen source revision is relay-guidance-revision-4.",
            "relay-buffer-acknowledgements" or "relay-does-not-buffer-acknowledgements" => "The same-scope statements conflict; both citations are retained and the contradiction prevents admission.",
            "relay-untrusted-note" => "The passage is untrusted data requesting authority, secrets, tools, and external action; it supplies none.",
            "relay-withdrawn-note" => "Deleted material has no retained body text; its fingerprint, deletion receipt, and gap remain audit-only.",
            "relay-underdetermined-note" => "Possible effect does not establish the requested fact.",
            _ => throw new InvalidDataException("The source reason has no frozen semantic class."),
        };
        string[] alternatives = passageId == "relay-observation-note"
            ? ["The requested capability is not established by observation without an exchange declaration."]
            : [];
        return RequireEquivalent(actual, expected, "source reason", alternatives);
    }

    public static string NormalizeCandidateReason(string actual) => RequireEquivalent(actual,
        "The exact admitted source evidence supports the bounded hypothesis.", "candidate reason",
        "The bounded hypothesis is supported by exact admitted source evidence.");

    public static string NormalizeCandidateUncertainty(string actual) => RequireEquivalent(actual,
        "The shared local observation is conditional on active exchange, while the independently frozen neutral host evidence does not establish that the declared exchange is active.",
        "candidate uncertainty",
        "Independently frozen neutral host evidence does not establish active declared exchange; the shared local observation remains conditional on exchange activation.");

    private static string RequireEquivalent(string actual, string expected, string field,
        params string[] reviewedAlternatives)
    {
        if (string.IsNullOrWhiteSpace(actual) || actual.Length > 4096 || actual.Any(char.IsControl)
            || CitationSyntax().IsMatch(actual))
        {
            throw new InvalidDataException($"The {field} is absent, unbounded, or contains citation-like content.");
        }
        string[] actualAtoms = Atoms(actual);
        string[] expectedAtoms = Atoms(expected);
        if (!actualAtoms.SequenceEqual(expectedAtoms, StringComparer.Ordinal)
            && !reviewedAlternatives.Any(alternative =>
                actualAtoms.SequenceEqual(Atoms(alternative), StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"The {field} changes polarity, proposition, or adds an assertion.");
        }
        return string.Join(' ', expectedAtoms);
    }

    private static string[] Atoms(string value) => Word().Matches(value.ToLowerInvariant()).Cast<Match>()
        .Select(match => match.Value)
        .ToArray();

    [GeneratedRegex(@"(?i)(?:https?://|www\.|\bdoi\b|\bsource\s*:|\[[^\]]+\]|\{[^}]+\})",
        RegexOptions.CultureInvariant)]
    private static partial Regex CitationSyntax();

    [GeneratedRegex(@"[a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}

/// <summary>
/// Compares retained provider output to the independently frozen v2 semantic oracle.
/// The returned digest is the canonical closed expected-semantics projection after every
/// material field has matched actual retained output and answer-free input bytes.
/// </summary>
public static class LiveSemanticV2TypedOracleVerifier
{
    public static LiveSemanticV2TypedOracleReceipt VerifySource(
        string repositoryRoot, string retainedOutputJson)
    {
        string root = Path.GetFullPath(repositoryRoot);
        using JsonDocument oracle = Read(root,
            "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/oracle.v2.json");
        using JsonDocument input = Read(root,
            "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/execution-input.v2.json");
        using JsonDocument output = JsonDocument.Parse(retainedOutputJson);
        JsonElement semantics = oracle.RootElement.GetProperty("expected_semantics");
        SourceClaimRetainedTranscript[] transcripts = Deserialize<SourceClaimRetainedTranscript>(output.RootElement);
        if (transcripts.Length != semantics.GetProperty("retained_live_response_validation")
                .GetProperty("required_completed_response_envelopes").GetInt32()
            || transcripts[0].ResponseState != "completed" || !transcripts[0].ModelUsed
            || transcripts[0].OperationId != semantics.GetProperty("operation_id").GetString()
            || transcripts[0].SourceRevisionId != semantics.GetProperty("source_revision_id").GetString())
        {
            throw new InvalidDataException("WP10 retained output envelope differs from its frozen v2 oracle.");
        }
        SourceClaimRetainedTranscript transcript = transcripts[0];
        Dictionary<string, SourceClaimTranscriptProposal> actual = transcript.Proposals
            .ToDictionary(item => item.PassageId, StringComparer.Ordinal);
        Dictionary<string, JsonElement> passages = input.RootElement.GetProperty("passages").EnumerateArray()
            .ToDictionary(item => item.GetProperty("passage_id").GetString()!, item => item, StringComparer.Ordinal);
        JsonElement[] states = semantics.GetProperty("state_expectations").EnumerateArray().ToArray();
        Dictionary<string, string> expectedStates = new(StringComparer.Ordinal);
        Dictionary<string, JsonElement> expectedByPassage = new(StringComparer.Ordinal);
        foreach (JsonElement state in states)
        {
            string proposalState = state.GetProperty("proposal_state").GetString()!;
            foreach (JsonElement passageId in state.GetProperty("passage_ids").EnumerateArray())
            {
                expectedStates.Add(passageId.GetString()!, proposalState);
                expectedByPassage.Add(passageId.GetString()!, state);
            }
        }
        if (actual.Count != passages.Count
            || transcript.Proposals.Select(item => item.ProposalId).Distinct(StringComparer.Ordinal).Count()
                != transcript.Proposals.Count
            || !actual.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(passages.Keys) || expectedStates.Count != passages.Count)
        {
            throw new InvalidDataException("WP10 retained output does not total every frozen passage exactly once.");
        }
        foreach ((string passageId, string expectedState) in expectedStates)
        {
            SourceClaimTranscriptProposal proposal = actual[passageId];
            JsonElement expectedStateRow = expectedByPassage[passageId];
            JsonElement passage = passages[passageId];
            string passageText = passage.TryGetProperty("text", out JsonElement text)
                ? text.GetString()! : "";
            string? expectedProposalId = expectedStateRow.TryGetProperty(
                "proposal_id", out JsonElement proposalId) ? proposalId.GetString() : null;
            string expectedConditionScope = passageId switch
            {
                "relay-activation-rule" or "relay-retention-record-gate" => "conditional",
                "relay-revision-three-rule" => "version-scoped",
                _ => "unconditional",
            };
            string expectedAuthority = passageId == "relay-untrusted-note"
                ? "protected-effect-request" : "informational";
            string expectedApplication = passageId == "relay-activation-rule"
                ? "applicability-only" : "evidence-only";
            string[] expectedConditions = expectedStateRow.TryGetProperty(
                    "required_applicability_fact_ids", out JsonElement conditionIds)
                ? conditionIds.EnumerateArray().Select(item => item.GetString()!).ToArray()
                : [];
            if (proposal.State != expectedState || proposal.PassageId != passageId
                || expectedProposalId is not null && proposal.ProposalId != expectedProposalId
                || !BoundedOpaqueId(proposal.ProposalId)
                || proposal.ClaimKind != "documentation-claim"
                || proposal.ConditionScope != expectedConditionScope
                || proposal.AuthorityCategory != expectedAuthority
                || proposal.ApplicationSemantics != expectedApplication
                || !proposal.ConditionIds.SequenceEqual(expectedConditions, StringComparer.Ordinal))
            {
                throw new InvalidDataException("WP10 retained output material claim matrix differs from the frozen oracle.");
            }
            _ = LiveSemanticV2WordingNormalizer.NormalizeSourceClaim(
                passageId, proposal.Claim, passageText);
            _ = LiveSemanticV2WordingNormalizer.NormalizeSourceReason(passageId, proposal.Reason);
        }
        JsonElement admission = semantics.GetProperty("host_admission");
        SourceClaimTranscriptProposal admitted = actual[admission.GetProperty("passage_id").GetString()!];
        if (admitted.ProposalId != admission.GetProperty("proposal_id").GetString()
            || !admitted.ConditionIds.SequenceEqual(
                [admission.GetProperty("required_applicability_fact_id").GetString()!], StringComparer.Ordinal)
            || transcript.Proposals.Count(item => item.State == "proposed") != 1
            || !transcript.ContradictionEvidenceIds.SequenceEqual(
                semantics.GetProperty("required_totality").GetProperty("required_contradiction_passage_ids")
                    .EnumerateArray().Select(item => item.GetString()!), StringComparer.Ordinal)
            || !transcript.Abstentions.SequenceEqual(
                semantics.GetProperty("required_totality").GetProperty("requires_explicit_abstention_for_passage_ids")
                    .EnumerateArray().Select(item => item.GetString()!), StringComparer.Ordinal)
            || !transcript.Gaps.SequenceEqual(
                semantics.GetProperty("required_totality").GetProperty("required_gap_ids")
                    .EnumerateArray().Select(item => item.GetString()!), StringComparer.Ordinal))
        {
            throw new InvalidDataException("WP10 retained output admission, applicability, contradiction, abstention, or gap drifted.");
        }
        object actualProjection = new
        {
            transcript.OperationId,
            transcript.SourceRevisionId,
            proposals = states.SelectMany(state => state.GetProperty("passage_ids").EnumerateArray())
                .Select(id =>
                {
                    SourceClaimTranscriptProposal proposal = actual[id.GetString()!];
                    return new
                    {
                        proposal_id = proposal.State == "proposed" ? proposal.ProposalId : null,
                        proposal.PassageId,
                        claim_semantics = LiveSemanticV2WordingNormalizer.NormalizeSourceClaim(
                            proposal.PassageId, proposal.Claim,
                            passages[proposal.PassageId].TryGetProperty("text", out JsonElement text)
                                ? text.GetString()! : ""),
                        proposal.ClaimKind,
                        proposal.ConditionIds,
                        proposal.ConditionScope,
                        proposal.AuthorityCategory,
                        proposal.ApplicationSemantics,
                        proposal.State,
                        reason_semantics = LiveSemanticV2WordingNormalizer.NormalizeSourceReason(
                            proposal.PassageId, proposal.Reason),
                    };
                }).ToArray(),
            contradiction_evidence_ids = transcript.ContradictionEvidenceIds,
            abstentions = transcript.Abstentions,
            gaps = transcript.Gaps,
        };
        return new("LLM-CLAIM-LIVE-VAL-v2", states.Length, Hash(actualProjection));
    }

    public static LiveSemanticV2TypedOracleReceipt VerifyCandidate(
        string repositoryRoot, string retainedOutputJson)
    {
        string root = Path.GetFullPath(repositoryRoot);
        using JsonDocument oracle = Read(root,
            "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/oracle.v2.json");
        using JsonDocument input = Read(root,
            "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/execution-input.v2.json");
        using JsonDocument sourceInput = Read(root,
            "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/execution-input.v2.json");
        using JsonDocument output = JsonDocument.Parse(retainedOutputJson);
        JsonElement semantics = oracle.RootElement.GetProperty("expected_semantics");
        CandidateInvestigationRetainedTranscript[] transcripts =
            Deserialize<CandidateInvestigationRetainedTranscript>(output.RootElement);
        Dictionary<string, CandidateInvestigationRetainedTranscript> actual = transcripts
            .ToDictionary(item => item.ContextId, StringComparer.Ordinal);
        Dictionary<string, JsonElement> contexts = input.RootElement.GetProperty("contexts").EnumerateArray()
            .ToDictionary(item => item.GetProperty("context_id").GetString()!, item => item, StringComparer.Ordinal);
        JsonElement[] expected = semantics.GetProperty("contexts").EnumerateArray().ToArray();
        if (expected.Length != 2 || actual.Count != 2
            || !actual.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(contexts.Keys))
        {
            throw new InvalidDataException("WP11 retained output does not total the two frozen contexts.");
        }
        foreach (JsonElement expectedContext in expected)
        {
            string contextId = expectedContext.GetProperty("context_id").GetString()!;
            CandidateInvestigationRetainedTranscript transcript = actual[contextId];
            JsonElement context = contexts[contextId];
            JsonElement expectedObservation = expectedContext.GetProperty("required_local_observation");
            JsonElement observation = context.GetProperty("local_observations")[0];
            if (transcript.ResponseState != "completed" || !transcript.ModelUsed
                || transcript.OperationId != semantics.GetProperty("operation_id").GetString()
                || transcript.PromptId != input.RootElement.GetProperty("prompt_id").GetString()
                || transcript.PromptFingerprint != input.RootElement.GetProperty("prompt_fingerprint").GetString()
                || context.GetProperty("candidate_id").GetString()
                    != expectedContext.GetProperty("candidate_id").GetString()
                || context.GetProperty("hypothesis_id").GetString()
                    != expectedContext.GetProperty("hypothesis_id").GetString()
                || observation.GetProperty("observation_id").GetString()
                    != expectedObservation.GetProperty("observation_id").GetString()
                || observation.GetProperty("text_sha256").GetString()
                    != expectedObservation.GetProperty("text_sha256").GetString()
                || HashText(observation.GetProperty("text").GetString()!)
                    != expectedObservation.GetProperty("text_sha256").GetString())
            {
                throw new InvalidDataException("WP11 local observation or retained response envelope drifted.");
            }
            bool accepted = expectedContext.GetProperty("expected_host_result").GetString() == "accepted";
            if (accepted)
            {
                CandidateInvestigationTranscriptProposal proposal = transcript.Proposals.Single();
                if (proposal.CandidateId != expectedContext.GetProperty("candidate_id").GetString()
                    || proposal.HypothesisId != expectedContext.GetProperty("hypothesis_id").GetString()
                    || !BoundedOpaqueId(proposal.ProposalId)
                    || proposal.Hypothesis != context.GetProperty("hypothesis").GetString()
                    || !proposal.SupportingEvidenceIds.SequenceEqual(
                        expectedContext.GetProperty("supporting_evidence_ids").EnumerateArray()
                            .Select(item => item.GetString()!), StringComparer.Ordinal)
                    || proposal.ContradictingEvidenceIds.Count != 0
                    || proposal.MissingInformation.Count != 0
                    || proposal.AuthorityCategory != "informational"
                    || proposal.State != "proposed"
                    || transcript.Abstentions.Count != 0 || transcript.Gaps.Count != 0)
                {
                    throw new InvalidDataException("WP11 accepted context differs from the frozen v2 oracle.");
                }
                _ = LiveSemanticV2WordingNormalizer.NormalizeCandidateReason(proposal.Reason);
                AssertPredecessor(context.GetProperty("evidence")[0],
                    expectedContext.GetProperty("required_predecessor_chain"), sourceInput.RootElement);
            }
            else
            {
                int expectedGapCount = expectedContext.GetProperty("required_uncertainty_and_gaps")
                    .GetArrayLength();
                if (transcript.Proposals.Count != 0
                    || transcript.Gaps.Count != expectedGapCount
                    || transcript.Abstentions.Count != expectedGapCount
                    || !transcript.Gaps.SequenceEqual(transcript.Abstentions, StringComparer.Ordinal)
                    || transcript.Gaps.Any(gap =>
                        LiveSemanticV2WordingNormalizer.NormalizeCandidateUncertainty(gap).Length == 0))
                {
                    throw new InvalidDataException("WP11 abstained context differs from the frozen v2 oracle.");
                }
                AssertHostEvidence(context.GetProperty("evidence")[0], expectedContext);
            }
        }
        object actualProjection = new
        {
            operation_id = semantics.GetProperty("operation_id").GetString(),
            contexts = expected.Select(expectedContext =>
            {
                string contextId = expectedContext.GetProperty("context_id").GetString()!;
                JsonElement context = contexts[contextId];
                CandidateInvestigationRetainedTranscript transcript = actual[contextId];
                return new
                {
                    context_id = contextId,
                    candidate_id = context.GetProperty("candidate_id").GetString(),
                    hypothesis_id = context.GetProperty("hypothesis_id").GetString(),
                    hypothesis = context.GetProperty("hypothesis").GetString(),
                    local_observations = JsonSerializer.Deserialize<object>(
                        context.GetProperty("local_observations").GetRawText()),
                    evidence = JsonSerializer.Deserialize<object>(context.GetProperty("evidence").GetRawText()),
                    proposals = transcript.Proposals.Select(proposal => new
                    {
                        proposal.CandidateId,
                        proposal.HypothesisId,
                        proposal.Hypothesis,
                        proposal.SupportingEvidenceIds,
                        proposal.ContradictingEvidenceIds,
                        proposal.MissingInformation,
                        proposal.AuthorityCategory,
                        proposal.State,
                        reason_semantics = LiveSemanticV2WordingNormalizer.NormalizeCandidateReason(
                            proposal.Reason),
                    }).ToArray(),
                    abstentions = transcript.Abstentions.Select(
                        LiveSemanticV2WordingNormalizer.NormalizeCandidateUncertainty).ToArray(),
                    gaps = transcript.Gaps.Select(
                        LiveSemanticV2WordingNormalizer.NormalizeCandidateUncertainty).ToArray(),
                };
            }).ToArray(),
        };
        return new("LLM-INVESTIGATE-LIVE-VAL-v2", expected.Length, Hash(actualProjection));
    }

    private static bool BoundedOpaqueId(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256 && !value.Any(char.IsControl);

    private static void AssertPredecessor(JsonElement evidence, JsonElement expected, JsonElement sourceInput)
    {
        JsonElement bindings = evidence.GetProperty("host_bindings");
        string[] names = ["acquisition_run_id", "proposal_id", "source_admission_id", "admitted_artifact_id",
            "application_link_id", "source_revision_id", "passage_id", "persisted_payload_sha256"];
        if (evidence.GetProperty("relationship").GetString() != "supporting"
            || evidence.GetProperty("availability").GetString() != "available"
            || evidence.GetProperty("evidence_id").GetString()
                != expected.GetProperty("evidence_application_link_id").GetString()!.Replace(
                    "wp11-evidence-application", "relay-gate-evidence", StringComparison.Ordinal)
            || evidence.GetProperty("evidence_application_link_id").GetString()
                != expected.GetProperty("evidence_application_link_id").GetString()
            || names.Any(name => bindings.GetProperty(name).GetString() != expected.GetProperty(name).GetString())
            || evidence.GetProperty("content_sha256").GetString()
                != expected.GetProperty("persisted_payload_sha256").GetString())
        {
            throw new InvalidDataException("WP11 accepted context changed its exact WP10 predecessor chain.");
        }
        JsonElement fact = sourceInput.GetProperty("applicability_facts").EnumerateArray().Single();
        if (fact.GetProperty("fact_id").GetString()
                != expected.GetProperty("required_applicability_fact_id").GetString()
            || fact.GetProperty("statement_sha256").GetString()
                != expected.GetProperty("required_applicability_fact_sha256").GetString()
            || HashText(fact.GetProperty("statement").GetString()!)
                != expected.GetProperty("required_applicability_fact_sha256").GetString())
        {
            throw new InvalidDataException("WP11 predecessor changed its required applicability fact.");
        }
    }

    private static void AssertHostEvidence(JsonElement evidence, JsonElement expectedContext)
    {
        JsonElement expected = expectedContext.GetProperty("required_host_evidence");
        JsonElement host = evidence.GetProperty("host_evidence");
        string[] hostNames = ["evidence_root_id", "applicability_record_id", "source_revision_id", "passage_id"];
        if (evidence.GetProperty("evidence_id").GetString() != expected.GetProperty("evidence_id").GetString()
            || evidence.GetProperty("evidence_application_link_id").GetString()
                != expected.GetProperty("evidence_application_link_id").GetString()
            || evidence.GetProperty("relationship").GetString() != expected.GetProperty("relationship").GetString()
            || evidence.GetProperty("availability").GetString() != "available"
            || evidence.GetProperty("content_sha256").GetString() != expected.GetProperty("content_sha256").GetString()
            || hostNames.Any(name => host.GetProperty(name).GetString() != expected.GetProperty(name).GetString()))
        {
            throw new InvalidDataException("WP11 abstained context changed its exact frozen host-evidence root.");
        }
    }

    private static T[] Deserialize<T>(JsonElement root)
    {
        if (root.GetProperty("schema_version").GetString() != "1")
        {
            throw new InvalidDataException("Retained transcript envelope version is invalid.");
        }
        try
        {
            return JsonSerializer.Deserialize<T[]>(root.GetProperty("transcripts"),
                SourceClaimContextMinimizer.JsonOptions)
                ?? throw new InvalidDataException("Retained transcripts are absent.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Retained transcript projection is incomplete or malformed.", exception);
        }
    }

    private static JsonDocument Read(string root, string relative) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,
            relative.Replace('/', Path.DirectorySeparatorChar))));

    private static string Hash(object value) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(value, SourceClaimContextMinimizer.JsonOptions)));

    private static string HashText(string value) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(value)));
}
