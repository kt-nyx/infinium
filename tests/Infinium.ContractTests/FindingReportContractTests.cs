using Infinium.Application.Analysis;
using Infinium.Application.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FindingReportContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void ProjectionRetainsPositiveNegativeAbstentionCoverageAndReplayMeaning()
    {
        ScopeReversionAnalysisContract analysis =
            ScopeReversionComposition.Execute(ScopeReversionFixtureReader.Read(TestRepository.Root).Request).Analysis;

        IReadOnlyList<FindingReportDocument> reports =
            FindingReportProjection.Project(analysis);

        Assert.IsTrue(reports.Any(item => item.State == FindingReportState.SupportedFinding));
        Assert.IsTrue(reports.Any(item => item.State == FindingReportState.ResolvedNegative));
        Assert.IsTrue(reports.Any(item => item.State == FindingReportState.Abstention));
        Assert.IsTrue(reports.All(item => item.Coverage.Count > 0));
        Assert.IsTrue(reports.All(item => item.UnsupportedOrNotEstablished.Count > 0));
        Assert.IsTrue(reports.All(item => item.Provenance.ReplayEquivalent));
        Assert.IsTrue(reports.All(item =>
            item.Provenance.CanonicalArtifactRole == "raw-run-output-is-canonical"));
        Assert.AreEqual(
            reports.Count,
            reports.Select(item => item.ReportId).Distinct().Count(),
            "Every projected condition must retain its own stable report identity.");
        Assert.IsTrue(reports.SelectMany(item => item.Coverage).All(item =>
            item.Denominator == item.Completed
                + item.CompletedWithGaps
                + item.Failed
                + item.SkippedOrUnsupported));
        FindingReportDocument supported =
            reports.First(item => item.State == FindingReportState.SupportedFinding);
        Assert.AreEqual(FindingSeverity.Moderate, supported.Assessment.Severity);
        Assert.AreEqual(
            AnalysisConfidence.StronglySupported,
            supported.Assessment.Confidence);
        StringAssert.Contains(supported.Assessment.CalibrationBoundary, "not calibrated");
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void ContractRejectsImpossibleCoverageAndEmptyFailure()
    {
        ScopeReversionAnalysisContract analysis =
            ScopeReversionComposition.Execute(ScopeReversionFixtureReader.Read(TestRepository.Root).Request).Analysis;
        FindingReportDocument supported = FindingReportProjection.Project(analysis)
            .First(item => item.State == FindingReportState.SupportedFinding);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            FindingReportContract.Validate(supported with
            {
                Coverage = [supported.Coverage[0] with
                {
                    Completed = supported.Coverage[0].Completed + 1,
                }],
            }));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            FindingReportContract.Validate(supported with
            {
                State = FindingReportState.Failure,
                FindingId = null,
                Assessment = supported.Assessment with
                {
                    Severity = FindingSeverity.Unspecified,
                    Confidence = AnalysisConfidence.Unspecified,
                },
                Failures = [],
            }));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CanonicalCodecRejectsMissingEvidenceAndPreservesHostileTextAsData()
    {
        ScopeReversionAnalysisContract analysis =
            ScopeReversionComposition.Execute(ScopeReversionFixtureReader.Read(TestRepository.Root).Request).Analysis;
        FindingReportDocument report = FindingReportProjection.Project(analysis)[0];
        FindingReportDocument hostile = report with
        {
            Conclusion = "<script>do not execute</script>",
        };

        byte[] bytes = FindingReportJsonCodec.Serialize(hostile);
        FindingReportDocument replay = FindingReportJsonCodec.Deserialize(bytes);
        Assert.AreEqual(hostile.Conclusion, replay.Conclusion);
        Assert.IsFalse(System.Text.Encoding.UTF8.GetString(bytes).Contains(
            "\"conclusion\": null",
            StringComparison.Ordinal));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            FindingReportJsonCodec.Serialize(report with
            {
                UnsupportedOrNotEstablished = [],
            }));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void ProjectionIsDeterministicAndDoesNotChangeRawRunOutput()
    {
        ScopeReversionAnalysisContract analysis =
            ScopeReversionComposition.Execute(ScopeReversionFixtureReader.Read(TestRepository.Root).Request).Analysis;
        byte[] before = Infinium.Application.Serialization.ScopeReversionJsonCodec.Serialize(analysis);

        byte[][] first = FindingReportProjection.Project(analysis)
            .Select(FindingReportJsonCodec.Serialize)
            .ToArray();
        byte[][] second = FindingReportProjection.Project(analysis)
            .Select(FindingReportJsonCodec.Serialize)
            .ToArray();

        Assert.AreEqual(first.Length, second.Length);
        for (int index = 0; index < first.Length; index++)
        {
            CollectionAssert.AreEqual(first[index], second[index]);
        }
        CollectionAssert.AreEqual(
            before,
            Infinium.Application.Serialization.ScopeReversionJsonCodec.Serialize(analysis));
    }
}
