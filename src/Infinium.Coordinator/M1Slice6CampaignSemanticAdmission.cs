using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Coordinator;

internal sealed record M1Slice6CampaignTranscriptEnvelope<T>(
    string SchemaId, string SchemaVersion, IReadOnlyList<T> Transcripts);

internal sealed record M1Slice6CampaignSemanticAdmissionReceipt(
    string ValidationId, string Disposition, int ProposalCount, int AdmissionCount,
    string ResultSha256, M1Slice6CampaignSemanticProvenance Provenance);

/// <summary>
/// Host-owned semantic boundary for the finite campaign. Provider output is only an untrusted
/// retained transcript. This boundary rehydrates the exact answer-free product input from the
/// canonical request, applies the existing deterministic host policy, and persists the admitted
/// proposal graph in the same authoritative SQLite store as the request and settlement.
/// </summary>
internal static class M1Slice6CampaignSemanticAdmission
{
    internal static (long Bytes, string Sha256, string TemplateSha256) BindCanonicalInputAndTemplate(
        ReadOnlySpan<byte> canonicalRequest)
    {
        string input = ExtractUntrustedInput(canonicalRequest);
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        JsonNode template = JsonNode.Parse(canonicalRequest.ToArray())
            ?? throw new InvalidDataException("The canonical request template is absent.");
        template["input"]![1]!["content"]![0]!["text"] =
            "BEGIN_UNTRUSTED_EVIDENCE\n{{CAMPAIGN_INPUT}}\nEND_UNTRUSTED_EVIDENCE";
        byte[] templateBytes = JsonSerializer.SerializeToUtf8Bytes(template);
        return (inputBytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(inputBytes)),
            Convert.ToHexStringLower(SHA256.HashData(templateBytes)));
    }

    internal static void PreparePrerequisites(AuthoritativeStore store,
        M1Slice6CampaignStageAuthority authority, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(store);
        string inputJson = ExtractUntrustedInput(authority.CanonicalRequest);
        if (authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction)
        {
            SourceClaimExecutionInput input = M1Slice6CampaignV2InputAdapter.ReadSourceClaim(inputJson);
            try
            {
                RequireSourceRevision(store, input);
            }
            catch (InvalidDataException)
            {
                MaterializeSourceRevision(store, input, occurredAt);
                RequireSourceRevision(store, input);
            }
            string installation = input.AcquisitionRunId + "-installation";
            string context = input.AcquisitionRunId + "-analysis-context";
            string configuration = input.AcquisitionRunId + "-effective-configuration";
            string manifest = input.AcquisitionRunId + "-input-manifest";
            store.EnsureSourceClaimCampaignParentRun(input.ParentAnalysisRunId, installation,
                context, configuration, manifest, occurredAt.AddTicks(1));
            store.EnsureSourceClaimCampaignAcquisition(new(input.AcquisitionRunId,
                installation, context, configuration, manifest,
                input.ParentAnalysisRunId, input.ApplicationScopeId, input.CostAttributionScopeId,
                input.AcquisitionRunId + "-source-claim-job", input.AcquisitionRunId + "-source-claim-command",
                input.AcquisitionRunId + "-parent-link", input.SourceRevisionId, occurredAt.AddTicks(2)));
        }
        else if (authority.Stage == M1Slice6CampaignStage.CandidateInvestigation)
        {
            M1Slice6CampaignCandidateInput campaignInput = RebindCandidateSourceApplication(
                store, M1Slice6CampaignV2InputAdapter.ReadCandidate(inputJson));
            CandidateInvestigationExecutionInput input = campaignInput.ProductInput;
            if (input.Contexts.Count != 2)
            {
                throw new InvalidDataException("WP11 live input must bind the exact two-context product contract.");
            }
            store.EnsureSourceClaimCampaignParentRun(input.AnalysisRunId,
                input.AnalysisRunId + "-install", input.AnalysisRunId + "-context",
                input.AnalysisRunId + "-config", input.AnalysisRunId + "-manifest",
                occurredAt.AddTicks(1));
            bool alreadyPrepared = input.Contexts.All(context =>
            {
                try
                {
                    RequireCandidateRoots(store, input, context.ContextId,
                        campaignInput.RootsByContext[context.ContextId]);
                    return true;
                }
                catch (InvalidDataException)
                {
                    return false;
                }
            });
            if (!alreadyPrepared)
            {
                using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
                connection.Open();
                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (CandidateInvestigationContextInput context in input.Contexts)
                {
                    MaterializeCandidateRoots(store, input, context,
                        campaignInput.RootsByContext[context.ContextId], occurredAt, connection, transaction);
                }
                transaction.Commit();
            }
        }
    }

    internal static M1Slice6CampaignSemanticAdmissionReceipt Admit(
        AuthoritativeStore store, M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignAccountingAdmission admission, OpenAiResponsesResult response,
        DateTimeOffset occurredAt, bool successorV6 = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        string inputJson = ExtractUntrustedInput(authority.CanonicalRequest);
        byte[] rawResponse = response.RawResponseBytes
            ?? throw new InvalidDataException("Semantic admission requires retained raw response bytes.");
        string rawResponseSha256 = Convert.ToHexStringLower(SHA256.HashData(rawResponse));
        string outputJson = successorV6
            ? ExtractSuccessorV6OutputText(rawResponse)
            : ExtractOutputText(rawResponse);
        return authority.Stage switch
        {
            M1Slice6CampaignStage.Qualification => AdmitQualification(outputJson),
            M1Slice6CampaignStage.SourceClaimExtraction => AdmitSourceClaim(
                store, inputJson, outputJson, rawResponseSha256, admission, occurredAt),
            M1Slice6CampaignStage.CandidateInvestigation => AdmitCandidate(
                store, inputJson, outputJson, rawResponseSha256, admission, occurredAt),
            _ => throw new InvalidDataException("The finite campaign semantic stage is not closed."),
        };
    }

    private static M1Slice6CampaignSemanticAdmissionReceipt AdmitQualification(string outputJson)
    {
        using JsonDocument document = JsonDocument.Parse(outputJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(["ok"], StringComparer.Ordinal)
            || !root.GetProperty("ok").GetBoolean())
        {
            throw new InvalidDataException("Qualification output is not the exact non-semantic transport receipt.");
        }
        return new("qualification-nonsemantic", "not-applicable", 0, 0, new string('0', 64),
            M1Slice6CampaignSemanticProvenance.Empty);
    }

    private static M1Slice6CampaignSemanticAdmissionReceipt AdmitSourceClaim(
        AuthoritativeStore store, string inputJson, string outputJson, string rawResponseSha256,
        M1Slice6CampaignAccountingAdmission admission, DateTimeOffset occurredAt)
    {
        M1Slice6CampaignSourceInput campaignInput =
            M1Slice6CampaignV2InputAdapter.ReadSourceClaimAuthority(inputJson);
        SourceClaimExecutionInput input = campaignInput.ProductInput;
        M1Slice6CampaignTranscriptEnvelope<SourceClaimRetainedTranscript> envelope =
            JsonSerializer.Deserialize<M1Slice6CampaignTranscriptEnvelope<SourceClaimRetainedTranscript>>(
                outputJson, SourceClaimContextMinimizer.JsonOptions)
            ?? throw new InvalidDataException("WP10 output is not an exact retained transcript envelope.");
        if (envelope.SchemaId != "infinium.llm.source-claim-retained-transcripts/v1"
            || envelope.SchemaVersion != "1" || envelope.Transcripts.Count != 1
            || input.OperationId != SemanticOperationId(admission)
            || input.HostAuthorizationId != SemanticAuthorizationId(admission))
        {
            throw new InvalidDataException("WP10 input/output identities differ from the authoritative provider admission.");
        }
        SourceClaimRetainedTranscript retainedTranscript = envelope.Transcripts[0] with
        {
            ResponseRecordId = "m1s6-campaign-stage-2-response",
            ResponseFingerprint = rawResponseSha256,
        };
        if (!BoundedOpaqueId(retainedTranscript.ResponseRecordId)
            || retainedTranscript.Proposals.Select(item => item.PassageId).Distinct(StringComparer.Ordinal).Count()
                != input.Passages.Count
            || !retainedTranscript.Proposals.Select(item => item.PassageId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(input.Passages.Select(item => item.PassageId)))
        {
            throw new InvalidDataException("WP10 transcript did not total the authoritative response and passages.");
        }
        if (retainedTranscript.Proposals.SelectMany(item => item.ConditionIds).Any(conditionId =>
            !campaignInput.ApplicabilityFacts.TryGetValue(conditionId, out M1Slice6CampaignApplicabilityFact? fact)
            || fact.SourceRevisionId != input.SourceRevisionId))
        {
            throw new InvalidDataException("WP10 proposals did not reopen exact applicability facts.");
        }
        string semanticResultSha256 = SourceActualSemanticResultSha256(input, retainedTranscript);
        SourceClaimRetainedTranscript transcript = retainedTranscript;
        RequireSourceRevision(store, input);
        SourceClaimScenarioResult preview = SourceClaimAcquisitionEngine.Execute(input, [transcript]).Scenarios.Single();
        HashSet<string> admittedProposalIds = preview.Extraction.AdmissionCorrelations
            .Where(item => item.State == ProposalAdmissionState.Admitted)
            .Select(item => item.ProposalId.Value).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, SourceClaimCampaignIdentity> identities = transcript.Proposals
            .Where(item => admittedProposalIds.Contains(item.ProposalId))
            .ToDictionary(item => item.ProposalId, item => CampaignSourceIdentity(item.ProposalId),
                StringComparer.Ordinal);
        Dictionary<string, SourceClaimPassageInput> passages = input.Passages.ToDictionary(
            item => item.PassageId, StringComparer.Ordinal);
        Dictionary<string, SourceClaimArtifactAuthority> artifactAuthority = transcript.Proposals
            .Where(item => admittedProposalIds.Contains(item.ProposalId))
            .ToDictionary(item => item.ProposalId, item =>
            {
                SourceClaimPassageInput passage = passages[item.PassageId];
                return new SourceClaimArtifactAuthority(identities[item.ProposalId].AdmittedArtifactId,
                    passage.SourceRevisionId, passage.PassageId, Encoding.UTF8.GetBytes(passage.Text),
                    passage.TextSha256, 0, Encoding.UTF8.GetByteCount(passage.Text));
            }, StringComparer.Ordinal);
        Dictionary<string, SourceClaimApplicabilityFactAuthority> applicabilityFacts =
            campaignInput.ApplicabilityFacts.Values.Where(item => preview.Extraction.ClaimProposals
                    .Where(proposal => proposal.State == ProposalAdmissionState.Admitted)
                    .SelectMany(proposal => proposal.ConditionIds).Any(id => id.Value == item.FactId))
                .ToDictionary(item => item.FactId,
                item => new SourceClaimApplicabilityFactAuthority(item.FactId, item.SourceRevisionId,
                    item.Statement, item.StatementSha256), StringComparer.Ordinal);
        Dictionary<string, SourceClaimApplicationAuthority> applicationAuthority = transcript.Proposals
            .Where(item => admittedProposalIds.Contains(item.ProposalId))
            .ToDictionary(item => item.ProposalId, item => new SourceClaimApplicationAuthority(
                identities[item.ProposalId].ApplicationLinkId, input.ParentAnalysisRunId,
                input.ApplicationScopeId, input.CostAttributionScopeId), StringComparer.Ordinal);
        SourceClaimAdmissionPublication publication = new SourceClaimAcquisitionCoordinator(store)
            .AdmitRetainedTranscript(input, transcript, SemanticAuthorizationId(admission),
                admission.AttemptId, admission.RequestId, admission.DispatchFenceId, occurredAt,
                identities, artifactAuthority, applicabilityFacts, applicationAuthority);
        SourceClaimScenarioResult scenario = publication.Scenario;
        int admittedCount = scenario.Extraction.AdmissionCorrelations.Count(item =>
            item.State == ProposalAdmissionState.Admitted);
        if (publication.Persistence.ProposalCount != input.Passages.Count
            || publication.Persistence.AdmissionCount != input.Passages.Count
            || admittedCount < 1
            || scenario.Extraction.ContradictionEvidenceIds.Count == 0
            || scenario.Extraction.Abstentions.Count == 0
            || scenario.Extraction.Gaps.Count == 0)
        {
            throw new InvalidDataException(
                "WP10 host admission did not durably retain the total semantic matrix and its admitted source claims.");
        }
        Dictionary<string, int> passageOrder = input.Passages.Select((item, index) => (item.PassageId, index))
            .ToDictionary(item => item.PassageId, item => item.index, StringComparer.Ordinal);
        Dictionary<string, string> proposalPassages = transcript.Proposals.ToDictionary(
            item => item.ProposalId, item => item.PassageId, StringComparer.Ordinal);
        SourceClaimAdmissionCorrelationContract admitted = scenario.Extraction.AdmissionCorrelations
            .Where(item => item.State == ProposalAdmissionState.Admitted)
            .OrderBy(item => passageOrder[proposalPassages[item.ProposalId.Value]])
            .First();
        SourceClaimCampaignIdentity semanticIdentity = identities[admitted.ProposalId.Value];
        SourceClaimApplicationReadModel consumed = store.ReadSourceClaimApplicationLinks(input.AcquisitionRunId)
            .Single(link => link.ApplicationLinkId == semanticIdentity.ApplicationLinkId);
        if (consumed.AdmittedArtifactId != semanticIdentity.AdmittedArtifactId
            || consumed.AdmissionId != semanticIdentity.AdmissionId)
        {
            throw new InvalidDataException("WP10 admitted claim did not produce the exact WP11 provenance handoff.");
        }
        return new("infinium.host.source-claim-admission/v1", scenario.Disposition,
            publication.Persistence.ProposalCount, admittedCount,
            semanticResultSha256, new(input.AcquisitionRunId,
                consumed.AdmissionId, consumed.AdmittedArtifactId, consumed.ApplicationLinkId,
                "", "", ""));
    }

    private static SourceClaimCampaignIdentity CampaignSourceIdentity(string proposalId)
    {
        if (!BoundedOpaqueId(proposalId))
        { throw new InvalidDataException("WP10 proposal identity is not bounded."); }
        string suffix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(proposalId)))[..24];
        return new("wp10-source-admission-" + suffix, "wp10-artifact-" + suffix,
            "wp10-application-link-" + suffix);
    }

    private static void MaterializeSourceRevision(AuthoritativeStore store, SourceClaimExecutionInput input,
        DateTimeOffset occurredAt)
    {
        foreach (SourceClaimPassageInput passage in input.Passages.Where(item => !item.Deleted))
        {
            byte[] payload = Encoding.UTF8.GetBytes(passage.Text);
            string sha = Convert.ToHexStringLower(SHA256.HashData(payload));
            if (sha != passage.TextSha256)
            {
                throw new InvalidDataException("WP10 prerequisite passage bytes differ from their frozen digest.");
            }
            string directory = Path.Combine(store.Paths.Payloads, sha[..2], sha[2..4]);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, sha);
            if (!File.Exists(path))
            {
                using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.WriteThrough);
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }
        }
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO documentation_revisions VALUES(
              $revision,$source,'fixture','1',NULL,NULL,$sha,0,
              'unavailable','unavailable','unavailable',$now)
            ON CONFLICT(documentation_revision_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$revision", input.SourceRevisionId);
        command.Parameters.AddWithValue("$source", "source-" + input.SourceRevisionId);
        command.Parameters.AddWithValue("$sha", input.Passages[0].TextSha256);
        command.Parameters.AddWithValue("$now", occurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
        foreach (SourceClaimPassageInput passage in input.Passages.Where(item => !item.Deleted))
        {
            byte[] payload = Encoding.UTF8.GetBytes(passage.Text);
            command.Parameters.Clear();
            command.CommandText =
                """
                INSERT INTO payloads VALUES(
                  $payload,$sha,$bytes,'text/plain','retained',
                  'payloads/' || substr($sha,1,2) || '/' || substr($sha,3,2) || '/' || $sha,$now)
                ON CONFLICT(content_sha256) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$payload", "wp10-passage-" + passage.PassageId);
            command.Parameters.AddWithValue("$sha", passage.TextSha256);
            command.Parameters.AddWithValue("$bytes", payload.LongLength);
            command.Parameters.AddWithValue("$now", occurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            _ = command.ExecuteNonQuery();
        }
        transaction.Commit();
        command.Transaction = null;
        command.Parameters.Clear();
        command.CommandText =
            """
            SELECT source_id,content_sha256,availability_state
              FROM documentation_revisions WHERE documentation_revision_id=$revision;
            """;
        command.Parameters.AddWithValue("$revision", input.SourceRevisionId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("WP10 source revision did not resolve to one exact authoritative root: absent, database="
                + store.Paths.Database);
        }
        string actualSource = reader.GetString(0);
        string actualSha = reader.GetString(1);
        string actualAvailability = reader.GetString(2);
        if (reader.Read() || actualSource != "source-" + input.SourceRevisionId
            || actualSha != input.Passages[0].TextSha256 || actualAvailability != "unavailable")
        {
            throw new InvalidDataException("WP10 source revision identity drift: source=" + actualSource
                + ", sha=" + actualSha + ", availability=" + actualAvailability);
        }
    }

    private static void RequireSourceRevision(AuthoritativeStore store, SourceClaimExecutionInput input)
    {
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT source_id,content_sha256,availability_state FROM documentation_revisions "
            + "WHERE documentation_revision_id=$revision;";
        command.Parameters.AddWithValue("$revision", input.SourceRevisionId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetString(0) != "source-" + input.SourceRevisionId
            || reader.GetString(1) != input.Passages[0].TextSha256
            || reader.GetString(2) != "unavailable" || reader.Read())
        {
            throw new InvalidDataException("WP10 response cannot create or repair its answer-free source prerequisite.");
        }
    }

    private static M1Slice6CampaignSemanticAdmissionReceipt AdmitCandidate(
        AuthoritativeStore store, string inputJson, string outputJson, string rawResponseSha256,
        M1Slice6CampaignAccountingAdmission admission, DateTimeOffset occurredAt)
    {
        M1Slice6CampaignCandidateInput campaignInput = RebindCandidateSourceApplication(
            store, M1Slice6CampaignV2InputAdapter.ReadCandidate(inputJson));
        CandidateInvestigationExecutionInput input = campaignInput.ProductInput;
        M1Slice6CampaignTranscriptEnvelope<CandidateInvestigationRetainedTranscript> envelope =
            JsonSerializer.Deserialize<M1Slice6CampaignTranscriptEnvelope<CandidateInvestigationRetainedTranscript>>(
                outputJson, SourceClaimContextMinimizer.JsonOptions)
            ?? throw new InvalidDataException("WP11 output is not an exact retained transcript envelope.");
        if (envelope.SchemaId != "infinium.llm.candidate-investigation-retained-transcripts/v1"
            || envelope.SchemaVersion != "1" || envelope.Transcripts.Count != 2
            || input.OperationId != SemanticOperationId(admission)
            || input.HostAuthorizationId != SemanticAuthorizationId(admission))
        {
            throw new InvalidDataException("WP11 input/output identities differ from the authoritative provider admission.");
        }
        if (!envelope.Transcripts.Select(item => item.ContextId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(input.Contexts.Select(item => item.ContextId))
            || envelope.Transcripts.Any(item => item.ResponseRecordId != "m1s6-campaign-stage-3-response"))
        {
            throw new InvalidDataException("WP11 transcripts did not total the exact contexts or response record.");
        }
        CandidateInvestigationRetainedTranscript[] retainedTranscripts = envelope.Transcripts
            .Select(item => item with { ResponseFingerprint = rawResponseSha256 }).ToArray();
        string semanticResultSha256 = CandidateActualSemanticResultSha256(campaignInput, retainedTranscripts);
        DurableCandidateInvestigationCoordinator coordinator = new(store);
        List<CandidateInvestigationAdmissionPublication> publications =
            store.ExecuteCandidateInvestigationBatch(() =>
        {
            List<CandidateInvestigationAdmissionPublication> staged = [];
            foreach (CandidateInvestigationRetainedTranscript transcript in retainedTranscripts)
            {
                M1Slice6CampaignEvidenceRoot root = campaignInput.RootsByContext[transcript.ContextId];
                RequireCandidateRoots(store, input, transcript.ContextId, root);
                staged.Add(coordinator.AdmitRetainedTranscript(
                    input, transcript, SemanticAuthorizationId(admission), admission.AttemptId,
                    admission.RequestId, admission.DispatchFenceId, occurredAt,
                    campaignInput.ExactV2Bytes, root,
                    campaignInput.LocalObservationsByContext[transcript.ContextId]));
            }
            return staged;
        });
        foreach (CandidateInvestigationAdmissionPublication publication in publications)
        {
            CandidateInvestigationScenarioResult replay = coordinator.ReplayRetained(
                input.AnalysisRunId, input.OperationId, publication.Scenario.ContextId);
            if (replay.CanonicalInvestigationSha256 != publication.Scenario.CanonicalInvestigationSha256)
            {
                throw new InvalidDataException("WP11 authoritative replay differs from an admitted candidate result.");
            }
        }
        CandidateInvestigationAdmissionPublication positive = publications.Single(item =>
            item.Scenario.Disposition is "accepted" or "accepted-conditional");
        CandidateInvestigationAdmissionPublication negative = publications.Single(item =>
            item.Scenario.Disposition == "empty-abstained");
        M1Slice6CampaignEvidenceRoot positiveRoot = campaignInput.RootsByContext[positive.Scenario.ContextId];
        M1Slice6CampaignEvidenceRoot negativeRoot = campaignInput.RootsByContext[negative.Scenario.ContextId];
        if (positive.Scenario.Disposition is not ("accepted" or "accepted-conditional")
            || negative.Scenario.Disposition != "empty-abstained"
            || positiveRoot.Kind != M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication
            || negativeRoot.Kind != M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence
            || positive.Scenario.SourceAcquisitionLinks.Count != 1
            || negative.Scenario.SourceAcquisitionLinks.Count != 0
            || positive.Persistence.ProposalCount != 1 || positive.Persistence.AdmissionCount != 1
            || negative.Persistence.ProposalCount != 0 || negative.Persistence.AdmissionCount != 0)
        {
            throw new InvalidDataException("WP11 host admission did not produce one accepted and one explicitly abstained context.");
        }
        CandidateInvestigationContextInput context = input.Contexts.Single(item =>
            item.ContextId == positive.Scenario.ContextId);
        CandidateEvidenceInput evidence = context.Evidence.Single();
        return new("infinium.host.candidate-investigation-admission/v1",
            "accepted-conditional", 1, 1,
            semanticResultSha256, new(
                evidence.SourceAcquisitionId, evidence.SourceAdmissionId,
                store.ReadSourceClaimApplicationLinks(evidence.SourceAcquisitionId)
                    .Single(link => link.ApplicationLinkId == evidence.SourceApplicationLinkId).AdmittedArtifactId,
                evidence.SourceApplicationLinkId, evidence.EvidenceApplicationLinkId,
                context.CandidateId, context.HypothesisId));
    }

    private static string SourceActualSemanticResultSha256(
        SourceClaimExecutionInput input, SourceClaimRetainedTranscript transcript)
    {
        Dictionary<string, SourceClaimTranscriptProposal> proposals = transcript.Proposals
            .ToDictionary(item => item.PassageId, StringComparer.Ordinal);
        if (proposals.Count != input.Passages.Count
            || transcript.Proposals.Select(item => item.ProposalId).Distinct(StringComparer.Ordinal).Count()
                != transcript.Proposals.Count
            || !proposals.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(input.Passages.Select(item => item.PassageId))
            || proposals.Values.Any(proposal => !BoundedOpaqueId(proposal.ProposalId)))
        {
            throw new InvalidDataException("WP10 retained semantic result does not total every input passage.");
        }
        object projection = new
        {
            transcript.OperationId,
            transcript.SourceRevisionId,
            proposals = input.Passages.Select(item =>
            {
                SourceClaimTranscriptProposal proposal = proposals[item.PassageId];
                return new
                {
                    proposal_id = proposal.State == "proposed" ? proposal.ProposalId : null,
                    proposal.PassageId,
                    proposal.Claim,
                    proposal.ClaimKind,
                    proposal.ConditionIds,
                    proposal.ConditionScope,
                    proposal.AuthorityCategory,
                    proposal.ApplicationSemantics,
                    proposal.State,
                    proposal.Reason,
                };
            }).ToArray(),
            contradiction_evidence_ids = transcript.ContradictionEvidenceIds,
            abstentions = transcript.Abstentions,
            gaps = transcript.Gaps,
        };
        return Convert.ToHexStringLower(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(projection, SourceClaimContextMinimizer.JsonOptions)));
    }

    private static string CandidateActualSemanticResultSha256(
        M1Slice6CampaignCandidateInput campaignInput,
        IReadOnlyList<CandidateInvestigationRetainedTranscript> transcripts)
    {
        CandidateInvestigationExecutionInput input = campaignInput.ProductInput;
        Dictionary<string, CandidateInvestigationRetainedTranscript> byContext = transcripts
            .ToDictionary(item => item.ContextId, StringComparer.Ordinal);
        if (byContext.Count != input.Contexts.Count
            || !byContext.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(input.Contexts.Select(item => item.ContextId)))
        {
            throw new InvalidDataException("WP11 retained semantic result does not total every input context.");
        }
        using JsonDocument exactInput = JsonDocument.Parse(campaignInput.ExactV2Bytes);
        Dictionary<string, JsonElement> exactContexts = exactInput.RootElement.GetProperty("contexts")
            .EnumerateArray().ToDictionary(
                item => item.GetProperty("context_id").GetString()!, item => item, StringComparer.Ordinal);
        if (byContext.Values.SelectMany(item => item.Proposals).Any(proposal =>
                !BoundedOpaqueId(proposal.ProposalId)))
        {
            throw new InvalidDataException("WP11 retained proposal identity is absent or unbounded.");
        }
        object projection = new
        {
            operation_id = input.OperationId,
            contexts = input.Contexts.Select(context =>
            {
                CandidateInvestigationRetainedTranscript transcript = byContext[context.ContextId];
                JsonElement exactContext = exactContexts[context.ContextId];
                return new
                {
                    context_id = context.ContextId,
                    candidate_id = context.CandidateId,
                    hypothesis_id = context.HypothesisId,
                    hypothesis = context.Hypothesis,
                    local_observations = JsonSerializer.Deserialize<object>(
                        exactContext.GetProperty("local_observations").GetRawText()),
                    evidence = JsonSerializer.Deserialize<object>(
                        exactContext.GetProperty("evidence").GetRawText()),
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
                        proposal.Reason,
                    }).ToArray(),
                    transcript.Abstentions,
                    transcript.Gaps,
                };
            }).ToArray(),
        };
        return Convert.ToHexStringLower(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(projection, SourceClaimContextMinimizer.JsonOptions)));
    }

    private static bool BoundedOpaqueId(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256 && !value.Any(char.IsControl);

    private static M1Slice6CampaignCandidateInput RebindCandidateSourceApplication(
        AuthoritativeStore store, M1Slice6CampaignCandidateInput campaignInput)
    {
        M1Slice6CampaignEvidenceRoot sourceRoot = campaignInput.RootsByContext.Values.Single(root =>
            root.Kind == M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication);
        SourceClaimResolvedApplicationReadModel resolved = store.ResolveSourceClaimApplication(
            sourceRoot.AcquisitionRunId, sourceRoot.SourceRevisionId, sourceRoot.PassageId,
            sourceRoot.ContentSha256);
        if (resolved.AcquisitionRunId != sourceRoot.AcquisitionRunId
            || resolved.SourceRevisionId != sourceRoot.SourceRevisionId
            || resolved.PassageId != sourceRoot.PassageId
            || resolved.ContentSha256 != sourceRoot.ContentSha256)
        { throw new InvalidDataException("The WP11 source application changed its answer-free evidence bytes."); }
        if (sourceRoot.ProposalId == resolved.ProposalId
            && sourceRoot.SourceAdmissionId == resolved.AdmissionId
            && sourceRoot.AdmittedArtifactId == resolved.AdmittedArtifactId
            && sourceRoot.ApplicationLinkId == resolved.ApplicationLinkId)
        { return campaignInput; }
        JsonNode rebound = JsonNode.Parse(campaignInput.ExactV2Bytes)
            ?? throw new InvalidDataException("The WP11 answer-free input cannot be rebound.");
        JsonObject context = rebound["contexts"]!.AsArray().Select(item => item!.AsObject())
            .Single(item => item["context_id"]!.GetValue<string>() == sourceRoot.ContextId);
        JsonObject host = context["evidence"]![0]!["host_bindings"]!.AsObject();
        host["proposal_id"] = resolved.ProposalId;
        host["source_admission_id"] = resolved.AdmissionId;
        host["admitted_artifact_id"] = resolved.AdmittedArtifactId;
        host["application_link_id"] = resolved.ApplicationLinkId;
        M1Slice6CampaignCandidateInput result = M1Slice6CampaignV2InputAdapter.ReadCandidate(
            rebound.ToJsonString(SourceClaimContextMinimizer.JsonOptions));
        M1Slice6CampaignEvidenceRoot reboundRoot = result.RootsByContext[sourceRoot.ContextId];
        if (reboundRoot.SourceRevisionId != sourceRoot.SourceRevisionId
            || reboundRoot.PassageId != sourceRoot.PassageId
            || reboundRoot.ContentSha256 != sourceRoot.ContentSha256
            || result.ProductInput.Contexts.Select(contextItem => (contextItem.ContextId,
                contextItem.CandidateId, contextItem.HypothesisId, contextItem.Hypothesis))
                .SequenceEqual(campaignInput.ProductInput.Contexts.Select(contextItem => (
                    contextItem.ContextId, contextItem.CandidateId, contextItem.HypothesisId,
                    contextItem.Hypothesis))) is false)
        { throw new InvalidDataException("WP11 provenance rebinding changed semantic input."); }
        return result;
    }

    private static void MaterializeCandidateRoots(AuthoritativeStore store,
        CandidateInvestigationExecutionInput input,
        CandidateInvestigationContextInput context, M1Slice6CampaignEvidenceRoot root,
        DateTimeOffset occurredAt, SqliteConnection connection, SqliteTransaction transaction)
    {
        if (context.Evidence.Count != 1)
        {
            throw new InvalidDataException("WP11 live candidate context requires one exact admitted evidence root.");
        }
        CandidateEvidenceInput evidence = context.Evidence[0];
        if (root.Kind == M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication)
        {
            SourceClaimApplicationReadModel application = store.ReadSourceClaimApplicationLinks(
                root.AcquisitionRunId).SingleOrDefault(link =>
                    link.ApplicationLinkId == root.ApplicationLinkId
                    && link.AdmissionId == root.SourceAdmissionId
                    && link.AdmittedArtifactId == root.AdmittedArtifactId
                    && link.ApplicationScopeId == input.ApplicationScopeId)
                ?? throw new InvalidDataException(
                    "WP11 prerequisite does not consume the exact admitted WP10 artifact.");
            if (string.IsNullOrWhiteSpace(application.ParentAnalysisRunId)
                || string.IsNullOrWhiteSpace(application.CostAttributionScopeId))
            {
                throw new InvalidDataException("WP11 persisted WP10 application omitted its owner or cost binding.");
            }
        }
        else if (root.AcquisitionRunId.Length != 0 || root.SourceAdmissionId.Length != 0
            || root.ApplicationLinkId.Length != 0)
        {
            throw new InvalidDataException("WP11 frozen host evidence cannot fabricate a parallel WP10 chain.");
        }
        (string payloadId, byte[] payload) = ReadPayloadBySha(store, evidence.ContentSha256);
        string payloadSha = Convert.ToHexStringLower(SHA256.HashData(payload));
        string payloadDirectory = Path.Combine(store.Paths.Payloads, payloadSha[..2], payloadSha[2..4]);
        Directory.CreateDirectory(payloadDirectory);
        string payloadPath = Path.Combine(payloadDirectory, payloadSha);
        if (!File.Exists(payloadPath))
        {
            using FileStream stream = new(payloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough);
            stream.Write(payload);
            stream.Flush(flushToDisk: true);
        }
        string candidatePayloadId = context.ContextId + "-host-payload";
        string decisionId = context.ContextId + "-decision";
        string dependencyId = context.ContextId + "-dependency";
        string documentApplicationId = context.ContextId + "-document-application";
        byte[] candidatePayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            originating_run_id = input.AnalysisRunId,
            candidates = new[] { new { candidate_id = context.CandidateId,
                decision_id = decisionId, hypothesis_id = context.HypothesisId } },
            hypotheses = new[] { new { hypothesis_id = context.HypothesisId,
                candidate_id = context.CandidateId, proposed_explanation = context.Hypothesis,
                supporting_evidence_ids = context.Evidence.Where(item => item.Relationship == "supporting")
                    .Select(item => item.EvidenceId).ToArray(),
                contradicting_evidence_ids = context.Evidence.Where(item => item.Relationship == "contradicting")
                    .Select(item => item.EvidenceId).ToArray() } },
            decisions = new[] { new { decision_id = decisionId,
                dependency_closure_id = context.DependencyClosureId,
                participants = context.ParticipantIds.Zip(context.ParticipantRoles,
                    (id, role) => new { participant_id = id, role }).ToArray(),
                path = context.CausalPathIds, dependency_ids = new[] { dependencyId } } },
            dependency_edges = new[] { new { from_kind = "dependency-closure",
                from_id = context.DependencyClosureId, to_kind = "dependency",
                to_id = dependencyId, edge_kind = "depends-on" } },
        });
        string candidatePayloadSha = Convert.ToHexStringLower(SHA256.HashData(candidatePayload));
        string candidatePayloadDirectory = Path.Combine(store.Paths.Payloads,
            candidatePayloadSha[..2], candidatePayloadSha[2..4]);
        Directory.CreateDirectory(candidatePayloadDirectory);
        string candidatePayloadPath = Path.Combine(candidatePayloadDirectory, candidatePayloadSha);
        if (!File.Exists(candidatePayloadPath))
        {
            using FileStream stream = new(candidatePayloadPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough);
            stream.Write(candidatePayload);
            stream.Flush(flushToDisk: true);
        }
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        string payloadAndCandidateInsert =
            """
            INSERT INTO payloads VALUES(
              $payload,$sha,$bytes,'application/json','retained',
              'payloads/' || substr($sha,1,2) || '/' || substr($sha,3,2) || '/' || $sha,$now)
            ON CONFLICT(content_sha256) DO NOTHING;
            INSERT INTO payloads VALUES(
              $candidatePayload,$candidatePayloadSha,$candidatePayloadBytes,'application/json','retained',
              'payloads/' || substr($candidatePayloadSha,1,2) || '/' || substr($candidatePayloadSha,3,2) || '/' || $candidatePayloadSha,$now);
            """;
        string evidenceInsert = root.Kind == M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication
            ?
            """
            INSERT INTO documentation_imports VALUES(
              $sourceImport,$run,$revision,'clean-import',NULL,
              'm1s6-campaign-source-closure','campaign-source-extractor','none','none',$payload,$payload,$now);
            INSERT INTO documentation_passages VALUES(
              $passage,$revision,0,$bytes,$sha,$payload,'present',$now);
            INSERT INTO evidence_revisions VALUES(
              $evidence,$passage,$sourceImport,'infinium.evidence.campaign/v1','1',
              'documentation-claim','requirement','authoritative-external','applicable','established',
              'admitted',$payload,NULL,$now);
            """
            :
            """
            INSERT INTO documentation_imports(
              documentation_import_id,import_run_id,documentation_revision_id,import_mode,reused_import_id,
              dependency_closure_id,extractor_id,llm_involvement,llm_operation,boundaries_payload_id,
              import_payload_id,created_at)
            SELECT $import,$run,documentation_revision_id,'retained-reuse',documentation_import_id,
              dependency_closure_id,extractor_id,llm_involvement,llm_operation,boundaries_payload_id,
              import_payload_id,$now
            FROM documentation_imports WHERE documentation_import_id=$sourceImport;
            INSERT INTO evidence_revisions VALUES(
              $evidence,NULL,$import,'infinium.evidence.campaign-host-observation/v1','1',
              'local-observation',NULL,'snapshot-bound-local','contradicted',NULL,
              'admitted',$payload,NULL,$now);
            """;
        string applicationAndCandidateInsert =
            """
            INSERT INTO documentation_application_bindings VALUES(
              $documentApplication,$run,'m1s6-campaign-stage-3-install',
              $context,'m1s6-campaign-stage-3-manifest',$candidate,
              'installed-entity',$closure,$now);
            INSERT INTO evidence_application_links VALUES(
              $evidenceApplication,$evidence,$run,$documentApplication,
              $context,$candidate,'installed-entity',$closure,
              'applicable',$payload,$now);
            INSERT INTO candidate_decisions VALUES(
              $decision,$run,'m1s6-campaign-population',
              'm1s6-campaign-relationship','candidate-admitted','optional-ranked',
              'm1s6-campaign-rule/v1',$candidatePayload,$now);
            INSERT INTO analysis_candidates VALUES(
              $candidate,$decision,$run,'optional-ranked','present',
              $closure,$candidatePayload,$now);
            INSERT INTO analysis_hypotheses VALUES(
              $hypothesis,$candidate,$run,'present','plausible',
              'm1s6-campaign-threshold',$candidatePayload,$now);
            """;
        command.CommandText = payloadAndCandidateInsert + evidenceInsert + applicationAndCandidateInsert;
        command.Parameters.AddWithValue("$payload", payloadId);
        command.Parameters.AddWithValue("$sha", payloadSha);
        command.Parameters.AddWithValue("$bytes", payload.LongLength);
        command.Parameters.AddWithValue("$candidatePayload", candidatePayloadId);
        command.Parameters.AddWithValue("$candidatePayloadSha", candidatePayloadSha);
        command.Parameters.AddWithValue("$candidatePayloadBytes", candidatePayload.LongLength);
        command.Parameters.AddWithValue("$run", input.AnalysisRunId);
        command.Parameters.AddWithValue("$sourceImport",
            input.AnalysisRunId + "-" + evidence.SourceRevisionId + "-source-import");
        command.Parameters.AddWithValue("$import", context.ContextId + "-source-import");
        command.Parameters.AddWithValue("$revision", evidence.SourceRevisionId);
        command.Parameters.AddWithValue("$passage", evidence.PassageId);
        command.Parameters.AddWithValue("$evidence", evidence.EvidenceId);
        command.Parameters.AddWithValue("$evidenceApplication", evidence.EvidenceApplicationLinkId);
        command.Parameters.AddWithValue("$documentApplication", documentApplicationId);
        command.Parameters.AddWithValue("$context", context.ContextId);
        command.Parameters.AddWithValue("$decision", decisionId);
        command.Parameters.AddWithValue("$candidate", context.CandidateId);
        command.Parameters.AddWithValue("$hypothesis", context.HypothesisId);
        command.Parameters.AddWithValue("$closure", context.DependencyClosureId);
        command.Parameters.AddWithValue("$now", occurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
    }

    private static void RequireCandidateRoots(AuthoritativeStore store,
        CandidateInvestigationExecutionInput input, string contextId, M1Slice6CampaignEvidenceRoot root)
    {
        CandidateInvestigationContextInput context = input.Contexts.Single(item => item.ContextId == contextId);
        CandidateEvidenceInput evidence = context.Evidence.Single();
        SourceClaimApplicationReadModel? application = root.Kind == M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication
            ? store.ReadSourceClaimApplicationLinks(root.AcquisitionRunId).SingleOrDefault(link =>
                link.ApplicationLinkId == root.ApplicationLinkId
                && link.AdmissionId == root.SourceAdmissionId
                && link.AdmittedArtifactId == root.AdmittedArtifactId
                && link.ApplicationScopeId == input.ApplicationScopeId
                && !string.IsNullOrWhiteSpace(link.ParentAnalysisRunId)
                && !string.IsNullOrWhiteSpace(link.CostAttributionScopeId))
            : null;
        if (application is not null)
        {
            SourceClaimExtractionDocument sourceMatrix = store.ReadSourceClaimExtraction(
                root.AcquisitionRunId, root.SourceAdmissionId);
            if (sourceMatrix.ClaimProposals.Count != 9
                || sourceMatrix.AdmissionCorrelations.Count != 9
                || !sourceMatrix.AdmissionCorrelations.Any(item =>
                    item.AdmissionId.Value == root.SourceAdmissionId
                    && item.State == ProposalAdmissionState.Admitted)
                || sourceMatrix.ContradictionEvidenceIds.Count == 0
                || sourceMatrix.Abstentions.Count == 0 || sourceMatrix.Gaps.Count == 0)
            {
                throw new InvalidDataException(
                    "WP11 prerequisite cannot reopen the full durable WP10 semantic matrix.");
            }
        }
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM evidence_application_links WHERE evidence_application_link_id=$evidenceApplication; "
            + "SELECT COUNT(*) FROM analysis_candidates WHERE candidate_id=$candidate; "
            + "SELECT COUNT(*) FROM analysis_hypotheses WHERE hypothesis_id=$hypothesis AND candidate_id=$candidate;";
        command.Parameters.AddWithValue("$evidenceApplication", evidence.EvidenceApplicationLinkId);
        command.Parameters.AddWithValue("$candidate", context.CandidateId);
        command.Parameters.AddWithValue("$hypothesis", context.HypothesisId);
        using SqliteDataReader reader = command.ExecuteReader();
        long evidenceCount = reader.Read() ? reader.GetInt64(0) : 0;
        _ = reader.NextResult();
        long candidateCount = reader.Read() ? reader.GetInt64(0) : 0;
        _ = reader.NextResult();
        long hypothesisCount = reader.Read() ? reader.GetInt64(0) : 0;
        if (root.Kind == M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication
                && application is null
            || evidenceCount != 1 || candidateCount != 1 || hypothesisCount != 1)
        {
            throw new InvalidDataException("WP11 response cannot fabricate its admitted-source or candidate roots.");
        }
    }

    private static (string PayloadId, byte[] Bytes) ReadPayloadBySha(
        AuthoritativeStore store, string sha256)
    {
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT payload_id FROM payloads WHERE content_sha256=$sha;";
        command.Parameters.AddWithValue("$sha", sha256);
        string payloadId = command.ExecuteScalar() as string
            ?? throw new InvalidDataException("WP11 frozen evidence payload was not retained by WP10 prerequisites.");
        string path = Path.Combine(store.Paths.Payloads, sha256[..2], sha256[2..4], sha256);
        byte[] bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != sha256)
        {
            throw new InvalidDataException("WP11 frozen evidence payload bytes drifted from their retained digest.");
        }
        return (payloadId, bytes);
    }

    private static string SemanticOperationId(M1Slice6CampaignAccountingAdmission admission) =>
        string.IsNullOrEmpty(admission.SemanticOperationId)
            ? admission.OperationId : admission.SemanticOperationId;

    private static string SemanticAuthorizationId(M1Slice6CampaignAccountingAdmission admission) =>
        string.IsNullOrEmpty(admission.SemanticAuthorizationId)
            ? admission.AuthorizationId : admission.SemanticAuthorizationId;

    internal static string ExtractUntrustedInput(ReadOnlySpan<byte> canonicalRequest)
    {
        using JsonDocument request = JsonDocument.Parse(canonicalRequest.ToArray());
        JsonElement[] messages = request.RootElement.GetProperty("input").EnumerateArray().ToArray();
        if (messages.Length != 2 || messages[1].GetProperty("role").GetString() != "user")
        {
            throw new InvalidDataException("The canonical request has no exact user evidence message.");
        }
        JsonElement[] parts = messages[1].GetProperty("content").EnumerateArray().ToArray();
        if (parts.Length != 1 || parts[0].GetProperty("type").GetString() != "input_text")
        {
            throw new InvalidDataException("The canonical request user message is not one exact input_text part.");
        }
        string content = parts[0].GetProperty("text").GetString()
            ?? throw new InvalidDataException("The canonical request user message is absent.");
        const string prefix = "BEGIN_UNTRUSTED_EVIDENCE\n";
        const string suffix = "\nEND_UNTRUSTED_EVIDENCE";
        if (!content.StartsWith(prefix, StringComparison.Ordinal)
            || !content.EndsWith(suffix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The canonical request evidence framing changed.");
        }
        return content[prefix.Length..^suffix.Length];
    }

    internal static string ExtractOutputText(ReadOnlySpan<byte> rawResponse)
    {
        using JsonDocument response = JsonDocument.Parse(rawResponse.ToArray());
        JsonElement[] output = response.RootElement.GetProperty("output").EnumerateArray().ToArray();
        if (output.Length != 1)
        {
            throw new InvalidDataException("The retained response has no unique output message.");
        }
        JsonElement[] content = output[0].GetProperty("content").EnumerateArray().ToArray();
        if (content.Length != 1 || content[0].GetProperty("type").GetString() != "output_text")
        {
            throw new InvalidDataException("The retained response has no unique output_text payload.");
        }
        return content[0].GetProperty("text").GetString()
            ?? throw new InvalidDataException("The retained response output_text is absent.");
    }

    internal static string ExtractSuccessorV6OutputText(ReadOnlySpan<byte> rawResponse)
    {
        using JsonDocument response = JsonDocument.Parse(rawResponse.ToArray());
        JsonElement[] output = response.RootElement.GetProperty("output").EnumerateArray().ToArray();
        JsonElement[] messages = output.Where(item =>
            item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("type", out JsonElement type)
            && type.GetString() == "message").ToArray();
        if (messages.Length != 1 || output.Any(item =>
                item.GetProperty("type").GetString() is not ("reasoning" or "message")))
        {
            throw new InvalidDataException("The successor response has no unique output message.");
        }
        JsonElement message = messages[0];
        if (message.TryGetProperty("phase", out JsonElement phase)
            && phase.GetString() != "final_answer")
        {
            throw new InvalidDataException("The successor response message phase is not final_answer.");
        }
        JsonElement[] content = message.GetProperty("content").EnumerateArray().ToArray();
        if (content.Length != 1 || content[0].GetProperty("type").GetString() != "output_text")
        {
            throw new InvalidDataException("The successor response has no unique output_text payload.");
        }
        return content[0].GetProperty("text").GetString()
            ?? throw new InvalidDataException("The successor response output_text is absent.");
    }
}
