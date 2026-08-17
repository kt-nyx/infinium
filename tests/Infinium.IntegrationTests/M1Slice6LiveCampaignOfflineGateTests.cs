using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;
using Infinium.PublicFixtures;
using Microsoft.Data.Sqlite;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class M1Slice6LiveCampaignOfflineGateTests
{
    private static readonly string[] CanaryEncodings = ["utf-8", "utf-16le"];
    private static readonly JsonSerializerOptions LedgerJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };
    [TestMethod]
    public void FrozenWp10AndWp11ValidationPackagesExecuteTypedProductOraclesOffline()
    {
        LiveSemanticV2TypedOracleReceipt source = LiveSemanticV2TypedOracleVerifier.VerifySource(
            TestRepository.Root,
            M1Slice6CampaignRehearsalTests.StageProviderOutput(M1Slice6CampaignStage.SourceClaimExtraction));
        LiveSemanticV2TypedOracleReceipt candidate = LiveSemanticV2TypedOracleVerifier.VerifyCandidate(
            TestRepository.Root,
            M1Slice6CampaignRehearsalTests.StageProviderOutput(M1Slice6CampaignStage.CandidateInvestigation));
        Console.WriteLine("WP10_RESULT_SHA256=" + source.DeterministicResultSha256);
        Console.WriteLine("WP11_RESULT_SHA256=" + candidate.DeterministicResultSha256);
        Assert.AreEqual("LLM-CLAIM-LIVE-VAL-v2", source.PackageId);
        Assert.AreEqual("LLM-INVESTIGATE-LIVE-VAL-v2", candidate.PackageId);
        Assert.AreEqual(8, source.StateOrContextCount);
        Assert.AreEqual(2, candidate.StateOrContextCount);
        Assert.AreEqual("beabc1d5cef06317d215bfa45ab481273e5995e0b4bd58ce412c78335bfc8e0f",
            source.DeterministicResultSha256);
        Assert.AreEqual("5d594a901257437387cf970c9456106f16461401024d206fd4c1109f207dedd5",
            candidate.DeterministicResultSha256);
        string exactSource = M1Slice6CampaignRehearsalTests.StageProviderOutput(
            M1Slice6CampaignStage.SourceClaimExtraction);
        string exactCandidate = M1Slice6CampaignRehearsalTests.StageProviderOutput(
            M1Slice6CampaignStage.CandidateInvestigation);
        JsonObject equivalentSource = JsonNode.Parse(exactSource)!.AsObject();
        equivalentSource["transcripts"]![0]!["proposals"]![1]!["proposal_id"] =
            "provider-generated-observation-id";
        equivalentSource["transcripts"]![0]!["proposals"]![1]!["claim"] =
            "Nearby markers are observed by the relay, which does not declare an exchange capability.";
        equivalentSource["transcripts"]![0]!["proposals"]![1]!["reason"] =
            "The requested capability is not established by observation without an exchange declaration.";
        LiveSemanticV2TypedOracleReceipt equivalentSourceReceipt =
            LiveSemanticV2TypedOracleVerifier.VerifySource(TestRepository.Root,
                equivalentSource.ToJsonString());
        Assert.AreEqual(source.DeterministicResultSha256,
            equivalentSourceReceipt.DeterministicResultSha256);

        JsonObject equivalentCandidate = JsonNode.Parse(exactCandidate)!.AsObject();
        equivalentCandidate["transcripts"]![0]!["proposals"]![0]!["proposal_id"] =
            "provider-generated-candidate-proposal";
        equivalentCandidate["transcripts"]![0]!["proposals"]![0]!["reason"] =
            "The bounded hypothesis is supported by exact admitted source evidence.";
        const string equivalentUncertainty =
            "Independently frozen neutral host evidence does not establish active declared exchange; "
            + "the shared local observation remains conditional on exchange activation.";
        equivalentCandidate["transcripts"]![1]!["gaps"]![0] = equivalentUncertainty;
        equivalentCandidate["transcripts"]![1]!["abstentions"]![0] = equivalentUncertainty;
        LiveSemanticV2TypedOracleReceipt equivalentCandidateReceipt =
            LiveSemanticV2TypedOracleVerifier.VerifyCandidate(TestRepository.Root,
                equivalentCandidate.ToJsonString());
        Assert.AreEqual(candidate.DeterministicResultSha256,
            equivalentCandidateReceipt.DeterministicResultSha256);
        foreach (Action<JsonObject> adversarial in new Action<JsonObject>[]
        {
            root => root["transcripts"]![0]!["proposals"]![1]!["claim"] =
                "A fabricated reactor proves an unrelated capability.",
            root => root["transcripts"]![0]!["proposals"]![1]!["reason"] =
                "Observation without an exchange declaration does not establish the requested capability [source: 77].",
            root => root["transcripts"]![0]!["proposals"]![1]!["claim"] =
                "The relay observes nearby markers without declaring an exchange capability. A dragon is present.",
            root => root["transcripts"]![0]!["proposals"]![1]!["claim"] =
                "The relay observes nearby markers and declares an exchange capability.",
            root => root["transcripts"]![0]!["proposals"]![1]!["claim"] =
                "If the relay observes nearby markers, it does not declare an exchange capability.",
            root => root["transcripts"]![0]!["proposals"]![1]!["claim"] =
                "The relay observes nearby markers without declaring an exchange capability, and the.",
        })
        {
            JsonObject changed = JsonNode.Parse(exactSource)!.AsObject();
            adversarial(changed);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                LiveSemanticV2TypedOracleVerifier.VerifySource(TestRepository.Root,
                    changed.ToJsonString()));
        }
        JsonObject keywordStuffed = JsonNode.Parse(exactCandidate)!.AsObject();
        keywordStuffed["transcripts"]![1]!["gaps"]![0] =
            "Conditional active declared exchange and frozen neutral host evidence do not establish activation, "
            + "but reactor 77 proves a second capability.";
        keywordStuffed["transcripts"]![1]!["abstentions"]![0] =
            keywordStuffed["transcripts"]![1]!["gaps"]![0]!.GetValue<string>();
        Assert.ThrowsExactly<InvalidDataException>(() =>
            LiveSemanticV2TypedOracleVerifier.VerifyCandidate(TestRepository.Root,
                keywordStuffed.ToJsonString()));
        JsonObject invertedCandidateReason = JsonNode.Parse(exactCandidate)!.AsObject();
        invertedCandidateReason["transcripts"]![0]!["proposals"]![0]!["reason"] =
            "The bounded hypothesis is not supported by exact admitted source evidence.";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            LiveSemanticV2TypedOracleVerifier.VerifyCandidate(TestRepository.Root,
                invertedCandidateReason.ToJsonString()));
        JsonObject reversedCandidateRoles = JsonNode.Parse(exactCandidate)!.AsObject();
        reversedCandidateRoles["transcripts"]![0]!["proposals"]![0]!["reason"] =
            "The exact bounded hypothesis supports the admitted source evidence.";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            LiveSemanticV2TypedOracleVerifier.VerifyCandidate(TestRepository.Root,
                reversedCandidateRoles.ToJsonString()));
        string mutated = M1Slice6CampaignRehearsalTests.StageProviderOutput(
            M1Slice6CampaignStage.SourceClaimExtraction).Replace(
                "\"state\":\"unsupported\"", "\"state\":\"proposed\"",
                StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            LiveSemanticV2TypedOracleVerifier.VerifySource(TestRepository.Root, mutated));
        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
        {
            root => root["transcripts"]![0]!["proposals"]![0]!.AsObject().Remove("claim"),
            root => root["transcripts"]![0]!["proposals"]![0]!["condition_scope"] = "unconditional",
            root => root["transcripts"]![0]!["proposals"]![0]!["authority_category"] = "protected-effect-request",
            root => root["transcripts"]![0]!["proposals"]![0]!["application_semantics"] = "evidence-only",
            root => root["transcripts"]![0]!["proposals"]![0]!["condition_ids"]!.AsArray().Clear(),
            root => root["transcripts"]![0]!["contradiction_evidence_ids"]!.AsArray().RemoveAt(0),
            root => root["transcripts"]![0]!["abstentions"]!.AsArray().RemoveAt(0),
            root => root["transcripts"]![0]!["gaps"]!.AsArray().Clear(),
        })
        {
            JsonObject changed = JsonNode.Parse(exactSource)!.AsObject();
            mutation(changed);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                LiveSemanticV2TypedOracleVerifier.VerifySource(TestRepository.Root, changed.ToJsonString()));
        }
        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
        {
            root => root["transcripts"]![0]!["proposals"]![0]!.AsObject().Remove("hypothesis"),
            root => root["transcripts"]![0]!["proposals"]![0]!["candidate_id"] = "parallel-candidate",
            root => root["transcripts"]![0]!["proposals"]![0]!["authority_category"] = "protected-effect-request",
            root => root["transcripts"]![0]!["proposals"]![0]!["supporting_evidence_ids"]!.AsArray().Clear(),
            root => root["transcripts"]![1]!["gaps"]!.AsArray().Clear(),
            root => root["transcripts"]![1]!["abstentions"]![0] = "weaker uncertainty",
            root => root["transcripts"]![1]!["operation_id"] = "parallel-operation",
        })
        {
            JsonObject changed = JsonNode.Parse(exactCandidate)!.AsObject();
            mutation(changed);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                LiveSemanticV2TypedOracleVerifier.VerifyCandidate(TestRepository.Root, changed.ToJsonString()));
        }
    }

    [TestMethod]
    public void MaterializedCampaignArtifactsReopenThroughLedgerSqliteReplayAndSemanticCrosslinks()
    {
        string? inputRoot = Environment.GetEnvironmentVariable("INFINIUM_CAMPAIGN_OFFLINE_INPUT_ROOT");
        if (string.IsNullOrWhiteSpace(inputRoot))
        {
            Assert.Inconclusive(
                "The exact live-evidence gate must supply INFINIUM_CAMPAIGN_OFFLINE_INPUT_ROOT.");
        }
        string root = FindRepositoryRoot(inputRoot);
        string ledgerPath = Path.Combine(inputRoot, "campaign-ledger-v2.jsonl");
        string[] ledgerLines = File.ReadAllLines(ledgerPath);
        Assert.IsGreaterThan(0, ledgerLines.Length);
        M1Slice6CampaignLedgerEntry first = JsonSerializer.Deserialize<M1Slice6CampaignLedgerEntry>(
            ledgerLines[0], LedgerJson)!;
        M1Slice6CampaignLedgerEntry last = JsonSerializer.Deserialize<M1Slice6CampaignLedgerEntry>(
            ledgerLines[^1], LedgerJson)!;
        DateTimeOffset exactCampaignExpiry = DateTimeOffset.Parse(
            "2026-08-31T23:59:00Z", CultureInfo.InvariantCulture);
        DateTimeOffset exactCredentialExpiry = DateTimeOffset.Parse(
            "2026-08-31T23:00:00Z", CultureInfo.InvariantCulture);
        M1Slice6FiniteCampaignLedger ledger = new(ledgerPath, first.Identity,
            exactCampaignExpiry, exactCredentialExpiry, last.RecordedAtUtc);
        Assert.AreEqual(M1Slice6CampaignState.Completed, ledger.Current.State);
        Assert.AreEqual(3L, ledger.Current.ProviderCallCount);
        Assert.AreEqual(3L, ledger.Current.DnsResolutionCount);
        Assert.AreEqual(new M1Slice6CampaignNativeEnvelope(1, 5, 0, 4, 10),
            ledger.Current.NativeEnvelope);

        string credentialPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v2.json");
        string campaignPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-finite-campaign-authorization.v2.json");
        using JsonDocument credential = JsonDocument.Parse(File.ReadAllBytes(credentialPath));
        using JsonDocument campaign = JsonDocument.Parse(File.ReadAllBytes(campaignPath));
        JsonElement credentialRoot = credential.RootElement;
        Assert.AreEqual("infinium.repository.wp9-production-profile-authorization/2.0.0",
            credentialRoot.GetProperty("schema_identity").GetString());
        Assert.AreEqual("2026-08-31T23:00:00.0000000Z",
            credentialRoot.GetProperty("expires_at_utc").GetString());
        Assert.AreEqual("2026-08-31T23:59:00.0000000Z",
            campaign.RootElement.GetProperty("expires_at_utc").GetString());
        Assert.AreEqual(first.Identity.CredentialManifestSha256,
            Sha(File.ReadAllBytes(credentialPath)));
        Assert.AreEqual(first.Identity.CampaignManifestSha256,
            Sha(File.ReadAllBytes(campaignPath)));
        string expiryMutationRoot = Path.Combine(inputRoot, "expiry-mutations-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(expiryMutationRoot);
        try
        {
            foreach ((string sourcePath, string changedExpiry, bool credentialChanged) in new[]
            {
                (credentialPath, "2026-08-31T22:59:59.0000000Z", true),
                (campaignPath, "2026-08-31T23:58:59.0000000Z", false),
            })
            {
                JsonObject changed = JsonNode.Parse(File.ReadAllBytes(sourcePath))!.AsObject();
                changed["expires_at_utc"] = changedExpiry;
                string changedPath = Path.Combine(expiryMutationRoot, Path.GetFileName(sourcePath));
                File.WriteAllText(changedPath, changed.ToJsonString(), new UTF8Encoding(false));
                string testedCredentialPath = credentialChanged ? changedPath : credentialPath;
                string testedCampaignPath = credentialChanged ? campaignPath : changedPath;
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    Wp9ProductionProfileEnrollmentRunner.ValidateCampaignAdmissionOnly(
                        testedCredentialPath, Sha(File.ReadAllBytes(testedCredentialPath)),
                        testedCampaignPath, Sha(File.ReadAllBytes(testedCampaignPath)),
                        first.Identity.VerificationCandidateCommit));
            }
        }
        finally
        {
            Directory.Delete(expiryMutationRoot, recursive: true);
        }
        string stateRoot = Path.GetFullPath(Path.Combine(root,
            credentialRoot.GetProperty("durable_state").GetProperty("product_state_root_relative")
                .GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        using AuthoritativeStore store = new(new StoragePaths(stateRoot));
        CredentialProfileProjection profile = store.GetCredentialProfile(first.Identity.CredentialProfileId);
        Assert.AreEqual(first.Identity.CredentialGenerationId, profile.GenerationId);
        Assert.AreEqual("active-verified", profile.LifecycleState);
        Assert.AreEqual("available", profile.VerificationState);
        string recordPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "record.md");
        string[] recordLines = File.ReadAllLines(recordPath);
        M1Slice6CampaignLedgerEntry credentialAcceptance = ledger.Entries.Single(entry =>
            entry.State == M1Slice6CampaignState.CredentialEvidenceAccepted);
        Assert.AreEqual("credential-evidence-independently-accepted", credentialAcceptance.Event);
        Assert.AreEqual("wp9-production-profile-enrollment-evidence-v2", credentialAcceptance.EvidenceId);
        string credentialEvidencePath = Path.Combine(inputRoot, "r2-rehearsal-authority",
            "wp9-production-profile-enrollment-evidence.v2.json");
        byte[] credentialEvidenceBytes = File.ReadAllBytes(credentialEvidencePath);
        Assert.AreEqual(Sha(credentialEvidenceBytes), credentialAcceptance.EvidenceSha256);
        using JsonDocument credentialEvidence = JsonDocument.Parse(credentialEvidenceBytes);
        JsonElement retainedCredential = credentialEvidence.RootElement;
        Assert.AreEqual(first.Identity.CredentialManifestId,
            retainedCredential.GetProperty("manifest_id").GetString());
        Assert.AreEqual(first.Identity.CredentialManifestSha256,
            retainedCredential.GetProperty("manifest_sha256").GetString());
        Assert.AreEqual(first.Identity.CredentialProfileId,
            retainedCredential.GetProperty("profile_id").GetString());
        Assert.AreEqual(first.Identity.CredentialGenerationId,
            retainedCredential.GetProperty("generation_id").GetString());
        Assert.AreEqual(first.Identity.CredentialTargetFingerprintSha256,
            retainedCredential.GetProperty("target_fingerprint_sha256").GetString());
        Wp9ProductionProfileEnrollmentRunner.ValidateAcceptedCampaignCredentialArtifacts(
            retainedCredential, first.Identity.CredentialTargetFingerprintSha256);
        JsonObject corruptedCredentialEvidence = JsonNode.Parse(credentialEvidenceBytes)!.AsObject();
        corruptedCredentialEvidence["native_call_trace"]![2]!["TargetFingerprintSha256"] =
            new string('f', 64);
        using JsonDocument corruptedCredential = JsonDocument.Parse(
            corruptedCredentialEvidence.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Wp9ProductionProfileEnrollmentRunner.ValidateAcceptedCampaignCredentialArtifacts(
                corruptedCredential.RootElement, first.Identity.CredentialTargetFingerprintSha256));
        Assert.AreEqual(0L, credentialAcceptance.ProviderCallCount);
        Assert.AreEqual(0L, credentialAcceptance.DnsResolutionCount);

        string[] stageNames = ["wp9-live", "wp10-live", "wp11-live"];
        JsonElement[] semantic = new JsonElement[3];
        JsonElement[] packages = new JsonElement[3];
        for (int index = 0; index < stageNames.Length; index++)
        {
            string evidencePath = Path.Combine(inputRoot, stageNames[index], "stage-evidence.json");
            using JsonDocument evidence = JsonDocument.Parse(File.ReadAllBytes(evidencePath));
            JsonElement evidenceRoot = evidence.RootElement;
            RequireExactNames(evidenceRoot, ["schema", "status", "campaign_id", "campaign_manifest_sha256",
                "stage_manifest_id", "stage_manifest_sha256", "stage", "canonical_request_sha256",
                "predecessor_evidence_id", "predecessor_evidence_sha256", "safety_identifier_projection",
                "provider_state", "http_status", "provider_response_id", "client_request_id",
                "provider_request_id", "requested_model", "returned_model", "requested_service_tier",
                "returned_service_tier", "reasoning_context", "reasoning_mode", "prompt_cache_mode",
                "provider_send_count", "dns_resolution_count", "retry_permitted", "credential_profile_id",
                "credential_generation_id", "credential_target_fingerprint_sha256", "input_tokens",
                "output_tokens", "raw_response_bytes", "calculated_nano_usd", "usage", "rate_facts",
                "credential_reads", "credential_frees", "credential_writes", "credential_deletes",
                "cumulative_credential_calls", "retained_artifacts", "authoritative_persistence",
                "validation_package", "semantic_validation"]);
            semantic[index] = evidenceRoot.GetProperty("semantic_validation").Clone();
            packages[index] = evidenceRoot.GetProperty("validation_package").Clone();
            JsonElement artifacts = evidenceRoot.GetProperty("retained_artifacts");
            string directory = Path.GetDirectoryName(evidencePath)!;
            byte[] raw = Bound(artifacts, directory, "raw_response", ".bin");
            byte[] headers = Bound(artifacts, directory, "response_headers", ".json");
            byte[] request = Bound(artifacts, directory, "canonical_request", ".json");
            byte[] trace = Bound(artifacts, directory, "native_trace", ".json");
            byte[] canary = Bound(artifacts, directory, "canary_evidence", ".json");
            ValidateCanaries(canary);
            using JsonDocument native = JsonDocument.Parse(trace);
            JsonElement[] calls = native.RootElement.EnumerateArray().ToArray();
            Assert.AreEqual(2, calls.Length);
            Assert.AreEqual("CredReadW", calls[0].GetProperty("Operation").GetString());
            Assert.AreEqual("success", calls[0].GetProperty("Result").GetString());
            Assert.AreEqual("CredFree", calls[1].GetProperty("Operation").GetString());
            Assert.AreEqual("released", calls[1].GetProperty("Result").GetString());
            Assert.AreEqual(calls[0].GetProperty("AllocationId").GetInt64(),
                calls[1].GetProperty("PairedAllocationId").GetInt64());

            string clientRequestId = evidenceRoot.GetProperty("client_request_id").GetString()!;
            OpenAiResponsesResult replay = OpenAiStagedResponseEnvelope.Replay(raw, headers, clientRequestId);
            Assert.AreEqual(ProviderResponseState.Completed, replay.State);
            Assert.IsTrue(replay.Admitted);
            if (index == 1)
            {
                LiveSemanticV2TypedOracleReceipt receipt = LiveSemanticV2TypedOracleVerifier.VerifySource(
                    root, M1Slice6CampaignSemanticAdmission.ExtractOutputText(raw));
                Assert.AreEqual(packages[index].GetProperty("deterministic_oracle_result_sha256").GetString(),
                    receipt.DeterministicResultSha256);
            }
            else if (index == 2)
            {
                LiveSemanticV2TypedOracleReceipt receipt = LiveSemanticV2TypedOracleVerifier.VerifyCandidate(
                    root, M1Slice6CampaignSemanticAdmission.ExtractOutputText(raw));
                Assert.AreEqual(packages[index].GetProperty("deterministic_oracle_result_sha256").GetString(),
                    receipt.DeterministicResultSha256);
            }
            JsonElement persistence = evidenceRoot.GetProperty("authoritative_persistence");
            ProviderOperationReadModel operation = store.ReadProviderOperation(
                persistence.GetProperty("operation_id").GetString()!);
            Assert.AreEqual(ProviderOperationState.Settled, operation.State);
            Assert.AreEqual(persistence.GetProperty("authorization_id").GetString(), operation.AuthorizationId);
            Assert.AreEqual(persistence.GetProperty("response_id").GetString(), operation.ResponseId);
            Assert.AreEqual(persistence.GetProperty("usage_entry_id").GetString(), operation.UsageEntryId);
            Assert.AreEqual(persistence.GetProperty("settlement_id").GetString(), operation.SettlementId);
            Assert.AreEqual(persistence.GetProperty("replay_edge_id").GetString(), operation.ReplayEdgeId);
            Assert.IsFalse(operation.UnresolvedHold);
            Assert.AreEqual("retained-response", operation.ReplayState);
            CollectionAssert.AreEqual(raw, operation.RawResponseBytes!);
            CollectionAssert.AreEqual(headers, operation.ResponseHeadersBytes!);
            OpenAiResponsesResult sqliteReplay = new ProviderAccountingCoordinator(store).Replay(new(
                new OpaqueId(operation.OperationId), new OpaqueId(operation.ResponseId), NetworkPermitted: false));
            CollectionAssert.AreEqual(raw, sqliteReplay.RawResponseBytes!);
            string stageManifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
                "live", (index + 1) + "-" + (M1Slice6CampaignStage)(index + 1) + ".json");
            using JsonDocument stageManifest = JsonDocument.Parse(File.ReadAllBytes(stageManifestPath));
            JsonElement requestBinding = stageManifest.RootElement.GetProperty("canonical_request");
            Assert.AreEqual(requestBinding.GetProperty("sha256").GetString(), Sha(request));
            (long bytes, string inputSha, string templateSha) =
                M1Slice6CampaignSemanticAdmission.BindCanonicalInputAndTemplate(request);
            Assert.AreEqual(bytes, requestBinding.GetProperty("campaign_input_bytes").GetInt64());
            Assert.AreEqual(inputSha, requestBinding.GetProperty("campaign_input_sha256").GetString());
            Assert.AreEqual(templateSha, requestBinding.GetProperty("request_template_sha256").GetString());
            string evidenceSha = Sha(File.ReadAllBytes(evidencePath));
            string evidenceMarker = "M1_S6_CAMPAIGN_STAGE_EVIDENCE_ACCEPTANCE campaign_id="
                + first.Identity.CampaignId + " campaign_sha256=" + first.Identity.CampaignManifestSha256
                + " stage_manifest_id=" + evidenceRoot.GetProperty("stage_manifest_id").GetString()
                + " stage_manifest_sha256=" + evidenceRoot.GetProperty("stage_manifest_sha256").GetString()
                + " evidence_id=campaign-stage-evidence-" + (index + 1) + " sha256=" + evidenceSha
                + " verdicts=security,semantics,budget,provenance";
            Assert.AreEqual(1, recordLines.Count(line => line == evidenceMarker));
        }

        LiveSemanticV2AuthorityReceipt authority = LiveSemanticV2AuthorityVerifier.Verify(root);
        Assert.AreEqual(43, authority.PackageCount);
        Assert.AreEqual(5, authority.NewPackageCount);
        Assert.AreEqual("LLM-CLAIM-LIVE-VAL-v2", packages[1].GetProperty("package_id").GetString());
        Assert.AreEqual("76a631ffa02eeff301c240588d1507e3fe3cc2fe13f19aa597aecb8d2ddb3e14",
            packages[1].GetProperty("oracle_sha256").GetString());
        Assert.AreEqual("LLM-INVESTIGATE-LIVE-VAL-v2", packages[2].GetProperty("package_id").GetString());
        Assert.AreEqual("52f13b89f0c0cab2dc91c72e3986b8bc358e41a5ab5253ea1e8fab3b19230e3a",
            packages[2].GetProperty("oracle_sha256").GetString());
        Assert.AreEqual("beabc1d5cef06317d215bfa45ab481273e5995e0b4bd58ce412c78335bfc8e0f",
            packages[1].GetProperty("deterministic_oracle_result_sha256").GetString());
        Assert.AreEqual("5d594a901257437387cf970c9456106f16461401024d206fd4c1109f207dedd5",
            packages[2].GetProperty("deterministic_oracle_result_sha256").GetString());
        string candidateInputPath = Path.Combine(root, "fixtures", "public", "provider",
            "candidate-investigations", "S6-CANDIDATE-LIVE-VAL-v2", "execution-input.v2.json");
        byte[] exactCandidateInput = File.ReadAllBytes(candidateInputPath);
        using JsonDocument candidateInput = JsonDocument.Parse(exactCandidateInput);
        string analysisRunId = candidateInput.RootElement.GetProperty("analysis_run_id").GetString()!;
        string candidateOperationId = candidateInput.RootElement.GetProperty("operation_id").GetString()!;
        DurableCandidateInvestigationCoordinator candidateCoordinator = new(store);
        List<string> retainedOutcomeIds = [];
        foreach (JsonElement context in candidateInput.RootElement.GetProperty("contexts").EnumerateArray())
        {
            string contextId = context.GetProperty("context_id").GetString()!;
            CandidateInvestigationOutcomeReadModel retained = store.ReadCandidateInvestigationOutcome(
                analysisRunId, candidateOperationId, contextId);
            retainedOutcomeIds.Add(retained.OutcomeId);
            CollectionAssert.AreEqual(exactCandidateInput, retained.InputPayload);
            CandidateInvestigationScenarioResult replay = candidateCoordinator.ReplayRetained(
                analysisRunId, candidateOperationId, contextId);
            Assert.AreEqual(contextId, replay.ContextId);
            JsonElement evidenceRoot = context.GetProperty("evidence")[0];
            Assert.AreNotEqual(evidenceRoot.TryGetProperty("host_bindings", out _),
                evidenceRoot.TryGetProperty("host_evidence", out JsonElement hostEvidence));
            JsonElement observation = context.GetProperty("local_observations")[0];
            Assert.AreEqual(observation.GetProperty("text_sha256").GetString(),
                Sha(Encoding.UTF8.GetBytes(observation.GetProperty("text").GetString()!)));
            if (hostEvidence.ValueKind != JsonValueKind.Undefined)
            {
                Assert.AreEqual("wp11-host-evidence-root-relay-observation",
                    hostEvidence.GetProperty("evidence_root_id").GetString());
                Assert.AreEqual("wp11-applicability-relay-observation",
                    hostEvidence.GetProperty("applicability_record_id").GetString());
            }
        }
        AssertRelationalAuthorityRejectsTornOrOrphanRows(store.Paths.Database, retainedOutcomeIds[0]);
        AssertReplayRejectsUnderlyingRowCorruption(store.Paths.ProductRoot, exactCandidateInput,
            analysisRunId, candidateOperationId, retainedOutcomeIds);
        foreach (string name in new[] { "source_acquisition_id", "source_admission_id",
            "admitted_artifact_id", "source_application_link_id" })
        {
            Assert.AreEqual(semantic[1].GetProperty(name).GetString(), semantic[2].GetProperty(name).GetString());
        }
        SourceClaimApplicationReadModel application = store.ReadSourceClaimApplicationLinks(
            semantic[1].GetProperty("source_acquisition_id").GetString()!).Single(item =>
                item.ApplicationLinkId == semantic[1].GetProperty("source_application_link_id").GetString());
        Assert.AreEqual(semantic[1].GetProperty("admitted_artifact_id").GetString(),
            application.AdmittedArtifactId);
        string composedPath = Path.Combine(inputRoot, "wp11-live", "composed-evidence.json");
        string composedMarker = "M1_S6_CAMPAIGN_COMPOSED_EVIDENCE_ACCEPTANCE campaign_id="
            + first.Identity.CampaignId + " campaign_sha256=" + first.Identity.CampaignManifestSha256
            + " evidence_id=campaign-composed-evidence sha256=" + Sha(File.ReadAllBytes(composedPath))
            + " verdicts=security,semantics,budget,provenance,diff";
        Assert.AreEqual(1, recordLines.Count(line => line == composedMarker));
    }

    private static void AssertRelationalAuthorityRejectsTornOrOrphanRows(string databasePath, string outcomeId)
    {
        using SqliteConnection connection = new($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.Parameters.AddWithValue("$outcome", outcomeId);
        foreach (string sql in new[]
                 {
                     "UPDATE candidate_evidence_authority SET root_kind='frozen-host-evidence' WHERE outcome_id=$outcome;",
                     "DELETE FROM candidate_evidence_authority WHERE outcome_id=$outcome;",
                     "INSERT INTO candidate_evidence_authority SELECT * FROM candidate_evidence_authority WHERE outcome_id=$outcome;",
                     "INSERT INTO candidate_evidence_authority SELECT 'orphan-outcome',evidence_id || '-orphan',evidence_application_link_id || '-orphan',root_kind,evidence_root_id,applicability_record_id,source_acquisition_id,source_proposal_id,source_admission_id,admitted_artifact_id,source_application_link_id,source_revision_id,passage_id,content_sha256,local_observation_id,local_observation_sha256,input_payload_id,created_at FROM candidate_evidence_authority WHERE outcome_id=$outcome;",
                     "UPDATE source_claim_admitted_artifacts SET content_sha256='ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff';",
                     "DELETE FROM source_claim_admitted_artifacts;",
                     "INSERT INTO source_claim_admitted_artifacts SELECT * FROM source_claim_admitted_artifacts;",
                 })
        {
            command.CommandText = sql;
            Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery(),
                "Append-only and relational constraints must reject torn, swapped, duplicate, or orphan authority rows.");
        }
    }

    private static void AssertReplayRejectsUnderlyingRowCorruption(string productRoot,
        byte[] exactCandidateInput, string analysisRunId, string operationId,
        List<string> outcomeIds)
    {
        using (SqliteConnection checkpoint = new($"Data Source={Path.Combine(productRoot, "data", "infinium.sqlite3")};Pooling=False"))
        {
            checkpoint.Open();
            using SqliteCommand command = checkpoint.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(FULL);";
            _ = command.ExecuteNonQuery();
        }
        (string Table, string Sql, string Outcome, string Context)[] mutations =
        [
            ("source_claim_admitted_artifacts",
                "DELETE FROM source_claim_admitted_artifacts WHERE admitted_artifact_id='wp10-artifact-relay-activation';",
                outcomeIds[0], "relay-gate-context-a"),
            ("source_claim_admitted_artifacts",
                "UPDATE source_claim_admitted_artifacts SET content_sha256='ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff' WHERE admitted_artifact_id='wp10-artifact-relay-activation';",
                outcomeIds[0], "relay-gate-context-a"),
            ("evidence_acquisition_application_links",
                "DELETE FROM evidence_acquisition_application_links WHERE application_link_id='wp10-application-link-relay-activation';",
                outcomeIds[0], "relay-gate-context-a"),
            ("source_claim_applicability_facts",
                "DELETE FROM source_claim_applicability_facts WHERE proposal_id='wp10-proposal-relay-activation';",
                outcomeIds[0], "relay-gate-context-a"),
            ("evidence_application_links",
                "DELETE FROM evidence_application_links WHERE evidence_application_link_id='wp11-evidence-application-b';",
                outcomeIds[1], "relay-gate-context-b"),
            ("candidate_evidence_authority",
                "UPDATE candidate_evidence_authority SET source_proposal_id='wp10-state-relay-observation' WHERE outcome_id='" + outcomeIds[0] + "';",
                outcomeIds[0], "relay-gate-context-a"),
        ];
        string temporary = Path.Combine(Path.GetTempPath(), "infinium-r2-replay-corruption-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            foreach ((string table, string sql, string outcome, string context) in mutations)
            {
                string copyRoot = Path.Combine(temporary, Guid.NewGuid().ToString("N"));
                using (StoragePaths prepared = new(copyRoot))
                {
                    prepared.Create();
                }
                CopyDirectory(productRoot, copyRoot);
                string database = Path.Combine(copyRoot, "data", "infinium.sqlite3");
                using (SqliteConnection connection = new($"Data Source={database};Pooling=False"))
                {
                    connection.Open();
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText = $"""
                        PRAGMA foreign_keys=OFF;
                        DROP TRIGGER {table}_append_only_update;
                        DROP TRIGGER {table}_append_only_delete;
                        {sql}
                        CREATE TRIGGER {table}_append_only_update
                        BEFORE UPDATE ON {table}
                        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                        CREATE TRIGGER {table}_append_only_delete
                        BEFORE DELETE ON {table}
                        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                        """;
                    _ = command.ExecuteNonQuery();
                }
                using AuthoritativeStore corrupted = new(new StoragePaths(copyRoot));
                Assert.ThrowsExactly<InvalidDataException>(() =>
                {
                    corrupted.ValidateCandidateEvidenceAuthority(outcome, context, exactCandidateInput);
                    _ = new DurableCandidateInvestigationCoordinator(corrupted)
                        .ReplayRetained(analysisRunId, operationId, context);
                }, "Replay accepted a missing, swapped, mismatched, or orphaned underlying evidence row.");
            }
        }
        finally
        {
            if (Directory.Exists(temporary)) { Directory.Delete(temporary, recursive: true); }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    [TestMethod]
    public async Task ReopenAfterSqliteSettlementConvergesToKnownSettledNoRetryWithoutRedispatch()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "infinium-campaign-reconcile-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            DateTimeOffset start = DateTimeOffset.Parse("2026-08-15T16:00:00Z",
                CultureInfo.InvariantCulture);
            string manifestPath = Path.Combine(temporary, "stage.json");
            File.WriteAllText(manifestPath,
                "{\"manifest_id\":\"request\",\"stage\":{\"ordinal\":1},\"canonical_request\":{\"sha256\":\""
                + new string('9', 64) + "\"}}\n", new UTF8Encoding(false));
            string manifestSha = Sha(File.ReadAllBytes(manifestPath));
            M1Slice6CampaignIdentity identity = new("infinium.m1-s6.finite-live-campaign/reconcile",
                new string('1', 64), new string('2', 64), new string('3', 40),
                "infinium.m1-s6.wp9/reconcile", new string('4', 64), "profile", "generation",
                new string('5', 64));
            string ledgerPath = Path.Combine(temporary, "ledger.jsonl");
            M1Slice6FiniteCampaignLedger ledger = new(ledgerPath, identity,
                DateTimeOffset.Parse("2026-08-22T23:59:00Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-08-17T15:25:00Z", CultureInfo.InvariantCulture), start);
            ledger.RecordIndependentReview(start.AddMinutes(1));
            ledger.AdmitCampaign(start.AddMinutes(2));
            ledger.BeginCredentialExecutionHandoff(start.AddMinutes(3));
            ledger.RecordCredentialEvidenceHandoff("credential", new string('6', 64),
                new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4), start.AddMinutes(4));
            ledger.AcceptCredentialEvidence("credential", new string('6', 64), start.AddMinutes(4).AddTicks(1));
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification,
                new("request", manifestSha, 3, 10, 1, 100, 100), start.AddMinutes(5));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, new string('a', 64),
                start.AddMinutes(5).AddTicks(1));
            ProductUserSafetyIdentifierStateStore safety = new(Path.Combine(temporary, "safety"));
            NeverDispatchBoundary boundary = new();
            M1Slice6CampaignStageCoordinator coordinator = new(ledger, safety, boundary,
                new RecoveredSettlementAccounting());
            await Assert.ThrowsExactlyAsync<M1Slice6CampaignKnownSettlementException>(() =>
                coordinator.ExecuteOneShotAsync(manifestPath, manifestSha,
                    Path.Combine(temporary, "evidence.json"), start.AddMinutes(8),
                    CancellationToken.None));
            Assert.AreEqual(0, boundary.SendCount);
            Assert.AreEqual(M1Slice6CampaignState.Stopped, ledger.Current.State);
            Assert.AreEqual("reconciled-sqlite-settlement-known-settled-no-retry",
                ledger.Current.Event);
            Assert.AreEqual(0L, ledger.Current.ReservedNanoUsd);
            Assert.AreEqual(1L, ledger.Current.ProviderCallCount);
            Assert.AreEqual(new M1Slice6CampaignNativeEnvelope(1, 3, 0, 2, 6),
                ledger.Current.NativeEnvelope);
            M1Slice6CampaignLedgerEntry settledEntry = ledger.Entries.Single(entry =>
                entry.State == M1Slice6CampaignState.StageSettled);
            Assert.IsGreaterThan(settledEntry.StageDeadlineUtc!.Value, settledEntry.RecordedAtUtc);
        }
        finally
        {
            if (Directory.Exists(temporary)) { Directory.Delete(temporary, recursive: true); }
        }
    }

    [TestMethod]
    public async Task UnreconciledPossibleStartIsDurablyTerminalForStaleMissingAndNullRecovery()
    {
        foreach (string scenario in new[]
        {
            "stale-manifest", "missing-recovery", "null-settlement", "recovery-query-fault"
        })
        {
            string temporary = Path.Combine(Path.GetTempPath(), "infinium-campaign-unreconciled-"
                + scenario + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                DateTimeOffset start = DateTimeOffset.Parse("2026-08-15T16:00:00Z",
                    CultureInfo.InvariantCulture);
                (M1Slice6FiniteCampaignLedger ledger, string manifestPath, string manifestSha) =
                    CreatePossibleStartFixture(temporary, start);
                IM1Slice6CampaignProviderAccounting accounting = scenario switch
                {
                    "missing-recovery" => new NoRecoveryAccounting(),
                    "recovery-query-fault" => new ThrowingRecoveryAccounting(),
                    _ => new NullRecoveryAccounting(),
                };
                if (scenario == "stale-manifest")
                {
                    File.AppendAllText(manifestPath, " ");
                }
                NeverDispatchBoundary boundary = new();
                M1Slice6CampaignStageCoordinator coordinator = new(ledger,
                    new ProductUserSafetyIdentifierStateStore(Path.Combine(temporary, "safety")),
                    boundary, accounting);
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() => coordinator.ExecuteOneShotAsync(
                    manifestPath, manifestSha, Path.Combine(temporary, "evidence.json"),
                    start.AddMinutes(8), CancellationToken.None));
                Assert.AreEqual(0, boundary.SendCount, scenario);
                Assert.AreEqual(M1Slice6CampaignState.Stopped, ledger.Current.State, scenario);
                Assert.AreEqual("unreconciled-start-hold-retained-no-retry", ledger.Current.Event, scenario);
                Assert.IsGreaterThan(0L, ledger.Current.ReservedNanoUsd, scenario);

                M1Slice6FiniteCampaignLedger reopened = new(Path.Combine(temporary, "ledger.jsonl"),
                    ledger.Current.Identity, DateTimeOffset.Parse("2026-08-22T23:59:00Z",
                        CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-08-17T15:25:00Z",
                        CultureInfo.InvariantCulture), start.AddMinutes(9));
                Assert.AreEqual("unreconciled-start-hold-retained-no-retry", reopened.Current.Event, scenario);
            }
            finally
            {
                if (Directory.Exists(temporary)) { Directory.Delete(temporary, recursive: true); }
            }
        }
    }

    private static (M1Slice6FiniteCampaignLedger Ledger, string ManifestPath, string ManifestSha)
        CreatePossibleStartFixture(string temporary, DateTimeOffset start)
    {
        string manifestPath = Path.Combine(temporary, "stage.json");
        File.WriteAllText(manifestPath,
            "{\"manifest_id\":\"request\",\"stage\":{\"ordinal\":1},\"canonical_request\":{\"sha256\":\""
            + new string('9', 64) + "\"}}\n", new UTF8Encoding(false));
        string manifestSha = Sha(File.ReadAllBytes(manifestPath));
        M1Slice6CampaignIdentity identity = new("infinium.m1-s6.finite-live-campaign/unreconciled",
            new string('1', 64), new string('2', 64), new string('3', 40),
            "infinium.m1-s6.wp9/unreconciled", new string('4', 64), "profile", "generation",
            new string('5', 64));
        M1Slice6FiniteCampaignLedger ledger = new(Path.Combine(temporary, "ledger.jsonl"), identity,
            DateTimeOffset.Parse("2026-08-22T23:59:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-08-17T15:25:00Z", CultureInfo.InvariantCulture), start);
        ledger.RecordIndependentReview(start.AddMinutes(1));
        ledger.AdmitCampaign(start.AddMinutes(2));
        ledger.BeginCredentialExecutionHandoff(start.AddMinutes(3));
        ledger.RecordCredentialEvidenceHandoff("credential", new string('6', 64),
            new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4), start.AddMinutes(4));
        ledger.AcceptCredentialEvidence("credential", new string('6', 64), start.AddMinutes(4).AddTicks(1));
        ledger.ReserveStage(M1Slice6CampaignStage.Qualification,
            new("request", manifestSha, 3, 10, 1, 100, 100), start.AddMinutes(5));
        ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, new string('a', 64),
            start.AddMinutes(5).AddTicks(1));
        return (ledger, manifestPath, manifestSha);
    }

    private static byte[] Bound(JsonElement artifacts, string directory, string prefix, string extension)
    {
        string name = artifacts.GetProperty(prefix + "_path").GetString()!;
        Assert.AreEqual(Path.GetFileName(name), name);
        Assert.IsTrue(name.EndsWith(extension, StringComparison.Ordinal));
        string path = Path.GetFullPath(Path.Combine(directory, name));
        Assert.AreEqual(Path.GetFullPath(directory), Path.GetDirectoryName(path));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreEqual(artifacts.GetProperty(prefix + "_sha256").GetString(), Sha(bytes));
        return bytes;
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void ValidateCredentialEvidence(string inputRoot, JsonElement credential,
        IReadOnlyList<M1Slice6CampaignLedgerEntry> entries, string[] recordLines,
        string campaignId, string campaignSha)
    {
        string path = Path.Combine(inputRoot, "wp9-live", "credential-evidence.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        RequireExactNames(root, ["schema", "status", "manifest_id", "manifest_sha256",
            "campaign_credential_handoff_event_hash", "profile_id", "generation_id",
            "target_fingerprint_sha256", "lifecycle_state", "verification_state",
            "native_credential_operation_count", "native_call_trace", "entry_evidence", "canaries",
            "network_operation_count", "listener_count", "provider_operation_count",
            "billable_operation_count", "retry_attempted", "containment", "namespace_reuse_blocked",
            "namespace_reuse_block_reason", "retention", "completed_at_utc"]);
        JsonElement profile = credential.GetProperty("profile");
        M1Slice6CampaignLedgerEntry handoff = entries.Single(entry =>
            entry.State == M1Slice6CampaignState.CredentialEvidenceHandoff);
        int handoffIndex = entries.ToList().IndexOf(handoff);
        Assert.IsGreaterThan(0, handoffIndex);
        Assert.AreEqual(entries[handoffIndex - 1].EventHash,
            root.GetProperty("campaign_credential_handoff_event_hash").GetString());
        Assert.AreEqual("passed-active-verified", root.GetProperty("status").GetString());
        Assert.AreEqual(profile.GetProperty("access_profile_id").GetString(), root.GetProperty("profile_id").GetString());
        Assert.AreEqual(profile.GetProperty("generation_id").GetString(), root.GetProperty("generation_id").GetString());
        Assert.AreEqual(profile.GetProperty("target_fingerprint_sha256").GetString(),
            root.GetProperty("target_fingerprint_sha256").GetString());
        Assert.AreEqual(Sha(bytes), handoff.EvidenceSha256);
        JsonElement[] trace = root.GetProperty("native_call_trace").EnumerateArray().ToArray();
        Assert.AreEqual(4, trace.Length);
        string[] operations = ["CredReadW", "CredWriteW", "CredReadW", "CredFree"];
        string[] results = ["ERROR_NOT_FOUND", "success", "success", "released"];
        for (int index = 0; index < trace.Length; index++)
        {
            Assert.AreEqual(operations[index], trace[index].GetProperty("Operation").GetString());
            Assert.AreEqual(results[index], trace[index].GetProperty("Result").GetString());
            Assert.AreEqual(index + 1, trace[index].GetProperty("Sequence").GetInt32());
            Assert.AreEqual(profile.GetProperty("target_fingerprint_sha256").GetString(),
                trace[index].GetProperty("TargetFingerprintSha256").GetString());
        }
        CredentialNativeQualificationSupervisor.ValidateWp9ProductionEntryEvidence(
            root.GetProperty("entry_evidence").GetRawText(), "submitted");
        ValidateCanaries(JsonSerializer.SerializeToUtf8Bytes(root.GetProperty("canaries")));
        string marker = "M1_S6_CAMPAIGN_CREDENTIAL_EVIDENCE_ACCEPTANCE campaign_id=" + campaignId
            + " campaign_sha256=" + campaignSha + " manifest_id=" + root.GetProperty("manifest_id").GetString()
            + " manifest_sha256=" + root.GetProperty("manifest_sha256").GetString()
            + " evidence_id=wp9-production-profile-enrollment-evidence sha256=" + Sha(bytes)
            + " verdicts=credential,security,semantics,diff";
        Assert.AreEqual(1, recordLines.Count(line => line == marker));
    }

    private static void ValidateCanaries(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        RequireExactNames(root, ["SecretMatches", "RawTargetMatches", "RawTargetEncodings", "ScannedSurfaces"]);
        Assert.AreEqual(0, root.GetProperty("SecretMatches").GetInt32());
        Assert.AreEqual(0, root.GetProperty("RawTargetMatches").GetInt32());
        CollectionAssert.AreEqual(CanaryEncodings, root.GetProperty("RawTargetEncodings")
            .EnumerateArray().Select(value => value.GetString()).ToArray());
        string[] names = ["private protocol request", "private protocol response", "native call trace",
            "process command line", "process environment names"];
        string[] kinds = ["private-pipe-bytes", "private-pipe-bytes", "canonical-trace-bytes",
            "captured-text", "captured-text"];
        JsonElement[] surfaces = root.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
        Assert.AreEqual(names.Length, surfaces.Length);
        for (int index = 0; index < surfaces.Length; index++)
        {
            Assert.AreEqual(names[index], surfaces[index].GetProperty("Name").GetString());
            Assert.AreEqual(kinds[index], surfaces[index].GetProperty("Kind").GetString());
            Assert.IsGreaterThan(0L, surfaces[index].GetProperty("ByteCount").GetInt64());
            Assert.AreEqual(0, surfaces[index].GetProperty("SecretMatches").GetInt32());
            Assert.AreEqual(0, surfaces[index].GetProperty("RawTargetMatches").GetInt32());
        }
    }

    private static void RequireExactNames(JsonElement value, string[] expected) =>
        CollectionAssert.AreEqual(expected,
            value.EnumerateObject().Select(property => property.Name).ToArray());

    private static string FindRepositoryRoot(string path)
    {
        DirectoryInfo? current = new(Path.GetFullPath(path));
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class RecoveredSettlementAccounting : IM1Slice6CampaignProviderAccounting,
        IM1Slice6CampaignRecoveryAccounting
    {
        public M1Slice6CampaignRecoveredSettlement? TryRecoverKnownSettlement(M1Slice6CampaignStage stage,
            string canonicalRequestSha256) =>
            stage == M1Slice6CampaignStage.Qualification ? new(10, 1, 100, 90) : null;
        public M1Slice6CampaignAccountingAdmission Prepare(M1Slice6CampaignStageAuthority authority,
            M1Slice6CampaignIdentity campaignIdentity, DateTimeOffset now) => throw new AssertFailedException();
        public void RecordPossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now) =>
            throw new AssertFailedException();
        public void ReleaseBeforePossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now) =>
            throw new AssertFailedException();
        public M1Slice6CampaignAccountingSettlement PersistSettleAndReplay(
            M1Slice6CampaignAccountingAdmission admission, M1Slice6CampaignStageAuthority authority,
            M1Slice6CampaignStageBoundaryResult result) => throw new AssertFailedException();
    }

    private sealed class NullRecoveryAccounting : NoRecoveryAccounting, IM1Slice6CampaignRecoveryAccounting
    {
        public M1Slice6CampaignRecoveredSettlement? TryRecoverKnownSettlement(
            M1Slice6CampaignStage stage, string canonicalRequestSha256) => null;
    }

    private sealed class ThrowingRecoveryAccounting : NoRecoveryAccounting,
        IM1Slice6CampaignRecoveryAccounting
    {
        public M1Slice6CampaignRecoveredSettlement? TryRecoverKnownSettlement(
            M1Slice6CampaignStage stage, string canonicalRequestSha256) =>
            throw new IOException("synthetic recovery query fault");
    }

    private class NoRecoveryAccounting : IM1Slice6CampaignProviderAccounting
    {
        public M1Slice6CampaignAccountingAdmission Prepare(M1Slice6CampaignStageAuthority authority,
            M1Slice6CampaignIdentity campaignIdentity, DateTimeOffset now) => throw new AssertFailedException();
        public void RecordPossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now) =>
            throw new AssertFailedException();
        public void ReleaseBeforePossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now) =>
            throw new AssertFailedException();
        public M1Slice6CampaignAccountingSettlement PersistSettleAndReplay(
            M1Slice6CampaignAccountingAdmission admission, M1Slice6CampaignStageAuthority authority,
            M1Slice6CampaignStageBoundaryResult result) => throw new AssertFailedException();
    }

    private sealed class NeverDispatchBoundary : IM1Slice6CampaignStageExecutionBoundary
    {
        public int SendCount { get; private set; }
        public Task<M1Slice6CampaignStageBoundaryResult> ExecuteOnceAsync(
            M1Slice6CampaignStageAuthority authority, M1Slice6CampaignAccountingAdmission accounting,
            Func<DateTimeOffset, CancellationToken, Task> possibleStart,
            CancellationToken cancellationToken)
        {
            SendCount++;
            throw new AssertFailedException("Recovery attempted a second dispatch.");
        }
    }
}
