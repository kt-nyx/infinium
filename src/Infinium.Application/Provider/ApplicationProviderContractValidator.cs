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

    public static void Validate(GetProviderReplayRequest value)
    {
        Require(value.OperationId?.Value, "operation_id");
        Require(value.RetainedResponseId, "retained_response_id");
    }

    public static void Validate(SubmitProviderEnrollmentRequest value)
    {
        Require(value.ProfileId?.Value, "profile_id");
        Require(value.GenerationId?.Value, "generation_id");
        if (value.Provider != "openai" || value.Purpose != "responses"
            || string.IsNullOrWhiteSpace(value.DisplayLabel) || value.DisplayLabel.Length > 128)
        {
            throw new InvalidDataException("Provider enrollment is outside the closed non-secret profile contract.");
        }
    }

    public static void Validate(ProviderProfilePayload value)
    {
        Require(value.ProfileId?.Value, "profile_id");
        if (!Enum.IsDefined(value.LifecycleState) || value.LifecycleState == ProviderProfileLifecycleState.Unspecified
            || !Enum.IsDefined(value.VerificationState) || value.VerificationState == ProviderAvailabilityState.Unspecified)
        {
            throw new InvalidDataException("Provider profile contains an unknown numeric lifecycle or verification state.");
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
            || Has(value.UsageEntryId) || Has(value.SettlementId) || Has(value.ReplayEdgeId);
        if (blocked)
        {
            if (value.InputBoundPolicyId != "unresolved-openai-responses-framing"
                || value.InputBoundPolicyVersion != "authority-required"
                || value.HasCanonicalRequestBytes || value.HasProvedInputTokenBound
                || value.State != ProviderOperationLifecycleState.InputBoundBlocked || anyDownstream
                || AnyValue(value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
                    value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd)
                || value.SettlementState != ProviderSettlementState.NotStarted
                || value.ReplayState != ProviderReplayState.NotAvailable || value.UnresolvedHold)
            {
                throw new InvalidDataException("Authority-required operations may expose only the truthful pre-proof blocked state.");
            }
            return;
        }

        if (!value.HasCanonicalRequestBytes || !value.HasProvedInputTokenBound
            || value.CanonicalRequestBytes == 0 || value.ProvedInputTokenBound == 0)
        {
            throw new InvalidDataException("A non-blocked provider operation requires an explicit proved request bound.");
        }
        Require(value.OwnerId, "owner_id");
        Require(value.JobNodeId, "job_node_id");
        if (value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run"))
        {
            throw new InvalidDataException("Provider operation owner kind is unknown.");
        }
        ValidateOperationShape(value);
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
        if (unavailable)
        {
            if (anyBinding)
            {
                throw new InvalidDataException("Unavailable replay cannot fabricate retained response or dependency bindings.");
            }
            return;
        }
        Require(value.RetainedResponseId, "retained_response_id");
        Require(value.DependencyManifestId, "dependency_manifest_id");
        RequireReplayBindings(value);
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
        if (value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.CanonicalRequestFingerprintSha256.Length != 32
            || value.PromptFingerprintSha256.Length != 32
            || value.CanonicalRequestBody.IsEmpty || value.CanonicalRequestBody.Length > 65_536
            || value.SettingsFingerprintSha256.Length != 32 || value.OutputSchemaFingerprintSha256.Length != 32
            || !Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKind.Unspecified
            || value.Limits is null || value.DispatchDeadline is null
            || value.DispatchDeadline.UnixSeconds <= 0 || value.DispatchDeadline.Nanoseconds is < 0 or > 999_999_999
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
            || value.InputBoundPolicyVersion != "authority-required"
            || value.HasCanonicalRequestBytes || value.HasProvedInputTokenBound)
        {
            throw new InvalidDataException("Provider submit must retain the exact unresolved input-bound proof status.");
        }
        throw new NotSupportedException("Provider operation confirmation is blocked pending accepted local tokenizer/framing authority.");
    }

    public static void RequireDispatchAdmission(SubmitProviderOperationRequest value)
    {
        Validate(value);
    }

    private static void Validate(OptionalProviderQuantity value)
    {
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

    private static void ValidateOperationShape(ProviderOperationPayload value)
    {
        bool authorization = Has(value.AuthorizationId);
        bool attempt = value.AttemptId is not null;
        bool request = Has(value.RequestId);
        bool reservation = value.ReservationId is not null;
        bool fence = value.DispatchFenceId is not null;
        bool response = Has(value.ResponseRecordId);
        bool usage = Has(value.UsageEntryId);
        bool settlement = Has(value.SettlementId);
        bool replay = Has(value.ReplayEdgeId);
        bool reserved = value.ReservedNanoUsd is { HasValue: true, Value: > 0 };
        bool coherentUnresolvedDependencies = (!response && !usage && !replay)
            || (response && !usage && !replay) || (response && usage && replay);
        bool valid = value.State switch
        {
            ProviderOperationLifecycleState.Proposed => !authorization && !attempt && !request && !reservation
                && !fence && !response && !usage && !settlement && !replay,
            ProviderOperationLifecycleState.Confirmed => authorization && !attempt && !request && !reservation
                && !fence && !response && !usage && !settlement && !replay,
            ProviderOperationLifecycleState.Reserved or ProviderOperationLifecycleState.Assigned =>
                authorization && attempt && request && reservation && !fence && !response && !usage && !settlement && !replay
                && reserved,
            ProviderOperationLifecycleState.FinalGateAuthorized or ProviderOperationLifecycleState.TransportNotStarted =>
                authorization && attempt && request && reservation && fence && !response && !usage && !settlement && !replay && reserved,
            ProviderOperationLifecycleState.TransportMayHaveStarted => authorization && attempt && request && reservation
                && fence && !response && !usage && !settlement && !replay && reserved && value.DispatchCount.Value == 1,
            ProviderOperationLifecycleState.ResponseStaged => authorization && attempt && request && reservation
                && fence && response && !usage && !settlement && !replay && reserved && value.DispatchCount.Value == 1,
            ProviderOperationLifecycleState.Admitted or ProviderOperationLifecycleState.Rejected =>
                authorization && attempt && request && reservation && fence && response && usage && !settlement && replay && reserved,
            ProviderOperationLifecycleState.Settled => authorization && attempt && request && reservation && fence
                && response && usage && settlement && replay && reserved && !value.UnresolvedHold
                && value.SettlementState is ProviderSettlementState.Settled or ProviderSettlementState.FailedKnown or ProviderSettlementState.Overrun,
            ProviderOperationLifecycleState.UnresolvedHold => authorization && attempt && request && reservation && fence
                && settlement && reserved && coherentUnresolvedDependencies && value.UnresolvedHold
                && value.SettlementState == ProviderSettlementState.UnresolvedHold,
            _ => false,
        };
        bool preSettlement = value.State is not (ProviderOperationLifecycleState.Settled or ProviderOperationLifecycleState.UnresolvedHold);
        if (!valid || (preSettlement && value.SettlementState != ProviderSettlementState.NotStarted)
            || (value.ReplayState == ProviderReplayState.NotAvailable) != !replay)
        {
            throw new InvalidDataException("Provider operation lifecycle, reservation, usage, settlement, and replay identities contradict one another.");
        }
    }

    private static void RequireReplayBindings(ProviderReplayPayload value)
    {
        Require(value.InstallationSnapshotId, "installation_snapshot_id");
        Require(value.AnalysisContextId, "analysis_context_id");
        Require(value.EffectiveConfigurationId, "effective_configuration_id");
        Require(value.ResolvedInputManifestId, "resolved_input_manifest_id");
        Require(value.PromptId, "prompt_id");
        Require(value.OutputSchemaId, "output_schema_id");
        Require(value.AuthorizationId, "authorization_id");
        Require(value.RequestId, "request_id");
        Require(value.UsageEntryId, "usage_entry_id");
        Require(value.SettlementId, "settlement_id");
        Require(value.ReplayEdgeId, "replay_edge_id");
        if (value.ProfileId is null || value.GenerationId is null || value.CapabilitySnapshotId is null
            || value.PriceSnapshotId is null || value.AttemptId is null || value.ReservationId is null
            || value.DispatchFenceId is null || value.Limits is null || value.DispatchDeadline is null
            || value.PromptFingerprintSha256.Length != 32 || value.OutputSchemaFingerprintSha256.Length != 32
            || value.CanonicalRequestBytes.IsEmpty || value.CanonicalRequestBytes.Length > 65_536
            || value.CanonicalRequestFingerprintSha256.Length != 32 || value.SettingsFingerprintSha256.Length != 32
            || !Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKind.Unspecified
            || !System.Security.Cryptography.SHA256.HashData(value.CanonicalRequestBytes.Span)
                .AsSpan().SequenceEqual(value.CanonicalRequestFingerprintSha256.Span)
            || value.RetainedHoldNanoUsd < 0)
        {
            throw new InvalidDataException("Provider replay is missing an exact immutable request, reservation, fence, usage, settlement, or provenance binding.");
        }
        Validate(value.OperationKind, value.Limits);
        if ((ulong)value.CanonicalRequestBytes.Length > value.Limits.MaximumRequestBytes)
        {
            throw new InvalidDataException("Replay request bytes exceed the exact retained request limit.");
        }
    }
}
