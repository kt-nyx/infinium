using System.Text.Json;
using Infinium.Application.Evaluation;

namespace Infinium.EvaluatorV2;

internal static class CalibrationSuite
{
    internal static CalibrationResults Run()
    {
        ExecutionManifest manifest = Manifest();
        ExpectedSemanticOutput oracle = Oracle();
        CandidateSemanticOutput correct = Candidate(oracle);
        List<CalibrationCaseResult> cases =
        [
            CompareCase("known-correct", "PASS", null, manifest, oracle, correct),
            CompareCase("wrong-winner", "FAIL", "winner", manifest, oracle, Mutate(correct, "/winner/plugin", "Wrong.esp")),
            CompareCase("reversed-override-chain", "FAIL", "override_chain", manifest, oracle,
                Mutate(Mutate(correct, "/chain/contributions/0", "Second.esp"), "/chain/contributions/1", "First.esm")),
            CompareCase("wrong-regular-form-key", "FAIL", "form_key", manifest, oracle, Mutate(correct, "/records/regular/form_key", "base.esm:00000043")),
            CompareCase("wrong-light-form-key", "FAIL", "form_key", manifest, oracle, Mutate(correct, "/records/light/form_key", "light.esl:00fe0002")),
            CompareCase("missing-fact", "FAIL", "missing_fact", manifest, oracle, Remove(correct, "/links/owner")),
            CompareCase("extra-fact", "FAIL", "extra_fact", manifest, oracle, Add(correct, Fact("/records/extra", "semantic", "unexpected"))),
            CompareCase("wrong-link", "FAIL", "link", manifest, oracle, Mutate(correct, "/links/base", "base.esm:00000099")),
            CompareCase("wrong-ownership", "FAIL", "ownership", manifest, oracle, Mutate(correct, "/links/owner", "base.esm:00000099")),
            CompareCase("wrong-placement", "FAIL", "placement", manifest, oracle, Mutate(correct, "/placement/x", 999L)),
            CompareCase("wrong-gap", "FAIL", "gap", manifest, oracle, Mutate(correct, "/gaps/{gap_id=archive}/denominator", 3L)),
            DocumentFailureCase("malformed-candidate-output", "FAIL", "candidate_schema", CandidateMalformedJson(), "candidate-semantic-output.v1.schema.json", productFailure: true),
            DocumentFailureCase("broken-manifest", "EVALUATOR_ERROR", "manifest", "{}", "execution-manifest.v1.schema.json", productFailure: false),
            DocumentFailureCase("malformed-oracle", "EVALUATOR_ERROR", "oracle", "{", "expected-semantic-output.v1.schema.json", productFailure: false),
        ];
        return new CalibrationResults(
            EvaluatorProtocol.CalibrationSchema,
            EvaluatorProtocol.ProtocolId,
            "infinium.evaluator-v2.public-calibration/1",
            cases,
            cases.All(item => item.Passed));
    }

    private static CalibrationCaseResult CompareCase(
        string id,
        string expectedTerminal,
        string? category,
        ExecutionManifest manifest,
        ExpectedSemanticOutput oracle,
        CandidateSemanticOutput candidate)
    {
        ScoreOutcome outcome = EvaluatorScorer.Compare(manifest, oracle, candidate);
        IReadOnlyList<string> observed = outcome.Result.FailureCategories ?? [];
        bool passed = outcome.Result.TerminalResult == expectedTerminal
            && (category is null ? observed.Count == 0 : observed.SequenceEqual([category], StringComparer.Ordinal));
        return new CalibrationCaseResult(id, expectedTerminal, outcome.Result.TerminalResult, category, observed, passed);
    }

    private static CalibrationCaseResult DocumentFailureCase(
        string id,
        string terminal,
        string category,
        string json,
        string schema,
        bool productFailure)
    {
        string actualTerminal = "PASS";
        string observedCategory = string.Empty;
        try
        {
            using BoundedJsonDocumentSnapshot snapshot = BoundedJsonDocumentReader.Parse(
                System.Text.Encoding.UTF8.GetBytes(json),
                id,
                96);
            EmbeddedJsonSchemaValidator.Validate(snapshot.Document.RootElement, schema);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            actualTerminal = productFailure ? "FAIL" : "EVALUATOR_ERROR";
            observedCategory = category;
        }

        bool passed = actualTerminal == terminal && observedCategory == category;
        return new CalibrationCaseResult(id, terminal, actualTerminal, category, [observedCategory], passed);
    }

    private static string CandidateMalformedJson() =>
        "{\"schema_id\":\"infinium.evaluator-v2.candidate-semantic-output/v1\",\"protocol_id\":\"infinium.evaluator-v2/1\"}";

    private static ExecutionManifest Manifest() => new(
        EvaluatorProtocol.ManifestSchema,
        EvaluatorProtocol.ProtocolId,
        new CandidateIdentity(new('a', 40), "candidate.dll", new ArtifactIdentity(1, new string('b', 64))),
        new EvaluatorIdentity(
            new('c', 40),
            EvaluatorProtocol.ProtocolId,
            EvaluatorProtocol.ScorerId,
            EvaluatorProtocol.ScorerVersion,
            EvaluatorProtocol.AdapterId,
            EvaluatorProtocol.AdapterVersion,
            ".",
            []),
        new CorpusIdentity("public-calibration", "1.0.0", new string('d', 64)),
        new ExecutionInput([], []));

    private static ExpectedSemanticOutput Oracle()
    {
        SemanticFact[] facts =
        [
            Fact("/chain/contributions/0", "override_chain", "First.esm"),
            Fact("/chain/contributions/1", "override_chain", "Second.esp"),
            Fact("/gaps/{gap_id=archive}/denominator", "gap", 2L),
            Fact("/links/base", "link", "base.esm:00000042"),
            Fact("/links/owner", "ownership", "base.esm:00000042"),
            Fact("/placement/x", "placement", 1L),
            Fact("/records/light/form_key", "form_key", "light.esl:00fe0001"),
            Fact("/records/regular/form_key", "form_key", "base.esm:00000042"),
            Fact("/winner/plugin", "winner", "Second.esp"),
        ];
        return new ExpectedSemanticOutput(
            EvaluatorProtocol.ExpectedSchema,
            EvaluatorProtocol.ProtocolId,
            "public-calibration",
            "1.0.0",
            new string('d', 64),
            "completed_with_gaps",
            facts.OrderBy(item => item.FactId, StringComparer.Ordinal).ToArray());
    }

    private static CandidateSemanticOutput Candidate(ExpectedSemanticOutput oracle) => new(
        EvaluatorProtocol.CandidateSchema,
        EvaluatorProtocol.ProtocolId,
        new('a', 40),
        new ArtifactIdentity(1, new string('b', 64)),
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
}
