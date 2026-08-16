using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.OpenAI;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Coordinator;

internal sealed record M1Slice6CampaignTranscriptEnvelope<T>(
    string SchemaId, string SchemaVersion, IReadOnlyList<T> Transcripts);

internal sealed record M1Slice6CampaignSemanticAdmissionReceipt(
    string ValidationId, string Disposition, int ProposalCount, int AdmissionCount,
    string ResultSha256);

/// <summary>
/// Host-owned semantic boundary for the finite campaign. Provider output is only an untrusted
/// retained transcript. This boundary rehydrates the exact answer-free product input from the
/// canonical request, applies the existing deterministic host policy, and persists the admitted
/// proposal graph in the same authoritative SQLite store as the request and settlement.
/// </summary>
internal static class M1Slice6CampaignSemanticAdmission
{
    internal static M1Slice6CampaignSemanticAdmissionReceipt Admit(
        AuthoritativeStore store, M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignAccountingAdmission admission, OpenAiResponsesResult response,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(store);
        string inputJson = ExtractUntrustedInput(authority.CanonicalRequest);
        byte[] rawResponse = response.RawResponseBytes
            ?? throw new InvalidDataException("Semantic admission requires retained raw response bytes.");
        string rawResponseSha256 = Convert.ToHexStringLower(SHA256.HashData(rawResponse));
        string outputJson = ExtractOutputText(rawResponse);
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
        return new("qualification-nonsemantic", "not-applicable", 0, 0, new string('0', 64));
    }

    private static M1Slice6CampaignSemanticAdmissionReceipt AdmitSourceClaim(
        AuthoritativeStore store, string inputJson, string outputJson, string rawResponseSha256,
        M1Slice6CampaignAccountingAdmission admission, DateTimeOffset occurredAt)
    {
        SourceClaimExecutionInput input = JsonSerializer.Deserialize<SourceClaimExecutionInput>(
            inputJson, SourceClaimContextMinimizer.JsonOptions)
            ?? throw new InvalidDataException("WP10 output has no exact source-claim product input.");
        M1Slice6CampaignTranscriptEnvelope<SourceClaimRetainedTranscript> envelope =
            JsonSerializer.Deserialize<M1Slice6CampaignTranscriptEnvelope<SourceClaimRetainedTranscript>>(
                outputJson, SourceClaimContextMinimizer.JsonOptions)
            ?? throw new InvalidDataException("WP10 output is not an exact retained transcript envelope.");
        if (envelope.SchemaId != "infinium.llm.source-claim-retained-transcripts/v1"
            || envelope.SchemaVersion != "1" || envelope.Transcripts.Count != 1
            || input.OperationId != admission.OperationId
            || input.HostAuthorizationId != admission.AuthorizationId)
        {
            throw new InvalidDataException("WP10 input/output identities differ from the authoritative provider admission.");
        }
        SourceClaimRetainedTranscript transcript = envelope.Transcripts[0] with
        {
            ResponseFingerprint = rawResponseSha256,
        };
        if (transcript.ResponseRecordId != "m1s6-campaign-stage-2-response")
        {
            throw new InvalidDataException("WP10 transcript did not bind the authoritative response record.");
        }
        EnsureSourceRevision(store, input, occurredAt.AddTicks(-1));
        SourceClaimAdmissionPublication publication = new SourceClaimAcquisitionCoordinator(store)
            .AdmitRetainedTranscript(input, transcript, admission.AuthorizationId,
                admission.AttemptId, admission.RequestId, admission.DispatchFenceId, occurredAt);
        SourceClaimScenarioResult scenario = publication.Scenario;
        if (scenario.Disposition is not ("accepted" or "accepted-conditional"
                or "accepted-conditional-applicability")
            || publication.Persistence.ProposalCount != 1
            || publication.Persistence.AdmissionCount != 1)
        {
            throw new InvalidDataException("WP10 host admission did not produce one exact admitted source claim.");
        }
        SourceClaimConsumptionReceipt consumed = store.ConsumeAdmittedSourceClaim(new(
            "m1s6-campaign-source-application", input.AcquisitionRunId,
            "admission-m1s6-campaign-source-proposal", input.ParentAnalysisRunId,
            input.ApplicationScopeId, input.CostAttributionScopeId, occurredAt.AddTicks(1)));
        if (consumed.AdmittedArtifactId.Length == 0)
        {
            throw new InvalidDataException("WP10 admitted claim did not produce the exact WP11 provenance handoff.");
        }
        byte[] result = publication.JsonTransparency;
        return new("infinium.host.source-claim-admission/v1", scenario.Disposition,
            publication.Persistence.ProposalCount, publication.Persistence.AdmissionCount,
            Convert.ToHexStringLower(SHA256.HashData(result)));
    }

    private static void EnsureSourceRevision(AuthoritativeStore store, SourceClaimExecutionInput input,
        DateTimeOffset occurredAt)
    {
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR IGNORE INTO documentation_revisions VALUES(
              $revision,$source,'fixture','1',NULL,NULL,$sha,0,
              'unavailable','unavailable','unavailable',$now);
            """;
        command.Parameters.AddWithValue("$revision", input.SourceRevisionId);
        command.Parameters.AddWithValue("$source", "source-" + input.SourceRevisionId);
        command.Parameters.AddWithValue("$sha", input.Passages[0].TextSha256);
        command.Parameters.AddWithValue("$now", occurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
        command.CommandText =
            """
            SELECT source_id,content_sha256,availability_state
              FROM documentation_revisions WHERE documentation_revision_id=$revision;
            """;
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

    private static M1Slice6CampaignSemanticAdmissionReceipt AdmitCandidate(
        AuthoritativeStore store, string inputJson, string outputJson, string rawResponseSha256,
        M1Slice6CampaignAccountingAdmission admission, DateTimeOffset occurredAt)
    {
        CandidateInvestigationExecutionInput input =
            JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
                inputJson, SourceClaimContextMinimizer.JsonOptions)
            ?? throw new InvalidDataException("WP11 output has no exact candidate product input.");
        M1Slice6CampaignTranscriptEnvelope<CandidateInvestigationRetainedTranscript> envelope =
            JsonSerializer.Deserialize<M1Slice6CampaignTranscriptEnvelope<CandidateInvestigationRetainedTranscript>>(
                outputJson, SourceClaimContextMinimizer.JsonOptions)
            ?? throw new InvalidDataException("WP11 output is not an exact retained transcript envelope.");
        if (envelope.SchemaId != "infinium.llm.candidate-investigation-retained-transcripts/v1"
            || envelope.SchemaVersion != "1" || envelope.Transcripts.Count != 1
            || input.OperationId != admission.OperationId
            || input.HostAuthorizationId != admission.AuthorizationId)
        {
            throw new InvalidDataException("WP11 input/output identities differ from the authoritative provider admission.");
        }
        CandidateInvestigationRetainedTranscript transcript = envelope.Transcripts[0] with
        {
            ResponseFingerprint = rawResponseSha256,
        };
        if (transcript.ResponseRecordId != "m1s6-campaign-stage-3-response")
        {
            throw new InvalidDataException("WP11 transcript did not bind the authoritative response record.");
        }
        EnsureCandidateRoots(store, input, transcript, occurredAt.AddTicks(-1));
        CandidateInvestigationAdmissionPublication publication =
            new DurableCandidateInvestigationCoordinator(store).AdmitRetainedTranscript(
                input, transcript, admission.AuthorizationId, admission.AttemptId,
                admission.RequestId, admission.DispatchFenceId, occurredAt);
        if (publication.Scenario.Disposition is not ("accepted" or "accepted-conditional")
            || publication.Persistence.ProposalCount != 1
            || publication.Persistence.AdmissionCount != 1)
        {
            throw new InvalidDataException("WP11 host admission did not produce one exact evidence-bound candidate proposal.");
        }
        CandidateInvestigationScenarioResult replay =
            new DurableCandidateInvestigationCoordinator(store).ReplayRetained(
                input.AnalysisRunId, input.OperationId, transcript.ContextId);
        if (replay.CanonicalInvestigationSha256 != publication.Scenario.CanonicalInvestigationSha256)
        {
            throw new InvalidDataException("WP11 authoritative replay differs from the admitted candidate result.");
        }
        return new("infinium.host.candidate-investigation-admission/v1",
            publication.Scenario.Disposition, publication.Persistence.ProposalCount,
            publication.Persistence.AdmissionCount,
            Convert.ToHexStringLower(SHA256.HashData(publication.JsonTransparency)));
    }

    private static void EnsureCandidateRoots(AuthoritativeStore store,
        CandidateInvestigationExecutionInput input,
        CandidateInvestigationRetainedTranscript transcript, DateTimeOffset occurredAt)
    {
        CandidateInvestigationContextInput context = input.Contexts.Single(item => item.ContextId == transcript.ContextId);
        if (context.Evidence.Count != 1)
        {
            throw new InvalidDataException("WP11 live candidate context requires one exact admitted evidence root.");
        }
        CandidateEvidenceInput evidence = context.Evidence[0];
        byte[] payload = Encoding.UTF8.GetBytes("Campaign source Alpha is enabled for the exact retained revision.");
        string payloadSha = Convert.ToHexStringLower(SHA256.HashData(payload));
        if (payloadSha != evidence.ContentSha256)
        {
            throw new InvalidDataException("WP11 candidate evidence bytes differ from the frozen product input.");
        }
        const string payloadId = "m1s6-campaign-evidence-payload";
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
        const string candidatePayloadId = "m1s6-campaign-candidate-host-payload";
        const string decisionId = "m1s6-campaign-candidate-decision";
        const string dependencyId = "m1s6-campaign-dependency";
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
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT OR IGNORE INTO payloads VALUES(
              $payload,$sha,$bytes,'application/json','retained',
              'payloads/' || substr($sha,1,2) || '/' || substr($sha,3,2) || '/' || $sha,$now);
            INSERT OR IGNORE INTO payloads VALUES(
              $candidatePayload,$candidatePayloadSha,$candidatePayloadBytes,'application/json','retained',
              'payloads/' || substr($candidatePayloadSha,1,2) || '/' || substr($candidatePayloadSha,3,2) || '/' || $candidatePayloadSha,$now);
            INSERT OR IGNORE INTO documentation_imports VALUES(
              'm1s6-campaign-source-import',$run,$revision,'clean-import',NULL,
              'm1s6-campaign-source-closure','campaign-source-extractor','none','none',$payload,$payload,$now);
            INSERT OR IGNORE INTO documentation_passages VALUES(
              $passage,$revision,0,$bytes,$sha,$payload,'present',$now);
            INSERT OR IGNORE INTO evidence_revisions VALUES(
              $evidence,$passage,'m1s6-campaign-source-import','infinium.evidence.campaign/v1','1',
              'documentation-claim','requirement','authoritative-external','applicable','established',
              'admitted',$payload,NULL,$now);
            INSERT OR IGNORE INTO documentation_application_bindings VALUES(
              'm1s6-campaign-document-application',$run,'m1s6-campaign-stage-3-install',
              'm1s6-campaign-stage-3-context','m1s6-campaign-stage-3-manifest',$candidate,
              'installed-entity',$closure,$now);
            INSERT OR IGNORE INTO evidence_application_links VALUES(
              $evidenceApplication,$evidence,$run,'m1s6-campaign-document-application',
              'm1s6-campaign-stage-3-context',$candidate,'installed-entity',$closure,
              'applicable',$payload,$now);
            INSERT OR IGNORE INTO candidate_decisions VALUES(
              'm1s6-campaign-candidate-decision',$run,'m1s6-campaign-population',
              'm1s6-campaign-relationship','candidate-admitted','optional-ranked',
              'm1s6-campaign-rule/v1',$candidatePayload,$now);
            INSERT OR IGNORE INTO analysis_candidates VALUES(
              $candidate,'m1s6-campaign-candidate-decision',$run,'optional-ranked','present',
              $closure,$candidatePayload,$now);
            INSERT OR IGNORE INTO analysis_hypotheses VALUES(
              $hypothesis,$candidate,$run,'present','plausible',
              'm1s6-campaign-threshold',$candidatePayload,$now);
            """;
        command.Parameters.AddWithValue("$payload", payloadId);
        command.Parameters.AddWithValue("$sha", payloadSha);
        command.Parameters.AddWithValue("$bytes", payload.LongLength);
        command.Parameters.AddWithValue("$candidatePayload", candidatePayloadId);
        command.Parameters.AddWithValue("$candidatePayloadSha", candidatePayloadSha);
        command.Parameters.AddWithValue("$candidatePayloadBytes", candidatePayload.LongLength);
        command.Parameters.AddWithValue("$run", input.AnalysisRunId);
        command.Parameters.AddWithValue("$revision", evidence.SourceRevisionId);
        command.Parameters.AddWithValue("$passage", evidence.PassageId);
        command.Parameters.AddWithValue("$evidence", evidence.EvidenceId);
        command.Parameters.AddWithValue("$evidenceApplication", evidence.EvidenceApplicationLinkId);
        command.Parameters.AddWithValue("$candidate", context.CandidateId);
        command.Parameters.AddWithValue("$hypothesis", context.HypothesisId);
        command.Parameters.AddWithValue("$closure", context.DependencyClosureId);
        command.Parameters.AddWithValue("$now", occurredAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
        transaction.Commit();
    }

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
}
