using System.Text;
using System.Text.Json;
using Infinium.Application.Provider;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateLlmTransparencyProviderProvenanceEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void CandidateLlmTransparencyExposesRawIntermediatesGapsAndClaimBoundaries()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load();
        CandidateInvestigationResult result = CandidateInvestigationEngine.Execute(input, transcripts);
        string json = Encoding.UTF8.GetString(CandidateInvestigationTransparencyRenderer.RenderJson(result));
        string human = CandidateInvestigationTransparencyRenderer.RenderHuman(result);
        StringAssert.Contains(json, "raw_intermediate_ids");
        StringAssert.Contains(json, "source_acquisition_links");
        StringAssert.Contains(json, "rejected-hostile-authority");
        StringAssert.Contains(json, "rejected-deleted-audit-only");
        StringAssert.Contains(json, "private_verdict\":\"not-performed");
        StringAssert.Contains(human, "no finding, case, taxonomy, readiness, reliability, or private-evaluation authority");
        Assert.IsFalse(json.Contains("held-out", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("independently validated", StringComparison.OrdinalIgnoreCase));
    }

    private static (CandidateInvestigationExecutionInput, CandidateInvestigationRetainedTranscript[]) Load()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        string directory = Path.Combine(current!.FullName, "fixtures", "public", "provider",
            "candidate-investigations", "S6-CANDIDATE-VAL-v2");
        CandidateInvestigationExecutionInput input = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "retained-transcripts.v1.json")));
        return (input, JsonSerializer.Deserialize<CandidateInvestigationRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), SourceClaimContextMinimizer.JsonOptions)!);
    }
}
