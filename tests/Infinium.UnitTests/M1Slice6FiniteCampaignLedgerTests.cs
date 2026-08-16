using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Persistence;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class M1Slice6FiniteCampaignLedgerTests
{
    private const string SafetyIdentifier = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string CampaignId = "infinium.m1-s6.finite-live-campaign/test";
    private static readonly M1Slice6CampaignIdentity Identity = new(CampaignId, new string('1', 64),
        new string('2', 64), new string('3', 40), "infinium.m1-s6.wp9/test", new string('4', 64),
        "openai-platform-test", "g-test", new string('5', 64));
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
            ledger.CompleteComposedEvidence("composed-evidence", new string('c', 64), Start.AddMinutes(8));

            Assert.AreEqual(M1Slice6CampaignState.Completed, ledger.Current.State);
            Assert.AreEqual(3L, ledger.Current.ProviderCallCount);
            Assert.AreEqual(3L, ledger.Current.DnsResolutionCount);
            Assert.AreEqual(790_000_000L, ledger.Current.SettledNanoUsd);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.CandidateInvestigation,
                Reservation(M1Slice6CampaignStage.CandidateInvestigation, 1), Start.AddMinutes(8)));

            M1Slice6FiniteCampaignLedger reopened = new(path, Identity, CampaignExpiry, CredentialExpiry, Start.AddMinutes(9));
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
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification, Reservation(M1Slice6CampaignStage.Qualification, 140_000_000), Start.AddMinutes(5));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, SafetyIdentifier, Start.AddMinutes(5).AddSeconds(1));
            ledger.StopAfterAmbiguousStart(M1Slice6CampaignStage.Qualification, "ambiguous-start", Start.AddMinutes(5).AddSeconds(2));

            Assert.AreEqual(M1Slice6CampaignState.Stopped, ledger.Current.State);
            Assert.AreEqual(1L, ledger.Current.ProviderCallCount);
            Assert.AreEqual(140_000_000L, ledger.Current.ReservedNanoUsd);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification, Reservation(M1Slice6CampaignStage.Qualification, 1), Start.AddMinutes(6)));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.BeginCredentialExecutionHandoff(Start.AddMinutes(6)));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void ExpiryIsCheckedAtAdmissionCredentialHandoffReservationAndPossibleStartNotEvidenceReview()
    {
        string path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = new(path, Identity, CampaignExpiry, CredentialExpiry, Start);
            ledger.RecordIndependentReview(Start.AddMinutes(1));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.AdmitCampaign(CampaignExpiry));
        }
        finally { Cleanup(path); }

        path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = new(path, Identity, CampaignExpiry, CredentialExpiry, Start);
            ledger.RecordIndependentReview(Start.AddMinutes(1));
            ledger.AdmitCampaign(Start.AddMinutes(2));
            ledger.BeginCredentialExecutionHandoff(Start.AddMinutes(3));
            ledger.RecordCredentialEvidenceHandoff("credential-evidence", new string('6', 64),
                new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4), Start.AddMinutes(4));
            ledger.AcceptCredentialEvidence("credential-evidence", new string('6', 64), CampaignExpiry);
            Assert.AreEqual(M1Slice6CampaignState.CredentialEvidenceAccepted, ledger.Current.State);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1), CampaignExpiry.AddTicks(1)));
            Assert.AreEqual(M1Slice6CampaignState.Stopped, ledger.Current.State);
            Assert.AreEqual("campaign-expired-before-stage-reservation-terminal-stop", ledger.Current.Event);
        }
        finally { Cleanup(path); }

        path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = new(path, Identity, CampaignExpiry, CredentialExpiry, Start);
            ledger.RecordIndependentReview(Start.AddMinutes(1));
            ledger.AdmitCampaign(Start.AddMinutes(2));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.BeginCredentialExecutionHandoff(CredentialExpiry));
        }
        finally { Cleanup(path); }

        path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification, Reservation(M1Slice6CampaignStage.Qualification, 1), CampaignExpiry.AddTicks(-1));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.LatchPossibleStart(
                M1Slice6CampaignStage.Qualification, SafetyIdentifier, CampaignExpiry.AddSeconds(1)));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.SourceClaimExtraction, Reservation(M1Slice6CampaignStage.SourceClaimExtraction, 1), CampaignExpiry));
        }
        finally { Cleanup(path); }

        path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1), Start.AddMinutes(5));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, SafetyIdentifier,
                Start.AddMinutes(5).AddSeconds(1));
            ledger.RecordKnownSettlement(M1Slice6CampaignStage.Qualification, 1, 1, 1, 0,
                new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2), Start.AddMinutes(5).AddSeconds(2));
            ledger.RecordStageEvidenceHandoff(M1Slice6CampaignStage.Qualification, "review-later",
                new string('a', 64), 1, 1, 1, 0, new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2),
                Start.AddMinutes(5).AddSeconds(3));
            ledger.AcceptStageEvidence(M1Slice6CampaignStage.Qualification, "review-later",
                new string('a', 64), Start.AddMinutes(7));
            Assert.AreEqual(M1Slice6CampaignState.StageAccepted, ledger.Current.State);
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
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification, Reservation(M1Slice6CampaignStage.Qualification, 1), Start.AddMinutes(5));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, SafetyIdentifier,
                Start.AddMinutes(5).AddSeconds(30));
            string text = File.ReadAllText(path);
            File.WriteAllText(path, text.Replace("\"provider_call_count\":1", "\"provider_call_count\":0", StringComparison.Ordinal));
            Assert.ThrowsExactly<InvalidDataException>(() => new M1Slice6FiniteCampaignLedger(
                path, Identity, CampaignExpiry, CredentialExpiry, Start.AddMinutes(7)));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void RehashedIllegalSuccessorIsRejectedAfterHashValidation()
    {
        string path = TempPath();
        try
        {
            _ = ReadyThroughCredential(path);
            string[] lines = File.ReadAllLines(path);
            M1Slice6CampaignLedgerEntry final = JsonSerializer.Deserialize<M1Slice6CampaignLedgerEntry>(
                lines[^1], LedgerJson) ?? throw new AssertFailedException("Ledger fixture did not deserialize.");
            M1Slice6CampaignLedgerEntry illegal = final with
            {
                State = M1Slice6CampaignState.Ready,
                Event = "campaign-ready",
                EventHash = Rehash(final with { State = M1Slice6CampaignState.Ready, Event = "campaign-ready" }),
            };
            lines[^1] = JsonSerializer.Serialize(illegal, LedgerJson);
            File.WriteAllText(path, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
            Assert.ThrowsExactly<InvalidDataException>(() => new M1Slice6FiniteCampaignLedger(
                path, Identity, CampaignExpiry, CredentialExpiry, Start.AddMinutes(5)));
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
        CollectionAssert.AreEqual(new long[] { 147_456, 167_936, 8_448, 2_359_296 }, new long[]
        {
            M1Slice6FiniteCampaignLedger.AggregateMaximumRequestBytes,
            M1Slice6FiniteCampaignLedger.AggregateMaximumInputTokens,
            M1Slice6FiniteCampaignLedger.AggregateMaximumOutputTokens,
            M1Slice6FiniteCampaignLedger.AggregateMaximumRawResponseBytes,
        });
    }

    [TestMethod]
    public void IdentityClockDeadlineAndEveryVectorDimensionFailClosed()
    {
        string path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1) with { RequestBytes = 16_385 }, Start.AddMinutes(5)));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1) with { InputTokens = 20_481 }, Start.AddMinutes(5)));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1) with { OutputTokens = 257 }, Start.AddMinutes(5)));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1) with { RawResponseBytes = 262_145 }, Start.AddMinutes(5)));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveStage(
                M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 140_000_001), Start.AddMinutes(5)));

            ledger.ReserveStage(M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1), Start.AddMinutes(5));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.LatchPossibleStart(
                M1Slice6CampaignStage.Qualification, SafetyIdentifier, Start.AddMinutes(4)));
            ledger.LatchPossibleStart(M1Slice6CampaignStage.Qualification, SafetyIdentifier,
                Start.AddMinutes(5).AddSeconds(1));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.RecordKnownSettlement(
                M1Slice6CampaignStage.Qualification, 1, 1, 1, 0,
                new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2), Start.AddMinutes(6)));
            Assert.AreEqual(M1Slice6CampaignState.Stopped, ledger.Current.State);
            Assert.AreEqual(1L, ledger.Current.ProviderCallCount);
            Assert.AreEqual(1L, ledger.Current.ReservedNanoUsd);

            M1Slice6CampaignIdentity stale = Identity with { CredentialManifestSha256 = new string('a', 64) };
            Assert.ThrowsExactly<InvalidDataException>(() => new M1Slice6FiniteCampaignLedger(
                path, stale, CampaignExpiry, CredentialExpiry, Start.AddMinutes(7)));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void UsedSafetyStateDeletionStopsBeforeAnotherPossibleStartAndNeverRegenerates()
    {
        string path = TempPath();
        string stateRoot = Path.Combine(Path.GetTempPath(), "infinium-campaign-safety-" + Guid.NewGuid().ToString("N"));
        try
        {
            M1Slice6FiniteCampaignLedger ledger = ReadyThroughCredential(path);
            ProductUserSafetyIdentifierStateStore state = new(stateRoot);
            string projection = state.GetOrCreateProjection();
            M1Slice6CampaignDispatchAdmission admission = new(ledger, state);
            admission.ReserveAndLatchPossibleStart(M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1), projection, Start.AddMinutes(5),
                Start.AddMinutes(5).AddSeconds(1));
            ledger.RecordKnownSettlement(M1Slice6CampaignStage.Qualification, 1, 1, 1, 0,
                new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2), Start.AddMinutes(5).AddSeconds(2));
            ledger.RecordStageEvidenceHandoff(M1Slice6CampaignStage.Qualification, "qualification-evidence",
                new string('b', 64), 1, 1, 1, 0, new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2),
                Start.AddMinutes(5).AddSeconds(3));
            ledger.AcceptStageEvidence(M1Slice6CampaignStage.Qualification, "qualification-evidence",
                new string('b', 64), Start.AddMinutes(5).AddSeconds(3));
            File.Delete(Path.Combine(stateRoot, ProductUserSafetyIdentifierStateStore.StateFileName));
            Assert.ThrowsExactly<InvalidDataException>(() => admission.ReserveAndLatchPossibleStart(
                M1Slice6CampaignStage.SourceClaimExtraction,
                Reservation(M1Slice6CampaignStage.SourceClaimExtraction, 1), projection, Start.AddMinutes(6),
                Start.AddMinutes(6).AddSeconds(1)));
            Assert.AreEqual(M1Slice6CampaignState.Stopped, ledger.Current.State);
            Assert.AreEqual(1L, ledger.Current.ProviderCallCount);
            Assert.IsFalse(File.Exists(Path.Combine(stateRoot, ProductUserSafetyIdentifierStateStore.StateFileName)));
        }
        finally
        {
            Cleanup(path);
            if (Directory.Exists(stateRoot)) { Directory.Delete(stateRoot, recursive: true); }
        }
    }

    [TestMethod]
    public void IndependentReopenUsesExclusiveCompareAndSwapAndRejectsStaleWriter()
    {
        string path = TempPath();
        try
        {
            M1Slice6FiniteCampaignLedger first = ReadyThroughCredential(path);
            M1Slice6FiniteCampaignLedger stale = new(path, Identity, CampaignExpiry, CredentialExpiry,
                Start.AddMinutes(4).AddTicks(2));
            first.ReserveStage(M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1), Start.AddMinutes(5));
            Assert.ThrowsExactly<InvalidOperationException>(() => stale.ReserveStage(
                M1Slice6CampaignStage.Qualification,
                Reservation(M1Slice6CampaignStage.Qualification, 1), Start.AddMinutes(5).AddTicks(1)));
            M1Slice6FiniteCampaignLedger reopened = new(path, Identity, CampaignExpiry, CredentialExpiry,
                Start.AddMinutes(6));
            Assert.AreEqual(M1Slice6CampaignState.StageReserved, reopened.Current.State);
            Assert.AreEqual(first.Current.EventHash, reopened.Current.EventHash);
        }
        finally { Cleanup(path); }
    }

    private static M1Slice6FiniteCampaignLedger ReadyThroughCredential(string path)
    {
        M1Slice6FiniteCampaignLedger ledger = new(path, Identity, CampaignExpiry, CredentialExpiry, Start);
        ledger.RecordIndependentReview(Start.AddMinutes(1));
        ledger.AdmitCampaign(Start.AddMinutes(2));
        ledger.BeginCredentialExecutionHandoff(Start.AddMinutes(3));
        ledger.RecordCredentialEvidenceHandoff("credential-evidence", new string('6', 64),
            new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4), Start.AddMinutes(4));
        ledger.AcceptCredentialEvidence("credential-evidence", new string('6', 64), Start.AddMinutes(4).AddTicks(1));
        return ledger;
    }

    private static void RunStage(M1Slice6FiniteCampaignLedger ledger, M1Slice6CampaignStage stage,
        long reserve, long settle, DateTimeOffset now)
    {
        ledger.ReserveStage(stage, Reservation(stage, reserve), now);
        ledger.LatchPossibleStart(stage, SafetyIdentifier, now.AddSeconds(1));
        ledger.RecordKnownSettlement(stage, 100, 10, 1000, settle,
            new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2), now.AddSeconds(2));
        ledger.RecordStageEvidenceHandoff(stage, "evidence-" + stage, new string('7', 64), 100, 10, 1000,
            settle, new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2), now.AddSeconds(3));
        ledger.AcceptStageEvidence(stage, "evidence-" + stage, new string('7', 64), now.AddSeconds(4));
    }

    private static M1Slice6CampaignStageReservation Reservation(M1Slice6CampaignStage stage, long reserve)
    {
        M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(stage);
        return new("request-" + stage, new string('8', 64), Math.Min(1024, limits.MaximumRequestBytes),
            Math.Min(1024, limits.MaximumInputTokens), Math.Min(128, limits.MaximumOutputTokens),
            Math.Min(4096, limits.MaximumRawResponseBytes), reserve);
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), "infinium-campaign-" + Guid.NewGuid().ToString("N"), "ledger.jsonl");
    private static string Rehash(M1Slice6CampaignLedgerEntry value)
    {
        string material = string.Join('|', value.Sequence, JsonSerializer.Serialize(value.Identity, LedgerJson),
            value.State, value.Stage, value.Event, value.RequestManifestId, value.RequestManifestSha256,
            value.EvidenceId, value.EvidenceSha256, value.ProviderCallCount, value.DnsResolutionCount,
            value.AggregateRequestBytes, value.AggregateInputTokens, value.AggregateOutputTokens,
            value.AggregateRawResponseBytes, value.ReservedNanoUsd, value.SettledNanoUsd,
            value.ObservedInputTokens, value.ObservedOutputTokens, value.ObservedRawResponseBytes,
            value.StageDeadlineUtc?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            value.PossibleStartLatched, value.SafetyIdentifierProjection, value.PreviousHash,
            value.RecordedAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
    private static readonly JsonSerializerOptions LedgerJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };
    private static void Cleanup(string path)
    {
        string? root = Path.GetDirectoryName(path);
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
