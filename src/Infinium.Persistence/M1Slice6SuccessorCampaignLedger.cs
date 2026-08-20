using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinium.Persistence;

public enum M1Slice6SuccessorCampaignState
{
    Ready,
    Reviewed,
    Admitted,
    AttemptReserved,
    AttemptStarted,
    AttemptEvidenceHandoff,
    AuthoritativeRecoveryHandoff,
    AttemptFailureAccepted,
    CorrectionReviewed,
    StageAccepted,
    ComposedEvidenceHandoff,
    Completed,
    Stopped,
}

public sealed record M1Slice6SuccessorAttemptIdentity(
    M1Slice6CampaignStage Stage,
    int AttemptOrdinal,
    string AttemptId,
    string StageManifestId,
    string StageManifestSha256,
    string RuntimeAuthorityId,
    string RuntimeAuthoritySha256,
    string RequestId,
    string ReservationId,
    string DispatchFenceId);

public sealed record M1Slice6SuccessorCampaignLedgerEntry(
    long Sequence,
    string CampaignId,
    string CampaignManifestSha256,
    string TerminalCampaignId,
    string TerminalEventHash,
    M1Slice6SuccessorCampaignState State,
    string Event,
    M1Slice6SuccessorAttemptIdentity? Attempt,
    string EvidenceId,
    string EvidenceSha256,
    string FailureDisposition,
    int Wp9PossibleStarts,
    int Wp10PossibleStarts,
    int Wp11PossibleStarts,
    bool Wp9Authoritative,
    bool Wp10Authoritative,
    bool Wp11Authoritative,
    long PriorConservativeNanoUsd,
    long SuccessorCumulativeReservedNanoUsd,
    long SuccessorOutstandingReservedNanoUsd,
    long SuccessorUnresolvedNanoUsd,
    long SuccessorSettledNanoUsd,
    string PreviousHash,
    string EventHash,
    DateTimeOffset RecordedAtUtc);

/// <summary>
/// Clean-break successor authority ledger. The terminal v4 ledger is referenced only by its
/// immutable identity and final event hash; its bytes and grammar are never reopened here.
/// One call is authorized per admitted attempt and a first structurally valid stage result is
/// permanent authority for that stage.
/// </summary>
public sealed class M1Slice6SuccessorCampaignLedger
{
    public const int MaximumPossibleStartsPerStage = 5;
    public const long SliceMaximumNanoUsd = 10_000_000_000;
    public const long PriorConservativeNanoUsd = 140_000_000;
    public const long SuccessorMaximumNanoUsd = SliceMaximumNanoUsd - PriorConservativeNanoUsd;
    public const string RequiredTerminalEventHash =
        "282c97151dbdcd354288b67f96c4b01d7f7ef43b1bbfb9f247cbd9b510506de9";
    public const string RequiredTerminalCampaignId =
        "infinium.m1-s6.finite-live-campaign/ff2d542a-04f0-448a-bcb8-a0ecbedde5b9";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    static M1Slice6SuccessorCampaignLedger() =>
        Json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));

    private readonly string path;
    private readonly string lockPath;
    private readonly string campaignId;
    private readonly string campaignManifestSha256;
    private readonly string terminalCampaignId;
    private readonly List<M1Slice6SuccessorCampaignLedgerEntry> entries;

    public M1Slice6SuccessorCampaignLedger(string path, string campaignId,
        string campaignManifestSha256, string terminalCampaignId, string terminalEventHash,
        DateTimeOffset now)
    {
        this.path = Path.GetFullPath(path);
        lockPath = this.path + ".lock";
        this.campaignId = Identity(campaignId);
        this.campaignManifestSha256 = Hex(campaignManifestSha256);
        this.terminalCampaignId = Identity(terminalCampaignId);
        if (this.terminalCampaignId != RequiredTerminalCampaignId
            || Hex(terminalEventHash) != RequiredTerminalEventHash)
        {
            throw new InvalidDataException("The successor campaign does not bind the exact terminal C2B event.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        if (File.Exists(this.path))
        {
            using FileStream lease = Lock();
            entries = ReadAndValidate();
            M1Slice6SuccessorCampaignLedgerEntry first = entries[0];
            if (first.CampaignId != this.campaignId
                || first.CampaignManifestSha256 != this.campaignManifestSha256
                || first.TerminalCampaignId != this.terminalCampaignId
                || first.TerminalEventHash != RequiredTerminalEventHash)
            {
                throw new InvalidDataException("The successor ledger identity differs from its immutable first event.");
            }
            RequireClock(now);
        }
        else
        {
            entries = [];
            Append(M1Slice6SuccessorCampaignState.Ready, "successor-campaign-ready", null,
                "", "", "", 1, 0, 0, false, false, false,
                PriorConservativeNanoUsd, 0, 0, 0, 0, now);
        }
    }

    public IReadOnlyList<M1Slice6SuccessorCampaignLedgerEntry> Entries => entries.AsReadOnly();
    public M1Slice6SuccessorCampaignLedgerEntry Current => entries[^1];

    public void RecordIndependentReview(string reviewId, string reviewSha256, DateTimeOffset now)
    {
        if (Current.State != M1Slice6SuccessorCampaignState.Ready)
        { throw new InvalidOperationException("Successor campaign review has a stale predecessor."); }
        Append(M1Slice6SuccessorCampaignState.Reviewed,
            "successor-campaign-independently-reviewed", null, Identity(reviewId),
            Hex(reviewSha256), "", Current.Wp9PossibleStarts, Current.Wp10PossibleStarts,
            Current.Wp11PossibleStarts, false, false, false, PriorConservativeNanoUsd,
            0, 0, 0, 0, now);
    }

    public void Admit(DateTimeOffset now) =>
        Transition(M1Slice6SuccessorCampaignState.Reviewed, M1Slice6SuccessorCampaignState.Admitted,
            "owner-authorized-successor-campaign-admitted", now);

    public void ReserveAttempt(M1Slice6SuccessorAttemptIdentity attempt, long reservedNanoUsd,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        RequireClock(now);
        ValidateAttempt(attempt);
        if (Current.State is not (M1Slice6SuccessorCampaignState.Admitted
            or M1Slice6SuccessorCampaignState.CorrectionReviewed
            or M1Slice6SuccessorCampaignState.StageAccepted))
        {
            throw new InvalidOperationException("A successor attempt requires admitted or independently accepted predecessor authority.");
        }
        RequireEligibleStage(attempt.Stage);
        int starts = Starts(attempt.Stage);
        int priorAttempts = entries.Where(entry => entry.Attempt?.Stage == attempt.Stage)
            .Select(entry => entry.Attempt!.AttemptId).Distinct(StringComparer.Ordinal).Count();
        int expectedOrdinal = priorAttempts + 1
            + (attempt.Stage == M1Slice6CampaignStage.Qualification ? 1 : 0);
        if (starts >= MaximumPossibleStartsPerStage || attempt.AttemptOrdinal != expectedOrdinal)
        {
            throw new InvalidOperationException("The successor attempt ordinal or per-stage start ceiling is exhausted.");
        }
        M1Slice6SuccessorAttemptIdentity[] history = entries.Where(entry => entry.Attempt is not null)
            .Select(entry => entry.Attempt!).Distinct().ToArray();
        if (history.Any(previous => previous.AttemptId == attempt.AttemptId
                || previous.StageManifestId == attempt.StageManifestId
                || previous.RuntimeAuthorityId == attempt.RuntimeAuthorityId
                || previous.RequestId == attempt.RequestId
                || previous.ReservationId == attempt.ReservationId
                || previous.DispatchFenceId == attempt.DispatchFenceId))
        {
            throw new InvalidOperationException("Every successor attempt and transport identity must be fresh.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reservedNanoUsd);
        long committed = checked(Current.PriorConservativeNanoUsd
            + Current.SuccessorCumulativeReservedNanoUsd + reservedNanoUsd);
        if (committed > SliceMaximumNanoUsd)
        {
            throw new InvalidOperationException("The exact Slice 6 conservative USD 10 ceiling is exhausted.");
        }
        Append(M1Slice6SuccessorCampaignState.AttemptReserved, "fresh-attempt-reserved", attempt,
            "", "", "", Current.Wp9PossibleStarts, Current.Wp10PossibleStarts,
            Current.Wp11PossibleStarts, Current.Wp9Authoritative, Current.Wp10Authoritative,
            Current.Wp11Authoritative, Current.PriorConservativeNanoUsd,
            checked(Current.SuccessorCumulativeReservedNanoUsd + reservedNanoUsd),
            checked(Current.SuccessorOutstandingReservedNanoUsd + reservedNanoUsd),
            Current.SuccessorUnresolvedNanoUsd, Current.SuccessorSettledNanoUsd, now);
    }

    public void LatchPossibleStart(M1Slice6SuccessorAttemptIdentity attempt, DateTimeOffset now)
    {
        RequireExactAttempt(attempt, M1Slice6SuccessorCampaignState.AttemptReserved);
        int wp9 = Current.Wp9PossibleStarts;
        int wp10 = Current.Wp10PossibleStarts;
        int wp11 = Current.Wp11PossibleStarts;
        switch (attempt.Stage)
        {
            case M1Slice6CampaignStage.Qualification: wp9++; break;
            case M1Slice6CampaignStage.SourceClaimExtraction: wp10++; break;
            case M1Slice6CampaignStage.CandidateInvestigation: wp11++; break;
            default: throw new InvalidDataException("The attempt stage is not dispatchable.");
        }
        if (wp9 > MaximumPossibleStartsPerStage || wp10 > MaximumPossibleStartsPerStage
            || wp11 > MaximumPossibleStartsPerStage)
        {
            throw new InvalidOperationException("A stage exceeded five possible provider starts.");
        }
        Append(M1Slice6SuccessorCampaignState.AttemptStarted, "possible-provider-start-latched",
            attempt, "", "", "", wp9, wp10, wp11, Current.Wp9Authoritative,
            Current.Wp10Authoritative, Current.Wp11Authoritative, Current.PriorConservativeNanoUsd,
            Current.SuccessorCumulativeReservedNanoUsd,
            Current.SuccessorOutstandingReservedNanoUsd, Current.SuccessorUnresolvedNanoUsd,
            Current.SuccessorSettledNanoUsd, now);
    }

    public void RecordAttemptEvidence(M1Slice6SuccessorAttemptIdentity attempt,
        string evidenceId, string evidenceSha256, string failureDisposition,
        bool structurallyValid, long reservedNanoUsd, long settledNanoUsd,
        long unresolvedNanoUsd, DateTimeOffset now)
    {
        RequireExactAttempt(attempt, M1Slice6SuccessorCampaignState.AttemptStarted);
        if (reservedNanoUsd <= 0 || reservedNanoUsd != Current.SuccessorOutstandingReservedNanoUsd
            || settledNanoUsd < 0 || unresolvedNanoUsd < 0
            || checked(settledNanoUsd + unresolvedNanoUsd) > reservedNanoUsd)
        {
            throw new InvalidDataException("Attempt settlement is outside its exact reservation.");
        }
        bool wp9 = Current.Wp9Authoritative;
        bool wp10 = Current.Wp10Authoritative;
        bool wp11 = Current.Wp11Authoritative;
        if (structurallyValid)
        {
            if (!string.IsNullOrEmpty(failureDisposition))
            {
                throw new InvalidDataException("A structurally valid response cannot carry a failure disposition.");
            }
            switch (attempt.Stage)
            {
                case M1Slice6CampaignStage.Qualification when !wp9: wp9 = true; break;
                case M1Slice6CampaignStage.SourceClaimExtraction when !wp10: wp10 = true; break;
                case M1Slice6CampaignStage.CandidateInvestigation when !wp11: wp11 = true; break;
                default: throw new InvalidOperationException("The stage already has its permanent authoritative response.");
            }
        }
        else if (string.IsNullOrWhiteSpace(failureDisposition))
        {
            throw new InvalidDataException("A non-valid attempt requires a closed failure disposition.");
        }
        long outstanding = checked(Current.SuccessorOutstandingReservedNanoUsd - reservedNanoUsd);
        long unresolved = checked(Current.SuccessorUnresolvedNanoUsd + unresolvedNanoUsd);
        long settled = checked(Current.SuccessorSettledNanoUsd + settledNanoUsd);
        Append(M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff,
            structurallyValid ? "first-structurally-valid-response-authoritative"
                : "attempt-failure-evidence-handoff", attempt, Identity(evidenceId),
            Hex(evidenceSha256), Failure(failureDisposition), Current.Wp9PossibleStarts,
            Current.Wp10PossibleStarts, Current.Wp11PossibleStarts, wp9, wp10, wp11,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            outstanding, unresolved, settled, now);
    }

    public void RecordPreStartRelease(M1Slice6SuccessorAttemptIdentity attempt,
        string evidenceId, string evidenceSha256, long reservedNanoUsd, DateTimeOffset now)
    {
        RequireExactAttempt(attempt, M1Slice6SuccessorCampaignState.AttemptReserved);
        if (reservedNanoUsd != Current.SuccessorOutstandingReservedNanoUsd)
        { throw new InvalidDataException("Pre-start release differs from the outstanding reservation."); }
        Append(M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff,
            "prestart-failure-evidence-handoff", attempt, Identity(evidenceId),
            Hex(evidenceSha256), "prestart-failure", Current.Wp9PossibleStarts,
            Current.Wp10PossibleStarts, Current.Wp11PossibleStarts,
            Current.Wp9Authoritative, Current.Wp10Authoritative, Current.Wp11Authoritative,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            0, Current.SuccessorUnresolvedNanoUsd, Current.SuccessorSettledNanoUsd, now);
    }

    public void RecordPreStartTerminalSafetyStop(M1Slice6SuccessorAttemptIdentity attempt,
        string evidenceId, string evidenceSha256, long reservedNanoUsd, DateTimeOffset now)
    {
        RequireExactAttempt(attempt, M1Slice6SuccessorCampaignState.AttemptReserved);
        if (reservedNanoUsd != Current.SuccessorOutstandingReservedNanoUsd)
        { throw new InvalidDataException("Pre-start safety release differs from the outstanding reservation."); }
        Append(M1Slice6SuccessorCampaignState.Stopped, "prestart-terminal-safety-stop", attempt,
            Identity(evidenceId), Hex(evidenceSha256), "safety-isolation-breach",
            Current.Wp9PossibleStarts, Current.Wp10PossibleStarts, Current.Wp11PossibleStarts,
            Current.Wp9Authoritative, Current.Wp10Authoritative, Current.Wp11Authoritative,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            0, Current.SuccessorUnresolvedNanoUsd, Current.SuccessorSettledNanoUsd, now);
    }

    public void RecordTerminalSafetyStop(M1Slice6SuccessorAttemptIdentity attempt,
        string evidenceId, string evidenceSha256, long reservedNanoUsd,
        long settledNanoUsd, long unresolvedNanoUsd, DateTimeOffset now)
    {
        RequireExactAttempt(attempt, M1Slice6SuccessorCampaignState.AttemptStarted);
        if (reservedNanoUsd != Current.SuccessorOutstandingReservedNanoUsd
            || settledNanoUsd < 0 || unresolvedNanoUsd < 0
            || checked(settledNanoUsd + unresolvedNanoUsd) > reservedNanoUsd)
        { throw new InvalidDataException("Terminal safety accounting differs from the exact reservation."); }
        Append(M1Slice6SuccessorCampaignState.Stopped, "terminal-safety-stop", attempt,
            Identity(evidenceId), Hex(evidenceSha256), "safety-isolation-breach",
            Current.Wp9PossibleStarts, Current.Wp10PossibleStarts, Current.Wp11PossibleStarts,
            Current.Wp9Authoritative, Current.Wp10Authoritative, Current.Wp11Authoritative,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            0, checked(Current.SuccessorUnresolvedNanoUsd + unresolvedNanoUsd),
            checked(Current.SuccessorSettledNanoUsd + settledNanoUsd), now);
    }

    public void AcceptAttemptEvidence(M1Slice6SuccessorAttemptIdentity attempt,
        string evidenceId, string evidenceSha256, string reviewId, string reviewSha256,
        DateTimeOffset now)
    {
        if (Current.State is not (M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff
            or M1Slice6SuccessorCampaignState.AuthoritativeRecoveryHandoff)
            || Current.Attempt != attempt)
        { throw new InvalidOperationException("Attempt evidence acceptance has a stale exact attempt."); }
        if (Current.EvidenceId != Identity(evidenceId) || Current.EvidenceSha256 != Hex(evidenceSha256))
        {
            throw new InvalidDataException("Attempt evidence acceptance differs from the retained handoff.");
        }
        bool valid = string.IsNullOrEmpty(Current.FailureDisposition);
        Append(valid ? M1Slice6SuccessorCampaignState.StageAccepted
                : M1Slice6SuccessorCampaignState.AttemptFailureAccepted,
            valid ? "authoritative-stage-evidence-independently-accepted"
                : "attempt-failure-evidence-independently-accepted",
            attempt, Identity(reviewId), Hex(reviewSha256), Current.FailureDisposition,
            Current.Wp9PossibleStarts, Current.Wp10PossibleStarts, Current.Wp11PossibleStarts,
            Current.Wp9Authoritative, Current.Wp10Authoritative, Current.Wp11Authoritative,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            Current.SuccessorOutstandingReservedNanoUsd,
            Current.SuccessorUnresolvedNanoUsd, Current.SuccessorSettledNanoUsd, now);
    }

    public void RecordAuthoritativeRecoveryEvidence(M1Slice6SuccessorAttemptIdentity attempt,
        string evidenceId, string evidenceSha256, DateTimeOffset now)
    {
        RequireExactAttempt(attempt, M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff);
        if (!string.IsNullOrEmpty(Current.FailureDisposition)
            || attempt.Stage == M1Slice6CampaignStage.Qualification)
        { throw new InvalidOperationException("Only a first-valid semantic stage may receive offline recovery evidence."); }
        Append(M1Slice6SuccessorCampaignState.AuthoritativeRecoveryHandoff,
            "authoritative-retained-response-semantic-recovery-handoff", attempt,
            Identity(evidenceId), Hex(evidenceSha256), "", Current.Wp9PossibleStarts,
            Current.Wp10PossibleStarts, Current.Wp11PossibleStarts, Current.Wp9Authoritative,
            Current.Wp10Authoritative, Current.Wp11Authoritative, Current.PriorConservativeNanoUsd,
            Current.SuccessorCumulativeReservedNanoUsd, Current.SuccessorOutstandingReservedNanoUsd,
            Current.SuccessorUnresolvedNanoUsd, Current.SuccessorSettledNanoUsd, now);
    }

    public void RecordOfflineCorrectionReview(string reviewId, string reviewSha256,
        string defectId, DateTimeOffset now)
    {
        if (Current.State != M1Slice6SuccessorCampaignState.AttemptFailureAccepted)
        {
            throw new InvalidOperationException("A correction review requires independently accepted failure evidence.");
        }
        string defect = "defect:" + Identity(defectId);
        if (entries.Any(entry => entry.State == M1Slice6SuccessorCampaignState.CorrectionReviewed
                && entry.FailureDisposition == defect))
        { throw new InvalidOperationException("The same defect recurred after reviewed diagnosis/correction."); }
        Append(M1Slice6SuccessorCampaignState.CorrectionReviewed,
            "offline-diagnosis-and-optional-correction-independently-reviewed", Current.Attempt,
            Identity(reviewId), Hex(reviewSha256), defect,
            Current.Wp9PossibleStarts, Current.Wp10PossibleStarts, Current.Wp11PossibleStarts,
            Current.Wp9Authoritative, Current.Wp10Authoritative, Current.Wp11Authoritative,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            Current.SuccessorOutstandingReservedNanoUsd,
            Current.SuccessorUnresolvedNanoUsd, Current.SuccessorSettledNanoUsd, now);
    }

    public void RecordComposedEvidence(string evidenceId, string evidenceSha256, DateTimeOffset now)
    {
        if (Current.State != M1Slice6SuccessorCampaignState.StageAccepted
            || !Current.Wp9Authoritative || !Current.Wp10Authoritative || !Current.Wp11Authoritative
            || Current.SuccessorOutstandingReservedNanoUsd != 0
            || checked(Current.PriorConservativeNanoUsd + Current.SuccessorCumulativeReservedNanoUsd)
                > SliceMaximumNanoUsd)
        {
            throw new InvalidOperationException("All three first-valid stage results must be accepted before C3 completion.");
        }
        Append(M1Slice6SuccessorCampaignState.ComposedEvidenceHandoff,
            "effect-free-c3-composed-evidence-handoff",
            null, Identity(evidenceId), Hex(evidenceSha256), "", Current.Wp9PossibleStarts,
            Current.Wp10PossibleStarts, Current.Wp11PossibleStarts, true, true, true,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            Current.SuccessorOutstandingReservedNanoUsd, Current.SuccessorUnresolvedNanoUsd,
            Current.SuccessorSettledNanoUsd, now);
    }

    public void Complete(string evidenceId, string evidenceSha256,
        string reviewId, string reviewSha256, DateTimeOffset now)
    {
        if (Current.State != M1Slice6SuccessorCampaignState.ComposedEvidenceHandoff
            || Current.EvidenceId != Identity(evidenceId)
            || Current.EvidenceSha256 != Hex(evidenceSha256))
        { throw new InvalidOperationException("C3 acceptance requires the exact composed-evidence handoff."); }
        Append(M1Slice6SuccessorCampaignState.Completed,
            "effect-free-c3-composed-evidence-independently-accepted", null,
            Identity(reviewId), Hex(reviewSha256), "", Current.Wp9PossibleStarts,
            Current.Wp10PossibleStarts, Current.Wp11PossibleStarts, true, true, true,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            Current.SuccessorOutstandingReservedNanoUsd, Current.SuccessorUnresolvedNanoUsd,
            Current.SuccessorSettledNanoUsd, now);
    }

    private void RequireEligibleStage(M1Slice6CampaignStage stage)
    {
        bool eligible = stage switch
        {
            M1Slice6CampaignStage.Qualification => !Current.Wp9Authoritative,
            M1Slice6CampaignStage.SourceClaimExtraction => Current.Wp9Authoritative
                && !Current.Wp10Authoritative
                && (Current.State == M1Slice6SuccessorCampaignState.StageAccepted
                    || Current.State == M1Slice6SuccessorCampaignState.CorrectionReviewed
                        && Current.Attempt?.Stage == M1Slice6CampaignStage.SourceClaimExtraction),
            M1Slice6CampaignStage.CandidateInvestigation => Current.Wp10Authoritative
                && !Current.Wp11Authoritative
                && (Current.State == M1Slice6SuccessorCampaignState.StageAccepted
                    || Current.State == M1Slice6SuccessorCampaignState.CorrectionReviewed
                        && Current.Attempt?.Stage == M1Slice6CampaignStage.CandidateInvestigation),
            _ => false,
        };
        if (!eligible)
        {
            throw new InvalidOperationException("The stage is not eligible or already has an authoritative response.");
        }
    }

    private int Starts(M1Slice6CampaignStage stage) => stage switch
    {
        M1Slice6CampaignStage.Qualification => Current.Wp9PossibleStarts,
        M1Slice6CampaignStage.SourceClaimExtraction => Current.Wp10PossibleStarts,
        M1Slice6CampaignStage.CandidateInvestigation => Current.Wp11PossibleStarts,
        _ => throw new InvalidDataException("The attempt stage is not dispatchable."),
    };

    private void Transition(M1Slice6SuccessorCampaignState from, M1Slice6SuccessorCampaignState to,
        string @event, DateTimeOffset now)
    {
        if (Current.State != from) { throw new InvalidOperationException("The successor transition predecessor is stale."); }
        Append(to, @event, null, Current.EvidenceId, Current.EvidenceSha256, Current.FailureDisposition,
            Current.Wp9PossibleStarts, Current.Wp10PossibleStarts, Current.Wp11PossibleStarts,
            Current.Wp9Authoritative, Current.Wp10Authoritative, Current.Wp11Authoritative,
            Current.PriorConservativeNanoUsd, Current.SuccessorCumulativeReservedNanoUsd,
            Current.SuccessorOutstandingReservedNanoUsd,
            Current.SuccessorUnresolvedNanoUsd, Current.SuccessorSettledNanoUsd, now);
    }

    private void RequireExactAttempt(M1Slice6SuccessorAttemptIdentity attempt,
        M1Slice6SuccessorCampaignState state)
    {
        RequireClock(DateTimeOffset.UtcNow);
        ValidateAttempt(attempt);
        if (Current.State != state || Current.Attempt != attempt)
        {
            throw new InvalidOperationException("The successor attempt identity or predecessor state is stale.");
        }
    }

    private void Append(M1Slice6SuccessorCampaignState state, string @event,
        M1Slice6SuccessorAttemptIdentity? attempt, string evidenceId, string evidenceSha256,
        string failureDisposition, int wp9Starts, int wp10Starts, int wp11Starts,
        bool wp9Authoritative, bool wp10Authoritative, bool wp11Authoritative,
        long prior, long cumulativeReserved, long outstanding, long unresolved, long settled, DateTimeOffset now)
    {
        RequireClock(now);
        string previous = entries.Count == 0 ? new string('0', 64) : entries[^1].EventHash;
        M1Slice6SuccessorCampaignLedgerEntry material = new(entries.Count + 1, campaignId,
            campaignManifestSha256, terminalCampaignId, RequiredTerminalEventHash, state, @event,
            attempt, evidenceId, evidenceSha256, failureDisposition, wp9Starts, wp10Starts, wp11Starts,
            wp9Authoritative, wp10Authoritative, wp11Authoritative, prior, cumulativeReserved,
            outstanding, unresolved, settled, previous, "", now.ToUniversalTime());
        string hash = Hash(material);
        M1Slice6SuccessorCampaignLedgerEntry entry = material with { EventHash = hash };
        ValidateEvolution(entries.Count == 0 ? null : entries[^1], entry);
        byte[] line = [.. JsonSerializer.SerializeToUtf8Bytes(entry, Json), (byte)'\n'];
        using FileStream lease = Lock();
        if (File.Exists(path) && new FileInfo(path).Length > 0)
        {
            List<M1Slice6SuccessorCampaignLedgerEntry> disk = ReadAndValidate();
            if (disk.Count != entries.Count || disk[^1].EventHash != entries[^1].EventHash)
            {
                throw new InvalidOperationException("The successor ledger changed under the single-writer lease.");
            }
        }
        using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read,
            4096, FileOptions.WriteThrough);
        stream.Write(line);
        stream.Flush(flushToDisk: true);
        entries.Add(entry);
    }

    private List<M1Slice6SuccessorCampaignLedgerEntry> ReadAndValidate()
    {
        List<M1Slice6SuccessorCampaignLedgerEntry> result = [];
        foreach (string line in File.ReadLines(path, Encoding.UTF8))
        {
            if (line.Length == 0) { throw new InvalidDataException("The successor ledger contains a blank event."); }
            M1Slice6SuccessorCampaignLedgerEntry entry = JsonSerializer.Deserialize<M1Slice6SuccessorCampaignLedgerEntry>(line, Json)
                ?? throw new InvalidDataException("The successor ledger contains an invalid event.");
            string expectedPrevious = result.Count == 0 ? new string('0', 64) : result[^1].EventHash;
            if (entry.Sequence != result.Count + 1 || entry.PreviousHash != expectedPrevious
                || entry.EventHash != Hash(entry with { EventHash = "" }))
            {
                throw new InvalidDataException("The successor ledger hash chain is invalid.");
            }
            ValidateEvolution(result.Count == 0 ? null : result[^1], entry);
            if (entry.Event == "fresh-attempt-reserved" && entry.Attempt is { } fresh)
            {
                M1Slice6SuccessorAttemptIdentity[] priorAttempts = result
                    .Where(item => item.Attempt is not null).Select(item => item.Attempt!)
                    .Distinct().ToArray();
                if (priorAttempts.Any(previous => previous.AttemptId == fresh.AttemptId
                        || previous.StageManifestId == fresh.StageManifestId
                        || previous.RuntimeAuthorityId == fresh.RuntimeAuthorityId
                        || previous.RequestId == fresh.RequestId
                        || previous.ReservationId == fresh.ReservationId
                        || previous.DispatchFenceId == fresh.DispatchFenceId))
                { throw new InvalidDataException("The successor ledger reuses a prior attempt identity."); }
            }
            result.Add(entry);
        }
        if (result.Count == 0) { throw new InvalidDataException("The successor ledger is empty."); }
        return result;
    }

    private static void ValidateEvolution(M1Slice6SuccessorCampaignLedgerEntry? prior,
        M1Slice6SuccessorCampaignLedgerEntry entry)
    {
        if (entry.CampaignId.Length == 0 || entry.CampaignManifestSha256.Length != 64
            || entry.TerminalCampaignId != RequiredTerminalCampaignId
            || entry.TerminalEventHash != RequiredTerminalEventHash
            || entry.PriorConservativeNanoUsd != PriorConservativeNanoUsd
            || entry.SuccessorCumulativeReservedNanoUsd < 0
            || entry.Wp9PossibleStarts is < 1 or > MaximumPossibleStartsPerStage
            || entry.Wp10PossibleStarts is < 0 or > MaximumPossibleStartsPerStage
            || entry.Wp11PossibleStarts is < 0 or > MaximumPossibleStartsPerStage
            || entry.SuccessorOutstandingReservedNanoUsd < 0 || entry.SuccessorUnresolvedNanoUsd < 0
            || entry.SuccessorSettledNanoUsd < 0
            || checked(entry.PriorConservativeNanoUsd + entry.SuccessorCumulativeReservedNanoUsd) > SliceMaximumNanoUsd
            || checked(entry.SuccessorOutstandingReservedNanoUsd + entry.SuccessorUnresolvedNanoUsd
                + entry.SuccessorSettledNanoUsd) > entry.SuccessorCumulativeReservedNanoUsd)
        {
            throw new InvalidDataException("The successor ledger violates its closed counters or budget.");
        }
        if (prior is null)
        {
            if (entry.State != M1Slice6SuccessorCampaignState.Ready || entry.Event != "successor-campaign-ready"
                || entry.Sequence != 1 || entry.Wp9PossibleStarts != 1 || entry.Wp10PossibleStarts != 0
                || entry.Wp11PossibleStarts != 0 || entry.Attempt is not null || entry.EvidenceId.Length != 0
                || entry.SuccessorOutstandingReservedNanoUsd != 0 || entry.SuccessorUnresolvedNanoUsd != 0
                || entry.SuccessorSettledNanoUsd != 0 || entry.SuccessorCumulativeReservedNanoUsd != 0)
            { throw new InvalidDataException("The successor ledger first event is not exact terminal-lineage authority."); }
            return;
        }
        if (entry.CampaignId != prior.CampaignId || entry.CampaignManifestSha256 != prior.CampaignManifestSha256
            || entry.TerminalCampaignId != prior.TerminalCampaignId
            || entry.RecordedAtUtc < prior.RecordedAtUtc || entry.Wp9PossibleStarts < prior.Wp9PossibleStarts
            || entry.Wp10PossibleStarts < prior.Wp10PossibleStarts || entry.Wp11PossibleStarts < prior.Wp11PossibleStarts
            || entry.SuccessorCumulativeReservedNanoUsd < prior.SuccessorCumulativeReservedNanoUsd
            || entry.SuccessorSettledNanoUsd < prior.SuccessorSettledNanoUsd
            || entry.SuccessorUnresolvedNanoUsd < prior.SuccessorUnresolvedNanoUsd
            || prior.Wp9Authoritative && !entry.Wp9Authoritative
            || prior.Wp10Authoritative && !entry.Wp10Authoritative
            || prior.Wp11Authoritative && !entry.Wp11Authoritative)
        { throw new InvalidDataException("The successor ledger identity, clock, counters, or stage latch regressed."); }
        bool sameCounters = SameCounters(prior, entry);
        bool sameAttempt = entry.Attempt == prior.Attempt;
        switch (prior.State, entry.State)
        {
            case (M1Slice6SuccessorCampaignState.Ready, M1Slice6SuccessorCampaignState.Reviewed):
                Require(entry.Event == "successor-campaign-independently-reviewed"
                    && entry.Attempt is null && entry.EvidenceId.Length > 0
                    && entry.EvidenceSha256.Length == 64 && entry.FailureDisposition.Length == 0
                    && sameCounters, "campaign review");
                break;
            case (M1Slice6SuccessorCampaignState.Reviewed, M1Slice6SuccessorCampaignState.Admitted):
                Require(entry.Event == "owner-authorized-successor-campaign-admitted"
                    && entry.Attempt is null && entry.EvidenceId == prior.EvidenceId
                    && entry.EvidenceSha256 == prior.EvidenceSha256 && sameCounters,
                    "campaign admission");
                break;
            case (M1Slice6SuccessorCampaignState.Admitted, M1Slice6SuccessorCampaignState.AttemptReserved):
            case (M1Slice6SuccessorCampaignState.CorrectionReviewed, M1Slice6SuccessorCampaignState.AttemptReserved):
            case (M1Slice6SuccessorCampaignState.StageAccepted, M1Slice6SuccessorCampaignState.AttemptReserved):
                {
                    long delta = entry.SuccessorCumulativeReservedNanoUsd
                        - prior.SuccessorCumulativeReservedNanoUsd;
                    Require(entry.Event == "fresh-attempt-reserved" && entry.Attempt is not null
                        && entry.EvidenceId.Length == 0 && entry.EvidenceSha256.Length == 0
                        && entry.FailureDisposition.Length == 0 && prior.SuccessorOutstandingReservedNanoUsd == 0
                        && delta > 0 && entry.SuccessorOutstandingReservedNanoUsd == delta
                        && entry.SuccessorSettledNanoUsd == prior.SuccessorSettledNanoUsd
                        && entry.SuccessorUnresolvedNanoUsd == prior.SuccessorUnresolvedNanoUsd
                        && SameStartsAndLatches(prior, entry), "attempt reservation");
                    break;
                }
            case (M1Slice6SuccessorCampaignState.AttemptReserved, M1Slice6SuccessorCampaignState.AttemptStarted):
                Require(entry.Event == "possible-provider-start-latched" && sameAttempt
                    && entry.EvidenceId.Length == 0 && entry.FailureDisposition.Length == 0
                    && SameBudget(prior, entry) && ExactStartIncrement(prior, entry),
                    "possible-start latch");
                break;
            case (M1Slice6SuccessorCampaignState.AttemptReserved, M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff):
                Require(entry.Event == "prestart-failure-evidence-handoff" && sameAttempt
                    && entry.FailureDisposition == "prestart-failure" && entry.EvidenceId.Length > 0
                    && entry.EvidenceSha256.Length == 64 && entry.SuccessorOutstandingReservedNanoUsd == 0
                    && entry.SuccessorCumulativeReservedNanoUsd == prior.SuccessorCumulativeReservedNanoUsd
                    && entry.SuccessorSettledNanoUsd == prior.SuccessorSettledNanoUsd
                    && entry.SuccessorUnresolvedNanoUsd == prior.SuccessorUnresolvedNanoUsd
                    && SameStartsAndLatches(prior, entry), "pre-start release");
                break;
            case (M1Slice6SuccessorCampaignState.AttemptReserved, M1Slice6SuccessorCampaignState.Stopped):
                Require(entry.Event == "prestart-terminal-safety-stop" && sameAttempt
                    && entry.FailureDisposition == "safety-isolation-breach"
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.SuccessorOutstandingReservedNanoUsd == 0
                    && entry.SuccessorCumulativeReservedNanoUsd == prior.SuccessorCumulativeReservedNanoUsd
                    && entry.SuccessorSettledNanoUsd == prior.SuccessorSettledNanoUsd
                    && entry.SuccessorUnresolvedNanoUsd == prior.SuccessorUnresolvedNanoUsd
                    && SameStartsAndLatches(prior, entry), "prestart terminal safety stop");
                break;
            case (M1Slice6SuccessorCampaignState.AttemptStarted, M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff):
                {
                    long settledDelta = entry.SuccessorSettledNanoUsd - prior.SuccessorSettledNanoUsd;
                    long unresolvedDelta = entry.SuccessorUnresolvedNanoUsd - prior.SuccessorUnresolvedNanoUsd;
                    bool authoritative = entry.Event == "first-structurally-valid-response-authoritative";
                    Require(sameAttempt && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                        && entry.SuccessorOutstandingReservedNanoUsd == 0
                        && entry.SuccessorCumulativeReservedNanoUsd == prior.SuccessorCumulativeReservedNanoUsd
                        && settledDelta >= 0 && unresolvedDelta >= 0
                        && checked(settledDelta + unresolvedDelta) <= prior.SuccessorOutstandingReservedNanoUsd
                        && SameStarts(prior, entry)
                        && (authoritative
                            ? entry.FailureDisposition.Length == 0 && ExactAuthorityLatch(prior, entry)
                            : entry.Event == "attempt-failure-evidence-handoff"
                                && entry.FailureDisposition.Length > 0 && SameLatches(prior, entry)),
                        "attempt evidence handoff");
                    break;
                }
            case (M1Slice6SuccessorCampaignState.AttemptStarted, M1Slice6SuccessorCampaignState.Stopped):
                Require(entry.Event == "terminal-safety-stop" && sameAttempt
                    && entry.FailureDisposition == "safety-isolation-breach"
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.SuccessorOutstandingReservedNanoUsd == 0
                    && entry.SuccessorCumulativeReservedNanoUsd == prior.SuccessorCumulativeReservedNanoUsd
                    && SameStartsAndLatches(prior, entry), "terminal safety stop");
                break;
            case (M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff,
                  M1Slice6SuccessorCampaignState.AttemptFailureAccepted):
                Require(entry.Event == "attempt-failure-evidence-independently-accepted"
                    && prior.FailureDisposition.Length > 0 && sameAttempt
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.FailureDisposition == prior.FailureDisposition && sameCounters,
                    "failure evidence acceptance");
                break;
            case (M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff,
                  M1Slice6SuccessorCampaignState.StageAccepted):
                Require(entry.Event == "authoritative-stage-evidence-independently-accepted"
                    && prior.FailureDisposition.Length == 0 && sameAttempt
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.FailureDisposition.Length == 0 && sameCounters,
                    "stage evidence acceptance");
                break;
            case (M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff,
                  M1Slice6SuccessorCampaignState.AuthoritativeRecoveryHandoff):
                Require(prior.FailureDisposition.Length == 0 && sameAttempt
                    && entry.Event == "authoritative-retained-response-semantic-recovery-handoff"
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.FailureDisposition.Length == 0 && sameCounters,
                    "authoritative semantic recovery handoff");
                break;
            case (M1Slice6SuccessorCampaignState.AuthoritativeRecoveryHandoff,
                  M1Slice6SuccessorCampaignState.StageAccepted):
                Require(entry.Event == "authoritative-stage-evidence-independently-accepted"
                    && prior.FailureDisposition.Length == 0 && sameAttempt
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.FailureDisposition.Length == 0 && sameCounters,
                    "recovered stage evidence acceptance");
                break;
            case (M1Slice6SuccessorCampaignState.AttemptFailureAccepted,
                  M1Slice6SuccessorCampaignState.CorrectionReviewed):
                Require(entry.Event == "offline-diagnosis-and-optional-correction-independently-reviewed"
                    && sameAttempt && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.FailureDisposition.StartsWith("defect:", StringComparison.Ordinal)
                    && entry.FailureDisposition.Length > 7 && sameCounters,
                    "correction review");
                break;
            case (M1Slice6SuccessorCampaignState.StageAccepted,
                  M1Slice6SuccessorCampaignState.ComposedEvidenceHandoff):
                Require(entry.Event == "effect-free-c3-composed-evidence-handoff"
                    && entry.Attempt is null && entry.FailureDisposition.Length == 0
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.Wp9Authoritative && entry.Wp10Authoritative && entry.Wp11Authoritative
                    && sameCounters, "C3 evidence handoff");
                break;
            case (M1Slice6SuccessorCampaignState.ComposedEvidenceHandoff,
                  M1Slice6SuccessorCampaignState.Completed):
                Require(entry.Event == "effect-free-c3-composed-evidence-independently-accepted"
                    && entry.Attempt is null && entry.FailureDisposition.Length == 0
                    && entry.EvidenceId.Length > 0 && entry.EvidenceSha256.Length == 64
                    && entry.Wp9Authoritative && entry.Wp10Authoritative && entry.Wp11Authoritative
                    && sameCounters, "C3 independent acceptance");
                break;
            default:
                throw new InvalidDataException("The successor ledger state transition is not closed.");
        }
    }

    private static void Require(bool condition, string transition)
    {
        if (!condition) { throw new InvalidDataException("The successor ledger has an invalid " + transition + " transition."); }
    }

    private static bool SameCounters(M1Slice6SuccessorCampaignLedgerEntry a,
        M1Slice6SuccessorCampaignLedgerEntry b) => SameBudget(a, b) && SameStartsAndLatches(a, b);

    private static bool SameBudget(M1Slice6SuccessorCampaignLedgerEntry a,
        M1Slice6SuccessorCampaignLedgerEntry b) =>
        a.SuccessorCumulativeReservedNanoUsd == b.SuccessorCumulativeReservedNanoUsd
        && a.SuccessorOutstandingReservedNanoUsd == b.SuccessorOutstandingReservedNanoUsd
        && a.SuccessorUnresolvedNanoUsd == b.SuccessorUnresolvedNanoUsd
        && a.SuccessorSettledNanoUsd == b.SuccessorSettledNanoUsd;

    private static bool SameStartsAndLatches(M1Slice6SuccessorCampaignLedgerEntry a,
        M1Slice6SuccessorCampaignLedgerEntry b) => SameStarts(a, b) && SameLatches(a, b);

    private static bool SameStarts(M1Slice6SuccessorCampaignLedgerEntry a,
        M1Slice6SuccessorCampaignLedgerEntry b) => a.Wp9PossibleStarts == b.Wp9PossibleStarts
        && a.Wp10PossibleStarts == b.Wp10PossibleStarts && a.Wp11PossibleStarts == b.Wp11PossibleStarts;

    private static bool SameLatches(M1Slice6SuccessorCampaignLedgerEntry a,
        M1Slice6SuccessorCampaignLedgerEntry b) => a.Wp9Authoritative == b.Wp9Authoritative
        && a.Wp10Authoritative == b.Wp10Authoritative && a.Wp11Authoritative == b.Wp11Authoritative;

    private static bool ExactStartIncrement(M1Slice6SuccessorCampaignLedgerEntry a,
        M1Slice6SuccessorCampaignLedgerEntry b) => b.Attempt?.Stage switch
        {
            M1Slice6CampaignStage.Qualification => b.Wp9PossibleStarts == a.Wp9PossibleStarts + 1
                && b.Wp10PossibleStarts == a.Wp10PossibleStarts && b.Wp11PossibleStarts == a.Wp11PossibleStarts,
            M1Slice6CampaignStage.SourceClaimExtraction => b.Wp10PossibleStarts == a.Wp10PossibleStarts + 1
                && b.Wp9PossibleStarts == a.Wp9PossibleStarts && b.Wp11PossibleStarts == a.Wp11PossibleStarts,
            M1Slice6CampaignStage.CandidateInvestigation => b.Wp11PossibleStarts == a.Wp11PossibleStarts + 1
                && b.Wp9PossibleStarts == a.Wp9PossibleStarts && b.Wp10PossibleStarts == a.Wp10PossibleStarts,
            _ => false,
        } && SameLatches(a, b);

    private static bool ExactAuthorityLatch(M1Slice6SuccessorCampaignLedgerEntry a,
        M1Slice6SuccessorCampaignLedgerEntry b) => b.Attempt?.Stage switch
        {
            M1Slice6CampaignStage.Qualification => !a.Wp9Authoritative && b.Wp9Authoritative
                && a.Wp10Authoritative == b.Wp10Authoritative && a.Wp11Authoritative == b.Wp11Authoritative,
            M1Slice6CampaignStage.SourceClaimExtraction => !a.Wp10Authoritative && b.Wp10Authoritative
                && a.Wp9Authoritative == b.Wp9Authoritative && a.Wp11Authoritative == b.Wp11Authoritative,
            M1Slice6CampaignStage.CandidateInvestigation => !a.Wp11Authoritative && b.Wp11Authoritative
                && a.Wp9Authoritative == b.Wp9Authoritative && a.Wp10Authoritative == b.Wp10Authoritative,
            _ => false,
        };

    private static string Hash(M1Slice6SuccessorCampaignLedgerEntry entry) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(entry, Json)));

    private FileStream Lock() => new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
        FileShare.None, 1, FileOptions.WriteThrough);

    private void RequireClock(DateTimeOffset now)
    {
        if (now.Offset != TimeSpan.Zero || entries.Count > 0 && now < entries[^1].RecordedAtUtc)
        {
            throw new InvalidOperationException("The successor ledger clock is non-UTC or moved backwards.");
        }
    }

    private static void ValidateAttempt(M1Slice6SuccessorAttemptIdentity attempt)
    {
        _ = Identity(attempt.AttemptId);
        _ = Identity(attempt.StageManifestId);
        _ = Hex(attempt.StageManifestSha256);
        _ = Identity(attempt.RuntimeAuthorityId);
        _ = Hex(attempt.RuntimeAuthoritySha256);
        _ = Identity(attempt.RequestId);
        _ = Identity(attempt.ReservationId);
        _ = Identity(attempt.DispatchFenceId);
        if (attempt.AttemptOrdinal is <= 0 or > 100 || attempt.Stage == M1Slice6CampaignStage.None)
        {
            throw new InvalidDataException("The successor attempt identity is incomplete.");
        }
    }

    private static string Identity(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256 && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '/' or ':' or '_' or '-')
            ? value : throw new InvalidDataException("A successor identity is malformed.");

    private static string Hex(string value) => value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
            ? value : throw new InvalidDataException("A successor digest is malformed.");

    private static string Failure(string value) => string.IsNullOrEmpty(value) ? ""
        : value is "transport-ambiguous" or "provider-failed" or "provider-refused"
            or "provider-malformed" or "provider-incomplete" or "provider-oversized"
            or "helper-evidence-failure" or "retention-failure" or "prestart-failure"
            or "safety-isolation-breach"
            ? value : throw new InvalidDataException("The successor failure disposition is not closed.");
}
