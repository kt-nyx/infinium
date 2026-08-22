using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class M1Slice6SuccessorAuthorityTests
{
    [TestMethod]
    public void SuccessorSemanticOutputSchemasTypeEveryConstantAndEnumString()
    {
        foreach (M1Slice6CampaignStage stage in new[]
                 { M1Slice6CampaignStage.SourceClaimExtraction, M1Slice6CampaignStage.CandidateInvestigation })
        {
            using JsonDocument document = JsonDocument.Parse(M1Slice6SuccessorAttemptMaterializer.OutputSchema(stage));
            JsonElement rootProperties = document.RootElement.GetProperty("properties");
            Assert.AreEqual("string", rootProperties.GetProperty("schema_id").GetProperty("type").GetString());
            Assert.AreEqual("string", rootProperties.GetProperty("schema_version").GetProperty("type").GetString());
            JsonElement transcriptProperties = rootProperties.GetProperty("transcripts")
                .GetProperty("items").GetProperty("properties");
            Assert.AreEqual("string", transcriptProperties.GetProperty("prompt_id").GetProperty("type").GetString());
            Assert.AreEqual("string", transcriptProperties.GetProperty("response_state").GetProperty("type").GetString());
        }
    }

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly string[] NormalizedAbsentFields =
        ["response_id", "usage_entry_id", "replay_edge_id", "semantic_failure_code"];
    private static readonly string[] HistoricalEvidenceLimitations =
        ["actual-adapter-send-count-unverified", "credential-read-free-trace-not-independently-retained",
            "exact-containment-predicate-unavailable"];
    private static readonly string[] RawTargetEncodings = ["utf-8", "utf-16le"];
    private static readonly string[] InvalidTransportDiagnosticTypes =
    [
        "HttpRequestError.ConnectionError.SocketError.NotCanonical",
        "HttpRequestError.ConnectionError.SocketError.Success",
        "HttpRequestError.ConnectionError.SocketError.999",
    ];

    [TestMethod]
    public void CheckedInGeneration2CampaignClosesReplacementAndImmutableLedgerLineage()
    {
        string repository = RepositoryRoot();
        string slice = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
        string campaignPath = Path.Combine(slice, "m1-slice6-successor-campaign-authorization.v7.json");
        string campaignSha = M1Slice6SuccessorAuthorityLoader.HashFile(campaignPath);
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha, requireRolloverBaseline: false);
        M1Slice6SuccessorCampaignAuthority active = M1Slice6SuccessorAuthorityLoader.Campaign(
            campaignPath, campaignSha, requireRolloverBaseline: true);
        Assert.AreEqual("g-8b25e655d13f42cdb35f5d59f599bd05", active.CredentialGenerationId);
        Assert.AreEqual("infinium.m1-s6.development-continuation/20260821",
            active.CredentialAccessAuthorityId);
        Assert.AreEqual("d4a93a3e09c2a5e6c489a795ecec971d2b4aae4dd5796be65b55326f0d59504a",
            active.CredentialManifestSha256);
        _ = M1Slice6SuccessorAuthorityLoader.Review(
            Path.Combine(slice, "m1-slice6-successor-campaign-v7-independent-review.v3.json"),
            "campaign-authority", campaign.CampaignId, campaign.ManifestSha256,
            false, successorV6: true);
        Assert.AreEqual("infinium.m1-s6.successor-campaign-v7/3e457821-389a-4ea8-a4c0-aed9da3b5966",
            campaign.CampaignId);
        Assert.AreEqual("g-e6b6a3f21ad74108ba65955850349f83", campaign.CredentialGenerationId);
        Assert.AreEqual("49b71673b144dc5c5118f4dbfec52d22ca9f8f380ebe4cb7f9d7959746d93939",
            campaign.CredentialManifestSha256);
        string helper = Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin", "Debug", "net10.0",
            "Infinium.CredentialHelper.exe");
        M1Slice6CampaignProductionStageBoundary providerBoundary = new(helper,
            M1Slice6SuccessorAuthorityLoader.HashFile(helper),
            Path.Combine(slice, "wp9-production-profile-authorization.v5.json"),
            campaign.CredentialManifestSha256, campaign.CredentialManifestId);
        Assert.AreEqual(2UL, (ulong)typeof(M1Slice6CampaignProductionStageBoundary)
            .GetField("generationOrdinal", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(providerBoundary)!);
        Assert.AreNotEqual("3ebb463346786506210498ea65c68af2768d2f73020e0e8e8b05c5d39b49f54e",
            M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(campaign.ProductStateRoot));
        string alternateRoot = Path.Combine(repository,
            ".infinium-rejected-v4-fork-" + Guid.NewGuid().ToString("N"));
        try
        {
            string amendmentPath = Path.Combine(slice, "m1-slice6-development-campaign-amendment.v2.json");
            string amendmentSha = M1Slice6SuccessorAuthorityLoader.HashFile(amendmentPath);
            string rejectedHardBudgetLedger = Path.Combine(alternateRoot, "legacy-hard-budget-ledger.jsonl");
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                M1Slice6SuccessorCampaignRunner.InitializeHardBudget(campaignPath, campaignSha,
                    amendmentPath, amendmentSha, rejectedHardBudgetLedger,
                    Path.Combine(alternateRoot, "missing-review.json"),
                    new DateTimeOffset(2026, 8, 22, 1, 0, 0, TimeSpan.Zero)));
            Assert.IsFalse(File.Exists(rejectedHardBudgetLedger));
            string rejectedLegacyLedger = Path.Combine(alternateRoot, "legacy-v2-ledger.jsonl");
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                M1Slice6SuccessorCampaignRunner.InitializeCampaign(campaignPath, campaignSha,
                    rejectedLegacyLedger, Path.Combine(alternateRoot, "missing-review.json"),
                    new DateTimeOffset(2026, 8, 22, 1, 0, 0, TimeSpan.Zero)));
            Assert.IsFalse(File.Exists(rejectedLegacyLedger));
            string output = Path.Combine(alternateRoot, "attempt");
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorAttemptMaterializer.Materialize(campaignPath, campaignSha,
                    amendmentPath, amendmentSha,
                    Path.Combine(alternateRoot, "ledger.v4.jsonl"), "Qualification", 9, output,
                    new string('a', 40),
                    Path.Combine(repository, "src", "Infinium.Coordinator", "bin", "Debug", "net10.0",
                        "Infinium.Coordinator.exe"),
                    Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin", "Debug", "net10.0",
                        "Infinium.CredentialHelper.exe"),
                    new DateTimeOffset(2026, 8, 22, 1, 0, 0, TimeSpan.Zero)));
            Assert.IsFalse(Directory.Exists(output));
        }
        finally
        {
            if (Directory.Exists(alternateRoot)) { Directory.Delete(alternateRoot, recursive: true); }
        }
    }

    [TestMethod]
    public void DevelopmentGeneration3MaterializesAndFinalizesWithoutPerAttemptReview()
    {
        string repository = RepositoryRoot();
        string slice = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
        string campaignPath = Path.Combine(slice, "m1-slice6-successor-campaign-authorization.v7.json");
        string amendmentPath = Path.Combine(slice, "m1-slice6-development-campaign-amendment.v2.json");
        string credentialPath = Path.Combine(slice,
            "m1-slice6-successor-credential-replacement-generation-3-authorization.v2.json");
        string continuationPath = Path.Combine(slice, "development-continuation.md");
        string ledgerPath = Path.Combine(repository, "artifacts", "m1-slice6", "successor-campaign",
            "ledger.v4.jsonl");
        string output = Path.Combine(Path.GetDirectoryName(ledgerPath)!,
            ".development-provider-materializer-test-" + Guid.NewGuid().ToString("N"));
        string productCopy = Path.Combine(repository,
            ".development-provider-accounting-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string campaignSha = M1Slice6SuccessorAuthorityLoader.HashFile(campaignPath);
            string amendmentSha = M1Slice6SuccessorAuthorityLoader.HashFile(amendmentPath);
            M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha, requireRolloverBaseline: true);
            M1Slice6HardBudgetAuthority amendment = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
                amendmentPath, amendmentSha, campaign);
            string coordinator = Path.Combine(repository, "src", "Infinium.Coordinator", "bin", "Debug", "net10.0",
                "Infinium.Coordinator.exe");
            string helper = Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin", "Debug", "net10.0",
                "Infinium.CredentialHelper.exe");
            string implementationCommit = typeof(M1Slice6SuccessorCampaignRunner).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[^1];
            DateTimeOffset now = DateTimeOffset.UtcNow;
            M1Slice6SuccessorAttemptMaterializer.Materialize(campaignPath, campaignSha,
                amendmentPath, amendmentSha, ledgerPath, "Qualification", 10, output,
                implementationCommit, coordinator, helper, now);
            string stagePath = Path.Combine(output, "stage-attempt.v6.json");
            string candidatePath = Path.Combine(output, "runtime-candidate.v2.json");
            string runtimePath = Path.Combine(output, "runtime-authority.v3.json");
            M1Slice6SuccessorAttemptMaterializer.FinalizeRuntime(campaignPath, campaignSha,
                amendmentPath, amendmentSha, stagePath, candidatePath, continuationPath, runtimePath,
                now, now.AddMinutes(5));
            M1Slice6SuccessorRuntimeAuthority runtime = M1Slice6SuccessorAuthorityLoader.Runtime(
                runtimePath, M1Slice6SuccessorAuthorityLoader.HashFile(runtimePath), coordinator,
                helper, amendment, now.AddMinutes(1), requireEffectAdmission: false);
            Assert.AreEqual(10, runtime.AttemptOrdinal);
            Assert.AreEqual("infinium.m1-s6.development-continuation/20260821", runtime.ReviewEvidenceId);
            M1Slice6CampaignProductionStageBoundary boundary = new(helper,
                M1Slice6SuccessorAuthorityLoader.HashFile(helper), credentialPath,
                campaign.CredentialManifestSha256, campaign.CredentialManifestId);
            Assert.AreEqual(3UL, (ulong)typeof(M1Slice6CampaignProductionStageBoundary)
                .GetField("generationOrdinal", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(boundary)!);
            Assert.AreEqual(Path.GetFullPath(credentialPath),
                M1Slice6SuccessorCampaignRunner.ActiveCredentialManifestPath(
                    repository, campaignPath, campaign));
            using (StoragePaths storage = new(productCopy)) { storage.Create(); }
            foreach (string directory in Directory.GetDirectories(
                campaign.ProductStateRoot, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(Path.Combine(productCopy,
                    Path.GetRelativePath(campaign.ProductStateRoot, directory)));
            }
            foreach (string source in Directory.GetFiles(
                campaign.ProductStateRoot, "*", SearchOption.AllDirectories))
            {
                File.Copy(source, Path.Combine(productCopy,
                    Path.GetRelativePath(campaign.ProductStateRoot, source)), overwrite: true);
            }
            using M1Slice6CampaignSqliteProviderAccounting accounting = new(
                productCopy, credentialPath, campaign.CredentialManifestSha256, now);
        }
        finally
        {
            if (Directory.Exists(output)) { Directory.Delete(output, recursive: true); }
            if (Directory.Exists(productCopy)) { Directory.Delete(productCopy, recursive: true); }
        }
    }

    [TestMethod]
    public void CredentialRolloverLedgerImportsSequence39AndAdmitsFreshOrdinal9WithoutCountCap()
    {
        string repository = RepositoryRoot();
        string root = Path.Combine(repository, ".infinium-successor-v4-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string campaignPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
                "m1-slice6-successor-campaign-authorization.v7.json");
            string campaignSha = M1Slice6SuccessorAuthorityLoader.HashFile(campaignPath);
            M1Slice6SuccessorCampaignAuthority campaign =
                M1Slice6SuccessorAuthorityLoader.Campaign(
                    campaignPath, campaignSha, requireRolloverBaseline: false);
            string predecessor = Path.Combine(repository, "artifacts", "m1-slice6", "successor-campaign",
                "ledger.v3.jsonl");
            string ledgerPath = Path.Combine(root, "ledger.v4.jsonl");
            DateTimeOffset now = new DateTimeOffset(2026, 8, 22, 0, 55, 0, TimeSpan.Zero).AddTicks(1);
            M1Slice6SuccessorCampaignLedgerV3 ledger = new(ledgerPath, campaign.CampaignId,
                campaign.ManifestSha256, predecessor,
                "9a1bbb048445f3eb969e16b894f8b9d8347cba5ab89c9d3c83be66e33fda5a25",
                "infinium.m1-s6.successor-credential-replacement-evidence/0dd95374-f9e1-400a-888d-ffd56f680214",
                "4778cb8e9275c34a5eab70d32635261f5ebf9eda75247960e7389e01fe448feb",
                "test-generation-2-campaign-review", new string('a', 64), now);
            Assert.AreEqual(40L, ledger.Current.Sequence);
            Assert.AreEqual(M1Slice6SuccessorCampaignV3State.CredentialAuthorityRolledOver,
                ledger.Current.State);
            Assert.AreEqual(910_560_000L, ledger.CommittedNanoUsd);
            M1Slice6SuccessorAttemptIdentity attempt = new(M1Slice6CampaignStage.Qualification, 9,
                "test-v4-wp9-attempt-9", "test-v4-wp9-stage-9", new string('b', 64),
                "test-v4-wp9-runtime-9", new string('c', 64), "test-v4-wp9-request-9",
                "test-v4-wp9-reservation-9", "test-v4-wp9-fence-9");
            ledger.ReserveAttempt(attempt, 110_080_000, now.AddTicks(1));
            Assert.AreEqual(41L, ledger.Current.Sequence);
            Assert.AreEqual(1_020_640_000L, ledger.CommittedNanoUsd);
            ActiveRepositoryJsonSchemaValidator.Validate(
                System.Text.Encoding.UTF8.GetBytes(File.ReadLines(ledgerPath).First()),
                File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                    "m1-slice6-successor-campaign-ledger.v4.schema.json")),
                "infinium.repository.m1-slice6-successor-campaign-ledger-entry/4.0.0");
            string unknownMemberLedger = Path.Combine(root, "unknown-member-ledger.v4.jsonl");
            string first = File.ReadLines(ledgerPath).First();
            File.WriteAllText(unknownMemberLedger,
                first.Insert(1, "\"unknown_member\":\"must-reject\",") + Environment.NewLine);
            Assert.ThrowsExactly<JsonException>(() => new M1Slice6SuccessorCampaignLedgerV3(
                unknownMemberLedger, campaign.CampaignId, campaign.ManifestSha256, predecessor,
                "9a1bbb048445f3eb969e16b894f8b9d8347cba5ab89c9d3c83be66e33fda5a25",
                "infinium.m1-s6.successor-credential-replacement-evidence/0dd95374-f9e1-400a-888d-ffd56f680214",
                "4778cb8e9275c34a5eab70d32635261f5ebf9eda75247960e7389e01fe448feb",
                null, null, now));
            Assert.ThrowsExactly<InvalidDataException>(() => new M1Slice6SuccessorCampaignLedgerV3(
                Path.Combine(root, "tampered.jsonl"), campaign.CampaignId, campaign.ManifestSha256,
                predecessor, new string('0', 64), "evidence", new string('1', 64),
                "review", new string('2', 64), now));
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }
    [TestMethod]
    public void CheckedInSuccessorAuthorityIsSchemaValidAndBindsTheReviewedSnapshot()
    {
        string repository = RepositoryRoot();
        string path = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-successor-campaign-authorization.v6.json");
        string sha = M1Slice6SuccessorAuthorityLoader.HashFile(path);
        string schema = Path.Combine(repository, "contracts", "repository",
            "m1-slice6-successor-campaign-authorization.v6.schema.json");
        ActiveRepositoryJsonSchemaValidator.Validate(File.ReadAllBytes(path), File.ReadAllBytes(schema),
            "infinium.repository.m1-slice6-successor-campaign-authorization/6.0.0");
        M1Slice6SuccessorCampaignAuthority authority =
            M1Slice6SuccessorAuthorityLoader.Campaign(path, sha);
        Assert.AreEqual("infinium.m1-s6.successor-campaign-v6/20260821-hard-budget",
            authority.CampaignId);
        Assert.AreEqual("5c76484a5835bb118a7143da25369b9571d96c39d8f533dcce1534f0b3ec84a6",
            authority.CredentialAccessAuthoritySha256);
    }

    [TestMethod]
    public void FreshAttemptMaterializerIsEffectFreeSchemaValidAndRequiresNextOrdinal()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository, ".infinium-successor-materializer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string campaignPath = Path.Combine(repository, "docs", "plans", "milestones", "m1",
                "slices", "s6", "m1-slice6-successor-campaign-authorization.v6.json");
            string amendmentPath = Path.Combine(repository, "docs", "plans", "milestones", "m1",
                "slices", "s6", "m1-slice6-development-campaign-amendment.v2.json");
            string campaignSha = M1Slice6SuccessorAuthorityLoader.HashFile(campaignPath);
            string amendmentSha = M1Slice6SuccessorAuthorityLoader.HashFile(amendmentPath);
            M1Slice6SuccessorCampaignAuthority campaign =
                M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
            M1Slice6HardBudgetAuthority amendment =
                M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(amendmentPath, amendmentSha, campaign);
            DateTimeOffset now = new(2026, 8, 21, 18, 0, 0, TimeSpan.Zero);
            M1Slice6SuccessorCampaignLedger predecessor = new(amendment.PredecessorLedgerPath,
                "infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1",
                "ff0a8a1cd499f5639c85fa7d43737643dc4b3494643d150b72d2772fc2fc18ef",
                campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
            string ledgerPath = Path.Combine(directory, "ledger.v3.jsonl");
            _ = new M1Slice6SuccessorCampaignLedgerV3(ledgerPath, campaign.CampaignId,
                campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash, 8,
                amendment.PredecessorEventHash, amendment.AmendmentId, amendment.ManifestSha256,
                "test-amendment-review", new string('a', 64), predecessor.Current.Wp9PossibleStarts,
                predecessor.Current.Wp10PossibleStarts, predecessor.Current.Wp11PossibleStarts,
                predecessor.Current.Wp9Authoritative, predecessor.Current.Wp10Authoritative,
                predecessor.Current.Wp11Authoritative, predecessor.Current.SuccessorCumulativeReservedNanoUsd,
                predecessor.Current.SuccessorUnresolvedNanoUsd,
                predecessor.Current.SuccessorSettledNanoUsd, now);
            M1Slice6SuccessorCampaignLedgerV3 expiredReopen =
                M1Slice6SuccessorCampaignRunner.OpenHardBudgetLedger(campaign, amendment,
                    ledgerPath, amendment.ExpiresAtUtc.AddTicks(1), requireExisting: true);
            Assert.AreEqual(M1Slice6SuccessorCampaignV3State.HardBudgetAmended,
                expiredReopen.Current.State);
            string coordinatorAssembly = typeof(M1Slice6SuccessorCampaignRunner).Assembly.Location;
            string coordinator = Path.Combine(repository, "src", "Infinium.Coordinator", "bin",
                "Debug", "net10.0", "Infinium.Coordinator.exe");
            string implementationCommit = typeof(M1Slice6SuccessorCampaignRunner).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion
                .Split('+')[^1];
            string helper = Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin",
                "Debug", "net10.0", "Infinium.CredentialHelper.exe");
            string wrongBinary = Path.Combine(directory, "wrong-binary");
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorAttemptMaterializer.Materialize(campaignPath, campaignSha,
                    amendmentPath, amendmentSha, ledgerPath, "Qualification", 3, wrongBinary,
                    implementationCommit, coordinatorAssembly, helper, now.AddTicks(1)));
            Assert.IsFalse(Directory.Exists(wrongBinary));
            string rejected = Path.Combine(directory, "wrong-ordinal");
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                M1Slice6SuccessorAttemptMaterializer.Materialize(campaignPath, campaignSha,
                    amendmentPath, amendmentSha, ledgerPath, "Qualification", 4, rejected,
                    new string('b', 40), coordinator, helper, now.AddTicks(1)));
            Assert.IsFalse(Directory.Exists(rejected));

            string output = Path.Combine(directory, "wp9-attempt-3-rederived");
            M1Slice6SuccessorAttemptMaterializer.Materialize(campaignPath, campaignSha,
                amendmentPath, amendmentSha, ledgerPath, "Qualification", 3, output,
                implementationCommit, coordinator, helper, now.AddTicks(2));
            string stagePath = Path.Combine(output, "stage-attempt.v6.json");
            string candidatePath = Path.Combine(output, "runtime-candidate.v2.json");
            ActiveRepositoryJsonSchemaValidator.Validate(File.ReadAllBytes(stagePath),
                File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                    "m1-slice6-successor-stage-attempt.v6.schema.json")),
                M1Slice6SuccessorAuthorityLoader.StageSchema);
            ActiveRepositoryJsonSchemaValidator.Validate(File.ReadAllBytes(candidatePath),
                File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                    "m1-slice6-successor-runtime-candidate.v2.schema.json")),
                M1Slice6SuccessorAuthorityLoader.RuntimeCandidateSchema);
            Assert.AreEqual(3, JsonNode.Parse(File.ReadAllBytes(stagePath))!["attempt"]!["ordinal"]!.GetValue<int>());
            JsonNode candidateNode = JsonNode.Parse(File.ReadAllBytes(candidatePath))!;
            string candidateId = candidateNode["candidate_id"]!.GetValue<string>();
            string candidateSha = M1Slice6SuccessorAuthorityLoader.HashFile(candidatePath);
            string reviewPath = Path.Combine(output, "runtime-review.v3.json");
            File.WriteAllBytes(reviewPath, JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_identity = M1Slice6SuccessorAuthorityLoader.IndependentReviewSchemaV3,
                review_id = "/root/review/runtime-attempt-v6-test",
                review_kind = "runtime-attempt",
                verdict = "accept",
                reviewer_id = "/root/successor-authority-review",
                independent = true,
                provider_effect_used = false,
                subject = new { id = candidateId, sha256 = candidateSha },
                correction = new
                {
                    required = false,
                    defect_id = (string?)null,
                    diagnosis_disposition = (string?)null,
                    failure_evidence_id = (string?)null,
                    failure_evidence_sha256 = (string?)null,
                    candidate_commit = (string?)null,
                },
                findings = Array.Empty<string>(),
                reviewed_at_utc = "2026-08-21T18:00:00.0000000+00:00",
            }));
            string runtimePath = Path.Combine(output, "runtime-authority.v3.json");
            M1Slice6SuccessorAttemptMaterializer.FinalizeRuntime(campaignPath, campaignSha,
                amendmentPath, amendmentSha, stagePath, candidatePath, reviewPath, runtimePath,
                now.AddTicks(3), now.AddMinutes(5));
            ActiveRepositoryJsonSchemaValidator.Validate(File.ReadAllBytes(runtimePath),
                File.ReadAllBytes(Path.Combine(repository, "contracts", "json-schema",
                    "provider-effect-runtime-authority.v3.schema.json")),
                M1Slice6SuccessorAuthorityLoader.RuntimeSchema);
            M1Slice6SuccessorRuntimeAuthority runtime = M1Slice6SuccessorAuthorityLoader.Runtime(
                runtimePath, M1Slice6SuccessorAuthorityLoader.HashFile(runtimePath), coordinator,
                helper, amendment, now.AddMinutes(10), requireEffectAdmission: false);
            Assert.AreEqual(3, runtime.AttemptOrdinal);
            Assert.AreEqual("/root/review/runtime-attempt-v6-test", runtime.ReviewEvidenceId);
            M1Slice6SuccessorRuntimeAuthority recovered =
                M1Slice6SuccessorAuthorityLoader.RuntimeForRecovery(runtimePath,
                    M1Slice6SuccessorAuthorityLoader.HashFile(runtimePath), amendment,
                    amendment.ExpiresAtUtc.AddDays(1));
            Assert.AreEqual(runtime.AuthorityId, recovered.AuthorityId);
            Assert.AreEqual(runtime.CoordinatorSha256, recovered.CoordinatorSha256);
            Assert.AreEqual(runtime.HelperSha256, recovered.HelperSha256);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void StartedAmbiguousRecoveryClosesExactRetainedAttemptOnceWithZeroEffect()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository,
            ".successor-ambiguous-recovery-test-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string product = Path.Combine(directory, "product-state");
        string outputRoot = Path.Combine(directory, "campaign");
        Directory.CreateDirectory(source);
        using (StoragePaths synthetic = new(product)) { synthetic.Create(); }
        Directory.CreateDirectory(outputRoot);
        try
        {
            string slice = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
            string checkedAccessPath = Path.Combine(slice,
                "m1-slice6-successor-credential-access.v2.json");
            JsonObject access = JsonNode.Parse(File.ReadAllBytes(checkedAccessPath))!.AsObject();
            string retainedSource = access["retained_product_state"]!["source_root_absolute"]!
                .GetValue<string>();
            string retainedProduct = access["retained_product_state"]!["successor_root_absolute"]!
                .GetValue<string>();
            JsonObject origin = JsonNode.Parse(File.ReadAllBytes(Path.Combine(retainedProduct,
                "successor-snapshot-origin.v1.json")))!.AsObject();
            foreach (JsonNode? item in origin["files"]!.AsArray())
            {
                string relative = item!["path"]!.GetValue<string>()
                    .Replace('/', Path.DirectorySeparatorChar);
                string sourceFile = Path.Combine(retainedSource, relative);
                string copiedSource = Path.Combine(source, relative);
                string copiedProduct = Path.Combine(product, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copiedSource)!);
                Directory.CreateDirectory(Path.GetDirectoryName(copiedProduct)!);
                File.Copy(sourceFile, copiedSource);
                File.Copy(sourceFile, copiedProduct);
            }
            origin["source_root"] = source;
            origin["destination_root"] = product;
            string originPath = Path.Combine(product, "successor-snapshot-origin.v1.json");
            File.WriteAllText(originPath, origin.ToJsonString(IndentedJson));
            access["retained_product_state"]!["source_root_absolute"] = source;
            access["retained_product_state"]!["successor_root_absolute"] = product;
            access["retained_product_state"]!["snapshot_origin_sha256"] =
                M1Slice6SuccessorAuthorityLoader.HashFile(originPath);
            string accessPath = Path.Combine(directory, "access.json");
            File.WriteAllText(accessPath, access.ToJsonString(IndentedJson));
            string accessSha = M1Slice6SuccessorAuthorityLoader.HashFile(accessPath);

            JsonObject campaignNode = JsonNode.Parse(File.ReadAllBytes(Path.Combine(slice,
                "m1-slice6-successor-campaign-authorization.v6.json")))!.AsObject();
            campaignNode["credential_inheritance"]!["access_authority_path"] =
                Path.GetRelativePath(repository, accessPath).Replace('\\', '/');
            campaignNode["credential_inheritance"]!["access_authority_sha256"] = accessSha;
            string campaignPath = Path.Combine(directory, "campaign.json");
            File.WriteAllText(campaignPath, campaignNode.ToJsonString(IndentedJson));
            string campaignSha = M1Slice6SuccessorAuthorityLoader.HashFile(campaignPath);
            M1Slice6SuccessorCampaignAuthority campaign =
                M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
            string amendmentPath = Path.Combine(slice,
                "m1-slice6-development-campaign-amendment.v2.json");
            string amendmentSha = M1Slice6SuccessorAuthorityLoader.HashFile(amendmentPath);
            M1Slice6HardBudgetAuthority amendment =
                M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
                    amendmentPath, amendmentSha, campaign);
            DateTimeOffset now = new(2026, 8, 21, 18, 0, 0, TimeSpan.Zero);
            M1Slice6SuccessorCampaignLedger predecessor = new(amendment.PredecessorLedgerPath,
                "infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1",
                "ff0a8a1cd499f5639c85fa7d43737643dc4b3494643d150b72d2772fc2fc18ef",
                campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
            string ledgerPath = Path.Combine(outputRoot, "ledger.v3.jsonl");
            M1Slice6SuccessorCampaignLedgerV3 ledger = new(ledgerPath, campaign.CampaignId,
                campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash, 8,
                amendment.PredecessorEventHash, amendment.AmendmentId, amendment.ManifestSha256,
                "synthetic-amendment-review", new string('a', 64),
                predecessor.Current.Wp9PossibleStarts, predecessor.Current.Wp10PossibleStarts,
                predecessor.Current.Wp11PossibleStarts, predecessor.Current.Wp9Authoritative,
                predecessor.Current.Wp10Authoritative, predecessor.Current.Wp11Authoritative,
                predecessor.Current.SuccessorCumulativeReservedNanoUsd,
                predecessor.Current.SuccessorUnresolvedNanoUsd,
                predecessor.Current.SuccessorSettledNanoUsd, now);
            string coordinator = Path.Combine(repository, "src", "Infinium.Coordinator", "bin",
                "Debug", "net10.0", "Infinium.Coordinator.exe");
            string helper = Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin",
                "Debug", "net10.0", "Infinium.CredentialHelper.exe");
            string implementationCommit = typeof(M1Slice6SuccessorCampaignRunner).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion
                .Split('+')[^1];
            string attemptDirectory = Path.Combine(outputRoot, "attempt");
            M1Slice6SuccessorAttemptMaterializer.Materialize(campaignPath, campaignSha,
                amendmentPath, amendmentSha, ledgerPath, "Qualification", 3, attemptDirectory,
                implementationCommit, coordinator, helper, now.AddTicks(1));
            string stagePath = Path.Combine(attemptDirectory, "stage-attempt.v6.json");
            string stageSha = M1Slice6SuccessorAuthorityLoader.HashFile(stagePath);
            string candidatePath = Path.Combine(attemptDirectory, "runtime-candidate.v2.json");
            JsonNode candidate = JsonNode.Parse(File.ReadAllBytes(candidatePath))!;
            string candidateId = candidate["candidate_id"]!.GetValue<string>();
            string candidateSha = M1Slice6SuccessorAuthorityLoader.HashFile(candidatePath);
            string reviewPath = Path.Combine(attemptDirectory, "runtime-review.v3.json");
            File.WriteAllBytes(reviewPath, JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_identity = M1Slice6SuccessorAuthorityLoader.IndependentReviewSchemaV3,
                review_id = "/root/review/synthetic-ambiguous-recovery",
                review_kind = "runtime-attempt",
                verdict = "accept",
                reviewer_id = "/root/successor-design-review",
                independent = true,
                provider_effect_used = false,
                subject = new { id = candidateId, sha256 = candidateSha },
                correction = new
                {
                    required = false,
                    defect_id = (string?)null,
                    diagnosis_disposition = (string?)null,
                    failure_evidence_id = (string?)null,
                    failure_evidence_sha256 = (string?)null,
                    candidate_commit = (string?)null,
                },
                findings = Array.Empty<string>(),
                reviewed_at_utc = "2026-08-21T18:00:00.0000000+00:00",
            }));
            string runtimePath = Path.Combine(attemptDirectory, "runtime-authority.v3.json");
            M1Slice6SuccessorAttemptMaterializer.FinalizeRuntime(campaignPath, campaignSha,
                amendmentPath, amendmentSha, stagePath, candidatePath, reviewPath, runtimePath,
                now.AddTicks(2), now.AddMinutes(5));
            string runtimeSha = M1Slice6SuccessorAuthorityLoader.HashFile(runtimePath);
            M1Slice6SuccessorRuntimeAuthority runtime =
                M1Slice6SuccessorAuthorityLoader.RuntimeForRecovery(
                    runtimePath, runtimeSha, amendment, now.AddMinutes(6));
            (M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt) =
                M1Slice6SuccessorAuthorityLoader.Stage(stagePath, stageSha, campaign, amendment, runtime);
            string credentialPath = Path.Combine(slice, "wp9-production-profile-authorization.v4.json");
            string credentialSha = M1Slice6SuccessorAuthorityLoader.HashFile(credentialPath);
            using (M1Slice6CampaignSqliteProviderAccounting accounting = new(
                product, credentialPath, credentialSha, now))
            {
                M1Slice6CampaignIdentity identity = new(campaign.CampaignId, campaign.ManifestSha256,
                    campaign.ManifestSha256, new string('0', 40), campaign.CredentialManifestId,
                    campaign.CredentialManifestSha256, campaign.CredentialProfileId,
                    campaign.CredentialGenerationId, campaign.CredentialTargetFingerprintSha256);
                M1Slice6CampaignAccountingAdmission admission = accounting.PrepareSuccessorV6(
                    authority, identity, attempt, now.AddTicks(3));
                ledger.ReserveAttempt(attempt, admission.ReservedNanoUsd, now.AddTicks(4));
                ledger.LatchPossibleStart(attempt, now.AddTicks(5));
                accounting.RecordPossibleStart(admission, now.AddTicks(6));
            }
            string evidencePath = Path.Combine(attemptDirectory, "attempt-evidence.v3.json");
            string stem = Path.GetFileNameWithoutExtension(evidencePath);
            File.WriteAllBytes(Path.Combine(attemptDirectory, stem + ".canonical-request.json"),
                authority.CanonicalRequest);
            File.WriteAllBytes(Path.Combine(attemptDirectory, stem + ".response-headers.json"),
                AmbiguousResponseHeaders("HttpRequestError.ConnectionError.SocketError.ConnectionRefused"));
            File.WriteAllBytes(Path.Combine(attemptDirectory, stem + ".native-trace.json"),
                SyntheticTrace(campaign.CredentialTargetFingerprintSha256));
            File.WriteAllBytes(Path.Combine(attemptDirectory, stem + ".canaries.json"),
                SyntheticCanaries());
            File.WriteAllBytes(Path.Combine(attemptDirectory, stem + ".preflight.json"),
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schema = "infinium.m1-s6.successor-evidence-preflight/v1",
                    attempt_id = attempt.AttemptId,
                    stage_manifest_sha256 = authority.ManifestSha256,
                    runtime_authority_sha256 = runtime.ManifestSha256,
                    disposition = "fresh-paths-created-before-possible-start",
                }));

            string copiedLedger = Path.Combine(outputRoot, "copied-ledger.v3.jsonl");
            File.Copy(ledgerPath, copiedLedger);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                    campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                    credentialPath, credentialSha, runtimePath, runtimeSha, copiedLedger,
                    evidencePath, now.AddMinutes(6)));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                    campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                    credentialPath, new string('0', 64), runtimePath, runtimeSha, ledgerPath,
                    evidencePath, now.AddMinutes(6)));
            string wrongEvidence = Path.Combine(outputRoot, "wrong-evidence.json");
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                    campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                    credentialPath, credentialSha, runtimePath, runtimeSha, ledgerPath,
                    wrongEvidence, now.AddMinutes(6)));
            string rawPath = Path.Combine(attemptDirectory, stem + ".raw-response.bin");
            File.WriteAllText(rawPath, "must-not-exist");
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                    campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                    credentialPath, credentialSha, runtimePath, runtimeSha, ledgerPath,
                    evidencePath, now.AddMinutes(6)));
            File.Delete(rawPath);
            string headersPath = Path.Combine(attemptDirectory, stem + ".response-headers.json");
            byte[] exactHeaders = File.ReadAllBytes(headersPath);
            File.WriteAllText(headersPath, "{}");
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                    campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                    credentialPath, credentialSha, runtimePath, runtimeSha, ledgerPath,
                    evidencePath, now.AddMinutes(6)));
            File.WriteAllBytes(headersPath, exactHeaders);
            foreach (string invalidDiagnostic in InvalidTransportDiagnosticTypes)
            {
                File.WriteAllBytes(headersPath, AmbiguousResponseHeaders(invalidDiagnostic));
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                        campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                        credentialPath, credentialSha, runtimePath, runtimeSha, ledgerPath,
                        evidencePath, now.AddMinutes(6)));
            }
            File.WriteAllBytes(headersPath, exactHeaders);

            M1Slice6CampaignBoundaryFailureReceipt historicalReceipt =
                M1Slice6SuccessorCampaignRunner.RecoveredAmbiguousReceipt(
                    AmbiguousResponseHeaders(), SyntheticTrace(campaign.CredentialTargetFingerprintSha256),
                    SyntheticCanaries(), attempt, campaign.CredentialTargetFingerprintSha256);
            Assert.IsNull(historicalReceipt.ProviderErrorType);

            M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                credentialPath, credentialSha, runtimePath, runtimeSha, ledgerPath,
                evidencePath, now.AddMinutes(6));
            M1Slice6SuccessorCampaignRunner.RecoverStartedAmbiguousAttempt(
                campaignPath, campaignSha, amendmentPath, amendmentSha, stagePath, stageSha,
                credentialPath, credentialSha, runtimePath, runtimeSha, ledgerPath,
                evidencePath, now.AddMinutes(7));
            M1Slice6SuccessorCampaignLedgerV3 recoveredLedger =
                M1Slice6SuccessorCampaignRunner.OpenHardBudgetLedger(
                    campaign, amendment, ledgerPath, now.AddMinutes(8), requireExisting: true);
            Assert.AreEqual(M1Slice6SuccessorCampaignV3State.AttemptEvidenceHandoff,
                recoveredLedger.Current.State);
            Assert.AreEqual("transport-ambiguous", recoveredLedger.Current.FailureDisposition);
            Assert.AreEqual(0, recoveredLedger.Current.SuccessorOutstandingReservedNanoUsd);
            Assert.AreEqual(authority.Limits.MaximumNanoUsd,
                recoveredLedger.Current.SuccessorUnresolvedNanoUsd
                    - predecessor.Current.SuccessorUnresolvedNanoUsd);
            string operationId = "m1s6-successor-v6-" + attempt.AttemptId + "-transport-operation";
            using (AuthoritativeStore store = new(new StoragePaths(product)))
            {
                ProviderOperationReadModel operation = store.ReadProviderOperation(operationId);
                Assert.AreEqual(ProviderOperationState.UnresolvedHold, operation.State);
                Assert.IsNull(operation.RawResponseBytes);
            }
            using (JsonDocument evidence = JsonDocument.Parse(File.ReadAllBytes(evidencePath)))
            {
                JsonElement root = evidence.RootElement;
                Assert.IsFalse(root.GetProperty("retry_permitted").GetBoolean());
                Assert.AreEqual(1, root.GetProperty("provider_send_count").GetInt32());
                Assert.AreEqual("HttpRequestError.ConnectionError.SocketError.ConnectionRefused",
                    root.GetProperty("provider_error_type").GetString());
                JsonElement observation = root.GetProperty("helper_boundary_observation");
                Assert.IsFalse(observation.GetProperty("receipt_available").GetBoolean());
                Assert.AreEqual(JsonValueKind.Null,
                    observation.GetProperty("containment_probe_executed").ValueKind);
                Assert.AreEqual(JsonValueKind.Null,
                    observation.GetProperty("process_tree_terminated").ValueKind);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
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
                M1Slice6SuccessorAuthorityLoader.HistoricalCampaign(campaignPath, campaignSha);
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
                        source_acquisition_id = "",
                        source_admission_id = "",
                        admitted_artifact_id = "",
                        source_application_link_id = "",
                        evidence_application_link_id = "",
                        candidate_id = "",
                        hypothesis_id = "",
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
                    required = false,
                    defect_id = (string?)null,
                    diagnosis_disposition = (string?)null,
                    failure_evidence_id = (string?)null,
                    failure_evidence_sha256 = (string?)null,
                    candidate_commit = (string?)null,
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
                "m1-slice6-successor-credential-access.v2.json")))!.AsObject();
            access["status"] = "reviewed-and-admitted";
            string accessPath = Path.Combine(directory, "access.json");
            File.WriteAllText(accessPath, access.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string accessSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(accessPath)));

            JsonObject campaign = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-campaign-authorization.v6.json")))!.AsObject();
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
                "m1-slice6-successor-credential-access.v2.json")))!.AsObject();
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
                "m1-slice6-successor-campaign-authorization.v6.json")))!.AsObject();
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

    private static byte[] AmbiguousResponseHeaders(string? providerErrorType = null)
    {
        object unavailable = new { Availability = 2, Value = (long?)null };
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "infinium.openai.response-headers/v2",
            state = (int)ProviderResponseState.Unknown,
            failure_stage = "provider-transport",
            transport_disposition = "may-have-started-no-response",
            http_status = (int?)null,
            response_bytes_existed = false,
            response_bytes_observed_lower_bound = 0,
            retained_response_bytes = 0,
            provider_response_id = (string?)null,
            provider_request_id = (string?)null,
            returned_model = (string?)null,
            returned_service_tier = (string?)null,
            refusal_code = (string?)null,
            incomplete_reason = (string?)null,
            provider_error_type = providerErrorType,
            provider_error_code = (string?)null,
            local_failure_code = "transport_ambiguous",
            requested_output_schema = (string?)null,
            usage = new
            {
                Availability = 2,
                DispatchCount = unavailable,
                InputTokens = unavailable,
                OutputTokens = unavailable,
                TotalTokens = unavailable,
                ReasoningTokens = unavailable,
                CacheReadTokens = unavailable,
                CacheWriteTokens = unavailable,
                PricedToolCalls = unavailable,
                CalculatedNanoUsd = unavailable,
                BillingAvailability = 2,
                RateAvailability = 2,
                CreditAvailability = 2,
                ReceiptState = 5,
            },
            dns_resolution_count = 1,
            network_used = true,
            send_count = 1,
            headers = Array.Empty<object>(),
        });
    }

    private static byte[] SyntheticTrace(string targetFingerprint) =>
        JsonSerializer.SerializeToUtf8Bytes(new object[]
        {
            new
            {
                Sequence = 1,
                Operation = "CredReadW",
                TargetFingerprintSha256 = targetFingerprint,
                Scenario = "m1-s6-campaign-provider-dispatch",
                Result = "success",
                AllocationId = (long?)1,
                PairedAllocationId = (long?)null,
            },
            new
            {
                Sequence = 2,
                Operation = "CredFree",
                TargetFingerprintSha256 = targetFingerprint,
                Scenario = "m1-s6-campaign-provider-dispatch",
                Result = "released",
                AllocationId = (long?)null,
                PairedAllocationId = (long?)1,
            },
        });

    private static byte[] SyntheticCanaries() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        SecretMatches = 0,
        RawTargetMatches = 0,
        RawTargetEncodings,
        ScannedSurfaces = new[]
        {
            new { Name = "private protocol request", Kind = "private-pipe-bytes", ByteCount = 10,
                SecretMatches = 0, RawTargetMatches = 0 },
            new { Name = "private protocol response", Kind = "private-pipe-bytes", ByteCount = 10,
                SecretMatches = 0, RawTargetMatches = 0 },
            new { Name = "native call trace", Kind = "canonical-trace-bytes", ByteCount = 10,
                SecretMatches = 0, RawTargetMatches = 0 },
            new { Name = "process command line", Kind = "captured-text", ByteCount = 10,
                SecretMatches = 0, RawTargetMatches = 0 },
            new { Name = "process environment names", Kind = "captured-text", ByteCount = 10,
                SecretMatches = 0, RawTargetMatches = 0 },
        },
    });

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
