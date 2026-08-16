using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinium.Persistence;

public enum M1Slice6CampaignState
{
    Ready,
    Reviewed,
    Admitted,
    CredentialExecutionHandoff,
    CredentialEvidenceAccepted,
    StageReserved,
    TransportMayHaveStarted,
    StageAccepted,
    Completed,
    Stopped,
}

public enum M1Slice6CampaignStage
{
    None,
    Qualification,
    SourceClaimExtraction,
    CandidateInvestigation,
}

public sealed record M1Slice6CampaignStageLimits(
    long MaximumRequestBytes,
    long MaximumInputTokens,
    long MaximumOutputTokens,
    long MaximumRawResponseBytes,
    long MaximumNanoUsd,
    long DeadlineMilliseconds)
{
    public static M1Slice6CampaignStageLimits For(M1Slice6CampaignStage stage) => stage switch
    {
        M1Slice6CampaignStage.Qualification => new(16_384, 20_480, 256, 262_144, 140_000_000, 60_000),
        M1Slice6CampaignStage.SourceClaimExtraction or M1Slice6CampaignStage.CandidateInvestigation =>
            new(65_536, 73_728, 4_096, 1_048_576, 600_000_000, 120_000),
        _ => throw new InvalidOperationException("The campaign stage is not dispatchable."),
    };
}

public sealed record M1Slice6CampaignLedgerEntry(
    long Sequence,
    string CampaignId,
    M1Slice6CampaignState State,
    M1Slice6CampaignStage Stage,
    string Event,
    long ProviderCallCount,
    long DnsResolutionCount,
    long ReservedNanoUsd,
    long SettledNanoUsd,
    bool PossibleStartLatched,
    string PreviousHash,
    string EventHash,
    DateTimeOffset RecordedAtUtc);

/// <summary>Coordinator-owned, append-only campaign authority and effect ledger.</summary>
public sealed class M1Slice6FiniteCampaignLedger
{
    public const long AggregateMaximumNanoUsd = 1_340_000_000;
    public const int AggregateMaximumProviderCalls = 3;
    public const int AggregateMaximumDnsResolutions = 3;
    private readonly string path;
    private readonly string campaignId;
    private readonly DateTimeOffset campaignExpiresAtUtc;
    private readonly DateTimeOffset credentialExpiresAtUtc;
    private readonly object gate = new();
    private readonly List<M1Slice6CampaignLedgerEntry> entries;

    public M1Slice6FiniteCampaignLedger(
        string path,
        string campaignId,
        DateTimeOffset campaignExpiresAtUtc,
        DateTimeOffset credentialExpiresAtUtc,
        DateTimeOffset now)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.campaignId = RequireIdentity(campaignId);
        this.campaignExpiresAtUtc = campaignExpiresAtUtc;
        this.credentialExpiresAtUtc = credentialExpiresAtUtc;
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        if (File.Exists(this.path))
        {
            entries = ReadAndValidate();
            if (entries[0].CampaignId != campaignId)
            {
                throw new InvalidDataException("The durable campaign ledger belongs to another campaign.");
            }
        }
        else
        {
            entries = [];
            Append(M1Slice6CampaignState.Ready, M1Slice6CampaignStage.None, "campaign-ready", 0, 0, 0, 0, false, now);
        }
    }

    public IReadOnlyList<M1Slice6CampaignLedgerEntry> Entries => entries.AsReadOnly();
    public M1Slice6CampaignLedgerEntry Current => entries[^1];

    public void RecordIndependentReview(DateTimeOffset now) =>
        Transition(M1Slice6CampaignState.Ready, M1Slice6CampaignState.Reviewed, M1Slice6CampaignStage.None,
            "independent-review-accepted", now);

    public void AdmitCampaign(DateTimeOffset now)
    {
        RequireBefore(now, campaignExpiresAtUtc, "Campaign admission");
        Transition(M1Slice6CampaignState.Reviewed, M1Slice6CampaignState.Admitted, M1Slice6CampaignStage.None,
            "exact-campaign-admitted", now);
    }

    public void BeginCredentialExecutionHandoff(DateTimeOffset now)
    {
        RequireBefore(now, credentialExpiresAtUtc, "Credential execution handoff");
        Transition(M1Slice6CampaignState.Admitted, M1Slice6CampaignState.CredentialExecutionHandoff,
            M1Slice6CampaignStage.None, "credential-execution-handoff", now);
    }

    public void AcceptCredentialEvidence(DateTimeOffset now) =>
        Transition(M1Slice6CampaignState.CredentialExecutionHandoff,
            M1Slice6CampaignState.CredentialEvidenceAccepted, M1Slice6CampaignStage.None,
            "credential-evidence-independently-accepted", now);

    public void ReserveStage(M1Slice6CampaignStage stage, long reservedNanoUsd, DateTimeOffset now)
    {
        RequireBefore(now, campaignExpiresAtUtc, "Stage reservation");
        M1Slice6CampaignStage expected = NextStage(Current);
        if (stage != expected || reservedNanoUsd <= 0 || reservedNanoUsd > M1Slice6CampaignStageLimits.For(stage).MaximumNanoUsd
            || Current.ReservedNanoUsd != 0 || checked(Current.SettledNanoUsd + reservedNanoUsd) > AggregateMaximumNanoUsd)
        {
            throw new InvalidOperationException("The stage reservation is outside the exact sequential campaign envelope.");
        }
        Append(M1Slice6CampaignState.StageReserved, stage, "stage-reserved", Current.ProviderCallCount,
            Current.DnsResolutionCount, reservedNanoUsd, Current.SettledNanoUsd, false, now);
    }

    public void LatchPossibleStart(M1Slice6CampaignStage stage, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.StageReserved || Current.Stage != stage
            || Current.ProviderCallCount >= AggregateMaximumProviderCalls
            || Current.DnsResolutionCount >= AggregateMaximumDnsResolutions)
        {
            throw new InvalidOperationException("A provider call cannot start outside its one-shot reserved stage.");
        }
        Append(M1Slice6CampaignState.TransportMayHaveStarted, stage, "transport-may-have-started",
            Current.ProviderCallCount + 1, Current.DnsResolutionCount + 1, Current.ReservedNanoUsd,
            Current.SettledNanoUsd, true, now);
    }

    public void AcceptStageEvidence(M1Slice6CampaignStage stage, long settledNanoUsd, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.TransportMayHaveStarted || Current.Stage != stage
            || !Current.PossibleStartLatched || settledNanoUsd < 0 || settledNanoUsd > Current.ReservedNanoUsd)
        {
            throw new InvalidOperationException("Stage evidence does not close the exact possible-start hold.");
        }
        long aggregate = checked(Current.SettledNanoUsd + settledNanoUsd);
        M1Slice6CampaignState state = stage == M1Slice6CampaignStage.CandidateInvestigation
            ? M1Slice6CampaignState.Completed : M1Slice6CampaignState.StageAccepted;
        Append(state, stage, "stage-evidence-independently-accepted", Current.ProviderCallCount,
            Current.DnsResolutionCount, 0, aggregate, true, now);
    }

    public void StopAfterAmbiguousStart(M1Slice6CampaignStage stage, DateTimeOffset now)
    {
        if (Current.State != M1Slice6CampaignState.TransportMayHaveStarted || Current.Stage != stage)
        {
            throw new InvalidOperationException("Only the current possible-start stage may stop ambiguously.");
        }
        Append(M1Slice6CampaignState.Stopped, stage, "ambiguous-start-hold-retained-no-retry",
            Current.ProviderCallCount, Current.DnsResolutionCount, Current.ReservedNanoUsd,
            Current.SettledNanoUsd, true, now);
    }

    private void Transition(M1Slice6CampaignState from, M1Slice6CampaignState to, M1Slice6CampaignStage stage,
        string eventName, DateTimeOffset now)
    {
        if (Current.State != from)
        {
            throw new InvalidOperationException($"Campaign transition {eventName} has a stale predecessor.");
        }
        Append(to, stage, eventName, Current.ProviderCallCount, Current.DnsResolutionCount,
            Current.ReservedNanoUsd, Current.SettledNanoUsd, Current.PossibleStartLatched, now);
    }

    private static M1Slice6CampaignStage NextStage(M1Slice6CampaignLedgerEntry current) => current.State switch
    {
        M1Slice6CampaignState.CredentialEvidenceAccepted => M1Slice6CampaignStage.Qualification,
        M1Slice6CampaignState.StageAccepted when current.Stage == M1Slice6CampaignStage.Qualification =>
            M1Slice6CampaignStage.SourceClaimExtraction,
        M1Slice6CampaignState.StageAccepted when current.Stage == M1Slice6CampaignStage.SourceClaimExtraction =>
            M1Slice6CampaignStage.CandidateInvestigation,
        _ => throw new InvalidOperationException("The campaign has no next provider stage."),
    };

    private void Append(M1Slice6CampaignState state, M1Slice6CampaignStage stage, string eventName,
        long calls, long dns, long reserved, long settled, bool latched, DateTimeOffset now)
    {
        lock (gate)
        {
            string previous = entries.Count == 0 ? new string('0', 64) : entries[^1].EventHash;
            long sequence = entries.Count + 1;
            string material = string.Join('|', sequence, campaignId, state, stage, eventName, calls, dns, reserved,
                settled, latched, previous, now.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
            M1Slice6CampaignLedgerEntry entry = new(sequence, campaignId, state, stage, eventName, calls, dns,
                reserved, settled, latched, previous, hash, now.ToUniversalTime());
            byte[] line = JsonSerializer.SerializeToUtf8Bytes(entry, JsonOptions);
            using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            stream.Write(line);
            stream.WriteByte((byte)'\n');
            stream.Flush(flushToDisk: true);
            entries.Add(entry);
        }
    }

    private List<M1Slice6CampaignLedgerEntry> ReadAndValidate()
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0 || lines.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("The durable campaign ledger is empty or partially written.");
        }
        List<M1Slice6CampaignLedgerEntry> result = [];
        foreach (string line in lines)
        {
            M1Slice6CampaignLedgerEntry entry = JsonSerializer.Deserialize<M1Slice6CampaignLedgerEntry>(line, JsonOptions)
                ?? throw new InvalidDataException("The durable campaign ledger entry is absent.");
            string previous = result.Count == 0 ? new string('0', 64) : result[^1].EventHash;
            string material = string.Join('|', entry.Sequence, entry.CampaignId, entry.State, entry.Stage, entry.Event,
                entry.ProviderCallCount, entry.DnsResolutionCount, entry.ReservedNanoUsd, entry.SettledNanoUsd,
                entry.PossibleStartLatched, previous,
                entry.RecordedAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
            if (entry.Sequence != result.Count + 1 || entry.PreviousHash != previous || entry.EventHash != expected
                || entry.ProviderCallCount is < 0 or > AggregateMaximumProviderCalls
                || entry.DnsResolutionCount is < 0 or > AggregateMaximumDnsResolutions
                || entry.ReservedNanoUsd < 0 || entry.SettledNanoUsd < 0
                || checked(entry.ReservedNanoUsd + entry.SettledNanoUsd) > AggregateMaximumNanoUsd)
            {
                throw new InvalidDataException("The durable campaign ledger hash chain or finite counter is invalid.");
            }
            result.Add(entry);
        }
        return result;
    }

    private static string RequireIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 160 || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '/' or ':')))
        {
            throw new ArgumentException("The campaign identity is outside the closed grammar.", nameof(value));
        }
        return value;
    }

    private static void RequireBefore(DateTimeOffset now, DateTimeOffset expiry, string action)
    {
        if (now >= expiry)
        {
            throw new InvalidOperationException($"{action} must begin before the exact expiry.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };
}
