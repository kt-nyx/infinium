using System.Text;
using System.Text.Json;

namespace Infinium.EvaluatorV2;

internal static class EvaluatorScorer
{
    internal static readonly string[] RequiredEvaluatorFiles =
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
        return Score(manifestPath, oraclePath, ReflectionCandidateAdapter.Execute);
    }

    internal static ScoreOutcome Score(
        string manifestPath,
        string oraclePath,
        Func<ExecutionManifest, CandidateSemanticOutput> adapter)
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
            candidate = adapter(manifest);
        }
        catch (CandidateOutputException)
        {
            return Error(manifest, "candidate_output");
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
            return Error(manifest, "candidate_output");
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
        string sanitized = ValidatedJson(outcome.Result, "sanitized-result.v1.schema.json");
        string? assertions = outcome.Assertions is null
            ? null
            : ValidatedJson(outcome.Assertions, "assertion-results.v1.schema.json");
        WriteAtomically(
            resultDirectory,
            assertions is null
                ? [("sanitized-result.json", sanitized)]
                : [("sanitized-result.json", sanitized), ("assertions.json", assertions)]);
    }

    internal static ExecutionManifest ReadAndValidateManifest(string manifestPath)
    {
        ExecutionManifest manifest = EvaluatorProtocol.Read<ExecutionManifest>(
            manifestPath,
            "execution-manifest.v1.schema.json");
        ValidateManifestIdentity(manifest);
        return manifest;
    }

    internal static void WriteSingleResult<T>(
        string resultDirectory,
        string fileName,
        T value,
        string schemaFileName)
    {
        WriteAtomically(resultDirectory, [(fileName, ValidatedJson(value, schemaFileName))]);
    }

    private static string ValidatedJson<T>(T value, string schemaFileName)
    {
        string json = EvaluatorProtocol.Serialize(value);
        using Infinium.Application.Evaluation.BoundedJsonDocumentSnapshot snapshot =
            Infinium.Application.Evaluation.BoundedJsonDocumentReader.Parse(
                Encoding.UTF8.GetBytes(json),
                schemaFileName,
                96);
        Infinium.Application.Evaluation.EmbeddedJsonSchemaValidator.Validate(
            snapshot.Document.RootElement,
            schemaFileName);
        return json;
    }

    private static void WriteAtomically(
        string resultDirectory,
        IReadOnlyList<(string FileName, string Content)> outputs)
    {
        ResultDirectoryAuthority? authority = null;
        List<(string Temporary, string Final)> paths = [];
        try
        {
            authority = ResultDirectoryAuthority.Create(resultDirectory);
            foreach ((string fileName, string content) in outputs)
            {
                string temporary = $".{fileName}.{Guid.NewGuid():N}.tmp";
                using FileStream stream = authority.OpenNew(temporary);
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                stream.Write(bytes);
                stream.Flush(true);
                paths.Add((Path.Combine(authority.Root, temporary), Path.Combine(authority.Root, fileName)));
            }

            foreach ((string temporary, string final) in paths)
            {
                File.Move(temporary, final);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            foreach ((string temporary, string final) in paths)
            {
                ResultDirectoryAuthority.TryDelete(temporary);
                ResultDirectoryAuthority.TryDelete(final);
            }

            if (authority is not null)
            {
                string root = authority.Root;
                authority.Dispose();
                authority = null;
                ResultDirectoryAuthority.TryDelete(root);
            }

            throw new ResultWriteException("Evaluator result publication failed at result_write.", exception);
        }
        finally
        {
            authority?.Dispose();
        }
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

        if (manifest.Corpus.QualificationState != "frozen"
            || manifest.Corpus.ContaminationState != "clean")
        {
            throw new InvalidDataException("Only a qualified, frozen, uncontaminated corpus may be scored.");
        }

        ArtifactIdentity candidate = EvaluatorProtocol.Identity(manifest.Candidate.AssemblyPath);
        RequireIdentity("candidate", manifest.Candidate.Artifact, candidate);
        HashSet<string> candidateFiles = ValidateFileInventory(
            manifest.Candidate.Root,
            manifest.Candidate.Files,
            "candidate");
        string candidateRelative = CanonicalRelativePath(
            manifest.Candidate.Root,
            manifest.Candidate.AssemblyPath);
        if (!candidateFiles.Contains(candidateRelative))
        {
            throw new InvalidDataException("The candidate assembly is absent from its dependency inventory.");
        }

        string evaluatorRoot = Path.GetFullPath(manifest.Evaluator.Root);
        HashSet<string> declaredEvaluatorFiles = ValidateFileInventory(
            evaluatorRoot,
            manifest.Evaluator.Files,
            "evaluator");

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
            || oracle.CorpusVersion != manifest.Corpus.Version)
        {
            throw new InvalidDataException("The oracle identity does not match the answer-free manifest.");
        }

        if (!string.Equals(CorpusFingerprint(manifest, oraclePath), manifest.Corpus.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The oracle and retained inputs do not match the frozen corpus identity.");
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

    internal static string CorpusFingerprint(ExecutionManifest manifest, string oraclePath)
    {
        ArtifactIdentity oracle = EvaluatorProtocol.Identity(oraclePath);
        StringBuilder material = new("infinium.evaluator-v2.corpus/1\n");
        material.Append(manifest.Corpus.CorpusId)
            .Append('|')
            .Append(manifest.Corpus.Version)
            .Append('\n');
        foreach (PluginExecutionInput plugin in manifest.Execution.Plugins.OrderBy(plugin => plugin.LoadOrder))
        {
            material.Append(plugin.LoadOrder)
                .Append('|')
                .Append(plugin.PluginName)
                .Append('|')
                .Append(plugin.LocalInstalledEntityId)
                .Append('|')
                .Append(plugin.ByteLength)
                .Append('|')
                .Append(plugin.Sha256)
                .Append('\n');
        }

        foreach (string capability in manifest.Execution.UnsupportedCapabilities.Order(StringComparer.Ordinal))
        {
            material.Append("unsupported|").Append(capability).Append('\n');
        }

        material.Append("oracle|")
            .Append(oracle.ByteLength)
            .Append('|')
            .Append(oracle.Sha256)
            .Append('\n');
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(material.ToString())));
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
            manifest?.Corpus.ContaminationState ?? "clean");
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
            manifest.Corpus.ContaminationState);

    private static HashSet<string> ValidateFileInventory(
        string rootPath,
        IReadOnlyList<EvaluatorFileIdentity> files,
        string label)
    {
        string root = Path.GetFullPath(rootPath);
        HashSet<string> declared = new(StringComparer.Ordinal);
        foreach (EvaluatorFileIdentity file in files)
        {
            if (Path.IsPathRooted(file.RelativePath))
            {
                throw new InvalidDataException($"{label} file identities must use relative paths.");
            }

            string path = Path.GetFullPath(Path.Combine(root, file.RelativePath));
            string relative = CanonicalRelativePath(root, path);
            if (!string.Equals(relative, file.RelativePath.Replace('\\', '/'), StringComparison.Ordinal)
                || !declared.Add(relative))
            {
                throw new InvalidDataException($"{label} file identities must be unique canonical relative paths.");
            }

            RequireIdentity(
                file.RelativePath,
                new ArtifactIdentity(file.ByteLength, file.Sha256),
                EvaluatorProtocol.Identity(path));
        }

        return declared;
    }

    private static string CanonicalRelativePath(string rootPath, string path)
    {
        string root = Path.GetFullPath(rootPath);
        string fullPath = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(root, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("A retained file identity escapes its declared root.");
        }

        return relative.Replace('\\', '/');
    }

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

internal sealed class ResultWriteException(string message, Exception? innerException = null)
    : Exception(message, innerException);
