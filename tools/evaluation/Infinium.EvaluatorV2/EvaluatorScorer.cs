using System.Text;
using System.Text.Json;

namespace Infinium.EvaluatorV2;

internal static class EvaluatorScorer
{
    private static readonly string[] RequiredEvaluatorFiles =
    [
        "Infinium.Application.dll",
        "Infinium.EvaluatorV2.dll",
        "protocol/assertion-results.v1.schema.json",
        "protocol/calibration-results.v1.schema.json",
        "protocol/candidate-semantic-output.v1.schema.json",
        "protocol/evaluator-v2-common.v1.schema.json",
        "protocol/execution-manifest.v1.schema.json",
        "protocol/expected-semantic-output.v1.schema.json",
        "protocol/protocol.json",
        "protocol/sanitized-result.v1.schema.json",
    ];

    internal static ScoreOutcome Score(string manifestPath, string oraclePath)
    {
        ExecutionManifest? manifest = null;
        try
        {
            manifest = EvaluatorProtocol.Read<ExecutionManifest>(manifestPath, "execution-manifest.v1.schema.json");
            ValidateManifestIdentity(manifest);
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(manifest, "manifest");
        }

        ExpectedSemanticOutput oracle;
        try
        {
            oracle = EvaluatorProtocol.Read<ExpectedSemanticOutput>(oraclePath, "expected-semantic-output.v1.schema.json");
            ValidateOracleIdentity(manifest, oracle, oraclePath);
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(manifest, "oracle");
        }

        CandidateSemanticOutput candidate;
        try
        {
            candidate = ReflectionCandidateAdapter.Execute(manifest);
        }
        catch (CandidateOutputException)
        {
            return ProductFailure(manifest, "candidate_output", "candidate_schema");
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(manifest, "candidate_execution");
        }

        try
        {
            ValidateCandidateIdentity(manifest, candidate);
            return Compare(manifest, oracle, candidate);
        }
        catch (CandidateOutputException)
        {
            return ProductFailure(manifest, "candidate_output", "candidate_schema");
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(manifest, "comparison");
        }
    }

    internal static ScoreOutcome Compare(
        ExecutionManifest manifest,
        ExpectedSemanticOutput oracle,
        CandidateSemanticOutput candidate)
    {
        List<TypedAssertion> assertions = [];
        AddAssertion(
            assertions,
            "state",
            "state",
            "state",
            null,
            EvaluatorProtocol.Primitive(oracle.State),
            EvaluatorProtocol.Primitive(candidate.State),
            string.Equals(oracle.State, candidate.State, StringComparison.Ordinal));

        Dictionary<string, SemanticFact> expected = UniqueFacts(oracle.Facts, "oracle");
        Dictionary<string, SemanticFact> actual = UniqueFacts(candidate.Facts, "candidate");
        foreach (string factId in expected.Keys.Union(actual.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            bool hasExpected = expected.TryGetValue(factId, out SemanticFact? expectedFact);
            bool hasActual = actual.TryGetValue(factId, out SemanticFact? actualFact);
            if (!hasActual)
            {
                AddAssertion(
                    assertions,
                    $"missing:{factId}",
                    "missing",
                    expectedFact!.FactType,
                    factId,
                    expectedFact.Value,
                    null,
                    false);
                continue;
            }

            if (!hasExpected)
            {
                AddAssertion(
                    assertions,
                    $"extra:{factId}",
                    "extra",
                    actualFact!.FactType,
                    factId,
                    null,
                    actualFact.Value,
                    false);
                continue;
            }

            bool equal = string.Equals(expectedFact!.FactType, actualFact!.FactType, StringComparison.Ordinal)
                && string.Equals(expectedFact.ValueType, actualFact.ValueType, StringComparison.Ordinal)
                && JsonElement.DeepEquals(expectedFact.Value, actualFact.Value);
            AddAssertion(
                assertions,
                $"value:{factId}",
                "value",
                expectedFact.FactType,
                factId,
                expectedFact.Value,
                actualFact.Value,
                equal);
        }

        int passed = assertions.Count(item => item.Outcome == "PASS");
        int failed = assertions.Count - passed;
        string terminal = failed == 0 ? "PASS" : "FAIL";
        string[] categories = assertions
            .Where(item => item.Outcome == "FAIL")
            .Select(FailureCategory)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        AssertionResults raw = new(EvaluatorProtocol.AssertionsSchema, EvaluatorProtocol.ProtocolId, assertions);
        SanitizedResult result = Result(
            manifest,
            terminal,
            failed == 0 ? null : "comparison",
            new AssertionCounts(assertions.Count, passed, failed),
            categories);
        return new ScoreOutcome(result, raw);
    }

    internal static void WriteResults(string resultDirectory, ScoreOutcome outcome)
    {
        string root = ConfinedResultRoot(resultDirectory);
        string[] outputs = outcome.Assertions is null
            ? ["sanitized-result.json"]
            : ["assertions.json", "sanitized-result.json"];
        if (outputs.Any(fileName => File.Exists(Path.Combine(root, fileName))))
        {
            throw new IOException("The result directory already contains an evaluator output file.");
        }

        if (outcome.Assertions is not null)
        {
            WriteNew(root, "assertions.json", EvaluatorProtocol.Serialize(outcome.Assertions));
        }

        WriteNew(root, "sanitized-result.json", EvaluatorProtocol.Serialize(outcome.Result));
    }

    internal static ExecutionManifest ReadAndValidateManifest(string manifestPath)
    {
        ExecutionManifest manifest = EvaluatorProtocol.Read<ExecutionManifest>(
            manifestPath,
            "execution-manifest.v1.schema.json");
        ValidateManifestIdentity(manifest);
        return manifest;
    }

    internal static string ConfinedResultRoot(string resultDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultDirectory);
        string root = Path.GetFullPath(resultDirectory);
        Directory.CreateDirectory(root);
        DirectoryInfo info = new(root);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
        {
            throw new InvalidDataException("The result directory cannot be a symbolic link or reparse point.");
        }

        return root;
    }

    internal static void WriteNew(string root, string fileName, string content)
    {
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || fileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new InvalidDataException("Evaluator output names must be single file names.");
        }

        string path = Path.GetFullPath(Path.Combine(root, fileName));
        string relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("Evaluator output escaped the designated result directory.");
        }

        byte[] bytes = new UTF8Encoding(false).GetBytes(content);
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }

    private static void ValidateManifestIdentity(ExecutionManifest manifest)
    {
        if (manifest.SchemaId != EvaluatorProtocol.ManifestSchema
            || manifest.ProtocolId != EvaluatorProtocol.ProtocolId
            || manifest.Evaluator.ProtocolId != EvaluatorProtocol.ProtocolId
            || manifest.Evaluator.ScorerId != EvaluatorProtocol.ScorerId
            || manifest.Evaluator.ScorerVersion != EvaluatorProtocol.ScorerVersion
            || manifest.Evaluator.AdapterId != EvaluatorProtocol.AdapterId
            || manifest.Evaluator.AdapterVersion != EvaluatorProtocol.AdapterVersion)
        {
            throw new InvalidDataException("The execution manifest identifies a different evaluator protocol.");
        }

        ArtifactIdentity candidate = EvaluatorProtocol.Identity(manifest.Candidate.AssemblyPath);
        RequireIdentity("candidate", manifest.Candidate.Artifact, candidate);
        string evaluatorRoot = Path.GetFullPath(manifest.Evaluator.Root);
        HashSet<string> declaredEvaluatorFiles = new(StringComparer.Ordinal);
        foreach (EvaluatorFileIdentity file in manifest.Evaluator.Files)
        {
            if (Path.IsPathRooted(file.RelativePath))
            {
                throw new InvalidDataException("Evaluator file identities must use relative paths.");
            }

            string path = Path.GetFullPath(Path.Combine(evaluatorRoot, file.RelativePath));
            string relative = Path.GetRelativePath(evaluatorRoot, path);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                throw new InvalidDataException("An evaluator file identity escapes its declared root.");
            }

            string canonicalRelative = relative.Replace('\\', '/');
            if (!string.Equals(canonicalRelative, file.RelativePath.Replace('\\', '/'), StringComparison.Ordinal)
                || !declaredEvaluatorFiles.Add(canonicalRelative))
            {
                throw new InvalidDataException("Evaluator file identities must be unique canonical relative paths.");
            }

            RequireIdentity(file.RelativePath, new ArtifactIdentity(file.ByteLength, file.Sha256), EvaluatorProtocol.Identity(path));
        }


        if (RequiredEvaluatorFiles.Except(declaredEvaluatorFiles, StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException("The evaluator identity omits a required executable or protocol file.");
        }

        HashSet<string> pluginNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int> loadOrders = [];
        foreach (PluginExecutionInput plugin in manifest.Execution.Plugins)
        {
            if (!pluginNames.Add(plugin.PluginName) || !loadOrders.Add(plugin.LoadOrder))
            {
                throw new InvalidDataException("Plugin names and load-order positions must be unique.");
            }

            RequireIdentity(
                plugin.PluginName,
                new ArtifactIdentity(plugin.ByteLength, plugin.Sha256),
                EvaluatorProtocol.Identity(plugin.Path));
        }
    }

    private static void ValidateOracleIdentity(
        ExecutionManifest manifest,
        ExpectedSemanticOutput oracle,
        string oraclePath)
    {
        if (oracle.SchemaId != EvaluatorProtocol.ExpectedSchema
            || oracle.ProtocolId != EvaluatorProtocol.ProtocolId
            || oracle.CorpusId != manifest.Corpus.CorpusId
            || oracle.CorpusVersion != manifest.Corpus.Version
            || oracle.CorpusSha256 != manifest.Corpus.Sha256)
        {
            throw new InvalidDataException("The oracle identity does not match the answer-free manifest.");
        }

        _ = UniqueFacts(oracle.Facts, "oracle");
    }

    private static void ValidateCandidateIdentity(ExecutionManifest manifest, CandidateSemanticOutput candidate)
    {
        if (candidate.SchemaId != EvaluatorProtocol.CandidateSchema
            || candidate.ProtocolId != EvaluatorProtocol.ProtocolId
            || candidate.CandidateCommit != manifest.Candidate.Commit
            || candidate.CandidateArtifact != manifest.Candidate.Artifact)
        {
            throw new CandidateOutputException("Candidate output identity does not match the manifest.");
        }

        _ = UniqueFacts(candidate.Facts, "candidate");
    }

    private static Dictionary<string, SemanticFact> UniqueFacts(IReadOnlyList<SemanticFact> facts, string source)
    {
        Dictionary<string, SemanticFact> result = new(StringComparer.Ordinal);
        string? previous = null;
        foreach (SemanticFact fact in facts)
        {
            string actualValueType = fact.Value.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number when fact.Value.TryGetInt64(out _) => "integer",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                _ => throw (source == "candidate"
                    ? new CandidateOutputException("Candidate fact values must be JSON primitives.")
                    : new InvalidDataException("Oracle fact values must be JSON primitives.")),
            };
            if (!string.Equals(actualValueType, fact.ValueType, StringComparison.Ordinal))
            {
                throw source == "candidate"
                    ? new CandidateOutputException("Candidate fact value_type does not match its JSON value.")
                    : new InvalidDataException("Oracle fact value_type does not match its JSON value.");
            }

            if (previous is not null && string.CompareOrdinal(previous, fact.FactId) >= 0)
            {
                throw source == "candidate"
                    ? new CandidateOutputException("Candidate facts are not unique and ordinally sorted.")
                    : new InvalidDataException("Oracle facts are not unique and ordinally sorted.");
            }

            if (!result.TryAdd(fact.FactId, fact))
            {
                throw source == "candidate"
                    ? new CandidateOutputException("Candidate facts contain duplicate identities.")
                    : new InvalidDataException("Oracle facts contain duplicate identities.");
            }

            previous = fact.FactId;
        }

        return result;
    }

    private static void RequireIdentity(string label, ArtifactIdentity expected, ArtifactIdentity actual)
    {
        if (expected != actual)
        {
            throw new InvalidDataException($"The retained identity for '{label}' does not match its bytes.");
        }
    }

    private static void AddAssertion(
        List<TypedAssertion> assertions,
        string id,
        string kind,
        string factType,
        string? factId,
        JsonElement? expected,
        JsonElement? actual,
        bool passed)
    {
        assertions.Add(new TypedAssertion(
            id,
            kind,
            passed ? "PASS" : "FAIL",
            factType,
            factId,
            expected,
            actual));
    }

    private static string FailureCategory(TypedAssertion assertion) => assertion.Kind switch
    {
        "missing" => "missing_fact",
        "extra" => "extra_fact",
        "candidate_schema" => "candidate_schema",
        "state" => "state",
        _ => assertion.FactType,
    };

    private static ScoreOutcome ProductFailure(ExecutionManifest manifest, string stage, string category)
    {
        TypedAssertion assertion = new("candidate_schema", "candidate_schema", "FAIL", category);
        AssertionResults assertions = new(EvaluatorProtocol.AssertionsSchema, EvaluatorProtocol.ProtocolId, [assertion]);
        return new ScoreOutcome(
            Result(manifest, "FAIL", stage, new AssertionCounts(1, 0, 1), [category]),
            assertions);
    }

    private static ScoreOutcome Error(ExecutionManifest? manifest, string stage)
    {
        string zeroCommit = new('0', 40);
        string zeroHash = new('0', 64);
        SanitizedResult result = new(
            EvaluatorProtocol.SanitizedSchema,
            EvaluatorProtocol.ProtocolId,
            manifest?.Candidate.Commit ?? zeroCommit,
            manifest?.Candidate.Artifact.ByteLength ?? 1,
            manifest?.Candidate.Artifact.Sha256 ?? zeroHash,
            manifest?.Evaluator.Commit ?? zeroCommit,
            manifest is null ? zeroHash : EvaluatorFilesFingerprint(manifest.Evaluator.Files),
            EvaluatorProtocol.ScorerId,
            EvaluatorProtocol.ScorerVersion,
            EvaluatorProtocol.AdapterId,
            EvaluatorProtocol.AdapterVersion,
            manifest?.Corpus.CorpusId ?? "unavailable",
            manifest?.Corpus.Version ?? "unavailable",
            manifest?.Corpus.Sha256 ?? zeroHash,
            "EVALUATOR_ERROR",
            stage,
            new AssertionCounts(0, 0, 0),
            [stage],
            "clean");
        return new ScoreOutcome(result, null);
    }

    private static SanitizedResult Result(
        ExecutionManifest manifest,
        string terminal,
        string? stage,
        AssertionCounts counts,
        IReadOnlyList<string> categories) => new(
            EvaluatorProtocol.SanitizedSchema,
            EvaluatorProtocol.ProtocolId,
            manifest.Candidate.Commit,
            manifest.Candidate.Artifact.ByteLength,
            manifest.Candidate.Artifact.Sha256,
            manifest.Evaluator.Commit,
            EvaluatorFilesFingerprint(manifest.Evaluator.Files),
            EvaluatorProtocol.ScorerId,
            EvaluatorProtocol.ScorerVersion,
            EvaluatorProtocol.AdapterId,
            EvaluatorProtocol.AdapterVersion,
            manifest.Corpus.CorpusId,
            manifest.Corpus.Version,
            manifest.Corpus.Sha256,
            terminal,
            stage,
            counts,
            categories,
            "clean");

    private static string EvaluatorFilesFingerprint(IReadOnlyList<EvaluatorFileIdentity> files)
    {
        string material = string.Join(
            '\n',
            files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Select(file => $"{file.RelativePath.Replace('\\', '/')}|{file.ByteLength}|{file.Sha256}"));
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static bool IsEvaluatorInputFailure(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        InvalidDataException or
        ArgumentException or
        System.Security.SecurityException or
        System.Reflection.TargetInvocationException or
        TypeLoadException;
}

internal sealed class CandidateOutputException(string message, Exception? innerException = null)
    : Exception(message, innerException);
