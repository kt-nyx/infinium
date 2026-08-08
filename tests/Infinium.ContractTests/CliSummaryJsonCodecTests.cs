using System.Text;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CliSummaryJsonCodecTests
{
    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void StableCliSummaryRoundTripsWithDurationAndSeparatedCost()
    {
        CliSummaryDocumentContract expected = CreateValidSummary();

        byte[] json = CliSummaryJsonCodec.Serialize(expected);
        CliSummaryDocumentContract actual = CliSummaryJsonCodec.Deserialize(json);

        Assert.AreEqual(expected.RunId, actual.RunId);
        Assert.AreEqual(17, actual.DurationMs);
        Assert.AreEqual(3, actual.Cost.ProviderInputTokens);
        Assert.AreEqual(10, actual.Cost.CalculatedActualNanoUsd);
        CollectionAssert.AreEqual(json, CliSummaryJsonCodec.Serialize(actual));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Fault")]
    public void StableCliSummaryRejectsExitAndCostAvailabilityDrift()
    {
        string json = Encoding.UTF8.GetString(CliSummaryJsonCodec.Serialize(CreateValidSummary()));
        string wrongExitCode = json.Replace("\"exit_code\": 0", "\"exit_code\": 5", StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(
            () => CliSummaryJsonCodec.Deserialize(Encoding.UTF8.GetBytes(wrongExitCode)));

        string unresolvedWithCost = json.Replace(
            "\"unresolved_hold\": false",
            "\"unresolved_hold\": true",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(
            () => CliSummaryJsonCodec.Deserialize(Encoding.UTF8.GetBytes(unresolvedWithCost)));
    }

    private static CliSummaryDocumentContract CreateValidSummary()
    {
        return new CliSummaryDocumentContract(
            ContractConstants.CliSummarySchemaId,
            "1",
            "run-1",
            "completed",
            (int)CliExitCode.Success,
            new TypedOutputCountsContract(
                1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                1, 1, 1, 1, 1),
            new CoverageStateCountsContract(1, 0, 0, 0, 0, 0),
            17,
            new CliCostContract(3, 2, 1, 1, 0, 10, 10, false),
            "no-readiness-evaluation",
            true);
    }
}
