using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Coordinator;

public sealed record M1Slice6CampaignStageAuthority(
    string ManifestId, string ManifestSha256, M1Slice6CampaignStage Stage,
    string WorkPackage, ProviderOperationKind Operation, string ReviewCandidateCommit,
    string PredecessorEvidenceId, string PredecessorEvidenceSha256,
    string CanonicalRequestPath, string CanonicalRequestSha256, byte[] CanonicalRequest,
    long ProvedInputTokens, M1Slice6CampaignStageLimits Limits, string SafetyIdentifierProjection,
    string ValidationPackageId, string ValidationPackagePath, string ValidationPackageSha256,
    string ValidationProductInputPath, long ValidationProductInputBytes,
    string ValidationProductInputSha256, string ValidationPredecessorManifestPath,
    long ValidationPredecessorManifestBytes, string ValidationPredecessorManifestSha256,
    string ValidationOraclePath, string ValidationOracleSha256,
    string DeterministicOracleResultSha256, bool SemanticUse);

public sealed record M1Slice6CampaignCredentialReadReceipt(
    string ProfileId, string GenerationId, string TargetFingerprintSha256,
    int CredReadW, int CredFree, int CredWriteW, int CredDeleteW,
    string ReadResult, string FreeResult);

public sealed record M1Slice6CampaignStageBoundaryResult(
    OpenAiResponsesResult Response, M1Slice6CampaignCredentialReadReceipt CredentialRead,
    string CanonicalRequestSha256, string SafetyIdentifierProjection, int DnsResolutionCount,
    byte[] ResponseHeadersBytes, byte[] NativeCallTraceBytes, byte[] CanaryEvidenceBytes,
    DateTimeOffset CompletedAtUtc);

public sealed record M1Slice6CampaignAccountingAdmission(
    string AuthorizationId, string OperationId, string AttemptId, string RequestId,
    string ReservationId, string DispatchFenceId, long CoordinatorFencingEpoch,
    DateTimeOffset EffectiveGateTimeUtc, DateTimeOffset DeadlineUtc,
    string AccountIdentityId, string BillingScopeIdentityId);

public sealed record M1Slice6CampaignAccountingSettlement(
    string ResponseId, string UsageEntryId, string SettlementId, string ReplayEdgeId,
    string RawResponseSha256, string ResponseHeadersSha256, long SettledNanoUsd,
    bool UnresolvedHold, bool RetryPermitted,
    string SemanticValidationId, string SemanticDisposition,
    int SemanticProposalCount, int SemanticAdmissionCount,
    string SemanticResultSha256, M1Slice6CampaignSemanticProvenance SemanticProvenance);

public sealed record M1Slice6CampaignRecoveredSettlement(
    long InputTokens, long OutputTokens, long RawResponseBytes, long SettledNanoUsd);

public sealed record M1Slice6CampaignSemanticProvenance(
    string SourceAcquisitionId, string SourceAdmissionId, string AdmittedArtifactId,
    string SourceApplicationLinkId, string EvidenceApplicationLinkId,
    string CandidateId, string HypothesisId)
{
    public static M1Slice6CampaignSemanticProvenance Empty { get; } = new("", "", "", "", "", "", "");
}

public sealed class M1Slice6CampaignKnownSettlementException(string message, Exception innerException)
    : IOException(message, innerException);

public interface IM1Slice6CampaignProviderAccounting
{
    public M1Slice6CampaignAccountingAdmission Prepare(M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignIdentity campaignIdentity, DateTimeOffset now);
    public void RecordPossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now);
    public void ReleaseBeforePossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now);
    public M1Slice6CampaignAccountingSettlement PersistSettleAndReplay(
        M1Slice6CampaignAccountingAdmission admission, M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignStageBoundaryResult result);
}

public interface IM1Slice6CampaignRecoveryAccounting
{
    public M1Slice6CampaignRecoveredSettlement? TryRecoverKnownSettlement(
        M1Slice6CampaignStage stage, string canonicalRequestSha256);
}

/// <summary>
/// Helper-owned execution boundary. Implementations keep credential bytes inside the helper and
/// must invoke <paramref name="possibleStart"/> exactly once immediately before the contained
/// descendant may start. This conservative latch precedes the helper-owned credential read/free;
/// the returned receipt must subsequently prove the exact R/F trace and single transport send.
/// </summary>
public interface IM1Slice6CampaignStageExecutionBoundary
{
    public Task<M1Slice6CampaignStageBoundaryResult> ExecuteOnceAsync(
        M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignAccountingAdmission accounting,
        Func<DateTimeOffset, CancellationToken, Task> possibleStart,
        CancellationToken cancellationToken);
}

/// <summary>
/// Production composition for the finite campaign. Credential bytes are read and consumed only by
/// the contained CredentialHelper process; this coordinator receives only the exact R/F trace,
/// staged provider envelope, and non-secret containment facts.
/// </summary>
public sealed class M1Slice6CampaignProductionStageBoundary : IM1Slice6CampaignStageExecutionBoundary
{
    private readonly Func<HelperPrivateFrameV2, HelperPrivateFrameV2, HelperPrivateFrameV2,
        TimeSpan, DateTimeOffset, CancellationToken, Task<HelperProcessReceipt>> executeHelper;
    private readonly string profileId;
    private readonly string generationId;
    private readonly string targetFingerprint;
    private readonly string accountIdentityId;
    private readonly string billingScopeIdentityId;
    private long sequence;

    public M1Slice6CampaignProductionStageBoundary(string helperBinary, string helperSha256,
        string credentialManifestPath, string credentialManifestSha256, string credentialManifestId)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != credentialManifestSha256)
        {
            throw new InvalidDataException("Campaign provider boundary has stale credential manifest bytes.");
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement profile = root.GetProperty("profile");
        JsonElement providerIntent = root.GetProperty("provider_intent");
        if (root.GetProperty("manifest_id").GetString() != credentialManifestId
            || root.GetProperty("status").GetString() != "ready-for-owner-acceptance")
        {
            throw new InvalidDataException("Campaign provider boundary requires the exact production credential identity.");
        }
        profileId = profile.GetProperty("access_profile_id").GetString()!;
        generationId = profile.GetProperty("generation_id").GetString()!;
        targetFingerprint = profile.GetProperty("target_fingerprint_sha256").GetString()!;
        accountIdentityId = providerIntent.GetProperty("account_identity_id").GetString()!;
        billingScopeIdentityId = providerIntent.GetProperty("billing_scope_identity_id").GetString()!;
        if (string.IsNullOrWhiteSpace(accountIdentityId) || string.IsNullOrWhiteSpace(billingScopeIdentityId)
            || accountIdentityId == "unavailable" || billingScopeIdentityId == "unavailable")
        {
            throw new InvalidDataException("Campaign provider boundary requires exact account and billing-scope identities.");
        }
        OneShotCredentialHelperLauncher launcher = OneShotCredentialHelperLauncher.CreateWp9CampaignProvider(
            helperBinary, helperSha256, credentialManifestPath, credentialManifestSha256, credentialManifestId);
        executeHelper = (bootstrap, assignment, final, timeout, now, cancellationToken) =>
            launcher.ExecuteAsync(bootstrap, assignment, final, timeout, now, cancellationToken);
    }

    internal M1Slice6CampaignProductionStageBoundary(string profileId, string generationId,
        string targetFingerprint,
        Func<HelperPrivateFrameV2, HelperPrivateFrameV2, HelperPrivateFrameV2,
            TimeSpan, DateTimeOffset, CancellationToken, Task<HelperProcessReceipt>> executeHelper,
        string accountIdentityId = "openai-account-owner-confirmed-at-enrollment",
        string billingScopeIdentityId = "openai-direct-usage-owner-confirmed-at-enrollment")
    {
        this.profileId = profileId;
        this.generationId = generationId;
        this.targetFingerprint = targetFingerprint;
        this.accountIdentityId = accountIdentityId;
        this.billingScopeIdentityId = billingScopeIdentityId;
        this.executeHelper = executeHelper ?? throw new ArgumentNullException(nameof(executeHelper));
    }

    public async Task<M1Slice6CampaignStageBoundaryResult> ExecuteOnceAsync(
        M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignAccountingAdmission accounting,
        Func<DateTimeOffset, CancellationToken, Task> possibleStart,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (accounting.AccountIdentityId != accountIdentityId
            || accounting.BillingScopeIdentityId != billingScopeIdentityId
            || accounting.DeadlineUtc <= now)
        {
            throw new InvalidDataException("Campaign helper assignment differs from the authoritative account, billing, or deadline gate.");
        }
        string suffix = Interlocked.Increment(ref sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string commandId = "m1-s6-campaign/" + (int)authority.Stage + "/" + suffix;
        string requestId = accounting.RequestId;
        string operationId = accounting.OperationId;
        string attemptId = accounting.AttemptId;
        string reservationId = accounting.ReservationId;
        string dispatchId = accounting.DispatchFenceId;
        ContentDigest canonical = Digest(authority.CanonicalRequest);
        byte[] settingsBytes = Encoding.UTF8.GetBytes("m1-s6-campaign-settings/" + (int)authority.Stage);
        byte[] schemaBytes;
        using (JsonDocument request = JsonDocument.Parse(authority.CanonicalRequest))
        {
            schemaBytes = JsonSerializer.SerializeToUtf8Bytes(request.RootElement.GetProperty("text")
                .GetProperty("format").GetProperty("schema"));
        }
        ContentDigest settings = Digest(settingsBytes);
        ContentDigest outputSchema = Digest(schemaBytes);
        Instant deadline = Instant(accounting.DeadlineUtc);
        HelperPrivateFrameV2 bootstrap = new()
        {
            Sequence = 1,
            ProtocolFingerprintSha256 = Fingerprint(),
            Bootstrap = new()
            {
                CoordinatorFencingEpoch = checked((ulong)accounting.CoordinatorFencingEpoch),
                ExpiresAt = deadline.Clone(),
                OneUseNonceFingerprintSha256 = ByteString.CopyFrom(RandomNumberGenerator.GetBytes(32)),
                CommandId = commandId,
                ProviderDispatch = new() { OperationId = new() { Value = operationId }, AttemptId = new() { Value = attemptId } },
            },
        };
        ProviderOperationKindV2 operation = authority.Stage switch
        {
            M1Slice6CampaignStage.Qualification => ProviderOperationKindV2.TransportQualification,
            M1Slice6CampaignStage.SourceClaimExtraction => ProviderOperationKindV2.SourceClaimExtraction,
            _ => ProviderOperationKindV2.CandidateInvestigation,
        };
        HelperLimitsV2 limits = new()
        {
            MaximumFrameBytes = HelperProtocolV2Constants.MaximumFrameBytes,
            MaximumRequestBytes = checked((ulong)authority.Limits.MaximumRequestBytes),
            MaximumResponseBytes = checked((ulong)authority.Limits.MaximumRawResponseBytes),
            MaximumStagedOutputBytes = checked((ulong)authority.Limits.MaximumRawResponseBytes),
            MaximumInputTokens = checked((ulong)authority.Limits.MaximumInputTokens),
            MaximumOutputTokens = checked((ulong)authority.Limits.MaximumOutputTokens),
            MaximumCalculatedNanoUsd = authority.Limits.MaximumNanoUsd,
            MaximumDuration = new() { Value = checked((ulong)authority.Limits.DeadlineMilliseconds) },
            MaximumDispatchCount = 1,
        };
        ProviderRequestV2 requestValue = new()
        {
            DispatchId = new() { Value = dispatchId },
            CanonicalRequestBytes = ByteString.CopyFrom(authority.CanonicalRequest),
            CanonicalRequest = canonical.Clone(),
            CapabilitySnapshotId = new() { Value = M1ProviderCatalog.Capability.Identity.Value },
            PriceSnapshotId = new() { Value = M1ProviderCatalog.Price.Identity.Value },
            ReservationGroupId = new() { Value = reservationId },
            DispatchDeadline = deadline.Clone(),
            EndpointIdentity = ProviderEndpointV2.OpenaiResponses,
            InputBoundProof = new()
            {
                PolicyId = OpenAiResponsesCanonicalSerializer.InputBoundPolicyId,
                PolicyVersion = OpenAiResponsesCanonicalSerializer.InputBoundPolicyVersion,
                Status = InputBoundProofStatusV2.Proved,
            },
            RequestId = requestId,
            ConfirmedAt = Instant(accounting.EffectiveGateTimeUtc),
            RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(authority.CanonicalRequest)),
        };
        HelperPrivateFrameV2 assignment = new()
        {
            Sequence = 2,
            ProtocolFingerprintSha256 = Fingerprint(),
            Assignment = new()
            {
                AssignmentId = commandId + "/assignment",
                CommandId = commandId,
                AssignmentKind = HelperAssignmentKindV2.ProviderDispatch,
                OperationKind = operation,
                AccessProfileId = new() { Value = profileId },
                GenerationId = new() { Value = generationId },
                GenerationOrdinal = 1,
                RevocationEpoch = 0,
                Credential = new() { AccessProfileId = new() { Value = profileId }, GenerationId = new() { Value = generationId } },
                ProviderDispatch = new() { OperationId = new() { Value = operationId }, AttemptId = new() { Value = attemptId } },
                ProviderRequest = requestValue,
                AccountIdentityId = new() { Value = accounting.AccountIdentityId },
                BillingScopeIdentityId = new() { Value = accounting.BillingScopeIdentityId },
                EffectiveConfigurationId = "m1-s6-campaign-exact-profile",
                Settings = settings,
                OutputSchema = outputSchema,
                Limits = limits,
            },
        };
        HelperPrivateFrameV2 final = new()
        {
            Sequence = 3,
            ProtocolFingerprintSha256 = Fingerprint(),
            DispatchRevalidation = new()
            {
                DispatchId = new() { Value = dispatchId },
                AttemptId = new() { Value = attemptId },
                CoordinatorFencingEpoch = checked((ulong)accounting.CoordinatorFencingEpoch),
                AccessProfileId = new() { Value = profileId },
                GenerationId = new() { Value = generationId },
                RevocationEpoch = 0,
                ReservationGroupId = new() { Value = reservationId },
                CanonicalRequest = canonical.Clone(),
                AuthorizedOnce = true,
                Disposition = DispatchDispositionV2.Authorized,
                AccountIdentityId = new() { Value = accounting.AccountIdentityId },
                BillingScopeIdentityId = new() { Value = accounting.BillingScopeIdentityId },
                EffectiveConfigurationId = "m1-s6-campaign-exact-profile",
                CapabilitySnapshotId = new() { Value = M1ProviderCatalog.Capability.Identity.Value },
                PriceSnapshotId = new() { Value = M1ProviderCatalog.Price.Identity.Value },
                Settings = settings.Clone(),
                OutputSchema = outputSchema.Clone(),
                OperationKind = operation,
                InputBoundProof = requestValue.InputBoundProof.Clone(),
                EvaluatedAt = Instant(now.AddTicks(1)),
                DispatchDeadline = deadline.Clone(),
                RequestId = requestId,
                OperationId = new() { Value = operationId },
                RequestFingerprintSha256 = ByteString.CopyFrom(requestValue.RequestFingerprintSha256.Span),
                Limits = limits.Clone(),
            },
        };

        // The durable latch is conservative: once granted, any helper failure is a possible start and
        // consumes the stage. The contained helper then proves the exact credential R/F trace.
        await possibleStart(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        HelperProcessReceipt process = await executeHelper(bootstrap, assignment, final,
            TimeSpan.FromMilliseconds(authority.Limits.DeadlineMilliseconds), now, cancellationToken)
            .ConfigureAwait(false);
        ValidateCredentialTrace(process, targetFingerprint);
        ValidateCanaries(process);
        if (!process.Receipt.TransportMayHaveStarted || process.RetryAttempted
            || process.NetworkOperationCount != 1 || process.ListenerCount != 0
            || !process.ContainmentProbeExecuted || process.TotalContainedProcessCount < 2
            || process.ProcessTreeSurvivorCount != 0 || !process.ProcessTreeTerminated
            || process.ExcludedHandleAccessible || process.StagedResponseBytes.Length == 0)
        {
            throw new InvalidDataException("Campaign provider helper returned ambiguous containment or transport evidence.");
        }
        if (!OpenAiStagedResponseEnvelope.TryRead(process.StagedResponseBytes, out byte[] raw, out byte[] headers))
        {
            throw new InvalidDataException("Campaign provider helper omitted its canonical staged response envelope.");
        }
        OpenAiResponsesResult result = OpenAiStagedResponseEnvelope.Replay(raw, headers, requestId) with
        {
            TransportMayHaveStarted = process.Receipt.TransportMayHaveStarted,
            NetworkUsed = process.NetworkOperationCount == 1,
            SendCount = 1,
        };
        M1Slice6CampaignCredentialReadReceipt observedRead = new(profileId, generationId,
            targetFingerprint, 1, 1, 0, 0, "success", "released");
        return new(result, observedRead, authority.CanonicalRequestSha256,
            authority.SafetyIdentifierProjection, result.DnsResolutionCount, headers,
            process.NativeCallTraceBytes!, process.NativeCanaryEvidenceBytes!, DateTimeOffset.UtcNow);
    }

    private static void ValidateCredentialTrace(HelperProcessReceipt process, string fingerprint)
    {
        using JsonDocument trace = JsonDocument.Parse(process.NativeCallTraceBytes
            ?? throw new InvalidDataException("Campaign provider helper omitted its credential trace."));
        JsonElement[] calls = trace.RootElement.EnumerateArray().ToArray();
        if (process.NativeCredentialOperationCount != 2 || calls.Length != 2
            || calls[0].GetProperty("Operation").GetString() != "CredReadW"
            || calls[0].GetProperty("Result").GetString() != "success"
            || calls[1].GetProperty("Operation").GetString() != "CredFree"
            || calls[1].GetProperty("Result").GetString() != "released"
            || calls.Any(call => call.GetProperty("Scenario").GetString()
                != "m1-s6-campaign-provider-dispatch")
            || calls.Any(call => call.GetProperty("TargetFingerprintSha256").GetString() != fingerprint)
            || calls[0].GetProperty("AllocationId").GetInt64()
                != calls[1].GetProperty("PairedAllocationId").GetInt64())
        {
            throw new InvalidDataException("Campaign provider credential read/free trace is not exact.");
        }
    }

    private static void ValidateCanaries(HelperProcessReceipt process)
    {
        using JsonDocument document = JsonDocument.Parse(process.NativeCanaryEvidenceBytes
            ?? throw new InvalidDataException("Campaign provider helper omitted canary evidence."));
        JsonElement root = document.RootElement;
        string[] encodings = root.GetProperty("RawTargetEncodings").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
        JsonElement[] surfaces = root.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
        string[] names = surfaces.Select(value => value.GetProperty("Name").GetString()!).ToArray();
        string[] exactNames = ["private protocol request", "private protocol response", "native call trace",
            "process command line", "process environment names"];
        string[] kinds = surfaces.Select(value => value.GetProperty("Kind").GetString()!).ToArray();
        string[] exactKinds = ["private-pipe-bytes", "private-pipe-bytes", "canonical-trace-bytes",
            "captured-text", "captured-text"];
        if (root.GetProperty("SecretMatches").GetInt32() != 0
            || root.GetProperty("RawTargetMatches").GetInt32() != 0
            || !encodings.SequenceEqual(["utf-8", "utf-16le"], StringComparer.Ordinal)
            || !names.SequenceEqual(exactNames, StringComparer.Ordinal)
            || !kinds.SequenceEqual(exactKinds, StringComparer.Ordinal)
            || names.Distinct(StringComparer.Ordinal).Count() != exactNames.Length
            || surfaces.Any(value => value.GetProperty("SecretMatches").GetInt32() != 0
                || value.GetProperty("RawTargetMatches").GetInt32() != 0
                || value.GetProperty("ByteCount").GetInt64() <= 0))
        {
            throw new InvalidDataException("Campaign provider helper canary evidence is vacuous, incomplete, or matched secret material.");
        }
    }

    private static ByteString Fingerprint() => ByteString.CopyFrom(
        Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256));
    private static ContentDigest Digest(ReadOnlySpan<byte> value) => new()
    {
        Algorithm = DigestAlgorithm.Sha256,
        Value = ByteString.CopyFrom(SHA256.HashData(value)),
        SizeBytes = checked((ulong)value.Length),
    };
    private static Instant Instant(DateTimeOffset value) => new()
    {
        UnixSeconds = value.ToUnixTimeSeconds(),
        Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
    };
}

public static class M1Slice6CampaignStageManifestValidator
{
    public static M1Slice6CampaignStageAuthority LoadAndValidate(string manifestPath,
        string expectedSha256, M1Slice6FiniteCampaignLedger ledger, bool requireAdmitted)
    {
        manifestPath = Path.GetFullPath(manifestPath);
        byte[] bytes = File.ReadAllBytes(manifestPath);
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (sha != expectedSha256) { throw new InvalidDataException("The stage manifest bytes are stale."); }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "manifest_id", "status", "candidate_binding", "campaign_binding",
            "stage", "predecessor_evidence", "canonical_request", "transport", "limits",
            "safety_identifier", "validation_package", "execution");
        if (root.GetProperty("schema_identity").GetString()
                != "infinium.repository.m1-slice6-campaign-stage-request/1.0.0")
        {
            throw new InvalidDataException("The stage manifest schema identity is stale.");
        }
        string status = root.GetProperty("status").GetString()!;
        if (requireAdmitted ? status != "reviewed-and-admitted"
            : status is not ("verification-pending" or "ready-for-independent-review"))
        {
            throw new InvalidDataException("The stage manifest is outside the requested authority state.");
        }

        JsonElement candidate = root.GetProperty("candidate_binding");
        Exact(candidate, "close_ready_implementation_commit", "review_candidate_resolution");
        string closeReady = candidate.GetProperty("close_ready_implementation_commit").GetString()!;
        string resolution = candidate.GetProperty("review_candidate_resolution").GetString()!;
        bool pendingBinding = closeReady == "pending" && resolution == "pending";
        bool committedBinding = Hex(closeReady, 40)
            && resolution == "exact-clean-committed-two-file-stage-candidate";
        if (status == "verification-pending" ? !pendingBinding : !committedBinding)
        {
            throw new InvalidDataException("The stage candidate binding is mixed or stale.");
        }

        JsonElement campaign = root.GetProperty("campaign_binding");
        Exact(campaign, "campaign_id", "campaign_manifest_sha256", "campaign_review_candidate_commit",
            "credential_manifest_id", "credential_manifest_sha256");
        M1Slice6CampaignIdentity identity = ledger.Current.Identity;
        if (campaign.GetProperty("campaign_id").GetString() != identity.CampaignId
            || campaign.GetProperty("campaign_manifest_sha256").GetString() != identity.CampaignManifestSha256
            || campaign.GetProperty("campaign_review_candidate_commit").GetString() != identity.VerificationCandidateCommit
            || campaign.GetProperty("credential_manifest_id").GetString() != identity.CredentialManifestId
            || campaign.GetProperty("credential_manifest_sha256").GetString() != identity.CredentialManifestSha256)
        {
            throw new InvalidDataException("The stage manifest campaign or credential identity is stale.");
        }

        JsonElement stageNode = root.GetProperty("stage");
        Exact(stageNode, "ordinal", "work_package", "operation");
        int ordinal = stageNode.GetProperty("ordinal").GetInt32();
        M1Slice6CampaignStage stage = ordinal switch
        {
            1 => M1Slice6CampaignStage.Qualification,
            2 => M1Slice6CampaignStage.SourceClaimExtraction,
            3 => M1Slice6CampaignStage.CandidateInvestigation,
            _ => throw new InvalidDataException("The stage ordinal is outside the finite campaign."),
        };
        string workPackage = stageNode.GetProperty("work_package").GetString()!;
        string operationText = stageNode.GetProperty("operation").GetString()!;
        ProviderOperationKind operation = stage switch
        {
            M1Slice6CampaignStage.Qualification => ProviderOperationKind.TransportQualification,
            M1Slice6CampaignStage.SourceClaimExtraction => ProviderOperationKind.SourceClaimExtraction,
            _ => ProviderOperationKind.CandidateInvestigation,
        };
        if (workPackage != "WP" + (8 + ordinal) || operationText != stage.ToString())
        {
            throw new InvalidDataException("The stage ordinal, work package, and operation were swapped.");
        }

        JsonElement predecessor = root.GetProperty("predecessor_evidence");
        Exact(predecessor, "ledger_event_hash", "evidence_id", "evidence_sha256");
        if (predecessor.GetProperty("ledger_event_hash").GetString() != ledger.Current.EventHash
            || predecessor.GetProperty("evidence_id").GetString() != ledger.Current.EvidenceId
            || predecessor.GetProperty("evidence_sha256").GetString() != ledger.Current.EvidenceSha256)
        {
            throw new InvalidDataException("The stage predecessor evidence is stale.");
        }

        M1Slice6CampaignStage expected = ledger.Current.State switch
        {
            M1Slice6CampaignState.CredentialEvidenceAccepted => M1Slice6CampaignStage.Qualification,
            M1Slice6CampaignState.StageAccepted when ledger.Current.Stage == M1Slice6CampaignStage.Qualification
                => M1Slice6CampaignStage.SourceClaimExtraction,
            M1Slice6CampaignState.StageAccepted when ledger.Current.Stage == M1Slice6CampaignStage.SourceClaimExtraction
                => M1Slice6CampaignStage.CandidateInvestigation,
            _ => throw new InvalidDataException("The ledger has no eligible next stage."),
        };
        if (stage != expected) { throw new InvalidDataException("The stage is not the exact sequential successor."); }

        JsonElement request = root.GetProperty("canonical_request");
        Exact(request, "path", "sha256", "bytes", "campaign_input_bytes", "campaign_input_sha256",
            "request_template_sha256", "input_bound_policy_id", "input_bound_policy_version",
            "o200k_token_count", "token_ids_sha256", "structural_allowance_tokens", "proved_input_tokens",
            "maximum_output_tokens");
        string relative = request.GetProperty("path").GetString()!;
        string directory = Path.GetDirectoryName(manifestPath)!;
        string requestPath = Path.GetFullPath(Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!requestPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The canonical request escaped the stage authority directory.");
        }
        byte[] canonical = File.ReadAllBytes(requestPath);
        string requestSha = Convert.ToHexStringLower(SHA256.HashData(canonical));
        long provedInput = request.GetProperty("proved_input_tokens").GetInt64();
        long maximumOutput = request.GetProperty("maximum_output_tokens").GetInt64();
        JsonElement limitsNode = root.GetProperty("limits");
        Exact(limitsNode, "maximum_request_bytes", "maximum_input_tokens", "maximum_output_tokens",
            "maximum_raw_response_bytes", "maximum_nano_usd", "deadline_milliseconds");
        M1Slice6CampaignStageLimits exactLimits = M1Slice6CampaignStageLimits.For(stage);
        ProviderFiniteLimitsContract proofLimits = new(exactLimits.MaximumRequestBytes,
            exactLimits.MaximumInputTokens, exactLimits.MaximumOutputTokens, exactLimits.MaximumRawResponseBytes,
            1, exactLimits.MaximumNanoUsd, exactLimits.DeadlineMilliseconds);
        ProviderInputBoundEvidence proof = OpenAiResponsesInputBoundPolicy.Prove(operation, canonical, proofLimits);
        (long campaignInputBytes, string campaignInputSha256, string requestTemplateSha256) =
            M1Slice6CampaignSemanticAdmission.BindCanonicalInputAndTemplate(canonical);
        if (request.GetProperty("sha256").GetString() != requestSha
            || request.GetProperty("bytes").GetInt64() != canonical.LongLength
            || request.GetProperty("campaign_input_bytes").GetInt64() != campaignInputBytes
            || request.GetProperty("campaign_input_sha256").GetString() != campaignInputSha256
            || request.GetProperty("request_template_sha256").GetString() != requestTemplateSha256
            || request.GetProperty("input_bound_policy_id").GetString() != OpenAiResponsesCanonicalSerializer.InputBoundPolicyId
            || request.GetProperty("input_bound_policy_version").GetString() != OpenAiResponsesCanonicalSerializer.InputBoundPolicyVersion
            || request.GetProperty("o200k_token_count").GetInt64() != proof.O200kTokenCount
            || request.GetProperty("token_ids_sha256").GetString() != proof.TokenIdsFingerprint.Value
            || request.GetProperty("structural_allowance_tokens").GetInt64() != proof.StructuralAllowanceTokens
            || provedInput != proof.ConservativeInputTokenUpperBound)
        {
            throw new InvalidDataException("The canonical request proof is stale.");
        }
        long[] actual = [limitsNode.GetProperty("maximum_request_bytes").GetInt64(),
            limitsNode.GetProperty("maximum_input_tokens").GetInt64(),
            limitsNode.GetProperty("maximum_output_tokens").GetInt64(),
            limitsNode.GetProperty("maximum_raw_response_bytes").GetInt64(),
            limitsNode.GetProperty("maximum_nano_usd").GetInt64(),
            limitsNode.GetProperty("deadline_milliseconds").GetInt64()];
        long[] exact = [exactLimits.MaximumRequestBytes, exactLimits.MaximumInputTokens,
            exactLimits.MaximumOutputTokens, exactLimits.MaximumRawResponseBytes,
            exactLimits.MaximumNanoUsd, exactLimits.DeadlineMilliseconds];
        if (!actual.SequenceEqual(exact) || canonical.LongLength > exactLimits.MaximumRequestBytes
            || provedInput < 0 || provedInput > exactLimits.MaximumInputTokens
            || maximumOutput != exactLimits.MaximumOutputTokens)
        {
            throw new InvalidDataException("The stage request or finite limits differ from the campaign envelope.");
        }
        OpenAiResponsesCanonicalSerializer.ValidateExactProfile(canonical, maximumOutput);
        using (JsonDocument canonicalDocument = JsonDocument.Parse(canonical))
        {
            string expectedName = operation switch
            {
                ProviderOperationKind.TransportQualification => "transport_qualification",
                ProviderOperationKind.SourceClaimExtraction => "source_claim_extraction",
                _ => "candidate_investigation",
            };
            if (canonicalDocument.RootElement.GetProperty("text").GetProperty("format")
                    .GetProperty("name").GetString() != expectedName)
            {
                throw new InvalidDataException("The canonical request operation differs from its stage.");
            }
        }

        JsonElement transport = root.GetProperty("transport");
        Exact(transport, "scheme", "host", "path", "method", "tool_choice", "tool_count",
            "retry_count", "parallel", "maximum_provider_calls", "maximum_dns_resolutions");
        if (transport.GetProperty("scheme").GetString() != "https"
            || transport.GetProperty("host").GetString() != "api.openai.com"
            || transport.GetProperty("path").GetString() != "/v1/responses"
            || transport.GetProperty("method").GetString() != "POST"
            || transport.GetProperty("tool_choice").GetString() != "none"
            || transport.GetProperty("tool_count").GetInt32() != 0
            || transport.GetProperty("retry_count").GetInt32() != 0
            || transport.GetProperty("parallel").GetBoolean()
            || transport.GetProperty("maximum_provider_calls").GetInt32() != 1
            || transport.GetProperty("maximum_dns_resolutions").GetInt32() != 1)
        {
            throw new InvalidDataException("The stage transport surface was broadened.");
        }

        JsonElement safety = root.GetProperty("safety_identifier");
        Exact(safety, "projection", "state_version", "raw_seed_present");
        string projection = safety.GetProperty("projection").GetString()!;
        if (!ProductUserSafetyIdentifier.IsValidProjection(projection)
            || safety.GetProperty("state_version").GetString() != "infinium.product-user-safety-identifier/1.0.0"
            || safety.GetProperty("raw_seed_present").GetBoolean()
            || ledger.Current.SafetyIdentifierProjection.Length != 0
                && ledger.Current.SafetyIdentifierProjection != projection)
        {
            throw new InvalidDataException("The stage safety identifier binding is missing, raw, or stale.");
        }

        JsonElement validation = root.GetProperty("validation_package");
        Exact(validation, "package_id", "manifest_path", "manifest_sha256", "product_input_path",
            "product_input_bytes", "product_input_sha256", "predecessor_manifest_path",
            "predecessor_manifest_bytes", "predecessor_manifest_sha256", "oracle_path",
            "oracle_sha256", "deterministic_oracle_result_sha256", "semantic_use");
        (string packageId, string packagePath, string packageSha, string productInputPath,
            long productInputBytes, string productInputSha, string predecessorPath,
            long predecessorBytes, string predecessorSha, string oraclePath,
            string oracleSha, string deterministicResultSha, bool semanticUse) expectedPackage = stage switch
            {
                M1Slice6CampaignStage.Qualification => (
                    "M1-PLAT-PROVIDER-CAPABILITY-VAL-v1",
                    "fixtures/public/platform/provider-budget/capability-val/public-manifest.json",
                    "3fa9f56a2ad1f815638ed7f4ce198b499cc072604e2a29b3bf5e418d6d33389c",
                    "fixtures/public/platform/provider-budget/capability-val/input.json", 190,
                    "c9cb054a578a244bca1a1d77bcc2ca7f2898ada42f4250da88588ddf6472b55a",
                    "fixtures/public/platform/provider-budget/capability-val/public-manifest.json", 526,
                    "3fa9f56a2ad1f815638ed7f4ce198b499cc072604e2a29b3bf5e418d6d33389c",
                    "fixtures/public/platform/provider-budget/capability-val/oracle.json",
                    "7ce656a0d056239cedcbfc75ec44b21ca7be79946da2613a109dfd233f6b8bda",
                    "0000000000000000000000000000000000000000000000000000000000000000", false),
                M1Slice6CampaignStage.SourceClaimExtraction => (
                    "LLM-CLAIM-LIVE-VAL",
                    "fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL/public-manifest.json",
                    "83a63ba290966f27f7f6ddc581f63f7e2cb7b4d745f6958177f17043f220b3e6",
                    "fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/execution-input.v1.json", 1767,
                    "77cffbebbc940357e1f8b39a9fd054c50e6c1a25c9e24c7b564f37867b95469c",
                    "fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/public-manifest.json", 1320,
                    "0f95265340873dc4abb083c6f857db9e8786c6e1ba36da385f07c876afe1c13f",
                    "fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL/oracle.v1.json",
                    "d917aed55912b0d6c82f8d19c772c6c504b9edcd3b1d3dcf9082da0f7a52e9eb",
                    "122aa491aca8fdb23e2ca9405bd0651b2c69c4e372fbc378584ce779520f0c3c", true),
                _ => (
                    "LLM-INVESTIGATE-LIVE-VAL",
                    "fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL/public-manifest.json",
                    "486181221a67311ca14e5454451f40012465535e0f09e999561a58ed5110a135",
                    "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/execution-input.v1.json", 12403,
                    "99029f0834e03e72bbba69ad4991a7ca22c441ce4888cfcfac31e7ca7e74fbe7",
                    "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/public-manifest.json", 2035,
                    "b42dff12144f192c1e7a913a3a99433398f0f2d41148a3353f7aa9cf89154323",
                    "fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL/oracle.v1.json",
                    "3f6db5e3618d8d0b5d35f2e79c203ef5bcd1bac8166e5cae417e7b5ac2e3348a",
                    "bb5ac6574182a5335f445d75df88e4e9514794da5756c0657f27b741711bdb14", true),
            };
        string repository = FindRepositoryRoot(manifestPath);
        string packageRelative = validation.GetProperty("manifest_path").GetString()!;
        string productInputRelative = validation.GetProperty("product_input_path").GetString()!;
        string predecessorRelative = validation.GetProperty("predecessor_manifest_path").GetString()!;
        string oracleRelative = validation.GetProperty("oracle_path").GetString()!;
        string packageAbsolute = Path.GetFullPath(Path.Combine(repository,
            packageRelative.Replace('/', Path.DirectorySeparatorChar)));
        string oracleAbsolute = Path.GetFullPath(Path.Combine(repository,
            oracleRelative.Replace('/', Path.DirectorySeparatorChar)));
        string productInputAbsolute = Path.GetFullPath(Path.Combine(repository,
            productInputRelative.Replace('/', Path.DirectorySeparatorChar)));
        string predecessorAbsolute = Path.GetFullPath(Path.Combine(repository,
            predecessorRelative.Replace('/', Path.DirectorySeparatorChar)));
        if (validation.GetProperty("package_id").GetString() != expectedPackage.packageId
            || packageRelative != expectedPackage.packagePath
            || validation.GetProperty("manifest_sha256").GetString() != expectedPackage.packageSha
            || productInputRelative != expectedPackage.productInputPath
            || validation.GetProperty("product_input_bytes").GetInt64() != expectedPackage.productInputBytes
            || validation.GetProperty("product_input_sha256").GetString() != expectedPackage.productInputSha
            || predecessorRelative != expectedPackage.predecessorPath
            || validation.GetProperty("predecessor_manifest_bytes").GetInt64() != expectedPackage.predecessorBytes
            || validation.GetProperty("predecessor_manifest_sha256").GetString() != expectedPackage.predecessorSha
            || oracleRelative != expectedPackage.oraclePath
            || validation.GetProperty("oracle_sha256").GetString() != expectedPackage.oracleSha
            || validation.GetProperty("deterministic_oracle_result_sha256").GetString()
                != expectedPackage.deterministicResultSha
            || validation.GetProperty("semantic_use").GetBoolean() != expectedPackage.semanticUse
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(packageAbsolute))) != expectedPackage.packageSha
            || new FileInfo(productInputAbsolute).Length != expectedPackage.productInputBytes
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(productInputAbsolute))) != expectedPackage.productInputSha
            || new FileInfo(predecessorAbsolute).Length != expectedPackage.predecessorBytes
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(predecessorAbsolute))) != expectedPackage.predecessorSha
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(oracleAbsolute))) != expectedPackage.oracleSha)
        {
            throw new InvalidDataException("The stage semantic validation package or frozen oracle is stale, swapped, or broadened.");
        }

        JsonElement execution = root.GetProperty("execution");
        Exact(execution, "provider_request_permitted", "requires_exact_review_marker",
            "requires_exact_admission_marker", "automatic_retry", "fourth_call_permitted");
        if (execution.GetProperty("provider_request_permitted").GetBoolean()
                != (status == "reviewed-and-admitted")
            || !execution.GetProperty("requires_exact_review_marker").GetBoolean()
            || !execution.GetProperty("requires_exact_admission_marker").GetBoolean()
            || execution.GetProperty("automatic_retry").GetBoolean()
            || execution.GetProperty("fourth_call_permitted").GetBoolean())
        {
            throw new InvalidDataException("The stage execution authority is absent or broadened.");
        }

        string reviewed = requireAdmitted
            ? ResolveCommittedStageAuthority(manifestPath, requestPath, sha, closeReady,
                root.GetProperty("manifest_id").GetString()!, identity, predecessor.GetProperty("evidence_sha256").GetString()!)
            : "pending";
        return new(root.GetProperty("manifest_id").GetString()!, sha, stage, workPackage, operation,
            reviewed, predecessor.GetProperty("evidence_id").GetString()!,
            predecessor.GetProperty("evidence_sha256").GetString()!, requestPath, requestSha,
            canonical, provedInput, exactLimits, projection, expectedPackage.packageId,
            expectedPackage.packagePath, expectedPackage.packageSha,
            expectedPackage.productInputPath, expectedPackage.productInputBytes,
            expectedPackage.productInputSha, expectedPackage.predecessorPath,
            expectedPackage.predecessorBytes, expectedPackage.predecessorSha, expectedPackage.oraclePath,
            expectedPackage.oracleSha, expectedPackage.deterministicResultSha, expectedPackage.semanticUse);
    }

    private static void Exact(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal))
        {
            throw new InvalidDataException("A stage authority object has an unknown, missing, duplicate, or reordered property.");
        }
    }

    private static bool Hex(string value, int length) => value.Length == length
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string ResolveCommittedStageAuthority(string manifestPath, string requestPath,
        string manifestSha, string closeReady, string manifestId, M1Slice6CampaignIdentity identity,
        string predecessorEvidenceSha)
    {
        DirectoryInfo? cursor = new(Path.GetDirectoryName(manifestPath)!);
        while (cursor is not null && !Directory.Exists(Path.Combine(cursor.FullName, ".git"))) { cursor = cursor.Parent; }
        string repository = cursor?.FullName
            ?? throw new InvalidDataException("Stage execution requires an exact Git worktree.");
        string manifestRelative = Path.GetRelativePath(repository, manifestPath).Replace('\\', '/');
        string requestRelative = Path.GetRelativePath(repository, requestPath).Replace('\\', '/');
        string prefix = "docs/plans/milestones/m1/slices/s6/live/";
        if (!manifestRelative.StartsWith(prefix, StringComparison.Ordinal)
            || !requestRelative.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Stage authority and canonical request are not at canonical live-plan paths.");
        }
        string recordRelative = "docs/plans/milestones/m1/slices/s6/record.md";
        string recordPath = Path.Combine(repository, recordRelative.Replace('/', Path.DirectorySeparatorChar));
        string[] lines = File.ReadAllLines(recordPath);
        string reviewPrefix = "M1_S6_CAMPAIGN_STAGE_REVIEW_ACCEPTANCE candidate_commit=";
        string reviewSuffix = $" campaign_id={identity.CampaignId} campaign_sha256={identity.CampaignManifestSha256}" +
            $" stage_manifest_id={manifestId} sha256={manifestSha} predecessor_evidence_sha256={predecessorEvidenceSha}" +
            " verdicts=security,semantics,diff";
        string[] reviewLines = lines.Where(line => line.StartsWith(reviewPrefix, StringComparison.Ordinal)
            && line.EndsWith(reviewSuffix, StringComparison.Ordinal)).ToArray();
        if (reviewLines.Length != 1) { throw new InvalidDataException("The exact stage review marker is absent or duplicated."); }
        string reviewed = reviewLines[0][reviewPrefix.Length..^reviewSuffix.Length];
        if (!Hex(reviewed, 40)) { throw new InvalidDataException("The stage review candidate is malformed."); }
        string admission = $"M1_S6_CAMPAIGN_STAGE_ADMISSION candidate_commit={reviewed} campaign_id={identity.CampaignId}" +
            $" campaign_sha256={identity.CampaignManifestSha256} stage_manifest_id={manifestId} sha256={manifestSha}" +
            $" predecessor_evidence_sha256={predecessorEvidenceSha} expires_at_utc=2026-08-22T23:59:00.0000000Z";
        if (lines.Count(line => line == admission) != 1)
        {
            throw new InvalidDataException("The exact stage admission marker is absent or duplicated.");
        }
        if (reviewed == closeReady || RunGit(repository, "merge-base", "--is-ancestor", closeReady, reviewed).ExitCode != 0)
        {
            throw new InvalidDataException("The stage candidate is not a distinct successor of its close-ready source.");
        }
        string[] changed = RunGit(repository, "-c", "core.quotePath=false", "diff", "--name-only",
            closeReady, reviewed, "--").Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] exact = [manifestRelative, requestRelative];
        Array.Sort(changed, StringComparer.Ordinal);
        Array.Sort(exact, StringComparer.Ordinal);
        if (!changed.SequenceEqual(exact, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The stage review candidate differs from its source by more or less than two exact files.");
        }
        string candidateManifestSha = GitBlobSha256(repository, reviewed, manifestRelative);
        if (candidateManifestSha != manifestSha)
        {
            throw new InvalidDataException("The stage review candidate does not bind exact manifest bytes: "
                + candidateManifestSha + " != " + manifestSha);
        }
        string reviewCommit = UniqueMarkerCommit(repository, reviewLines[0], reviewed, recordRelative);
        _ = UniqueMarkerCommit(repository, admission, reviewCommit, recordRelative);
        return reviewed;
    }

    internal static string UniqueMarkerCommit(string repository, string marker, string expectedParent,
        string recordRelative)
    {
        string commit = FindMarkerAddition(repository, marker, recordRelative, expectedParent + "..HEAD");
        string actualParent = RunGit(repository, "rev-parse", commit + "^").Output.Trim();
        if (actualParent != expectedParent)
        {
            throw new InvalidDataException("A stage marker transition has a stale predecessor: "
                + actualParent + " != " + expectedParent);
        }
        string[] predecessorLines = GitRecordLines(repository, expectedParent, recordRelative);
        string[] transitionLines = GitRecordLines(repository, commit, recordRelative);
        string[] headLines = GitRecordLines(repository, "HEAD", recordRelative);
        int predecessorMatches = Array.FindAll(predecessorLines, line => line == marker).Length;
        int transitionMatches = Array.FindAll(transitionLines, line => line == marker).Length;
        int headMatches = Array.FindAll(headLines, line => line == marker).Length;
        if (predecessorMatches != 0 || transitionMatches != 1 || headMatches != 1)
        {
            throw new InvalidDataException($"A stage marker is absent, duplicated, or present before its transition ({predecessorMatches}/{transitionMatches}/{headMatches}).");
        }
        string[] changed = RunGit(repository, "-c", "core.quotePath=false", "diff", "--name-only",
            expectedParent, commit, "--").Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] exact = ["docs/current-state.md", "docs/plans/milestones/m1/slices/s6/README.md", recordRelative];
        Array.Sort(changed, StringComparer.Ordinal);
        Array.Sort(exact, StringComparer.Ordinal);
        if (!changed.SequenceEqual(exact, StringComparer.Ordinal))
        {
            throw new InvalidDataException("A stage marker transition changed a fourth or missing path.");
        }
        string predecessorRecord = RunGit(repository, "show", expectedParent + ":" + recordRelative).Output.TrimEnd('\n');
        string currentRecord = RunGit(repository, "show", commit + ":" + recordRelative).Output.TrimEnd('\n');
        if (!currentRecord.StartsWith(predecessorRecord + "\n", StringComparison.Ordinal))
        {
            throw new InvalidDataException("A stage marker transition is not append-only.");
        }
        if (RunGit(repository, "merge-base", "--is-ancestor", commit, "HEAD").ExitCode != 0)
        {
            throw new InvalidDataException("A stage marker transition is not an ancestor of current HEAD.");
        }
        return commit;
    }

    internal static string FindUniqueMarkerCommit(string repository, string marker, string recordRelative)
    {
        string[] lines = GitRecordLines(repository, "HEAD", recordRelative);
        if (Array.FindAll(lines, line => line == marker).Length != 1)
        {
            throw new InvalidDataException("A campaign marker has no unique committed transition.");
        }
        return FindMarkerAddition(repository, marker, recordRelative, "HEAD");
    }

    private static string FindMarkerAddition(string repository, string marker, string recordRelative,
        string revision)
    {
        string historyOutput = revision.Contains("..", StringComparison.Ordinal)
            ? RunGit(repository, "log", "--format=%H", revision, "--", recordRelative).Output
            : RunGit(repository, "log", "--format=%H", "--fixed-strings", "-S", marker,
                revision, "--", recordRelative).Output;
        string[] history = historyOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        List<string> additions = [];
        foreach (string candidate in history)
        {
            string[] ancestry = RunGit(repository, "rev-list", "--parents", "-n", "1", candidate).Output
                .Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (ancestry.Length != 2) { continue; }
            string parent = ancestry[1];
            if (string.IsNullOrWhiteSpace(RunGit(repository, "ls-tree", "--name-only", parent,
                "--", recordRelative).Output)) { continue; }
            int candidateMatches = Array.FindAll(GitRecordLines(repository, candidate, recordRelative),
                line => line == marker).Length;
            int parentMatches = Array.FindAll(GitRecordLines(repository, parent, recordRelative),
                line => line == marker).Length;
            if (candidateMatches == 1 && parentMatches == 0) { additions.Add(candidate); }
        }
        if (additions.Count != 1 || !Hex(additions[0], 40))
        {
            throw new InvalidDataException("A campaign marker has no unique exact-line addition commit.");
        }
        return additions[0];
    }

    private static string[] GitRecordLines(string repository, string commit, string recordRelative)
        => RunGit(repository, "show", commit + ":" + recordRelative).Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    internal static string FindRepositoryRoot(string path)
    {
        DirectoryInfo? cursor = new(Path.GetDirectoryName(Path.GetFullPath(path))!);
        while (cursor is not null && !Directory.Exists(Path.Combine(cursor.FullName, ".git")))
        {
            cursor = cursor.Parent;
        }
        return cursor?.FullName ?? throw new InvalidDataException("Campaign authority requires an exact Git worktree.");
    }

    private static (int ExitCode, string Output) RunGit(string repository, params string[] arguments)
    {
        System.Diagnostics.ProcessStartInfo start = new("git")
        {
            WorkingDirectory = repository,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("Git authority probe did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 && arguments[0] is not "merge-base")
        {
            throw new InvalidDataException("Git authority probe failed: " + error.GetType().Name);
        }
        return (process.ExitCode, output.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static string GitBlobSha256(string repository, string commit, string relativePath)
    {
        System.Diagnostics.ProcessStartInfo start = new("git")
        {
            WorkingDirectory = repository,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("show");
        start.ArgumentList.Add(commit + ":" + relativePath);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("Git blob authority probe did not start.");
        using MemoryStream bytes = new();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || bytes.Length == 0)
        {
            throw new InvalidDataException("Git blob authority probe failed: " + error.GetType().Name);
        }
        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
    }
}

public sealed class M1Slice6CampaignStageCoordinator
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { WriteIndented = true };
    private readonly M1Slice6FiniteCampaignLedger ledger;
    private readonly ProductUserSafetyIdentifierStateStore safetyState;
    private readonly IM1Slice6CampaignStageExecutionBoundary boundary;
    private readonly IM1Slice6CampaignProviderAccounting accounting;

    public M1Slice6CampaignStageCoordinator(M1Slice6FiniteCampaignLedger ledger,
        ProductUserSafetyIdentifierStateStore safetyState,
        IM1Slice6CampaignStageExecutionBoundary boundary,
        IM1Slice6CampaignProviderAccounting accounting)
    {
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.safetyState = safetyState ?? throw new ArgumentNullException(nameof(safetyState));
        this.boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        this.accounting = accounting ?? throw new ArgumentNullException(nameof(accounting));
    }

    public async Task<string> ExecuteOneShotAsync(string manifestPath, string manifestSha256,
        string evidencePath, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (ledger.Current.State == M1Slice6CampaignState.TransportMayHaveStarted)
        {
            string actualManifestSha = Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(manifestPath))));
            if (actualManifestSha != manifestSha256
                || actualManifestSha != ledger.Current.RequestManifestSha256
                || accounting is not IM1Slice6CampaignRecoveryAccounting recovery)
            {
                ledger.StopAfterAmbiguousStart(ledger.Current.Stage,
                    "unreconciled-start", now);
                throw new InvalidDataException(
                    "A possible-start predecessor cannot be reconciled to exact SQLite settlement evidence.");
            }
            using JsonDocument recoveryManifest = JsonDocument.Parse(
                File.ReadAllBytes(Path.GetFullPath(manifestPath)));
            JsonElement recoveryRoot = recoveryManifest.RootElement;
            string canonicalRequestSha = recoveryRoot.GetProperty("canonical_request")
                .GetProperty("sha256").GetString() ?? string.Empty;
            if (recoveryRoot.GetProperty("manifest_id").GetString() != ledger.Current.RequestManifestId
                || recoveryRoot.GetProperty("stage").GetProperty("ordinal").GetInt32()
                    != (int)ledger.Current.Stage)
            {
                ledger.StopAfterAmbiguousStart(ledger.Current.Stage,
                    "unreconciled-start", now);
                throw new InvalidDataException(
                    "A possible-start predecessor has a stale stage manifest identity.");
            }
            M1Slice6CampaignRecoveredSettlement? recovered =
                recovery.TryRecoverKnownSettlement(ledger.Current.Stage, canonicalRequestSha);
            if (recovered is null)
            {
                ledger.StopAfterAmbiguousStart(ledger.Current.Stage,
                    "unreconciled-start", now);
                throw new InvalidDataException(
                    "A possible-start predecessor has no exact SQLite settlement evidence.");
            }
            M1Slice6CampaignNativeEnvelope recoveredNative = new(0, 1, 0, 1, 2);
            ledger.RecordKnownSettlement(ledger.Current.Stage, recovered.InputTokens,
                recovered.OutputTokens, recovered.RawResponseBytes, recovered.SettledNanoUsd,
                recoveredNative, now);
            ledger.StopAfterKnownSettlement(ledger.Current.Stage,
                "reconciled-sqlite-settlement", now.AddTicks(1));
            throw new M1Slice6CampaignKnownSettlementException(
                "The provider response was already settled before restart; execution is terminal with no retry.",
                new InvalidOperationException("reconciled-sqlite-settlement"));
        }
        M1Slice6CampaignStageAuthority authority = M1Slice6CampaignStageManifestValidator.LoadAndValidate(
            manifestPath, manifestSha256, ledger, requireAdmitted: true);
        ledger.ReserveStage(authority.Stage, new(authority.ManifestId, authority.ManifestSha256,
            authority.CanonicalRequest.LongLength, authority.ProvedInputTokens,
            authority.Limits.MaximumOutputTokens, authority.Limits.MaximumRawResponseBytes,
            authority.Limits.MaximumNanoUsd), now);
        M1Slice6CampaignAccountingAdmission? accountingAdmission = null;
        try
        {
            accountingAdmission = accounting.Prepare(
                authority, ledger.Current.Identity, now);
            int possibleStartCount = 0;
            M1Slice6CampaignCredentialReadReceipt? readReceipt = null;
            M1Slice6CampaignStageBoundaryResult result;
            try
            {
                result = await boundary.ExecuteOnceAsync(authority, accountingAdmission, (possibleStartAt, _) =>
                {
                    if (Interlocked.Increment(ref possibleStartCount) != 1)
                    {
                        throw new InvalidDataException("The stage possible-start latch is not exactly once.");
                    }
                    string durableProjection = ledger.Current.SafetyIdentifierProjection.Length == 0
                        ? safetyState.LatchPossibleStart()
                        : safetyState.GetRequiredProjection(authority.SafetyIdentifierProjection);
                    if (durableProjection != authority.SafetyIdentifierProjection)
                    {
                        ledger.StopBeforePossibleStart(authority.Stage, "safety-projection-drift", possibleStartAt);
                        throw new InvalidDataException("The durable safety projection changed before possible start.");
                    }
                    ledger.LatchPossibleStart(authority.Stage, durableProjection, possibleStartAt);
                    accounting.RecordPossibleStart(accountingAdmission, possibleStartAt);
                    return Task.CompletedTask;
                }, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (ledger.Current.State == M1Slice6CampaignState.TransportMayHaveStarted)
                {
                    ledger.StopAfterAmbiguousStart(authority.Stage, "ambiguous-start",
                        ledger.Current.RecordedAtUtc.AddTicks(1));
                }
                throw;
            }
            OpenAiResponsesResult response = result.Response;
            readReceipt = result.CredentialRead;
            if (possibleStartCount != 1
                || readReceipt.ProfileId != ledger.Current.Identity.CredentialProfileId
                || readReceipt.GenerationId != ledger.Current.Identity.CredentialGenerationId
                || readReceipt.TargetFingerprintSha256 != ledger.Current.Identity.CredentialTargetFingerprintSha256
                || readReceipt.CredReadW != 1 || readReceipt.CredFree != 1
                || readReceipt.CredWriteW != 0 || readReceipt.CredDeleteW != 0
                || readReceipt.ReadResult != "success" || readReceipt.FreeResult != "released"
                || response.SendCount != 1
                || !response.TransportMayHaveStarted || !response.NetworkUsed || response.RetryPermitted
                || result.DnsResolutionCount != 1 || result.CanonicalRequestSha256 != authority.CanonicalRequestSha256
                || result.SafetyIdentifierProjection != authority.SafetyIdentifierProjection
                || response.State != ProviderResponseState.Completed || !response.Admitted
                || response.HttpStatus is < 200 or > 299 || response.RawResponseBytes is null
                || response.RawResponseBytes.Length == 0 || result.ResponseHeadersBytes.Length == 0
                || string.IsNullOrWhiteSpace(response.ProviderResponseId)
                || string.IsNullOrWhiteSpace(response.ClientRequestId)
                || string.IsNullOrWhiteSpace(response.ProviderRequestId)
                || response.ReturnedModel != OpenAiResponsesCanonicalSerializer.Model
                || response.ReturnedServiceTier != OpenAiResponsesCanonicalSerializer.ServiceTier
                || response.RefusalCode is not null || response.IncompleteReason is not null
                || response.ErrorCode is not null)
            {
                ledger.StopAfterAmbiguousStart(authority.Stage, "ambiguous-start", result.CompletedAtUtc);
                throw new InvalidDataException("The one-shot stage boundary returned ambiguous or broadened transport facts.");
            }
            long input = Required(response.Usage.InputTokens, "input tokens");
            long output = Required(response.Usage.OutputTokens, "output tokens");
            long cost = Required(response.Usage.CalculatedNanoUsd, "calculated cost");
            long raw = response.RawResponseBytes?.LongLength
                ?? throw new InvalidDataException("A reviewable stage result requires exact retained raw bytes.");
            if (input > authority.Limits.MaximumInputTokens || output > authority.Limits.MaximumOutputTokens
                || raw > authority.Limits.MaximumRawResponseBytes || cost > authority.Limits.MaximumNanoUsd)
            {
                ledger.StopAfterAmbiguousStart(authority.Stage, "settlement-overrun", result.CompletedAtUtc);
                throw new InvalidDataException("The observed stage result exceeded its admitted envelope.");
            }
            M1Slice6CampaignAccountingSettlement accountingSettlement;
            try
            {
                accountingSettlement = accounting.PersistSettleAndReplay(
                    accountingAdmission, authority, result);
            }
            catch (M1Slice6CampaignKnownSettlementException)
            {
                M1Slice6CampaignNativeEnvelope knownNative = new(0, readReceipt.CredReadW,
                    readReceipt.CredDeleteW, readReceipt.CredFree,
                    checked(readReceipt.CredReadW + readReceipt.CredWriteW
                        + readReceipt.CredDeleteW + readReceipt.CredFree));
                ledger.RecordKnownSettlement(authority.Stage, input, output, raw, cost, knownNative,
                    result.CompletedAtUtc);
                ledger.StopAfterKnownSettlement(authority.Stage,
                    "semantic-admission-failure",
                    result.CompletedAtUtc.AddTicks(1));
                throw;
            }
            if (accountingSettlement.SettledNanoUsd != cost || accountingSettlement.UnresolvedHold
                || accountingSettlement.RetryPermitted)
            {
                ledger.StopAfterAmbiguousStart(authority.Stage, "stage-processing-failure", result.CompletedAtUtc);
                throw new InvalidDataException("Authoritative provider accounting did not settle exact usage with no hold and no retry.");
            }
            M1Slice6CampaignNativeEnvelope priorNative = ledger.CurrentNativeEnvelope;
            M1Slice6CampaignNativeEnvelope stageNative = new(0, readReceipt.CredReadW,
                readReceipt.CredDeleteW, readReceipt.CredFree,
                checked(readReceipt.CredReadW + readReceipt.CredWriteW
                    + readReceipt.CredDeleteW + readReceipt.CredFree));
            M1Slice6CampaignNativeEnvelope native = new(
                checked(priorNative.CredWriteW + stageNative.CredWriteW),
                checked(priorNative.CredReadW + stageNative.CredReadW),
                checked(priorNative.CredDeleteW + stageNative.CredDeleteW),
                checked(priorNative.CredFree + stageNative.CredFree),
                checked(priorNative.Total + stageNative.Total));
            ledger.RecordKnownSettlement(authority.Stage, input, output, raw, cost, stageNative,
                result.CompletedAtUtc);
            evidencePath = Path.GetFullPath(evidencePath);
            string evidenceDirectory = Path.GetDirectoryName(evidencePath)!;
            Directory.CreateDirectory(evidenceDirectory);
            string stem = Path.GetFileNameWithoutExtension(evidencePath);
            string rawPath = Path.Combine(evidenceDirectory, stem + ".raw-response.bin");
            string headersPath = Path.Combine(evidenceDirectory, stem + ".response-headers.json");
            string requestPath = Path.Combine(evidenceDirectory, stem + ".canonical-request.json");
            string tracePath = Path.Combine(evidenceDirectory, stem + ".native-trace.json");
            string canaryPath = Path.Combine(evidenceDirectory, stem + ".canaries.json");
            WriteNew(rawPath, response.RawResponseBytes);
            WriteNew(headersPath, result.ResponseHeadersBytes);
            WriteNew(requestPath, authority.CanonicalRequest);
            WriteNew(tracePath, result.NativeCallTraceBytes);
            WriteNew(canaryPath, result.CanaryEvidenceBytes);
            object evidence = new
            {
                schema = "infinium.m1-s6.campaign-stage-evidence/v1",
                status = "independent-review-pending",
                campaign_id = ledger.Current.Identity.CampaignId,
                campaign_manifest_sha256 = ledger.Current.Identity.CampaignManifestSha256,
                stage_manifest_id = authority.ManifestId,
                stage_manifest_sha256 = authority.ManifestSha256,
                stage = authority.Stage.ToString(),
                canonical_request_sha256 = authority.CanonicalRequestSha256,
                predecessor_evidence_id = authority.PredecessorEvidenceId,
                predecessor_evidence_sha256 = authority.PredecessorEvidenceSha256,
                safety_identifier_projection = authority.SafetyIdentifierProjection,
                provider_state = response.State.ToString(),
                http_status = response.HttpStatus,
                provider_response_id = response.ProviderResponseId,
                client_request_id = response.ClientRequestId,
                provider_request_id = response.ProviderRequestId,
                requested_model = OpenAiResponsesCanonicalSerializer.Model,
                returned_model = response.ReturnedModel,
                requested_service_tier = OpenAiResponsesCanonicalSerializer.ServiceTier,
                returned_service_tier = response.ReturnedServiceTier,
                reasoning_context = "current_turn",
                reasoning_mode = "standard",
                prompt_cache_mode = "explicit",
                provider_send_count = response.SendCount,
                dns_resolution_count = result.DnsResolutionCount,
                retry_permitted = response.RetryPermitted,
                input_tokens = input,
                output_tokens = output,
                raw_response_bytes = raw,
                calculated_nano_usd = cost,
                usage = response.Usage,
                rate_facts = response.RateHeaders.Select(item => new { name = item.Name, value = item.Value }).ToArray(),
                credential_reads = 1,
                credential_frees = 1,
                credential_writes = 0,
                credential_deletes = 0,
                cumulative_credential_calls = new
                {
                    CredWriteW = native.CredWriteW,
                    CredReadW = native.CredReadW,
                    CredDeleteW = native.CredDeleteW,
                    CredFree = native.CredFree,
                    total = native.Total,
                },
                retained_artifacts = new
                {
                    canonical_request_path = Path.GetFileName(requestPath),
                    canonical_request_sha256 = HashFile(requestPath),
                    raw_response_path = Path.GetFileName(rawPath),
                    raw_response_sha256 = HashFile(rawPath),
                    response_headers_path = Path.GetFileName(headersPath),
                    response_headers_sha256 = HashFile(headersPath),
                    native_trace_path = Path.GetFileName(tracePath),
                    native_trace_sha256 = HashFile(tracePath),
                    canary_evidence_path = Path.GetFileName(canaryPath),
                    canary_evidence_sha256 = HashFile(canaryPath),
                },
                authoritative_persistence = new
                {
                    authorization_id = accountingAdmission.AuthorizationId,
                    operation_id = accountingAdmission.OperationId,
                    attempt_id = accountingAdmission.AttemptId,
                    request_id = accountingAdmission.RequestId,
                    reservation_id = accountingAdmission.ReservationId,
                    dispatch_fence_id = accountingAdmission.DispatchFenceId,
                    response_id = accountingSettlement.ResponseId,
                    usage_entry_id = accountingSettlement.UsageEntryId,
                    settlement_id = accountingSettlement.SettlementId,
                    replay_edge_id = accountingSettlement.ReplayEdgeId,
                    raw_response_sha256 = accountingSettlement.RawResponseSha256,
                    response_headers_sha256 = accountingSettlement.ResponseHeadersSha256,
                    unresolved_hold = accountingSettlement.UnresolvedHold,
                    retry_permitted = accountingSettlement.RetryPermitted,
                },
                validation_package = new
                {
                    package_id = authority.ValidationPackageId,
                    manifest_path = authority.ValidationPackagePath,
                    manifest_sha256 = authority.ValidationPackageSha256,
                    product_input_path = authority.ValidationProductInputPath,
                    product_input_bytes = authority.ValidationProductInputBytes,
                    product_input_sha256 = authority.ValidationProductInputSha256,
                    predecessor_manifest_path = authority.ValidationPredecessorManifestPath,
                    predecessor_manifest_bytes = authority.ValidationPredecessorManifestBytes,
                    predecessor_manifest_sha256 = authority.ValidationPredecessorManifestSha256,
                    oracle_path = authority.ValidationOraclePath,
                    oracle_sha256 = authority.ValidationOracleSha256,
                    deterministic_oracle_result_sha256 = authority.DeterministicOracleResultSha256,
                    semantic_use = authority.SemanticUse,
                },
                semantic_validation = new
                {
                    validation_id = accountingSettlement.SemanticValidationId,
                    disposition = accountingSettlement.SemanticDisposition,
                    proposal_count = accountingSettlement.SemanticProposalCount,
                    admission_count = accountingSettlement.SemanticAdmissionCount,
                    result_sha256 = accountingSettlement.SemanticResultSha256,
                    source_acquisition_id = accountingSettlement.SemanticProvenance.SourceAcquisitionId,
                    source_admission_id = accountingSettlement.SemanticProvenance.SourceAdmissionId,
                    admitted_artifact_id = accountingSettlement.SemanticProvenance.AdmittedArtifactId,
                    source_application_link_id = accountingSettlement.SemanticProvenance.SourceApplicationLinkId,
                    evidence_application_link_id = accountingSettlement.SemanticProvenance.EvidenceApplicationLinkId,
                    candidate_id = accountingSettlement.SemanticProvenance.CandidateId,
                    hypothesis_id = accountingSettlement.SemanticProvenance.HypothesisId,
                },
            };
            byte[] evidenceBytes = JsonSerializer.SerializeToUtf8Bytes(evidence, EvidenceJson);
            WriteNew(evidencePath, [.. evidenceBytes, (byte)'\n']);
            string evidenceSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(evidencePath)));
            ledger.RecordStageEvidenceHandoff(authority.Stage, "campaign-stage-evidence-" + (int)authority.Stage,
                evidenceSha, input, output, raw, cost, stageNative, result.CompletedAtUtc);
            return evidenceSha;
        }
        catch
        {
            DateTimeOffset stopAt = ledger.Current.RecordedAtUtc.AddTicks(1);
            if (ledger.Current.State == M1Slice6CampaignState.StageReserved)
            {
                if (accountingAdmission is not null)
                {
                    accounting.ReleaseBeforePossibleStart(accountingAdmission, stopAt);
                }
                ledger.StopBeforePossibleStart(authority.Stage, "stage-prestart-failure", stopAt);
            }
            else if (ledger.Current.State == M1Slice6CampaignState.StageSettled)
            {
                ledger.StopAfterKnownSettlement(authority.Stage, "evidence-write-failure", stopAt);
            }
            else if (ledger.Current.State == M1Slice6CampaignState.TransportMayHaveStarted)
            {
                ledger.StopAfterAmbiguousStart(authority.Stage, "stage-processing-failure", stopAt);
            }
            throw;
        }
    }

    public void AcceptEvidence(M1Slice6CampaignStage stage, string evidenceId, string evidenceSha256,
        DateTimeOffset now) => ledger.AcceptStageEvidence(stage, evidenceId, evidenceSha256, now);

    public void CompleteComposedEvidence(string evidenceId, string evidenceSha256, DateTimeOffset now) =>
        ledger.CompleteComposedEvidence(evidenceId, evidenceSha256, now);

    private static long Required(ProviderQuantityContract quantity, string name) =>
        quantity.Availability == ProviderAvailabilityState.Available && quantity.Value is >= 0
            ? quantity.Value.Value : throw new InvalidDataException($"Exact {name} are unavailable.");

    private static void WriteNew(string path, byte[] bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}

internal static class M1Slice6CampaignStageRunner
{
    internal static async Task<int> RunAsync(string stageManifestPath, string stageManifestSha256,
        string campaignManifestPath, string campaignManifestSha256, string campaignReviewedCandidate,
        string credentialManifestPath, string credentialManifestSha256, string ledgerPath,
        string safetyStateRoot, string helperBinary, string helperSha256, string evidencePath,
        CancellationToken cancellationToken = default)
    {
        Wp9ProductionProfileEnrollmentRunner.ValidateCampaignAdmissionOnly(credentialManifestPath,
            credentialManifestSha256, campaignManifestPath, campaignManifestSha256,
            campaignReviewedCandidate);
        byte[] campaignBytes = File.ReadAllBytes(Path.GetFullPath(campaignManifestPath));
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        using JsonDocument campaign = JsonDocument.Parse(campaignBytes);
        using JsonDocument credential = JsonDocument.Parse(credentialBytes);
        JsonElement campaignRoot = campaign.RootElement;
        JsonElement credentialRoot = credential.RootElement;
        JsonElement profile = credentialRoot.GetProperty("profile");
        JsonElement attachment = campaignRoot.GetProperty("authority_source");
        DateTimeOffset campaignExpiry = DateTimeOffset.Parse(campaignRoot.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset credentialExpiry = DateTimeOffset.Parse(credentialRoot.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        if (credentialRoot.GetProperty("release_build").GetProperty("helper_sha256").GetString() != helperSha256
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(helperBinary)))) != helperSha256)
        {
            throw new InvalidDataException("Campaign stage helper bytes differ from the exact reviewed Release closure.");
        }
        M1Slice6CampaignIdentity identity = new(
            campaignRoot.GetProperty("campaign_id").GetString()!, campaignManifestSha256,
            attachment.GetProperty("attachment_sha256").GetString()!, campaignReviewedCandidate,
            credentialRoot.GetProperty("manifest_id").GetString()!, credentialManifestSha256,
            profile.GetProperty("access_profile_id").GetString()!,
            profile.GetProperty("generation_id").GetString()!,
            profile.GetProperty("target_fingerprint_sha256").GetString()!);
        M1Slice6FiniteCampaignLedger ledger = new(Path.GetFullPath(ledgerPath), identity,
            campaignExpiry, credentialExpiry, DateTimeOffset.UtcNow);
        ProductUserSafetyIdentifierStateStore safety = new(Path.GetFullPath(safetyStateRoot));
        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(credentialManifestPath);
        string expectedStateRoot = Path.GetFullPath(Path.Combine(repository,
            credentialRoot.GetProperty("durable_state").GetProperty("product_state_root_relative")
                .GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        if (Path.GetFullPath(safetyStateRoot) != expectedStateRoot)
        {
            throw new InvalidDataException("Campaign stage execution did not open the exact WP9 product-state root.");
        }
        using M1Slice6CampaignSqliteProviderAccounting accounting = new(expectedStateRoot,
            credentialManifestPath, credentialManifestSha256, DateTimeOffset.UtcNow);
        M1Slice6CampaignProductionStageBoundary boundary = new(helperBinary, helperSha256,
            credentialManifestPath, credentialManifestSha256, identity.CredentialManifestId);
        M1Slice6CampaignStageCoordinator coordinator = new(ledger, safety, boundary, accounting);
        _ = await coordinator.ExecuteOneShotAsync(stageManifestPath, stageManifestSha256,
            evidencePath, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    internal static void AcceptEvidence(string campaignManifestPath, string campaignManifestSha256,
        string campaignReviewedCandidate, string credentialManifestPath, string credentialManifestSha256,
        string ledgerPath, string stageManifestPath, string evidencePath, M1Slice6CampaignStage stage,
        string repositoryRecordPath, DateTimeOffset now)
    {
        M1Slice6FiniteCampaignLedger ledger = OpenLedger(campaignManifestPath, campaignManifestSha256,
            campaignReviewedCandidate, credentialManifestPath, credentialManifestSha256, ledgerPath, now);
        evidencePath = Path.GetFullPath(evidencePath);
        byte[] evidenceBytes = File.ReadAllBytes(evidencePath);
        string evidenceSha = Convert.ToHexStringLower(SHA256.HashData(evidenceBytes));
        using JsonDocument evidence = JsonDocument.Parse(evidenceBytes);
        JsonElement root = evidence.RootElement;
        string[] evidenceProperties = ["schema", "status", "campaign_id", "campaign_manifest_sha256",
            "stage_manifest_id", "stage_manifest_sha256", "stage", "canonical_request_sha256",
            "predecessor_evidence_id", "predecessor_evidence_sha256", "safety_identifier_projection",
            "provider_state", "http_status", "provider_response_id", "client_request_id",
            "provider_request_id", "requested_model", "returned_model", "requested_service_tier",
            "returned_service_tier", "reasoning_context", "reasoning_mode", "prompt_cache_mode",
            "provider_send_count", "dns_resolution_count", "retry_permitted",
            "input_tokens", "output_tokens", "raw_response_bytes", "calculated_nano_usd",
            "usage", "rate_facts",
            "credential_reads", "credential_frees", "credential_writes", "credential_deletes",
            "cumulative_credential_calls", "retained_artifacts", "authoritative_persistence",
            "validation_package", "semantic_validation"];
        if (!root.EnumerateObject().Select(property => property.Name).SequenceEqual(evidenceProperties,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException("Campaign stage evidence has unknown, missing, duplicate, or reordered properties.");
        }
        string evidenceId = "campaign-stage-evidence-" + (int)stage;
        M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(stage);
        JsonElement native = root.GetProperty("cumulative_credential_calls");
        if (!native.EnumerateObject().Select(property => property.Name).SequenceEqual(
                ["CredWriteW", "CredReadW", "CredDeleteW", "CredFree", "total"], StringComparer.Ordinal))
        {
            throw new InvalidDataException("Campaign stage evidence native vector is not recursively closed.");
        }
        IReadOnlyList<M1Slice6CampaignLedgerEntry> entries = ledger.Entries;
        int reservationIndex = -1;
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index].State == M1Slice6CampaignState.StageReserved && entries[index].Stage == stage)
            {
                reservationIndex = index;
                break;
            }
        }
        if (reservationIndex <= 0)
        {
            throw new InvalidDataException("Campaign stage evidence has no exact reservation predecessor.");
        }
        M1Slice6CampaignLedgerEntry reservation = entries[reservationIndex];
        M1Slice6CampaignLedgerEntry predecessor = entries[reservationIndex - 1];
        byte[] stageManifestBytes = File.ReadAllBytes(Path.GetFullPath(stageManifestPath));
        using JsonDocument stageManifest = JsonDocument.Parse(stageManifestBytes);
        JsonElement stageManifestRoot = stageManifest.RootElement;
        string stageManifestSha = Convert.ToHexStringLower(SHA256.HashData(stageManifestBytes));
        long observedInput = root.GetProperty("input_tokens").GetInt64();
        long observedOutput = root.GetProperty("output_tokens").GetInt64();
        long observedRaw = root.GetProperty("raw_response_bytes").GetInt64();
        long observedCost = root.GetProperty("calculated_nano_usd").GetInt64();
        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(credentialManifestPath);
        using JsonDocument credentialManifest = JsonDocument.Parse(
            File.ReadAllBytes(Path.GetFullPath(credentialManifestPath)));
        string authoritativeStateRoot = Path.GetFullPath(Path.Combine(repository,
            credentialManifest.RootElement.GetProperty("durable_state")
                .GetProperty("product_state_root_relative").GetString()!
                .Replace('/', Path.DirectorySeparatorChar)));
        OpenAiResponsesResult replayed = ValidateRetainedStageEvidence(root, evidencePath,
            authoritativeStateRoot, stageManifestRoot,
            ledger.Current.Identity.CredentialTargetFingerprintSha256);
        ValidateSemanticEvidence(root.GetProperty("validation_package"),
            root.GetProperty("semantic_validation"), stageManifestRoot.GetProperty("validation_package"), stage);
        if (root.GetProperty("schema").GetString() != "infinium.m1-s6.campaign-stage-evidence/v1"
            || root.GetProperty("status").GetString() != "independent-review-pending"
            || root.GetProperty("campaign_id").GetString() != ledger.Current.Identity.CampaignId
            || root.GetProperty("campaign_manifest_sha256").GetString() != campaignManifestSha256
            || root.GetProperty("stage").GetString() != stage.ToString()
            || ledger.Current.State != M1Slice6CampaignState.StageEvidenceHandoff
            || ledger.Current.Stage != stage || ledger.Current.EvidenceId != evidenceId
            || ledger.Current.EvidenceSha256 != evidenceSha
            || root.GetProperty("stage_manifest_id").GetString() != reservation.RequestManifestId
            || root.GetProperty("stage_manifest_sha256").GetString() != reservation.RequestManifestSha256
            || stageManifestSha != reservation.RequestManifestSha256
            || stageManifestRoot.GetProperty("manifest_id").GetString() != reservation.RequestManifestId
            || root.GetProperty("canonical_request_sha256").GetString()
                != stageManifestRoot.GetProperty("canonical_request").GetProperty("sha256").GetString()
            || root.GetProperty("predecessor_evidence_id").GetString() != predecessor.EvidenceId
            || root.GetProperty("predecessor_evidence_sha256").GetString() != predecessor.EvidenceSha256
            || root.GetProperty("safety_identifier_projection").GetString()
                != ledger.Current.SafetyIdentifierProjection
            || root.GetProperty("provider_state").GetString() != "Completed"
            || root.GetProperty("http_status").GetInt32() is < 200 or > 299
            || root.GetProperty("provider_response_id").GetString() != replayed.ProviderResponseId
            || root.GetProperty("client_request_id").GetString() != replayed.ClientRequestId
            || root.GetProperty("provider_request_id").GetString() != replayed.ProviderRequestId
            || root.GetProperty("requested_model").GetString() != OpenAiResponsesCanonicalSerializer.Model
            || root.GetProperty("returned_model").GetString() != replayed.ReturnedModel
            || root.GetProperty("requested_service_tier").GetString() != OpenAiResponsesCanonicalSerializer.ServiceTier
            || root.GetProperty("returned_service_tier").GetString() != replayed.ReturnedServiceTier
            || root.GetProperty("reasoning_context").GetString() != "current_turn"
            || root.GetProperty("reasoning_mode").GetString() != "standard"
            || root.GetProperty("prompt_cache_mode").GetString() != "explicit"
            || root.GetProperty("provider_send_count").GetInt32() != 1
            || root.GetProperty("dns_resolution_count").GetInt32() != 1
            || root.GetProperty("retry_permitted").GetBoolean()
            || observedInput < 0 || observedInput > limits.MaximumInputTokens
            || observedOutput < 0 || observedOutput > limits.MaximumOutputTokens
            || observedRaw <= 0 || observedRaw > limits.MaximumRawResponseBytes
            || observedCost < 0 || observedCost > limits.MaximumNanoUsd
            || observedInput != ledger.Current.ObservedInputTokens - predecessor.ObservedInputTokens
            || observedOutput != ledger.Current.ObservedOutputTokens - predecessor.ObservedOutputTokens
            || observedRaw != ledger.Current.ObservedRawResponseBytes - predecessor.ObservedRawResponseBytes
            || observedCost != ledger.Current.SettledNanoUsd - predecessor.SettledNanoUsd
            || root.GetProperty("credential_reads").GetInt32() != 1
            || root.GetProperty("credential_frees").GetInt32() != 1
            || root.GetProperty("credential_writes").GetInt32() != 0
            || root.GetProperty("credential_deletes").GetInt32() != 0
            || native.GetProperty("CredWriteW").GetInt64() != ledger.Current.NativeEnvelope.CredWriteW
            || native.GetProperty("CredReadW").GetInt64() != ledger.Current.NativeEnvelope.CredReadW
            || native.GetProperty("CredDeleteW").GetInt64() != ledger.Current.NativeEnvelope.CredDeleteW
            || native.GetProperty("CredFree").GetInt64() != ledger.Current.NativeEnvelope.CredFree
            || native.GetProperty("total").GetInt64() != ledger.Current.NativeEnvelope.Total)
        {
            throw new InvalidDataException("Campaign stage evidence acceptance has stale or changed bytes.");
        }
        string marker = "M1_S6_CAMPAIGN_STAGE_EVIDENCE_ACCEPTANCE campaign_id="
            + ledger.Current.Identity.CampaignId + " campaign_sha256=" + campaignManifestSha256
            + " stage_manifest_id=" + root.GetProperty("stage_manifest_id").GetString()
            + " stage_manifest_sha256=" + root.GetProperty("stage_manifest_sha256").GetString()
            + " evidence_id=" + evidenceId + " sha256=" + evidenceSha
            + " verdicts=security,semantics,budget,provenance";
        if (File.ReadAllLines(Path.GetFullPath(repositoryRecordPath)).Count(line => line == marker) != 1)
        {
            throw new InvalidDataException("Campaign stage evidence lacks one exact independent acceptance marker.");
        }
        repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(repositoryRecordPath);
        const string recordRelative = "docs/plans/milestones/m1/slices/s6/record.md";
        string reviewPrefix = "M1_S6_CAMPAIGN_STAGE_REVIEW_ACCEPTANCE candidate_commit=";
        string reviewSuffix = $" campaign_id={ledger.Current.Identity.CampaignId} campaign_sha256={campaignManifestSha256}"
            + $" stage_manifest_id={reservation.RequestManifestId} sha256={reservation.RequestManifestSha256}"
            + $" predecessor_evidence_sha256={predecessor.EvidenceSha256} verdicts=security,semantics,diff";
        string[] reviewLines = File.ReadAllLines(Path.GetFullPath(repositoryRecordPath)).Where(line =>
            line.StartsWith(reviewPrefix, StringComparison.Ordinal)
            && line.EndsWith(reviewSuffix, StringComparison.Ordinal)).ToArray();
        if (reviewLines.Length != 1)
        {
            throw new InvalidDataException("Campaign stage evidence has no unique exact stage review predecessor.");
        }
        string stageReviewedCandidate = reviewLines[0][reviewPrefix.Length..^reviewSuffix.Length];
        string admissionMarker = $"M1_S6_CAMPAIGN_STAGE_ADMISSION candidate_commit={stageReviewedCandidate}"
            + $" campaign_id={ledger.Current.Identity.CampaignId} campaign_sha256={campaignManifestSha256}"
            + $" stage_manifest_id={reservation.RequestManifestId} sha256={reservation.RequestManifestSha256}"
            + $" predecessor_evidence_sha256={predecessor.EvidenceSha256}"
            + " expires_at_utc=2026-08-22T23:59:00.0000000Z";
        string admissionCommit = M1Slice6CampaignStageManifestValidator.FindUniqueMarkerCommit(
            repository, admissionMarker, recordRelative);
        _ = M1Slice6CampaignStageManifestValidator.UniqueMarkerCommit(repository, marker,
            admissionCommit, recordRelative);
        ledger.AcceptStageEvidence(stage, evidenceId, evidenceSha, now);
    }

    private static void ValidateSemanticEvidence(JsonElement package, JsonElement validation,
        JsonElement manifestPackage, M1Slice6CampaignStage stage)
    {
        RequireExactNames(package, "validation package", ["package_id", "manifest_path",
            "manifest_sha256", "product_input_path", "product_input_bytes", "product_input_sha256",
            "predecessor_manifest_path", "predecessor_manifest_bytes", "predecessor_manifest_sha256",
            "oracle_path", "oracle_sha256", "deterministic_oracle_result_sha256", "semantic_use"]);
        RequireExactNames(validation, "semantic validation", ["validation_id", "disposition",
            "proposal_count", "admission_count", "result_sha256", "source_acquisition_id",
            "source_admission_id", "admitted_artifact_id", "source_application_link_id",
            "evidence_application_link_id", "candidate_id", "hypothesis_id"]);
        if (!JsonElement.DeepEquals(package, manifestPackage))
        {
            throw new InvalidDataException("Stage evidence validation package differs from its exact admitted manifest.");
        }
        string resultSha = validation.GetProperty("result_sha256").GetString() ?? string.Empty;
        bool qualification = stage == M1Slice6CampaignStage.Qualification;
        string[] provenance = ["source_acquisition_id", "source_admission_id", "admitted_artifact_id",
            "source_application_link_id", "evidence_application_link_id", "candidate_id", "hypothesis_id"];
        if (package.GetProperty("semantic_use").GetBoolean() == qualification
            || qualification && (validation.GetProperty("validation_id").GetString() != "qualification-nonsemantic"
                || validation.GetProperty("disposition").GetString() != "not-applicable"
                || validation.GetProperty("proposal_count").GetInt32() != 0
                || validation.GetProperty("admission_count").GetInt32() != 0
                || resultSha != new string('0', 64)
                || provenance.Any(name => validation.GetProperty(name).GetString() != ""))
            || !qualification && (validation.GetProperty("validation_id").GetString() is not
                    ("infinium.host.source-claim-admission/v1" or "infinium.host.candidate-investigation-admission/v1")
                || validation.GetProperty("disposition").GetString() is not ("accepted" or "accepted-conditional"
                    or "accepted-conditional-applicability")
                || validation.GetProperty("proposal_count").GetInt32() != 1
                || validation.GetProperty("admission_count").GetInt32() != 1
                || resultSha.Length != 64 || resultSha.Any(character => !Uri.IsHexDigit(character)))
            || !qualification && provenance.Take(4).Any(name =>
                string.IsNullOrWhiteSpace(validation.GetProperty(name).GetString()))
            || stage == M1Slice6CampaignStage.SourceClaimExtraction
                && provenance.Skip(4).Any(name => validation.GetProperty(name).GetString() != "")
            || stage == M1Slice6CampaignStage.CandidateInvestigation
                && provenance.Skip(4).Any(name => string.IsNullOrWhiteSpace(validation.GetProperty(name).GetString()))
            || stage == M1Slice6CampaignStage.SourceClaimExtraction
                && validation.GetProperty("validation_id").GetString() != "infinium.host.source-claim-admission/v1"
            || stage == M1Slice6CampaignStage.CandidateInvestigation
                && validation.GetProperty("validation_id").GetString() != "infinium.host.candidate-investigation-admission/v1")
        {
            throw new InvalidDataException("Stage semantic validation is absent, stale, or promoted qualification output.");
        }
    }

    private static OpenAiResponsesResult ValidateRetainedStageEvidence(JsonElement evidence,
        string evidencePath, string authoritativeStateRoot, JsonElement stageManifest,
        string targetFingerprint)
    {
        JsonElement artifacts = evidence.GetProperty("retained_artifacts");
        RequireExactNames(artifacts, "retained artifacts",
            ["canonical_request_path", "canonical_request_sha256", "raw_response_path",
                "raw_response_sha256", "response_headers_path", "response_headers_sha256",
                "native_trace_path", "native_trace_sha256", "canary_evidence_path",
                "canary_evidence_sha256"]);
        string directory = Path.GetDirectoryName(Path.GetFullPath(evidencePath))!;
        byte[] request = ReadBoundArtifact(artifacts, directory, "canonical_request", ".json");
        byte[] raw = ReadBoundArtifact(artifacts, directory, "raw_response", ".bin");
        byte[] headers = ReadBoundArtifact(artifacts, directory, "response_headers", ".json");
        byte[] trace = ReadBoundArtifact(artifacts, directory, "native_trace", ".json");
        byte[] canaries = ReadBoundArtifact(artifacts, directory, "canary_evidence", ".json");
        string requestSha = Convert.ToHexStringLower(SHA256.HashData(request));
        if (requestSha != evidence.GetProperty("canonical_request_sha256").GetString()
            || requestSha != stageManifest.GetProperty("canonical_request").GetProperty("sha256").GetString())
        {
            throw new InvalidDataException("Retained stage request bytes differ from the exact admitted request.");
        }
        string clientRequestId = evidence.GetProperty("client_request_id").GetString()
            ?? throw new InvalidDataException("Retained stage evidence omitted its client request identity.");
        OpenAiResponsesResult replayed = OpenAiStagedResponseEnvelope.Replay(raw, headers, clientRequestId);
        if (replayed.State != ProviderResponseState.Completed || !replayed.Admitted
            || replayed.HttpStatus != evidence.GetProperty("http_status").GetInt32()
            || replayed.RawResponseBytes is null || !replayed.RawResponseBytes.AsSpan().SequenceEqual(raw)
            || replayed.DnsResolutionCount != evidence.GetProperty("dns_resolution_count").GetInt32()
            || replayed.ReturnedModel != OpenAiResponsesCanonicalSerializer.Model
            || replayed.ReturnedServiceTier != OpenAiResponsesCanonicalSerializer.ServiceTier
            || replayed.RefusalCode is not null || replayed.IncompleteReason is not null
            || replayed.ErrorCode is not null)
        {
            throw new InvalidDataException("Retained stage response does not replay to the exact completed admitted result.");
        }
        ValidateUsage(evidence.GetProperty("usage"), replayed.Usage);
        ValidateRateFacts(evidence.GetProperty("rate_facts"), replayed.RateHeaders);
        ValidateNativeTrace(trace, targetFingerprint);
        ValidateCanaryEvidence(canaries);
        ValidateAuthoritativePersistence(evidence.GetProperty("authoritative_persistence"), authoritativeStateRoot,
            raw, headers, replayed);
        return replayed;
    }

    private static void ValidateAuthoritativePersistence(JsonElement retained, string authoritativeStateRoot,
        byte[] raw, byte[] headers, OpenAiResponsesResult replayed)
    {
        RequireExactNames(retained, "authoritative persistence",
            ["authorization_id", "operation_id", "attempt_id", "request_id", "reservation_id",
                "dispatch_fence_id", "response_id", "usage_entry_id", "settlement_id", "replay_edge_id",
                "raw_response_sha256", "response_headers_sha256", "unresolved_hold", "retry_permitted"]);
        using AuthoritativeStore store = new(new StoragePaths(Path.GetFullPath(authoritativeStateRoot)));
        ProviderOperationReadModel operation = store.ReadProviderOperation(
            retained.GetProperty("operation_id").GetString()!);
        if (operation.AuthorizationId != retained.GetProperty("authorization_id").GetString()
            || operation.OperationId != retained.GetProperty("operation_id").GetString()
            || operation.ResponseId != retained.GetProperty("response_id").GetString()
            || operation.UsageEntryId != retained.GetProperty("usage_entry_id").GetString()
            || operation.SettlementId != retained.GetProperty("settlement_id").GetString()
            || operation.ReplayEdgeId != retained.GetProperty("replay_edge_id").GetString()
            || operation.ClientRequestId != retained.GetProperty("request_id").GetString()
            || operation.RawResponseBytes is null || !operation.RawResponseBytes.AsSpan().SequenceEqual(raw)
            || operation.ResponseHeadersBytes is null || !operation.ResponseHeadersBytes.AsSpan().SequenceEqual(headers)
            || operation.UnresolvedHold || retained.GetProperty("unresolved_hold").GetBoolean()
            || retained.GetProperty("retry_permitted").GetBoolean()
            || retained.GetProperty("raw_response_sha256").GetString()
                != Convert.ToHexStringLower(SHA256.HashData(raw))
            || retained.GetProperty("response_headers_sha256").GetString()
                != Convert.ToHexStringLower(SHA256.HashData(headers)))
        {
            throw new InvalidDataException("Authoritative SQLite operation/response/usage/settlement/replay evidence is stale or incomplete.");
        }
        OpenAiResponsesResult authoritativeReplay = new ProviderAccountingCoordinator(store).Replay(new(
            new OpaqueId(operation.OperationId), new OpaqueId(operation.ResponseId), NetworkPermitted: false));
        if (authoritativeReplay.ProviderResponseId != replayed.ProviderResponseId
            || authoritativeReplay.ProviderRequestId != replayed.ProviderRequestId
            || authoritativeReplay.ReturnedModel != replayed.ReturnedModel
            || authoritativeReplay.ReturnedServiceTier != replayed.ReturnedServiceTier
            || authoritativeReplay.RawResponseBytes is null
            || !authoritativeReplay.RawResponseBytes.AsSpan().SequenceEqual(raw))
        {
            throw new InvalidDataException("Authoritative SQLite replay differs from retained network-disabled evidence replay.");
        }
    }

    private static byte[] ReadBoundArtifact(JsonElement artifacts, string directory,
        string prefix, string extension)
    {
        string fileName = artifacts.GetProperty(prefix + "_path").GetString()
            ?? throw new InvalidDataException("Retained stage artifact path is absent.");
        if (fileName != Path.GetFileName(fileName)
            || !fileName.EndsWith(extension, StringComparison.Ordinal)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("Retained stage artifact path escapes its evidence directory.");
        }
        string path = Path.GetFullPath(Path.Combine(directory, fileName));
        if (Path.GetDirectoryName(path) != Path.GetFullPath(directory) || !File.Exists(path))
        {
            throw new InvalidDataException("Retained stage artifact is absent or outside its evidence directory.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        string expectedHash = artifacts.GetProperty(prefix + "_sha256").GetString() ?? string.Empty;
        if (expectedHash.Length != 64
            || expectedHash.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            || Convert.ToHexStringLower(SHA256.HashData(bytes)) != expectedHash)
        {
            throw new InvalidDataException("Retained stage artifact hash is absent, malformed, or stale.");
        }
        return bytes;
    }

    private static void ValidateUsage(JsonElement retained, ProviderUsageContract replayed)
    {
        RequireExactNames(retained, "usage", ["Availability", "DispatchCount", "InputTokens",
            "OutputTokens", "TotalTokens", "ReasoningTokens", "CacheReadTokens", "CacheWriteTokens",
            "PricedToolCalls", "CalculatedNanoUsd", "BillingAvailability", "RateAvailability",
            "CreditAvailability", "ReceiptState"]);
        foreach (string quantity in new[] { "DispatchCount", "InputTokens", "OutputTokens", "TotalTokens",
            "ReasoningTokens", "CacheReadTokens", "CacheWriteTokens", "PricedToolCalls", "CalculatedNanoUsd" })
        {
            RequireExactNames(retained.GetProperty(quantity), "usage." + quantity,
                ["Availability", "Value"]);
        }
        JsonElement expected = JsonSerializer.SerializeToElement(replayed);
        if (!JsonElement.DeepEquals(retained, expected))
        {
            throw new InvalidDataException("Retained stage usage differs from network-disabled replay.");
        }
    }

    private static void ValidateRateFacts(JsonElement retained, IReadOnlyList<OpenAiRateHeader> replayed)
    {
        if (retained.ValueKind != JsonValueKind.Array || retained.GetArrayLength() != replayed.Count)
        {
            throw new InvalidDataException("Retained stage rate facts are incomplete.");
        }
        JsonElement[] values = retained.EnumerateArray().ToArray();
        for (int index = 0; index < values.Length; index++)
        {
            RequireExactNames(values[index], "rate fact", ["name", "value"]);
            if (values[index].GetProperty("name").GetString() != replayed[index].Name
                || values[index].GetProperty("value").GetInt64() != replayed[index].Value)
            {
                throw new InvalidDataException("Retained stage rate facts differ from network-disabled replay.");
            }
        }
    }

    private static void ValidateNativeTrace(byte[] bytes, string targetFingerprint)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Array
            || document.RootElement.GetArrayLength() != 2)
        {
            throw new InvalidDataException("Retained stage native trace is not the exact two-call read/free grammar.");
        }
        JsonElement[] calls = document.RootElement.EnumerateArray().ToArray();
        foreach (JsonElement call in calls)
        {
            RequireExactNames(call, "native trace call", ["Operation", "Result", "Scenario",
                "TargetFingerprintSha256", "AllocationId", "PairedAllocationId"]);
        }
        if (calls[0].GetProperty("Operation").GetString() != "CredReadW"
            || calls[0].GetProperty("Result").GetString() != "success"
            || calls[1].GetProperty("Operation").GetString() != "CredFree"
            || calls[1].GetProperty("Result").GetString() != "released"
            || calls.Any(call => call.GetProperty("Scenario").GetString()
                != "m1-s6-campaign-provider-dispatch")
            || calls.Any(call => call.GetProperty("TargetFingerprintSha256").GetString() != targetFingerprint)
            || calls[0].GetProperty("AllocationId").GetInt64() <= 0
            || calls[0].GetProperty("PairedAllocationId").GetInt64() != 0
            || calls[1].GetProperty("AllocationId").GetInt64() != 0
            || calls[1].GetProperty("PairedAllocationId").GetInt64()
                != calls[0].GetProperty("AllocationId").GetInt64())
        {
            throw new InvalidDataException("Retained stage native trace changed operation, result, target, order, or allocation pairing.");
        }
    }

    private static void ValidateCanaryEvidence(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        RequireExactNames(root, "canary evidence",
            ["SecretMatches", "RawTargetMatches", "RawTargetEncodings", "ScannedSurfaces"]);
        string[] encodings = root.GetProperty("RawTargetEncodings").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
        JsonElement[] surfaces = root.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
        string[] names = ["private protocol request", "private protocol response", "native call trace",
            "process command line", "process environment names"];
        string[] kinds = ["private-pipe-bytes", "private-pipe-bytes", "canonical-trace-bytes",
            "captured-text", "captured-text"];
        if (root.GetProperty("SecretMatches").GetInt32() != 0
            || root.GetProperty("RawTargetMatches").GetInt32() != 0
            || !encodings.SequenceEqual(["utf-8", "utf-16le"], StringComparer.Ordinal)
            || surfaces.Length != names.Length)
        {
            throw new InvalidDataException("Retained stage canary evidence is incomplete or matched secret material.");
        }
        for (int index = 0; index < surfaces.Length; index++)
        {
            RequireExactNames(surfaces[index], "canary surface",
                ["Name", "Kind", "ByteCount", "SecretMatches", "RawTargetMatches"]);
            if (surfaces[index].GetProperty("Name").GetString() != names[index]
                || surfaces[index].GetProperty("Kind").GetString() != kinds[index]
                || surfaces[index].GetProperty("ByteCount").GetInt64() <= 0
                || surfaces[index].GetProperty("SecretMatches").GetInt32() != 0
                || surfaces[index].GetProperty("RawTargetMatches").GetInt32() != 0)
            {
                throw new InvalidDataException("Retained stage canary inventory is stale, vacuous, or matched secret material.");
            }
        }
    }

    private static void RequireExactNames(JsonElement value, string label, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException(label + " has unknown, missing, duplicate, or reordered properties.");
        }
    }

    internal static void CompleteComposedEvidence(string campaignManifestPath, string campaignManifestSha256,
        string campaignReviewedCandidate, string credentialManifestPath, string credentialManifestSha256,
        string ledgerPath, string composedEvidencePath, string repositoryRecordPath, DateTimeOffset now)
    {
        M1Slice6FiniteCampaignLedger ledger = OpenLedger(campaignManifestPath, campaignManifestSha256,
            campaignReviewedCandidate, credentialManifestPath, credentialManifestSha256, ledgerPath, now);
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(composedEvidencePath));
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        using JsonDocument composed = JsonDocument.Parse(bytes);
        JsonElement composedRoot = composed.RootElement;
        RequireExactNames(composedRoot, "campaign composed evidence",
            ["schema", "campaign_id", "campaign_manifest_sha256", "credential_manifest_id",
                "credential_manifest_sha256", "credential_profile_id", "credential_generation_id",
                "stages", "composed_validation_package", "explicit_omissions", "provider_call_count",
                "dns_resolution_count", "aggregate_request_bytes",
                "aggregate_input_tokens", "aggregate_output_tokens", "aggregate_raw_response_bytes",
                "aggregate_maximum_nano_usd", "outstanding_reserved_nano_usd", "settled_nano_usd",
                "cumulative_credential_calls",
                "prohibited_effects", "fourth_call_observed"]);
        if (composedRoot.GetProperty("schema").GetString()
                != "infinium.m1-s6.campaign-composed-evidence/v1"
            || composedRoot.GetProperty("campaign_id").GetString() != ledger.Current.Identity.CampaignId
            || composedRoot.GetProperty("campaign_manifest_sha256").GetString() != campaignManifestSha256
            || composedRoot.GetProperty("credential_manifest_id").GetString()
                != ledger.Current.Identity.CredentialManifestId
            || composedRoot.GetProperty("credential_manifest_sha256").GetString()
                != ledger.Current.Identity.CredentialManifestSha256
            || composedRoot.GetProperty("credential_profile_id").GetString()
                != ledger.Current.Identity.CredentialProfileId
            || composedRoot.GetProperty("credential_generation_id").GetString()
                != ledger.Current.Identity.CredentialGenerationId
            || composedRoot.GetProperty("provider_call_count").GetInt32() != 3
            || composedRoot.GetProperty("dns_resolution_count").GetInt32() != 3
            || composedRoot.GetProperty("aggregate_request_bytes").GetInt64()
                != ledger.Current.AggregateRequestBytes
            || composedRoot.GetProperty("aggregate_input_tokens").GetInt64()
                != ledger.Current.AggregateInputTokens
            || composedRoot.GetProperty("aggregate_output_tokens").GetInt64()
                != ledger.Current.AggregateOutputTokens
            || composedRoot.GetProperty("aggregate_raw_response_bytes").GetInt64()
                != ledger.Current.AggregateRawResponseBytes
            || composedRoot.GetProperty("aggregate_maximum_nano_usd").GetInt64()
                != M1Slice6FiniteCampaignLedger.AggregateMaximumNanoUsd
            || composedRoot.GetProperty("outstanding_reserved_nano_usd").GetInt64()
                != ledger.Current.ReservedNanoUsd
            || composedRoot.GetProperty("settled_nano_usd").GetInt64() != ledger.Current.SettledNanoUsd
            || composedRoot.GetProperty("fourth_call_observed").GetBoolean())
        {
            throw new InvalidDataException("Campaign composed evidence is incomplete or permits a fourth call.");
        }
        JsonElement[] stages = composedRoot.GetProperty("stages").EnumerateArray().ToArray();
        if (stages.Length != 3)
        {
            throw new InvalidDataException("Campaign composed evidence does not bind exactly three stage results.");
        }
        ValidateComposedStage(stages[0], M1Slice6CampaignStage.Qualification,
            "M1-PLAT-PROVIDER-CAPABILITY-VAL-v1", false, "qualification-nonsemantic", 0);
        ValidateComposedStage(stages[1], M1Slice6CampaignStage.SourceClaimExtraction,
            "LLM-CLAIM-LIVE-VAL", true, "infinium.host.source-claim-admission/v1", 1);
        ValidateComposedStage(stages[2], M1Slice6CampaignStage.CandidateInvestigation,
            "LLM-INVESTIGATE-LIVE-VAL", true, "infinium.host.candidate-investigation-admission/v1", 1);
        ValidateComposedSemanticSuccessor(stages[1], stages[2], credentialManifestPath);
        JsonElement composedPackage = composedRoot.GetProperty("composed_validation_package");
        RequireExactNames(composedPackage, "composed provenance validation package",
            ["package_id", "manifest_path", "manifest_sha256", "oracle_path", "oracle_sha256", "semantic_use"]);
        string[] omissions = composedRoot.GetProperty("explicit_omissions").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        if (composedPackage.GetProperty("package_id").GetString() != "PROV-LIVE-COMPOSED-VAL"
            || composedPackage.GetProperty("manifest_path").GetString()
                != "fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL/public-manifest.json"
            || composedPackage.GetProperty("manifest_sha256").GetString()
                != "da6b6b05456a2ed956393c3a0f7c7d470a947adba8e991cae72e9dd7f28250c8"
            || composedPackage.GetProperty("oracle_path").GetString()
                != "fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL/oracle.v1.json"
            || composedPackage.GetProperty("oracle_sha256").GetString()
                != "2b8174e58cfdda414883aff245b8f9087647d05a9d4ca6c11e7b0ac076634f8e"
            || !composedPackage.GetProperty("semantic_use").GetBoolean()
            || !omissions.SequenceEqual(["credential-secret", "hosted-search", "nexus", "private-fixture"],
                StringComparer.Ordinal))
        {
            throw new InvalidDataException("Composed provenance lacks its frozen oracle or exact omission inventory.");
        }
        if (stages.Any(stage => stage.GetProperty("credential_profile_id").GetString()
                    != ledger.Current.Identity.CredentialProfileId
                || stage.GetProperty("credential_generation_id").GetString()
                    != ledger.Current.Identity.CredentialGenerationId)
            || stages.Select(stage => stage.GetProperty("stage_manifest_sha256").GetString())
                .Distinct(StringComparer.Ordinal).Count() != 3
            || stages.Select(stage => stage.GetProperty("evidence_sha256").GetString())
                .Distinct(StringComparer.Ordinal).Count() != 3
            || stages.Select(stage => stage.GetProperty("operation_id").GetString())
                .Distinct(StringComparer.Ordinal).Count() != 3
            || stages.Select(stage => stage.GetProperty("response_id").GetString())
                .Distinct(StringComparer.Ordinal).Count() != 3)
        {
            throw new InvalidDataException("Campaign composed evidence reuses or changes a stage or credential identity.");
        }
        JsonElement native = composedRoot.GetProperty("cumulative_credential_calls");
        RequireExactNames(native, "campaign composed native envelope",
            ["CredWriteW", "CredReadW", "CredDeleteW", "CredFree", "total"]);
        JsonElement prohibited = composedRoot.GetProperty("prohibited_effects");
        RequireExactNames(prohibited, "campaign composed prohibited effects",
            ["fourth_provider_call", "automatic_retry", "credential_delete", "hosted_search",
                "private_fixture_access", "secret_retained"]);
        if (native.GetProperty("CredWriteW").GetInt32() != 1
            || native.GetProperty("CredReadW").GetInt32() != 5
            || native.GetProperty("CredDeleteW").GetInt32() != 0
            || native.GetProperty("CredFree").GetInt32() != 4
            || native.GetProperty("total").GetInt32() != 10
            || prohibited.EnumerateObject().Any(property => property.Value.GetBoolean()))
        {
            throw new InvalidDataException("Campaign composed evidence changed the exact native or prohibited-effect envelope.");
        }
        const string evidenceId = "campaign-composed-evidence";
        string marker = "M1_S6_CAMPAIGN_COMPOSED_EVIDENCE_ACCEPTANCE campaign_id="
            + ledger.Current.Identity.CampaignId + " campaign_sha256=" + campaignManifestSha256
            + " evidence_id=" + evidenceId + " sha256=" + sha
            + " verdicts=security,semantics,budget,provenance,diff";
        if (File.ReadAllLines(Path.GetFullPath(repositoryRecordPath)).Count(line => line == marker) != 1)
        {
            throw new InvalidDataException("Campaign composed evidence lacks one exact acceptance marker.");
        }
        if (ledger.Current.State != M1Slice6CampaignState.StageAccepted
            || ledger.Current.Stage != M1Slice6CampaignStage.CandidateInvestigation
            || ledger.Current.ProviderCallCount != 3 || ledger.Current.DnsResolutionCount != 3)
        {
            throw new InvalidDataException("Campaign composed evidence has no exact three-stage accepted predecessor.");
        }
        string stageEvidenceMarker = "M1_S6_CAMPAIGN_STAGE_EVIDENCE_ACCEPTANCE campaign_id="
            + ledger.Current.Identity.CampaignId + " campaign_sha256=" + campaignManifestSha256
            + " stage_manifest_id=" + ledger.Current.RequestManifestId
            + " stage_manifest_sha256=" + ledger.Current.RequestManifestSha256
            + " evidence_id=" + ledger.Current.EvidenceId + " sha256=" + ledger.Current.EvidenceSha256
            + " verdicts=security,semantics,budget,provenance";
        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(repositoryRecordPath);
        const string recordRelative = "docs/plans/milestones/m1/slices/s6/record.md";
        string stageEvidenceCommit = M1Slice6CampaignStageManifestValidator.FindUniqueMarkerCommit(
            repository, stageEvidenceMarker, recordRelative);
        _ = M1Slice6CampaignStageManifestValidator.UniqueMarkerCommit(repository, marker,
            stageEvidenceCommit, recordRelative);
        ledger.CompleteComposedEvidence(evidenceId, sha, now);
    }

    private static void ValidateComposedSemanticSuccessor(JsonElement wp10, JsonElement wp11,
        string credentialManifestPath)
    {
        JsonElement source = wp10.GetProperty("semantic_validation");
        JsonElement candidate = wp11.GetProperty("semantic_validation");
        string[] shared = ["source_acquisition_id", "source_admission_id", "admitted_artifact_id",
            "source_application_link_id"];
        string[] applied = ["evidence_application_link_id", "candidate_id", "hypothesis_id"];
        if (shared.Any(name => string.IsNullOrWhiteSpace(source.GetProperty(name).GetString())
                || source.GetProperty(name).GetString() != candidate.GetProperty(name).GetString())
            || applied.Any(name => string.IsNullOrWhiteSpace(candidate.GetProperty(name).GetString())))
        {
            throw new InvalidDataException(
                "Composed provenance does not bind the admitted WP10 artifact to the WP11 application graph.");
        }
        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(credentialManifestPath);
        using JsonDocument credential = JsonDocument.Parse(File.ReadAllBytes(credentialManifestPath));
        string stateRoot = Path.GetFullPath(Path.Combine(repository,
            credential.RootElement.GetProperty("durable_state").GetProperty("product_state_root_relative")
                .GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        using AuthoritativeStore store = new(new StoragePaths(stateRoot));
        SourceClaimApplicationReadModel application = store.ReadSourceClaimApplicationLinks(
            source.GetProperty("source_acquisition_id").GetString()!).SingleOrDefault(item =>
                item.ApplicationLinkId == source.GetProperty("source_application_link_id").GetString()
                && item.AdmissionId == source.GetProperty("source_admission_id").GetString())
            ?? throw new InvalidDataException("Composed provenance has no exact authoritative WP10 application.");
        if (application.AdmittedArtifactId != source.GetProperty("admitted_artifact_id").GetString())
        {
            throw new InvalidDataException("Composed provenance changed the admitted WP10 artifact identity.");
        }
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM evidence_application_links WHERE evidence_application_link_id=$evidence; "
            + "SELECT COUNT(*) FROM analysis_candidates WHERE candidate_id=$candidate; "
            + "SELECT COUNT(*) FROM analysis_hypotheses WHERE hypothesis_id=$hypothesis AND candidate_id=$candidate;";
        command.Parameters.AddWithValue("$evidence", candidate.GetProperty("evidence_application_link_id").GetString()!);
        command.Parameters.AddWithValue("$candidate", candidate.GetProperty("candidate_id").GetString()!);
        command.Parameters.AddWithValue("$hypothesis", candidate.GetProperty("hypothesis_id").GetString()!);
        using SqliteDataReader reader = command.ExecuteReader();
        long evidenceCount = reader.Read() ? reader.GetInt64(0) : 0;
        _ = reader.NextResult();
        long candidateCount = reader.Read() ? reader.GetInt64(0) : 0;
        _ = reader.NextResult();
        long hypothesisCount = reader.Read() ? reader.GetInt64(0) : 0;
        if (evidenceCount != 1 || candidateCount != 1 || hypothesisCount != 1)
        {
            throw new InvalidDataException("Composed provenance has a missing or ambiguous WP11 application graph.");
        }
    }

    private static void ValidateComposedStage(JsonElement stage, M1Slice6CampaignStage expectedStage,
        string expectedPackage, bool semanticUse, string expectedValidation, int expectedAdmissions)
    {
        RequireExactNames(stage, "campaign composed stage",
            ["ordinal", "stage", "stage_manifest_id", "stage_manifest_sha256", "evidence_id",
                "evidence_sha256", "canonical_request_sha256", "raw_response_sha256",
                "response_headers_sha256", "provider_response_id", "client_request_id",
                "provider_request_id", "operation_id", "reservation_id", "response_id",
                "usage_entry_id", "settlement_id", "replay_edge_id", "credential_profile_id",
                "credential_generation_id", "validation_package", "semantic_validation"]);
        JsonElement package = stage.GetProperty("validation_package");
        JsonElement semantic = stage.GetProperty("semantic_validation");
        RequireExactNames(package, "campaign composed validation package",
            ["package_id", "manifest_path", "manifest_sha256", "product_input_path",
                "product_input_bytes", "product_input_sha256", "predecessor_manifest_path",
                "predecessor_manifest_bytes", "predecessor_manifest_sha256", "oracle_path",
                "oracle_sha256", "deterministic_oracle_result_sha256", "semantic_use"]);
        RequireExactNames(semantic, "campaign composed semantic validation",
            ["validation_id", "disposition", "proposal_count", "admission_count", "result_sha256",
                "source_acquisition_id", "source_admission_id", "admitted_artifact_id",
                "source_application_link_id", "evidence_application_link_id", "candidate_id",
                "hypothesis_id"]);
        foreach (string property in new[] { "stage_manifest_sha256", "evidence_sha256",
            "canonical_request_sha256", "raw_response_sha256", "response_headers_sha256" })
        {
            string value = stage.GetProperty(property).GetString() ?? string.Empty;
            if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9')
                    and not (>= 'a' and <= 'f')))
            {
                throw new InvalidDataException("Campaign composed stage has a malformed retained hash.");
            }
        }
        foreach (string property in new[] { "stage_manifest_id", "evidence_id", "provider_response_id",
            "client_request_id", "provider_request_id", "operation_id", "reservation_id", "response_id",
            "usage_entry_id", "settlement_id", "replay_edge_id", "credential_profile_id",
            "credential_generation_id" })
        {
            if (string.IsNullOrWhiteSpace(stage.GetProperty(property).GetString()))
            {
                throw new InvalidDataException("Campaign composed stage has an absent exact identity.");
            }
        }
        string semanticResult = semantic.GetProperty("result_sha256").GetString() ?? string.Empty;
        string expectedOracleResult = expectedStage switch
        {
            M1Slice6CampaignStage.Qualification => new string('0', 64),
            M1Slice6CampaignStage.SourceClaimExtraction =>
                "122aa491aca8fdb23e2ca9405bd0651b2c69c4e372fbc378584ce779520f0c3c",
            _ => "bb5ac6574182a5335f445d75df88e4e9514794da5756c0657f27b741711bdb14",
        };
        if (stage.GetProperty("ordinal").GetInt32() != (int)expectedStage
            || stage.GetProperty("stage").GetString() != expectedStage.ToString()
            || stage.GetProperty("stage_manifest_id").GetString()
                != "infinium.m1-s6.campaign-stage/" + expectedStage
            || stage.GetProperty("evidence_id").GetString()
                != "campaign-stage-evidence-" + (int)expectedStage
            || package.GetProperty("package_id").GetString() != expectedPackage
            || package.GetProperty("deterministic_oracle_result_sha256").GetString()
                != expectedOracleResult
            || package.GetProperty("semantic_use").GetBoolean() != semanticUse
            || semantic.GetProperty("validation_id").GetString() != expectedValidation
            || semantic.GetProperty("proposal_count").GetInt32() != expectedAdmissions
            || semantic.GetProperty("admission_count").GetInt32() != expectedAdmissions
            || expectedAdmissions == 0 && (semantic.GetProperty("disposition").GetString() != "not-applicable"
                || semanticResult != new string('0', 64))
            || expectedAdmissions == 1 && (semantic.GetProperty("disposition").GetString() is not
                    ("accepted" or "accepted-conditional" or "accepted-conditional-applicability")
                || semanticResult.Length != 64))
        {
            throw new InvalidDataException("Campaign composed stage has stale stage, package, or semantic identities.");
        }
    }

    private static M1Slice6FiniteCampaignLedger OpenLedger(string campaignManifestPath,
        string campaignManifestSha256, string campaignReviewedCandidate, string credentialManifestPath,
        string credentialManifestSha256, string ledgerPath, DateTimeOffset now)
    {
        byte[] campaignBytes = File.ReadAllBytes(Path.GetFullPath(campaignManifestPath));
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        if (Convert.ToHexStringLower(SHA256.HashData(campaignBytes)) != campaignManifestSha256
            || Convert.ToHexStringLower(SHA256.HashData(credentialBytes)) != credentialManifestSha256)
        {
            throw new InvalidDataException("Campaign evidence transition has stale authority bytes.");
        }
        using JsonDocument campaign = JsonDocument.Parse(campaignBytes);
        using JsonDocument credential = JsonDocument.Parse(credentialBytes);
        JsonElement campaignRoot = campaign.RootElement;
        JsonElement credentialRoot = credential.RootElement;
        JsonElement profile = credentialRoot.GetProperty("profile");
        M1Slice6CampaignIdentity identity = new(campaignRoot.GetProperty("campaign_id").GetString()!,
            campaignManifestSha256, campaignRoot.GetProperty("authority_source").GetProperty("attachment_sha256").GetString()!,
            campaignReviewedCandidate, credentialRoot.GetProperty("manifest_id").GetString()!, credentialManifestSha256,
            profile.GetProperty("access_profile_id").GetString()!, profile.GetProperty("generation_id").GetString()!,
            profile.GetProperty("target_fingerprint_sha256").GetString()!);
        return new(Path.GetFullPath(ledgerPath), identity,
            DateTimeOffset.Parse(campaignRoot.GetProperty("expires_at_utc").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(credentialRoot.GetProperty("expires_at_utc").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture), now);
    }
}
