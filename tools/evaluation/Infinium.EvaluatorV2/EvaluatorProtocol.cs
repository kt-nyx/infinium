using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Application.Evaluation;

namespace Infinium.EvaluatorV2;

internal static class EvaluatorProtocol
{
    internal const string ProtocolId = "infinium.evaluator-v2/3";
    internal const string ProtocolVersion = "3.0.0";
    internal const string ScorerId = "infinium.evaluator-v2.scorer";
    internal const string ScorerVersion = "3.0.0";
    internal const string AdapterId = "infinium.evaluator-v2.slice4-reflection-adapter";
    internal const string AdapterVersion = "3.0.0";
    internal const string ProjectionId = "infinium.evaluator-v2.slice4-semantic-projection";
    internal const string ProjectionVersion = "2.0.0";
    internal const string CandidateSchema = "infinium.evaluator-v2.candidate-semantic-output/v3";
    internal const string ExpectedSchema = "infinium.evaluator-v2.expected-semantic-output/v3";
    internal const string ManifestSchema = "infinium.evaluator-v2.execution-manifest/v3";
    internal const string PreparedManifestSchema = "infinium.evaluator-v2.prepared-comparison-manifest/v3";
    internal const string CorpusManifestSchema = "infinium.evaluator-v2.corpus-execution-manifest/v3";
    internal const string AssertionsSchema = "infinium.evaluator-v2.assertion-results/v3";
    internal const string SanitizedSchema = "infinium.evaluator-v2.sanitized-result/v3";
    internal const string CalibrationSchema = "infinium.evaluator-v2.calibration-results/v3";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
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

    internal static JsonElement Primitive(string value) => ParsePrimitive(JsonSerializer.Serialize(value));
    internal static JsonElement Primitive(long value) => ParsePrimitive(value.ToString(CultureInfo.InvariantCulture));
    internal static JsonElement Primitive(double value) => ParsePrimitive(
        value == 0d ? "0" : value.ToString("R", CultureInfo.InvariantCulture));
    internal static JsonElement Primitive(bool value) => ParsePrimitive(value ? "true" : "false");
    internal static JsonElement Null() => ParsePrimitive("null");

    private static JsonElement ParsePrimitive(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal sealed record ArtifactIdentity(long ByteLength, string Sha256);
internal sealed record CandidateIdentity(
    string Commit,
    string AssemblyPath,
    ArtifactIdentity Artifact,
    string Root,
    IReadOnlyList<EvaluatorFileIdentity> Files);
internal sealed record PreparedCandidateIdentity(string Commit, ArtifactIdentity Artifact, string QualificationId);
internal sealed record EvaluatorFileIdentity(string RelativePath, long ByteLength, string Sha256);
internal sealed record EvaluatorIdentity(
    string Commit,
    string ProtocolId,
    string ScorerId,
    string ScorerVersion,
    string AdapterId,
    string AdapterVersion,
    string ProjectionId,
    string ProjectionVersion,
    string Root,
    IReadOnlyList<EvaluatorFileIdentity> Files);
internal sealed record CorpusIdentity(
    string CorpusId,
    string Version,
    string Sha256,
    string QualificationState,
    string ContaminationState);
internal sealed record PluginExecutionInput(
    string PluginName,
    int LoadOrder,
    string LocalInstalledEntityId,
    string Path,
    long ByteLength,
    string Sha256);
internal sealed record LooseProviderExecutionInput(
    string LocalInstalledEntityId,
    string ProviderKind,
    int Priority,
    string? Path,
    long? ByteLength,
    string? Sha256);
internal sealed record LooseProviderChainExecutionInput(
    string NormalizedRelativePath,
    IReadOnlyList<LooseProviderExecutionInput> Providers,
    string WinnerLocalInstalledEntityId);
internal sealed record ExecutionInput(
    IReadOnlyList<PluginExecutionInput> Plugins,
    IReadOnlyList<LooseProviderChainExecutionInput> LooseProviderChains,
    bool ArchiveMemberPopulationSupported,
    IReadOnlyList<string> UnsupportedCapabilities);
internal sealed record ExecutionManifest(
    string SchemaId,
    string ProtocolId,
    CandidateIdentity Candidate,
    EvaluatorIdentity Evaluator,
    CorpusIdentity Corpus,
    ExecutionInput Execution);
internal sealed record PreparedComparisonManifest(
    string SchemaId,
    string ProtocolId,
    PreparedCandidateIdentity Candidate,
    EvaluatorIdentity Evaluator,
    CorpusIdentity Corpus);
internal sealed record CorpusExecutionMember(string MemberId, ExecutionInput Execution, string OraclePath);
internal sealed record CorpusExecutionManifest(
    string SchemaId,
    string ProtocolId,
    CandidateIdentity Candidate,
    EvaluatorIdentity Evaluator,
    CorpusIdentity Corpus,
    IReadOnlyList<CorpusExecutionMember> Members);
internal sealed record SemanticFact(string FactId, string FactType, string ValueType, JsonElement Value);
internal sealed record CandidateSemanticOutput(
    string SchemaId,
    string ProtocolId,
    string ProjectionId,
    string ProjectionVersion,
    string CandidateCommit,
    ArtifactIdentity CandidateArtifact,
    string State,
    IReadOnlyList<SemanticFact> Facts);
internal sealed record ExpectedSemanticOutput(
    string SchemaId,
    string ProtocolId,
    string ProjectionId,
    string ProjectionVersion,
    string CorpusId,
    string CorpusVersion,
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
internal sealed record MemberCounts(int Total, int Passed, int Failed, int EvaluatorErrors);
internal sealed record SanitizedResult(
    string SchemaId,
    string ProtocolId,
    string ProjectionId,
    string ProjectionVersion,
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
    MemberCounts MemberCounts,
    IReadOnlyList<string>? FailureCategories,
    string ContaminationState);
internal sealed record ScoreOutcome(SanitizedResult Result, AssertionResults? Assertions);
internal sealed record CorpusScoreOutcome(SanitizedResult Result, IReadOnlyList<AssertionResults> MemberAssertions);
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
