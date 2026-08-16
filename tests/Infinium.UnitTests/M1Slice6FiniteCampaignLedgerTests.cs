using Infinium.Persistence;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class M1Slice6FiniteCampaignLedgerTests
{
    private const string CampaignId = "infinium.m1-s6.finite-live-campaign/test";
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-08-15T16:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset CredentialExpiry = DateTimeOffset.Parse("2026-08-17T15:25:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset CampaignExpiry = DateTimeOffset.Parse("2026-08-22T23:59:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [TestMethod]
    public void CompleteCampaignIsSequentialFiniteDurableAndHasNoFourthCall()
    {
        string path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            RunStage(ledger, M1Slice6CampaignStage.Qualification, 100_000_000, 90_000_000, Start.AddMinutes(5));
            RunStage(ledger, M1Slice6CampaignStage.SourceClaimExtraction, 500_000_000, 400_000_000, Start.AddMinutes(6));
            RunStage(ledger, M1Slice6CampaignStage.CandidateInvestigation, 500_000_000, 300_000_000, Start.AddMinutes(7));

            Assert.AreEqual(M1Slice6CampaignState.Completed, ledger.Current.State);
            Assert.AreEqual(3L, ledger.Current.ProviderCallCount);
            Assert.AreEqual(3L, ledger.Current.DnsResolutionCount);
            Assert.AreEqual(790_000_000L, ledger.Current.SettledNanoUsd);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.CandidateInvestigation, 1, Start.AddMinutes(8)));

            M1Slice6FiniteCampaignLedger reopened = new(path, CampaignId, CampaignExpiry, CredentialExpiry, Start.AddMinutes(9));
            Assert.AreEqual(ledger.Current.EventHash, reopened.Current.EventHash);
            Assert.AreEqual(M1Slice6CampaignState.Completed, reopened.Current.State);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void PossibleStartConsumesCallRetainsFullHoldAndCannotRetryOrRollover()
    {
        string path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification, 140_000_000, Start.AddMinutes(5));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, Start.AddMinutes(5).AddSeconds(1));
            ledger.StopAfterAmbiguousStart(M1Slice6CampaignStage.Qualification, Start.AddMinutes(5).AddSeconds(2));

            Assert.AreEqual(M1Slice6CampaignState.Stopped, ledger.Current.State);
            Assert.AreEqual(1L, ledger.Current.ProviderCallCount);
            Assert.AreEqual(140_000_000L, ledger.Current.ReservedNanoUsd);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification, 1, Start.AddMinutes(6)));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.BeginCredentialExecutionHandoff(Start.AddMinutes(6)));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void ExpiryIsCheckedAtAdmissionCredentialAndStageBeginNotCompletion()
    {
        string path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = new(path, CampaignId, CampaignExpiry, CredentialExpiry, Start);
            ledger.RecordIndependentReview(Start.AddMinutes(1));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.AdmitCampaign(CampaignExpiry));
        }
        finally { Cleanup(path); }

        path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = new(path, CampaignId, CampaignExpiry, CredentialExpiry, Start);
            ledger.RecordIndependentReview(Start.AddMinutes(1));
            ledger.AdmitCampaign(Start.AddMinutes(2));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.BeginCredentialExecutionHandoff(CredentialExpiry));
        }
        finally { Cleanup(path); }

        path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification, 1, CampaignExpiry.AddTicks(-1));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, CampaignExpiry.AddSeconds(1));
            ledger.AcceptStageEvidence(M1Slice6CampaignStage.Qualification, 1, CampaignExpiry.AddSeconds(2));
            Assert.AreEqual(M1Slice6CampaignState.StageAccepted, ledger.Current.State);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.SourceClaimExtraction, 1, CampaignExpiry));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void TamperTruncationAndCounterResetAreRejected()
    {
        string path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification, 1, Start.AddMinutes(5));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, Start.AddMinutes(6));
            string text = File.ReadAllText(path);
            File.WriteAllText(path, text.Replace("\"provider_call_count\":1", "\"provider_call_count\":0", StringComparison.Ordinal));
            Assert.ThrowsExactly<InvalidDataException>(() => new M1Slice6FiniteCampaignLedger(
                path, CampaignId, CampaignExpiry, CredentialExpiry, Start.AddMinutes(7)));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void ExactStageAndAggregateCeilingsAreClosed()
    {
        Assert.AreEqual(new M1Slice6CampaignStageLimits(16_384, 20_480, 256, 262_144, 140_000_000, 60_000),
            M1Slice6CampaignStageLimits.For(M1Slice6CampaignStage.Qualification));
        Assert.AreEqual(new M1Slice6CampaignStageLimits(65_536, 73_728, 4_096, 1_048_576, 600_000_000, 120_000),
            M1Slice6CampaignStageLimits.For(M1Slice6CampaignStage.SourceClaimExtraction));
        CollectionAssert.AreEqual(new long[] { 3, 3, 1_340_000_000 }, new long[]
        {
            M1Slice6FiniteCampaignLedger.AggregateMaximumProviderCalls,
            M1Slice6FiniteCampaignLedger.AggregateMaximumDnsResolutions,
            M1Slice6FiniteCampaignLedger.AggregateMaximumNanoUsd,
        });
    }

    private static M1Slice6FiniteCampaignLedger ReadyThroughCredential(string path)
    {
        M1Slice6FiniteCampaignLedger ledger = new(path, CampaignId, CampaignExpiry, CredentialExpiry, Start);
        ledger.RecordIndependentReview(Start.AddMinutes(1));
        ledger.AdmitCampaign(Start.AddMinutes(2));
        ledger.BeginCredentialExecutionHandoff(Start.AddMinutes(3));
        ledger.AcceptCredentialEvidence(Start.AddMinutes(4));
        return ledger;
    }

    private static void RunStage(M1Slice6FiniteCampaignLedger ledger, M1Slice6CampaignStage stage,
        long reserve, long settle, DateTimeOffset now)
    {
        ledger.ReserveStage(stage, reserve, now);
        ledger.LatchPossibleStart(stage, now.AddSeconds(1));
        ledger.AcceptStageEvidence(stage, settle, now.AddSeconds(2));
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), "infinium-campaign-" + Guid.NewGuid().ToString("N"), "ledger.jsonl");
    private static void Cleanup(string path)
    {
        string? root = Path.GetDirectoryName(path);
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
