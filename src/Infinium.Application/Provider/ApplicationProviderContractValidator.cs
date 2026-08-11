using Infinium.Contracts.Protobuf.Application.V1;

namespace Infinium.Application.Provider;

public static class ApplicationProviderContractValidator
{
    public static void Validate(GetProviderProfileRequest value)
    {
        Require(value.ProfileId?.Value, "profile_id");
    }

    public static void Validate(GetProviderOperationRequest value)
    {
        Require(value.OperationId?.Value, "operation_id");
        if ((value.IncludeSettlement && !value.IncludeUsage)
            || (value.IncludeReplay && (!value.IncludeUsage || !value.IncludeSettlement)))
        {
            throw new InvalidDataException("Provider operation query expansions must include their prerequisite usage and settlement surfaces.");
        }
    }

    public static void Validate(ListProviderBudgetRequest value)
    {
        if (value.ScopeKind is not ("operation" or "evidence-acquisition-run" or "analysis-run"
            or "provider-profile" or "provider-account" or "global")
            || string.IsNullOrWhiteSpace(value.ScopeId) || value.ScopeId.Length > 128
            || value.RequestedPageSize is 0 or > 100 || value.After?.OpaqueValue.Length > 512)
        {
            throw new InvalidDataException("Provider budget query is outside the closed scope/page contract.");
        }
    }

    public static void Validate(ListProviderBudgetResponse value)
    {
        if (value.ResultCase == ListProviderBudgetResponse.ResultOneofCase.None
            || value.ResultCase == ListProviderBudgetResponse.ResultOneofCase.Page
                && value.Page.Items.Count > 100)
        {
            throw new InvalidDataException("Provider budget response must select exactly one bounded success or failure result.");
        }
    }

    public static void Validate(GetProviderReplayRequest value)
    {
        Require(value.OperationId?.Value, "operation_id");
        if (value.RetainedResponseId.Length > 128
            || (value.RetainedResponseId.Length > 0 && !Has(value.RetainedResponseId)))
        {
            throw new InvalidDataException("Replay lookup response identity must be absent or a bounded retained identity.");
        }
    }

    public static void Validate(SubmitProviderEnrollmentRequest value)
    {
        Require(value.ProfileId?.Value, "profile_id");
        Require(value.GenerationId?.Value, "generation_id");
        Require(value.CommandId, "command_id");
        if (value.Provider != "openai" || value.Purpose != "responses"
            || string.IsNullOrWhiteSpace(value.DisplayLabel) || value.DisplayLabel.Length > 128
            || !ValidInstant(value.RequestedAt))
        {
            throw new InvalidDataException("Provider enrollment is outside the closed non-secret profile contract.");
        }
    }

    public static void Validate(ProviderProfilePayload value)
    {
        Require(value.ProfileId?.Value, "profile_id");
        Require(value.GenerationId?.Value, "generation_id");
        bool identityGroupAbsent = value.AccountIdentityId is null && value.BillingScopeIdentityId is null
            && value.CapabilitySnapshotId is null;
        bool identityGroupPresent = Has(value.AccountIdentityId?.Value) && Has(value.BillingScopeIdentityId?.Value)
            && Has(value.CapabilitySnapshotId?.Value);
        bool intentPresent = !string.IsNullOrWhiteSpace(value.IntentId);
        if (value.Provider != "openai" || value.Purpose != "responses"
            || string.IsNullOrWhiteSpace(value.DisplayLabel) || value.DisplayLabel.Length > 128
            || !ValidInstant(value.RecordedAt)
            || !Enum.IsDefined(value.LifecycleState) || value.LifecycleState == ProviderProfileLifecycleState.Unspecified
            || !Enum.IsDefined(value.VerificationState) || value.VerificationState == ProviderAvailabilityState.Unspecified)
        {
            throw new InvalidDataException("Provider profile contains an unknown numeric lifecycle or verification state.");
        }
        bool validShape = value.LifecycleState switch
        {
            ProviderProfileLifecycleState.PendingEnrollment => identityGroupAbsent && intentPresent
                && value.VerificationState == ProviderAvailabilityState.NotApplicable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileLifecycleState.ActiveUnverified => identityGroupPresent && intentPresent
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileLifecycleState.ActiveVerified =>
                identityGroupPresent && intentPresent && value.VerificationState == ProviderAvailabilityState.Available
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileLifecycleState.Replacing or ProviderProfileLifecycleState.Disabled => identityGroupPresent && intentPresent
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileLifecycleState.DeletePending => identityGroupPresent && intentPresent
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition is "pending" or "failed",
            ProviderProfileLifecycleState.Deleted => identityGroupAbsent && !intentPresent
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "confirmed",
            ProviderProfileLifecycleState.SecureStoreUnavailable => (identityGroupAbsent || identityGroupPresent)
                && intentPresent && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "unavailable"
                && value.CleanupDisposition is "not-requested" or "failed",
            ProviderProfileLifecycleState.RecoveryRequired => (identityGroupAbsent || identityGroupPresent)
                && intentPresent && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "required"
                && value.CleanupDisposition is "not-requested" or "failed",
            _ => false,
        };
        if (value.GenerationId is null || value.GenerationOrdinal == 0 || !validShape)
        {
            throw new InvalidDataException("Provider profile lifecycle requires an exact generation/account/billing/capability/intent identity shape.");
        }
    }

    public static void Validate(ProviderOperationPayload value)
    {
        Require(value.OperationId?.Value, "operation_id");
        Require(value.ProfileId?.Value, "profile_id");
        Require(value.GenerationId?.Value, "generation_id");
        Require(value.CapabilitySnapshotId?.Value, "capability_snapshot_id");
        Require(value.PriceSnapshotId?.Value, "price_snapshot_id");
        if (!Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKind.Unspecified
            || !Enum.IsDefined(value.State) || value.State == ProviderOperationLifecycleState.Unspecified
            || !Enum.IsDefined(value.SettlementState) || value.SettlementState == ProviderSettlementState.Unspecified
            || !Enum.IsDefined(value.ReplayState) || value.ReplayState == ProviderReplayState.Unspecified)
        {
            throw new InvalidDataException("Provider operation contains an unknown numeric state.");
        }
        Validate(value.InputTokens);
        Validate(value.OutputTokens);
        Validate(value.TotalTokens);
        Validate(value.CalculatedNanoUsd);
        Validate(value.DispatchCount);
        Validate(value.ReasoningTokens);
        Validate(value.CacheReadTokens);
        Validate(value.CacheWriteTokens);
        Validate(value.ReservedNanoUsd);

        if (!Enum.IsDefined(value.InputBoundProofStatus)
            || value.InputBoundProofStatus == InputBoundProofStatus.Unspecified)
        {
            throw new InvalidDataException("Provider operation proof status is unknown.");
        }
        bool blocked = value.InputBoundProofStatus == InputBoundProofStatus.AuthorityRequired;
        bool anyDownstream = Has(value.AuthorizationId) || value.AttemptId is not null || Has(value.RequestId)
            || value.ReservationId is not null || value.DispatchFenceId is not null || Has(value.ResponseRecordId)
            || value.Response is not null || Has(value.UsageEntryId) || Has(value.SettlementId) || Has(value.ReplayEdgeId);
        if (blocked)
        {
            if (value.InputBoundPolicyId != "unresolved-openai-responses-framing"
                || value.InputBoundPolicyVersion != "authority-required"
                || value.State != ProviderOperationLifecycleState.InputBoundBlocked || anyDownstream
                || AnyValue(value.DispatchCount, value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
                    value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd)
                || value.SettlementState != ProviderSettlementState.NotStarted
                || value.ReplayState != ProviderReplayState.NotAvailable || value.UnresolvedHold)
            {
                throw new InvalidDataException("Authority-required operations may expose only the truthful pre-proof blocked state.");
            }
            return;
        }
        throw new InvalidDataException("No non-blocked provider operation state exists before an accepted local input-bound policy changes this contract.");
    }

    public static void Validate(ProviderResponsePayload value)
    {
        Require(value.ResponseRecordId, "response_record_id");
        if (value.MaximumRawResponseBytes is 0 or > 1_048_576
            || value.RequestedModel != "gpt-5.6-sol" || value.RequestedServiceTier != "default"
            || value.ReasoningContext != "current_turn" || value.ReasoningMode != "standard"
            || value.PromptCacheMode != "explicit"
            || value.Availability != ProviderAvailabilityState.Unavailable
            || value.InputBoundProofStatus != InputBoundProofStatus.AuthorityRequired
            || value.InputBoundPolicyId != "unresolved-openai-responses-framing"
            || value.InputBoundPolicyVersion != "authority-required"
            || Has(value.RequestId) || value.DispatchFenceId is not null || value.RawResponse is not null
            || value.ResponseHeaders is not null || value.HasHttpStatus || Has(value.ProviderResponseId)
            || Has(value.ClientRequestId) || value.BillingEvidence is not null
            || Has(value.ProviderRequestId) || value.ProviderRequestIdAvailability != ProviderAvailabilityState.Unavailable
            || value.ResponseHeadersAvailability != ProviderAvailabilityState.Unavailable
            || value.ResponseState != "unknown" || Has(value.RefusalCode) || Has(value.IncompleteReason)
            || Has(value.ErrorCode) || Has(value.ReturnedModel) || Has(value.ReturnedServiceTier)
            || value.RateLimitFacts.Count != 0 || value.ValidationState != "unavailable"
            || value.AdmissionState != "unavailable" || !ValidInstant(value.RecordedAt))
        {
            throw new InvalidDataException("Provider response payload is unavailable until proof-qualified authorization exists.");
        }
        Validate(value.DispatchCount);
        Validate(value.InputTokens);
        Validate(value.OutputTokens);
        Validate(value.TotalTokens);
        Validate(value.ReasoningTokens);
        Validate(value.CacheReadTokens);
        Validate(value.CacheWriteTokens);
        Validate(value.PricedToolCalls);
        Validate(value.CalculatedNanoUsd);
        if (AnyValue(value.DispatchCount, value.InputTokens, value.OutputTokens, value.TotalTokens,
            value.ReasoningTokens, value.CacheReadTokens, value.CacheWriteTokens,
            value.PricedToolCalls, value.CalculatedNanoUsd))
        {
            throw new InvalidDataException("Unavailable provider response cannot publish usage quantities.");
        }
    }

    public static void Validate(ProviderReplayPayload value)
    {
        Require(value.OperationId?.Value, "operation_id");
        if (!Enum.IsDefined(value.ReplayState) || value.ReplayState is ProviderReplayState.Unspecified
            || value.NetworkPermitted)
        {
            throw new InvalidDataException("Retained-response replay is fail-closed and network-free.");
        }
        bool unavailable = value.ReplayState == ProviderReplayState.NotAvailable;
        bool anyBinding = Has(value.RetainedResponseId) || Has(value.DependencyManifestId)
            || Has(value.InstallationSnapshotId) || Has(value.AnalysisContextId) || Has(value.EffectiveConfigurationId)
            || Has(value.ResolvedInputManifestId) || Has(value.PromptId) || !value.PromptFingerprintSha256.IsEmpty
            || Has(value.OutputSchemaId) || !value.OutputSchemaFingerprintSha256.IsEmpty
            || !value.CanonicalRequestBytes.IsEmpty || !value.CanonicalRequestFingerprintSha256.IsEmpty
            || !value.SettingsFingerprintSha256.IsEmpty || value.ProfileId is not null || value.GenerationId is not null
            || value.CapabilitySnapshotId is not null || value.PriceSnapshotId is not null || value.Limits is not null
            || value.DispatchDeadline is not null || Has(value.AuthorizationId) || value.AttemptId is not null
            || Has(value.RequestId) || value.ReservationId is not null || value.DispatchFenceId is not null
            || Has(value.UsageEntryId) || Has(value.SettlementId) || Has(value.ReplayEdgeId)
            || value.RetainedHoldNanoUsd != 0 || value.OperationKind != ProviderOperationKind.Unspecified;
        bool blockedProof = value.InputBoundProofStatus == InputBoundProofStatus.AuthorityRequired
            && value.InputBoundPolicyId == "unresolved-openai-responses-framing"
            && value.InputBoundPolicyVersion == "authority-required";
        if (unavailable)
        {
            if (anyBinding || !blockedProof)
            {
                throw new InvalidDataException("Unavailable replay must retain only the authority-required proof and cannot fabricate response or dependency bindings.");
            }
            return;
        }
        throw new InvalidDataException("Retained provider replay is unreachable before accepted input-bound authority enables dispatch.");
    }

    public static void Validate(SubmitProviderOperationRequest value)
    {
        Require(value.OperationId?.Value, "operation_id");
        Require(value.ProfileId?.Value, "profile_id");
        Require(value.GenerationId?.Value, "generation_id");
        Require(value.CapabilitySnapshotId?.Value, "capability_snapshot_id");
        Require(value.PriceSnapshotId?.Value, "price_snapshot_id");
        Require(value.OutputSchemaId, "output_schema_id");
        Require(value.OwnerId, "owner_id");
        Require(value.JobNodeId, "job_node_id");
        Require(value.InstallationSnapshotId, "installation_snapshot_id");
        Require(value.AnalysisContextId, "analysis_context_id");
        Require(value.EffectiveConfigurationId, "effective_configuration_id");
        Require(value.ResolvedInputManifestId, "resolved_input_manifest_id");
        Require(value.PromptId, "prompt_id");
        Require(value.CommandId, "command_id");
        if (value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.OperationKind == ProviderOperationKind.SourceClaimExtraction && value.OwnerKind != "evidence-acquisition-run"
            || value.OperationKind is ProviderOperationKind.TransportQualification or ProviderOperationKind.CandidateInvestigation
                && value.OwnerKind != "analysis-run"
            || value.CanonicalRequestFingerprintSha256.Length != 32
            || value.RequestFingerprintSha256.Length != 32
            || value.PromptFingerprintSha256.Length != 32
            || value.CanonicalRequestBody.IsEmpty || value.CanonicalRequestBody.Length > 65_536
            || value.SettingsFingerprintSha256.Length != 32 || value.OutputSchemaFingerprintSha256.Length != 32
            || !Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKind.Unspecified
            || value.Limits is null || !ValidInstant(value.DispatchDeadline)
            || !ValidInstant(value.RequestedAt) || !ValidInstant(value.ConfirmedAt)
            || Compare(value.RequestedAt, value.ConfirmedAt) > 0
            || Compare(value.ConfirmedAt, value.DispatchDeadline) >= 0
            || value.CoordinatorFencingEpoch == 0
            || !System.Security.Cryptography.SHA256.HashData(value.CanonicalRequestBody.Span)
                .AsSpan().SequenceEqual(value.CanonicalRequestFingerprintSha256.Span))
        {
            throw new InvalidDataException("Provider submit confirmation is missing an exact replay or ownership binding.");
        }
        Validate(value.OperationKind, value.Limits);
        if ((ulong)value.CanonicalRequestBody.Length > value.Limits.MaximumRequestBytes)
        {
            throw new InvalidDataException("Canonical request bytes exceed the operation's retained request limit.");
        }
        if (value.InputBoundProofStatus != InputBoundProofStatus.AuthorityRequired
            || value.InputBoundPolicyId != "unresolved-openai-responses-framing"
            || value.InputBoundPolicyVersion != "authority-required")
        {
            throw new InvalidDataException("Provider submit must retain the exact unresolved input-bound proof status.");
        }
        throw new NotSupportedException("Provider operation confirmation is blocked pending accepted local tokenizer/framing authority.");
    }

    public static void RequireDispatchAdmission(SubmitProviderOperationRequest value)
    {
        Validate(value);
    }

    public static void Validate(ProviderCommandReceipt value, string expectedCommandId)
    {
        Require(value.CommandId, "command_id");
        Require(value.ReceiptId, "receipt_id");
        if (value.CommandId != expectedCommandId || value.OperationId is null
            || !Enum.IsDefined(value.State) || value.State == ProviderCommandState.Unspecified
            || !ValidInstant(value.RequestedAt) || !ValidInstant(value.ConfirmedAt)
            || Compare(value.RequestedAt, value.ConfirmedAt) > 0
            || value.State != ProviderCommandState.BlockedAuthorityRequired)
        {
            throw new InvalidDataException("Provider command receipt must bind the exact command and truthful authority block.");
        }
    }

    private static void Validate(OptionalProviderQuantity value)
    {
        if (value is null)
        {
            throw new InvalidDataException("Provider quantity availability is required.");
        }
        if (!Enum.IsDefined(value.Availability) || value.Availability == ProviderAvailabilityState.Unspecified
            || (value.Availability == ProviderAvailabilityState.Available) != value.HasValue)
        {
            throw new InvalidDataException("Provider quantity availability contradicts its value.");
        }
    }

    private static void Validate(ProviderOperationKind kind, ProviderOperationLimits value)
    {
        (ulong request, ulong input, ulong output, ulong response, long cost, ulong deadline) = kind switch
        {
            ProviderOperationKind.TransportQualification => (16_384UL, 20_480UL, 256UL, 262_144UL, 140_000_000L, 60_000UL),
            ProviderOperationKind.SourceClaimExtraction or ProviderOperationKind.CandidateInvestigation =>
                (65_536UL, 73_728UL, 4_096UL, 1_048_576UL, 600_000_000L, 120_000UL),
            _ => throw new InvalidDataException("Provider operation kind is unknown."),
        };
        if (value.MaximumRequestBytes is 0 || value.MaximumRequestBytes > request
            || value.MaximumInputTokens is 0 || value.MaximumInputTokens > input
            || value.MaximumOutputTokens is 0 || value.MaximumOutputTokens > output
            || value.MaximumRawResponseBytes is 0 || value.MaximumRawResponseBytes > response
            || value.MaximumDispatchCount != 1 || value.MaximumCalculatedNanoUsd is <= 0
            || value.MaximumCalculatedNanoUsd > cost || value.DeadlineMilliseconds is 0
            || value.DeadlineMilliseconds > deadline)
        {
            throw new InvalidDataException("Provider submit limits exceed the operation-specific seven-dimensional ceiling.");
        }
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            throw new InvalidDataException(field + " is required.");
        }
    }

    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool AnyValue(params OptionalProviderQuantity[] values) => values.Any(x => x.HasValue);

    private static bool ValidInstant(Infinium.Contracts.Protobuf.Common.V1.Instant? value) =>
        value is not null && value.UnixSeconds > 0 && value.Nanoseconds is >= 0 and <= 999_999_999;

    private static int Compare(
        Infinium.Contracts.Protobuf.Common.V1.Instant left,
        Infinium.Contracts.Protobuf.Common.V1.Instant right) =>
        left.UnixSeconds != right.UnixSeconds
            ? left.UnixSeconds.CompareTo(right.UnixSeconds)
            : left.Nanoseconds.CompareTo(right.Nanoseconds);
}
