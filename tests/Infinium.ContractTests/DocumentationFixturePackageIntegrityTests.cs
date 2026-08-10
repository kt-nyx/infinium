using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DocumentationFixturePackageIntegrityTests
{
    private const string CoreFixture = "DOC-CLAIM-CORE-DEV";
    private const string AdversarialFixture = "DOC-CLAIM-ADVERSARIAL-VAL";

    [TestMethod]
    [DataRow(CoreFixture)]
    [DataRow(AdversarialFixture)]
    [TestCategory("Contract")]
    public void RegisteredDocumentationFixturePackageIsClosed(string fixtureId)
    {
        DocumentationFixturePackage package = DocumentationFixturePackageReader.Read(FixtureRoot(fixtureId));
        Assert.AreEqual(fixtureId, package.FixtureId.Value);
    }

    [TestMethod]
    [DataRow("provenance")]
    [DataRow("replay")]
    [DataRow("redistribution")]
    [DataRow("partition-history")]
    [DataRow("case-matrix")]
    [DataRow("oracle")]
    [TestCategory("Contract")]
    public void SchemaInvalidStructuredDocumentIsRejected(string document)
    {
        AssertMutationRejected(AdversarialFixture, root =>
        {
            switch (document)
            {
                case "provenance":
                    {
                        JsonObject value = ReadObject(root, "provenance.json");
                        value["unexpected"] = true;
                        WriteJson(root, "provenance.json", value);
                        ResealManifestDocument(root, "provenance_fingerprint", "provenance.json");
                        break;
                    }
                case "replay":
                    {
                        JsonObject value = ReadObject(root, "replay-dependencies.json");
                        value["unexpected"] = true;
                        WriteJson(root, "replay-dependencies.json", value);
                        ResealManifestDocument(root, "replay_dependency_fingerprint", "replay-dependencies.json");
                        break;
                    }
                case "redistribution":
                    {
                        JsonObject value = ReadObject(root, "redistribution.json");
                        value["unexpected"] = true;
                        WriteJson(root, "redistribution.json", value);
                        break;
                    }
                case "partition-history":
                    {
                        JsonObject value = ReadObject(root, "partition-history.json");
                        value["unexpected"] = true;
                        WriteJson(root, "partition-history.json", value);
                        break;
                    }
                case "case-matrix":
                    {
                        JsonObject value = ReadObject(root, "inputs/case-matrix.json");
                        value["unexpected"] = true;
                        WriteJson(root, "inputs/case-matrix.json", value);
                        ResealReplayInput(root, "inputs/case-matrix.json");
                        break;
                    }
                case "oracle":
                    {
                        JsonObject value = ReadObject(root, "expected-oracle.json");
                        value["unexpected"] = true;
                        WriteJson(root, "expected-oracle.json", value);
                        ResealExpectedOutput(root, "expected-oracle.json", "oracle_fingerprint");
                        break;
                    }
                default:
                    Assert.Fail($"Unknown mutation '{document}'.");
                    break;
            }
        });
    }

    [TestMethod]
    [DataRow("case-matrix")]
    [DataRow("oracle")]
    [TestCategory("Contract")]
    public void StructuredDocumentBranchShapeIsClosed(string document)
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            if (document == "case-matrix")
            {
                JsonObject matrix = ReadObject(root, "inputs/case-matrix.json");
                matrix["clean_execution_binding"] = matrix["execution_binding"]!.DeepClone();
                WriteJson(root, "inputs/case-matrix.json", matrix);
                ResealReplayInput(root, "inputs/case-matrix.json");
                return;
            }

            JsonObject oracle = ReadObject(root, "expected-oracle.json");
            oracle["accepted_boundaries"] = new JsonObject
            {
                ["fixture-missing-snapshot"] = "schema-valid adversarial-only branch value",
            };
            WriteJson(root, "expected-oracle.json", oracle);
            ResealExpectedOutput(root, "expected-oracle.json", "oracle_fingerprint");
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void PartitionHistoryMustExactlyMatchPublicManifest()
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject history = ReadObject(root, "partition-history.json");
            history["partition_history"]![0]!["reason"] = "Schema-valid but divergent history.";
            WriteJson(root, "partition-history.json", history);
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void AnswerIsolationAndRedistributionAgreementAreEnforced()
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject provenance = ReadObject(root, "provenance.json");
            provenance["answer_isolation"]!["product_output_used"] = true;
            WriteJson(root, "provenance.json", provenance);
            ResealManifestDocument(root, "provenance_fingerprint", "provenance.json");
        });

        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject redistribution = ReadObject(root, "redistribution.json");
            redistribution["redistribution_class"] = "manifest-only";
            WriteJson(root, "redistribution.json", redistribution);
        });

        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject provenance = ReadObject(root, "provenance.json");
            provenance["answer_isolation"]!["derivation_record"]!["artifact_id"] = "oracle/other.md";
            WriteJson(root, "provenance.json", provenance);
            ResealManifestDocument(root, "provenance_fingerprint", "provenance.json");
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CaseMatrixApplicationTargetsMustMatchClaimImport()
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject matrix = ReadObject(root, "inputs/case-matrix.json");
            matrix["application_targets"]![0]!["analysis_context_id"] = "different-analysis-context";
            WriteJson(root, "inputs/case-matrix.json", matrix);
            ResealReplayInput(root, "inputs/case-matrix.json");
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CaseMatrixApplicationTargetsMustMatchOracle()
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject matrix = ReadObject(root, "inputs/case-matrix.json");
            matrix["application_targets"]![0]!["installation_snapshot_id"] = "different-snapshot";
            WriteJson(root, "inputs/case-matrix.json", matrix);
            ResealReplayInput(root, "inputs/case-matrix.json");
        });
    }

    [TestMethod]
    [DataRow("retention-location")]
    [DataRow("availability")]
    [DataRow("permission")]
    [DataRow("expected-state")]
    [TestCategory("Contract")]
    public void ReplayGovernanceMustMatchPackageAndCleanOracle(string mutation)
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject replay = ReadObject(root, "replay-dependencies.json");
            JsonObject dependency = replay["dependencies"]![0]!.AsObject();
            switch (mutation)
            {
                case "retention-location":
                    dependency["retention_location_class"] = "external-authoritative-source";
                    break;
                case "availability":
                    dependency["availability"] = "externally-reacquirable";
                    break;
                case "permission":
                    dependency["permission_and_redistribution"] = "manifest-only";
                    break;
                case "expected-state":
                    replay["expected_replay_state"] = "audit-only";
                    break;
                default:
                    Assert.Fail($"Unknown mutation '{mutation}'.");
                    break;
            }

            WriteJson(root, "replay-dependencies.json", replay);
            ResealManifestDocument(root, "replay_dependency_fingerprint", "replay-dependencies.json");
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void InitialPartitionAssignmentCannotClaimImplementationInfluence()
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject manifest = ReadObject(root, "public-manifest.json");
            JsonObject history = ReadObject(root, "partition-history.json");
            history["partition_history"]![0]!["change_influenced_implementation"] = true;
            manifest["partition_history"] = history["partition_history"]!.DeepClone();
            WriteJson(root, "partition-history.json", history);
            WriteJson(root, "public-manifest.json", manifest);
        });
    }

    [TestMethod]
    [DataRow(CoreFixture, "execution-time")]
    [DataRow(CoreFixture, "passage-state")]
    [DataRow(AdversarialFixture, "execution-time")]
    [DataRow(AdversarialFixture, "reuse-retention")]
    [DataRow(AdversarialFixture, "reuse-time")]
    [DataRow(AdversarialFixture, "deletion-reason")]
    [TestCategory("Contract")]
    public void CaseMatrixExecutionAndAggregateBindingsMustMatchOracle(
        string fixtureId,
        string mutation)
    {
        AssertMutationRejected(fixtureId, root =>
        {
            JsonObject matrix = ReadObject(root, "inputs/case-matrix.json");
            switch (mutation)
            {
                case "execution-time":
                    string binding = fixtureId == CoreFixture
                        ? "execution_binding"
                        : "clean_execution_binding";
                    matrix[binding]!["imported_at"] = "2026-08-08T18:31:00.0000000+00:00";
                    break;
                case "passage-state":
                    matrix["aggregate_output_binding"]!["passage_state"] = "partial";
                    break;
                case "reuse-retention":
                    matrix["aggregate_output_binding"]!["retained_reuse_revision_retention_state"] = "partial";
                    break;
                case "reuse-time":
                    matrix["cases"]!.AsArray().Single(item =>
                        item!["case_id"]!.GetValue<string>() == "retained-reuse-unavailable")!["imported_at"] =
                        "2026-08-08T18:31:00.0000000+00:00";
                    break;
                case "deletion-reason":
                    matrix["cases"]!.AsArray().Single(item =>
                        item!["case_id"]!.GetValue<string>() == "retained-reuse-deleted")!["deletion_reason"] =
                        "Schema-valid but divergent deletion reason.";
                    break;
                default:
                    Assert.Fail($"Unknown mutation '{mutation}'.");
                    break;
            }

            WriteJson(root, "inputs/case-matrix.json", matrix);
            ResealReplayInput(root, "inputs/case-matrix.json");
        });
    }

    [TestMethod]
    [DataRow(CoreFixture, "kind")]
    [DataRow(AdversarialFixture, "applicability")]
    [TestCategory("Contract")]
    public void OracleClaimsRequireTheirExactBranchTypedFields(
        string fixtureId,
        string property)
    {
        AssertMutationRejected(fixtureId, root =>
        {
            JsonObject oracle = ReadObject(root, "expected-oracle.json");
            JsonArray claims = fixtureId == CoreFixture
                ? oracle["claims"]!.AsArray()
                : oracle["clean"]!["claims"]!.AsArray();
            claims[0]!.AsObject().Remove(property);
            WriteJson(root, "expected-oracle.json", oracle);
            ResealExpectedOutput(root, "expected-oracle.json", "oracle_fingerprint");
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void ExternalBoundaryCannotBecomeUsedEvenWhenCaseAndOracleAgree()
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject matrix = ReadObject(root, "inputs/case-matrix.json");
            matrix["aggregate_output_binding"]!["boundaries"]![0]!["state"] = "used";
            WriteJson(root, "inputs/case-matrix.json", matrix);
            ResealReplayInput(root, "inputs/case-matrix.json");

            JsonObject oracle = ReadObject(root, "expected-oracle.json");
            oracle["import"]!["boundaries"]![0]!["state"] = "used";
            WriteJson(root, "expected-oracle.json", oracle);
            ResealExpectedOutput(root, "expected-oracle.json", "oracle_fingerprint");
        });
    }

    [TestMethod]
    [DataRow("kind")]
    [DataRow("required-for")]
    [TestCategory("Contract")]
    public void ReplayDependenciesMustRemainCleanRecomputableTrackedInputs(string mutation)
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject replay = ReadObject(root, "replay-dependencies.json");
            JsonObject dependency = replay["dependencies"]![0]!.AsObject();
            if (mutation == "kind")
            {
                dependency["kind"] = "external-authoritative-input";
            }
            else
            {
                dependency["required_for"] = new JsonArray("audit");
            }
            WriteJson(root, "replay-dependencies.json", replay);
            ResealManifestDocument(root, "replay_dependency_fingerprint", "replay-dependencies.json");
        });
    }

    [TestMethod]
    [DataRow("count")]
    [DataRow("reuse-index")]
    [TestCategory("Contract")]
    public void OracleCountsAndReuseIndexesMustMatchTypedObjects(string mutation)
    {
        AssertMutationRejected(AdversarialFixture, root =>
        {
            JsonObject oracle = ReadObject(root, "expected-oracle.json");
            if (mutation == "count")
            {
                oracle["clean"]!["expected_counts"]!["passages"] = 5;
            }
            else
            {
                oracle["retained_reuse_deleted"]!["gap_ids"]!.AsArray().RemoveAt(0);
            }
            WriteJson(root, "expected-oracle.json", oracle);
            ResealExpectedOutput(root, "expected-oracle.json", "oracle_fingerprint");
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void EveryStructuredCompanionMustMatchFixtureIdentity()
    {
        string[] documents =
        [
            "expected-oracle.json",
            "partition-history.json",
            "provenance.json",
            "redistribution.json",
            "replay-dependencies.json",
            "inputs/case-matrix.json",
        ];
        foreach (string document in documents)
        {
            AssertMutationRejected(CoreFixture, root =>
            {
                JsonObject value = ReadObject(root, document);
                value["fixture_version"] = "9.9.9";
                WriteJson(root, document, value);
                switch (document)
                {
                    case "expected-oracle.json":
                        ResealExpectedOutput(root, document, "oracle_fingerprint");
                        break;
                    case "provenance.json":
                        ResealManifestDocument(root, "provenance_fingerprint", document);
                        break;
                    case "replay-dependencies.json":
                        ResealManifestDocument(root, "replay_dependency_fingerprint", document);
                        break;
                    case "inputs/case-matrix.json":
                        ResealReplayInput(root, document);
                        break;
                }
            });
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void ValidKnownAnswerTransitionToDevelopmentIsAccepted()
    {
        WithFixtureCopy(AdversarialFixture, root =>
        {
            JsonObject manifest = ReadObject(root, "public-manifest.json");
            JsonObject history = ReadObject(root, "partition-history.json");
            JsonArray transitions = history["partition_history"]!.AsArray();
            transitions.Add(new JsonObject
            {
                ["from"] = "validation",
                ["to"] = "development",
                ["at"] = "2026-08-08T18:06:00.0000000+00:00",
                ["reason"] = "Known answers influenced implementation; materially independent validation replacement registered.",
                ["change_influenced_implementation"] = true,
                ["replacement_fixture_id"] = "DOC-CLAIM-REPLACEMENT-VAL",
                ["replacement_partition"] = "validation",
                ["replacement_input_package_fingerprint"] = new string('1', 64),
                ["replacement_oracle_fingerprint"] = new string('2', 64),
                ["independence_evidence_reference"] = new JsonObject
                {
                    ["artifact_id"] = "oracle/independent-derivation.md",
                    ["artifact_version"] = "1.0.0",
                    ["fingerprint"] = "5cafb981d970a2e0895f808232d60d25818713e5b70ba23fef012b93d1e810af",
                    ["availability"] = "retained",
                    ["byte_length"] = 2932,
                },
                ["authorized_by"] = "infinium-evaluation-owner",
            });
            manifest["partition"] = "development";
            manifest["partition_history"] = transitions.DeepClone();
            WriteJson(root, "partition-history.json", history);
            WriteJson(root, "public-manifest.json", manifest);

            DocumentationFixturePackage package = DocumentationFixturePackageReader.Read(root);
            Assert.AreEqual("Development", package.Partition.ToString());
        });
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("extra")]
    [DataRow("duplicate")]
    [DataRow("path-escape")]
    [DataRow("unbounded")]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void ReplayDependencyClosureMutationIsRejected(string mutation)
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            JsonObject replay = ReadObject(root, "replay-dependencies.json");
            JsonArray dependencies = replay["dependencies"]!.AsArray();
            switch (mutation)
            {
                case "missing":
                    dependencies.RemoveAt(0);
                    break;
                case "extra":
                    {
                        JsonObject extra = dependencies[0]!.DeepClone().AsObject();
                        extra["dependency_id"] = "unregistered-extra-dependency";
                        extra["identity_or_version"] = "inputs/not-present.json";
                        dependencies.Add(extra);
                        break;
                    }
                case "duplicate":
                    {
                        JsonObject duplicate = dependencies[0]!.DeepClone().AsObject();
                        duplicate["dependency_id"] = "duplicate-path-dependency";
                        dependencies.Add(duplicate);
                        break;
                    }
                case "path-escape":
                    dependencies[0]!["identity_or_version"] = "../outside.json";
                    break;
                case "unbounded":
                    while (dependencies.Count <= 64)
                    {
                        JsonObject unbounded = dependencies[0]!.DeepClone().AsObject();
                        unbounded["dependency_id"] = $"unbounded-{dependencies.Count}";
                        unbounded["identity_or_version"] = $"inputs/unbounded-{dependencies.Count}.json";
                        dependencies.Add(unbounded);
                    }
                    break;
                default:
                    Assert.Fail($"Unknown mutation '{mutation}'.");
                    break;
            }

            WriteJson(root, "replay-dependencies.json", replay);
            ResealManifestDocument(root, "replay_dependency_fingerprint", "replay-dependencies.json");
        });
    }

    [TestMethod]
    [DataRow("hash")]
    [DataRow("length")]
    [DataRow("independent-derivation")]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void TamperEvidentBindingDriftIsRejected(string mutation)
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            if (mutation == "independent-derivation")
            {
                File.AppendAllText(Path.Combine(root, "oracle", "independent-derivation.md"), "\nTampered.\n");
                return;
            }

            JsonObject replay = ReadObject(root, "replay-dependencies.json");
            JsonObject dependency = replay["dependencies"]![0]!.AsObject();
            if (mutation == "hash")
            {
                dependency["sha256"] = new string('0', 64);
            }
            else if (mutation == "length")
            {
                dependency["byte_length"] = dependency["byte_length"]!.GetValue<long>() + 1;
            }
            else
            {
                Assert.Fail($"Unknown mutation '{mutation}'.");
            }

            WriteJson(root, "replay-dependencies.json", replay);
            ResealManifestDocument(root, "replay_dependency_fingerprint", "replay-dependencies.json");
        });
    }

    [TestMethod]
    [DataRow("file")]
    [DataRow("directory")]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void UnexpectedPackageEntryIsRejected(string entryKind)
    {
        AssertMutationRejected(CoreFixture, root =>
        {
            if (entryKind == "file")
            {
                File.WriteAllText(Path.Combine(root, "unexpected.json"), "{}\n");
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(root, "unexpected-directory"));
                File.WriteAllText(Path.Combine(root, "unexpected-directory", "payload.txt"), "unexpected");
            }
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void ReparsePointInPackageIsRejected()
    {
        WithFixtureCopy(CoreFixture, root =>
        {
            string outside = Path.Combine(Path.GetTempPath(), $"infinium-doc-fixture-outside-{Guid.NewGuid():N}");
            string junction = Path.Combine(root, "reparse-entry");
            Directory.CreateDirectory(outside);
            try
            {
                CreateJunctionOrInconclusive(junction, outside);
                Assert.ThrowsExactly<InvalidDataException>(() => DocumentationFixturePackageReader.Read(root));
            }
            finally
            {
                if (Directory.Exists(junction)
                    && (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0)
                {
                    Directory.Delete(junction);
                }
                Directory.Delete(outside);
            }
        });
    }

    private static void AssertMutationRejected(string fixtureId, Action<string> mutate)
    {
        WithFixtureCopy(fixtureId, root =>
        {
            mutate(root);
            Assert.ThrowsExactly<InvalidDataException>(() => DocumentationFixturePackageReader.Read(root));
        });
    }

    private static void WithFixtureCopy(string fixtureId, Action<string> action)
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-doc-fixture-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(FixtureRoot(fixtureId), root);
            action(root);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string FixtureRoot(string fixtureId) => TestRepository.PathFromRoot(
        "test-data", "public-fixtures", "documentation", fixtureId);

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
            File.Copy(file, target);
        }
    }

    private static JsonObject ReadObject(string root, string relative) => JsonNode.Parse(
        File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))!.AsObject();

    private static void WriteJson(string root, string relative, JsonNode value)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(path, value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static void ResealManifestDocument(
        string root,
        string manifestProperty,
        string relativeDocument)
    {
        JsonObject manifest = ReadObject(root, "public-manifest.json");
        manifest[manifestProperty] = Sha256(Path.Combine(root, relativeDocument));
        WriteJson(root, "public-manifest.json", manifest);
    }

    private static void ResealReplayInput(string root, string relativeInput)
    {
        JsonObject replay = ReadObject(root, "replay-dependencies.json");
        JsonObject dependency = replay["dependencies"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["identity_or_version"]!.GetValue<string>() == relativeInput);
        string inputPath = Path.Combine(root, relativeInput.Replace('/', Path.DirectorySeparatorChar));
        dependency["sha256"] = Sha256(inputPath);
        dependency["byte_length"] = new FileInfo(inputPath).Length;
        replay["dependency_graph_fingerprint"] = DependencyGraphFingerprint(replay);
        WriteJson(root, "replay-dependencies.json", replay);

        JsonObject manifest = ReadObject(root, "public-manifest.json");
        manifest["input_package_fingerprint"] = replay["dependency_graph_fingerprint"]!.GetValue<string>();
        manifest["replay_dependency_fingerprint"] = Sha256(Path.Combine(root, "replay-dependencies.json"));
        WriteJson(root, "public-manifest.json", manifest);
    }

    private static void ResealExpectedOutput(
        string root,
        string relativeOutput,
        string manifestOutputProperty)
    {
        JsonObject replay = ReadObject(root, "replay-dependencies.json");
        JsonObject reference = replay["expected_output_references"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["artifact_id"]!.GetValue<string>() == relativeOutput);
        string outputPath = Path.Combine(root, relativeOutput.Replace('/', Path.DirectorySeparatorChar));
        string fingerprint = Sha256(outputPath);
        reference["fingerprint"] = fingerprint;
        reference["byte_length"] = new FileInfo(outputPath).Length;
        WriteJson(root, "replay-dependencies.json", replay);

        JsonObject manifest = ReadObject(root, "public-manifest.json");
        manifest[manifestOutputProperty] = fingerprint;
        manifest["replay_dependency_fingerprint"] = Sha256(Path.Combine(root, "replay-dependencies.json"));
        WriteJson(root, "public-manifest.json", manifest);
    }

    private static string DependencyGraphFingerprint(JsonObject replay)
    {
        string canonical = string.Concat(replay["dependencies"]!.AsArray()
            .Select(item => item!.AsObject())
            .Select(item => FormattableString.Invariant(
                $"{item["identity_or_version"]!.GetValue<string>()}\0{item["sha256"]!.GetValue<string>()}\0{item["byte_length"]!.GetValue<long>()}\n"))
            .Order(StringComparer.Ordinal));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Sha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static void CreateJunctionOrInconclusive(string link, string target)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "cmd.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "/d", "/c", "mklink", "/J", link, target },
        }) ?? throw new InvalidOperationException("Could not start the junction helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Assert.Inconclusive($"Junction creation is unavailable: {process.StandardError.ReadToEnd()}");
        }
    }
}
