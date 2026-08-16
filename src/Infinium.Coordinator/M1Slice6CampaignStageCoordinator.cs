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

namespace Infinium.Coordinator;

public sealed record M1Slice6CampaignStageAuthority(
    string ManifestId, string ManifestSha256, M1Slice6CampaignStage Stage,
    string WorkPackage, ProviderOperationKind Operation, string ReviewCandidateCommit,
    string PredecessorEvidenceId, string PredecessorEvidenceSha256,
    string CanonicalRequestPath, string CanonicalRequestSha256, byte[] CanonicalRequest,
    long ProvedInputTokens, M1Slice6CampaignStageLimits Limits, string SafetyIdentifierProjection);

public sealed record M1Slice6CampaignCredentialReadReceipt(
    string ProfileId, string GenerationId, string TargetFingerprintSha256,
    int CredReadW, int CredFree, int CredWriteW, int CredDeleteW,
    string ReadResult, string FreeResult);

public sealed record M1Slice6CampaignStageBoundaryResult(
    OpenAiResponsesResult Response, M1Slice6CampaignCredentialReadReceipt CredentialRead,
    string CanonicalRequestSha256, string SafetyIdentifierProjection, int DnsResolutionCount,
    DateTimeOffset CompletedAtUtc);

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
        if (root.GetProperty("manifest_id").GetString() != credentialManifestId
            || root.GetProperty("status").GetString() != "ready-for-owner-acceptance")
        {
            throw new InvalidDataException("Campaign provider boundary requires the exact production credential identity.");
        }
        profileId = profile.GetProperty("access_profile_id").GetString()!;
        generationId = profile.GetProperty("generation_id").GetString()!;
        targetFingerprint = profile.GetProperty("target_fingerprint_sha256").GetString()!;
        OneShotCredentialHelperLauncher launcher = OneShotCredentialHelperLauncher.CreateWp9CampaignProvider(
            helperBinary, helperSha256, credentialManifestPath, credentialManifestSha256, credentialManifestId);
        executeHelper = (bootstrap, assignment, final, timeout, now, cancellationToken) =>
            launcher.ExecuteAsync(bootstrap, assignment, final, timeout, now, cancellationToken);
    }

    internal M1Slice6CampaignProductionStageBoundary(string profileId, string generationId,
        string targetFingerprint,
        Func<HelperPrivateFrameV2, HelperPrivateFrameV2, HelperPrivateFrameV2,
            TimeSpan, DateTimeOffset, CancellationToken, Task<HelperProcessReceipt>> executeHelper)
    {
        this.profileId = profileId;
        this.generationId = generationId;
        this.targetFingerprint = targetFingerprint;
        this.executeHelper = executeHelper ?? throw new ArgumentNullException(nameof(executeHelper));
    }

    public async Task<M1Slice6CampaignStageBoundaryResult> ExecuteOnceAsync(
        M1Slice6CampaignStageAuthority authority,
        Func<DateTimeOffset, CancellationToken, Task> possibleStart,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string suffix = Interlocked.Increment(ref sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string commandId = "m1-s6-campaign/" + (int)authority.Stage + "/" + suffix;
        string requestId = "m1-s6-campaign-request-" + (int)authority.Stage + "-" + suffix;
        string operationId = "m1-s6-campaign-operation-" + (int)authority.Stage;
        string attemptId = operationId + "/attempt-1";
        string reservationId = operationId + "/reservation-1";
        string dispatchId = operationId + "/dispatch-1";
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
        Instant deadline = Instant(now.AddMilliseconds(authority.Limits.DeadlineMilliseconds));
        HelperPrivateFrameV2 bootstrap = new()
        {
            Sequence = 1,
            ProtocolFingerprintSha256 = Fingerprint(),
            Bootstrap = new()
            {
                CoordinatorFencingEpoch = 1,
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
            ConfirmedAt = Instant(now),
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
                AccountIdentityId = new() { Value = "unavailable" },
                BillingScopeIdentityId = new() { Value = "unavailable" },
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
                CoordinatorFencingEpoch = 1,
                AccessProfileId = new() { Value = profileId },
                GenerationId = new() { Value = generationId },
                RevocationEpoch = 0,
                ReservationGroupId = new() { Value = reservationId },
                CanonicalRequest = canonical.Clone(),
                AuthorizedOnce = true,
                Disposition = DispatchDispositionV2.Authorized,
                AccountIdentityId = new() { Value = "unavailable" },
                BillingScopeIdentityId = new() { Value = "unavailable" },
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
            authority.SafetyIdentifierProjection, 1, DateTimeOffset.UtcNow);
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
            "safety_identifier", "execution");
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
        if (requireAdmitted && (!Hex(closeReady, 40)
                || resolution != "exact-clean-committed-two-file-stage-candidate")
            || !requireAdmitted && (closeReady != "pending" || resolution != "pending"))
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
        Exact(request, "path", "sha256", "bytes", "input_bound_policy_id", "input_bound_policy_version",
            "proved_input_tokens", "maximum_output_tokens");
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
        if (request.GetProperty("sha256").GetString() != requestSha
            || request.GetProperty("bytes").GetInt64() != canonical.LongLength
            || request.GetProperty("input_bound_policy_id").GetString() != OpenAiResponsesCanonicalSerializer.InputBoundPolicyId
            || request.GetProperty("input_bound_policy_version").GetString() != OpenAiResponsesCanonicalSerializer.InputBoundPolicyVersion)
        {
            throw new InvalidDataException("The canonical request proof is stale.");
        }

        JsonElement limitsNode = root.GetProperty("limits");
        Exact(limitsNode, "maximum_request_bytes", "maximum_input_tokens", "maximum_output_tokens",
            "maximum_raw_response_bytes", "maximum_nano_usd", "deadline_milliseconds");
        M1Slice6CampaignStageLimits exactLimits = M1Slice6CampaignStageLimits.For(stage);
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

        JsonElement execution = root.GetProperty("execution");
        Exact(execution, "provider_request_permitted", "requires_exact_review_marker",
            "requires_exact_admission_marker", "automatic_retry", "fourth_call_permitted");
        if (execution.GetProperty("provider_request_permitted").GetBoolean() != requireAdmitted
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
            canonical, provedInput, exactLimits, projection);
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
        string[] commits = RunGit(repository, "log", "--format=%H", "--fixed-strings", "-S", marker,
            "--", recordRelative).Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (commits.Length != 1) { throw new InvalidDataException("A stage marker has no unique committed transition."); }
        string commit = commits[0];
        if (RunGit(repository, "rev-parse", commit + "^").Output.Trim() != expectedParent)
        {
            throw new InvalidDataException("A stage marker transition has a stale predecessor.");
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
        string[] commits = RunGit(repository, "log", "--format=%H", "--fixed-strings", "-S", marker,
            "--", recordRelative).Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (commits.Length != 1)
        {
            throw new InvalidDataException("A campaign marker has no unique committed transition.");
        }
        return commits[0];
    }

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

    public M1Slice6CampaignStageCoordinator(M1Slice6FiniteCampaignLedger ledger,
        ProductUserSafetyIdentifierStateStore safetyState,
        IM1Slice6CampaignStageExecutionBoundary boundary)
    {
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.safetyState = safetyState ?? throw new ArgumentNullException(nameof(safetyState));
        this.boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
    }

    public async Task<string> ExecuteOneShotAsync(string manifestPath, string manifestSha256,
        string evidencePath, DateTimeOffset now, CancellationToken cancellationToken)
    {
        M1Slice6CampaignStageAuthority authority = M1Slice6CampaignStageManifestValidator.LoadAndValidate(
            manifestPath, manifestSha256, ledger, requireAdmitted: true);
        ledger.ReserveStage(authority.Stage, new(authority.ManifestId, authority.ManifestSha256,
            authority.CanonicalRequest.LongLength, authority.ProvedInputTokens,
            authority.Limits.MaximumOutputTokens, authority.Limits.MaximumRawResponseBytes,
            authority.Limits.MaximumNanoUsd), now);
        int possibleStartCount = 0;
        M1Slice6CampaignCredentialReadReceipt? readReceipt = null;
        M1Slice6CampaignStageBoundaryResult result;
        try
        {
            result = await boundary.ExecuteOnceAsync(authority, (possibleStartAt, _) =>
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
            || result.SafetyIdentifierProjection != authority.SafetyIdentifierProjection)
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
        M1Slice6CampaignNativeEnvelope native = ledger.CurrentNativeEnvelope;
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
            provider_send_count = response.SendCount,
            dns_resolution_count = result.DnsResolutionCount,
            retry_permitted = response.RetryPermitted,
            input_tokens = input,
            output_tokens = output,
            raw_response_bytes = raw,
            calculated_nano_usd = cost,
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
            retained_response_sha256 = Convert.ToHexStringLower(SHA256.HashData(response.RawResponseBytes)),
        };
        byte[] evidenceBytes = JsonSerializer.SerializeToUtf8Bytes(evidence, EvidenceJson);
        evidencePath = Path.GetFullPath(evidencePath);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        using (FileStream stream = new(evidencePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough))
        {
            stream.Write(evidenceBytes);
            stream.WriteByte((byte)'\n');
            stream.Flush(flushToDisk: true);
        }
        string evidenceSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(evidencePath)));
        ledger.RecordStageEvidenceHandoff(authority.Stage, "campaign-stage-evidence-" + (int)authority.Stage,
            evidenceSha, input, output, raw, cost, result.CompletedAtUtc);
        return evidenceSha;
    }

    public void AcceptEvidence(M1Slice6CampaignStage stage, string evidenceId, string evidenceSha256,
        DateTimeOffset now) => ledger.AcceptStageEvidence(stage, evidenceId, evidenceSha256, now);

    public void CompleteComposedEvidence(string evidenceId, string evidenceSha256, DateTimeOffset now) =>
        ledger.CompleteComposedEvidence(evidenceId, evidenceSha256, now);

    private static long Required(ProviderQuantityContract quantity, string name) =>
        quantity.Availability == ProviderAvailabilityState.Available && quantity.Value is >= 0
            ? quantity.Value.Value : throw new InvalidDataException($"Exact {name} are unavailable.");
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
        M1Slice6CampaignProductionStageBoundary boundary = new(helperBinary, helperSha256,
            credentialManifestPath, credentialManifestSha256, identity.CredentialManifestId);
        M1Slice6CampaignStageCoordinator coordinator = new(ledger, safety, boundary);
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
            "provider_state", "provider_send_count", "dns_resolution_count", "retry_permitted",
            "input_tokens", "output_tokens", "raw_response_bytes", "calculated_nano_usd",
            "credential_reads", "credential_frees", "credential_writes", "credential_deletes",
            "cumulative_credential_calls", "retained_response_sha256"];
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
            || native.GetProperty("CredWriteW").GetInt32() != 1
            || native.GetProperty("CredReadW").GetInt32() != 2 + (int)stage
            || native.GetProperty("CredDeleteW").GetInt32() != 0
            || native.GetProperty("CredFree").GetInt32() != 1 + (int)stage
            || native.GetProperty("total").GetInt32() != 4 + 2 * (int)stage)
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
        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(repositoryRecordPath);
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
        if (!composedRoot.EnumerateObject().Select(property => property.Name).SequenceEqual(
                ["schema", "provider_call_count", "fourth_call_observed"], StringComparer.Ordinal)
            || composedRoot.GetProperty("schema").GetString()
                != "infinium.m1-s6.campaign-composed-evidence/v1"
            || composedRoot.GetProperty("provider_call_count").GetInt32() != 3
            || composedRoot.GetProperty("fourth_call_observed").GetBoolean())
        {
            throw new InvalidDataException("Campaign composed evidence is incomplete or permits a fourth call.");
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
