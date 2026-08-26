using Infinium.Application.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ResultsReviewApplicationContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void ResultsAndReviewSurfaceIsClosedBoundedAndPathFree()
    {
        string application = TestRepository.Read(
            "contracts", "protobuf", "infinium", "application", "v1", "application.proto");
        string[] requiredRpcs =
        [
            "GetResultOverview", "ListResultItems", "GetResultDetail", "GetEvidenceExpansion",
            "GetFocusedModView", "GetReviewState", "SubmitReviewEvent", "ListAssumptions",
            "SubmitAssumptionEvent", "StartTargetedVerification", "CreateStructuredExport",
            "GetStructuredExport",
        ];
        foreach (string rpc in requiredRpcs)
        {
            StringAssert.Contains(application, $"rpc {rpc}(");
        }
        StringAssert.Contains(application, "reserved \"sql\", \"path\", \"url\", \"object_type\", \"object_id\", \"query\";");
        StringAssert.Contains(application, "sharing_class = 7;");
        StringAssert.Contains(application, "string llm_involvement_state = 7;");
        Assert.IsFalse(application.Contains("rpc Download", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("bytes raw_payload", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("string payload_path =", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("string sql =", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("string query =", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void PhaseCComputedVersionAxesAndFingerprintAreExact()
    {
        Assert.AreEqual("1.13.0", ProtocolConstants.StorageContractVersion);
        Assert.AreEqual(
            "8e6b8b3cdeeb634a744d57be49fcfb6b6d77d3fbbeb9afb020c9e17a6b9336bf",
            Convert.ToHexStringLower(ProtocolConstants.Version.SchemaFingerprintSha256.Span));
    }
}
