using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class M1Slice6SuccessorAuthorityTests
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly string[] NormalizedAbsentFields =
        ["response_id", "usage_entry_id", "replay_edge_id", "semantic_failure_code"];
    private static readonly string[] HistoricalEvidenceLimitations =
        ["actual-adapter-send-count-unverified", "credential-read-free-trace-not-independently-retained",
            "exact-containment-predicate-unavailable"];
    [TestMethod]
    public void CheckedInSuccessorAuthorityIsSchemaValidAndBindsTheReviewedSnapshot()
    {
        string repository = RepositoryRoot();
        string path = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-successor-campaign-authorization.v5.json");
        string sha = M1Slice6SuccessorAuthorityLoader.HashFile(path);
        string schema = Path.Combine(repository, "contracts", "repository",
            "m1-slice6-successor-campaign-authorization.v5.schema.json");
        ActiveRepositoryJsonSchemaValidator.Validate(File.ReadAllBytes(path), File.ReadAllBytes(schema),
            "infinium.repository.m1-slice6-successor-campaign-authorization/5.0.0");
        M1Slice6SuccessorCampaignAuthority authority =
            M1Slice6SuccessorAuthorityLoader.Campaign(path, sha);
        Assert.AreEqual("infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1",
            authority.CampaignId);
        Assert.AreEqual("e6788d546308a8ec8f7c3374c52cf8700a7a2245f52d213587e6a84d1d779b0d",
            authority.CredentialAccessAuthoritySha256);
        string reviewPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-successor-campaign-independent-review.v1.json");
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "campaign-authority", authority.CampaignId, sha, false);
        Assert.AreEqual("/root/successor-authority-review/campaign-final-20260820", review.ReviewId);
    }

    [TestMethod]
    public void IndependentReviewMustBeClosedAcceptedAndBindExactSubject()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository, ".successor-review-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "review.json");
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_identity = M1Slice6SuccessorAuthorityLoader.IndependentReviewSchemaV1,
                review_id = "/root/review/independent-review-test",
                review_kind = "campaign-authority",
                verdict = "accept",
                reviewer_id = "/root/successor-authority-review",
                independent = true,
                provider_effect_used = false,
                subject = new { id = "campaign-test", sha256 = new string('a', 64) },
                correction = new
                {
                    required = false,
                    defect_id = (string?)null,
                    diagnosis_disposition = (string?)null,
                    failure_evidence_id = (string?)null,
                    failure_evidence_sha256 = (string?)null,
                    candidate_commit = (string?)null
                },
                findings = Array.Empty<string>(),
                reviewed_at_utc = "2026-08-20T18:00:00.0000000+00:00",
            });
            File.WriteAllBytes(path, bytes);
            M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
                path, "campaign-authority", "campaign-test", new string('a', 64), false);
            Assert.AreEqual("/root/review/independent-review-test", review.ReviewId);
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice6SuccessorAuthorityLoader.Review(
                path, "campaign-authority", "different-campaign", new string('a', 64), false));
            File.WriteAllText(Path.Combine(directory, "prose.md"), "accepted");
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice6SuccessorAuthorityLoader.Review(
                Path.Combine(directory, "prose.md"), "campaign-authority", "campaign-test",
                new string('a', 64), false));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void EvidenceSupplementReviewRequiresTheVersionedV2ReviewContract()
    {
        string directory = Path.Combine(RepositoryRoot(),
            ".infinium-successor-supplement-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "review.json");
            string[] limitations = ["actual-adapter-send-count-unverified",
                "credential-read-free-trace-not-independently-retained",
                "exact-containment-predicate-unavailable"];
            void Write(string schema) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_identity = schema,
                review_id = "/root/review/supplement-test",
                review_kind = "attempt-evidence-supplement",
                verdict = "accept",
                reviewer_id = "/root/successor-authority-review",
                independent = true,
                provider_effect_used = false,
                subject = new { id = "supplement-test", sha256 = new string('b', 64) },
                correction = new
                {
                    required = false,
                    defect_id = (string?)null,
                    diagnosis_disposition = (string?)null,
                    failure_evidence_id = (string?)null,
                    failure_evidence_sha256 = (string?)null,
                    candidate_commit = (string?)null,
                },
                findings = schema == M1Slice6SuccessorAuthorityLoader.IndependentReviewSchema
                    ? limitations : Array.Empty<string>(),
                reviewed_at_utc = "2026-08-21T01:00:00.0000000+00:00",
            }));
            Write(M1Slice6SuccessorAuthorityLoader.IndependentReviewSchemaV1);
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice6SuccessorAuthorityLoader.Review(
                path, "attempt-evidence-supplement", "supplement-test", new string('b', 64), false));
            Write(M1Slice6SuccessorAuthorityLoader.IndependentReviewSchema);
            M1Slice6SuccessorIndependentReview accepted = M1Slice6SuccessorAuthorityLoader.Review(
                path, "attempt-evidence-supplement", "supplement-test", new string('b', 64), false);
            Assert.AreEqual("/root/review/supplement-test", accepted.ReviewId);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void HistoricalEvidenceSupplementRevalidatesFactsAndAcceptsOriginalLedgerBinding()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository,
            ".infinium-successor-supplement-acceptance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string campaignPath = Path.Combine(repository, "docs", "plans", "milestones", "m1",
                "slices", "s6", "m1-slice6-successor-campaign-authorization.v5.json");
            string campaignSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(campaignPath)));
            M1Slice6SuccessorCampaignAuthority campaign =
                M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
            DateTimeOffset start = new(2026, 8, 21, 1, 0, 0, TimeSpan.Zero);
            string ledgerPath = Path.Combine(directory, "ledger.jsonl");
            M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId,
                campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash, start);
            ledger.RecordIndependentReview("review-test", new string('1', 64), start.AddTicks(1));
            ledger.Admit(start.AddTicks(2));
            M1Slice6SuccessorAttemptIdentity attempt = new(M1Slice6CampaignStage.Qualification, 2,
                "supplement-attempt-2", "supplement-stage-2", new string('2', 64),
                "supplement-runtime-2", new string('3', 64), "supplement-request-2",
                "supplement-reservation-2", "supplement-fence-2");
            const long reservation = 110_080_000;
            ledger.ReserveAttempt(attempt, reservation, start.AddTicks(3));
            ledger.LatchPossibleStart(attempt, start.AddTicks(4));

            string canonicalName = "historical.canonical-request.json";
            string canonicalPath = Path.Combine(directory, canonicalName);
            byte[] canonical = "{}"u8.ToArray();
            File.WriteAllBytes(canonicalPath, canonical);
            string canonicalSha = Convert.ToHexStringLower(SHA256.HashData(canonical));
            byte[] originalBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchemaV1,
                status = "failure-review-pending",
                campaign_id = campaign.CampaignId,
                campaign_manifest_sha256 = campaign.ManifestSha256,
                stage = "Qualification",
                work_package = "WP9",
                attempt_id = attempt.AttemptId,
                attempt_ordinal = attempt.AttemptOrdinal,
                stage_manifest_id = attempt.StageManifestId,
                stage_manifest_sha256 = attempt.StageManifestSha256,
                runtime_authority_id = attempt.RuntimeAuthorityId,
                runtime_authority_sha256 = attempt.RuntimeAuthoritySha256,
                failure_stage = "helper-evidence",
                failure_disposition = "helper-evidence-failure",
                transport_disposition = "may-have-started-no-response",
                http_status = (int?)null,
                provider_error_type = (string?)null,
                provider_error_code = (string?)null,
                local_failure_code = "helper-containment-invalid",
                provider_response_id = (string?)null,
                client_request_id = attempt.RequestId,
                provider_request_id = (string?)null,
                response_bytes_existed = false,
                response_bytes_observed_lower_bound = 0,
                retained_response_bytes = (long?)null,
                provider_send_count = 1,
                dns_resolution_count = 1,
                retry_permitted = false,
                reserved_nano_usd = reservation,
                settled_nano_usd = 0,
                unresolved_hold_nano_usd = reservation,
                usage = (object?)null,
                rate_facts = Array.Empty<object>(),
                retained_artifacts = new
                {
                    canonical_request_path = canonicalName,
                    canonical_request_sha256 = canonicalSha,
                    raw_response_path = (string?)null,
                    raw_response_sha256 = (string?)null,
                    response_headers_path = (string?)null,
                    response_headers_sha256 = (string?)null,
                    native_trace_path = (string?)null,
                    native_trace_sha256 = (string?)null,
                    canary_evidence_path = (string?)null,
                    canary_evidence_sha256 = (string?)null,
                },
                accounting = new
                {
                    authorization_id = "supplement-authorization-2",
                    operation_id = "supplement-operation-2",
                    attempt_id = attempt.AttemptId,
                    request_id = attempt.RequestId,
                    reservation_id = attempt.ReservationId,
                    dispatch_fence_id = attempt.DispatchFenceId,
                    response_id = "",
                    usage_entry_id = "",
                    settlement_id = "m1s6-successor-m1-s6-successor-wp9-attempt-2/87920f3f-dc97-4c43-ac73-d0d819f0d646-settlement",
                    replay_edge_id = "",
                    response_persisted = false,
                    semantic_validation_id = (string?)null,
                    semantic_disposition = (string?)null,
                    semantic_result_sha256 = (string?)null,
                    semantic_provenance = new
                    {
                        source_acquisition_id = "", source_admission_id = "", admitted_artifact_id = "",
                        source_application_link_id = "", evidence_application_link_id = "",
                        candidate_id = "", hypothesis_id = "",
                    },
                    semantic_failure_code = "",
                },
            }, IndentedJson);
            string originalPath = Path.Combine(directory, "historical.v1.json");
            File.WriteAllBytes(originalPath, originalBytes);
            string originalSha = Convert.ToHexStringLower(SHA256.HashData(originalBytes));
            string evidenceId = "successor-attempt-evidence-" + attempt.AttemptId;
            ledger.RecordAttemptEvidence(attempt, evidenceId, originalSha,
                "helper-evidence-failure", false, reservation, 0, reservation, start.AddTicks(5));

            string normalizedPath = Path.Combine(directory, "historical.normalized.v1.json");
            File.WriteAllText(normalizedPath, M1Slice6SuccessorCampaignRunner
                .NormalizeKnownV1AbsentValues(originalBytes).ToJsonString(IndentedJson));
            string normalizedSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(normalizedPath)));
            byte[] normalizedBytes = File.ReadAllBytes(normalizedPath);
            Assert.ThrowsExactly<InvalidDataException>(() => ActiveRepositoryJsonSchemaValidator.Validate(
                normalizedBytes,
                File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                    "m1-slice6-successor-attempt-evidence.v1.schema.json")),
                M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchemaV1));
            ActiveRepositoryJsonSchemaValidator.Validate(normalizedBytes,
                File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                    "m1-slice6-successor-attempt-evidence-normalized-view.v1.schema.json")),
                M1Slice6SuccessorAuthorityLoader.HistoricalNormalizedAttemptEvidenceSchema);
            string supplementId = "successor-attempt-evidence-supplement-test";
            string supplementPath = Path.Combine(directory, "historical.supplement.v1.json");
            byte[] supplementBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSupplementSchema,
                supplement_id = supplementId,
                campaign_id = campaign.CampaignId,
                attempt_id = attempt.AttemptId,
                original_evidence_id = evidenceId,
                original_evidence_sha256 = originalSha,
                normalized_evidence_path = Path.GetFileName(normalizedPath),
                normalized_evidence_sha256 = normalizedSha,
                normalized_fields = NormalizedAbsentFields,
                accepted_claims = new
                {
                    possible_start_and_accounting = "accepted",
                    actual_adapter_send_count = "unverified",
                    exact_containment_predicate = "unavailable",
                    credential_read_free_trace = "not-independently-retained",
                },
                limitations = HistoricalEvidenceLimitations,
                provider_effect_used = false,
                created_at_utc = "2026-08-21T01:00:00.0000000+00:00",
            }, IndentedJson);
            File.WriteAllBytes(supplementPath, supplementBytes);
            string supplementSha = Convert.ToHexStringLower(SHA256.HashData(supplementBytes));
            string reviewPath = Path.Combine(directory, "supplement-review.v2.json");
            File.WriteAllBytes(reviewPath, JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_identity = M1Slice6SuccessorAuthorityLoader.IndependentReviewSchema,
                review_id = "/root/review/supplement-acceptance-test",
                review_kind = "attempt-evidence-supplement",
                verdict = "accept",
                reviewer_id = "/root/successor-authority-review",
                independent = true,
                provider_effect_used = false,
                subject = new { id = supplementId, sha256 = supplementSha },
                correction = new
                {
                    required = false, defect_id = (string?)null,
                    diagnosis_disposition = (string?)null, failure_evidence_id = (string?)null,
                    failure_evidence_sha256 = (string?)null, candidate_commit = (string?)null,
                },
                findings = HistoricalEvidenceLimitations,
                reviewed_at_utc = "2026-08-21T01:00:00.0000000+00:00",
            }, IndentedJson));

            string otherDirectory = Path.Combine(directory, "other");
            Directory.CreateDirectory(otherDirectory);
            string displaced = Path.Combine(otherDirectory, Path.GetFileName(normalizedPath));
            File.Copy(normalizedPath, displaced);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.AcceptAttemptSupplement(campaignPath, campaignSha,
                    ledgerPath, originalPath, displaced, supplementPath, reviewPath, start.AddTicks(6)));

            byte[] canonicalOriginal = File.ReadAllBytes(canonicalPath);
            File.WriteAllText(canonicalPath, "tampered");
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.AcceptAttemptSupplement(campaignPath, campaignSha,
                    ledgerPath, originalPath, normalizedPath, supplementPath, reviewPath, start.AddTicks(6)));
            File.WriteAllBytes(canonicalPath, canonicalOriginal);

            JsonObject staleIdentity = JsonNode.Parse(File.ReadAllBytes(normalizedPath))!.AsObject();
            staleIdentity["campaign_id"] = "stale-campaign";
            string staleIdentityPath = Path.Combine(directory, "historical.stale-identity.v1.json");
            File.WriteAllText(staleIdentityPath, staleIdentity.ToJsonString(IndentedJson));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.ValidateAttemptEvidence(campaignPath, campaign,
                    ledger, attempt, staleIdentityPath, historicalNormalizedV1: true));

            JsonObject staleAccounting = JsonNode.Parse(File.ReadAllBytes(normalizedPath))!.AsObject();
            staleAccounting["reserved_nano_usd"] = reservation - 1;
            string staleAccountingPath = Path.Combine(directory, "historical.stale-accounting.v1.json");
            File.WriteAllText(staleAccountingPath, staleAccounting.ToJsonString(IndentedJson));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.ValidateAttemptEvidence(campaignPath, campaign,
                    ledger, attempt, staleAccountingPath, historicalNormalizedV1: true));

            JsonObject staleSettlement = JsonNode.Parse(File.ReadAllBytes(normalizedPath))!.AsObject();
            staleSettlement["accounting"]!["settlement_id"] = "different-settlement";
            string staleSettlementPath = Path.Combine(directory, "historical.stale-settlement.v1.json");
            File.WriteAllText(staleSettlementPath, staleSettlement.ToJsonString(IndentedJson));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.ValidateAttemptEvidence(campaignPath, campaign,
                    ledger, attempt, staleSettlementPath, historicalNormalizedV1: true));

            JsonObject transformed = JsonNode.Parse(File.ReadAllBytes(normalizedPath))!.AsObject();
            transformed["campaign_id"] = "stale-campaign";
            File.WriteAllText(normalizedPath, transformed.ToJsonString(IndentedJson));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.AcceptAttemptSupplement(campaignPath, campaignSha,
                    ledgerPath, originalPath, normalizedPath, supplementPath, reviewPath, start.AddTicks(6)));
            File.WriteAllText(normalizedPath, M1Slice6SuccessorCampaignRunner
                .NormalizeKnownV1AbsentValues(originalBytes).ToJsonString(IndentedJson));

            M1Slice6SuccessorCampaignRunner.AcceptAttemptSupplement(campaignPath, campaignSha,
                ledgerPath, originalPath, normalizedPath, supplementPath, reviewPath, start.AddTicks(6));
            M1Slice6SuccessorCampaignLedger accepted = new(ledgerPath, campaign.CampaignId,
                campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash,
                start.AddTicks(7));
            Assert.AreEqual(M1Slice6SuccessorCampaignState.AttemptFailureAccepted, accepted.Current.State);
            Assert.AreEqual("/root/review/supplement-acceptance-test", accepted.Current.EvidenceId);
            Assert.AreEqual(originalSha, accepted.Entries[^2].EvidenceSha256);
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(originalPath));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ReviewedTemporaryAuthorityVerifiesEverySnapshotOriginByteWithoutAnyProviderEffect()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository, ".successor-snapshot-authority-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string slice = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
            JsonObject access = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-credential-access.v1.json")))!.AsObject();
            access["status"] = "reviewed-and-admitted";
            string accessPath = Path.Combine(directory, "access.json");
            File.WriteAllText(accessPath, access.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string accessSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(accessPath)));

            JsonObject campaign = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-campaign-authorization.v5.json")))!.AsObject();
            campaign["status"] = "owner-authorized-reviewed-and-admitted";
            string accessRelative = Path.GetRelativePath(repository, accessPath).Replace('\\', '/');
            campaign["credential_inheritance"]!["access_authority_path"] = accessRelative;
            campaign["credential_inheritance"]!["access_authority_sha256"] = accessSha;
            string campaignPath = Path.Combine(directory, "campaign.json");
            File.WriteAllText(campaignPath, campaign.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string campaignSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(campaignPath)));

            M1Slice6SuccessorCampaignAuthority loaded = M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha);
            Assert.AreEqual(accessSha, loaded.CredentialAccessAuthoritySha256);
            Assert.AreEqual("e3d23f0a11d66c243fd857e66e741d957abb8d470b45d653ae53f17d74fe4945",
                loaded.ProductStateSnapshotOriginSha256);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void InitialSnapshotAllowsOnlyTransientSqliteSharedMemoryDrift()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository, ".successor-transient-snapshot-test-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string destination = Path.Combine(directory, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        try
        {
            string slice = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
            JsonObject checkedInAccess = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-credential-access.v1.json")))!.AsObject();
            string checkedInSource = checkedInAccess["retained_product_state"]!["source_root_absolute"]!.GetValue<string>();
            JsonObject checkedInOrigin = JsonNode.Parse(File.ReadAllText(Path.Combine(
                checkedInAccess["retained_product_state"]!["successor_root_absolute"]!.GetValue<string>(),
                "successor-snapshot-origin.v1.json")))!.AsObject();
            foreach (JsonNode? item in checkedInOrigin["files"]!.AsArray())
            {
                string relative = item!["path"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar);
                string sourceFile = Path.Combine(checkedInSource, relative);
                string copiedSource = Path.Combine(source, relative);
                string copiedDestination = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copiedSource)!);
                Directory.CreateDirectory(Path.GetDirectoryName(copiedDestination)!);
                File.Copy(sourceFile, copiedSource);
                File.Copy(sourceFile, copiedDestination);
            }
            checkedInOrigin["source_root"] = source;
            checkedInOrigin["destination_root"] = destination;
            string originPath = Path.Combine(destination, "successor-snapshot-origin.v1.json");
            File.WriteAllText(originPath, checkedInOrigin.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            string sharedMemory = Path.Combine(destination, "data", "infinium.sqlite3-shm");
            byte[] sharedMemoryBytes = File.ReadAllBytes(sharedMemory);
            sharedMemoryBytes[0] ^= 0xff;
            File.WriteAllBytes(sharedMemory, sharedMemoryBytes);

            JsonObject access = checkedInAccess.DeepClone().AsObject();
            access["retained_product_state"]!["source_root_absolute"] = source;
            access["retained_product_state"]!["successor_root_absolute"] = destination;
            access["retained_product_state"]!["snapshot_origin_sha256"] =
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(originPath)));
            string accessPath = Path.Combine(directory, "access.json");
            File.WriteAllText(accessPath, access.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string accessSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(accessPath)));

            JsonObject campaign = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-campaign-authorization.v5.json")))!.AsObject();
            campaign["credential_inheritance"]!["access_authority_path"] =
                Path.GetRelativePath(repository, accessPath).Replace('\\', '/');
            campaign["credential_inheritance"]!["access_authority_sha256"] = accessSha;
            string campaignPath = Path.Combine(directory, "campaign.json");
            File.WriteAllText(campaignPath, campaign.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            _ = M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(campaignPath))));

            string retainedPayload = Directory.GetFiles(Path.Combine(destination, "payloads"), "*", SearchOption.AllDirectories).Single();
            File.AppendAllText(retainedPayload, "changed");
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(campaignPath)))));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ProductStateCheckpointIsDeterministicAndDetectsDatabaseOrRetainedFileMutation()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-successor-state-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "data"));
        Directory.CreateDirectory(Path.Combine(root, "payloads"));
        try
        {
            string database = Path.Combine(root, "data", "infinium.sqlite3");
            using (SqliteConnection connection = new($"Data Source={database};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE exact_state(id INTEGER PRIMARY KEY,value TEXT NOT NULL); INSERT INTO exact_state VALUES(1,'one');";
                command.ExecuteNonQuery();
            }
            string retained = Path.Combine(root, "payloads", "retained");
            File.WriteAllText(retained, "first");
            string first = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root);
            Assert.AreEqual(first, M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root));
            using (SqliteConnection connection = new($"Data Source={database};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO exact_state VALUES(2,'two');";
                command.ExecuteNonQuery();
            }
            string databaseChanged = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root);
            Assert.AreNotEqual(first, databaseChanged);
            File.WriteAllText(retained, "second");
            Assert.AreNotEqual(databaseChanged,
                M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string RepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "Infinium.sln"))) { return current; }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
