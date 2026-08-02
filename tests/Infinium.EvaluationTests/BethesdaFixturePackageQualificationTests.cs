using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaFixturePackageQualificationTests
{
    private static readonly (string FixtureId, FixturePartition Partition)[] Fixtures =
    [
        ("BETH-NPC-DEV", FixturePartition.Development),
        ("BETH-REFR-DEV", FixturePartition.Development),
        ("BETH-LIGHT-VAL", FixturePartition.Development),
        ("BETH-MALFORMED-VAL", FixturePartition.Development),
        ("BETH-UNSUPPORTED-VAL", FixturePartition.Development),
    ];
    private static readonly (string FixtureId, string PredecessorId)[]
        EvaluatorPrivateReplacements =
        [
            ("BETH-LIGHT-VAL-002", "BETH-LIGHT-VAL"),
            ("BETH-MALFORMED-VAL-002", "BETH-MALFORMED-VAL"),
            ("BETH-UNSUPPORTED-VAL-002", "BETH-UNSUPPORTED-VAL"),
        ];
    private static readonly string[] HeldOutEvaluationIds = ["EVAL-0052"];
    private static readonly string[] HeldOutRegistryProperties =
    [
        "schema_id",
        "schema_version",
        "supersessions",
        "fixtures",
    ];
    private static readonly string[] EvaluatorPrivateRegistryProperties =
    [
        "schema_id",
        "schema_version",
        "fixtures",
    ];
    private static readonly string[] HeldOutIdentityProperties = ["byte_length", "sha256"];
    private static readonly string[] HeldOutFixtureProperties =
    [
        "fixture_id",
        "fixture_version",
        "partition",
        "evaluation_ids",
        "purpose",
        "classification",
        "redistribution_class",
        "owner",
        "review_state",
        "reviewed_at",
        "complete_package_retention",
        "private_store",
        "disclosure_class",
        "contamination_state",
        "governance_id",
        "declared_manifest_input_package_fingerprint",
        "execution_input_document_sha256",
        "retained_input_set_fingerprint",
        "oracle_fingerprint",
        "supplemental_oracle_fingerprint",
        "package_fingerprint",
        "package_fingerprint_scope",
        "documents",
        "independence_evidence",
        "supersession_authority_id",
        "custodian_attestation",
    ];
    private static readonly string[] HeldOutSupersessionProperties =
    [
        "fixture_id",
        "fixture_version",
        "invalidated_state",
        "reason",
        "successor_fixture_id",
        "successor_fixture_version",
        "authority_id",
        "public_v1_registry",
        "predecessor_answers_inspected",
        "production_material_inspected",
        "contamination_state",
    ];
    private static readonly string[] EvaluatorPrivateStoreProperties =
    [
        "store_id",
        "relationship",
        "revision",
        "governance_version",
    ];
    private static readonly string[] HeldOutAttestationProperties =
    [
        "construction_runs_byte_identical",
        "manual_and_independent_reader_agreed",
        "seven_documents_schema_valid",
        "supplemental_oracle_schema_valid",
        "cross_document_fingerprints_valid",
        "answer_bearing_execution_mutations_rejected",
        "deterministic_reconstruction_verified",
        "evidence_replay_verified",
        "manifest_fingerprints_match_disclosed_documents",
        "predecessor_package_accessed",
        "production_parser_or_output_accessed",
        "third_party_payload_consumed",
        "protected_root_accessed",
        "network_or_billable_provider_used",
        "evaluation_case_pass_claimed",
    ];
    private static readonly string[] EvaluatorPrivateFixtureProperties =
    [
        "fixture_id",
        "fixture_version",
        "partition",
        "evaluation_ids",
        "purpose",
        "classification",
        "owner",
        "review_state",
        "reviewed_at",
        "complete_package_retention",
        "private_store",
        "disclosure_class",
        "contamination_state",
        "governance_id",
        "declared_manifest_input_package_fingerprint",
        "execution_input_document_sha256",
        "retained_input_set_fingerprint",
        "oracle_fingerprint",
        "supplemental_oracle_fingerprint",
        "package_fingerprint",
        "package_fingerprint_scope",
        "documents",
        "independence_evidence",
        "corrective_authority_id",
        "custodian_attestation",
    ];
    private static readonly string[] EvaluatorPrivateDocumentProperties =
    [
        "public_manifest",
        "execution_input",
        "expected_oracle",
        "provenance",
        "replay_dependencies",
        "redistribution",
        "partition_history",
    ];
    private static readonly string[] EvaluatorPrivateEvidenceProperties =
    [
        "artifact_id",
        "artifact_version",
        "fingerprint",
        "availability",
    ];
    private static readonly string[] EvaluatorPrivateAttestationProperties =
    [
        "two_independent_raw_byte_methods_agreed",
        "two_clean_constructions_byte_identical",
        "seven_root_documents_schema_valid",
        "supplemental_oracle_schema_valid",
        "cross_document_fingerprints_valid",
        "execution_input_isolation_passed",
        "deterministic_reconstruction_verified",
        "evidence_replay_verified",
        "predecessor_package_accessed",
        "prior_reviewer_output_accessed",
        "production_parser_or_output_accessed",
        "mutagen_or_xedit_used",
        "third_party_payload_consumed",
        "protected_root_accessed",
        "network_or_billable_provider_used",
        "evaluation_case_pass_claimed",
    ];

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void TrackedBethesdaPackagesAreCompleteBoundAndAccepted()
    {
        foreach ((string fixtureId, FixturePartition partition) in Fixtures)
        {
            string directory = TestRepository.PathFromRoot(
                "test-data",
                "evaluation",
                "m1-semantic",
                fixtureId);

            EvaluationHarnessFixturePackage package =
                FixturePackageReader.ReadForEvaluationHarness(directory);

            Assert.AreEqual(fixtureId, package.FixtureId.Value);
            Assert.AreEqual("1.2.0", package.FixtureVersion.ToString());
            Assert.AreEqual(partition, package.Partition);
            Assert.AreEqual(
                "accepted",
                package.PublicManifest.GetProperty("review_state").GetString());
            Assert.IsTrue(
                package.PublicManifest
                    .GetProperty("evaluation_ids")
                    .EnumerateArray()
                    .Any(value => value.GetString() == "EVAL-0052"));
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void HeldOutRegistryRetainsOnlySealedPublicMetadata()
    {
        string semanticRoot = TestRepository.PathFromRoot(
            "test-data",
            "evaluation",
            "m1-semantic");
        string registryPath = Path.Combine(semanticRoot, "held-out-registry.json");
        using BoundedJsonDocumentSnapshot snapshot =
            BoundedJsonDocumentReader.Read(registryPath, 1024 * 1024, maximumDepth: 16);
        JsonElement root = snapshot.Document.RootElement;
        CollectionAssert.AreEquivalent(
            HeldOutRegistryProperties,
            root.EnumerateObject().Select(property => property.Name).ToArray());

        Assert.AreEqual(
            "infinium.evaluation.held-out-fixture-registry/v2",
            root.GetProperty("schema_id").GetString());
        Assert.AreEqual("2", root.GetProperty("schema_version").GetString());

        JsonElement[] supersessions = root.GetProperty("supersessions")
            .EnumerateArray()
            .ToArray();
        Assert.AreEqual(2, supersessions.Length);
        JsonElement supersession = supersessions.Single(item =>
            item.GetProperty("fixture_id").GetString() == "BETH-HO-001");
        CollectionAssert.AreEquivalent(
            HeldOutSupersessionProperties,
            supersession.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("BETH-HO-001", supersession.GetProperty("fixture_id").GetString());
        Assert.AreEqual("1.0.0", supersession.GetProperty("fixture_version").GetString());
        Assert.AreEqual(
            "invalidated-unavailable",
            supersession.GetProperty("invalidated_state").GetString());
        Assert.AreEqual(
            "BETH-HO-002",
            supersession.GetProperty("successor_fixture_id").GetString());
        Assert.IsFalse(supersession.GetProperty("predecessor_answers_inspected").GetBoolean());
        Assert.IsFalse(supersession.GetProperty("production_material_inspected").GetBoolean());
        Assert.AreEqual("clean", supersession.GetProperty("contamination_state").GetString());

        JsonElement structureCorrection = supersessions.Single(item =>
            item.GetProperty("fixture_id").GetString() == "BETH-HO-002");
        CollectionAssert.AreEquivalent(
            HeldOutSupersessionProperties,
            structureCorrection.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("1.0.0", structureCorrection.GetProperty("fixture_version").GetString());
        Assert.AreEqual(
            "sealed-structurally-invalid",
            structureCorrection.GetProperty("invalidated_state").GetString());
        Assert.AreEqual(
            "BETH-HO-002",
            structureCorrection.GetProperty("successor_fixture_id").GetString());
        Assert.AreEqual(
            "1.1.0",
            structureCorrection.GetProperty("successor_fixture_version").GetString());
        Assert.IsTrue(
            structureCorrection.GetProperty("predecessor_answers_inspected").GetBoolean());
        Assert.IsFalse(
            structureCorrection.GetProperty("production_material_inspected").GetBoolean());
        Assert.AreEqual(
            "clean",
            structureCorrection.GetProperty("contamination_state").GetString());

        string v1RegistryPath = Path.Combine(semanticRoot, "held-out-registry-v1.json");
        byte[] v1RegistryBytes = File.ReadAllBytes(v1RegistryPath);
        using JsonDocument v1Registry = JsonDocument.Parse(v1RegistryBytes);
        JsonElement v1Fixture = v1Registry.RootElement
            .GetProperty("fixtures")
            .EnumerateArray()
            .Single();
        Assert.AreEqual(
            v1Fixture.GetProperty("fixture_id").GetString(),
            supersession.GetProperty("fixture_id").GetString());
        Assert.AreEqual(
            v1Fixture.GetProperty("fixture_version").GetString(),
            supersession.GetProperty("fixture_version").GetString());
        JsonElement v1Identity = supersession.GetProperty("public_v1_registry");
        Assert.AreEqual(v1Identity.GetProperty("byte_length").GetInt64(), v1RegistryBytes.LongLength);
        Assert.AreEqual(
            v1Identity.GetProperty("sha256").GetString(),
            Convert.ToHexString(SHA256.HashData(v1RegistryBytes)).ToLowerInvariant());

        JsonElement fixture = root.GetProperty("fixtures").EnumerateArray().Single();
        CollectionAssert.AreEquivalent(
            HeldOutFixtureProperties,
            fixture.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("BETH-HO-002", fixture.GetProperty("fixture_id").GetString());
        Assert.AreEqual("1.1.0", fixture.GetProperty("fixture_version").GetString());
        Assert.AreEqual("held-out", fixture.GetProperty("partition").GetString());
        Assert.AreEqual("sealed", fixture.GetProperty("review_state").GetString());
        Assert.AreEqual(
            "separate-private-git",
            fixture.GetProperty("complete_package_retention").GetString());
        Assert.AreEqual("sanitized-result", fixture.GetProperty("disclosure_class").GetString());
        Assert.AreEqual("clean", fixture.GetProperty("contamination_state").GetString());
        CollectionAssert.AreEqual(
            HeldOutEvaluationIds,
            fixture.GetProperty("evaluation_ids")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());

        JsonElement privateStore = fixture.GetProperty("private_store");
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateStoreProperties,
            privateStore.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("infinium-evaluator-fixtures", privateStore.GetProperty("store_id").GetString());
        Assert.AreEqual("separate-git-history", privateStore.GetProperty("relationship").GetString());
        StringAssert.Matches(
            privateStore.GetProperty("revision").GetString()!,
            new System.Text.RegularExpressions.Regex("^[0-9a-f]{40}$"));

        JsonElement documents = fixture.GetProperty("documents");
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateDocumentProperties,
            documents.EnumerateObject().Select(property => property.Name).ToArray());
        foreach (JsonProperty document in documents.EnumerateObject())
        {
            JsonElement identity = document.Value;
            CollectionAssert.AreEquivalent(
                HeldOutIdentityProperties,
                identity.EnumerateObject().Select(property => property.Name).ToArray(),
                document.Name);
            Assert.IsTrue(identity.GetProperty("byte_length").GetInt64() > 0, document.Name);
            string fingerprintText = identity.GetProperty("sha256").GetString()!;
            Sha256Fingerprint fingerprint = new(fingerprintText);
            Assert.AreEqual(fingerprint.Value, fingerprintText, document.Name);
        }
        Assert.AreEqual(
            documents.GetProperty("execution_input").GetProperty("sha256").GetString(),
            fixture.GetProperty("execution_input_document_sha256").GetString());
        Assert.AreEqual(
            documents.GetProperty("expected_oracle").GetProperty("sha256").GetString(),
            fixture.GetProperty("oracle_fingerprint").GetString());

        JsonElement evidence = fixture.GetProperty("independence_evidence");
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateEvidenceProperties,
            evidence.EnumerateObject().Select(property => property.Name).ToArray());

        JsonElement attestation = fixture.GetProperty("custodian_attestation");
        CollectionAssert.AreEquivalent(
            HeldOutAttestationProperties,
            attestation.EnumerateObject().Select(property => property.Name).ToArray());
        foreach (JsonProperty property in attestation.EnumerateObject())
        {
            bool expected = property.Name is
                "construction_runs_byte_identical"
                or "manual_and_independent_reader_agreed"
                or "seven_documents_schema_valid"
                or "supplemental_oracle_schema_valid"
                or "cross_document_fingerprints_valid"
                or "answer_bearing_execution_mutations_rejected"
                or "deterministic_reconstruction_verified"
                or "evidence_replay_verified"
                or "manifest_fingerprints_match_disclosed_documents"
                or "predecessor_package_accessed";
            Assert.AreEqual(expected, property.Value.GetBoolean(), property.Name);
        }

        string rawText = File.ReadAllText(registryPath);
        foreach (string forbidden in new[]
        {
            "\"path\"",
            "\"locator\"",
            "\"payload\"",
            "\"answers\"",
            "C:\\\\",
            "Z:\\\\",
            "../",
        })
        {
            Assert.IsFalse(
                rawText.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                forbidden);
        }

        Assert.IsFalse(
            Directory.Exists(Path.Combine(semanticRoot, "BETH-HO-001")),
            "A public partial held-out package must not be presented as executable.");
        Assert.IsFalse(
            Directory.Exists(Path.Combine(semanticRoot, "BETH-HO-002")),
            "A public partial held-out package must not be presented as executable.");
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void EvaluatorPrivateReplacementRegistryRetainsOnlyBoundPublicMetadata()
    {
        string semanticRoot = TestRepository.PathFromRoot(
            "test-data",
            "evaluation",
            "m1-semantic");
        string registryPath = Path.Combine(
            semanticRoot,
            "evaluator-private-registry.json");
        using BoundedJsonDocumentSnapshot snapshot =
            BoundedJsonDocumentReader.Read(registryPath, 1024 * 1024, maximumDepth: 16);
        JsonElement root = snapshot.Document.RootElement;
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateRegistryProperties,
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(
            "infinium.evaluation.evaluator-private-fixture-registry/v2",
            root.GetProperty("schema_id").GetString());
        Assert.AreEqual("2", root.GetProperty("schema_version").GetString());

        Dictionary<string, JsonElement> fixtures = root.GetProperty("fixtures")
            .EnumerateArray()
            .ToDictionary(
                fixture => fixture.GetProperty("fixture_id").GetString()!,
                StringComparer.Ordinal);
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateReplacements.Select(item => item.FixtureId).ToArray(),
            fixtures.Keys.ToArray());

        foreach ((string fixtureId, string predecessorId) in EvaluatorPrivateReplacements)
        {
            JsonElement fixture = fixtures[fixtureId];
            CollectionAssert.AreEquivalent(
                EvaluatorPrivateFixtureProperties,
                fixture.EnumerateObject().Select(property => property.Name).ToArray(),
                fixtureId);
            Assert.AreEqual("validation", fixture.GetProperty("partition").GetString());
            Assert.AreEqual("sealed", fixture.GetProperty("review_state").GetString());
            Assert.AreEqual(
                "separate-private-git",
                fixture.GetProperty("complete_package_retention").GetString());
            Assert.AreEqual(
                "sanitized-result",
                fixture.GetProperty("disclosure_class").GetString());
            Assert.AreEqual(
                "clean",
                fixture.GetProperty("contamination_state").GetString());
            Assert.AreEqual(
                "adr-0026/evaluator-private-fixture-governance/2026-08-01",
                fixture.GetProperty("governance_id").GetString());
            Assert.IsTrue(
                fixture.GetProperty("evaluation_ids")
                    .EnumerateArray()
                    .Any(value => value.GetString() == "EVAL-0052"),
                fixtureId);

            JsonElement privateStore = fixture.GetProperty("private_store");
            CollectionAssert.AreEquivalent(
                EvaluatorPrivateStoreProperties,
                privateStore.EnumerateObject().Select(property => property.Name).ToArray(),
                fixtureId);
            Assert.AreEqual(
                "infinium-evaluator-fixtures",
                privateStore.GetProperty("store_id").GetString());
            Assert.AreEqual(
                "separate-git-history",
                privateStore.GetProperty("relationship").GetString());
            Assert.AreEqual(
                "2026-08-01",
                privateStore.GetProperty("governance_version").GetString());
            StringAssert.Matches(
                privateStore.GetProperty("revision").GetString()!,
                new System.Text.RegularExpressions.Regex("^[0-9a-f]{40}$"));

            JsonElement documents = fixture.GetProperty("documents");
            CollectionAssert.AreEquivalent(
                EvaluatorPrivateDocumentProperties,
                documents.EnumerateObject().Select(property => property.Name).ToArray());
            foreach (JsonProperty document in documents.EnumerateObject())
            {
                CollectionAssert.AreEquivalent(
                    HeldOutIdentityProperties,
                    document.Value.EnumerateObject().Select(property => property.Name).ToArray(),
                    document.Name);
                Assert.IsTrue(
                    document.Value.GetProperty("byte_length").GetInt64() > 0,
                    document.Name);
                string fingerprintText = document.Value.GetProperty("sha256").GetString()!;
                Sha256Fingerprint fingerprint = new(fingerprintText);
                Assert.AreEqual(fingerprint.Value, fingerprintText, document.Name);
            }

            Assert.AreEqual(
                documents.GetProperty("execution_input").GetProperty("sha256").GetString(),
                fixture.GetProperty("execution_input_document_sha256").GetString());
            Assert.AreEqual(
                documents.GetProperty("expected_oracle").GetProperty("sha256").GetString(),
                fixture.GetProperty("oracle_fingerprint").GetString());
            _ = new Sha256Fingerprint(
                fixture.GetProperty("declared_manifest_input_package_fingerprint").GetString()!);
            _ = new Sha256Fingerprint(
                fixture.GetProperty("retained_input_set_fingerprint").GetString()!);
            _ = new Sha256Fingerprint(
                fixture.GetProperty("supplemental_oracle_fingerprint").GetString()!);
            _ = new Sha256Fingerprint(
                fixture.GetProperty("package_fingerprint").GetString()!);

            JsonElement evidence = fixture.GetProperty("independence_evidence");
            CollectionAssert.AreEquivalent(
                EvaluatorPrivateEvidenceProperties,
                evidence.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.AreEqual(
                "evaluator-private",
                evidence.GetProperty("availability").GetString());
            _ = new Sha256Fingerprint(evidence.GetProperty("fingerprint").GetString()!);

            using BoundedJsonDocumentSnapshot predecessorHistorySnapshot =
                BoundedJsonDocumentReader.Read(
                    Path.Combine(
                        semanticRoot,
                        predecessorId,
                        FixturePackageReader.PartitionHistoryFileName),
                    1024 * 1024,
                    maximumDepth: 16);
            JsonElement transition = predecessorHistorySnapshot.Document.RootElement
                .GetProperty("partition_history")
                .EnumerateArray()
                .Last();
            Assert.AreEqual("validation", transition.GetProperty("from").GetString());
            Assert.AreEqual("development", transition.GetProperty("to").GetString());
            Assert.IsTrue(
                transition.GetProperty("change_influenced_implementation").GetBoolean());
            Assert.AreEqual(
                fixture.GetProperty("fixture_id").GetString(),
                transition.GetProperty("replacement_fixture_id").GetString());
            Assert.AreEqual(
                fixture.GetProperty("partition").GetString(),
                transition.GetProperty("replacement_partition").GetString());
            Assert.AreEqual(
                fixture.GetProperty("declared_manifest_input_package_fingerprint").GetString(),
                transition.GetProperty("replacement_input_package_fingerprint").GetString());
            Assert.AreEqual(
                fixture.GetProperty("oracle_fingerprint").GetString(),
                transition.GetProperty("replacement_oracle_fingerprint").GetString());
            Assert.AreEqual(
                fixture.GetProperty("corrective_authority_id").GetString(),
                transition.GetProperty("authorized_by").GetString());
            JsonElement transitionEvidence =
                transition.GetProperty("independence_evidence_reference");
            foreach (string propertyName in EvaluatorPrivateEvidenceProperties)
            {
                Assert.AreEqual(
                    evidence.GetProperty(propertyName).GetRawText(),
                    transitionEvidence.GetProperty(propertyName).GetRawText(),
                    propertyName);
            }

            JsonElement attestation = fixture.GetProperty("custodian_attestation");
            CollectionAssert.AreEquivalent(
                EvaluatorPrivateAttestationProperties,
                attestation.EnumerateObject().Select(property => property.Name).ToArray());
            foreach (JsonProperty property in attestation.EnumerateObject())
            {
                bool correctedFixture = fixtureId is
                    "BETH-MALFORMED-VAL-002" or "BETH-UNSUPPORTED-VAL-002";
                bool expected = property.Name is
                    "two_independent_raw_byte_methods_agreed"
                    or "two_clean_constructions_byte_identical"
                    or "seven_root_documents_schema_valid"
                    or "supplemental_oracle_schema_valid"
                    or "cross_document_fingerprints_valid"
                    or "execution_input_isolation_passed"
                    or "deterministic_reconstruction_verified"
                    or "evidence_replay_verified"
                    || correctedFixture && property.Name is
                        "predecessor_package_accessed"
                        or "prior_reviewer_output_accessed"
                        or "mutagen_or_xedit_used";
                Assert.AreEqual(expected, property.Value.GetBoolean(), property.Name);
            }

            Assert.IsFalse(
                Directory.Exists(Path.Combine(semanticRoot, fixtureId)),
                $"A public partial evaluator-private package must not exist: {fixtureId}");
        }

        string rawText = File.ReadAllText(registryPath);
        foreach (string forbidden in new[]
        {
            "\"path\"",
            "\"locator\"",
            "\"payload\"",
            "\"answers\"",
            "C:\\\\",
            "Z:\\\\",
            "../",
        })
        {
            Assert.IsFalse(
                rawText.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                forbidden);
        }

    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void PrivateStoreDescriptorIsWindowsPowerShellCompatibleAndMetadataOnlyByDefault()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows PowerShell compatibility is Windows-specific.");
        }

        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"infinium-private-store-descriptor-{Guid.NewGuid():N}");
        string productRoot = Path.Combine(temporaryRoot, "product");
        string privateStoreRoot = Path.Combine(temporaryRoot, "evaluator-private");
        Directory.CreateDirectory(productRoot);
        Directory.CreateDirectory(privateStoreRoot);

        try
        {
            AssertProcessSucceeded(RunProcess("git", productRoot, "init", "--quiet"));
            AssertProcessSucceeded(RunProcess("git", privateStoreRoot, "init", "--quiet"));
            AssertProcessSucceeded(
                RunProcess("git", privateStoreRoot, "config", "user.name", "Infinium Test"));
            AssertProcessSucceeded(
                RunProcess(
                    "git",
                    privateStoreRoot,
                    "config",
                    "user.email",
                    "infinium-test@example.invalid"));

            File.WriteAllText(
                Path.Combine(privateStoreRoot, "STORE.json"),
                """
                {
                  "schema_id": "infinium.evaluation.private-store/v1",
                  "store_id": "infinium-evaluator-fixtures",
                  "relationship": "separate-git-history",
                  "governance_version": "2026-08-01"
                }
                """);
            AssertProcessSucceeded(RunProcess("git", privateStoreRoot, "add", "STORE.json"));
            AssertProcessSucceeded(
                RunProcess(
                    "git",
                    privateStoreRoot,
                    "-c",
                    "commit.gpgsign=false",
                    "commit",
                    "--quiet",
                    "-m",
                    "test fixture store"));
            AssertProcessSucceeded(
                RunProcess(
                    "git",
                    productRoot,
                    "config",
                    "infinium.evaluatorPrivateStorePath",
                    privateStoreRoot));

            string windowsPowerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            Assert.IsTrue(File.Exists(windowsPowerShell), windowsPowerShell);
            string scriptPath = TestRepository.PathFromRoot(
                "tools",
                "evaluation",
                "private-fixtures",
                "Get-PrivateStoreDescriptor.ps1");

            ProcessResult defaultResult = RunProcess(
                windowsPowerShell,
                productRoot,
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                scriptPath);
            AssertProcessSucceeded(defaultResult);
            using JsonDocument defaultDescriptor = JsonDocument.Parse(defaultResult.StandardOutput);
            JsonElement defaultRoot = defaultDescriptor.RootElement;
            Assert.AreEqual(
                "infinium-evaluator-fixtures",
                defaultRoot.GetProperty("store_id").GetString());
            Assert.IsFalse(defaultRoot.TryGetProperty("delegation_store_root", out _));
            StringAssert.Matches(
                defaultRoot.GetProperty("revision").GetString()!,
                new System.Text.RegularExpressions.Regex("^[0-9a-f]{40}$"));

            ProcessResult delegatedResult = RunProcess(
                windowsPowerShell,
                productRoot,
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                scriptPath,
                "-IncludeLocatorForDelegation");
            AssertProcessSucceeded(delegatedResult);
            using JsonDocument delegatedDescriptor = JsonDocument.Parse(
                delegatedResult.StandardOutput);
            Assert.AreEqual(
                Path.GetFullPath(privateStoreRoot),
                delegatedDescriptor.RootElement
                    .GetProperty("delegation_store_root")
                    .GetString());
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                foreach (string file in Directory.EnumerateFiles(
                    temporaryRoot,
                    "*",
                    SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static ProcessResult RunProcess(
        string fileName,
        string workingDirectory,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        bool exited = process.WaitForExit(milliseconds: 30_000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        return new ProcessResult(exited, process.ExitCode, standardOutput, standardError);
    }

    private static void AssertProcessSucceeded(ProcessResult result)
    {
        Assert.IsTrue(result.Exited, "Process did not terminate within 30 seconds.");
        Assert.AreEqual(
            0,
            result.ExitCode,
            $"stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{result.StandardError}");
    }

    private sealed record ProcessResult(
        bool Exited,
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
