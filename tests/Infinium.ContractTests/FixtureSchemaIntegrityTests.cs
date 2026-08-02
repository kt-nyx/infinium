using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FixtureSchemaIntegrityTests
{
    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void ExecutionReaderRejectsRecursiveDuplicateProperties()
    {
        using FixturePackageTestBuilder fixture = new();
        string path = fixture.FilePath(FixturePackageReader.ExecutionInputFileName);
        string json = File.ReadAllText(path);
        json = json.Replace(
            "\"state\": \"empty\",",
            "\"state\": \"empty\",\n    \"state\": \"empty\",",
            StringComparison.Ordinal);
        File.WriteAllText(path, json);

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadExecutionInput(path));
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void ExecutionReaderRejectsDocumentBeyondBound()
    {
        using FixturePackageTestBuilder fixture = new();
        string path = fixture.FilePath(FixturePackageReader.ExecutionInputFileName);
        using (FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.SetLength((16 * 1024 * 1024) + 1);
        }

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadExecutionInput(path));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    public void HarnessRejectsUnknownSupportingDocumentFieldsAndRedistributionDrift()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddProvenanceProperty("unversioned_extension", new JsonObject());
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.SetRedistributionClass("non-redistributable");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void HarnessBindsRetainedInputAndOracleArtifactsTransitively()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddRetainedInputArtifact(
            "inputs/project-authored.esp",
            [0x54, 0x45, 0x53, 0x34]);
        fixture.AddRetainedOracleArtifact(
            "oracle/independent-review-evidence.json",
            "{}"u8.ToArray());

        EvaluationHarnessFixturePackage package =
            FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);

        Assert.AreEqual("fixture-development-1", package.FixtureId.Value);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void TaxonomyBindingClosureRejectsMissingDuplicateUnexpectedAndUnreferencedMaterial()
    {
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject bindings = ReadObject(root, "inputs/taxonomy-subject-bindings.json");
            bindings["bindings"]!.AsArray().RemoveAt(0);
            WriteAndResealInput(root, "inputs/taxonomy-subject-bindings.json", bindings);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject bindings = ReadObject(root, "inputs/taxonomy-subject-bindings.json");
            JsonArray items = bindings["bindings"]!.AsArray();
            items[1]!["sealed_subject_id"] = items[0]!["sealed_subject_id"]!.GetValue<string>();
            WriteAndResealInput(root, "inputs/taxonomy-subject-bindings.json", bindings);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject bindings = ReadObject(root, "inputs/taxonomy-subject-bindings.json");
            JsonArray items = bindings["bindings"]!.AsArray();
            items[1]!["production_subject_participant_id"] =
                items[0]!["production_subject_participant_id"]!.GetValue<string>();
            WriteAndResealInput(root, "inputs/taxonomy-subject-bindings.json", bindings);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject bindings = ReadObject(root, "inputs/taxonomy-subject-bindings.json");
            bindings["bindings"]!.AsArray()[0]!["sealed_subject_id"] = "TAX-UNEXPECTED";
            WriteAndResealInput(root, "inputs/taxonomy-subject-bindings.json", bindings);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            File.WriteAllText(
                Path.Combine(root, "oracle", "unreferenced-answer.json"),
                "{}\n");
        });
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Fault")]
    public void TaxonomyBindingClosureRejectsStaleCanonicalAndLengthSeals()
    {
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject projections = ReadObject(root, "oracle/taxonomy-projections.json");
            projections["subjects"]!.AsArray()[0]!["canonical_value_fingerprint"] = new string('0', 64);
            WriteAndResealOracle(root, "oracle/taxonomy-projections.json", projections);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject reference = oracle["ground_truth_methods"]!.AsArray()
                .SelectMany(method => method!["evidence_references"]!.AsArray())
                .Select(item => item!.AsObject())
                .Single(item => item["artifact_id"]!.GetValue<string>() == "oracle/taxonomy-projections.json");
            reference["byte_length"] = reference["byte_length"]!.GetValue<long>() + 1;
            WriteRootOracleAndResealManifest(root, oracle);
        });
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Fault")]
    public void RepeatedTaxonomyReferenceRequiresExactFirstReferenceMetadata()
    {
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject[] references = TaxonomyProjectionReferences(oracle);
            references[1]["artifact_id"] = "oracle/Taxonomy-projections.json";
            WriteRootOracleAndResealManifest(root, oracle);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject[] references = TaxonomyProjectionReferences(oracle);
            references[1]["artifact_version"] = "1.1.1";
            WriteRootOracleAndResealManifest(root, oracle);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject[] references = TaxonomyProjectionReferences(oracle);
            references[1]["fingerprint"] = new string('0', 64);
            WriteRootOracleAndResealManifest(root, oracle);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject[] references = TaxonomyProjectionReferences(oracle);
            references[1]["availability"] = "externally-reacquirable";
            WriteRootOracleAndResealManifest(root, oracle);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject[] references = TaxonomyProjectionReferences(oracle);
            references[1]["byte_length"] = references[1]["byte_length"]!.GetValue<long>() + 1;
            WriteRootOracleAndResealManifest(root, oracle);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject[] references = TaxonomyProjectionReferences(oracle);
            _ = references[0].Remove("byte_length");
            WriteRootOracleAndResealManifest(root, oracle);
        });

        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject[] references = TaxonomyProjectionReferences(oracle);
            _ = references[1].Remove("byte_length");
            WriteRootOracleAndResealManifest(root, oracle);
        });
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Fault")]
    public void TaxonomyProjectionSourcesRequireExactResolvedRetainedSet()
    {
        AssertTaxonomySourceMutationRejected((_, source, _) =>
            source["fingerprint"] = new string('0', 64));

        AssertTaxonomySourceMutationRejected((_, source, _) =>
            source["artifact_version"] = "1.1.1");

        AssertTaxonomySourceMutationRejected((_, source, _) =>
            source["availability"] = "externally-reacquirable");

        AssertTaxonomySourceMutationRejected((root, source, _) =>
        {
            string artifactId = source["artifact_id"]!.GetValue<string>();
            string artifactPath = Path.Combine(
                root,
                artifactId.Replace('/', Path.DirectorySeparatorChar));
            source["byte_length"] = new FileInfo(artifactPath).Length + 1;
        });

        AssertTaxonomySourceMutationRejected((_, source, sources) =>
            sources.Add(source.DeepClone()));

        AssertTaxonomySourceMutationRejected((_, _, sources) =>
            sources.RemoveAt(0));

        AssertTaxonomySourceMutationRejected((root, _, sources) =>
        {
            JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
            JsonObject unexpected = ArtifactReferences(
                    oracle,
                    "oracle/manual-hex-worksheet.json")
                .First()
                .DeepClone()
                .AsObject();
            sources[0] = unexpected;
        });

        AssertTaxonomySourceMutationRejected((_, source, _) =>
            source["artifact_id"] = "oracle/unresolved-source.json");
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessRejectsRetainedArtifactMutationMissingFilesAndTraversal()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputArtifact(
                "inputs/project-authored.esp",
                [0x54, 0x45, 0x53, 0x34]);
            fixture.MutateRetainedArtifact(
                "inputs/project-authored.esp",
                [0x54, 0x45, 0x53, 0x35]);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputReference(
                "inputs/missing.esp",
                new string('1', 64));

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputReference(
                "inputs/../escaped.esp",
                new string('1', 64));

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessRejectsHardLinkedRetainedArtifactsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using FixturePackageTestBuilder fixture = new();
        fixture.AddRetainedInputArtifact(
            "inputs/project-authored.esp",
            [0x54, 0x45, 0x53, 0x34]);
        string retained = Path.Combine(
            fixture.DirectoryPath,
            "inputs",
            "project-authored.esp");
        string source = retained + ".source";
        File.Move(retained, source);
        Assert.IsTrue(CreateHardLinkW(retained, source, 0));

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void HarnessRejectsAlternateDataStreamsAndWindowsDeviceNames()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            byte[] bytes = [0x54, 0x45, 0x53, 0x34];
            if (OperatingSystem.IsWindows())
            {
                string basePath = Path.Combine(fixture.DirectoryPath, "inputs", "base.esp");
                Directory.CreateDirectory(Path.GetDirectoryName(basePath)!);
                File.WriteAllBytes(basePath, [0x00]);
                File.WriteAllBytes(basePath + ":payload", bytes);
            }

            fixture.AddRetainedInputReference(
                "inputs/base.esp:payload",
                Convert.ToHexStringLower(SHA256.HashData(bytes)));
            fixture.SetDeclaredInputBytes(bytes.LongLength);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputReference("inputs/NUL.esp", new string('1', 64));
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputReference("INPUTS/missing.esp", new string('1', 64));
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void HarnessReusesConsistentScopedReferencesAndRejectsRetainedArtifactBudgetOverruns()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            byte[] bytes = [0x01];
            fixture.AddRetainedInputArtifact("inputs/duplicate.esp", bytes);
            fixture.AddRetainedInputReference(
                "inputs/duplicate.esp",
                Convert.ToHexStringLower(SHA256.HashData(bytes)));
            fixture.SetDeclaredInputBytes(1);

            _ = FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            byte[] bytes = [0x01];
            fixture.AddRetainedInputArtifact("inputs/duplicate.esp", bytes);
            fixture.AddRetainedInputReference("inputs/duplicate.esp", new string('1', 64));
            fixture.SetDeclaredInputBytes(1);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputArtifact("inputs/one.esp", [0x01]);
            fixture.AddRetainedInputArtifact("inputs/two.esp", [0x02]);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(
                    fixture.DirectoryPath,
                    new RetainedArtifactValidationTestOptions(MaximumReferenceCount: 1)));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputArtifact("inputs/one.esp", [0x01, 0x02]);
            fixture.AddRetainedInputArtifact("inputs/two.esp", [0x03, 0x04]);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(
                    fixture.DirectoryPath,
                    new RetainedArtifactValidationTestOptions(MaximumAggregateBytes: 3)));
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void HarnessRejectsRetainedArtifactGrowthAndInputByteDeclarationDrift()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            const string artifactId = "inputs/growing.esp";
            fixture.AddRetainedInputArtifact(artifactId, [0x01, 0x02, 0x03]);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(
                    fixture.DirectoryPath,
                    new RetainedArtifactValidationTestOptions(
                        MaximumArtifactBytes: 3,
                        BeforeArtifactOpen: candidate =>
                        {
                            if (StringComparer.Ordinal.Equals(candidate, artifactId))
                            {
                                File.WriteAllBytes(
                                    Path.Combine(
                                        fixture.DirectoryPath,
                                        artifactId.Replace(
                                            '/',
                                            Path.DirectorySeparatorChar)),
                                    [0x01, 0x02, 0x03, 0x04]);
                            }
                        })));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputArtifact("inputs/input.esp", [0x01, 0x02]);
            _ = fixture.AddSupplementalBethesdaOracle(
                "inputs/project-authored.esp",
                [0x54, 0x45, 0x53, 0x34]);
            fixture.SetDeclaredInputBytes(1);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    public void HarnessRequiresExactlySevenRootDocumentsButAllowsSupportDirectories()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            _ = fixture.AddSupplementalBethesdaOracle(
                "inputs/project-authored.esp",
                [0x54, 0x45, 0x53, 0x34]);
            Directory.CreateDirectory(Path.Combine(fixture.DirectoryPath, "support"));
            _ = FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            _ = fixture.AddSupplementalBethesdaOracle(
                "inputs/project-authored.esp",
                [0x54, 0x45, 0x53, 0x34]);
            File.WriteAllText(Path.Combine(fixture.DirectoryPath, "extra.json"), "{}");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void ExecutionInputRejectsOracleAnswerBearingAndPrivatePathArtifactIds()
    {
        string[] rejectedArtifactIds =
        [
            "oracle/independent-byte-facts.json",
            "inputs/expected-oracle.json",
            "inputs/reviewer/answers.json",
            "C:/private/evaluator/input.esp",
            "file:private-evaluator-input.esp",
        ];
        foreach (string artifactId in rejectedArtifactIds)
        {
            using FixturePackageTestBuilder fixture = new();
            fixture.AddRetainedInputReference(artifactId, new string('1', 64));
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath),
                artifactId);
        }

        using FixturePackageTestBuilder validFixture = new();
        validFixture.AddRetainedInputReference(
            "logical-dependency-id",
            new string('1', 64));
        validFixture.AddRetainedInputReference(
            "https://public.example/specification",
            new string('2', 64));
        _ = FixturePackageReader.ReadForEvaluationHarness(validFixture.DirectoryPath);
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [DoNotParallelize]
    public void HarnessRejectsJunctionSwapBeforeScopePinOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using FixturePackageTestBuilder fixture = new();
        const string artifactId = "inputs/project-authored.esp";
        byte[] bytes = [0x54, 0x45, 0x53, 0x34];
        fixture.AddRetainedInputArtifact(artifactId, bytes);
        string inputRoot = Path.Combine(fixture.DirectoryPath, "inputs");
        string originalRoot = Path.Combine(fixture.DirectoryPath, "inputs-original");
        string outsideRoot = Path.Combine(fixture.DirectoryPath, "outside");
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllBytes(Path.Combine(outsideRoot, "project-authored.esp"), bytes);

        try
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(
                    fixture.DirectoryPath,
                    new RetainedArtifactValidationTestOptions(
                        BeforeScopePin: candidate =>
                        {
                            if (StringComparer.Ordinal.Equals(candidate, artifactId))
                            {
                                Directory.Move(inputRoot, originalRoot);
                                CreateJunctionOrInconclusive(inputRoot, outsideRoot);
                            }
                        })));
        }
        finally
        {
            DeleteJunction(inputRoot);
            if (Directory.Exists(originalRoot))
            {
                Directory.Move(originalRoot, inputRoot);
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [DoNotParallelize]
    public void HarnessRejectsJunctionSwapAfterScopePinOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using FixturePackageTestBuilder fixture = new();
        const string artifactId = "inputs/project-authored.esp";
        byte[] bytes = [0x54, 0x45, 0x53, 0x34];
        fixture.AddRetainedInputArtifact(artifactId, bytes);
        string inputRoot = Path.Combine(fixture.DirectoryPath, "inputs");
        string originalRoot = Path.Combine(fixture.DirectoryPath, "inputs-original");
        string outsideRoot = Path.Combine(fixture.DirectoryPath, "outside");
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllBytes(Path.Combine(outsideRoot, "project-authored.esp"), bytes);

        try
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(
                    fixture.DirectoryPath,
                    new RetainedArtifactValidationTestOptions(
                        BeforeArtifactOpen: candidate =>
                        {
                            if (StringComparer.Ordinal.Equals(candidate, artifactId))
                            {
                                Directory.Move(inputRoot, originalRoot);
                                CreateJunctionOrInconclusive(inputRoot, outsideRoot);
                            }
                        })));
        }
        finally
        {
            DeleteJunction(inputRoot);
            if (Directory.Exists(originalRoot))
            {
                Directory.Move(originalRoot, inputRoot);
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [DoNotParallelize]
    public void SupplementalOracleUsesTheFingerprintValidatedSnapshotAcrossPhases()
    {
        using FixturePackageTestBuilder fixture = new();
        byte[] supplemental = fixture.AddSupplementalBethesdaOracle(
            "inputs/project-authored.esp",
            [0x54, 0x45, 0x53, 0x34]);
        string oraclePath = Path.Combine(
            fixture.DirectoryPath,
            BethesdaByteOracleValidator.ArtifactId.Replace(
                '/',
                Path.DirectorySeparatorChar));

        EvaluationHarnessFixturePackage package =
            FixturePackageReader.ReadForEvaluationHarness(
                fixture.DirectoryPath,
                new RetainedArtifactValidationTestOptions(
                    AfterArtifactsSnapshotted: () =>
                        File.WriteAllBytes(oraclePath, new byte[supplemental.Length])));

        Assert.AreEqual("fixture-development-1", package.FixtureId.Value);
        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void MalformedFingerprintValidSupplementalOracleIsAFixtureDataFailure()
    {
        using FixturePackageTestBuilder fixture = new();
        _ = fixture.AddSupplementalBethesdaOracle(
            "inputs/project-authored.esp",
            [0x54, 0x45, 0x53, 0x34]);
        fixture.ReplaceRetainedOracleArtifactAndRefreshReference(
            BethesdaByteOracleValidator.ArtifactId,
            "not-json"u8.ToArray());

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void SupplementalOracleArtifactIdRejectsCaseOnlyAlias()
    {
        using FixturePackageTestBuilder fixture = new();
        _ = fixture.AddSupplementalBethesdaOracle(
            "inputs/project-authored.esp",
            [0x54, 0x45, 0x53, 0x34]);
        fixture.RenameRetainedOracleReference(
            BethesdaByteOracleValidator.ArtifactId,
            "oracle/INDEPENDENT-BYTE-FACTS.JSON");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleRejectsWrongCollectionTypeUnknownMethodsAndDuplicateIds()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_findings", "expected-1", "observation");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem(
                "expected_findings",
                "expected-1",
                "finding",
                "missing-ground-truth-method");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_observations", "duplicate-expected-id", "observation");
            fixture.AddOracleExpectedItem("expected_findings", "duplicate-expected-id", "finding");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddDuplicateGroundTruthMethod();
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleRequiresFailureAndCollectionStateExpectations()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.RemoveOracleProperty("expected_failures");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.RemoveOracleProperty("expected_collection_states");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleCollectionStatesMustMatchExpectedItemCounts()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.SetExpectedCollectionState("observations", "populated");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_observations", "expected-1", "observation");
            fixture.SetExpectedCollectionState("observations", "empty");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    public void CoverageExpectationDoesNotLaunderCoverageGapCollectionState()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddOracleExpectedItem(
            "expected_coverage_and_gaps",
            "expected-coverage-1",
            "coverage");

        EvaluationHarnessFixturePackage package =
            FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);

        Assert.AreEqual(
            "empty",
            package.Oracle.GetProperty("expected_collection_states")
                .GetProperty("coverage_gaps")
                .GetProperty("state")
                .GetString());
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleTaxonomyAssignmentsRequireUniqueIdsAndExpectedSubjects()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddTaxonomyAssignment("assignment-1", "missing-expected-item");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_observations", "expected-1", "observation");
            fixture.AddTaxonomyAssignment("assignment-1", "expected-1");
            fixture.AddTaxonomyAssignment("assignment-1", "expected-1");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    public void PartitionHistoryForbidsKnownAnswerLaundering()
    {
        foreach ((string from, string to) in new[]
        {
            ("development", "validation"),
            ("development", "held-out"),
            ("validation", "held-out"),
            ("validation", "validation"),
        })
        {
            using FixturePackageTestBuilder fixture = new();
            JsonArray history =
            [
                FixturePackageTestBuilder.PartitionTransition(
                    null,
                    from,
                    DateTimeOffset.UnixEpoch,
                    "initial registration",
                    false),
                FixturePackageTestBuilder.PartitionTransition(
                    from,
                    to,
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "forbidden relabel",
                    false),
            ];
            fixture.SetPartitionHistory(to, history);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath),
                $"{from} -> {to}");
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void KnownAnswerTransitionRequiresChronologyAndIndependentFingerprints()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            JsonArray history =
            [
                FixturePackageTestBuilder.PartitionTransition(
                    null,
                    "validation",
                    DateTimeOffset.UnixEpoch.AddSeconds(2),
                    "initial registration",
                    false),
                FixturePackageTestBuilder.PartitionTransition(
                    "validation",
                    "development",
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "implementation influence",
                    true,
                    includeReplacement: true),
            ];
            fixture.SetPartitionHistory("development", history);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllBytes(fixture.FilePath(FixturePackageReader.PublicManifestFileName)));
            string currentInput = manifest.RootElement.GetProperty("input_package_fingerprint").GetString()!;
            JsonArray history =
            [
                FixturePackageTestBuilder.PartitionTransition(
                    null,
                    "held-out",
                    DateTimeOffset.UnixEpoch,
                    "initial registration",
                    false),
                FixturePackageTestBuilder.PartitionTransition(
                    "held-out",
                    "development",
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "implementation influence",
                    true,
                    includeReplacement: true,
                    replacementInputFingerprint: currentInput),
            ];
            fixture.SetPartitionHistory("development", history);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Contract")]
    public void KnownAnswerTransitionAcceptsCompleteIndependentReplacementMetadata()
    {
        using FixturePackageTestBuilder fixture = new();
        JsonArray history =
        [
            FixturePackageTestBuilder.PartitionTransition(
                null,
                "held-out",
                DateTimeOffset.UnixEpoch,
                "initial registration",
                false),
            FixturePackageTestBuilder.PartitionTransition(
                "held-out",
                "development",
                DateTimeOffset.UnixEpoch.AddSeconds(1),
                "implementation influence",
                true,
                includeReplacement: true),
        ];
        fixture.SetPartitionHistory("development", history);

        EvaluationHarnessFixturePackage package =
            FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);

        Assert.AreEqual(FixturePartition.Development, package.Partition);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    public void SchemasRequireCanonicalUtcTimestampText()
    {
        foreach (string timestamp in new[]
        {
            "1970-01-01T01:00:00.0000000+01:00",
            "1970-01-01T00:00:00Z",
        })
        {
            using FixturePackageTestBuilder fixture = new();
            fixture.SetPublicString("created_at", timestamp);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath),
                timestamp);
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    public void CliSummaryRequiresDurationAndSeparatedCostUsage()
    {
        JsonObject summary = CreateCliSummary();
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            EmbeddedJsonSchemaValidator.Validate(
                document.RootElement,
                "cli-summary.v1.schema.json");
        }

        summary.Remove("duration_ms");
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => EmbeddedJsonSchemaValidator.Validate(
                    document.RootElement,
                    "cli-summary.v1.schema.json"));
        }

        summary = CreateCliSummary();
        summary["cost"]!.AsObject()["unresolved_hold"] = true;
        summary["cost"]!.AsObject()["calculated_actual_nano_usd"] = null;
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            EmbeddedJsonSchemaValidator.Validate(
                document.RootElement,
                "cli-summary.v1.schema.json");
        }

        summary = CreateCliSummary();
        summary["cost"]!.AsObject()["reserved_nano_usd"] = -1;
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => EmbeddedJsonSchemaValidator.Validate(
                    document.RootElement,
                    "cli-summary.v1.schema.json"));
        }

        summary = CreateCliSummary();
        summary["cost"]!.AsObject()["calculated_actual_nano_usd"] = null;
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => EmbeddedJsonSchemaValidator.Validate(
                    document.RootElement,
                    "cli-summary.v1.schema.json"));
        }
    }

    private static JsonObject CreateCliSummary()
    {
        JsonObject typedCounts = new();
        foreach (string name in new[]
        {
            "observations",
            "deterministic_results",
            "external_claims",
            "application_links",
            "discovery_leads",
            "model_proposals",
            "proposal_admissions",
            "candidates",
            "hypotheses",
            "findings",
            "recommendations",
            "supported_cases",
            "lead_only_cases",
            "abstentions",
            "invalid_inputs",
            "coverage_gaps",
            "failures",
        })
        {
            typedCounts[name] = 0;
        }

        JsonObject coverageCounts = new();
        foreach (string name in new[]
        {
            "completed",
            "completed_with_gaps",
            "failed",
            "skipped_by_configuration",
            "skipped_by_limit",
            "unsupported",
        })
        {
            coverageCounts[name] = 0;
        }

        return new JsonObject
        {
            ["schema_id"] = "infinium.cli-summary/v1",
            ["schema_version"] = "1",
            ["run_id"] = "run-1",
            ["outcome"] = "completed",
            ["exit_code"] = 0,
            ["typed_counts"] = typedCounts,
            ["coverage_state_counts"] = coverageCounts,
            ["duration_ms"] = 0,
            ["cost"] = new JsonObject
            {
                ["provider_input_tokens"] = 0,
                ["provider_output_tokens"] = 0,
                ["provider_reasoning_tokens"] = 0,
                ["dispatch_count"] = 0,
                ["tool_call_count"] = 0,
                ["calculated_actual_nano_usd"] = 0,
                ["reserved_nano_usd"] = 0,
                ["unresolved_hold"] = false,
            },
            ["readiness"] = "no-readiness-evaluation",
            ["no_safety_guarantee"] = true,
        };
    }

    private static void AssertTaxonomyPackageMutationRejected(Action<string> mutate)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-taxonomy-contract-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(
                TestRepository.PathFromRoot(
                    "test-data", "evaluation", "m1-semantic", "BETH-UNSUPPORTED-VAL"),
                root);
            mutate(root);
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            File.Copy(file, target);
        }
    }

    private static JsonObject ReadObject(string root, string relativePath) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar))))!.AsObject();

    private static void WriteAndResealInput(
        string root,
        string artifactId,
        JsonObject value)
    {
        string artifactPath = Path.Combine(
            root,
            artifactId.Replace('/', Path.DirectorySeparatorChar));
        WriteJson(artifactPath, value);
        JsonObject execution = ReadObject(root, FixturePackageReader.ExecutionInputFileName);
        JsonObject reference = execution["input_payload_refs"]!.AsArray()
            .Select(item => item!.AsObject())
            .Single(item => item["artifact_id"]!.GetValue<string>() == artifactId);
        reference["fingerprint"] = Sha256(artifactPath);
        if (reference.ContainsKey("byte_length"))
        {
            reference["byte_length"] = new FileInfo(artifactPath).Length;
        }

        string executionPath = Path.Combine(root, FixturePackageReader.ExecutionInputFileName);
        WriteJson(executionPath, execution);
        JsonObject manifest = ReadObject(root, FixturePackageReader.PublicManifestFileName);
        manifest["input_package_fingerprint"] = Sha256(executionPath);
        WriteJson(Path.Combine(root, FixturePackageReader.PublicManifestFileName), manifest);
    }

    private static void WriteAndResealOracle(
        string root,
        string artifactId,
        JsonObject value)
    {
        string artifactPath = Path.Combine(
            root,
            artifactId.Replace('/', Path.DirectorySeparatorChar));
        WriteJson(artifactPath, value);
        JsonObject oracle = ReadObject(root, FixturePackageReader.OracleFileName);
        JsonObject[] references = ArtifactReferences(oracle, artifactId).ToArray();
        Assert.IsNotEmpty(references);
        foreach (JsonObject reference in references)
        {
            reference["fingerprint"] = Sha256(artifactPath);
            if (reference.ContainsKey("byte_length"))
            {
                reference["byte_length"] = new FileInfo(artifactPath).Length;
            }
        }

        WriteRootOracleAndResealManifest(root, oracle);
    }

    private static void WriteRootOracleAndResealManifest(string root, JsonObject oracle)
    {
        string oraclePath = Path.Combine(root, FixturePackageReader.OracleFileName);
        WriteJson(oraclePath, oracle);
        JsonObject manifest = ReadObject(root, FixturePackageReader.PublicManifestFileName);
        manifest["oracle_fingerprint"] = Sha256(oraclePath);
        WriteJson(Path.Combine(root, FixturePackageReader.PublicManifestFileName), manifest);
    }

    private static JsonObject[] TaxonomyProjectionReferences(JsonObject oracle)
    {
        JsonObject[] references = ArtifactReferences(
            oracle,
            "oracle/taxonomy-projections.json").ToArray();
        Assert.HasCount(2, references);
        return references;
    }

    private static IEnumerable<JsonObject> ArtifactReferences(
        JsonNode? node,
        string artifactId)
    {
        if (node is JsonObject value)
        {
            if (value["artifact_id"]?.GetValue<string>() == artifactId)
            {
                yield return value;
            }

            foreach ((_, JsonNode? child) in value)
            {
                foreach (JsonObject reference in ArtifactReferences(child, artifactId))
                {
                    yield return reference;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                foreach (JsonObject reference in ArtifactReferences(child, artifactId))
                {
                    yield return reference;
                }
            }
        }
    }

    private static void AssertTaxonomySourceMutationRejected(
        Action<string, JsonObject, JsonArray> mutate)
    {
        AssertTaxonomyPackageMutationRejected(root =>
        {
            const string artifactId = "oracle/taxonomy-projections.json";
            JsonObject projection = ReadObject(root, artifactId);
            JsonArray sources = projection["source_artifacts"]!.AsArray();
            mutate(root, sources[0]!.AsObject(), sources);
            WriteAndResealOracle(root, artifactId, projection);
        });
    }

    private static void WriteJson(string path, JsonNode value)
    {
        File.WriteAllText(
            path,
            value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
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
            ArgumentList =
            {
                "/d",
                "/c",
                "mklink",
                "/J",
                link,
                target,
            },
        }) ?? throw new InvalidOperationException("Could not start the junction helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Assert.Inconclusive(
                $"Junction creation is unavailable: {process.StandardError.ReadToEnd()}");
        }
    }

    private static void DeleteJunction(string path)
    {
        if (Directory.Exists(path)
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(
        string fileName,
        string existingFileName,
        nint securityAttributes);
}
