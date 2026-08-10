using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.PublicFixtures;

public sealed record DocumentationFixturePackage(
    OpaqueId FixtureId,
    ContractVersion FixtureVersion,
    FixturePartition Partition,
    DocumentationClaimImportManifestContract ClaimImport,
    ReadOnlyMemory<byte> SourceBytes,
    JsonElement CaseMatrix,
    JsonElement Oracle,
    JsonElement PublicManifest,
    JsonElement Provenance,
    JsonElement ReplayDependencies);

public static class DocumentationFixturePackageReader
{
    private const long MaximumJsonBytes = 4 * 1024 * 1024;
    private const long MaximumDerivationBytes = 1024 * 1024;
    private const int MaximumDepth = 64;
    private const int MaximumReplayDependencies = 64;
    private const string IndependentDerivationPath = "oracle/independent-derivation.md";
    private static readonly HashSet<string> ExpectedRootFiles = new(StringComparer.Ordinal)
    {
        "expected-oracle.json",
        "partition-history.json",
        "provenance.json",
        "public-manifest.json",
        "redistribution.json",
        "replay-dependencies.json",
    };
    private static readonly HashSet<string> ExpectedInputFiles = new(StringComparer.Ordinal)
    {
        "case-matrix.json",
        "claim-import.json",
        "source.txt",
    };

    public static DocumentationFixturePackage Read(string fixtureDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureDirectory);
        string root = Path.GetFullPath(fixtureDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Documentation fixture directory '{root}' does not exist.");
        }
        EnsureNoReparsePoints(root);
        EnsureExactFiles(root, ExpectedRootFiles);
        EnsureExactDirectories(root, ["inputs", "oracle"]);
        string inputs = RequiredDirectory(root, "inputs");
        string oracleDirectory = RequiredDirectory(root, "oracle");
        EnsureExactFiles(inputs, ExpectedInputFiles);
        EnsureExactDirectories(inputs, []);
        EnsureExactFiles(oracleDirectory, ["independent-derivation.md"]);
        EnsureExactDirectories(oracleDirectory, []);

        using BoundedJsonDocumentSnapshot publicSnapshot = ReadJson(root, "public-manifest.json");
        using BoundedJsonDocumentSnapshot claimSnapshot = ReadJson(inputs, "claim-import.json");
        using BoundedJsonDocumentSnapshot caseSnapshot = ReadJson(inputs, "case-matrix.json");
        using BoundedJsonDocumentSnapshot oracleSnapshot = ReadJson(root, "expected-oracle.json");
        using BoundedJsonDocumentSnapshot provenanceSnapshot = ReadJson(root, "provenance.json");
        using BoundedJsonDocumentSnapshot replaySnapshot = ReadJson(root, "replay-dependencies.json");
        using BoundedJsonDocumentSnapshot redistributionSnapshot = ReadJson(root, "redistribution.json");
        using BoundedJsonDocumentSnapshot partitionSnapshot = ReadJson(root, "partition-history.json");

        JsonElement publicManifest = RequireObject(publicSnapshot.Document.RootElement, "public-manifest.json");
        ActiveJsonSchemaValidator.Validate(publicManifest, "fixture-public-manifest.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(
            RequireObject(caseSnapshot.Document.RootElement, "case-matrix.json"),
            "documentation-fixture-case-matrix.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(
            RequireObject(oracleSnapshot.Document.RootElement, "expected-oracle.json"),
            "documentation-fixture-oracle.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(
            RequireObject(provenanceSnapshot.Document.RootElement, "provenance.json"),
            "documentation-fixture-provenance.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(
            RequireObject(replaySnapshot.Document.RootElement, "replay-dependencies.json"),
            "replay-dependencies.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(
            RequireObject(redistributionSnapshot.Document.RootElement, "redistribution.json"),
            "fixture-redistribution.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(
            RequireObject(partitionSnapshot.Document.RootElement, "partition-history.json"),
            "fixture-partition-history.v1.schema.json");
        OpaqueId fixtureId = new(RequireString(publicManifest, "fixture_id"));
        ContractVersion fixtureVersion = ContractVersion.Parse(RequireString(publicManifest, "fixture_version"));
        if (!StringComparer.Ordinal.Equals(RequireString(publicManifest, "review_state"), "accepted"))
        {
            throw new InvalidDataException("Current documentation fixtures must have independent accepted review state.");
        }

        DocumentationClaimImportManifestContract claimImport =
            DocumentationClaimImportJsonCodec.Deserialize(claimSnapshot.Document.RootElement.GetRawTextBytes());
        byte[] sourceBytes = ReadBoundedSource(Path.Combine(inputs, "source.txt"));
        string sourceSha = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
        if (sourceBytes.LongLength != claimImport.ByteLength
            || !StringComparer.Ordinal.Equals(sourceSha, claimImport.ByteFingerprint.Value))
        {
            throw new InvalidDataException("Documentation fixture source bytes do not match the claim-import identity.");
        }

        ValidateIdentity(caseSnapshot.Document.RootElement, fixtureId, fixtureVersion, "case-matrix.json");
        ValidateIdentity(oracleSnapshot.Document.RootElement, fixtureId, fixtureVersion, "expected-oracle.json");
        ValidateIdentity(provenanceSnapshot.Document.RootElement, fixtureId, fixtureVersion, "provenance.json");
        ValidateIdentity(replaySnapshot.Document.RootElement, fixtureId, fixtureVersion, "replay-dependencies.json");
        ValidateIdentity(redistributionSnapshot.Document.RootElement, fixtureId, fixtureVersion, "redistribution.json");
        ValidateIdentity(partitionSnapshot.Document.RootElement, fixtureId, fixtureVersion, "partition-history.json");
        ValidateCaseMatrix(
            caseSnapshot.Document.RootElement,
            oracleSnapshot.Document.RootElement,
            claimImport);
        FixturePartition partition = ParsePartition(RequireString(publicManifest, "partition"));
        ValidatePartitionHistory(publicManifest, partitionSnapshot.Document.RootElement, partition);
        byte[] derivationBytes = ReadBoundedFile(
            Path.Combine(root, IndependentDerivationPath.Replace('/', Path.DirectorySeparatorChar)),
            MaximumDerivationBytes,
            "Documentation fixture independent derivation");
        string derivationSha = Convert.ToHexStringLower(SHA256.HashData(derivationBytes));
        ValidateProvenance(
            publicManifest,
            provenanceSnapshot.Document.RootElement,
            derivationSha,
            derivationBytes.LongLength);
        ValidateFingerprint(publicManifest, "oracle_fingerprint", oracleSnapshot.Sha256);
        ValidateFingerprint(publicManifest, "provenance_fingerprint", provenanceSnapshot.Sha256);
        ValidateFingerprint(publicManifest, "replay_dependency_fingerprint", replaySnapshot.Sha256);
        ValidateRedistribution(publicManifest, redistributionSnapshot.Document.RootElement);
        string graphFingerprint = ValidateReplayClosure(
            root,
            replaySnapshot.Document.RootElement,
            RequireString(publicManifest, "redistribution_class"),
            ExpectedCleanReplayState(oracleSnapshot.Document.RootElement),
            oracleSnapshot.Sha256,
            new FileInfo(Path.Combine(root, "expected-oracle.json")).Length,
            derivationSha,
            derivationBytes.LongLength);
        ValidateFingerprint(publicManifest, "input_package_fingerprint", graphFingerprint);

        return new DocumentationFixturePackage(
            fixtureId,
            fixtureVersion,
            partition,
            claimImport,
            sourceBytes,
            caseSnapshot.Document.RootElement.Clone(),
            oracleSnapshot.Document.RootElement.Clone(),
            publicManifest.Clone(),
            provenanceSnapshot.Document.RootElement.Clone(),
            replaySnapshot.Document.RootElement.Clone());
    }

    private static string ValidateReplayClosure(
        string root,
        JsonElement replay,
        string redistributionClass,
        string expectedCleanReplayState,
        string oracleSha,
        long oracleByteLength,
        string derivationSha,
        long derivationByteLength)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireString(replay, "expected_replay_state"),
                expectedCleanReplayState))
        {
            throw new InvalidDataException(
                "Documentation fixture replay expectation drifted from its clean oracle state.");
        }

        JsonElement dependencies = RequireObject(replay, "replay-dependencies.json")
            .GetProperty("dependencies");
        if (dependencies.GetArrayLength() == 0
            || dependencies.GetArrayLength() > MaximumReplayDependencies)
        {
            throw new InvalidDataException("Documentation fixture replay dependency closure is empty or unbounded.");
        }

        HashSet<string> dependencyIds = new(StringComparer.Ordinal);
        HashSet<string> dependencyPaths = new(StringComparer.Ordinal);
        List<string> entries = [];
        foreach (JsonElement dependency in dependencies.EnumerateArray())
        {
            string dependencyId = RequireString(dependency, "dependency_id");
            string relative = RequireSafePackagePath(
                RequireString(dependency, "identity_or_version"),
                "inputs/");
            if (!dependencyIds.Add(dependencyId) || !dependencyPaths.Add(relative))
            {
                throw new InvalidDataException("Documentation fixture replay dependencies must have unique IDs and paths.");
            }

            if (!StringComparer.Ordinal.Equals(
                    RequireString(dependency, "kind"),
                    "tracked-fixture-input")
                || !dependency.GetProperty("required_for").EnumerateArray()
                    .Select(item => item.GetString())
                    .ToHashSet(StringComparer.Ordinal)
                    .IsSupersetOf(["clean-recomputation", "audit"])
                || !StringComparer.Ordinal.Equals(
                    RequireString(dependency, "retention_location_class"),
                    "tracked-repository")
                || !StringComparer.Ordinal.Equals(
                    RequireString(dependency, "availability"),
                    "retained")
                || !StringComparer.Ordinal.Equals(
                    RequireString(dependency, "permission_and_redistribution"),
                    redistributionClass))
            {
                throw new InvalidDataException(
                    "Documentation fixture replay dependencies must be retained, tracked, and redistributable with the package.");
            }

            string fullPath = RequiredPackageFile(root, relative);
            byte[] bytes = ReadBoundedFile(fullPath, 8 * 1024 * 1024, "Documentation fixture replay dependency");
            string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            long length = bytes.LongLength;
            if (!StringComparer.Ordinal.Equals(sha, RequireString(dependency, "sha256"))
                || length != dependency.GetProperty("byte_length").GetInt64())
            {
                throw new InvalidDataException($"Documentation fixture replay dependency drifted: {relative}");
            }
            entries.Add(FormattableString.Invariant($"{relative}\0{sha}\0{length}\n"));
        }

        HashSet<string> physicalInputs = Directory.EnumerateFiles(
                Path.Combine(root, "inputs"),
                "*",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);
        if (!physicalInputs.SetEquals(dependencyPaths))
        {
            throw new InvalidDataException("Documentation fixture replay dependencies do not exactly close the input tree.");
        }

        string graph = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Concat(entries.Order(StringComparer.Ordinal)))));
        ValidateFingerprint(replay, "dependency_graph_fingerprint", graph);
        ValidateExpectedOutputReferences(
            root,
            replay,
            oracleSha,
            oracleByteLength,
            derivationSha,
            derivationByteLength);
        return graph;
    }

    private static string ExpectedCleanReplayState(JsonElement oracle) =>
        oracle.TryGetProperty("revision", out JsonElement revision)
            ? RequireString(revision, "replay_state")
            : RequireString(oracle.GetProperty("clean"), "replay_state");

    private static void ValidateExpectedOutputReferences(
        string root,
        JsonElement replay,
        string oracleSha,
        long oracleByteLength,
        string derivationSha,
        long derivationByteLength)
    {
        Dictionary<string, (string Sha, long ByteLength)> expected = new(StringComparer.Ordinal)
        {
            ["expected-oracle.json"] = (oracleSha, oracleByteLength),
            [IndependentDerivationPath] = (derivationSha, derivationByteLength),
        };
        HashSet<string> actualPaths = new(StringComparer.Ordinal);
        foreach (JsonElement reference in replay.GetProperty("expected_output_references").EnumerateArray())
        {
            string relative = RequireSafePackagePath(RequireString(reference, "artifact_id"), allowedPrefix: null);
            if (!expected.TryGetValue(relative, out (string Sha, long ByteLength) snapshot)
                || !actualPaths.Add(relative))
            {
                throw new InvalidDataException("Documentation fixture expected-output references are missing, extra, or duplicated.");
            }

            _ = RequiredPackageFile(root, relative);
            if (!StringComparer.Ordinal.Equals(
                    RequireString(reference, "artifact_version"),
                    RequireString(replay, "fixture_version"))
                || !StringComparer.Ordinal.Equals(RequireString(reference, "fingerprint"), snapshot.Sha)
                || reference.GetProperty("byte_length").GetInt64() != snapshot.ByteLength)
            {
                throw new InvalidDataException($"Documentation fixture expected-output reference drifted: {relative}");
            }
        }

        if (!actualPaths.SetEquals(expected.Keys))
        {
            throw new InvalidDataException("Documentation fixture expected-output reference closure is incomplete.");
        }
    }

    private static void ValidateProvenance(
        JsonElement publicManifest,
        JsonElement provenance,
        string derivationSha,
        long derivationByteLength)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireString(publicManifest, "created_at"),
                RequireString(provenance, "created_at")))
        {
            throw new InvalidDataException("Documentation fixture provenance creation time drifted from its manifest.");
        }

        JsonElement isolation = provenance.GetProperty("answer_isolation");
        JsonElement derivation = isolation.GetProperty("derivation_record");
        if (!StringComparer.Ordinal.Equals(
                RequireString(derivation, "artifact_id"),
                IndependentDerivationPath)
            || !StringComparer.Ordinal.Equals(
                RequireString(derivation, "artifact_version"),
                RequireString(provenance, "fixture_version"))
            || !StringComparer.Ordinal.Equals(RequireString(derivation, "fingerprint"), derivationSha)
            || derivation.GetProperty("byte_length").GetInt64() != derivationByteLength)
        {
            throw new InvalidDataException("Documentation fixture independent derivation binding drifted.");
        }
    }

    private static void ValidateCaseMatrix(
        JsonElement caseMatrix,
        JsonElement oracle,
        DocumentationClaimImportManifestContract claimImport)
    {
        HashSet<string> caseIds = new(StringComparer.Ordinal);
        foreach (JsonElement item in caseMatrix.GetProperty("cases").EnumerateArray())
        {
            if (!caseIds.Add(RequireString(item, "case_id")))
            {
                throw new InvalidDataException("Documentation fixture case IDs must be unique.");
            }
        }

        HashSet<string> boundaryIds = new(StringComparer.Ordinal);
        foreach (JsonElement boundary in caseMatrix
                     .GetProperty("aggregate_output_binding")
                     .GetProperty("boundaries")
                     .EnumerateArray())
        {
            if (!boundaryIds.Add(RequireString(boundary, "boundary_id")))
            {
                throw new InvalidDataException("Documentation fixture boundary IDs must be unique.");
            }
        }
        if (!boundaryIds.SetEquals(["provider", "hosted-search", "nexus", "loot"]))
        {
            throw new InvalidDataException("Documentation fixture must explicitly close every external boundary.");
        }

        Dictionary<OpaqueId, DocumentationApplicationInputContract> applications = claimImport.Applications
            .ToDictionary(item => item.ClaimKey);
        HashSet<OpaqueId> targetKeys = [];
        foreach (JsonElement target in caseMatrix.GetProperty("application_targets").EnumerateArray())
        {
            OpaqueId claimKey = new(RequireString(target, "application_claim_key"));
            if (!targetKeys.Add(claimKey)
                || !applications.TryGetValue(claimKey, out DocumentationApplicationInputContract? application)
                || application.ConsumingRunId != new OpaqueId(RequireString(target, "consuming_run_id"))
                || application.AnalysisContextId != new OpaqueId(RequireString(target, "analysis_context_id"))
                || application.SubjectId != new OpaqueId(RequireString(target, "subject_id"))
                || !StringComparer.Ordinal.Equals(application.SubjectType, RequireString(target, "subject_type"))
                || application.DependencyClosureId != new OpaqueId(RequireString(target, "dependency_closure_id")))
            {
                throw new InvalidDataException("Documentation fixture application targets drifted from claim-import semantics.");
            }
        }
        if (!targetKeys.SetEquals(applications.Keys))
        {
            throw new InvalidDataException("Documentation fixture application target closure is incomplete.");
        }

        ValidateOracleInternalClosure(oracle);
        ValidateExecutionAndAggregateBindings(caseMatrix, oracle);
        ValidateOracleBindings(caseMatrix, oracle);
    }

    private static void ValidateOracleInternalClosure(JsonElement oracle)
    {
        if (oracle.TryGetProperty("applications", out JsonElement applications))
        {
            JsonElement counts = oracle.GetProperty("expected_counts");
            ValidateExactCounts(counts,
                ("revisions", 1),
                ("imports", 1),
                ("passages", oracle.GetProperty("passages").GetArrayLength()),
                ("claims", oracle.GetProperty("claims").GetArrayLength()),
                ("applications", applications.GetArrayLength()),
                ("purpose_assignments", 1),
                ("deletion_receipts", oracle.GetProperty("deletion_receipts").GetArrayLength()),
                ("gaps", oracle.GetProperty("gaps").GetArrayLength()),
                ("failures", oracle.GetProperty("failure_descriptors").GetArrayLength()));
            RequireUniqueIds(oracle.GetProperty("passages"), "passage_id");
            RequireUniqueIds(oracle.GetProperty("claims"), "claim_id");
            RequireUniqueIds(applications, "application_id");
            RequireUniqueIds(oracle.GetProperty("gaps"), "gap_id");
            RequireUniqueIds(oracle.GetProperty("deletion_receipts"), "receipt_id");
            return;
        }

        JsonElement clean = oracle.GetProperty("clean");
        JsonElement cleanCounts = clean.GetProperty("expected_counts");
        ValidateExactCounts(cleanCounts,
            ("revisions", 1),
            ("imports", 1),
            ("passages", clean.GetProperty("passages").GetArrayLength()),
            ("claims", clean.GetProperty("claims").GetArrayLength()),
            ("applications", 1),
            ("purpose_assignments", 0),
            ("deletion_receipts", clean.GetProperty("deletion_receipts").GetArrayLength()),
            ("gaps", 0),
            ("failures", clean.GetProperty("failure_descriptors").GetArrayLength()));
        RequireUniqueIds(clean.GetProperty("passages"), "passage_id");
        RequireUniqueIds(clean.GetProperty("claims"), "claim_id");
        foreach (string property in new[] { "retained_reuse_deleted", "retained_reuse_unavailable" })
        {
            JsonElement reuse = oracle.GetProperty(property);
            JsonElement gaps = reuse.GetProperty("gaps");
            JsonElement receipts = reuse.GetProperty("deletion_receipts");
            if (reuse.GetProperty("deletion_receipt_count").GetInt32() != receipts.GetArrayLength()
                || !SequenceEquals(
                    reuse.GetProperty("gap_ids"),
                    gaps.EnumerateArray().Select(item => RequireString(item, "gap_id")))
                || !SequenceEquals(
                    reuse.GetProperty("gap_kinds"),
                    gaps.EnumerateArray().Select(item => RequireString(item, "kind"))))
            {
                throw new InvalidDataException(
                    $"Documentation fixture oracle reuse indexes drifted in '{property}'.");
            }
            RequireUniqueIds(gaps, "gap_id");
            RequireUniqueIds(receipts, "receipt_id");
        }
    }

    private static void ValidateExactCounts(
        JsonElement counts,
        params (string Property, int Expected)[] expectedCounts)
    {
        if (expectedCounts.Any(item => counts.GetProperty(item.Property).GetInt32() != item.Expected))
        {
            throw new InvalidDataException(
                "Documentation fixture oracle expected counts drifted from its typed objects.");
        }
    }

    private static void RequireUniqueIds(JsonElement array, string property)
    {
        HashSet<string> identities = new(StringComparer.Ordinal);
        if (array.EnumerateArray().Any(item => !identities.Add(RequireString(item, property))))
        {
            throw new InvalidDataException(
                $"Documentation fixture oracle contains duplicate '{property}' identities.");
        }
    }

    private static bool SequenceEquals(JsonElement array, IEnumerable<string> expected) =>
        array.EnumerateArray().Select(item => item.GetString())
            .SequenceEqual(expected, StringComparer.Ordinal);

    private static void ValidateExecutionAndAggregateBindings(JsonElement caseMatrix, JsonElement oracle)
    {
        JsonElement aggregate = caseMatrix.GetProperty("aggregate_output_binding");
        if (oracle.TryGetProperty("applications", out _))
        {
            JsonElement execution = caseMatrix.GetProperty("execution_binding");
            JsonElement expectedImport = oracle.GetProperty("import");
            ValidateExactFields(
                execution,
                expectedImport,
                ("import_run_id", "import_run_id"),
                ("mode", "mode"),
                ("dependency_closure_id", "dependency_closure_id"),
                ("extractor_id", "extractor_id"),
                ("imported_at", "created_at"));
            if (!StringComparer.Ordinal.Equals(
                    RequireString(execution, "originating_run_id"),
                    RequireString(expectedImport, "import_run_id"))
                || !AllStringPropertiesEqual(
                    oracle.GetProperty("passages"),
                    "state",
                    RequireString(aggregate, "passage_state"))
                || !StringComparer.Ordinal.Equals(
                    RequireString(aggregate, "llm_involvement"),
                    RequireString(expectedImport, "llm_involvement"))
                || !StringComparer.Ordinal.Equals(
                    RequireString(aggregate, "llm_operation"),
                    RequireString(expectedImport, "llm_operation")))
            {
                throw new InvalidDataException(
                    "Documentation fixture core execution or aggregate binding drifted from its oracle.");
            }
            return;
        }

        JsonElement cleanExecution = caseMatrix.GetProperty("clean_execution_binding");
        JsonElement clean = oracle.GetProperty("clean");
        JsonElement cleanTarget = clean.GetProperty("application_target");
        if (!StringComparer.Ordinal.Equals(
                RequireString(cleanExecution, "originating_run_id"),
                RequireString(cleanTarget, "consuming_run_id"))
            || !StringComparer.Ordinal.Equals(
                RequireString(cleanExecution, "import_run_id"),
                RequireString(cleanTarget, "consuming_run_id"))
            || !StringComparer.Ordinal.Equals(
                RequireString(cleanExecution, "dependency_closure_id"),
                RequireString(cleanTarget, "dependency_closure_id"))
            || !StringComparer.Ordinal.Equals(
                RequireString(cleanExecution, "imported_at"),
                RequireString(clean, "created_at"))
            || !AllStringPropertiesEqual(
                clean.GetProperty("passages"),
                "state",
                RequireString(aggregate, "clean_passage_state"))
            || !AllStringPropertiesEqual(
                clean.GetProperty("passages"),
                "state",
                RequireString(aggregate, "deleted_reuse_passage_state"))
            || !AllStringPropertiesEqual(
                clean.GetProperty("passages"),
                "state",
                RequireString(aggregate, "unavailable_live_source_reuse_passage_state"))
            || !StringComparer.Ordinal.Equals(
                RequireString(aggregate, "llm_involvement"),
                RequireString(clean, "llm_involvement"))
            || !StringComparer.Ordinal.Equals(
                RequireString(aggregate, "llm_operation"),
                RequireString(clean, "llm_operation")))
        {
            throw new InvalidDataException(
                "Documentation fixture adversarial clean execution or aggregate binding drifted from its oracle.");
        }

        JsonElement deleted = oracle.GetProperty("retained_reuse_deleted");
        JsonElement unavailable = oracle.GetProperty("retained_reuse_unavailable");
        if (!StringComparer.Ordinal.Equals(
                RequireString(aggregate, "retained_reuse_revision_retention_state"),
                RequireString(deleted, "retention_state"))
            || !StringComparer.Ordinal.Equals(
                RequireString(aggregate, "retained_reuse_revision_retention_state"),
                RequireString(unavailable, "retention_state"))
            || !StringComparer.Ordinal.Equals(
                RequireString(aggregate, "retained_reuse_revision_replay_state"),
                RequireString(deleted, "revision_replay_state"))
            || !StringComparer.Ordinal.Equals(
                RequireString(aggregate, "retained_reuse_revision_replay_state"),
                RequireString(unavailable, "revision_replay_state")))
        {
            throw new InvalidDataException(
                "Documentation fixture retained-reuse aggregate binding drifted from its oracle.");
        }

        ValidateReuseCaseBinding(caseMatrix, "retained-reuse-deleted", deleted, requiresReceipt: true);
        ValidateReuseCaseBinding(caseMatrix, "retained-reuse-unavailable", unavailable, requiresReceipt: false);
    }

    private static void ValidateReuseCaseBinding(
        JsonElement caseMatrix,
        string caseId,
        JsonElement expected,
        bool requiresReceipt)
    {
        JsonElement[] matchingCases = caseMatrix.GetProperty("cases").EnumerateArray()
            .Where(item => StringComparer.Ordinal.Equals(RequireString(item, "case_id"), caseId))
            .ToArray();
        if (matchingCases.Length != 1)
        {
            throw new InvalidDataException(
                $"Documentation fixture must contain exactly one '{caseId}' case.");
        }
        JsonElement matrixCase = matchingCases[0];
        string originatingRunId = RequireString(matrixCase, "originating_run_id");
        if (!StringComparer.Ordinal.Equals(originatingRunId, RequireString(matrixCase, "import_run_id"))
            || !StringComparer.Ordinal.Equals(
                RequireString(matrixCase, "imported_at"),
                RequireString(expected, "created_at"))
            || expected.GetProperty("gaps").EnumerateArray().Any(gap => !StringComparer.Ordinal.Equals(
                RequireString(gap, "originating_run_id"),
                originatingRunId)))
        {
            throw new InvalidDataException(
                $"Documentation fixture reuse case '{caseId}' drifted from its oracle execution identity.");
        }

        JsonElement receipts = expected.GetProperty("deletion_receipts");
        if (!requiresReceipt)
        {
            if (receipts.GetArrayLength() != 0)
            {
                throw new InvalidDataException(
                    "Documentation fixture unavailable-source reuse cannot carry a deletion receipt.");
            }
            return;
        }

        if (receipts.GetArrayLength() != 1)
        {
            throw new InvalidDataException(
                "Documentation fixture deleted-source reuse must carry exactly one deletion receipt.");
        }
        JsonElement receipt = receipts.EnumerateArray().Single();
        if (!StringComparer.Ordinal.Equals(
                RequireString(receipt, "originating_run_id"),
                originatingRunId)
            || !StringComparer.Ordinal.Equals(
                RequireString(receipt, "deleted_at"),
                RequireString(matrixCase, "deleted_at"))
            || !StringComparer.Ordinal.Equals(
                RequireString(receipt, "reason"),
                RequireString(matrixCase, "deletion_reason")))
        {
            throw new InvalidDataException(
                "Documentation fixture deletion receipt drifted from its case-matrix authority.");
        }
    }

    private static void ValidateExactFields(
        JsonElement left,
        JsonElement right,
        params (string Left, string Right)[] fields)
    {
        if (fields.Any(field => !StringComparer.Ordinal.Equals(
                RequireString(left, field.Left),
                RequireString(right, field.Right))))
        {
            throw new InvalidDataException("Documentation fixture structured execution binding drifted.");
        }
    }

    private static bool AllStringPropertiesEqual(
        JsonElement array,
        string property,
        string expected) => array.EnumerateArray().All(item => StringComparer.Ordinal.Equals(
            RequireString(item, property),
            expected));

    private static void ValidateOracleBindings(JsonElement caseMatrix, JsonElement oracle)
    {
        JsonElement expectedBoundaries;
        if (oracle.TryGetProperty("applications", out JsonElement oracleApplications))
        {
            Dictionary<string, JsonElement> expectedTargets = caseMatrix
                .GetProperty("application_targets")
                .EnumerateArray()
                .ToDictionary(item => RequireString(item, "application_claim_key"), StringComparer.Ordinal);
            foreach (JsonElement application in oracleApplications.EnumerateArray())
            {
                string claimKey = RequireString(application, "claim_key");
                if (!expectedTargets.Remove(claimKey, out JsonElement target)
                    || !ApplicationTargetMatches(target, application))
                {
                    throw new InvalidDataException(
                        "Documentation fixture oracle application targets drifted from the case matrix.");
                }
            }
            if (expectedTargets.Count != 0)
            {
                throw new InvalidDataException(
                    "Documentation fixture oracle application target closure is incomplete.");
            }
            expectedBoundaries = oracle.GetProperty("import").GetProperty("boundaries");
            if (!StringComparer.Ordinal.Equals(
                    RequireString(oracle.GetProperty("revision"), "replay_state"),
                    "complete-clean"))
            {
                throw new InvalidDataException("Documentation fixture core oracle replay state drifted.");
            }
        }
        else
        {
            JsonElement caseTargets = caseMatrix.GetProperty("application_targets");
            JsonElement clean = oracle.GetProperty("clean");
            if (caseTargets.GetArrayLength() != 1
                || !ApplicationTargetMatches(
                    caseTargets.EnumerateArray().Single(),
                    clean.GetProperty("application_target")))
            {
                throw new InvalidDataException(
                    "Documentation fixture adversarial oracle target drifted from the case matrix.");
            }
            expectedBoundaries = clean.GetProperty("boundaries");
            if (!StringComparer.Ordinal.Equals(RequireString(clean, "replay_state"), "complete-clean"))
            {
                throw new InvalidDataException("Documentation fixture adversarial oracle replay state drifted.");
            }
        }

        if (!JsonElement.DeepEquals(
                caseMatrix.GetProperty("aggregate_output_binding").GetProperty("boundaries"),
                expectedBoundaries))
        {
            throw new InvalidDataException(
                "Documentation fixture oracle external-boundary declarations drifted from the case matrix.");
        }
    }

    private static bool ApplicationTargetMatches(JsonElement caseTarget, JsonElement oracleTarget)
    {
        string[] fields =
        [
            "consuming_run_id",
            "installation_snapshot_id",
            "analysis_context_id",
            "resolved_input_manifest_id",
            "subject_id",
            "subject_type",
            "dependency_closure_id",
        ];
        return fields.All(field => StringComparer.Ordinal.Equals(
            RequireString(caseTarget, field),
            RequireString(oracleTarget, field)));
    }

    private static void ValidatePartitionHistory(
        JsonElement publicManifest,
        JsonElement partitionHistoryDocument,
        FixturePartition partition)
    {
        JsonElement publicHistory = publicManifest.GetProperty("partition_history");
        JsonElement separateHistory = partitionHistoryDocument.GetProperty("partition_history");
        if (!JsonElement.DeepEquals(publicHistory, separateHistory))
        {
            throw new InvalidDataException("Documentation fixture partition histories do not match.");
        }

        string? priorPartition = null;
        UtcTimestamp? priorAt = null;
        Sha256Fingerprint currentInputFingerprint = new(
            RequireString(publicManifest, "input_package_fingerprint"));
        Sha256Fingerprint currentOracleFingerprint = new(
            RequireString(publicManifest, "oracle_fingerprint"));
        int index = 0;
        foreach (JsonElement transition in separateHistory.EnumerateArray())
        {
            JsonElement from = transition.GetProperty("from");
            if ((index == 0 && from.ValueKind != JsonValueKind.Null)
                || (index > 0 && (from.ValueKind != JsonValueKind.String
                    || !StringComparer.Ordinal.Equals(from.GetString(), priorPartition))))
            {
                throw new InvalidDataException("Documentation fixture partition history is not contiguous from its initial assignment.");
            }

            if (index == 0 && transition.GetProperty("change_influenced_implementation").GetBoolean())
            {
                throw new InvalidDataException(
                    "Documentation fixture initial partition assignment cannot claim implementation influence.");
            }

            string next = RequireString(transition, "to");
            _ = ParsePartition(next);
            UtcTimestamp at = UtcTimestamp.Parse(RequireString(transition, "at"));
            if (priorAt is not null && at.Value <= priorAt.Value)
            {
                throw new InvalidDataException("Documentation fixture partition-history timestamps must increase.");
            }

            if (index > 0)
            {
                if (StringComparer.Ordinal.Equals(priorPartition, next)
                    || next != "development"
                    || priorPartition is not ("validation" or "held-out")
                    || !transition.GetProperty("change_influenced_implementation").GetBoolean())
                {
                    throw new InvalidDataException("Documentation fixture partition transition is forbidden.");
                }

                OpaqueId replacementId = new(RequireString(transition, "replacement_fixture_id"));
                if (replacementId == new OpaqueId(RequireString(publicManifest, "fixture_id")))
                {
                    throw new InvalidDataException("Documentation fixture replacement identity must be materially independent.");
                }
                Sha256Fingerprint replacementInputFingerprint = new(
                    RequireString(transition, "replacement_input_package_fingerprint"));
                Sha256Fingerprint replacementOracleFingerprint = new(
                    RequireString(transition, "replacement_oracle_fingerprint"));
                if (replacementInputFingerprint == currentInputFingerprint
                    || replacementOracleFingerprint == currentOracleFingerprint)
                {
                    throw new InvalidDataException(
                        "Documentation fixture replacement input and oracle must be materially independent.");
                }
                _ = RequireString(transition, "replacement_partition");
                _ = transition.GetProperty("independence_evidence_reference");
                _ = RequireString(transition, "authorized_by");
            }

            priorPartition = next;
            priorAt = at;
            index++;
        }

        if (index == 0 || ParsePartition(priorPartition!) != partition)
        {
            throw new InvalidDataException("Documentation fixture manifest partition does not match its final history entry.");
        }
    }

    private static FixturePartition ParsePartition(string value) => value switch
    {
        "development" => FixturePartition.Development,
        "validation" => FixturePartition.Validation,
        _ => throw new InvalidDataException("documentation stage documentation fixture partition must be development or validation."),
    };

    private static void ValidateRedistribution(JsonElement manifest, JsonElement redistribution)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireString(manifest, "redistribution_class"),
                RequireString(redistribution, "redistribution_class")))
        {
            throw new InvalidDataException("Documentation fixture redistribution identity drifted.");
        }
    }

    private static void ValidateIdentity(
        JsonElement document,
        OpaqueId fixtureId,
        ContractVersion fixtureVersion,
        string description)
    {
        JsonElement root = RequireObject(document, description);
        if (!StringComparer.Ordinal.Equals(RequireString(root, "fixture_id"), fixtureId.Value)
            || !StringComparer.Ordinal.Equals(RequireString(root, "fixture_version"), fixtureVersion.ToString()))
        {
            throw new InvalidDataException($"Documentation fixture identity drifted in {description}.");
        }
    }

    private static void ValidateFingerprint(JsonElement document, string property, string actual)
    {
        if (!StringComparer.Ordinal.Equals(RequireString(document, property), actual))
        {
            throw new InvalidDataException($"Documentation fixture fingerprint mismatch at {property}.");
        }
    }

    private static BoundedJsonDocumentSnapshot ReadJson(string directory, string fileName) =>
        BoundedJsonDocumentReader.Read(Path.Combine(directory, fileName), MaximumJsonBytes, MaximumDepth);

    private static byte[] ReadBoundedSource(string path)
        => ReadBoundedFile(path, 8 * 1024 * 1024, "Documentation fixture source");

    private static byte[] ReadBoundedFile(string path, long maximumBytes, string description)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes)
        {
            throw new InvalidDataException($"{description} exceeds its byte bound.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string RequireSafePackagePath(string relative, string? allowedPrefix)
    {
        if ((allowedPrefix is not null && !relative.StartsWith(allowedPrefix, StringComparison.Ordinal))
            || Path.IsPathRooted(relative)
            || relative.Contains('\\')
            || relative.Contains(':', StringComparison.Ordinal)
            || relative.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new InvalidDataException("Documentation fixture artifact path is not a safe normalized in-package path.");
        }
        return relative;
    }

    private static string RequiredPackageFile(string root, string relative)
    {
        string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string expectedPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new InvalidDataException("Documentation fixture artifact is missing or escapes its package.");
        }
        return fullPath;
    }

    private static void EnsureExactFiles(string directory, IEnumerable<string> expectedNames)
    {
        HashSet<string> expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            throw new InvalidDataException($"Documentation fixture file closure drifted under '{directory}'.");
        }
    }

    private static void EnsureExactDirectories(string directory, IEnumerable<string> expectedNames)
    {
        HashSet<string> expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = Directory.EnumerateDirectories(directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            throw new InvalidDataException($"Documentation fixture directory closure drifted under '{directory}'.");
        }
    }

    private static void EnsureNoReparsePoints(string root)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count != 0)
        {
            string directory = pending.Pop();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Documentation fixture packages must not contain reparse points.");
            }
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Documentation fixture packages must not contain reparse points.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
            }
        }
    }

    private static string RequiredDirectory(string root, string name)
    {
        string path = Path.Combine(root, name);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Required documentation fixture directory is missing: {name}");
        }
        return path;
    }

    private static JsonElement RequireObject(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{description} must contain a JSON object.");
        }
        return value;
    }

    private static string RequireString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement item)
            || item.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(item.GetString()))
        {
            throw new InvalidDataException($"Documentation fixture property '{property}' must be a non-empty string.");
        }
        return item.GetString()!;
    }

    private static ReadOnlySpan<byte> GetRawTextBytes(this JsonElement value) =>
        Encoding.UTF8.GetBytes(value.GetRawText());
}
