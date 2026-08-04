namespace Infinium.EvaluatorV2;

internal static class CalibrationSuite
{
    internal static CalibrationResults Run()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-v2-calibration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            CalibrationContext context = CreateContext(root);
            CandidateSemanticOutput correct = Candidate(context.Oracle);
            List<CalibrationCaseResult> cases =
            [
                ScoreCase("known-correct", "PASS", null, context, correct),
                ScoreCase("wrong-winner", "FAIL", "winner", context, Mutate(correct, "/winner/plugin", "Wrong.esp")),
                ScoreCase("reversed-override-chain", "FAIL", "override_chain", context,
                    Mutate(Mutate(correct, "/chain/contributions/0", "Second.esp"), "/chain/contributions/1", "First.esm")),
                ScoreCase("wrong-regular-form-key", "FAIL", "form_key", context, Mutate(correct, "/records/regular/form_key", "00000043:base.esm")),
                ScoreCase("wrong-light-form-key", "FAIL", "form_key", context, Mutate(correct, "/records/light/form_key", "00000802:light.esl")),
                ScoreCase("missing-fact", "FAIL", "missing_fact", context, Remove(correct, "/links/owner")),
                ScoreCase("extra-fact", "FAIL", "extra_fact", context, Add(correct, Fact("/records/extra", "semantic", "unexpected"))),
                ScoreCase("wrong-link", "FAIL", "link", context, Mutate(correct, "/links/base", "00000099:base.esm")),
                ScoreCase("wrong-ownership", "FAIL", "ownership", context, Mutate(correct, "/links/owner", "00000099:base.esm")),
                ScoreCase("wrong-placement", "FAIL", "placement", context, Mutate(correct, "/placement/x", 999L)),
                ScoreCase("wrong-gap", "FAIL", "gap", context, Mutate(correct, "/gaps/{gap_id=archive}/denominator", 3L)),
                ScoreCase("candidate-output-contract", "FAIL", "candidate_output_contract", context,
                    correct with { ProtocolId = "wrong-protocol" }),
                ExecutionFailureCase(context),
                BrokenManifestCase(context, correct),
                MalformedOracleCase(context, correct),
                TamperedOracleCase(context, correct),
            ];
            cases.AddRange(PreparedCases(context, correct));
            cases.AddRange(AggregateCases(context, correct));
            cases.Add(EvaluatorRootDriftCase(context, correct));
            cases.Add(CandidateDependencyDriftCase(context, correct));
            return new CalibrationResults(
                EvaluatorProtocol.CalibrationSchema,
                EvaluatorProtocol.ProtocolId,
                "infinium.evaluator-v2.public-calibration/2",
                cases,
                cases.All(item => item.Passed));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CalibrationContext CreateContext(string root)
    {
        string candidateRoot = Path.Combine(root, "candidate");
        Directory.CreateDirectory(candidateRoot);
        string candidatePath = Path.Combine(candidateRoot, "CalibrationCandidate.dll");
        File.WriteAllBytes(candidatePath, [0x43, 0x41, 0x4c]);
        ArtifactIdentity candidateArtifact = EvaluatorProtocol.Identity(candidatePath);
        string dependencyPath = Path.Combine(candidateRoot, "CalibrationDependency.dll");
        File.WriteAllBytes(dependencyPath, [0x44, 0x45, 0x50]);
        ArtifactIdentity dependencyArtifact = EvaluatorProtocol.Identity(dependencyPath);

        string pluginPath = Path.Combine(root, "Calibration.esm");
        File.WriteAllBytes(pluginPath, [0x50, 0x4c, 0x47]);
        ArtifactIdentity pluginArtifact = EvaluatorProtocol.Identity(pluginPath);
        string evaluatorRoot = AppContext.BaseDirectory;
        EvaluatorFileIdentity[] evaluatorFiles = EvaluatorScorer.RequiredEvaluatorFiles
            .Select(relative => FileIdentity(evaluatorRoot, relative))
            .ToArray();
        ExpectedSemanticOutput oracle = Oracle();
        string oraclePath = Path.Combine(root, "oracle.json");
        File.WriteAllText(oraclePath, EvaluatorProtocol.Serialize(oracle), new System.Text.UTF8Encoding(false));

        ExecutionManifest manifest = new(
            EvaluatorProtocol.ManifestSchema,
            EvaluatorProtocol.ProtocolId,
            new CandidateIdentity(
                new('a', 40),
                candidatePath,
                candidateArtifact,
                candidateRoot,
                [
                    new EvaluatorFileIdentity("CalibrationCandidate.dll", candidateArtifact.ByteLength, candidateArtifact.Sha256),
                    new EvaluatorFileIdentity("CalibrationDependency.dll", dependencyArtifact.ByteLength, dependencyArtifact.Sha256),
                ]),
            new EvaluatorIdentity(
                new('c', 40),
                EvaluatorProtocol.ProtocolId,
                EvaluatorProtocol.ScorerId,
                EvaluatorProtocol.ScorerVersion,
                EvaluatorProtocol.AdapterId,
                EvaluatorProtocol.AdapterVersion,
                EvaluatorProtocol.ProjectionId,
                EvaluatorProtocol.ProjectionVersion,
                evaluatorRoot,
                evaluatorFiles),
            new CorpusIdentity("public-calibration", "1.0.0", new string('0', 64), "frozen", "clean"),
            new ExecutionInput(
                [new PluginExecutionInput("Calibration.esm", 0, "calibration-provider", pluginPath, pluginArtifact.ByteLength, pluginArtifact.Sha256)],
                [],
                false,
                []));
        manifest = manifest with
        {
            Corpus = manifest.Corpus with { Sha256 = EvaluatorScorer.CorpusFingerprint(manifest, oraclePath) },
        };
        string manifestPath = Path.Combine(root, "manifest.json");
        File.WriteAllText(manifestPath, EvaluatorProtocol.Serialize(manifest), new System.Text.UTF8Encoding(false));
        return new CalibrationContext(manifest, oracle, manifestPath, oraclePath, dependencyPath);
    }

    private static CalibrationCaseResult ScoreCase(
        string id,
        string expectedTerminal,
        string? category,
        CalibrationContext context,
        CandidateSemanticOutput candidate)
    {
        ScoreOutcome outcome = EvaluatorScorer.Score(
            context.ManifestPath,
            context.OraclePath,
            _ => candidate);
        return CaseResult(id, expectedTerminal, category, outcome);
    }

    private static CalibrationCaseResult ExecutionFailureCase(CalibrationContext context)
    {
        ScoreOutcome outcome = EvaluatorScorer.Score(
            context.ManifestPath,
            context.OraclePath,
            _ => throw new CandidateExecutionException("calibrated execution failure"));
        return CaseResult("candidate-execution-failure", "FAIL", "candidate_execution", outcome);
    }

    private static CalibrationCaseResult BrokenManifestCase(
        CalibrationContext context,
        CandidateSemanticOutput candidate)
    {
        string path = Path.Combine(Path.GetDirectoryName(context.ManifestPath)!, "broken-manifest.json");
        File.WriteAllText(path, "{}", new System.Text.UTF8Encoding(false));
        ScoreOutcome outcome = EvaluatorScorer.Score(path, context.OraclePath, _ => candidate);
        return CaseResult("broken-manifest", "EVALUATOR_ERROR", "manifest", outcome);
    }

    private static CalibrationCaseResult MalformedOracleCase(
        CalibrationContext context,
        CandidateSemanticOutput candidate)
    {
        string path = Path.Combine(Path.GetDirectoryName(context.OraclePath)!, "malformed-oracle.json");
        File.WriteAllText(path, "{", new System.Text.UTF8Encoding(false));
        ScoreOutcome outcome = EvaluatorScorer.Score(context.ManifestPath, path, _ => candidate);
        return CaseResult("malformed-oracle", "EVALUATOR_ERROR", "oracle", outcome);
    }

    private static CalibrationCaseResult TamperedOracleCase(
        CalibrationContext context,
        CandidateSemanticOutput candidate)
    {
        string path = Path.Combine(Path.GetDirectoryName(context.OraclePath)!, "tampered-oracle.json");
        ExpectedSemanticOutput changed = context.Oracle with
        {
            Facts = context.Oracle.Facts.Select(fact => fact.FactId == "/winner/plugin"
                ? fact with { Value = EvaluatorProtocol.Primitive("Tampered.esp") }
                : fact).ToArray(),
        };
        File.WriteAllText(path, EvaluatorProtocol.Serialize(changed), new System.Text.UTF8Encoding(false));
        ScoreOutcome outcome = EvaluatorScorer.Score(context.ManifestPath, path, _ => candidate);
        return CaseResult("tampered-oracle-identity", "EVALUATOR_ERROR", "oracle", outcome);
    }

    private static CalibrationCaseResult CandidateDependencyDriftCase(
        CalibrationContext context,
        CandidateSemanticOutput candidate)
    {
        File.AppendAllText(context.DependencyPath, "drift", new System.Text.UTF8Encoding(false));
        ScoreOutcome outcome = EvaluatorScorer.Score(context.ManifestPath, context.OraclePath, _ => candidate);
        return CaseResult("candidate-dependency-drift", "EVALUATOR_ERROR", "manifest", outcome);
    }

    private static IReadOnlyList<CalibrationCaseResult> PreparedCases(
        CalibrationContext context,
        CandidateSemanticOutput correct)
    {
        string root = Path.Combine(Path.GetDirectoryName(context.ManifestPath)!, "prepared");
        Directory.CreateDirectory(root);
        ExpectedSemanticOutput oracle = context.Oracle with { CorpusId = "public-prepared-calibration" };
        string oraclePath = Path.Combine(root, "oracle.json");
        string candidatePath = Path.Combine(root, "candidate.json");
        File.WriteAllText(oraclePath, EvaluatorProtocol.Serialize(oracle), new System.Text.UTF8Encoding(false));
        File.WriteAllText(candidatePath, EvaluatorProtocol.Serialize(correct), new System.Text.UTF8Encoding(false));
        PreparedComparisonManifest manifest = new(
            EvaluatorProtocol.PreparedManifestSchema,
            EvaluatorProtocol.ProtocolId,
            new PreparedCandidateIdentity(correct.CandidateCommit, correct.CandidateArtifact, "answer-known-non-product-calibration"),
            context.Manifest.Evaluator,
            new CorpusIdentity("public-prepared-calibration", "1.0.0", new('0', 64), "frozen", "clean"));
        manifest = manifest with
        {
            Corpus = manifest.Corpus with
            {
                Sha256 = EvaluatorScorer.PreparedCorpusFingerprint(manifest, candidatePath, oraclePath),
            },
        };
        string manifestPath = Path.Combine(root, "manifest.json");
        File.WriteAllText(manifestPath, EvaluatorProtocol.Serialize(manifest), new System.Text.UTF8Encoding(false));
        ScoreOutcome pass = EvaluatorScorer.ComparePrepared(manifestPath, candidatePath, oraclePath);

        CandidateSemanticOutput mutation = Mutate(correct, "/winner/plugin", "Wrong.esp");
        string mutationPath = Path.Combine(root, "mutation.json");
        File.WriteAllText(mutationPath, EvaluatorProtocol.Serialize(mutation), new System.Text.UTF8Encoding(false));
        PreparedComparisonManifest mutationManifest = manifest with { Corpus = manifest.Corpus with { Sha256 = new('0', 64) } };
        mutationManifest = mutationManifest with
        {
            Corpus = mutationManifest.Corpus with
            {
                Sha256 = EvaluatorScorer.PreparedCorpusFingerprint(mutationManifest, mutationPath, oraclePath),
            },
        };
        string mutationManifestPath = Path.Combine(root, "mutation-manifest.json");
        File.WriteAllText(mutationManifestPath, EvaluatorProtocol.Serialize(mutationManifest), new System.Text.UTF8Encoding(false));
        ScoreOutcome fail = EvaluatorScorer.ComparePrepared(mutationManifestPath, mutationPath, oraclePath);
        return
        [
            CaseResult("prepared-known-correct", "PASS", null, pass),
            CaseResult("prepared-targeted-mutation", "FAIL", "winner", fail),
        ];
    }

    private static IReadOnlyList<CalibrationCaseResult> AggregateCases(
        CalibrationContext context,
        CandidateSemanticOutput correct)
    {
        ScoreOutcome pass = EvaluatorScorer.Score(context.ManifestPath, context.OraclePath, _ => correct);
        ScoreOutcome fail = EvaluatorScorer.Score(
            context.ManifestPath,
            context.OraclePath,
            _ => Mutate(correct, "/winner/plugin", "Wrong.esp"));
        SanitizedResult one = EvaluatorScorer.AggregateForCalibration(context.Manifest, [pass]);
        SanitizedResult multiple = EvaluatorScorer.AggregateForCalibration(context.Manifest, [pass, pass]);
        SanitizedResult mismatch = EvaluatorScorer.AggregateForCalibration(context.Manifest, [pass, fail]);
        return
        [
            DirectCase("aggregate-one-member", "PASS", null, one),
            DirectCase("aggregate-multi-member", "PASS", null, multiple),
            DirectCase("aggregate-member-mismatch", "FAIL", "winner", mismatch),
        ];
    }

    private static CalibrationCaseResult EvaluatorRootDriftCase(
        CalibrationContext context,
        CandidateSemanticOutput correct)
    {
        ExecutionManifest drift = context.Manifest with
        {
            Evaluator = context.Manifest.Evaluator with { Root = Path.GetDirectoryName(context.ManifestPath)! },
        };
        string path = Path.Combine(Path.GetDirectoryName(context.ManifestPath)!, "wrong-evaluator-root.json");
        File.WriteAllText(path, EvaluatorProtocol.Serialize(drift), new System.Text.UTF8Encoding(false));
        ScoreOutcome outcome = EvaluatorScorer.Score(path, context.OraclePath, _ => correct);
        return CaseResult("executing-evaluator-root-drift", "EVALUATOR_ERROR", "manifest", outcome);
    }

    private static CalibrationCaseResult DirectCase(
        string id,
        string expectedTerminal,
        string? category,
        SanitizedResult result) => CaseResult(
            id,
            expectedTerminal,
            category,
            new ScoreOutcome(result, null));

    private static CalibrationCaseResult CaseResult(
        string id,
        string expectedTerminal,
        string? category,
        ScoreOutcome outcome)
    {
        IReadOnlyList<string> observed = outcome.Result.FailureCategories ?? [];
        bool passed = outcome.Result.TerminalResult == expectedTerminal
            && (category is null ? observed.Count == 0 : observed.SequenceEqual([category], StringComparer.Ordinal));
        return new CalibrationCaseResult(id, expectedTerminal, outcome.Result.TerminalResult, category, observed, passed);
    }

    private static EvaluatorFileIdentity FileIdentity(string root, string relative)
    {
        ArtifactIdentity identity = EvaluatorProtocol.Identity(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        return new EvaluatorFileIdentity(relative, identity.ByteLength, identity.Sha256);
    }

    private static ExpectedSemanticOutput Oracle()
    {
        SemanticFact[] facts =
        [
            Fact("/chain/contributions/0", "override_chain", "First.esm"),
            Fact("/chain/contributions/1", "override_chain", "Second.esp"),
            Fact("/gaps/{gap_id=archive}/denominator", "gap", 2L),
            Fact("/links/base", "link", "00000042:base.esm"),
            Fact("/links/owner", "ownership", "00000042:base.esm"),
            Fact("/placement/x", "placement", 1L),
            Fact("/records/light/form_key", "form_key", "00000801:light.esl"),
            Fact("/records/regular/form_key", "form_key", "00000042:base.esm"),
            Fact("/winner/plugin", "winner", "Second.esp"),
        ];
        return new ExpectedSemanticOutput(
            EvaluatorProtocol.ExpectedSchema,
            EvaluatorProtocol.ProtocolId,
            EvaluatorProtocol.ProjectionId,
            EvaluatorProtocol.ProjectionVersion,
            "public-calibration",
            "1.0.0",
            "completed_with_gaps",
            facts.OrderBy(item => item.FactId, StringComparer.Ordinal).ToArray());
    }

    private static CandidateSemanticOutput Candidate(ExpectedSemanticOutput oracle) => new(
        EvaluatorProtocol.CandidateSchema,
        EvaluatorProtocol.ProtocolId,
        EvaluatorProtocol.ProjectionId,
        EvaluatorProtocol.ProjectionVersion,
        new('a', 40),
        new ArtifactIdentity(3, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData([0x43, 0x41, 0x4c]))),
        oracle.State,
        oracle.Facts.ToArray());

    private static CandidateSemanticOutput Mutate(CandidateSemanticOutput source, string factId, string value) =>
        Replace(source, factId, fact => fact with { Value = EvaluatorProtocol.Primitive(value), ValueType = "string" });

    private static CandidateSemanticOutput Mutate(CandidateSemanticOutput source, string factId, long value) =>
        Replace(source, factId, fact => fact with { Value = EvaluatorProtocol.Primitive(value), ValueType = "integer" });

    private static CandidateSemanticOutput Replace(
        CandidateSemanticOutput source,
        string factId,
        Func<SemanticFact, SemanticFact> replacement) => source with
        {
            Facts = source.Facts.Select(item => item.FactId == factId ? replacement(item) : item).ToArray(),
        };

    private static CandidateSemanticOutput Remove(CandidateSemanticOutput source, string factId) => source with
    {
        Facts = source.Facts.Where(item => item.FactId != factId).ToArray(),
    };

    private static CandidateSemanticOutput Add(CandidateSemanticOutput source, SemanticFact fact) => source with
    {
        Facts = source.Facts.Append(fact).OrderBy(item => item.FactId, StringComparer.Ordinal).ToArray(),
    };

    private static SemanticFact Fact(string id, string type, string value) =>
        new(id, type, "string", EvaluatorProtocol.Primitive(value));

    private static SemanticFact Fact(string id, string type, long value) =>
        new(id, type, "integer", EvaluatorProtocol.Primitive(value));

    private sealed record CalibrationContext(
        ExecutionManifest Manifest,
        ExpectedSemanticOutput Oracle,
        string ManifestPath,
        string OraclePath,
        string DependencyPath);
}
