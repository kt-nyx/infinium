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
        if (value.ScopeKind is not ("request" or "operation" or "evidence-acquisition-run" or "analysis-run"
            or "provider-profile" or "provider-account" or "billing-scope" or "global")
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
                && (value.Page.Items.Count > 100 || value.Page.Next?.OpaqueValue.Length > 512))
        {
            throw new InvalidDataException("Provider budget response must select exactly one bounded success or failure result.");
        }
        if (value.ResultCase == ListProviderBudgetResponse.ResultOneofCase.Failure)
        {
            if (!Enum.IsDefined(value.Failure.Code)
                || value.Failure.Code == Infinium.Contracts.Protobuf.Common.V1.FailureCode.Unspecified
                || value.Failure.Detail.Length > 1024)
            {
                throw new InvalidDataException("Provider budget failure must use a typed bounded failure result.");
            }
            return;
        }
        foreach (ProviderBudgetPayload item in value.Page.Items)
        {
            if (item.ScopeKind is not ("request" or "operation" or "evidence-acquisition-run" or "analysis-run"
                or "provider-profile" or "provider-account" or "billing-scope" or "global")
                || string.IsNullOrWhiteSpace(item.ScopeId) || item.ScopeId.Length > 128
                || !AmountsFit(item.ReservedNanoUsd, item.SettledNanoUsd, item.UnresolvedNanoUsd))
            {
                throw new InvalidDataException("Provider budget item contradicts its exact scope or finite amounts.");
            }
        }
    }

    private static bool AmountsFit(ulong reserved, ulong settled, ulong unresolved)
    {
        try
        {
            return checked(settled + unresolved) <= reserved;
        }
        catch (OverflowException)
        {
            return false;
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
        Require(value.EffectiveConfigurationV2Id, "effective_configuration_v2_id");
        Require(value.CommandId, "command_id");
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
        ValidateOperationQuantities(value);
        if (value.Reserved is not null) { Validate(value.Reserved); }
        if (value.Observed is not null) { Validate(value.Observed); }
        if (value.Released is not null) { Validate(value.Released); }
        if (value.Retained is not null) { Validate(value.Retained); }

        if (!Enum.IsDefined(value.InputBoundProofStatus)
            || value.InputBoundProofStatus == InputBoundProofStatus.Unspecified)
        {
            throw new InvalidDataException("Provider operation proof status is unknown.");
        }
        bool blocked = value.InputBoundProofStatus == InputBoundProofStatus.AuthorityRequired;
        bool anyDownstream = Has(value.AuthorizationId) || value.AttemptId is not null || Has(value.RequestId)
            || value.ReservationId is not null || value.DispatchFenceId is not null || Has(value.ResponseRecordId)
            || value.Response is not null || Has(value.UsageEntryId) || Has(value.SettlementId) || Has(value.ReplayEdgeId)
            || Has(value.TransportEventId) || Has(value.ReceiptId) || value.Reserved is not null
            || value.Observed is not null || value.Released is not null || value.Retained is not null;
        if (value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.OperationKind == ProviderOperationKind.SourceClaimExtraction && value.OwnerKind != "evidence-acquisition-run"
            || value.OperationKind is ProviderOperationKind.TransportQualification or ProviderOperationKind.CandidateInvestigation
                && value.OwnerKind != "analysis-run"
            || string.IsNullOrWhiteSpace(value.OwnerId) || string.IsNullOrWhiteSpace(value.JobNodeId)
            || !ValidInstant(value.RequestedAt))
        {
            throw new InvalidDataException("Provider operation requires an exact durable owner and requested time.");
        }
        if (blocked)
        {
            if (value.InputBoundPolicyId != "unresolved-openai-responses-framing"
                || value.InputBoundPolicyVersion != "authority-required"
                || value.State != ProviderOperationLifecycleState.InputBoundBlocked || anyDownstream
                || value.TransportState != "not-started" || value.ReceiptState != "not-available"
                || AnyValue(value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
                    value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd)
                || value.DispatchCount.Availability != ProviderAvailabilityState.Available
                || !value.DispatchCount.HasValue || value.DispatchCount.Value != 0
                || value.UsageReceiptState != UsageReceiptState.NotDispatched
                || new[] { value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
                    value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd }
                    .Any(quantity => quantity.Availability != ProviderAvailabilityState.Unavailable)
                || value.SettlementState != ProviderSettlementState.NotStarted
                || value.ReplayState != ProviderReplayState.NotAvailable || value.UnresolvedHold)
            {
                throw new InvalidDataException("Authority-required operations may expose only the truthful pre-proof blocked state.");
            }
            return;
        }
        if (value.InputBoundProofStatus != InputBoundProofStatus.Proved
            || value.InputBoundPolicyId != OpenAiResponsesInputBoundPolicy.PolicyId
            || value.InputBoundPolicyVersion != OpenAiResponsesInputBoundPolicy.PolicyVersion)
        {
            throw new InvalidDataException("A future provider operation requires one explicit proved input-bound policy identity.");
        }
        ValidateFutureOperationShape(value);
    }

    public static void Validate(ProviderResponsePayload value)
    {
        Require(value.ResponseRecordId, "response_record_id");
        Require(value.OwnerId, "owner_id");
        if (value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.OperationKind == ProviderOperationKind.SourceClaimExtraction && value.OwnerKind != "evidence-acquisition-run"
            || value.OperationKind is ProviderOperationKind.TransportQualification or ProviderOperationKind.CandidateInvestigation
                && value.OwnerKind != "analysis-run")
        {
            throw new InvalidDataException("Provider response must bind one exact durable operation owner kind.");
        }
        Validate(value.OperationKind, value.Limits);
        ProviderAvailabilityState[] factAvailabilities =
        [
            value.RawResponseAvailability, value.ResponseHeadersAvailability, value.HttpStatusAvailability,
            value.ProviderResponseIdAvailability, value.ClientRequestIdAvailability,
            value.ProviderRequestIdAvailability, value.RefusalAvailability, value.IncompleteAvailability,
            value.ErrorAvailability, value.ReturnedModelAvailability,
            value.ReturnedServiceTierAvailability, value.BillingEvidenceAvailability,
        ];
        if (factAvailabilities.Any(x => !Enum.IsDefined(x) || x == ProviderAvailabilityState.Unspecified)
            || !Enum.IsDefined(value.UsageAvailability) || value.UsageAvailability == ProviderAvailabilityState.Unspecified
            || !Enum.IsDefined(value.UsageReceiptState) || value.UsageReceiptState == UsageReceiptState.Unspecified)
        {
            throw new InvalidDataException("Every optional provider response fact requires typed availability.");
        }
        RequireAvailability(value.RawResponseAvailability, value.RawResponse);
        RequireAvailability(value.ResponseHeadersAvailability, value.ResponseHeaders);
        RequireAvailability(value.HttpStatusAvailability, value.HasHttpStatus);
        RequireAvailability(value.ProviderResponseIdAvailability, Has(value.ProviderResponseId));
        RequireAvailability(value.ClientRequestIdAvailability, Has(value.ClientRequestId));
        RequireAvailability(value.ProviderRequestIdAvailability, Has(value.ProviderRequestId));
        RequireAvailability(value.RefusalAvailability, Has(value.RefusalCode));
        RequireAvailability(value.IncompleteAvailability, Has(value.IncompleteReason));
        RequireAvailability(value.ErrorAvailability, Has(value.ErrorCode));
        RequireAvailability(value.ReturnedModelAvailability, Has(value.ReturnedModel));
        RequireAvailability(value.ReturnedServiceTierAvailability, Has(value.ReturnedServiceTier));
        RequireAvailability(value.BillingEvidenceAvailability, value.BillingEvidence);
        if (value.MaximumRawResponseBytes is 0 or > 1_048_576
            || value.MaximumRawResponseBytes != value.Limits.MaximumRawResponseBytes
            || value.RawResponse is not null && (!ValidDigest(value.RawResponse)
                || value.RawResponse.SizeBytes > value.MaximumRawResponseBytes)
            || value.HasOverflowObservedExcessBytes
                && value.OverflowObservedExcessBytes != 1
            || value.ResponseHeaders is not null && (!ValidDigest(value.ResponseHeaders)
                || value.ResponseHeaders.SizeBytes > 65_536)
            || value.BillingEvidence is not null && (!ValidDigest(value.BillingEvidence)
                || value.BillingEvidence.SizeBytes > 65_536)
            || value.RequestedModel != "gpt-5.6-sol" || value.RequestedServiceTier != "default"
            || value.ReasoningContext != "current_turn" || value.ReasoningMode != "standard"
            || value.PromptCacheMode != "explicit"
            || !ValidInstant(value.RecordedAt))
        {
            throw new InvalidDataException("Provider response payload requested profile or bound is invalid.");
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
        if (value.UsageAvailability != value.Availability
            || value.TotalTokens.HasValue && (!value.InputTokens.HasValue || !value.OutputTokens.HasValue
                || value.TotalTokens.Value != checked(value.InputTokens.Value + value.OutputTokens.Value))
            || value.ReasoningTokens.HasValue && (!value.OutputTokens.HasValue
                || value.ReasoningTokens.Value > value.OutputTokens.Value)
            || value.DispatchCount.HasValue && value.DispatchCount.Value > 2
            || value.InputTokens.HasValue && value.InputTokens.Value > 147_456
            || value.OutputTokens.HasValue && value.OutputTokens.Value > 8_192
            || value.TotalTokens.HasValue && value.TotalTokens.Value > 155_648
            || value.ReasoningTokens.HasValue && value.ReasoningTokens.Value > 8_192
            || value.CalculatedNanoUsd.HasValue && value.CalculatedNanoUsd.Value > 1_200_000_000
            || value.CacheReadTokens.HasValue && value.CacheReadTokens.Value > 147_456
            || value.CacheWriteTokens.HasValue && value.CacheWriteTokens.Value > 147_456
            || value.PricedToolCalls.HasValue && value.PricedToolCalls.Value > 64
            || value.CreditAvailability == ProviderAvailabilityState.Available)
        {
            throw new InvalidDataException("Provider usage must match response availability, exact totals, and absolute retained-evidence bounds.");
        }
        if (value.RateLimitFacts.Count > 64
            || value.RateLimitFacts.GroupBy(x => (x.Scope, x.Dimension)).Any(x => x.Count() != 1)
            || value.RateLimitFacts.Any(x => !ValidRateLimitFact(x)
                || Compare(x.ObservedAt, value.RecordedAt) < 0)
            || (value.RateAvailability == ProviderAvailabilityState.Available) != (value.RateLimitFacts.Count != 0)
            || (value.BillingAvailability == ProviderAvailabilityState.Available)
                != (value.BillingEvidenceAvailability == ProviderAvailabilityState.Available))
        {
            throw new InvalidDataException("Provider rate and billing evidence is not exact or availability-bound.");
        }
        bool blocked = value.InputBoundProofStatus == InputBoundProofStatus.AuthorityRequired;
        if (blocked && (value.InputBoundPolicyId != "unresolved-openai-responses-framing"
            || value.InputBoundPolicyVersion != "authority-required"
            || value.Availability != ProviderAvailabilityState.Unavailable
            || Has(value.AuthorizationId) || value.AttemptId is not null || Has(value.RequestId)
            || value.ReservationId is not null || value.DispatchFenceId is not null
            || factAvailabilities.Any(x => x != ProviderAvailabilityState.Unavailable)
            || value.BillingAvailability != ProviderAvailabilityState.Unavailable
            || value.RateAvailability != ProviderAvailabilityState.Unavailable
            || value.CreditAvailability != ProviderAvailabilityState.Unavailable
            || value.UsageAvailability != ProviderAvailabilityState.Unavailable
            || value.ResponseState != "unknown" || value.RateLimitFacts.Count != 0
            || value.ValidationState != "unavailable" || value.AdmissionState != "unavailable"
            || value.DispatchCount.Availability != ProviderAvailabilityState.Available
            || !value.DispatchCount.HasValue || value.DispatchCount.Value != 0
            || AnyValue(value.InputTokens, value.OutputTokens, value.TotalTokens,
                value.ReasoningTokens, value.CacheReadTokens, value.CacheWriteTokens,
                value.PricedToolCalls, value.CalculatedNanoUsd)
            || new[] { value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
                value.CacheReadTokens, value.CacheWriteTokens, value.PricedToolCalls, value.CalculatedNanoUsd }
                .Any(quantity => quantity.Availability != ProviderAvailabilityState.Unavailable)))
        {
            throw new InvalidDataException("Unavailable provider response must publish exact zero dispatch and no usage quantities.");
        }
        if (blocked)
        {
            return;
        }
        if (value.InputBoundProofStatus != InputBoundProofStatus.Proved
            || value.InputBoundPolicyId != OpenAiResponsesInputBoundPolicy.PolicyId
            || value.InputBoundPolicyVersion != OpenAiResponsesInputBoundPolicy.PolicyVersion
            || (value.ResponseState == "cancelled"
                ? value.Availability != ProviderAvailabilityState.Unavailable
                    || !Has(value.AuthorizationId) || !Has(value.AttemptId?.Value) || !Has(value.RequestId)
                    || !Has(value.ReservationId?.Value) || value.DispatchFenceId is not null
                : value.Availability != ProviderAvailabilityState.Available
                    || !Has(value.AuthorizationId) || !Has(value.AttemptId?.Value) || !Has(value.RequestId)
                    || !Has(value.ReservationId?.Value) || value.DispatchFenceId is null))
        {
            throw new InvalidDataException("Future provider response requires exact proof and transport identities.");
        }
        ValidateFutureResponseShape(value);
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
        Require(value.EffectiveConfigurationV2Id, "effective_configuration_v2_id");
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
            || ElapsedHundredNanoseconds(value.ConfirmedAt, value.DispatchDeadline)
                > checked(value.Limits.DeadlineMilliseconds * 10_000UL)
            || ElapsedHundredNanoseconds(value.RequestedAt, value.DispatchDeadline)
                > checked(value.Limits.DeadlineMilliseconds * 10_000UL)
            || value.CoordinatorFencingEpoch == 0
            || !value.RequestFingerprintSha256.Span.SequenceEqual(value.CanonicalRequestFingerprintSha256.Span)
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
        if (value.InputBoundProofStatus != InputBoundProofStatus.Proved
            || value.InputBoundPolicyId != OpenAiResponsesInputBoundPolicy.PolicyId
            || value.InputBoundPolicyVersion != OpenAiResponsesInputBoundPolicy.PolicyVersion)
        {
            throw new InvalidDataException("Provider submit must retain the exact accepted local input-bound proof identity.");
        }
        ProviderInputBoundEvidence evidence = OpenAiResponsesInputBoundPolicy.Prove(
            value.OperationKind switch
            {
                ProviderOperationKind.TransportQualification => Domain.Contracts.ProviderOperationKind.TransportQualification,
                ProviderOperationKind.SourceClaimExtraction => Domain.Contracts.ProviderOperationKind.SourceClaimExtraction,
                ProviderOperationKind.CandidateInvestigation => Domain.Contracts.ProviderOperationKind.CandidateInvestigation,
                _ => throw new InvalidDataException("Provider submit operation kind is unknown."),
            },
            value.CanonicalRequestBody.Memory,
            new Domain.Contracts.ProviderFiniteLimitsContract(
                checked((long)value.Limits.MaximumRequestBytes),
                checked((long)value.Limits.MaximumInputTokens),
                checked((long)value.Limits.MaximumOutputTokens),
                checked((long)value.Limits.MaximumRawResponseBytes),
                checked((long)value.Limits.MaximumDispatchCount),
                value.Limits.MaximumCalculatedNanoUsd,
                checked((long)value.Limits.DeadlineMilliseconds)));
        if (!evidence.CanonicalRequestFingerprint.Value.Equals(
            Convert.ToHexStringLower(value.CanonicalRequestFingerprintSha256.Span),
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("Provider submit input-bound evidence does not match the canonical request fingerprint.");
        }
    }

    public static void RequireDispatchAdmission(SubmitProviderOperationRequest value)
    {
        Validate(value);
    }

    public static void Validate(
        ProviderCommandReceipt value,
        string expectedCommandId,
        string? expectedOperationId = null,
        ReadOnlySpan<byte> expectedRequestFingerprint = default,
        string? expectedProfileId = null,
        string? expectedGenerationId = null)
    {
        Require(value.CommandId, "command_id");
        Require(value.ReceiptId, "receipt_id");
        bool enrollment = value.State == ProviderCommandState.EnrollmentIntentRecorded;
        bool blockedOperation = value.State == ProviderCommandState.BlockedAuthorityRequired;
        if (value.CommandId != expectedCommandId
            || !Enum.IsDefined(value.State) || value.State == ProviderCommandState.Unspecified
            || !ValidInstant(value.RequestedAt) || !ValidInstant(value.ConfirmedAt)
            || Compare(value.RequestedAt, value.ConfirmedAt) > 0
            || enrollment != (value.SubjectCase == ProviderCommandReceipt.SubjectOneofCase.Enrollment)
            || blockedOperation != (value.SubjectCase == ProviderCommandReceipt.SubjectOneofCase.Operation)
            || (!enrollment && !blockedOperation))
        {
            throw new InvalidDataException("Provider command receipt kind, outcome, and subject are contradictory.");
        }
        if (enrollment)
        {
            Require(expectedProfileId, "expected_profile_id");
            Require(expectedGenerationId, "expected_generation_id");
            if (value.Enrollment.ProfileId?.Value != expectedProfileId
                || value.Enrollment.GenerationId?.Value != expectedGenerationId)
            {
                throw new InvalidDataException("Enrollment receipt must bind the exact profile generation.");
            }
        }
        else
        {
            Require(expectedOperationId, "expected_operation_id");
            if (expectedRequestFingerprint.Length != 32
                || value.Operation.OperationId?.Value != expectedOperationId
                || !value.Operation.RequestFingerprintSha256.Span.SequenceEqual(expectedRequestFingerprint))
            {
                throw new InvalidDataException("Blocked operation receipt must bind the exact operation request identity.");
            }
        }
    }

    public static void Validate(SourceClaimExtractionPayload value)
    {
        Require(value.AcquisitionRunId, "acquisition_run_id");
        Require(value.OperationId?.Value, "operation_id");
        Require(value.OwnerId, "owner_id");
        Require(value.ParentAnalysisRunId, "parent_analysis_run_id");
        Require(value.ApplicationScopeId, "application_scope_id");
        Require(value.CostAttributionScopeId, "cost_attribution_scope_id");
        Require(value.SourceRevisionId, "source_revision_id");
        if (value.OwnerKind != "evidence-acquisition-run" || value.OwnerId != value.AcquisitionRunId
            || value.ValidationIds.Count == 0 || value.AdmissionCorrelationIds.Count == 0
            || !ValidUniqueIds(value.ValidationIds) || !ValidUniqueIds(value.AdmissionCorrelationIds)
            || !ValidSourceClaimAdmissionCorrelations(value.AdmissionCorrelations, value.OperationId!.Value,
                value.OwnerKind, value.OwnerId, value.SourceRevisionId, value.ValidationIds,
                value.AdmissionCorrelationIds))
        {
            throw new InvalidDataException("Source-claim projection must retain exact acquisition ownership and admission correlations.");
        }
    }

    public static void Validate(CandidateInvestigationPayload value)
    {
        Require(value.OperationId?.Value, "operation_id");
        Require(value.OwnerId, "owner_id");
        Require(value.AnalysisRunId, "analysis_run_id");
        Require(value.CandidateId, "candidate_id");
        if (value.OwnerKind != "analysis-run" || value.OwnerId != value.AnalysisRunId
            || value.ValidationIds.Count == 0 || value.AdmissionLinkIds.Count == 0
            || !ValidUniqueIds(value.ValidationIds) || !ValidUniqueIds(value.AdmissionLinkIds)
            || !ValidAdmissionLinks(value.AdmissionLinks, value.OperationId!.Value, value.OwnerKind,
                value.OwnerId, value.CandidateId, value.ValidationIds, value.AdmissionLinkIds,
                declaredIdsAreAdmissions: true))
        {
            throw new InvalidDataException("Candidate projection must retain exact analysis ownership and admission links.");
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

    private static void Validate(ProviderAccountingVector value)
    {
        OptionalProviderQuantity[] quantities =
        [
            value.DispatchCount, value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
            value.CacheReadTokens, value.CacheWriteTokens, value.PricedToolCalls, value.CalculatedNanoUsd,
        ];
        foreach (OptionalProviderQuantity quantity in quantities)
        {
            Validate(quantity);
        }
        if (value.DispatchCount.Value > 2 || value.InputTokens.Value > 147_456 || value.OutputTokens.Value > 8_192
            || value.TotalTokens.Value > 155_648 || value.ReasoningTokens.Value > 8_192
            || value.CacheReadTokens.Value > 147_456 || value.CacheWriteTokens.Value > 147_456
            || value.PricedToolCalls.Value > 64 || value.CalculatedNanoUsd.Value > 1_200_000_000
            || value.TotalTokens.Availability == ProviderAvailabilityState.Available
                && (value.InputTokens.Availability != ProviderAvailabilityState.Available
                    || value.OutputTokens.Availability != ProviderAvailabilityState.Available
                    || value.TotalTokens.Value != checked(value.InputTokens.Value + value.OutputTokens.Value))
            || value.ReasoningTokens.Availability == ProviderAvailabilityState.Available
                && (value.OutputTokens.Availability != ProviderAvailabilityState.Available
                    || value.ReasoningTokens.Value > value.OutputTokens.Value))
        {
            throw new InvalidDataException("Provider accounting vector exceeds its closed post-fact bounds or is internally inconsistent.");
        }
    }

    private static void ValidateOperationQuantities(ProviderOperationPayload value)
    {
        if (value.DispatchCount.Value > 2 || value.InputTokens.Value > 147_456 || value.OutputTokens.Value > 8_192
            || value.TotalTokens.Value > 155_648 || value.ReasoningTokens.Value > 8_192
            || value.CacheReadTokens.Value > 147_456 || value.CacheWriteTokens.Value > 147_456
            || value.CalculatedNanoUsd.Value > 1_200_000_000 || value.ReservedNanoUsd.Value > 600_000_000
            || value.TotalTokens.Availability == ProviderAvailabilityState.Available
                && (value.InputTokens.Availability != ProviderAvailabilityState.Available
                    || value.OutputTokens.Availability != ProviderAvailabilityState.Available
                    || value.TotalTokens.Value != checked(value.InputTokens.Value + value.OutputTokens.Value))
            || value.ReasoningTokens.Availability == ProviderAvailabilityState.Available
                && (value.OutputTokens.Availability != ProviderAvailabilityState.Available
                    || value.ReasoningTokens.Value > value.OutputTokens.Value))
        {
            throw new InvalidDataException("Provider operation quantities exceed their closed assignment or post-fact bounds.");
        }
    }

    private static void ValidateFutureOperationShape(ProviderOperationPayload value)
    {
        bool blockedUsage = value.DispatchCount is
        { Availability: ProviderAvailabilityState.Available, HasValue: true, Value: 0 }
            && new[] { value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
                value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd }
                .All(quantity => quantity.Availability == ProviderAvailabilityState.Unavailable && !quantity.HasValue)
            && value.UsageReceiptState == UsageReceiptState.NotDispatched;
        int identityStage;
        bool validProjection;
        switch (value.State)
        {
            case ProviderOperationLifecycleState.Proposed:
                identityStage = 0;
                validProjection = value.TransportState == "not-started" && value.ReceiptState == "not-available"
                    && blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.Confirmed:
                identityStage = 1;
                validProjection = value.TransportState == "not-started" && value.ReceiptState == "not-available"
                    && blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.Reserved:
            case ProviderOperationLifecycleState.Assigned:
                identityStage = 4;
                validProjection = value.TransportState == "not-started" && value.ReceiptState == "not-available"
                    && blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.FinalGateAuthorized:
                identityStage = 5;
                validProjection = value.TransportState == "not-started" && value.ReceiptState == "not-available"
                    && blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.TransportNotStarted:
                identityStage = 6;
                validProjection = value.TransportState == "not-started" && value.ReceiptState == "not-available"
                    && blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.TransportMayHaveStarted:
                identityStage = 6;
                validProjection = value.TransportState == "may-have-started" && value.ReceiptState == "not-available"
                    && blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.ResponseStaged:
                identityStage = 9;
                validProjection = value.TransportState == "completed" && value.ReceiptState == "staged"
                    && !blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.Admitted:
                identityStage = 9;
                validProjection = value.TransportState == "completed" && value.ReceiptState == "validated"
                    && !blockedUsage && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.Rejected:
                identityStage = 9;
                validProjection = value.TransportState is "completed" or "failed-known" or "ambiguous"
                    && value.ReceiptState == "rejected" && !blockedUsage
                    && value.SettlementState == ProviderSettlementState.NotStarted;
                break;
            case ProviderOperationLifecycleState.Settled:
                identityStage = 10;
                validProjection = value.TransportState is "completed" or "failed-known"
                    && value.ReceiptState is "validated" or "rejected" && !blockedUsage
                    && value.SettlementState is ProviderSettlementState.Settled
                        or ProviderSettlementState.FailedKnown or ProviderSettlementState.Overrun;
                break;
            case ProviderOperationLifecycleState.UnresolvedHold:
                identityStage = 10;
                validProjection = value.TransportState is "may-have-started" or "started" or "ambiguous"
                    && value.ReceiptState == "unresolved" && !blockedUsage
                    && value.SettlementState == ProviderSettlementState.UnresolvedHold;
                break;
            default:
                throw new InvalidDataException("Provider operation state is not part of the closed lifecycle matrix.");
        }

        bool replayProjection = identityStage < 9
            ? value.ReplayState == ProviderReplayState.NotAvailable && !Has(value.ReplayEdgeId)
            : value.ReplayState == ProviderReplayState.NotAvailable
                ? !Has(value.ReplayEdgeId)
                : value.ReplayState is ProviderReplayState.RetainedResponse or ProviderReplayState.AuditOnly
                    && Has(value.ReplayEdgeId);
        bool exactVectors = identityStage < 4 ? value.Reserved is null : value.Reserved is not null;
        exactVectors &= identityStage < 9
            ? value.Observed is null && value.Response is null
            : value.Observed is not null && value.Response is not null;
        exactVectors &= identityStage < 10
            ? value.Released is null && value.Retained is null
            : value.Released is not null && value.Retained is not null;
        if (!validProjection || !ExactOperationIdentityStage(value, identityStage) || !replayProjection
            || !exactVectors || value.UnresolvedHold != (value.State == ProviderOperationLifecycleState.UnresolvedHold))
        {
            throw new InvalidDataException("Provider operation state contradicts its reachable identities, accounting, or terminal projections.");
        }
    }

    private static bool ExactOperationIdentityStage(ProviderOperationPayload value, int stage)
    {
        bool[] present =
        [
            Has(value.AuthorizationId), value.AttemptId is not null, Has(value.RequestId), value.ReservationId is not null,
            value.DispatchFenceId is not null, Has(value.TransportEventId), Has(value.ReceiptId),
            Has(value.ResponseRecordId), Has(value.UsageEntryId), Has(value.SettlementId),
        ];
        bool validTypedIdentities = (value.AttemptId is null || Has(value.AttemptId.Value))
            && (value.ReservationId is null || Has(value.ReservationId.Value))
            && (value.DispatchFenceId is null || Has(value.DispatchFenceId.Value));
        return validTypedIdentities && present.Take(stage).All(x => x) && present.Skip(stage).All(x => !x);
    }

    private static void RequireAvailability(ProviderAvailabilityState availability, object? value) =>
        RequireAvailability(availability, value is not null);

    private static void RequireAvailability(ProviderAvailabilityState availability, bool hasValue)
    {
        if ((availability == ProviderAvailabilityState.Available) != hasValue)
        {
            throw new InvalidDataException("Provider fact availability contradicts presence.");
        }
    }

    private static void ValidateFutureResponseShape(ProviderResponsePayload value)
    {
        bool raw = value.RawResponseAvailability == ProviderAvailabilityState.Available;
        bool http = value.HttpStatusAvailability == ProviderAvailabilityState.Available;
        bool refusal = value.RefusalAvailability == ProviderAvailabilityState.Available;
        bool incomplete = value.IncompleteAvailability == ProviderAvailabilityState.Available;
        bool error = value.ErrorAvailability == ProviderAvailabilityState.Available;
        bool transport = Has(value.AuthorizationId) && Has(value.AttemptId?.Value) && Has(value.RequestId)
            && Has(value.ReservationId?.Value) && value.DispatchFenceId is not null;
        bool reservedUndispatched = Has(value.AuthorizationId) && Has(value.AttemptId?.Value) && Has(value.RequestId)
            && Has(value.ReservationId?.Value) && value.DispatchFenceId is null;
        bool semanticFactsAbsent = !refusal && !incomplete && !error;
        bool completedUsage = value.UsageAvailability == ProviderAvailabilityState.Available
            && new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.TotalTokens,
                value.ReasoningTokens, value.CacheReadTokens, value.CacheWriteTokens,
                value.PricedToolCalls, value.CalculatedNanoUsd }
                .All(quantity => quantity.Availability == ProviderAvailabilityState.Available)
            && value.DispatchCount.Value >= 1 && value.UsageReceiptState == UsageReceiptState.Complete;
        bool dispatchedUsage = value.UsageAvailability == ProviderAvailabilityState.Available
            && value.DispatchCount is { Availability: ProviderAvailabilityState.Available, HasValue: true, Value: >= 1 };
        bool cancelledUsage = value.UsageAvailability == ProviderAvailabilityState.Unavailable
            && value.DispatchCount is { Availability: ProviderAvailabilityState.Available, HasValue: true, Value: 0 }
            && new[] { value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
                value.CacheReadTokens, value.CacheWriteTokens, value.PricedToolCalls, value.CalculatedNanoUsd }
                .All(quantity => quantity.Availability != ProviderAvailabilityState.Available);
        bool allProviderFactsUnavailable = new[]
            {
                value.RawResponseAvailability, value.ResponseHeadersAvailability, value.HttpStatusAvailability,
                value.ProviderResponseIdAvailability, value.ClientRequestIdAvailability,
                value.ProviderRequestIdAvailability, value.RefusalAvailability, value.IncompleteAvailability,
                value.ErrorAvailability, value.ReturnedModelAvailability, value.ReturnedServiceTierAvailability,
                value.BillingEvidenceAvailability,
            }.All(availability => availability == ProviderAvailabilityState.Unavailable)
            && value.RateLimitFacts.Count == 0
            && value.BillingAvailability == ProviderAvailabilityState.Unavailable
            && value.RateAvailability == ProviderAvailabilityState.Unavailable
            && value.CreditAvailability == ProviderAvailabilityState.Unavailable;
        bool boundedOverflow = !raw && value.RawResponse is null
            && value.HasOverflowObservedExcessBytes
            && value.OverflowObservedExcessBytes == 1;
        bool noOverflow = !value.HasOverflowObservedExcessBytes;
        bool nonSuccess = value.ValidationState is "rejected" or "abstained" or "unavailable" or "unsupported"
            && value.AdmissionState is "rejected" or "abstained" or "unavailable" or "unsupported";
        bool policyCompliant = value.DispatchCount.Value <= value.Limits.MaximumDispatchCount
            && value.InputTokens.Value <= value.Limits.MaximumInputTokens
            && value.OutputTokens.Value <= value.Limits.MaximumOutputTokens
            && value.CacheReadTokens.Value == 0 && value.CacheWriteTokens.Value == 0
            && value.PricedToolCalls.Value == 0
            && value.CalculatedNanoUsd.Value <= (ulong)value.Limits.MaximumCalculatedNanoUsd;
        bool valid = value.ResponseState switch
        {
            "completed" => transport && raw && http && value.ReturnedModelAvailability == ProviderAvailabilityState.Available
                && value.ReturnedServiceTierAvailability == ProviderAvailabilityState.Available
                && value.ReturnedModel == "gpt-5.6-sol" && value.ReturnedServiceTier == "default"
                && semanticFactsAbsent && noOverflow && completedUsage
                && (policyCompliant
                    ? value.ValidationState == "admitted" && value.AdmissionState == "admitted"
                    : nonSuccess),
            "refusal" => transport && raw && http && refusal && !incomplete && !error && noOverflow
                && dispatchedUsage && value.UsageReceiptState == UsageReceiptState.Complete && nonSuccess,
            "incomplete" => transport && raw && http && !refusal && incomplete && !error && noOverflow
                && dispatchedUsage && value.UsageReceiptState == UsageReceiptState.Partial && nonSuccess,
            "failed" => transport && raw && http && !refusal && !incomplete && error && noOverflow
                && dispatchedUsage && value.UsageReceiptState == UsageReceiptState.FailedKnown && nonSuccess,
            "queued" or "in-progress" => transport && raw && http && semanticFactsAbsent && noOverflow
                && dispatchedUsage && value.UsageReceiptState == UsageReceiptState.Partial && nonSuccess,
            "malformed" => transport && raw && http && !refusal && !incomplete && !error && noOverflow
                && dispatchedUsage && value.UsageReceiptState == UsageReceiptState.Complete && nonSuccess,
            "oversized" => transport && boundedOverflow && http && !refusal && !incomplete && !error
                && dispatchedUsage && value.UsageReceiptState is UsageReceiptState.Complete or UsageReceiptState.Partial && nonSuccess,
            "mismatched" => transport && raw && http && dispatchedUsage
                && value.ReturnedModelAvailability == ProviderAvailabilityState.Available
                && value.ReturnedServiceTierAvailability == ProviderAvailabilityState.Available
                && (value.ReturnedModel != "gpt-5.6-sol" || value.ReturnedServiceTier != "default") && noOverflow
                && value.UsageReceiptState == UsageReceiptState.Complete && nonSuccess,
            "unknown" => transport && raw && http && semanticFactsAbsent && noOverflow && dispatchedUsage
                && value.UsageReceiptState == UsageReceiptState.Ambiguous && nonSuccess,
            "cancelled" => reservedUndispatched && !raw && !http && semanticFactsAbsent && allProviderFactsUnavailable
                && noOverflow && cancelledUsage && value.UsageReceiptState == UsageReceiptState.NotDispatched && nonSuccess,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidDataException("Provider response state contradicts typed facts and admission state.");
        }
    }

    private static bool ValidDigest(Infinium.Contracts.Protobuf.Common.V1.ContentDigest value) =>
        value.Algorithm == Infinium.Contracts.Protobuf.Common.V1.DigestAlgorithm.Sha256
        && value.Value.Length == 32
        && value.SizeBytes > 0;

    private static bool ValidAdmissionLinks(
        IEnumerable<ProviderSemanticAdmissionLink> links,
        string operationId,
        string ownerKind,
        string ownerId,
        string rootSubjectId,
        IEnumerable<string> validationIds,
        IEnumerable<string> declaredLinkIds,
        bool declaredIdsAreAdmissions)
    {
        ProviderSemanticAdmissionLink[] items = links.ToArray();
        return items.Length <= 64
            && items.Select(x => x.AdmissionId).Distinct(StringComparer.Ordinal).Count() == items.Length
            && items.All(link => Has(link.AdmissionId) && Has(link.ProposalId) && Has(link.AuthorizationId)
                && link.OperationId?.Value == operationId && Has(link.ResponseRecordId)
                && link.OwnerKind == ownerKind && link.OwnerId == ownerId
                && link.RootSubjectId == rootSubjectId
                && validationIds.Contains(link.ValidationId)
                && (declaredIdsAreAdmissions
                    ? declaredLinkIds.Contains(link.AdmissionId)
                    : declaredLinkIds.Contains(link.ApplicationLinkId))
                && link.State is "admitted" or "rejected" or "abstained" or "unavailable" or "unsupported" or "deleted");
    }

    private static bool ValidSourceClaimAdmissionCorrelations(
        IEnumerable<SourceClaimAdmissionCorrelation> links,
        string operationId,
        string ownerKind,
        string ownerId,
        string rootSubjectId,
        IEnumerable<string> validationIds,
        IEnumerable<string> correlationIds)
    {
        SourceClaimAdmissionCorrelation[] items = links.ToArray();
        return items.Length <= 64
            && items.Select(x => x.AdmissionId).Distinct(StringComparer.Ordinal).Count() == items.Length
            && items.Select(x => x.AdmissionCorrelationId).Distinct(StringComparer.Ordinal).Count() == items.Length
            && items.All(link => Has(link.AdmissionId) && Has(link.ProposalId) && Has(link.AuthorizationId)
                && link.OperationId?.Value == operationId && Has(link.ResponseRecordId)
                && link.OwnerKind == ownerKind && link.OwnerId == ownerId
                && link.RootSubjectId == rootSubjectId
                && validationIds.Contains(link.ValidationId)
                && correlationIds.Contains(link.AdmissionCorrelationId)
                && link.State is "admitted" or "rejected" or "abstained" or "unavailable" or "unsupported" or "deleted");
    }

    private static bool ValidRateLimitFact(ProviderRateLimitFact value)
    {
        if (value.Scope is not ("request" or "project" or "organization" or "model")
            || value.Dimension is not ("requests" or "input-tokens" or "output-tokens" or "total-tokens")
            || !Enum.IsDefined(value.Availability) || value.Availability == ProviderAvailabilityState.Unspecified
            || !ValidInstant(value.ObservedAt))
        {
            return false;
        }
        if (value.Availability == ProviderAvailabilityState.Available)
        {
            return value.HasLimit && value.HasRemaining && value.Remaining <= value.Limit
                && (value.ResetsAt is null || ValidInstant(value.ResetsAt) && Compare(value.ObservedAt, value.ResetsAt) <= 0);
        }
        return !value.HasLimit && !value.HasRemaining && value.ResetsAt is null;
    }

    private static void Validate(ProviderOperationKind kind, ProviderOperationLimits value)
    {
        if (value is null)
        {
            throw new InvalidDataException("Provider operation limits are required.");
        }
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
        value is not null && value.UnixSeconds > 0 && value.Nanoseconds is >= 0 and <= 999_999_999
        && value.Nanoseconds % 100 == 0;

    private static int Compare(
        Infinium.Contracts.Protobuf.Common.V1.Instant left,
        Infinium.Contracts.Protobuf.Common.V1.Instant right) =>
        left.UnixSeconds != right.UnixSeconds
            ? left.UnixSeconds.CompareTo(right.UnixSeconds)
            : left.Nanoseconds.CompareTo(right.Nanoseconds);

    private static ulong ElapsedHundredNanoseconds(
        Infinium.Contracts.Protobuf.Common.V1.Instant start,
        Infinium.Contracts.Protobuf.Common.V1.Instant end)
    {
        long totalNanos = checked((end.UnixSeconds - start.UnixSeconds) * 1_000_000_000L
            + end.Nanoseconds - start.Nanoseconds);
        if (totalNanos <= 0)
        {
            throw new InvalidDataException("Dispatch deadline must follow its retained confirmation instant.");
        }
        if (totalNanos % 100 != 0)
        {
            throw new InvalidDataException("Provider authority instants must use exact 100-nanosecond precision.");
        }
        return checked((ulong)(totalNanos / 100));
    }

    private static bool ValidUniqueIds(IEnumerable<string> values) =>
        values.All(Has) && values.Distinct(StringComparer.Ordinal).Count() == values.Count();
}
