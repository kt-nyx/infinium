using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Coordinator;

namespace Infinium.Tests;

[TestClass]
public sealed class SourceClaimAdmissionIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void SourceClaimAdmissionExecutesRegisteredPackagesWithoutTransport()
    {
        foreach (string package in new[] { "S6-CLAIM-DEV-v1", "S6-CLAIM-VAL-v1" })
        {
            (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) =
                LoadCurrentContractPackage(package);
            SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, transcripts);
            Assert.IsFalse(result.NetworkUsed);
            Assert.IsFalse(result.CredentialUsed);
            Assert.IsFalse(result.SourceRefreshUsed);
            Assert.IsTrue(result.Scenarios.All(x => x.Extraction.OwnerId.Value == input.AcquisitionRunId));
            Assert.IsTrue(result.Scenarios.All(x => x.Extraction.SourceRevisionId.Value == input.SourceRevisionId));
            byte[] json = SourceClaimTransparencyRenderer.RenderJson(result);
            StringAssert.Contains(System.Text.Encoding.UTF8.GetString(json), "private_verdict\":\"not-performed");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void SourceClaimReplayIsByteStableOrDegradesAuditOnlyOnDrift()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) =
            LoadCurrentContractPackage("S6-CLAIM-DEV-v1");
        SourceClaimScenarioResult first = SourceClaimAcquisitionEngine.Replay(input, transcripts[0], transcripts[0].ResponseFingerprint);
        SourceClaimScenarioResult second = SourceClaimAcquisitionEngine.Replay(input, transcripts[0], transcripts[0].ResponseFingerprint);
        Assert.AreEqual(first.CanonicalExtractionSha256, second.CanonicalExtractionSha256);
        Assert.AreEqual("retained-response", second.ReplayState);
        Assert.AreEqual("audit-only", SourceClaimAcquisitionEngine.Replay(input, transcripts[0], new string('0', 64)).ReplayState);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void SourceClaimAdmissionNoModelCoordinatorPathCannotFabricateProviderUse()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) =
            LoadCurrentContractPackage("S6-CLAIM-DEV-v1");
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionCoordinator.NoModel(
            input, transcripts.Single(x => x.TranscriptId == "dev-04"));
        Assert.AreEqual("not-applicable", result.Scenarios.Single().ReplayState);
        Assert.AreEqual(0, result.Scenarios.Single().Extraction.ClaimProposals.Count);
        Assert.IsFalse(result.NetworkUsed);
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionCoordinator.NoModel(input, transcripts[0]));
    }

    internal static (SourceClaimExecutionInput Input, SourceClaimRetainedTranscript[] Transcripts)
        LoadCurrentContractPackage(string package)
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] historical) = LoadHistoricalPackage(package);
        Dictionary<string, SourceClaimPassageInput> passages = input.Passages.ToDictionary(
            passage => passage.PassageId, StringComparer.Ordinal);
        SourceClaimRetainedTranscript[] current = historical.Select(transcript => transcript with
        {
            Proposals = transcript.Proposals.Select(proposal => proposal.State is "proposed" or "unsupported"
                    && passages.TryGetValue(proposal.PassageId, out SourceClaimPassageInput? passage)
                    && !passage.Deleted
                ? proposal with { Claim = passage.Text }
                : proposal).ToArray(),
        }).ToArray();
        return (input, current);
    }

    internal static (SourceClaimExecutionInput Input, SourceClaimRetainedTranscript[] Transcripts)
        LoadHistoricalPackage(string package)
    {
        string directory = Path.Combine(RepositoryRoot(), "fixtures", "public", "provider", "source-claims", package);
        SourceClaimExecutionInput input = JsonSerializer.Deserialize<SourceClaimExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "retained-transcripts.v1.json")));
        return (input, JsonSerializer.Deserialize<SourceClaimRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), SourceClaimContextMinimizer.JsonOptions)!);
    }

    internal static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
