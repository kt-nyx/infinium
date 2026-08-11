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
    }

    public static void Validate(ListProviderBudgetRequest value)
    {
        if (value.ScopeKind is not ("operation" or "evidence-acquisition-run" or "analysis-run"
            or "provider-profile" or "provider-account" or "global")
            || string.IsNullOrWhiteSpace(value.ScopeId) || value.RequestedPageSize is 0 or > 100)
        {
            throw new InvalidDataException("Provider budget query is outside the closed scope/page contract.");
        }
    }

    public static void Validate(GetProviderReplayRequest value)
    {
        Require(value.OperationId?.Value, "operation_id");
        Require(value.RetainedResponseId, "retained_response_id");
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
    }

    public static void Validate(ProviderReplayPayload value)
    {
        Require(value.OperationId?.Value, "operation_id");
        Require(value.RetainedResponseId, "retained_response_id");
        Require(value.DependencyManifestId, "dependency_manifest_id");
        if (!Enum.IsDefined(value.ReplayState) || value.ReplayState is ProviderReplayState.Unspecified
            || value.NetworkPermitted)
        {
            throw new InvalidDataException("Retained-response replay is fail-closed and network-free.");
        }
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
        if (value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.CanonicalRequestFingerprintSha256.Length != 32
            || value.SettingsFingerprintSha256.Length != 32 || value.OutputSchemaFingerprintSha256.Length != 32
            || !Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKind.Unspecified
            || value.Limits is null)
        {
            throw new InvalidDataException("Provider submit confirmation is missing an exact replay or ownership binding.");
        }
        Validate(value.OperationKind, value.Limits);
        if (value.InputBoundProofStatus != InputBoundProofStatus.AuthorityRequired
            || value.InputBoundPolicyId != "unresolved-openai-responses-framing"
            || value.InputBoundPolicyVersion != "authority-required"
            || value.HasCanonicalRequestBytes || value.HasProvedInputTokenBound)
        {
            throw new InvalidDataException("Provider submit must retain the exact unresolved input-bound proof status.");
        }
    }

    public static void RequireDispatchAdmission(SubmitProviderOperationRequest value)
    {
        Validate(value);
        throw new NotSupportedException("Provider dispatch is blocked pending accepted local tokenizer/framing authority.");
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
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(field + " is required.");
        }
    }
}
