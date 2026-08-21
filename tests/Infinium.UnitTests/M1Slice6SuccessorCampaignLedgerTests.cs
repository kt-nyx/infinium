using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Infinium.Persistence;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class M1Slice6SuccessorCampaignLedgerTests
{
    private const string CampaignId = "infinium.m1-s6.successor-campaign/test";
    private static readonly DateTimeOffset Start = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void TerminalWp9StartIsConsumedAndOnlyFourFreshQualificationStartsRemain()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            for (int ordinal = 2; ordinal <= 5; ordinal++)
            {
                M1Slice6SuccessorAttemptIdentity attempt = Attempt(M1Slice6CampaignStage.Qualification, ordinal);
                DateTimeOffset at = Start.AddMinutes(ordinal);
                ledger.ReserveAttempt(attempt, 140_000_000, at);
                ledger.LatchPossibleStart(attempt, at.AddTicks(1));
                ledger.RecordAttemptEvidence(attempt, "evidence-" + ordinal, new string((char)('a' + ordinal), 64),
                    "transport-ambiguous", structurallyValid: false, 140_000_000, 0,
                    140_000_000, at.AddTicks(2));
                ledger.AcceptAttemptEvidence(attempt, "evidence-" + ordinal,
                    new string((char)('a' + ordinal), 64), "evidence-review-" + ordinal,
                    new string('d', 64), at.AddTicks(3));
                ledger.RecordOfflineCorrectionReview("review-" + ordinal,
                    new string((char)('f' - ordinal), 64), "transport-defect-" + ordinal, at.AddTicks(4));
            }

            Assert.AreEqual(5, ledger.Current.Wp9PossibleStarts);
            Assert.AreEqual(560_000_000, ledger.Current.SuccessorCumulativeReservedNanoUsd);
            Assert.AreEqual(560_000_000, ledger.Current.SuccessorUnresolvedNanoUsd);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveAttempt(
                Attempt(M1Slice6CampaignStage.Qualification, 6), 140_000_000, Start.AddHours(1)));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void FirstStructurallyValidResponsePermanentlyClosesStageAndReopensExactly()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            M1Slice6SuccessorAttemptIdentity attempt = Attempt(M1Slice6CampaignStage.Qualification, 2);
            ledger.ReserveAttempt(attempt, 140_000_000, Start.AddMinutes(2));
            ledger.LatchPossibleStart(attempt, Start.AddMinutes(2).AddTicks(1));
            ledger.RecordAttemptEvidence(attempt, "qualification-evidence", new string('a', 64), "",
                structurallyValid: true, 140_000_000, 21_000_000, 0,
                Start.AddMinutes(2).AddTicks(2));
            ledger.AcceptAttemptEvidence(attempt, "qualification-evidence", new string('a', 64),
                "qualification-review", new string('d', 64), Start.AddMinutes(2).AddTicks(3));

            Assert.IsTrue(ledger.Current.Wp9Authoritative);
            Assert.AreEqual(140_000_000, ledger.Current.SuccessorCumulativeReservedNanoUsd);
            Assert.AreEqual(21_000_000, ledger.Current.SuccessorSettledNanoUsd);
            Assert.AreEqual(0, ledger.Current.SuccessorOutstandingReservedNanoUsd);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveAttempt(
                Attempt(M1Slice6CampaignStage.Qualification, 3), 140_000_000, Start.AddMinutes(3)));

            M1Slice6SuccessorCampaignLedger reopened = Open(path, Start.AddMinutes(4));
            Assert.AreEqual(ledger.Current.EventHash, reopened.Current.EventHash);
            Assert.IsTrue(reopened.Current.Wp9Authoritative);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void FreshAttemptRequiresAcceptedFailureAndIndependentCorrectionReview()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            M1Slice6SuccessorAttemptIdentity attempt = Attempt(M1Slice6CampaignStage.Qualification, 2);
            ledger.ReserveAttempt(attempt, 140_000_000, Start.AddMinutes(2));
            ledger.LatchPossibleStart(attempt, Start.AddMinutes(2).AddTicks(1));
            ledger.RecordAttemptEvidence(attempt, "failure-evidence", new string('b', 64),
                "provider-failed", false, 140_000_000, 0, 140_000_000,
                Start.AddMinutes(2).AddTicks(2));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveAttempt(
                Attempt(M1Slice6CampaignStage.Qualification, 3), 140_000_000, Start.AddMinutes(3)));
            ledger.AcceptAttemptEvidence(attempt, "failure-evidence", new string('b', 64),
                "failure-review", new string('d', 64), Start.AddMinutes(2).AddTicks(3));
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveAttempt(
                Attempt(M1Slice6CampaignStage.Qualification, 3), 140_000_000, Start.AddMinutes(3)));
            ledger.RecordOfflineCorrectionReview("offline-review", new string('c', 64),
                "offline-defect", Start.AddMinutes(2).AddTicks(4));
            ledger.ReserveAttempt(Attempt(M1Slice6CampaignStage.Qualification, 3), 140_000_000,
                Start.AddMinutes(3));
            Assert.AreEqual(M1Slice6SuccessorCampaignState.AttemptReserved, ledger.Current.State);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void CumulativeReservationCeilingCountsReleasedOrSettledReservationHistory()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.ReserveAttempt(
                Attempt(M1Slice6CampaignStage.Qualification, 2),
                M1Slice6SuccessorCampaignLedger.SuccessorMaximumNanoUsd + 1, Start.AddMinutes(2)));
            Assert.AreEqual(0, ledger.Current.SuccessorCumulativeReservedNanoUsd);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void PreStartFailureReleasesOutstandingWithoutConsumingPossibleStart()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            M1Slice6SuccessorAttemptIdentity first = Attempt(M1Slice6CampaignStage.Qualification, 2);
            ledger.ReserveAttempt(first, 140_000_000, Start.AddMinutes(2));
            ledger.RecordPreStartRelease(first, "prestart-evidence", new string('b', 64),
                140_000_000, Start.AddMinutes(2).AddTicks(1));
            ledger.AcceptAttemptEvidence(first, "prestart-evidence", new string('b', 64),
                "prestart-evidence-review", new string('d', 64), Start.AddMinutes(2).AddTicks(2));
            ledger.RecordOfflineCorrectionReview("prestart-review", new string('c', 64),
                "prestart-defect", Start.AddMinutes(2).AddTicks(3));
            ledger.ReserveAttempt(Attempt(M1Slice6CampaignStage.Qualification, 3),
                140_000_000, Start.AddMinutes(3));
            Assert.AreEqual(1, ledger.Current.Wp9PossibleStarts);
            Assert.AreEqual(280_000_000, ledger.Current.SuccessorCumulativeReservedNanoUsd);
            Assert.AreEqual(140_000_000, ledger.Current.SuccessorOutstandingReservedNanoUsd);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void Wp10AndWp11CanRetryOnlyAfterAcceptedFailureAndCorrectionReview()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            DateTimeOffset at = Start.AddMinutes(2);
            AcceptValid(ledger, Attempt(M1Slice6CampaignStage.Qualification, 2),
                140_000_000, at, 'b');

            M1Slice6SuccessorAttemptIdentity wp10a = Attempt(M1Slice6CampaignStage.SourceClaimExtraction, 1);
            at = at.AddMinutes(1);
            ledger.ReserveAttempt(wp10a, 600_000_000, at);
            ledger.LatchPossibleStart(wp10a, at.AddTicks(1));
            ledger.RecordAttemptEvidence(wp10a, "wp10-failure", new string('c', 64),
                "provider-failed", false, 600_000_000, 0, 600_000_000, at.AddTicks(2));
            ledger.AcceptAttemptEvidence(wp10a, "wp10-failure", new string('c', 64),
                "wp10-failure-review", new string('b', 64), at.AddTicks(3));
            ledger.RecordOfflineCorrectionReview("wp10-correction", new string('d', 64), "wp10-defect", at.AddTicks(4));
            at = at.AddMinutes(1);
            AcceptValid(ledger, Attempt(M1Slice6CampaignStage.SourceClaimExtraction, 2),
                600_000_000, at, 'e');

            M1Slice6SuccessorAttemptIdentity wp11a = Attempt(M1Slice6CampaignStage.CandidateInvestigation, 1);
            at = at.AddMinutes(1);
            ledger.ReserveAttempt(wp11a, 600_000_000, at);
            ledger.LatchPossibleStart(wp11a, at.AddTicks(1));
            ledger.RecordAttemptEvidence(wp11a, "wp11-failure", new string('f', 64),
                "provider-refused", false, 600_000_000, 0, 600_000_000, at.AddTicks(2));
            ledger.AcceptAttemptEvidence(wp11a, "wp11-failure", new string('f', 64),
                "wp11-failure-review", new string('b', 64), at.AddTicks(3));
            ledger.RecordOfflineCorrectionReview("wp11-correction", new string('a', 64), "wp11-defect", at.AddTicks(4));
            at = at.AddMinutes(1);
            ledger.ReserveAttempt(Attempt(M1Slice6CampaignStage.CandidateInvestigation, 2),
                600_000_000, at);
            Assert.AreEqual(M1Slice6SuccessorCampaignState.AttemptReserved, ledger.Current.State);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void StaleSecondWriterCannotForkTheHashChain()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger first = Open(path, Start);
            M1Slice6SuccessorCampaignLedger stale = Open(path, Start);
            first.RecordIndependentReview("review", new string('a', 64), Start.AddTicks(1));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                stale.RecordIndependentReview("review", new string('a', 64), Start.AddTicks(2)));
            M1Slice6SuccessorCampaignLedger reopened = Open(path, Start.AddTicks(3));
            Assert.AreEqual(2, reopened.Entries.Count);
            Assert.AreEqual(M1Slice6SuccessorCampaignState.Reviewed, reopened.Current.State);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void RehashedCounterRegressionCannotReopenBudget()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            M1Slice6SuccessorAttemptIdentity attempt = Attempt(M1Slice6CampaignStage.Qualification, 2);
            ledger.ReserveAttempt(attempt, 140_000_000, Start.AddMinutes(2));
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            JsonObject last = JsonNode.Parse(lines[^1])!.AsObject();
            last["successor_cumulative_reserved_nano_usd"] = 0;
            last["successor_outstanding_reserved_nano_usd"] = 0;
            last["event_hash"] = "";
            last["event_hash"] = Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(last.ToJsonString())));
            lines[^1] = last.ToJsonString();
            File.WriteAllText(path, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
            Assert.ThrowsExactly<InvalidDataException>(() => Open(path, Start.AddMinutes(3)));
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void C3RequiresComposedHandoffThenIndependentReview()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedger ledger = Admitted(path);
            DateTimeOffset at = Start.AddMinutes(2);
            AcceptValid(ledger, Attempt(M1Slice6CampaignStage.Qualification, 2), 140_000_000, at, 'b');
            at = at.AddMinutes(1);
            AcceptValid(ledger, Attempt(M1Slice6CampaignStage.SourceClaimExtraction, 1), 600_000_000, at, 'c');
            at = at.AddMinutes(1);
            AcceptValid(ledger, Attempt(M1Slice6CampaignStage.CandidateInvestigation, 1), 600_000_000, at, 'e');
            Assert.ThrowsExactly<InvalidOperationException>(() => ledger.Complete(
                "composed", new string('f', 64), "composed-review", new string('a', 64), at.AddTicks(4)));
            ledger.RecordComposedEvidence("composed", new string('f', 64), at.AddTicks(4));
            ledger = Open(path, at.AddTicks(4));
            Assert.AreEqual(M1Slice6SuccessorCampaignState.ComposedEvidenceHandoff, ledger.Current.State);
            ledger.Complete("composed", new string('f', 64), "composed-review", new string('a', 64), at.AddTicks(5));
            Assert.AreEqual(M1Slice6SuccessorCampaignState.Completed, ledger.Current.State);
            Assert.AreEqual("composed-review", ledger.Current.EvidenceId);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void HardBudgetContinuationHasNoStartOrRepeatedDefectCeiling()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedgerV3 ledger = OpenHardBudget(path, Start);
            for (int ordinal = 3; ordinal <= 7; ordinal++)
            {
                DateTimeOffset at = Start.AddMinutes(ordinal);
                M1Slice6SuccessorAttemptIdentity attempt = Attempt(
                    M1Slice6CampaignStage.Qualification, ordinal);
                ledger.ReserveAttempt(attempt, 1, at);
                ledger.LatchPossibleStart(attempt, at.AddTicks(1));
                ledger.RecordAttemptEvidence(attempt, "hard-failure-" + ordinal,
                    new string('b', 64), "provider-failed", false,
                    1, 1, 0, at.AddTicks(2));
                ledger.AcceptAttemptEvidence(attempt, "hard-failure-" + ordinal,
                    new string('b', 64), "hard-review-" + ordinal,
                    new string('d', 64), at.AddTicks(3));
                ledger.RecordOfflineCorrectionReview("hard-correction-" + ordinal,
                    new string('e', 64), "repeated-defect", at.AddTicks(4));
            }
            Assert.AreEqual(7, ledger.Current.Wp9PossibleStarts);
            Assert.AreEqual(250_080_005, ledger.CommittedNanoUsd);
        }
        finally { Cleanup(path); }
    }

    [TestMethod]
    public void HardBudgetPrestartReleaseDoesNotPermanentlyBurnReservation()
    {
        string path = TempPath();
        try
        {
            M1Slice6SuccessorCampaignLedgerV3 ledger = OpenHardBudget(path, Start);
            const long reservation = 9_700_000_000;
            M1Slice6SuccessorAttemptIdentity first = Attempt(
                M1Slice6CampaignStage.Qualification, 3);
            ledger.ReserveAttempt(first, reservation, Start.AddMinutes(1));
            ledger.RecordPreStartRelease(first, "released", new string('b', 64),
                reservation, Start.AddMinutes(1).AddTicks(1));
            ledger.AcceptAttemptEvidence(first, "released", new string('b', 64),
                "released-review", new string('c', 64), Start.AddMinutes(1).AddTicks(2));
            ledger.RecordOfflineCorrectionReview("released-correction", new string('d', 64),
                "prestart-defect", Start.AddMinutes(1).AddTicks(3));
            ledger.ReserveAttempt(Attempt(M1Slice6CampaignStage.Qualification, 4),
                reservation, Start.AddMinutes(2));
            Assert.AreEqual(9_950_080_000, ledger.CommittedNanoUsd);
            Assert.AreEqual(19_510_080_000, ledger.Current.SuccessorCumulativeReservedNanoUsd);
        }
        finally { Cleanup(path); }
    }

    private static M1Slice6SuccessorCampaignLedger Admitted(string path)
    {
        M1Slice6SuccessorCampaignLedger ledger = Open(path, Start);
        ledger.RecordIndependentReview("review", new string('a', 64), Start.AddTicks(1));
        ledger.Admit(Start.AddTicks(2));
        return ledger;
    }

    private static M1Slice6SuccessorCampaignLedger Open(string path, DateTimeOffset now) => new(path,
        CampaignId, new string('1', 64), M1Slice6SuccessorCampaignLedger.RequiredTerminalCampaignId,
        M1Slice6SuccessorCampaignLedger.RequiredTerminalEventHash, now);

    private static M1Slice6SuccessorCampaignLedgerV3 OpenHardBudget(string path, DateTimeOffset now) => new(path,
        CampaignId, new string('1', 64), M1Slice6SuccessorCampaignLedgerV3.RequiredTerminalCampaignId,
        M1Slice6SuccessorCampaignLedgerV3.RequiredTerminalEventHash, 8,
        "be76dfd4d47b33e97f8585c66c951df7184b9995e02e9539e2c5bdf2ee089f2b",
        "hard-budget-amendment", new string('a', 64), "amendment-review",
        new string('b', 64), 2, 0, 0, false, false, false,
        110_080_000, 110_080_000, 0, now);

    private static M1Slice6SuccessorAttemptIdentity Attempt(M1Slice6CampaignStage stage, int ordinal) => new(
        stage, ordinal, "attempt-" + (int)stage + "-" + ordinal,
        "stage-manifest-" + (int)stage + "-" + ordinal,
        new string('2', 64), "runtime-" + (int)stage + "-" + ordinal, new string('3', 64),
        "request-" + (int)stage + "-" + ordinal, "reservation-" + (int)stage + "-" + ordinal,
        "fence-" + (int)stage + "-" + ordinal);

    private static void AcceptValid(M1Slice6SuccessorCampaignLedger ledger,
        M1Slice6SuccessorAttemptIdentity attempt, long reservation, DateTimeOffset at, char digest)
    {
        string evidence = "valid-" + attempt.AttemptId;
        string sha = new(digest, 64);
        ledger.ReserveAttempt(attempt, reservation, at);
        ledger.LatchPossibleStart(attempt, at.AddTicks(1));
        ledger.RecordAttemptEvidence(attempt, evidence, sha, "", true,
            reservation, 1, 0, at.AddTicks(2));
        ledger.AcceptAttemptEvidence(attempt, evidence, sha, "valid-evidence-review",
            new string('d', 64), at.AddTicks(3));
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(),
        "infinium-successor-" + Guid.NewGuid().ToString("N"), "ledger.v2.jsonl");

    private static void Cleanup(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); }
    }
}
