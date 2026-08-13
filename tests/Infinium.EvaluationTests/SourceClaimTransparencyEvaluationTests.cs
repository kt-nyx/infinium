using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class SourceClaimTransparencyEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void LlmClaimTransparencyMatchesIndependentlyFrozenPublicOracles()
    {
        foreach (string packageId in new[] { "S6-CLAIM-DEV-v1", "S6-CLAIM-VAL-v1" })
        {
            SourceClaimFixturePackage package = SourceClaimFixtureReader.Read(Path.Combine(
                RepositoryRoot(), "fixtures", "public", "provider", "source-claims", packageId));
            SourceClaimAcquisitionResult actual = SourceClaimAcquisitionEngine.Execute(package.ExecutionInput, package.Transcripts);
            SourceClaimOracleVerifier.Verify(package, actual);
            string human = SourceClaimTransparencyRenderer.RenderHuman(actual);
            StringAssert.Contains(human, "network not used");
            StringAssert.Contains(human, "private verdict not performed");
        }
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void Slice5ProviderAdmissionRemainsHostOwnedAndModelClaimsRemainUntrusted()
    {
        SourceClaimFixturePackage package = SourceClaimFixtureReader.Read(Path.Combine(
            RepositoryRoot(), "fixtures", "public", "provider", "source-claims", "S6-CLAIM-VAL-v1"));
        SourceClaimAcquisitionResult actual = SourceClaimAcquisitionEngine.Execute(package.ExecutionInput, package.Transcripts);
        Assert.AreEqual(0, actual.Scenarios.Sum(x => x.Extraction.ClaimProposals.Count(p => p.State == ProposalAdmissionState.Admitted)));
        Assert.IsTrue(actual.Scenarios.SelectMany(x => x.Extraction.ClaimProposals)
            .All(x => x.Reason != "Requires host validation"));
        Assert.IsFalse(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts", "json-schema", "source-claim-extraction.v1.schema.json"))
            .Contains("finding", StringComparison.OrdinalIgnoreCase));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
