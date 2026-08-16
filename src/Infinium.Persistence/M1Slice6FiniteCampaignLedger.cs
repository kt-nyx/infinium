using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Persistence;

public enum M1Slice6CampaignState
{
    Ready, Reviewed, Admitted, CredentialExecutionHandoff, CredentialEvidenceAccepted,
    StageReserved, TransportMayHaveStarted, StageAccepted, Completed, Stopped,
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

public sealed record M1Slice6CampaignLedgerEntry(
    long Sequence, M1Slice6CampaignIdentity Identity, M1Slice6CampaignState State,
    M1Slice6CampaignStage Stage, string Event, string RequestManifestId, string RequestManifestSha256,
    string EvidenceId, string EvidenceSha256, long ProviderCallCount, long DnsResolutionCount,
    long AggregateRequestBytes, long AggregateInputTokens, long AggregateOutputTokens,
    long AggregateRawResponseBytes, long ReservedNanoUsd, long SettledNanoUsd,
    DateTimeOffset? StageDeadlineUtc, bool PossibleStartLatched, string SafetyIdentifierProjection,
    string PreviousHash, string EventHash, DateTimeOffset RecordedAtUtc);

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
    private readonly M1Slice6CampaignIdentity identity;
    private readonly DateTimeOffset campaignExpiresAtUtc;
    private readonly DateTimeOffset credentialExpiresAtUtc;
    private readonly object gate = new();
    private readonly List<M1Slice6CampaignLedgerEntry> entries;

    public M1Slice6FiniteCampaignLedger(string path, M1Slice6CampaignIdentity identity,
        DateTimeOffset campaignExpiresAtUtc, DateTimeOffset credentialExpiresAtUtc, DateTimeOffset now)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.identity = ValidateIdentity(identity);
        this.campaignExpiresAtUtc = RequireUtc(campaignExpiresAtUtc, nameof(campaignExpiresAtUtc));
        this.credentialExpiresAtUtc = RequireUtc(credentialExpiresAtUtc, nameof(credentialExpiresAtUtc));
        if (credentialExpiresAtUtc >= campaignExpiresAtUtc)
        {
            throw new ArgumentException("Credential expiry must precede campaign expiry.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
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
            Append(M1Slice6CampaignState.Ready, M1Slice6CampaignStage.None, "campaign-ready", "", "", "", "",
                0, 0, 0, 0, 0, 0, 0, 0, null, false, "", now);
        }
    }

    public IReadOnlyList<M1Slice6CampaignLedgerEntry> Entries => entries.AsReadOnly();
    public M1Slice6CampaignLedgerEntry Current => entries[^1];

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

    public void AcceptCredentialEvidence(string evidenceId, string evidenceSha256, DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        if (now >= campaignExpiresAtUtc)
        {
            StopBeforeEffect("campaign-expired-before-credential-evidence-handoff", now);
            throw new InvalidOperationException("Credential evidence handoff expired and is terminally stopped.");
        }
        if (Current.State != M1Slice6CampaignState.CredentialExecutionHandoff)
        {
            throw new InvalidOperationException("Credential evidence has a stale predecessor.");
        }

        Append(M1Slice6CampaignState.CredentialEvidenceAccepted, M1Slice6CampaignStage.None,
            "credential-evidence-independently-accepted", "", "", RequireIdentity(evidenceId), RequireHex(evidenceSha256, 64),
            Current.ProviderCallCount, Current.DnsResolutionCount, Current.AggregateRequestBytes,
            Current.AggregateInputTokens, Current.AggregateOutputTokens, Current.AggregateRawResponseBytes,
            Current.ReservedNanoUsd, Current.SettledNanoUsd, null, Current.PossibleStartLatched,
            Current.SafetyIdentifierProjection, now);
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
            Current.SettledNanoUsd, deadline, Current.PossibleStartLatched, Current.SafetyIdentifierProjection, now);
    }

    public void LatchPossibleStart(M1Slice6CampaignStage stage, string safetyIdentifierProjection, DateTimeOffset now)
    {
        RequireMonotonicClock(now);
        if (now >= campaignExpiresAtUtc)
        {
            StopBeforeEffect("campaign-expired-before-possible-start", now);
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
            Current.SettledNanoUsd, Current.StageDeadlineUtc, true, safetyIdentifierProjection, now);
    }

    public void AcceptStageEvidence(M1Slice6CampaignStage stage, string evidenceId, string evidenceSha256,
        long settledNanoUsd, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.TransportMayHaveStarted || Current.Stage != stage
            || !Current.PossibleStartLatched || Current.StageDeadlineUtc is null)
        {
            throw new InvalidOperationException("Stage evidence has a stale possible-start predecessor.");
        }
        if (now >= Current.StageDeadlineUtc)
        {
            StopAfterAmbiguousStart(stage, "deadline-overrun", now);
            throw new InvalidOperationException("Stage evidence exceeded its immutable deadline; the full hold remains.");
        }
        if (settledNanoUsd < 0 || settledNanoUsd > Current.ReservedNanoUsd)
        {
            StopAfterAmbiguousStart(stage, "settlement-overrun", now);
            throw new InvalidOperationException("Stage settlement exceeded its reservation; the full hold remains.");
        }

        long aggregate = checked(Current.SettledNanoUsd + settledNanoUsd);
        M1Slice6CampaignState state = stage == M1Slice6CampaignStage.CandidateInvestigation
            ? M1Slice6CampaignState.Completed : M1Slice6CampaignState.StageAccepted;
        Append(state, stage, "stage-evidence-independently-accepted", Current.RequestManifestId,
            Current.RequestManifestSha256, RequireIdentity(evidenceId), RequireHex(evidenceSha256, 64),
            Current.ProviderCallCount, Current.DnsResolutionCount, Current.AggregateRequestBytes,
            Current.AggregateInputTokens, Current.AggregateOutputTokens, Current.AggregateRawResponseBytes,
            0, aggregate, Current.StageDeadlineUtc, true, Current.SafetyIdentifierProjection, now);
    }

    public void StopAfterAmbiguousStart(M1Slice6CampaignStage stage, string reason, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.TransportMayHaveStarted || Current.Stage != stage)
        {
            throw new InvalidOperationException("Only the current possible-start stage may stop ambiguously.");
        }

        string exactReason = reason switch { "ambiguous-start" => reason, "deadline-overrun" => reason, "settlement-overrun" => reason, _ => throw new ArgumentException("Unknown campaign stop reason.", nameof(reason)) };
        Append(M1Slice6CampaignState.Stopped, stage, exactReason + "-hold-retained-no-retry", Current.RequestManifestId,
            Current.RequestManifestSha256, "", "", Current.ProviderCallCount, Current.DnsResolutionCount,
            Current.AggregateRequestBytes, Current.AggregateInputTokens, Current.AggregateOutputTokens,
            Current.AggregateRawResponseBytes, Current.ReservedNanoUsd, Current.SettledNanoUsd,
            Current.StageDeadlineUtc, true, Current.SafetyIdentifierProjection, now);
    }

    public void StopBeforePossibleStart(M1Slice6CampaignStage stage, string reason, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.StageReserved || Current.Stage != stage
            || reason is not ("safety-state-missing" or "safety-state-corrupt" or "safety-projection-drift"))
        {
            throw new InvalidOperationException("Only an exact reserved stage may stop before possible start.");
        }
        StopBeforeEffect(reason, now);
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
            Current.SettledNanoUsd, null, Current.PossibleStartLatched, Current.SafetyIdentifierProjection, now);
    }

    private void StopBeforeEffect(string eventName, DateTimeOffset now)
    {
        if (Current.State == M1Slice6CampaignState.Stopped || Current.State == M1Slice6CampaignState.Completed)
        {
            throw new InvalidOperationException("The campaign is already terminal.");
        }
        Append(M1Slice6CampaignState.Stopped, Current.Stage, eventName + "-terminal-stop", Current.RequestManifestId,
            Current.RequestManifestSha256, "", "", Current.ProviderCallCount, Current.DnsResolutionCount,
            Current.AggregateRequestBytes, Current.AggregateInputTokens, Current.AggregateOutputTokens,
            Current.AggregateRawResponseBytes, Current.ReservedNanoUsd, Current.SettledNanoUsd,
            Current.StageDeadlineUtc, Current.PossibleStartLatched, Current.SafetyIdentifierProjection, now);
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
        DateTimeOffset? stageDeadline, bool latched, string safetyProjection, DateTimeOffset now)
    {
        lock (gate)
        {
            RequireMonotonicClock(now);
            string previous = entries.Count == 0 ? new string('0', 64) : entries[^1].EventHash;
            long sequence = entries.Count + 1;
            DateTimeOffset utc = RequireUtc(now, nameof(now));
            string material = Material(sequence, identity, state, stage, eventName, requestId, requestSha, evidenceId,
                evidenceSha, calls, dns, requestBytes, inputTokens, outputTokens, rawBytes, reserved, settled,
                stageDeadline, latched, safetyProjection, previous, utc);
            string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
            M1Slice6CampaignLedgerEntry entry = new(sequence, identity, state, stage, eventName, requestId,
                requestSha, evidenceId, evidenceSha, calls, dns, requestBytes, inputTokens, outputTokens, rawBytes,
                reserved, settled, stageDeadline, latched, safetyProjection, previous, hash, utc);
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
            using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            stream.Write(line); stream.WriteByte((byte)'\n'); stream.Flush(flushToDisk: true); entries.Add(entry);
        }
    }

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
                entry.SettledNanoUsd, entry.StageDeadlineUtc, entry.PossibleStartLatched,
                entry.SafetyIdentifierProjection, previous, entry.RecordedAtUtc))));
            if (entry.Sequence != result.Count + 1 || entry.Identity != identity || entry.PreviousHash != previous
                || entry.EventHash != expected || entry.ProviderCallCount is < 0 or > AggregateMaximumProviderCalls
                || entry.DnsResolutionCount is < 0 or > AggregateMaximumDnsResolutions
                || entry.AggregateRequestBytes is < 0 or > AggregateMaximumRequestBytes
                || entry.AggregateInputTokens is < 0 or > AggregateMaximumInputTokens
                || entry.AggregateOutputTokens is < 0 or > AggregateMaximumOutputTokens
                || entry.AggregateRawResponseBytes is < 0 or > AggregateMaximumRawResponseBytes
                || entry.ReservedNanoUsd < 0 || entry.SettledNanoUsd < 0
                || checked(entry.ReservedNanoUsd + entry.SettledNanoUsd) > AggregateMaximumNanoUsd
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
            return current.State == M1Slice6CampaignState.Ready && current.Event == "campaign-ready";
        }

        return (previous.State, current.State, current.Event) switch
        {
            (M1Slice6CampaignState.Ready, M1Slice6CampaignState.Reviewed, "independent-review-accepted") => true,
            (M1Slice6CampaignState.Reviewed, M1Slice6CampaignState.Admitted, "exact-campaign-admitted") => true,
            (M1Slice6CampaignState.Admitted, M1Slice6CampaignState.CredentialExecutionHandoff, "credential-execution-handoff") => true,
            (M1Slice6CampaignState.CredentialExecutionHandoff, M1Slice6CampaignState.CredentialEvidenceAccepted, "credential-evidence-independently-accepted") => true,
            (M1Slice6CampaignState.CredentialEvidenceAccepted or M1Slice6CampaignState.StageAccepted, M1Slice6CampaignState.StageReserved, "stage-reserved") => true,
            (M1Slice6CampaignState.StageReserved, M1Slice6CampaignState.TransportMayHaveStarted, "transport-may-have-started") => true,
            (M1Slice6CampaignState.TransportMayHaveStarted, M1Slice6CampaignState.StageAccepted or M1Slice6CampaignState.Completed, "stage-evidence-independently-accepted") => true,
            (M1Slice6CampaignState.TransportMayHaveStarted, M1Slice6CampaignState.Stopped, _) when current.Event.EndsWith("-hold-retained-no-retry", StringComparison.Ordinal) => true,
            (_, M1Slice6CampaignState.Stopped, _) when current.Event.EndsWith("-terminal-stop", StringComparison.Ordinal) => true,
            _ => false,
        };
    }

    private static string Material(long sequence, M1Slice6CampaignIdentity id, M1Slice6CampaignState state,
        M1Slice6CampaignStage stage, string eventName, string requestId, string requestSha, string evidenceId,
        string evidenceSha, long calls, long dns, long requestBytes, long inputTokens, long outputTokens,
        long rawBytes, long reserved, long settled, DateTimeOffset? deadline, bool latched, string safety,
        string previous, DateTimeOffset recorded) => string.Join('|', sequence, JsonSerializer.Serialize(id, JsonOptions),
            state, stage, eventName, requestId, requestSha, evidenceId, evidenceSha, calls, dns, requestBytes,
            inputTokens, outputTokens, rawBytes, reserved, settled,
            deadline?.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            latched, safety, previous, recorded.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

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
