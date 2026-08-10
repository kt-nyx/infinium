using System.Text;
using Infinium.Application.Analysis;
using Infinium.Application.Evaluation;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Slice5OutputQueryContractTests
{
    [TestMethod]
    [TestCategory("M1Contract")]
    public void Slice5OutputHumanAndJsonContractsCarryEquivalentQualifiedSemantics()
    {
        RunOutputContract output = RunOutputJsonCodecTests.CreateValidOutput();
        CliSummaryDocumentContract summary = new(
            ContractConstants.CliSummarySchemaId, "1", output.RunId, "completed",
            (int)CliExitCode.Success,
            new TypedOutputCountsContract(
                1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0),
            new CoverageStateCountsContract(1, 0, 0, 0, 0, 0),
            1,
            new CliCostContract(0, 0, 0, 0, 0, 0, 0, false),
            "no-readiness-evaluation",
            true);

        byte[] outputBytes = RunOutputJsonCodec.Serialize(output);
        byte[] summaryBytes = CliSummaryJsonCodec.Serialize(summary);
        string rendered = AnalysisOutputRenderer.Render(
            RunOutputJsonCodec.Deserialize(outputBytes),
            CliSummaryJsonCodec.Deserialize(summaryBytes));

        StringAssert.Contains(rendered, $"run {output.RunId} state={output.RunState} outcome={summary.Outcome}");
        StringAssert.Contains(rendered, "coverage populations=1 gaps=0 failures=0");
        StringAssert.Contains(rendered, "no-safety-guarantee=true");
        StringAssert.Contains(rendered, "provider:not-used");
        const string outputMarker = "canonical-run-output-json=";
        const string summaryMarker = "canonical-cli-summary-json=";
        int outputStart = rendered.IndexOf(outputMarker, StringComparison.Ordinal) + outputMarker.Length;
        int summaryLine = rendered.IndexOf(summaryMarker, outputStart, StringComparison.Ordinal);
        string renderedOutput = rendered[outputStart..summaryLine].TrimEnd('\r', '\n');
        string renderedSummary = rendered[(summaryLine + summaryMarker.Length)..].TrimEnd('\r', '\n');
        CollectionAssert.AreEqual(outputBytes, Encoding.UTF8.GetBytes(renderedOutput));
        CollectionAssert.AreEqual(summaryBytes, Encoding.UTF8.GetBytes(renderedSummary));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    public void Slice5QueryProtocolIsTypedBoundedAndCarriesNoDatabaseSurface()
    {
        Assert.IsNotNull(ApplicationService.Descriptor.FindMethodByName("GetAnalysisOutput"));
        Assert.IsNotNull(ApplicationService.Descriptor.FindMethodByName("ListAnalysisArtifacts"));

        ListAnalysisArtifactsRequest request = new()
        {
            RunId = new() { Value = "run-1" },
            RequestedPageSize = 25,
            ExpectedProjectionVersion = new() { Value = "1" },
            Sort = AnalysisArtifactSort.RankDescendingIdentityAscending,
        };
        Assert.AreEqual(25u, request.RequestedPageSize);
        Assert.AreEqual(AnalysisArtifactSort.RankDescendingIdentityAscending, request.Sort);
        Assert.IsNull(ListAnalysisArtifactsRequest.Descriptor.FindFieldByName("sql"));
        Assert.IsNull(ListAnalysisArtifactsRequest.Descriptor.FindFieldByName("path"));
        Assert.IsNull(ListAnalysisArtifactsRequest.Descriptor.FindFieldByName("object_type"));
        Assert.IsNull(ListAnalysisArtifactsRequest.Descriptor.FindFieldByName("object_id"));
        Assert.IsTrue(request.RequestedPageSize <= AnalysisV1WorkAssignment.AbsoluteMaximumQueryItems);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    public void Slice5OutputAssignmentRejectsExternalUseAndEveryExceededBound()
    {
        AnalysisV1WorkAssignment valid = ValidAssignment();
        AnalysisPublicationBuilder.ValidateAssignment(valid);

        AnalysisExecutionInputContract external = valid.ExecutionInput with
        {
            Boundaries = valid.ExecutionInput.Boundaries.Select(item => item.BoundaryId == "provider"
                ? item with { State = BoundaryUseState.Used, Reason = "forbidden" }
                : item).ToArray(),
        };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            AnalysisPublicationBuilder.ValidateAssignment(valid with { ExecutionInput = external }));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisPublicationBuilder.ValidateAssignment(
            valid with { MaximumQueryItems = AnalysisV1WorkAssignment.AbsoluteMaximumQueryItems + 1 }));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisPublicationBuilder.ValidateAssignment(
            valid with { MaximumInputBytes = AnalysisV1WorkAssignment.AbsoluteMaximumInputBytes + 1 }));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisPublicationBuilder.ValidateAssignment(
            valid with { MaximumInputBytes = 2 }));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisPublicationBuilder.ValidateAssignment(
            valid with
            {
                DocumentationEvidence = valid.DocumentationEvidence with
                {
                    SchemaId = ContractConstants.CandidateAnalysisSchemaId,
                },
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisPublicationBuilder.ValidateAssignment(
            valid with { MaximumOutputBytes = AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes + 1 }));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisPublicationBuilder.ValidateAssignment(
            valid with { MaximumOutputBytes = AnalysisV1WorkAssignment.MinimumTerminalOutputBytes - 1 }));
    }

    private static AnalysisV1WorkAssignment ValidAssignment()
    {
        ArtifactReferenceContract Reference(string id) => new(
            new OpaqueId(id), new ContractVersion(1, 0, 0), new Sha256Fingerprint(new string('a', 64)), "retained");
        AnalysisExecutionInputContract input = new(
            ContractConstants.AnalysisExecutionInputSchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId("execution-input-1"),
            new OpaqueId("run-1"),
            Reference("snapshot-1"),
            Reference("bethesda-1"),
            [Reference("source-1")],
            [Reference("analyzer-1")],
            Reference("configuration-1"),
            Reference("manifest-1"),
            ReplayMode.Clean,
            null,
            7,
            new AnalysisExecutionLimitsContract(1000, 2000, 1000, 100_000, 120_000),
            [
                new ExecutionBoundaryContract("provider", BoundaryUseState.NotUsed, "local-only"),
                new ExecutionBoundaryContract("hosted-search", BoundaryUseState.NotUsed, "local-only"),
                new ExecutionBoundaryContract("nexus", BoundaryUseState.NotUsed, "local-only"),
                new ExecutionBoundaryContract("loot", BoundaryUseState.NotUsed, "local-only"),
            ]);
        return new AnalysisV1WorkAssignment(
            1, "assignment-1", input, "context-1",
            new RetainedAnalysisPayloadSeal("documentation-1", ContractConstants.DocumentationEvidenceSchemaId, "1.0.0", new string('a', 64), 1),
            new RetainedAnalysisPayloadSeal("candidate-1", ContractConstants.CandidateAnalysisSchemaId, "1.0.0", new string('b', 64), 1),
            new RetainedAnalysisPayloadSeal("finding-case-1", ContractConstants.FindingCaseSchemaId, "1.0.0", new string('c', 64), 1),
            new string('d', 40), DateTimeOffset.UnixEpoch, AnalysisTerminalOutcome.Completed,
            "bounded contract test", 1024, AnalysisV1WorkAssignment.MinimumTerminalOutputBytes, 100);
    }
}
