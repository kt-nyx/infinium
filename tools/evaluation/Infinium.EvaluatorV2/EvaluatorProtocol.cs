using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Application.Evaluation;

namespace Infinium.EvaluatorV2;

internal static class EvaluatorProtocol
{
    internal const string ProtocolId = "infinium.evaluator-v2/1";
    internal const string ScorerId = "infinium.evaluator-v2.scorer";
    internal const string ScorerVersion = "1.0.0";
    internal const string AdapterId = "infinium.evaluator-v2.slice4-reflection-adapter";
    internal const string AdapterVersion = "1.0.0";
    internal const string CandidateSchema = "infinium.evaluator-v2.candidate-semantic-output/v1";
    internal const string ExpectedSchema = "infinium.evaluator-v2.expected-semantic-output/v1";
    internal const string ManifestSchema = "infinium.evaluator-v2.execution-manifest/v1";
    internal const string AssertionsSchema = "infinium.evaluator-v2.assertion-results/v1";
    internal const string SanitizedSchema = "infinium.evaluator-v2.sanitized-result/v1";
    internal const string CalibrationSchema = "infinium.evaluator-v2.calibration-results/v1";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    internal static T Read<T>(string path, string schemaFileName)
    {
        using BoundedJsonDocumentSnapshot snapshot = BoundedJsonDocumentReader.Read(path, 64 * 1024 * 1024, 96);
        EmbeddedJsonSchemaValidator.Validate(snapshot.Document.RootElement, schemaFileName);
        return JsonSerializer.Deserialize<T>(snapshot.Document.RootElement.GetRawText(), JsonOptions)
            ?? throw new InvalidDataException($"'{path}' contains JSON null instead of a document.");
    }

    internal static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine;

    internal static ArtifactIdentity Identity(string path)
    {
        FileInfo info = new(path);
        if (!info.Exists || info.Length < 1)
        {
            throw new InvalidDataException($"Required file '{path}' is absent or empty.");
        }

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return new ArtifactIdentity(info.Length, hash);
    }

    internal static string JsonValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => throw new InvalidDataException("A semantic fact value must be a JSON primitive."),
    };

    internal static JsonElement Primitive(string value)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    internal static JsonElement Primitive(long value)
    {
        using JsonDocument document = JsonDocument.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return document.RootElement.Clone();
    }

    internal static JsonElement Primitive(bool value)
    {
        using JsonDocument document = JsonDocument.Parse(value ? "true" : "false");
        return document.RootElement.Clone();
    }

    internal static JsonElement Null()
    {
        using JsonDocument document = JsonDocument.Parse("null");
        return document.RootElement.Clone();
    }
}

internal sealed record ArtifactIdentity(long ByteLength, string Sha256);
internal sealed record CandidateIdentity(string Commit, string AssemblyPath, ArtifactIdentity Artifact);
internal sealed record EvaluatorFileIdentity(string RelativePath, long ByteLength, string Sha256);
internal sealed record EvaluatorIdentity(
    string Commit,
    string ProtocolId,
    string ScorerId,
    string ScorerVersion,
    string AdapterId,
    string AdapterVersion,
    string Root,
    IReadOnlyList<EvaluatorFileIdentity> Files);
internal sealed record CorpusIdentity(string CorpusId, string Version, string Sha256);
internal sealed record PluginExecutionInput(
    string PluginName,
    int LoadOrder,
    string LocalInstalledEntityId,
    string Path,
    long ByteLength,
    string Sha256);
internal sealed record ExecutionInput(
    IReadOnlyList<PluginExecutionInput> Plugins,
    IReadOnlyList<string> UnsupportedCapabilities);
internal sealed record ExecutionManifest(
    string SchemaId,
    string ProtocolId,
    CandidateIdentity Candidate,
    EvaluatorIdentity Evaluator,
    CorpusIdentity Corpus,
    ExecutionInput Execution);
internal sealed record SemanticFact(string FactId, string FactType, string ValueType, JsonElement Value);
internal sealed record CandidateSemanticOutput(
    string SchemaId,
    string ProtocolId,
    string CandidateCommit,
    ArtifactIdentity CandidateArtifact,
    string State,
    IReadOnlyList<SemanticFact> Facts);
internal sealed record ExpectedSemanticOutput(
    string SchemaId,
    string ProtocolId,
    string CorpusId,
    string CorpusVersion,
    string CorpusSha256,
    string State,
    IReadOnlyList<SemanticFact> Facts);
internal sealed record TypedAssertion(
    string AssertionId,
    string Kind,
    string Outcome,
    string FactType,
    string? FactId = null,
    JsonElement? Expected = null,
    JsonElement? Actual = null);
internal sealed record AssertionResults(string SchemaId, string ProtocolId, IReadOnlyList<TypedAssertion> Assertions);
internal sealed record AssertionCounts(int Total, int Passed, int Failed);
internal sealed record SanitizedResult(
    string SchemaId,
    string ProtocolId,
    string CandidateCommit,
    long CandidateArtifactByteLength,
    string CandidateArtifactSha256,
    string EvaluatorCommit,
    string EvaluatorFilesSha256,
    string ScorerId,
    string ScorerVersion,
    string AdapterId,
    string AdapterVersion,
    string CorpusId,
    string CorpusVersion,
    string CorpusSha256,
    string TerminalResult,
    string? FailureStage,
    AssertionCounts AssertionCounts,
    IReadOnlyList<string>? FailureCategories,
    string ContaminationState);
internal sealed record ScoreOutcome(SanitizedResult Result, AssertionResults? Assertions);
internal sealed record CalibrationCaseResult(
    string CaseId,
    string ExpectedTerminal,
    string ActualTerminal,
    string? IntendedFailureCategory,
    IReadOnlyList<string>? ObservedFailureCategories,
    bool Passed);
internal sealed record CalibrationResults(
    string SchemaId,
    string ProtocolId,
    string SuiteId,
    IReadOnlyList<CalibrationCaseResult> Cases,
    bool Passed);
