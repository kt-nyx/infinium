using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Infinium.EvaluatorV2;

internal static class EvaluatorScorer
{
    private static readonly string[] RequiredPublicFiles =
    [
        "Infinium.EvaluatorV2.deps.json",
        "Infinium.EvaluatorV2.runtimeconfig.json",
        "protocol/assertion-results.v4.schema.json",
        "protocol/calibration-results.v4.schema.json",
        "protocol/candidate-semantic-output.v4.schema.json",
        "protocol/corpus-execution-manifest.v4.schema.json",
        "protocol/evaluator-v2-common.v4.schema.json",
        "protocol/execution-manifest.v4.schema.json",
        "protocol/expected-semantic-output.v4.schema.json",
        "protocol/prepared-comparison-manifest.v4.schema.json",
        "protocol/protocol.json",
        "protocol/sanitized-result.v4.schema.json",
    ];

    internal static readonly string[] RequiredEvaluatorFiles = BuildRequiredEvaluatorFiles();

    internal static ScoreOutcome Score(string manifestPath, string oraclePath) =>
        Score(manifestPath, oraclePath, ReflectionCandidateAdapter.Execute);

    internal static ScoreOutcome Score(
        string manifestPath,
        string oraclePath,
        Func<ExecutionManifest, CandidateSemanticOutput> adapter)
    {
        ExecutionManifest? manifest = null;
        try
        {
            manifest = EvaluatorProtocol.Read<ExecutionManifest>(manifestPath, "execution-manifest.v4.schema.json");
            ValidateManifestIdentity(manifest);
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(manifest is null ? null : Identity(manifest), "manifest", "manifest");
        }

        ExpectedSemanticOutput oracle;
        try
        {
            oracle = EvaluatorProtocol.Read<ExpectedSemanticOutput>(oraclePath, "expected-semantic-output.v4.schema.json");
            ValidateOracleIdentity(manifest, oracle, CorpusFingerprint(manifest, oraclePath));
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(Identity(manifest), "oracle", "oracle");
        }

        CandidateSemanticOutput candidate;
        try
        {
            candidate = adapter(manifest);
        }
        catch (CandidateExecutionException)
        {
            return ProductFailure(Identity(manifest), "candidate_execution");
        }
        catch (CandidateOutputException)
        {
            return ProductFailure(Identity(manifest), "candidate_output_contract");
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(Identity(manifest), "candidate_admission", "candidate_admission");
        }

        try
        {
            ValidateCandidateIdentity(Identity(manifest), candidate);
            return Compare(Identity(manifest), oracle, candidate);
        }
        catch (CandidateOutputException)
        {
            return ProductFailure(Identity(manifest), "candidate_output_contract");
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(Identity(manifest), "comparison", "comparison");
        }
    }

    internal static ScoreOutcome ComparePrepared(
        string manifestPath,
        string candidateOutputPath,
        string oraclePath)
    {
        PreparedComparisonManifest? manifest = null;
        try
        {
            manifest = EvaluatorProtocol.Read<PreparedComparisonManifest>(manifestPath, "prepared-comparison-manifest.v4.schema.json");
            ValidatePreparedManifest(manifest);
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(manifest is null ? null : Identity(manifest), "manifest", "manifest");
        }

        ExpectedSemanticOutput oracle;
        try
        {
            oracle = EvaluatorProtocol.Read<ExpectedSemanticOutput>(oraclePath, "expected-semantic-output.v4.schema.json");
            ValidateOracleIdentity(manifest, oracle, PreparedCorpusFingerprint(manifest, candidateOutputPath, oraclePath));
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            return Error(Identity(manifest), "oracle", "oracle");
        }

        CandidateSemanticOutput candidate;
        try
        {
            candidate = EvaluatorProtocol.Read<CandidateSemanticOutput>(candidateOutputPath, "candidate-semantic-output.v4.schema.json");
            ValidateCandidateIdentity(Identity(manifest), candidate);
        }
        catch (Exception exception) when (exception is CandidateOutputException || IsEvaluatorInputFailure(exception))
        {
            return ProductFailure(Identity(manifest), "candidate_output_contract");
        }

        return Compare(Identity(manifest), oracle, candidate);
    }

    internal static CorpusScoreOutcome ScoreCorpus(string manifestPath)
    {
        CorpusExecutionManifest? suite = null;
        try
        {
            suite = EvaluatorProtocol.Read<CorpusExecutionManifest>(manifestPath, "corpus-execution-manifest.v4.schema.json");
            ValidateCorpusManifest(suite);
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            ScoreOutcome error = Error(suite is null ? null : Identity(suite), "manifest", "manifest", suite?.Members.Count ?? 0);
            return new CorpusScoreOutcome(error.Result, []);
        }

        List<AssertionResults> raw = [];
        List<ScoreOutcome> members = [];
        string suiteFingerprint;
        try
        {
            suiteFingerprint = CorpusFingerprint(suite);
            if (!string.Equals(suiteFingerprint, suite.Corpus.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The aggregate corpus bytes do not match the frozen corpus identity.");
            }

            foreach (CorpusExecutionMember member in suite.Members)
            {
                ExecutionManifest execution = new(
                    EvaluatorProtocol.ManifestSchema,
                    EvaluatorProtocol.ProtocolId,
                    suite.Candidate,
                    suite.Evaluator,
                    suite.Corpus,
                    member.Execution);
                ValidateExecutionInput(member.Execution);
                ExpectedSemanticOutput oracle = EvaluatorProtocol.Read<ExpectedSemanticOutput>(member.OraclePath, "expected-semantic-output.v4.schema.json");
                ValidateOracleIdentity(suite, oracle, suiteFingerprint, requireFingerprint: false);

                ScoreOutcome outcome;
                try
                {
                    CandidateSemanticOutput candidate = ReflectionCandidateAdapter.Execute(execution);
                    ValidateCandidateIdentity(Identity(suite), candidate);
                    outcome = Compare(Identity(suite), oracle, candidate);
                }
                catch (CandidateExecutionException)
                {
                    outcome = ProductFailure(Identity(suite), "candidate_execution");
                }
                catch (CandidateOutputException)
                {
                    outcome = ProductFailure(Identity(suite), "candidate_output_contract");
                }
                catch (Exception exception) when (IsEvaluatorInputFailure(exception))
                {
                    ScoreOutcome error = Error(Identity(suite), "candidate_admission", "candidate_admission", suite.Members.Count);
                    return new CorpusScoreOutcome(error.Result, raw);
                }

                members.Add(outcome);
                if (outcome.Assertions is not null)
                {
                    raw.Add(outcome.Assertions);
                }
            }
        }
        catch (Exception exception) when (IsEvaluatorInputFailure(exception))
        {
            ScoreOutcome error = Error(Identity(suite), "oracle", "oracle", suite.Members.Count);
            return new CorpusScoreOutcome(error.Result, raw);
        }

        return Aggregate(Identity(suite), members, raw);
    }

    internal static SanitizedResult AggregateForCalibration(
        ExecutionManifest manifest,
        IReadOnlyList<ScoreOutcome> members) => Aggregate(
            Identity(manifest),
            members,
            members.Where(item => item.Assertions is not null).Select(item => item.Assertions!).ToArray()).Result;

    private static CorpusScoreOutcome Aggregate(
        ScoreIdentity identity,
        IReadOnlyList<ScoreOutcome> members,
        IReadOnlyList<AssertionResults> raw)
    {
        int evaluatorErrors = members.Count(item => item.Result.TerminalResult == "EVALUATOR_ERROR");
        int failedMembers = members.Count(item => item.Result.TerminalResult == "FAIL");
        AssertionCounts counts = new(
            members.Sum(item => item.Result.AssertionCounts.Total),
            members.Sum(item => item.Result.AssertionCounts.Passed),
            members.Sum(item => item.Result.AssertionCounts.Failed));
        string[] categories = members.SelectMany(item => item.Result.FailureCategories ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string terminal = evaluatorErrors > 0 ? "EVALUATOR_ERROR" : failedMembers == 0 ? "PASS" : "FAIL";
        SanitizedResult result = Result(
            identity,
            terminal,
            evaluatorErrors > 0 ? "comparison" : failedMembers == 0 ? null : "comparison",
            counts,
            new MemberCounts(members.Count, members.Count - failedMembers - evaluatorErrors, failedMembers, evaluatorErrors),
            categories);
        return new CorpusScoreOutcome(result, raw);
    }

    internal static ScoreOutcome Compare(
        ExecutionManifest manifest,
        ExpectedSemanticOutput oracle,
        CandidateSemanticOutput candidate) => Compare(Identity(manifest), oracle, candidate);

    private static ScoreOutcome Compare(ScoreIdentity identity, ExpectedSemanticOutput oracle, CandidateSemanticOutput candidate)
    {
        List<TypedAssertion> assertions = [];
        AddAssertion(assertions, "state", "state", "state", null,
            EvaluatorProtocol.Primitive(oracle.State), EvaluatorProtocol.Primitive(candidate.State),
            string.Equals(oracle.State, candidate.State, StringComparison.Ordinal));

        Dictionary<string, SemanticFact> expected = UniqueFacts(oracle.Facts, "oracle");
        Dictionary<string, SemanticFact> actual = UniqueFacts(candidate.Facts, "candidate");
        foreach (string factId in expected.Keys.Union(actual.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            bool hasExpected = expected.TryGetValue(factId, out SemanticFact? expectedFact);
            bool hasActual = actual.TryGetValue(factId, out SemanticFact? actualFact);
            if (!hasActual)
            {
                AddAssertion(assertions, $"missing:{factId}", "missing", expectedFact!.FactType, factId, expectedFact.Value, null, false);
            }
            else if (!hasExpected)
            {
                AddAssertion(assertions, $"extra:{factId}", "extra", actualFact!.FactType, factId, null, actualFact.Value, false);
            }
            else
            {
                bool equal = string.Equals(expectedFact!.FactType, actualFact!.FactType, StringComparison.Ordinal)
                    && string.Equals(expectedFact.ValueType, actualFact.ValueType, StringComparison.Ordinal)
                    && SemanticValuesEqual(expectedFact.ValueType, expectedFact.Value, actualFact.Value);
                AddAssertion(assertions, $"value:{factId}", "value", expectedFact.FactType, factId, expectedFact.Value, actualFact.Value, equal);
            }
        }

        int passed = assertions.Count(item => item.Outcome == "PASS");
        int failed = assertions.Count - passed;
        string[] categories = assertions.Where(item => item.Outcome == "FAIL")
            .Select(FailureCategory).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        AssertionResults raw = new(EvaluatorProtocol.AssertionsSchema, EvaluatorProtocol.ProtocolId, assertions);
        SanitizedResult result = Result(identity, failed == 0 ? "PASS" : "FAIL", failed == 0 ? null : "comparison",
            new AssertionCounts(assertions.Count, passed, failed),
            new MemberCounts(1, failed == 0 ? 1 : 0, failed == 0 ? 0 : 1, 0), categories);
        return new ScoreOutcome(result, raw);
    }

    internal static void WriteResults(string resultDirectory, ScoreOutcome outcome)
    {
        List<(string FileName, string Content)> outputs = [("sanitized-result.json", ValidatedJson(outcome.Result, "sanitized-result.v4.schema.json"))];
        if (outcome.Assertions is not null)
        {
            outputs.Add(("assertions.json", ValidatedJson(outcome.Assertions, "assertion-results.v4.schema.json")));
        }

        WriteAtomically(resultDirectory, outputs);
    }

    internal static void WriteCorpusResults(string resultDirectory, CorpusScoreOutcome outcome)
    {
        List<(string FileName, string Content)> outputs = [("sanitized-result.json", ValidatedJson(outcome.Result, "sanitized-result.v4.schema.json"))];
        for (int index = 0; index < outcome.MemberAssertions.Count; index++)
        {
            outputs.Add(($"member-{index + 1:D4}-assertions.json", ValidatedJson(outcome.MemberAssertions[index], "assertion-results.v4.schema.json")));
        }

        WriteAtomically(resultDirectory, outputs);
    }

    internal static ExecutionManifest ReadAndValidateManifest(string manifestPath)
    {
        ExecutionManifest manifest = EvaluatorProtocol.Read<ExecutionManifest>(manifestPath, "execution-manifest.v4.schema.json");
        ValidateManifestIdentity(manifest);
        return manifest;
    }

    internal static void ValidateEvaluatorIdentityForTests(EvaluatorIdentity identity) => ValidateEvaluator(identity);

    internal static void WriteSingleResult<T>(string resultDirectory, string fileName, T value, string schemaFileName) =>
        WriteAtomically(resultDirectory, [(fileName, ValidatedJson(value, schemaFileName))]);

    private static string ValidatedJson<T>(T value, string schemaFileName)
    {
        string json = EvaluatorProtocol.Serialize(value);
        using Infinium.Application.Evaluation.BoundedJsonDocumentSnapshot snapshot =
            Infinium.Application.Evaluation.BoundedJsonDocumentReader.Parse(Encoding.UTF8.GetBytes(json), schemaFileName, 96);
        Infinium.Application.Evaluation.EmbeddedJsonSchemaValidator.Validate(snapshot.Document.RootElement, schemaFileName);
        return json;
    }

    private static void WriteAtomically(string resultDirectory, IReadOnlyList<(string FileName, string Content)> outputs)
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
        RequireProtocol(manifest.SchemaId, EvaluatorProtocol.ManifestSchema, manifest.ProtocolId, manifest.Evaluator);
        RequireCorpus(manifest.Corpus);
        ValidateCandidate(manifest.Candidate);
        ValidateEvaluator(manifest.Evaluator);
        ValidateExecutionInput(manifest.Execution);
    }

    private static void ValidatePreparedManifest(PreparedComparisonManifest manifest)
    {
        RequireProtocol(manifest.SchemaId, EvaluatorProtocol.PreparedManifestSchema, manifest.ProtocolId, manifest.Evaluator);
        RequireCorpus(manifest.Corpus);
        ValidateEvaluator(manifest.Evaluator);
    }

    private static void ValidateCorpusManifest(CorpusExecutionManifest manifest)
    {
        RequireProtocol(manifest.SchemaId, EvaluatorProtocol.CorpusManifestSchema, manifest.ProtocolId, manifest.Evaluator);
        RequireCorpus(manifest.Corpus);
        ValidateCandidate(manifest.Candidate);
        ValidateEvaluator(manifest.Evaluator);
        if (manifest.Members.Count == 0 || manifest.Members.Select(item => item.MemberId).Distinct(StringComparer.Ordinal).Count() != manifest.Members.Count)
        {
            throw new InvalidDataException("A corpus requires unique private member identities.");
        }

        foreach (CorpusExecutionMember member in manifest.Members)
        {
            ValidateExecutionInput(member.Execution);
        }
    }

    private static void RequireProtocol(string actualSchema, string expectedSchema, string protocol, EvaluatorIdentity evaluator)
    {
        if (actualSchema != expectedSchema || protocol != EvaluatorProtocol.ProtocolId
            || evaluator.ProtocolId != EvaluatorProtocol.ProtocolId
            || evaluator.ScorerId != EvaluatorProtocol.ScorerId || evaluator.ScorerVersion != EvaluatorProtocol.ScorerVersion
            || evaluator.AdapterId != EvaluatorProtocol.AdapterId || evaluator.AdapterVersion != EvaluatorProtocol.AdapterVersion
            || evaluator.ProjectionId != EvaluatorProtocol.ProjectionId || evaluator.ProjectionVersion != EvaluatorProtocol.ProjectionVersion)
        {
            throw new InvalidDataException("The manifest identifies a different evaluator protocol or projection.");
        }
    }

    private static void RequireCorpus(CorpusIdentity corpus)
    {
        if (corpus.QualificationState != "frozen" || corpus.ContaminationState != "clean")
        {
            throw new InvalidDataException("Only a qualified, frozen, uncontaminated corpus may be scored.");
        }
    }

    private static void ValidateCandidate(CandidateIdentity candidate)
    {
        ArtifactIdentity actual = EvaluatorProtocol.Identity(candidate.AssemblyPath);
        RequireIdentity("candidate", candidate.Artifact, actual);
        HashSet<string> files = ValidateFileInventory(candidate.Root, candidate.Files, "candidate");
        if (!files.Contains(CanonicalRelativePath(candidate.Root, candidate.AssemblyPath)))
        {
            throw new InvalidDataException("The candidate assembly is absent from its dependency inventory.");
        }
    }

    private static void ValidateEvaluator(EvaluatorIdentity evaluator)
    {
        string root = Path.GetFullPath(evaluator.Root);
        HashSet<string> declared = ValidateFileInventory(root, evaluator.Files, "evaluator");
        if (RequiredEvaluatorFiles.Except(declared, StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException("The evaluator identity omits a required executable, dependency, or protocol file.");
        }

        string executing = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
        if (!string.Equals(CanonicalRelativePath(root, executing), "Infinium.EvaluatorV2.dll", StringComparison.Ordinal)
            || !string.Equals(Path.GetDirectoryName(executing), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The declared evaluator root is not the actually executing evaluator root.");
        }

        using JsonDocument deps = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "Infinium.EvaluatorV2.deps.json")));
        string[] requiredManagedDependencies = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value
            .EnumerateObject().SelectMany(library => library.Value.TryGetProperty("runtime", out JsonElement runtime)
                ? runtime.EnumerateObject().Select(file => Path.GetFileName(file.Name))
                : []).Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (requiredManagedDependencies.Except(declared, StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException("The evaluator dependency graph contains an undeclared non-framework dependency.");
        }
    }

    private static void ValidateExecutionInput(ExecutionInput execution)
    {
        HashSet<string> pluginNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int> loadOrders = [];
        foreach (PluginExecutionInput plugin in execution.Plugins)
        {
            if (!pluginNames.Add(plugin.PluginName) || !loadOrders.Add(plugin.LoadOrder))
            {
                throw new InvalidDataException("Plugin names and load-order positions must be unique.");
            }

            RequireIdentity(plugin.PluginName, new ArtifactIdentity(plugin.ByteLength, plugin.Sha256), EvaluatorProtocol.Identity(plugin.Path));
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (LooseProviderChainExecutionInput chain in execution.LooseProviderChains)
        {
            string normalized = chain.NormalizedRelativePath.Replace('\\', '/').ToLowerInvariant();
            if (Path.IsPathRooted(normalized) || normalized.StartsWith("../", StringComparison.Ordinal) || !paths.Add(normalized)
                || chain.Providers.Count == 0
                || chain.Providers.Select(item => item.LocalInstalledEntityId).Distinct(StringComparer.Ordinal).Count() != chain.Providers.Count
                || !chain.Providers.Any(item => item.LocalInstalledEntityId == chain.WinnerLocalInstalledEntityId))
            {
                throw new InvalidDataException("Loose-provider chains require a unique normalized relative path, providers, and an exact declared winner.");
            }

            foreach (LooseProviderExecutionInput provider in chain.Providers)
            {
                bool hasAnyIdentity = provider.Path is not null || provider.ByteLength is not null || provider.Sha256 is not null;
                bool hasFullIdentity = provider.Path is not null && provider.ByteLength is not null && provider.Sha256 is not null;
                if (hasAnyIdentity != hasFullIdentity)
                {
                    throw new InvalidDataException("A retained loose provider requires path, length, and hash together.");
                }

                if (hasFullIdentity)
                {
                    RequireIdentity(provider.LocalInstalledEntityId,
                        new ArtifactIdentity(provider.ByteLength!.Value, provider.Sha256!), EvaluatorProtocol.Identity(provider.Path!));
                }
            }
        }
    }

    private static void ValidateOracleIdentity(ExecutionManifest manifest, ExpectedSemanticOutput oracle, string fingerprint) =>
        ValidateOracleIdentity(Identity(manifest), oracle, fingerprint);
    private static void ValidateOracleIdentity(PreparedComparisonManifest manifest, ExpectedSemanticOutput oracle, string fingerprint) =>
        ValidateOracleIdentity(Identity(manifest), oracle, fingerprint);
    private static void ValidateOracleIdentity(CorpusExecutionManifest manifest, ExpectedSemanticOutput oracle, string fingerprint, bool requireFingerprint) =>
        ValidateOracleIdentity(Identity(manifest), oracle, fingerprint, requireFingerprint);

    private static void ValidateOracleIdentity(ScoreIdentity identity, ExpectedSemanticOutput oracle, string fingerprint, bool requireFingerprint = true)
    {
        if (oracle.SchemaId != EvaluatorProtocol.ExpectedSchema || oracle.ProtocolId != EvaluatorProtocol.ProtocolId
            || oracle.ProjectionId != EvaluatorProtocol.ProjectionId || oracle.ProjectionVersion != EvaluatorProtocol.ProjectionVersion
            || oracle.CorpusId != identity.Corpus.CorpusId || oracle.CorpusVersion != identity.Corpus.Version)
        {
            throw new InvalidDataException("The oracle identity does not match the manifest and projection.");
        }

        if (requireFingerprint && !string.Equals(fingerprint, identity.Corpus.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The oracle and retained inputs do not match the frozen corpus identity.");
        }

        _ = UniqueFacts(oracle.Facts, "oracle");
    }

    private static void ValidateCandidateIdentity(ScoreIdentity identity, CandidateSemanticOutput candidate)
    {
        if (candidate.SchemaId != EvaluatorProtocol.CandidateSchema || candidate.ProtocolId != EvaluatorProtocol.ProtocolId
            || candidate.ProjectionId != EvaluatorProtocol.ProjectionId || candidate.ProjectionVersion != EvaluatorProtocol.ProjectionVersion
            || candidate.CandidateCommit != identity.CandidateCommit || candidate.CandidateArtifact != identity.CandidateArtifact)
        {
            throw new CandidateOutputException("Candidate output identity does not match the manifest and projection.");
        }

        _ = UniqueFacts(candidate.Facts, "candidate");
    }

    private static Dictionary<string, SemanticFact> UniqueFacts(IReadOnlyList<SemanticFact> facts, string source)
    {
        Dictionary<string, SemanticFact> result = new(StringComparer.Ordinal);
        string? previous = null;
        foreach (SemanticFact fact in facts)
        {
            if (!ValueMatchesDeclaredType(fact.ValueType, fact.Value)
                || previous is not null && string.CompareOrdinal(previous, fact.FactId) >= 0
                || !result.TryAdd(fact.FactId, fact))
            {
                throw source == "candidate" ? new CandidateOutputException("Candidate facts must be typed, unique, and ordinally sorted.") : new InvalidDataException("Oracle facts must be typed, unique, and ordinally sorted.");
            }

            previous = fact.FactId;
        }

        return result;
    }

    private static bool ValueMatchesDeclaredType(string declaredType, JsonElement value) => declaredType switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => TryGetSemanticInteger(value, out _),
        "number" => value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double number)
            && double.IsFinite(number),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => false,
    };

    private static bool SemanticValuesEqual(string valueType, JsonElement expected, JsonElement actual) => valueType switch
    {
        "integer" => TryGetSemanticInteger(expected, out long expectedInteger)
            && TryGetSemanticInteger(actual, out long actualInteger)
            && expectedInteger == actualInteger,
        "number" => expected.GetDouble().Equals(actual.GetDouble()),
        _ => JsonElement.DeepEquals(expected, actual),
    };

    private static bool TryGetSemanticInteger(JsonElement value, out long integer)
    {
        integer = default;
        if (value.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (value.TryGetInt64(out integer))
        {
            return true;
        }

        if (!decimal.TryParse(
                value.GetRawText(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal number)
            || decimal.Truncate(number) != number
            || number < long.MinValue
            || number > long.MaxValue)
        {
            return false;
        }

        integer = decimal.ToInt64(number);
        return true;
    }

    private static void AddAssertion(List<TypedAssertion> assertions, string id, string kind, string factType,
        string? factId, JsonElement? expected, JsonElement? actual, bool passed) =>
        assertions.Add(new TypedAssertion(id, kind, passed ? "PASS" : "FAIL", factType, factId, expected, actual));

    private static string FailureCategory(TypedAssertion assertion) => assertion.Kind switch
    {
        "missing" => "missing_fact",
        "extra" => "extra_fact",
        "state" => "state",
        _ => assertion.FactType,
    };

    internal static string CorpusFingerprint(ExecutionManifest manifest, string oraclePath) =>
        FingerprintCorpus(manifest.Corpus, [("single", manifest.Execution, oraclePath)]);

    internal static string CorpusFingerprint(CorpusExecutionManifest manifest) =>
        FingerprintCorpus(manifest.Corpus, manifest.Members.Select(item => (item.MemberId, item.Execution, item.OraclePath)));

    internal static string PreparedCorpusFingerprint(PreparedComparisonManifest manifest, string candidateOutputPath, string oraclePath)
    {
        ArtifactIdentity candidate = EvaluatorProtocol.Identity(candidateOutputPath);
        ArtifactIdentity oracle = EvaluatorProtocol.Identity(oraclePath);
        string material = $"infinium.evaluator-v2.prepared-corpus/4\n{manifest.Corpus.CorpusId}|{manifest.Corpus.Version}\nqualification|{manifest.Candidate.QualificationId}|{candidate.ByteLength}|{candidate.Sha256}\noracle|{oracle.ByteLength}|{oracle.Sha256}\n";
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string FingerprintCorpus(
        CorpusIdentity corpus,
        IEnumerable<(string MemberId, ExecutionInput Execution, string OraclePath)> members)
    {
        StringBuilder material = new("infinium.evaluator-v2.corpus/4\n");
        material.Append(corpus.CorpusId).Append('|').Append(corpus.Version).Append('\n');
        int index = 0;
        foreach ((string memberId, ExecutionInput execution, string oraclePath) in members)
        {
            material.Append("member|").Append(index++).Append('|').Append(memberId).Append('\n');
            foreach (PluginExecutionInput plugin in execution.Plugins.OrderBy(item => item.LoadOrder))
            {
                material.Append("plugin|").Append(plugin.LoadOrder).Append('|').Append(plugin.PluginName.ToLowerInvariant()).Append('|')
                    .Append(plugin.LocalInstalledEntityId).Append('|').Append(plugin.ByteLength).Append('|').Append(plugin.Sha256).Append('\n');
            }

            foreach (LooseProviderChainExecutionInput chain in execution.LooseProviderChains.OrderBy(item => item.NormalizedRelativePath, StringComparer.Ordinal))
            {
                material.Append("loose|").Append(chain.NormalizedRelativePath.Replace('\\', '/').ToLowerInvariant()).Append('|').Append(chain.WinnerLocalInstalledEntityId).Append('\n');
                foreach (LooseProviderExecutionInput provider in chain.Providers)
                {
                    material.Append("provider|").Append(provider.LocalInstalledEntityId).Append('|').Append(provider.ProviderKind).Append('|').Append(provider.Priority)
                        .Append('|').Append(provider.ByteLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "absent")
                        .Append('|').Append(provider.Sha256 ?? "absent").Append('\n');
                }
            }

            material.Append("archive|").Append(execution.ArchiveMemberPopulationSupported ? "true" : "false").Append('\n');
            foreach (string capability in execution.UnsupportedCapabilities.Order(StringComparer.Ordinal))
            {
                material.Append("unsupported|").Append(capability).Append('\n');
            }

            ArtifactIdentity oracle = EvaluatorProtocol.Identity(oraclePath);
            material.Append("oracle|").Append(oracle.ByteLength).Append('|').Append(oracle.Sha256).Append('\n');
        }

        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString())));
    }

    private static ScoreOutcome ProductFailure(ScoreIdentity identity, string category)
    {
        TypedAssertion assertion = new(category, category, "FAIL", category);
        AssertionResults raw = new(EvaluatorProtocol.AssertionsSchema, EvaluatorProtocol.ProtocolId, [assertion]);
        SanitizedResult result = Result(identity, "FAIL", category,
            new AssertionCounts(1, 0, 1), new MemberCounts(1, 0, 1, 0), [category]);
        return new ScoreOutcome(result, raw);
    }

    private static ScoreOutcome Error(ScoreIdentity? identity, string stage, string category, int members = 1)
    {
        ScoreIdentity safe = identity ?? ScoreIdentity.Empty;
        SanitizedResult result = Result(safe, "EVALUATOR_ERROR", stage,
            new AssertionCounts(0, 0, 0), new MemberCounts(members, 0, 0, members), [category]);
        return new ScoreOutcome(result, null);
    }

    private static SanitizedResult Result(ScoreIdentity identity, string terminal, string? stage,
        AssertionCounts counts, MemberCounts memberCounts, IReadOnlyList<string> categories) => new(
        EvaluatorProtocol.SanitizedSchema, EvaluatorProtocol.ProtocolId,
        EvaluatorProtocol.ProjectionId, EvaluatorProtocol.ProjectionVersion,
        identity.CandidateCommit, identity.CandidateArtifact.ByteLength, identity.CandidateArtifact.Sha256,
        identity.Evaluator.Commit, EvaluatorFilesFingerprint(identity.Evaluator.Files),
        EvaluatorProtocol.ScorerId, EvaluatorProtocol.ScorerVersion,
        EvaluatorProtocol.AdapterId, EvaluatorProtocol.AdapterVersion,
        identity.Corpus.CorpusId, identity.Corpus.Version, identity.Corpus.Sha256,
        terminal, stage, counts, memberCounts, categories, identity.Corpus.ContaminationState);

    private static ScoreIdentity Identity(ExecutionManifest manifest) => new(manifest.Candidate.Commit, manifest.Candidate.Artifact, manifest.Evaluator, manifest.Corpus);
    private static ScoreIdentity Identity(CorpusExecutionManifest manifest) => new(manifest.Candidate.Commit, manifest.Candidate.Artifact, manifest.Evaluator, manifest.Corpus);
    private static ScoreIdentity Identity(PreparedComparisonManifest manifest) => new(manifest.Candidate.Commit, manifest.Candidate.Artifact, manifest.Evaluator, manifest.Corpus);

    private static void RequireIdentity(string label, ArtifactIdentity expected, ArtifactIdentity actual)
    {
        if (expected != actual)
        {
            throw new InvalidDataException($"The retained identity for '{label}' does not match its bytes.");
        }
    }

    private static HashSet<string> ValidateFileInventory(string rootPath, IReadOnlyList<EvaluatorFileIdentity> files, string label)
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
            if (relative != file.RelativePath.Replace('\\', '/') || !declared.Add(relative))
            {
                throw new InvalidDataException($"{label} file identities must be unique canonical relative paths.");
            }
            RequireIdentity(file.RelativePath, new ArtifactIdentity(file.ByteLength, file.Sha256), EvaluatorProtocol.Identity(path));
        }

        return declared;
    }

    private static string CanonicalRelativePath(string rootPath, string path)
    {
        string root = Path.GetFullPath(rootPath);
        string relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("A retained file identity escapes its declared root.");
        }
        return relative.Replace('\\', '/');
    }

    internal static string EvaluatorFilesFingerprint(IReadOnlyList<EvaluatorFileIdentity> files)
    {
        string material = string.Join('\n', files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => $"{file.RelativePath.Replace('\\', '/')}|{file.ByteLength}|{file.Sha256}"));
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string[] BuildRequiredEvaluatorFiles()
    {
        string depsPath = Path.Combine(AppContext.BaseDirectory, "Infinium.EvaluatorV2.deps.json");
        using JsonDocument deps = JsonDocument.Parse(File.ReadAllBytes(depsPath));
        IEnumerable<string> managed = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value
            .EnumerateObject().SelectMany(library => library.Value.TryGetProperty("runtime", out JsonElement runtime)
                ? runtime.EnumerateObject().Select(file => Path.GetFileName(file.Name))
                : []).Where(name => name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        return RequiredPublicFiles.Concat(managed).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsEvaluatorInputFailure(Exception exception) => exception is IOException or UnauthorizedAccessException
        or InvalidDataException or ArgumentException or System.Security.SecurityException or TargetInvocationException or TypeLoadException or JsonException;

    private sealed record ScoreIdentity(string CandidateCommit, ArtifactIdentity CandidateArtifact, EvaluatorIdentity Evaluator, CorpusIdentity Corpus)
    {
        internal static readonly ScoreIdentity Empty = new(new('0', 40), new ArtifactIdentity(1, new('0', 64)),
            new EvaluatorIdentity(new('0', 40), EvaluatorProtocol.ProtocolId, EvaluatorProtocol.ScorerId, EvaluatorProtocol.ScorerVersion,
                EvaluatorProtocol.AdapterId, EvaluatorProtocol.AdapterVersion, EvaluatorProtocol.ProjectionId, EvaluatorProtocol.ProjectionVersion, ".", []),
            new CorpusIdentity("unavailable", "unavailable", new('0', 64), "frozen", "clean"));
    }
}

internal sealed class CandidateOutputException(string message, Exception? innerException = null) : Exception(message, innerException);
internal sealed class CandidateExecutionException(string message, Exception? innerException = null) : Exception(message, innerException);
internal sealed class ResultWriteException(string message, Exception? innerException = null) : Exception(message, innerException);
