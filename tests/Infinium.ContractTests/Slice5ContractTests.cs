using System.Text;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Slice5ContractTests
{
    private static readonly Sha256Fingerprint Fingerprint = new(new string('a', 64));

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5ContractPayloadsRoundTripThroughStrictEmbeddedSchemas()
    {
        DocumentationEvidenceContract documentation = new(
            ContractConstants.DocumentationEvidenceSchemaId,
            Version(),
            Id("documentation-payload"),
            Id("run-1"),
            [], [], [], [], [], [], [], [], []);
        documentation = documentation with
        {
            PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(documentation),
        };
        CandidateAnalysisContract candidates = new(
            ContractConstants.CandidateAnalysisSchemaId,
            Version(),
            Id("candidate-payload"),
            Id("run-1"),
            Id("analyzer-1"),
            Id("population-1"),
            0,
            [], [], [], [], []);
        FindingCaseContract findings = new(
            ContractConstants.FindingCaseSchemaId,
            Version(),
            Id("finding-payload"),
            Id("run-1"),
            [], [], [], [], [], [], [], []);
        AnalysisReplayContract replay = new(
            ContractConstants.AnalysisReplaySchemaId,
            Version(),
            Id("replay-1"),
            Id("run-1"),
            ReplayMode.Clean,
            ReplayState.Partial,
            AuditabilityState.Partial,
            [], [], [], [], [], false, null);
        AnalysisExecutionInputContract execution = CreateExecutionInput();

        AssertSemanticRoundTrip(documentation, DocumentationEvidenceJsonCodec.Serialize, bytes => DocumentationEvidenceJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(candidates, CandidateAnalysisJsonCodec.Serialize, bytes => CandidateAnalysisJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(findings, FindingCaseJsonCodec.Serialize, bytes => FindingCaseJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(replay, AnalysisReplayJsonCodec.Serialize, bytes => AnalysisReplayJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(execution, AnalysisExecutionInputJsonCodec.Serialize, bytes => AnalysisExecutionInputJsonCodec.Deserialize(bytes));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void Slice5ExecutionInputRejectsAnswersUnknownPropertiesAndDuplicateProperties()
    {
        string json = Encoding.UTF8.GetString(AnalysisExecutionInputJsonCodec.Serialize(CreateExecutionInput()));
        string answerBearing = json.Replace(
            "\"run_id\": \"run-1\"",
            "\"run_id\": \"run-1\",\n  \"oracle\": \"forbidden\"",
            StringComparison.Ordinal);
        Assert.AreNotEqual(json, answerBearing, "The unknown-property mutation must alter the serialized payload.");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            AnalysisExecutionInputJsonCodec.Deserialize(Encoding.UTF8.GetBytes(answerBearing)));

        string duplicate = json.Replace(
            "\"schema_version\": \"1.0.0\"",
            "\"schema_version\": \"1.0.0\",\n  \"schema_version\": \"1.0.0\"",
            StringComparison.Ordinal);
        Assert.AreNotEqual(json, duplicate, "The duplicate-property mutation must alter the serialized payload.");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            AnalysisExecutionInputJsonCodec.Deserialize(Encoding.UTF8.GetBytes(duplicate)));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5ExecutionInputRepresentsFixedStressPopulationsExactly()
    {
        AnalysisExecutionInputContract value = CreateExecutionInput() with
        {
            Limits = new AnalysisExecutionLimitsContract(1_000_000, 2_000_000, 100_000, 100_000, 120_000),
        };

        byte[] encoded = AnalysisExecutionInputJsonCodec.Serialize(value);
        AnalysisExecutionInputContract decoded = AnalysisExecutionInputJsonCodec.Deserialize(encoded);
        Assert.AreEqual(1_000_000, decoded.Limits.MaximumEntities);
        Assert.AreEqual(2_000_000, decoded.Limits.MaximumEdges);
        Assert.AreEqual(100_000, decoded.Limits.MaximumTruthRows);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5ReplayModesBindPriorRunIdentityExactly()
    {
        AnalysisExecutionInputContract retained = CreateExecutionInput() with
        {
            Mode = ReplayMode.RetainedDownstreamReplay,
            PriorRunId = Id("prior-run"),
        };
        AssertSemanticRoundTrip(
            retained,
            AnalysisExecutionInputJsonCodec.Serialize,
            bytes => AnalysisExecutionInputJsonCodec.Deserialize(bytes));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            AnalysisExecutionInputJsonCodec.Serialize(retained with { PriorRunId = null }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            AnalysisExecutionInputJsonCodec.Serialize(CreateExecutionInput() with { PriorRunId = Id("prior-run") }));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5CleanReplayCanCloseWithoutComparedRunIdentity()
    {
        AnalysisReplayContract replay = new(
            ContractConstants.AnalysisReplaySchemaId,
            Version(),
            Id("replay-1"),
            Id("run-1"),
            ReplayMode.Clean,
            ReplayState.CompleteClean,
            AuditabilityState.Complete,
            [], [], [], [], [], true, null);

        AssertSemanticRoundTrip(
            replay,
            AnalysisReplayJsonCodec.Serialize,
            bytes => AnalysisReplayJsonCodec.Deserialize(bytes));
    }

    private static AnalysisExecutionInputContract CreateExecutionInput()
    {
        return new AnalysisExecutionInputContract(
            ContractConstants.AnalysisExecutionInputSchemaId,
            Version(),
            Id("execution-1"),
            Id("run-1"),
            Reference("snapshot-1"),
            Reference("bethesda-1"),
            [],
            [],
            Reference("configuration-1"),
            Reference("manifest-1"),
            ReplayMode.Clean,
            null,
            7,
            new AnalysisExecutionLimitsContract(1_000, 2_000, 100, 100, 1_000),
            [
                new("provider", BoundaryUseState.NotUsed, "local-only"),
                new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
                new("nexus", BoundaryUseState.NotUsed, "local-only"),
                new("loot", BoundaryUseState.NotUsed, "not configured"),
            ]);
    }

    private static ArtifactReferenceContract Reference(string id) =>
        new(Id(id), Version(), Fingerprint, "retained");

    private static void AssertSemanticRoundTrip<T>(
        T value,
        Func<T, byte[]> serialize,
        Func<byte[], T> deserialize)
    {
        byte[] encoded = serialize(value);
        T decoded = deserialize(encoded);
        CollectionAssert.AreEqual(encoded, serialize(decoded));
    }

    private static ContractVersion Version() => new(1, 0, 0);

    private static OpaqueId Id(string value) => new(value);
}
