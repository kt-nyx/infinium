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
            "[switch] $Wp4OwnerReviewHandoff",
            "[switch] $OwnerTestProcessCleanup",
            "merge-base --is-ancestor",
            "layer6-changed-paths.json",
            "docs/evaluation/specifications/semantic-fixture-catalog.md",
            "fixtures/public/provider/source-claims/",
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
        StringAssert.Contains(script, "or record accepted WP4 and authorize non-live M1/S6/WP8 only");
        StringAssert.Contains(script, "`M1/S6/WP8` accumulated non-live verification and pre-live review only");
        StringAssert.Contains(script, "Accepted `M1/S6/WP4` qualification");
        StringAssert.Contains(script, "1fe62bbad155b4e9b8fc2d1056fee14a15dbc11b");
        StringAssert.Contains(script, "3f148b76fef94c077293d863a06447bb22b395997db2b09dea291193c1598390");
        StringAssert.Contains(script, "no further Credential Manager operation is authorized");
        StringAssert.Contains(script, "no provider request is authorized");
        StringAssert.Contains(script, "Wp4OwnerReviewHandoff requires exactly one changed candidate docs/current-state.md.");
        StringAssert.Contains(script, "fresh qualification-manifest consumer binding and owner-review preparation only");
        StringAssert.Contains(script, "03ae6929bad069c7c9e351b2ed5bd361e31b89e7");
        StringAssert.Contains(script, "c6e9226e-3d95-496c-bda6-c9142bb6b980");
        StringAssert.Contains(script, "Do not append an owner marker or execute `CredentialNative` during preparation");
        StringAssert.Contains(script, "OwnerTestProcessCleanup requires exactly one changed candidate docs/execution-policy.md");
        StringAssert.Contains(script, "Never terminate by process name alone");
        StringAssert.Contains(script, "JsonDocumentOptions");
        StringAssert.Contains(script, "Assert-NoDuplicateJsonProperties");
    }
}
