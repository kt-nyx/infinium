using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Persistence;

public enum M1Slice6CampaignState
{
    Ready, Reviewed, Admitted, CredentialExecutionHandoff, CredentialEvidenceHandoff,
    CredentialEvidenceAccepted, StageReserved, TransportMayHaveStarted, StageSettled, StageEvidenceHandoff,
    StageAccepted, Completed, Stopped,
}

public enum M1Slice6CampaignStage { None, Qualification, SourceClaimExtraction, CandidateInvestigation }

public sealed record M1Slice6CampaignIdentity(
    string CampaignId,
    string CampaignManifestSha256,
    string AuthorityAttachmentSha256,
    string VerificationCandidateCommit,
    string CredentialManifestId,
    string CredentialManifestSha256,
    string CredentialProfileId,
    string CredentialGenerationId,
    string CredentialTargetFingerprintSha256);

public sealed record M1Slice6CampaignStageLimits(
    long MaximumRequestBytes, long MaximumInputTokens, long MaximumOutputTokens,
    long MaximumRawResponseBytes, long MaximumNanoUsd, long DeadlineMilliseconds)
{
    public static M1Slice6CampaignStageLimits For(M1Slice6CampaignStage stage) => stage switch
    {
        M1Slice6CampaignStage.Qualification => new(16_384, 20_480, 256, 262_144, 140_000_000, 60_000),
        M1Slice6CampaignStage.SourceClaimExtraction or M1Slice6CampaignStage.CandidateInvestigation =>
            new(65_536, 73_728, 4_096, 1_048_576, 600_000_000, 120_000),
        _ => throw new InvalidOperationException("The campaign stage is not dispatchable."),
    };
}

public sealed record M1Slice6CampaignStageReservation(
    string RequestManifestId, string RequestManifestSha256, long RequestBytes, long InputTokens,
    long OutputTokens, long RawResponseBytes, long ReservedNanoUsd);

public sealed record M1Slice6CampaignNativeEnvelope(
    long CredWriteW, long CredReadW, long CredDeleteW, long CredFree, long Total);

public sealed record M1Slice6CampaignLedgerEntry(
    long Sequence, M1Slice6CampaignIdentity Identity, M1Slice6CampaignState State,
    M1Slice6CampaignStage Stage, string Event, string RequestManifestId, string RequestManifestSha256,
    string EvidenceId, string EvidenceSha256, long ProviderCallCount, long DnsResolutionCount,
    long AggregateRequestBytes, long AggregateInputTokens, long AggregateOutputTokens,
    long AggregateRawResponseBytes, long ReservedNanoUsd, long SettledNanoUsd,
    long ObservedInputTokens, long ObservedOutputTokens, long ObservedRawResponseBytes,
    DateTimeOffset? StageDeadlineUtc, bool PossibleStartLatched, string SafetyIdentifierProjection,
    string PreviousHash, string EventHash, DateTimeOffset RecordedAtUtc)
{
    public M1Slice6CampaignNativeEnvelope NativeEnvelope { get; init; } = new(0, 0, 0, 0, 0);
}

/// <summary>Coordinator-owned append-only exact-identity campaign authority/effect ledger.</summary>
public sealed class M1Slice6FiniteCampaignLedger
{
    public const long AggregateMaximumRequestBytes = 147_456;
    public const long AggregateMaximumInputTokens = 167_936;
    public const long AggregateMaximumOutputTokens = 8_448;
    public const long AggregateMaximumRawResponseBytes = 2_359_296;
    public const long AggregateMaximumNanoUsd = 1_340_000_000;
    public const int AggregateMaximumProviderCalls = 3;
    public const int AggregateMaximumDnsResolutions = 3;

    private readonly string path;
    private readonly string lockPath;
    private readonly M1Slice6CampaignIdentity identity;
    private readonly DateTimeOffset campaignExpiresAtUtc;
    private readonly DateTimeOffset credentialExpiresAtUtc;
    private readonly object gate = new();
    private readonly List<M1Slice6CampaignLedgerEntry> entries;

    public M1Slice6FiniteCampaignLedger(string path, M1Slice6CampaignIdentity identity,
        DateTimeOffset campaignExpiresAtUtc, DateTimeOffset credentialExpiresAtUtc, DateTimeOffset now)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        lockPath = this.path + ".lock";
        this.identity = ValidateIdentity(identity);
        this.campaignExpiresAtUtc = RequireUtc(campaignExpiresAtUtc, nameof(campaignExpiresAtUtc));
        this.credentialExpiresAtUtc = RequireUtc(credentialExpiresAtUtc, nameof(credentialExpiresAtUtc));
        if (credentialExpiresAtUtc >= campaignExpiresAtUtc)
        {
            throw new ArgumentException("Credential expiry must precede campaign expiry.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        using FileStream lease = AcquireExclusiveLock();
        if (File.Exists(this.path))
        {
            entries = ReadAndValidate();
            if (entries[0].Identity != this.identity)
            {
                throw new InvalidDataException("The campaign ledger identity is stale.");
            }
            RequireMonotonicClock(now);
        }
        else
        {
            entries = [];
            AppendUnlocked(M1Slice6CampaignState.Ready, M1Slice6CampaignStage.None, "campaign-ready", "", "", "", "",
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, false, "", now);
        }
    }

    public IReadOnlyList<M1Slice6CampaignLedgerEntry> Entries => entries.AsReadOnly();
    public M1Slice6CampaignLedgerEntry Current => entries[^1];
    public M1Slice6CampaignNativeEnvelope CurrentNativeEnvelope => Current.NativeEnvelope;

    public void RecordIndependentReview(DateTimeOffset now) =>
        Transition(M1Slice6CampaignState.Ready, M1Slice6CampaignState.Reviewed, "independent-review-accepted", now);

    public void AdmitCampaign(DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        if (now >= campaignExpiresAtUtc)
        {
            StopBeforeEffect("campaign-expired-before-admission", now);
            throw new InvalidOperationException("Campaign admission expired and is terminally stopped.");
        }
        Transition(M1Slice6CampaignState.Reviewed, M1Slice6CampaignState.Admitted, "exact-campaign-admitted", now);
    }

    public void BeginCredentialExecutionHandoff(DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        if (now >= credentialExpiresAtUtc)
        {
            StopBeforeEffect("credential-expired-before-handoff", now);
            throw new InvalidOperationException("Credential handoff expired and is terminally stopped.");
        }
        Transition(M1Slice6CampaignState.Admitted, M1Slice6CampaignState.CredentialExecutionHandoff,
            "credential-execution-handoff", now);
    }

    public void RecordCredentialEvidenceHandoff(string evidenceId, string evidenceSha256,
        M1Slice6CampaignNativeEnvelope nativeEnvelope, DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        if (Current.State != M1Slice6CampaignState.CredentialExecutionHandoff)
        {
            throw new InvalidOperationException("Credential evidence handoff has a stale predecessor.");
        }
        Append(M1Slice6CampaignState.CredentialEvidenceHandoff, M1Slice6CampaignStage.None,
            "credential-evidence-handoff", "", "", RequireIdentity(evidenceId), RequireHex(evidenceSha256, 64),
            Current.ProviderCallCount, Current.DnsResolutionCount, Current.AggregateRequestBytes,
            Current.AggregateInputTokens, Current.AggregateOutputTokens, Current.AggregateRawResponseBytes,
            Current.ReservedNanoUsd, Current.SettledNanoUsd, null, Current.PossibleStartLatched,
            Current.SafetyIdentifierProjection, Current.ObservedInputTokens, Current.ObservedOutputTokens,
            Current.ObservedRawResponseBytes, now, ValidateNativeEnvelope(nativeEnvelope, credential: true));
    }

    public void AcceptCredentialEvidence(string evidenceId, string evidenceSha256, DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        string exactId = RequireIdentity(evidenceId);
        string exactSha = RequireHex(evidenceSha256, 64);
        if (Current.State != M1Slice6CampaignState.CredentialEvidenceHandoff
            || Current.EvidenceId != exactId || Current.EvidenceSha256 != exactSha)
        {
            throw new InvalidOperationException("Credential evidence acceptance has a stale or changed handoff.");
        }
        Append(M1Slice6CampaignState.CredentialEvidenceAccepted, M1Slice6CampaignStage.None,
            "credential-evidence-independently-accepted", "", "", exactId, exactSha,
            Current.ProviderCallCount, Current.DnsResolutionCount, Current.AggregateRequestBytes,
            Current.AggregateInputTokens, Current.AggregateOutputTokens, Current.AggregateRawResponseBytes,
            Current.ReservedNanoUsd, Current.SettledNanoUsd, null, Current.PossibleStartLatched,
            Current.SafetyIdentifierProjection, Current.ObservedInputTokens, Current.ObservedOutputTokens,
            Current.ObservedRawResponseBytes, now);
    }

    public void StopCredentialHandoff(string reason, string evidenceId, string evidenceSha256, DateTimeOffset now)
    {
        string exactReason = reason switch
        {
            "owner-cancelled" or "preflight-collision" or "readiness-failure" or "native-failure"
                or "cleanup-ambiguity" or "helper-evidence-ambiguity" => reason,
            _ => throw new ArgumentException("Unknown credential terminal reason.", nameof(reason)),
        };
        if (Current.State != M1Slice6CampaignState.CredentialExecutionHandoff)
        {
            throw new InvalidOperationException("Credential terminal evidence has a stale handoff.");
        }
        Append(M1Slice6CampaignState.Stopped, M1Slice6CampaignStage.None,
            "credential-" + exactReason + "-terminal-stop", "", "", RequireIdentity(evidenceId),
            RequireHex(evidenceSha256, 64), Current.ProviderCallCount, Current.DnsResolutionCount,
            Current.AggregateRequestBytes, Current.AggregateInputTokens, Current.AggregateOutputTokens,
            Current.AggregateRawResponseBytes, Current.ReservedNanoUsd, Current.SettledNanoUsd, null,
            Current.PossibleStartLatched, Current.SafetyIdentifierProjection, Current.ObservedInputTokens,
            Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void ReserveStage(M1Slice6CampaignStage stage, M1Slice6CampaignStageReservation reservation, DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        if (now >= campaignExpiresAtUtc)
        {
            StopBeforeEffect("campaign-expired-before-stage-reservation", now);
            throw new InvalidOperationException("Stage reservation expired and is terminally stopped.");
        }
        M1Slice6CampaignStage expected = NextStage(Current);
        M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(stage);
        if (stage != expected || Current.ReservedNanoUsd != 0 || reservation.RequestBytes is <= 0
            || reservation.InputTokens is < 0 || reservation.OutputTokens is <= 0 || reservation.RawResponseBytes <= 0
            || reservation.ReservedNanoUsd <= 0 || reservation.RequestBytes > limits.MaximumRequestBytes
            || reservation.InputTokens > limits.MaximumInputTokens || reservation.OutputTokens > limits.MaximumOutputTokens
            || reservation.RawResponseBytes > limits.MaximumRawResponseBytes || reservation.ReservedNanoUsd > limits.MaximumNanoUsd)
        {
            throw new InvalidOperationException("The stage reservation exceeds its exact per-stage envelope.");
        }

        long requests = checked(Current.AggregateRequestBytes + reservation.RequestBytes);
        long inputs = checked(Current.AggregateInputTokens + reservation.InputTokens);
        long outputs = checked(Current.AggregateOutputTokens + reservation.OutputTokens);
        long raw = checked(Current.AggregateRawResponseBytes + reservation.RawResponseBytes);
        if (requests > AggregateMaximumRequestBytes || inputs > AggregateMaximumInputTokens
            || outputs > AggregateMaximumOutputTokens || raw > AggregateMaximumRawResponseBytes
            || checked(Current.SettledNanoUsd + reservation.ReservedNanoUsd) > AggregateMaximumNanoUsd)
        {
            throw new InvalidOperationException("The stage reservation exceeds the atomic aggregate campaign envelope.");
        }

        DateTimeOffset deadline = now.AddMilliseconds(limits.DeadlineMilliseconds);
        Append(M1Slice6CampaignState.StageReserved, stage, "stage-reserved", RequireIdentity(reservation.RequestManifestId),
            RequireHex(reservation.RequestManifestSha256, 64), "", "", Current.ProviderCallCount,
            Current.DnsResolutionCount, requests, inputs, outputs, raw, reservation.ReservedNanoUsd,
            Current.SettledNanoUsd, deadline, Current.PossibleStartLatched, Current.SafetyIdentifierProjection,
            Current.ObservedInputTokens, Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void LatchPossibleStart(M1Slice6CampaignStage stage, string safetyIdentifierProjection, DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        if (now >= campaignExpiresAtUtc)
        {
            StopBeforePossibleStart(stage, "campaign-expired-before-possible-start", now);
            throw new InvalidOperationException("Possible start expired and is terminally stopped.");
        }
        if (Current.State != M1Slice6CampaignState.StageReserved || Current.Stage != stage
            || Current.StageDeadlineUtc is null || now >= Current.StageDeadlineUtc
            || Current.ProviderCallCount >= AggregateMaximumProviderCalls || Current.DnsResolutionCount >= AggregateMaximumDnsResolutions
            || !ProductUserSafetyIdentifier.IsValidProjection(safetyIdentifierProjection)
            || (Current.SafetyIdentifierProjection.Length != 0 && Current.SafetyIdentifierProjection != safetyIdentifierProjection))
        {
            throw new InvalidOperationException("A provider call cannot start outside its exact one-shot stage/deadline binding.");
        }

        Append(M1Slice6CampaignState.TransportMayHaveStarted, stage, "transport-may-have-started",
            Current.RequestManifestId, Current.RequestManifestSha256, "", "", Current.ProviderCallCount + 1,
            Current.DnsResolutionCount + 1, Current.AggregateRequestBytes, Current.AggregateInputTokens,
            Current.AggregateOutputTokens, Current.AggregateRawResponseBytes, Current.ReservedNanoUsd,
            Current.SettledNanoUsd, Current.StageDeadlineUtc, true, safetyIdentifierProjection,
            Current.ObservedInputTokens, Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void RecordStageEvidenceHandoff(M1Slice6CampaignStage stage, string evidenceId, string evidenceSha256,
        long observedInputTokens, long observedOutputTokens, long observedRawResponseBytes,
        long settledNanoUsd, M1Slice6CampaignNativeEnvelope stageNativeTrace, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.StageSettled || Current.Stage != stage
            || !Current.PossibleStartLatched || Current.StageDeadlineUtc is null)
        {
            throw new InvalidOperationException("Stage evidence has no exact known-settled predecessor.");
        }
        M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(stage);
        if (observedInputTokens is < 0 || observedInputTokens > limits.MaximumInputTokens
            || observedOutputTokens is < 0 || observedOutputTokens > limits.MaximumOutputTokens
            || observedRawResponseBytes is < 0 || observedRawResponseBytes > limits.MaximumRawResponseBytes
            || settledNanoUsd < 0 || stageNativeTrace != new M1Slice6CampaignNativeEnvelope(0, 1, 0, 1, 2))
        {
            throw new InvalidOperationException("Stage evidence differs from its known-settled finite result.");
        }
        Append(M1Slice6CampaignState.StageEvidenceHandoff, stage, "stage-evidence-handoff", Current.RequestManifestId,
            Current.RequestManifestSha256, RequireIdentity(evidenceId), RequireHex(evidenceSha256, 64),
            Current.ProviderCallCount, Current.DnsResolutionCount, Current.AggregateRequestBytes,
            Current.AggregateInputTokens, Current.AggregateOutputTokens, Current.AggregateRawResponseBytes,
            0, Current.SettledNanoUsd, Current.StageDeadlineUtc, true, Current.SafetyIdentifierProjection,
            Current.ObservedInputTokens, Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void RecordKnownSettlement(M1Slice6CampaignStage stage, long observedInputTokens,
        long observedOutputTokens, long observedRawResponseBytes, long settledNanoUsd,
        M1Slice6CampaignNativeEnvelope stageNativeTrace, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.TransportMayHaveStarted || Current.Stage != stage
            || !Current.PossibleStartLatched || Current.StageDeadlineUtc is null)
        {
            throw new InvalidOperationException("Known settlement has no exact possible-start predecessor.");
        }
        M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(stage);
        if (now >= Current.StageDeadlineUtc || settledNanoUsd < 0 || settledNanoUsd > Current.ReservedNanoUsd
            || observedInputTokens is < 0 || observedInputTokens > limits.MaximumInputTokens
            || observedOutputTokens is < 0 || observedOutputTokens > limits.MaximumOutputTokens
            || observedRawResponseBytes is < 0 || observedRawResponseBytes > limits.MaximumRawResponseBytes)
        {
            StopAfterAmbiguousStart(stage, "settlement-overrun", now);
            throw new InvalidOperationException("Known settlement exceeded its exact stage envelope.");
        }
        M1Slice6CampaignNativeEnvelope exactStage = ValidateNativeEnvelope(stageNativeTrace, credential: false);
        M1Slice6CampaignNativeEnvelope cumulative = new(
            checked(Current.NativeEnvelope.CredWriteW + exactStage.CredWriteW),
            checked(Current.NativeEnvelope.CredReadW + exactStage.CredReadW),
            checked(Current.NativeEnvelope.CredDeleteW + exactStage.CredDeleteW),
            checked(Current.NativeEnvelope.CredFree + exactStage.CredFree),
            checked(Current.NativeEnvelope.Total + exactStage.Total));
        long aggregate = checked(Current.SettledNanoUsd + settledNanoUsd);
        long aggregateInput = checked(Current.ObservedInputTokens + observedInputTokens);
        long aggregateOutput = checked(Current.ObservedOutputTokens + observedOutputTokens);
        long aggregateRaw = checked(Current.ObservedRawResponseBytes + observedRawResponseBytes);
        if (aggregate > AggregateMaximumNanoUsd || aggregateInput > AggregateMaximumInputTokens
            || aggregateOutput > AggregateMaximumOutputTokens || aggregateRaw > AggregateMaximumRawResponseBytes)
        {
            StopAfterAmbiguousStart(stage, "settlement-overrun", now);
            throw new InvalidOperationException("Known settlement exceeded the atomic campaign envelope.");
        }
        Append(M1Slice6CampaignState.StageSettled, stage, "stage-known-settled-no-retry",
            Current.RequestManifestId, Current.RequestManifestSha256, "", "", Current.ProviderCallCount,
            Current.DnsResolutionCount, Current.AggregateRequestBytes, Current.AggregateInputTokens,
            Current.AggregateOutputTokens, Current.AggregateRawResponseBytes, 0, aggregate,
            Current.StageDeadlineUtc, true, Current.SafetyIdentifierProjection, aggregateInput,
            aggregateOutput, aggregateRaw, now, cumulative);
    }

    public void StopAfterKnownSettlement(M1Slice6CampaignStage stage, string reason, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.StageSettled || Current.Stage != stage
            || reason is not ("evidence-write-failure" or "evidence-serialization-failure"
                or "semantic-admission-failure" or "reconciled-sqlite-settlement"))
        {
            throw new InvalidOperationException("Only a known-settled stage may stop as evidence-unreviewable.");
        }
        Append(M1Slice6CampaignState.Stopped, stage, reason + "-known-settled-no-retry",
            Current.RequestManifestId, Current.RequestManifestSha256, "", "", Current.ProviderCallCount,
            Current.DnsResolutionCount, Current.AggregateRequestBytes, Current.AggregateInputTokens,
            Current.AggregateOutputTokens, Current.AggregateRawResponseBytes, 0, Current.SettledNanoUsd,
            Current.StageDeadlineUtc, true, Current.SafetyIdentifierProjection, Current.ObservedInputTokens,
            Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void AcceptStageEvidence(M1Slice6CampaignStage stage, string evidenceId, string evidenceSha256,
        DateTimeOffset now)
    {
        string exactId = RequireIdentity(evidenceId);
        string exactSha = RequireHex(evidenceSha256, 64);
        if (Current.State != M1Slice6CampaignState.StageEvidenceHandoff || Current.Stage != stage
            || Current.EvidenceId != exactId || Current.EvidenceSha256 != exactSha
            || Current.StageDeadlineUtc is null)
        {
            throw new InvalidOperationException("Stage evidence acceptance has a stale or changed handoff.");
        }
        Append(M1Slice6CampaignState.StageAccepted, stage, "stage-evidence-independently-accepted",
            Current.RequestManifestId, Current.RequestManifestSha256, exactId, exactSha,
            Current.ProviderCallCount, Current.DnsResolutionCount, Current.AggregateRequestBytes,
            Current.AggregateInputTokens, Current.AggregateOutputTokens, Current.AggregateRawResponseBytes,
            0, Current.SettledNanoUsd, Current.StageDeadlineUtc, true, Current.SafetyIdentifierProjection,
            Current.ObservedInputTokens, Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void CompleteComposedEvidence(string evidenceId, string evidenceSha256, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.StageAccepted
            || Current.Stage != M1Slice6CampaignStage.CandidateInvestigation
            || Current.ProviderCallCount != AggregateMaximumProviderCalls
            || Current.DnsResolutionCount != AggregateMaximumDnsResolutions || Current.ReservedNanoUsd != 0)
        {
            throw new InvalidOperationException("Composed closeout requires three exact accepted stages and no remaining hold.");
        }
        Append(M1Slice6CampaignState.Completed, M1Slice6CampaignStage.None, "composed-evidence-independently-accepted",
            "", "", RequireIdentity(evidenceId), RequireHex(evidenceSha256, 64), Current.ProviderCallCount,
            Current.DnsResolutionCount, Current.AggregateRequestBytes, Current.AggregateInputTokens,
            Current.AggregateOutputTokens, Current.AggregateRawResponseBytes, 0, Current.SettledNanoUsd,
            null, true, Current.SafetyIdentifierProjection, Current.ObservedInputTokens,
            Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void StopAfterAmbiguousStart(M1Slice6CampaignStage stage, string reason, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.TransportMayHaveStarted || Current.Stage != stage)
        {
            throw new InvalidOperationException("Only the current possible-start stage may stop ambiguously.");
        }

        string exactReason = reason switch
        {
            "ambiguous-start" => reason,
            "deadline-overrun" => reason,
            "settlement-overrun" => reason,
            "stage-processing-failure" => reason,
            _ => throw new ArgumentException("Unknown campaign stop reason.", nameof(reason)),
        };
        Append(M1Slice6CampaignState.Stopped, stage, exactReason + "-hold-retained-no-retry", Current.RequestManifestId,
            Current.RequestManifestSha256, "", "", Current.ProviderCallCount, Current.DnsResolutionCount,
            Current.AggregateRequestBytes, Current.AggregateInputTokens, Current.AggregateOutputTokens,
            Current.AggregateRawResponseBytes, Current.ReservedNanoUsd, Current.SettledNanoUsd,
            Current.StageDeadlineUtc, true, Current.SafetyIdentifierProjection, Current.ObservedInputTokens,
            Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    public void StopBeforePossibleStart(M1Slice6CampaignStage stage, string reason, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.StageReserved || Current.Stage != stage
            || reason is not ("safety-state-missing" or "safety-state-corrupt" or "safety-projection-drift"
                or "stage-prestart-failure" or "campaign-expired-before-possible-start"))
        {
            throw new InvalidOperationException("Only an exact reserved stage may stop before possible start.");
        }
        Append(M1Slice6CampaignState.Stopped, stage, reason + "-released-undispatched-terminal-stop",
            Current.RequestManifestId, Current.RequestManifestSha256, Current.EvidenceId,
            Current.EvidenceSha256, Current.ProviderCallCount, Current.DnsResolutionCount,
            Current.AggregateRequestBytes, Current.AggregateInputTokens, Current.AggregateOutputTokens,
            Current.AggregateRawResponseBytes, 0, Current.SettledNanoUsd, Current.StageDeadlineUtc,
            Current.PossibleStartLatched, Current.SafetyIdentifierProjection, Current.ObservedInputTokens,
            Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    private void Transition(M1Slice6CampaignState from, M1Slice6CampaignState to, string eventName, DateTimeOffset now)
    {
        if (Current.State != from)
        {
            throw new InvalidOperationException($"Campaign transition {eventName} has a stale predecessor.");
        }

        Append(to, M1Slice6CampaignStage.None, eventName, "", "", "", "", Current.ProviderCallCount,
            Current.DnsResolutionCount, Current.AggregateRequestBytes, Current.AggregateInputTokens,
            Current.AggregateOutputTokens, Current.AggregateRawResponseBytes, Current.ReservedNanoUsd,
            Current.SettledNanoUsd, null, Current.PossibleStartLatched, Current.SafetyIdentifierProjection,
            Current.ObservedInputTokens, Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    private void StopBeforeEffect(string eventName, DateTimeOffset now)
    {
        if (Current.State == M1Slice6CampaignState.Stopped || Current.State == M1Slice6CampaignState.Completed)
        {
            throw new InvalidOperationException("The campaign is already terminal.");
        }
        Append(M1Slice6CampaignState.Stopped, Current.Stage, eventName + "-terminal-stop", Current.RequestManifestId,
            Current.RequestManifestSha256, Current.EvidenceId, Current.EvidenceSha256,
            Current.ProviderCallCount, Current.DnsResolutionCount,
            Current.AggregateRequestBytes, Current.AggregateInputTokens, Current.AggregateOutputTokens,
            Current.AggregateRawResponseBytes, Current.ReservedNanoUsd, Current.SettledNanoUsd,
            Current.StageDeadlineUtc, Current.PossibleStartLatched, Current.SafetyIdentifierProjection,
            Current.ObservedInputTokens, Current.ObservedOutputTokens, Current.ObservedRawResponseBytes, now);
    }

    private static M1Slice6CampaignStage NextStage(M1Slice6CampaignLedgerEntry current) => current.State switch
    {
        M1Slice6CampaignState.CredentialEvidenceAccepted => M1Slice6CampaignStage.Qualification,
        M1Slice6CampaignState.StageAccepted when current.Stage == M1Slice6CampaignStage.Qualification => M1Slice6CampaignStage.SourceClaimExtraction,
        M1Slice6CampaignState.StageAccepted when current.Stage == M1Slice6CampaignStage.SourceClaimExtraction => M1Slice6CampaignStage.CandidateInvestigation,
        _ => throw new InvalidOperationException("The campaign has no next provider stage."),
    };

    private void Append(M1Slice6CampaignState state, M1Slice6CampaignStage stage, string eventName,
        string requestId, string requestSha, string evidenceId, string evidenceSha, long calls, long dns,
        long requestBytes, long inputTokens, long outputTokens, long rawBytes, long reserved, long settled,
        DateTimeOffset? stageDeadline, bool latched, string safetyProjection, long observedInput,
        long observedOutput, long observedRaw, DateTimeOffset now,
        M1Slice6CampaignNativeEnvelope? nativeEnvelope = null)
    {
        lock (gate)
        {
            using FileStream lease = AcquireExclusiveLock();
            List<M1Slice6CampaignLedgerEntry> durable = ReadAndValidate();
            if (durable.Count != entries.Count || durable[^1].EventHash != entries[^1].EventHash)
            {
                throw new InvalidOperationException("The campaign ledger compare-and-swap predecessor changed in another process.");
            }
            AppendUnlocked(state, stage, eventName, requestId, requestSha, evidenceId, evidenceSha, calls, dns,
                requestBytes, inputTokens, outputTokens, rawBytes, reserved, settled, observedInput, observedOutput,
                observedRaw, stageDeadline, latched, safetyProjection, now, nativeEnvelope);
        }
    }

    private void AppendUnlocked(M1Slice6CampaignState state, M1Slice6CampaignStage stage, string eventName,
        string requestId, string requestSha, string evidenceId, string evidenceSha, long calls, long dns,
        long requestBytes, long inputTokens, long outputTokens, long rawBytes, long reserved, long settled,
        long observedInput, long observedOutput, long observedRaw, DateTimeOffset? stageDeadline, bool latched,
        string safetyProjection, DateTimeOffset now,
        M1Slice6CampaignNativeEnvelope? nativeEnvelope = null)
    {
        RequireMonotonicClock(now);
        string previous = entries.Count == 0 ? new string('0', 64) : entries[^1].EventHash;
        long sequence = entries.Count + 1;
        DateTimeOffset utc = RequireUtc(now, nameof(now));
        M1Slice6CampaignNativeEnvelope effectiveNative = nativeEnvelope ?? (entries.Count == 0
            ? new(0, 0, 0, 0, 0) : entries[^1].NativeEnvelope);
        string material = Material(sequence, identity, state, stage, eventName, requestId, requestSha, evidenceId,
            evidenceSha, calls, dns, requestBytes, inputTokens, outputTokens, rawBytes, reserved, settled,
            observedInput, observedOutput, observedRaw, stageDeadline, latched, safetyProjection,
            effectiveNative, previous, utc);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        M1Slice6CampaignLedgerEntry entry = new(sequence, identity, state, stage, eventName, requestId,
            requestSha, evidenceId, evidenceSha, calls, dns, requestBytes, inputTokens, outputTokens, rawBytes,
            reserved, settled, observedInput, observedOutput, observedRaw, stageDeadline, latched,
            safetyProjection, previous, hash, utc)
        {
            NativeEnvelope = effectiveNative,
        };
        if (!IsLegalSuccessor(entries.LastOrDefault(), entry))
        {
            throw new InvalidOperationException("The campaign ledger transition violates its exact successor equation.");
        }
        byte[] line = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
        using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        stream.Write(line); stream.WriteByte((byte)'\n'); stream.Flush(flushToDisk: true); entries.Add(entry);
    }

    private FileStream AcquireExclusiveLock() => new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
        FileShare.None, 1, FileOptions.WriteThrough);

    private List<M1Slice6CampaignLedgerEntry> ReadAndValidate()
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0 || lines.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("The campaign ledger is empty or partially written.");
        }

        List<M1Slice6CampaignLedgerEntry> result = [];
        foreach (string line in lines)
        {
            M1Slice6CampaignLedgerEntry entry = JsonSerializer.Deserialize<M1Slice6CampaignLedgerEntry>(line, JsonOptions)
                ?? throw new InvalidDataException("The campaign ledger entry is absent.");
            string previous = result.Count == 0 ? new string('0', 64) : result[^1].EventHash;
            string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Material(entry.Sequence,
                entry.Identity, entry.State, entry.Stage, entry.Event, entry.RequestManifestId,
                entry.RequestManifestSha256, entry.EvidenceId, entry.EvidenceSha256, entry.ProviderCallCount,
                entry.DnsResolutionCount, entry.AggregateRequestBytes, entry.AggregateInputTokens,
                entry.AggregateOutputTokens, entry.AggregateRawResponseBytes, entry.ReservedNanoUsd,
                entry.SettledNanoUsd, entry.ObservedInputTokens, entry.ObservedOutputTokens,
                entry.ObservedRawResponseBytes, entry.StageDeadlineUtc, entry.PossibleStartLatched,
                entry.SafetyIdentifierProjection, entry.NativeEnvelope, previous, entry.RecordedAtUtc))));
            if (entry.Sequence != result.Count + 1 || entry.Identity != identity || entry.PreviousHash != previous
                || entry.EventHash != expected || entry.ProviderCallCount is < 0 or > AggregateMaximumProviderCalls
                || entry.DnsResolutionCount is < 0 or > AggregateMaximumDnsResolutions
                || entry.AggregateRequestBytes is < 0 or > AggregateMaximumRequestBytes
                || entry.AggregateInputTokens is < 0 or > AggregateMaximumInputTokens
                || entry.AggregateOutputTokens is < 0 or > AggregateMaximumOutputTokens
                || entry.AggregateRawResponseBytes is < 0 or > AggregateMaximumRawResponseBytes
                || entry.ReservedNanoUsd < 0 || entry.SettledNanoUsd < 0
                || checked(entry.ReservedNanoUsd + entry.SettledNanoUsd) > AggregateMaximumNanoUsd
                || entry.ObservedInputTokens is < 0 or > AggregateMaximumInputTokens
                || entry.NativeEnvelope.CredWriteW is < 0 or > 1
                || entry.NativeEnvelope.CredReadW is < 0 or > 5
                || entry.NativeEnvelope.CredDeleteW != 0
                || entry.NativeEnvelope.CredFree is < 0 or > 4
                || entry.NativeEnvelope.Total != entry.NativeEnvelope.CredWriteW
                    + entry.NativeEnvelope.CredReadW + entry.NativeEnvelope.CredDeleteW
                    + entry.NativeEnvelope.CredFree
                || entry.ObservedOutputTokens is < 0 or > AggregateMaximumOutputTokens
                || entry.ObservedRawResponseBytes is < 0 or > AggregateMaximumRawResponseBytes
                || !IsOptionalIdentity(entry.RequestManifestId) || !IsOptionalHex(entry.RequestManifestSha256)
                || !IsOptionalIdentity(entry.EvidenceId) || !IsOptionalHex(entry.EvidenceSha256)
                || (entry.PossibleStartLatched && !ProductUserSafetyIdentifier.IsValidProjection(entry.SafetyIdentifierProjection))
                || (result.Count > 0 && entry.RecordedAtUtc < result[^1].RecordedAtUtc)
                || !IsLegalSuccessor(result.LastOrDefault(), entry))
            {
                throw new InvalidDataException("The campaign ledger hash chain, identity, state, clock, or finite vector is invalid.");
            }

            result.Add(entry);
        }
        return result;
    }

    private static bool IsLegalSuccessor(M1Slice6CampaignLedgerEntry? previous, M1Slice6CampaignLedgerEntry current)
    {
        if (previous is null)
        {
            return current.State == M1Slice6CampaignState.Ready && current.Stage == M1Slice6CampaignStage.None
                && current.Event == "campaign-ready" && current.RequestManifestId.Length == 0
                && current.RequestManifestSha256.Length == 0 && current.EvidenceId.Length == 0
                && current.EvidenceSha256.Length == 0 && current.ProviderCallCount == 0
                && current.DnsResolutionCount == 0 && current.AggregateRequestBytes == 0
                && current.AggregateInputTokens == 0 && current.AggregateOutputTokens == 0
                && current.AggregateRawResponseBytes == 0 && current.ReservedNanoUsd == 0
                && current.SettledNanoUsd == 0 && current.ObservedInputTokens == 0
                && current.ObservedOutputTokens == 0 && current.ObservedRawResponseBytes == 0
                && current.StageDeadlineUtc is null && !current.PossibleStartLatched
                && current.SafetyIdentifierProjection.Length == 0
                && current.NativeEnvelope == new M1Slice6CampaignNativeEnvelope(0, 0, 0, 0, 0);
        }
        bool sameVector = SameVector(previous, current);
        if ((previous.State, current.State, current.Event) is
            (M1Slice6CampaignState.Ready, M1Slice6CampaignState.Reviewed, "independent-review-accepted") or
            (M1Slice6CampaignState.Reviewed, M1Slice6CampaignState.Admitted, "exact-campaign-admitted") or
            (M1Slice6CampaignState.Admitted, M1Slice6CampaignState.CredentialExecutionHandoff, "credential-execution-handoff"))
        {
            return sameVector && current.Stage == M1Slice6CampaignStage.None
                && current.RequestManifestId.Length == 0 && current.RequestManifestSha256.Length == 0
                && current.EvidenceId.Length == 0 && current.EvidenceSha256.Length == 0
                && current.StageDeadlineUtc is null;
        }
        if (previous.State == M1Slice6CampaignState.CredentialExecutionHandoff
            && current.State == M1Slice6CampaignState.CredentialEvidenceHandoff
            && current.Event == "credential-evidence-handoff")
        {
            return SameVector(previous, current, ignoreEvidence: true, ignoreNative: true)
                && current.NativeEnvelope == new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4)
                && current.Stage == M1Slice6CampaignStage.None
                && current.RequestManifestId.Length == 0 && current.RequestManifestSha256.Length == 0
                && current.EvidenceId.Length > 0 && current.EvidenceSha256.Length == 64;
        }
        if (previous.State == M1Slice6CampaignState.CredentialEvidenceHandoff
            && current.State == M1Slice6CampaignState.CredentialEvidenceAccepted
            && current.Event == "credential-evidence-independently-accepted")
        {
            return sameVector && current.Stage == M1Slice6CampaignStage.None;
        }
        if (current.State == M1Slice6CampaignState.StageReserved && current.Event == "stage-reserved"
            && previous.State is M1Slice6CampaignState.CredentialEvidenceAccepted or M1Slice6CampaignState.StageAccepted)
        {
            M1Slice6CampaignStage expected = previous.State == M1Slice6CampaignState.CredentialEvidenceAccepted
                ? M1Slice6CampaignStage.Qualification
                : previous.Stage == M1Slice6CampaignStage.Qualification
                    ? M1Slice6CampaignStage.SourceClaimExtraction : M1Slice6CampaignStage.CandidateInvestigation;
            M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(current.Stage);
            return current.Stage == expected && current.RequestManifestId.Length > 0
                && current.RequestManifestSha256.Length == 64 && current.EvidenceId.Length == 0
                && current.EvidenceSha256.Length == 0 && current.ProviderCallCount == previous.ProviderCallCount
                && current.DnsResolutionCount == previous.DnsResolutionCount
                && current.AggregateRequestBytes > previous.AggregateRequestBytes
                && current.AggregateRequestBytes - previous.AggregateRequestBytes <= limits.MaximumRequestBytes
                && current.AggregateInputTokens >= previous.AggregateInputTokens
                && current.AggregateInputTokens - previous.AggregateInputTokens <= limits.MaximumInputTokens
                && current.AggregateOutputTokens > previous.AggregateOutputTokens
                && current.AggregateOutputTokens - previous.AggregateOutputTokens <= limits.MaximumOutputTokens
                && current.AggregateRawResponseBytes > previous.AggregateRawResponseBytes
                && current.AggregateRawResponseBytes - previous.AggregateRawResponseBytes <= limits.MaximumRawResponseBytes
                && current.ReservedNanoUsd is > 0 && current.ReservedNanoUsd <= limits.MaximumNanoUsd
                && current.SettledNanoUsd == previous.SettledNanoUsd
                && SameObserved(previous, current) && current.StageDeadlineUtc == current.RecordedAtUtc.AddMilliseconds(limits.DeadlineMilliseconds)
                && current.PossibleStartLatched == previous.PossibleStartLatched
                && current.SafetyIdentifierProjection == previous.SafetyIdentifierProjection;
        }
        if (previous.State == M1Slice6CampaignState.StageReserved
            && current.State == M1Slice6CampaignState.TransportMayHaveStarted
            && current.Event == "transport-may-have-started")
        {
            return SameReservationVector(previous, current, ignoreCounters: true, ignoreLatchAndSafety: true)
                && current.ProviderCallCount == previous.ProviderCallCount + 1
                && current.DnsResolutionCount == previous.DnsResolutionCount + 1 && current.PossibleStartLatched
                && current.SettledNanoUsd == previous.SettledNanoUsd && SameObserved(previous, current)
                && ProductUserSafetyIdentifier.IsValidProjection(current.SafetyIdentifierProjection)
                && (previous.SafetyIdentifierProjection.Length == 0
                    || previous.SafetyIdentifierProjection == current.SafetyIdentifierProjection);
        }
        if (previous.State == M1Slice6CampaignState.TransportMayHaveStarted
            && current.State == M1Slice6CampaignState.StageSettled
            && current.Event == "stage-known-settled-no-retry")
        {
            M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(current.Stage);
            M1Slice6CampaignNativeEnvelope expectedNative = new(previous.NativeEnvelope.CredWriteW,
                previous.NativeEnvelope.CredReadW + 1, previous.NativeEnvelope.CredDeleteW,
                previous.NativeEnvelope.CredFree + 1, previous.NativeEnvelope.Total + 2);
            return previous.Stage == current.Stage && previous.RequestManifestId == current.RequestManifestId
                && previous.RequestManifestSha256 == current.RequestManifestSha256
                && current.EvidenceId.Length == 0 && current.EvidenceSha256.Length == 0
                && previous.ProviderCallCount == current.ProviderCallCount
                && previous.DnsResolutionCount == current.DnsResolutionCount
                && SameAggregate(previous, current) && current.ReservedNanoUsd == 0
                && current.NativeEnvelope == expectedNative
                && current.SettledNanoUsd >= previous.SettledNanoUsd
                && current.SettledNanoUsd - previous.SettledNanoUsd <= previous.ReservedNanoUsd
                && current.ObservedInputTokens >= previous.ObservedInputTokens
                && current.ObservedInputTokens - previous.ObservedInputTokens <= limits.MaximumInputTokens
                && current.ObservedOutputTokens >= previous.ObservedOutputTokens
                && current.ObservedOutputTokens - previous.ObservedOutputTokens <= limits.MaximumOutputTokens
                && current.ObservedRawResponseBytes >= previous.ObservedRawResponseBytes
                && current.ObservedRawResponseBytes - previous.ObservedRawResponseBytes <= limits.MaximumRawResponseBytes;
        }
        if (previous.State == M1Slice6CampaignState.StageSettled
            && current.State == M1Slice6CampaignState.StageEvidenceHandoff
            && current.Event == "stage-evidence-handoff")
        {
            return SameReservationVector(previous, current, ignoreEvidence: true)
                && current.EvidenceId.Length > 0 && current.EvidenceSha256.Length == 64
                && current.ReservedNanoUsd == 0 && current.SettledNanoUsd == previous.SettledNanoUsd
                && SameObserved(previous, current);
        }
        if (previous.State == M1Slice6CampaignState.StageEvidenceHandoff
            && current.State == M1Slice6CampaignState.StageAccepted
            && current.Event == "stage-evidence-independently-accepted")
        {
            return SameReservationVector(previous, current, ignoreReserved: true) && current.ReservedNanoUsd == 0
                && current.SettledNanoUsd == previous.SettledNanoUsd && SameObserved(previous, current);
        }
        if (previous.State == M1Slice6CampaignState.StageAccepted
            && previous.Stage == M1Slice6CampaignStage.CandidateInvestigation
            && current.State == M1Slice6CampaignState.Completed
            && current.Event == "composed-evidence-independently-accepted")
        {
            return current.Stage == M1Slice6CampaignStage.None && current.RequestManifestId.Length == 0
                && current.RequestManifestSha256.Length == 0 && current.EvidenceId.Length > 0
                && current.EvidenceSha256.Length == 64 && current.ProviderCallCount == previous.ProviderCallCount
                && current.DnsResolutionCount == previous.DnsResolutionCount
                && SameAggregate(previous, current) && SameObserved(previous, current)
                && current.ReservedNanoUsd == 0 && current.SettledNanoUsd == previous.SettledNanoUsd
                && current.StageDeadlineUtc is null && current.PossibleStartLatched
                && current.SafetyIdentifierProjection == previous.SafetyIdentifierProjection
                && current.ProviderCallCount == 3;
        }
        if (current.State == M1Slice6CampaignState.Stopped)
        {
            bool credentialTerminal = previous.State == M1Slice6CampaignState.CredentialExecutionHandoff
                && current.Event is ("credential-owner-cancelled-terminal-stop" or "credential-preflight-collision-terminal-stop"
                    or "credential-readiness-failure-terminal-stop" or "credential-native-failure-terminal-stop"
                    or "credential-cleanup-ambiguity-terminal-stop" or "credential-helper-evidence-ambiguity-terminal-stop")
                && SameVector(previous, current, ignoreEvidence: true) && current.EvidenceId.Length > 0
                && current.EvidenceSha256.Length == 64;
            bool ambiguousTransport = previous.State == M1Slice6CampaignState.TransportMayHaveStarted
                && current.Event is ("ambiguous-start-hold-retained-no-retry" or "deadline-overrun-hold-retained-no-retry"
                    or "settlement-overrun-hold-retained-no-retry" or "stage-processing-failure-hold-retained-no-retry")
                && sameVector;
            bool knownSettled = previous.State == M1Slice6CampaignState.StageSettled
                && current.Event is ("evidence-write-failure-known-settled-no-retry"
                    or "evidence-serialization-failure-known-settled-no-retry"
                    or "semantic-admission-failure-known-settled-no-retry"
                    or "reconciled-sqlite-settlement-known-settled-no-retry")
                && sameVector && current.ReservedNanoUsd == 0;
            bool preEffectTerminal = current.Event is ("campaign-expired-before-admission-terminal-stop"
                or "credential-expired-before-handoff-terminal-stop"
                or "campaign-expired-before-stage-reservation-terminal-stop") && sameVector;
            bool releasedUndispatched = current.Event is ("campaign-expired-before-possible-start-released-undispatched-terminal-stop"
                or "safety-state-missing-released-undispatched-terminal-stop"
                or "safety-state-corrupt-released-undispatched-terminal-stop"
                or "safety-projection-drift-released-undispatched-terminal-stop"
                or "stage-prestart-failure-released-undispatched-terminal-stop")
                && previous.State == M1Slice6CampaignState.StageReserved
                && SameReservationVector(previous, current, ignoreReserved: true)
                && current.ReservedNanoUsd == 0;
            return credentialTerminal || ambiguousTransport || knownSettled || preEffectTerminal
                || releasedUndispatched;
        }
        return false;
    }

    private static bool SameVector(M1Slice6CampaignLedgerEntry previous, M1Slice6CampaignLedgerEntry current,
        bool ignoreEvidence = false, bool ignoreNative = false) => previous.RequestManifestId == current.RequestManifestId
        && previous.RequestManifestSha256 == current.RequestManifestSha256
        && (ignoreEvidence || previous.EvidenceId == current.EvidenceId && previous.EvidenceSha256 == current.EvidenceSha256)
        && previous.ProviderCallCount == current.ProviderCallCount && previous.DnsResolutionCount == current.DnsResolutionCount
        && SameAggregate(previous, current) && previous.ReservedNanoUsd == current.ReservedNanoUsd
        && previous.SettledNanoUsd == current.SettledNanoUsd && SameObserved(previous, current)
        && previous.StageDeadlineUtc == current.StageDeadlineUtc
        && previous.PossibleStartLatched == current.PossibleStartLatched
        && previous.SafetyIdentifierProjection == current.SafetyIdentifierProjection
        && (ignoreNative || previous.NativeEnvelope == current.NativeEnvelope);

    private static bool SameAggregate(M1Slice6CampaignLedgerEntry previous, M1Slice6CampaignLedgerEntry current) =>
        previous.AggregateRequestBytes == current.AggregateRequestBytes
        && previous.AggregateInputTokens == current.AggregateInputTokens
        && previous.AggregateOutputTokens == current.AggregateOutputTokens
        && previous.AggregateRawResponseBytes == current.AggregateRawResponseBytes;

    private static bool SameObserved(M1Slice6CampaignLedgerEntry previous, M1Slice6CampaignLedgerEntry current) =>
        previous.ObservedInputTokens == current.ObservedInputTokens
        && previous.ObservedOutputTokens == current.ObservedOutputTokens
        && previous.ObservedRawResponseBytes == current.ObservedRawResponseBytes;

    private static bool SameReservationVector(M1Slice6CampaignLedgerEntry previous, M1Slice6CampaignLedgerEntry current,
        bool ignoreEvidence = false, bool ignoreCounters = false, bool ignoreReserved = false,
        bool ignoreLatchAndSafety = false, bool ignoreNative = false) => previous.Stage == current.Stage
        && previous.RequestManifestId == current.RequestManifestId
        && previous.RequestManifestSha256 == current.RequestManifestSha256
        && (ignoreEvidence || previous.EvidenceId == current.EvidenceId && previous.EvidenceSha256 == current.EvidenceSha256)
        && (ignoreCounters || previous.ProviderCallCount == current.ProviderCallCount
            && previous.DnsResolutionCount == current.DnsResolutionCount)
        && SameAggregate(previous, current) && (ignoreReserved || previous.ReservedNanoUsd == current.ReservedNanoUsd)
        && previous.StageDeadlineUtc == current.StageDeadlineUtc
        && (ignoreLatchAndSafety || previous.PossibleStartLatched == current.PossibleStartLatched
            && previous.SafetyIdentifierProjection == current.SafetyIdentifierProjection)
        && (ignoreNative || previous.NativeEnvelope == current.NativeEnvelope);

    private static string Material(long sequence, M1Slice6CampaignIdentity id, M1Slice6CampaignState state,
        M1Slice6CampaignStage stage, string eventName, string requestId, string requestSha, string evidenceId,
        string evidenceSha, long calls, long dns, long requestBytes, long inputTokens, long outputTokens,
        long rawBytes, long reserved, long settled, long observedInput, long observedOutput, long observedRaw,
        DateTimeOffset? deadline, bool latched, string safety, M1Slice6CampaignNativeEnvelope native,
        string previous, DateTimeOffset recorded) => string.Join('|', sequence, JsonSerializer.Serialize(id, JsonOptions),
            state, stage, eventName, requestId, requestSha, evidenceId, evidenceSha, calls, dns, requestBytes,
            inputTokens, outputTokens, rawBytes, reserved, settled, observedInput, observedOutput, observedRaw,
            deadline?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            latched, safety, native.CredWriteW, native.CredReadW, native.CredDeleteW, native.CredFree,
            native.Total, previous, recorded.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    private static bool IsOptionalIdentity(string value) => value.Length == 0 || value.Length <= 200
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '/' or ':' or '_');
    private static bool IsOptionalHex(string value) => value.Length == 0 || value.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static M1Slice6CampaignNativeEnvelope ValidateNativeEnvelope(
        M1Slice6CampaignNativeEnvelope value, bool credential)
    {
        ArgumentNullException.ThrowIfNull(value);
        M1Slice6CampaignNativeEnvelope expected = credential
            ? new(1, 2, 0, 1, 4)
            : new(0, 1, 0, 1, 2);
        if (value != expected)
        {
            throw new InvalidDataException(
                "The retained native operation trace does not match its exact campaign phase.");
        }
        return value;
    }

    private static M1Slice6CampaignIdentity ValidateIdentity(M1Slice6CampaignIdentity value) => new(
        RequireIdentity(value.CampaignId), RequireHex(value.CampaignManifestSha256, 64),
        RequireHex(value.AuthorityAttachmentSha256, 64), RequireHex(value.VerificationCandidateCommit, 40),
        RequireIdentity(value.CredentialManifestId), RequireHex(value.CredentialManifestSha256, 64),
        RequireIdentity(value.CredentialProfileId), RequireIdentity(value.CredentialGenerationId),
        RequireHex(value.CredentialTargetFingerprintSha256, 64));

    private static string RequireIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 200 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '/' or ':' or '_')))
        {
            throw new ArgumentException("Campaign identity is outside the closed grammar.");
        }

        return value;
    }
    private static string RequireHex(string value, int length)
    {
        if (value.Length != length || value.Any(c => !char.IsAsciiHexDigit(c) || char.IsAsciiLetterUpper(c)))
        {
            throw new ArgumentException("Campaign identity hash is not exact lowercase hex.");
        }

        return value;
    }
    private void RequireMonotonicClock(DateTimeOffset now)
    {
        DateTimeOffset utc = RequireUtc(now, nameof(now));
        if (entries.Count > 0 && utc < entries[^1].RecordedAtUtc)
        {
            throw new InvalidOperationException("Campaign clock rollback is prohibited.");
        }
    }
    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Campaign timestamps must be UTC.", name);
        }

        return value;
    }
    private static void RequireBefore(DateTimeOffset now, DateTimeOffset expiry, string action)
    {
        if (RequireUtc(now, nameof(now)) >= expiry)
        {
            throw new InvalidOperationException($"{action} must begin before exact expiry.");
        }
    }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };
}

/// <summary>
/// Production admission seam that binds the durable safety projection to the exact
/// ledger possible-start latch. Callers may serialize with a pre-created projection,
/// but no transport may start until this seam succeeds.
/// </summary>
public sealed class M1Slice6CampaignDispatchAdmission
{
    private readonly M1Slice6FiniteCampaignLedger ledger;
    private readonly ProductUserSafetyIdentifierStateStore safetyState;

    public M1Slice6CampaignDispatchAdmission(
        M1Slice6FiniteCampaignLedger ledger,
        ProductUserSafetyIdentifierStateStore safetyState)
    {
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.safetyState = safetyState ?? throw new ArgumentNullException(nameof(safetyState));
    }

    public string ReserveAndLatchPossibleStart(M1Slice6CampaignStage stage,
        M1Slice6CampaignStageReservation reservation, string requestSafetyProjection,
        DateTimeOffset reservationTimeUtc, DateTimeOffset possibleStartTimeUtc)
    {
        if (!ProductUserSafetyIdentifier.IsValidProjection(requestSafetyProjection))
        {
            throw new InvalidOperationException("The request safety projection is invalid.");
        }
        ledger.ReserveStage(stage, reservation, reservationTimeUtc);
        string durableProjection;
        try
        {
            durableProjection = ledger.Current.SafetyIdentifierProjection.Length == 0
                ? safetyState.LatchPossibleStart()
                : safetyState.GetRequiredProjection(ledger.Current.SafetyIdentifierProjection);
        }
        catch (InvalidDataException exception)
        {
            ledger.StopBeforePossibleStart(stage,
                exception.Message.Contains("absent", StringComparison.OrdinalIgnoreCase)
                    ? "safety-state-missing" : "safety-state-corrupt",
                possibleStartTimeUtc);
            throw;
        }
        if (!string.Equals(durableProjection, requestSafetyProjection, StringComparison.Ordinal))
        {
            ledger.StopBeforePossibleStart(stage, "safety-projection-drift", possibleStartTimeUtc);
            throw new InvalidOperationException("The serialized request safety projection differs from durable product-user state.");
        }
        ledger.LatchPossibleStart(stage, durableProjection, possibleStartTimeUtc);
        return durableProjection;
    }
}
