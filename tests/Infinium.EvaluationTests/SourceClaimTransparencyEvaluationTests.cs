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
            JsonElement expected = package.Oracle;
            Assert.AreEqual(expected.GetProperty("expected_identity").GetProperty("acquisition_run_id").GetString(),
                actual.Scenarios[0].Extraction.AcquisitionRunId.Value);
            foreach (JsonElement scenarioOracle in expected.GetProperty("scenarios").EnumerateArray())
            {
                string transcriptId = scenarioOracle.GetProperty("transcript_id").GetString()!;
                SourceClaimScenarioResult scenario = actual.Scenarios.Single(x => x.TranscriptId == transcriptId);
                HashSet<string> expectedAdmitted = scenarioOracle.GetProperty("admitted_proposal_ids").EnumerateArray()
                    .Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
                HashSet<string> actualAdmitted = scenario.Extraction.ClaimProposals
                    .Where(x => x.State == ProposalAdmissionState.Admitted).Select(x => x.ProposalId.Value)
                    .ToHashSet(StringComparer.Ordinal);
                Assert.IsTrue(expectedAdmitted.SetEquals(actualAdmitted), transcriptId + " admitted proposals");
                Assert.AreEqual(scenarioOracle.GetProperty("expected_abstention_count").GetInt32(), scenario.Extraction.Abstentions.Count);
                Assert.AreEqual(scenarioOracle.GetProperty("expected_gap_count").GetInt32(), scenario.Extraction.Gaps.Count);
                Assert.AreEqual(scenarioOracle.GetProperty("contradiction_evidence_ids").GetArrayLength(),
                    scenario.Extraction.ContradictionEvidenceIds.Count);
                int actualApplied = scenario.Extraction.AdmissionLinks.Count(x => x.State == ProposalAdmissionState.Admitted);
                Assert.AreEqual(scenarioOracle.GetProperty("expected_application_link_count").GetInt32(), actualApplied);
            }
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
