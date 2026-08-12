using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderLayer6VerifierContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void Layer6ReviewHasCandidateBoundInterfaceAndRetainedReports()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");

        string[] requiredInterfaceAndEvidence =
        [
            "'Layer6Review'",
            "[string] $BaselineCommit",
            "[string] $CandidateCommit",
            "[switch] $HandoffCloseout",
            "[switch] $OwnerTestProcessCleanup",
            "merge-base --is-ancestor",
            "layer6-changed-paths.json",
            "layer6-relative-links.json",
            "layer6-changed-json.json",
            "layer6-status-claims.json",
            "layer6-gap-inventory.json",
            "layer6-private-archive-absence.json",
            "candidate_bound = $true",
            "network_permitted = $false",
            "credential_access_permitted = $false",
        ];

        foreach (string required in requiredInterfaceAndEvidence)
        {
            StringAssert.Contains(script, required);
        }

        StringAssert.Contains(script, "Test-Wp1AllowedPath");
        StringAssert.Contains(script, "Test-Wp1ProtectedPath");
        StringAssert.Contains(script, "isHandoffCurrentState");
        StringAssert.Contains(script, "HandoffCloseout current state must record accepted WP1");
        StringAssert.Contains(script, "OwnerTestProcessCleanup requires exactly one changed candidate docs/execution-policy.md");
        StringAssert.Contains(script, "Never terminate by process name alone");
        StringAssert.Contains(script, "JsonDocumentOptions");
        StringAssert.Contains(script, "Assert-NoDuplicateJsonProperties");
    }
}
