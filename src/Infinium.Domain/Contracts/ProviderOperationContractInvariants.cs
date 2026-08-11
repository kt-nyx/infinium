namespace Infinium.Domain.Contracts;

public static class ProviderOperationContractInvariants
{
    public const long MaximumCanonicalRequestBytes = 65_536;
    public const long MaximumLocallyAdmittedInputTokens = 73_728;
    public const string LocalInputBoundProofStatus = "authority-required";
    public const string LocalInputBoundPolicyId = "unresolved-openai-responses-framing";
    public const string LocalInputBoundPolicyVersion = "authority-required";

    public static void RequireLocalInputBoundProof(long canonicalRequestBytes)
    {
        if (canonicalRequestBytes <= 0 || canonicalRequestBytes > MaximumCanonicalRequestBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(canonicalRequestBytes));
        }
        throw new NotSupportedException(
            "WP1 has no accepted repository-local tokenizer or framing grammar from which to prove the framing-inclusive input-token bound.");
    }

    public static void ValidateBlockedInputBoundProof(ProviderInputBoundProofContract proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (proof.PolicyId != LocalInputBoundPolicyId
            || proof.PolicyVersion != LocalInputBoundPolicyVersion
            || proof.Status != ProviderInputBoundProofState.AuthorityRequired)
        {
            throw new InvalidOperationException("The unresolved WP1 input-bound proof must remain explicitly authority-required with no fabricated byte or token bound.");
        }
    }

    public static void RequireDispatchableInputBoundProof(ProviderInputBoundProofContract proof)
    {
        ValidateBlockedInputBoundProof(proof);
        throw new NotSupportedException(
            "Provider dispatch is blocked because no accepted repository-local tokenizer/framing proof exists.");
    }

    public static long CalculateComponentNanoUsd(long tokens, ProviderPriceRuleContract rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (tokens < 0 || rule.NumeratorNanoUsd < 0 || rule.DenominatorTokens <= 0)
        {
            throw new InvalidOperationException("Price components require finite non-negative quantities and a positive denominator.");
        }
        RequireClosedPriceRule(rule);
        long product = checked(tokens * rule.NumeratorNanoUsd);
        long quotient = product / rule.DenominatorTokens;
        long remainder = product % rule.DenominatorTokens;
        return checked(quotient + (remainder == 0 ? 0 : 1));
    }

    public static void Validate(ProviderOperationKind kind, ProviderFiniteLimitsContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ProviderFiniteLimitsContract ceiling = kind switch
        {
            ProviderOperationKind.TransportQualification => new(16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000),
            ProviderOperationKind.SourceClaimExtraction or ProviderOperationKind.CandidateInvestigation =>
                new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000),
            _ => throw new InvalidOperationException("Provider operation kind must be explicit."),
        };
        if (value.MaximumRequestBytes is <= 0 || value.MaximumRequestBytes > ceiling.MaximumRequestBytes
            || value.MaximumInputTokens is <= 0 || value.MaximumInputTokens > ceiling.MaximumInputTokens
            || value.MaximumOutputTokens is <= 0 || value.MaximumOutputTokens > ceiling.MaximumOutputTokens
            || value.MaximumRawResponseBytes is <= 0 || value.MaximumRawResponseBytes > ceiling.MaximumRawResponseBytes
            || value.MaximumDispatchCount != 1
            || value.MaximumCalculatedNanoUsd is <= 0 || value.MaximumCalculatedNanoUsd > ceiling.MaximumCalculatedNanoUsd
            || value.DeadlineMilliseconds is <= 0 || value.DeadlineMilliseconds > ceiling.DeadlineMilliseconds)
        {
            throw new InvalidOperationException($"Provider limits exceed the accepted seven-dimensional {kind} ceiling.");
        }
    }

    public static void Validate(EffectiveScanConfigurationV2Document value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.EffectiveScanConfigurationV2SchemaId);
        Validate(ProviderOperationKind.SourceClaimExtraction, value.Limits);
        if (value.Model != "gpt-5.6-sol" || value.ReasoningEffort != "medium"
            || value.ReasoningContext != "current_turn" || value.ReasoningMode != "standard"
            || value.Store || value.ServiceTier != "default" || value.Background || value.Stream
            || value.ToolChoice != "none" || value.ToolCount != 0 || value.Truncation != "disabled"
            || value.PromptCacheMode != "explicit" || value.HasPromptCacheKey
            || value.HasPromptCacheBreakpoint)
        {
            throw new InvalidOperationException("Provider-active configuration must use the exact stateless, cache-off M1 profile.");
        }
        string[] expected = ["hosted-search", "nexus", "loot"];
        if (!expected.SequenceEqual(value.NotUsedBoundaries, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Provider-active v2 must retain exactly the three non-provider not-used boundaries.");
        }
    }

    public static void Validate(ProviderAccessProfileDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.ProviderAccessProfileSchemaId);
        RequireExplicit(value.LifecycleState, nameof(value.LifecycleState));
        RequireExplicit(value.VerificationState, nameof(value.VerificationState));
        if (value.GenerationOrdinal <= 0 || value.RevocationEpoch < 0
            || value.Provider != "openai" || value.Purpose != "responses"
            || string.IsNullOrWhiteSpace(value.DisplayLabel)
            || value.RecoveryDisposition is not ("not-required" or "required" or "unavailable")
            || value.CleanupDisposition is not ("not-requested" or "pending" or "confirmed" or "failed"))
        {
            throw new InvalidOperationException("Provider access-profile metadata is not a closed non-secret M1 state.");
        }
        bool idsAbsent = value.AccountIdentityId is null && value.BillingScopeIdentityId is null
            && value.CapabilitySnapshotId is null;
        bool idsPresent = value.AccountIdentityId is not null && value.BillingScopeIdentityId is not null
            && value.CapabilitySnapshotId is not null;
        bool validShape = value.LifecycleState switch
        {
            ProviderProfileState.PendingEnrollment => idsAbsent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.NotApplicable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileState.ActiveUnverified => idsPresent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileState.ActiveVerified or ProviderProfileState.Replacing => idsPresent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Available
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileState.Disabled => idsPresent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileState.DeletePending => idsPresent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required"
                && value.CleanupDisposition == "pending",
            ProviderProfileState.Deleted => idsAbsent && value.IntentId is null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "confirmed",
            ProviderProfileState.SecureStoreUnavailable => (idsAbsent || idsPresent) && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "unavailable"
                && value.CleanupDisposition is "not-requested" or "failed",
            ProviderProfileState.RecoveryRequired => (idsAbsent || idsPresent) && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "required"
                && value.CleanupDisposition is "not-requested" or "failed",
            _ => false,
        };
        if (!validShape)
        {
            throw new InvalidOperationException("Provider access-profile identities contradict lifecycle, verification, recovery, or cleanup state.");
        }
    }

    public static void Validate(ProviderOperationDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.ProviderOperationSchemaId);
        Validate(value.OperationKind, value.Limits);
        Validate(value.Usage);
        Validate(value.CapabilitySnapshot);
        Validate(value.PriceSnapshot);
        ValidateBlockedInputBoundProof(value.InputBoundProof);
        RequireExplicit(value.State, nameof(value.State));
        if (value.State != ProviderOperationState.InputBoundBlocked)
        {
            throw new InvalidOperationException("An authority-required input-bound proof may retain only the truthful pre-proof blocked operation state.");
        }
        if (value.RevocationEpoch < 0
            || value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.TransportState is not ("not-started" or "may-have-started" or "started" or "completed" or "failed-known" or "ambiguous")
            || value.ReceiptState is not ("not-available" or "staged" or "validated" or "rejected" or "unresolved")
            || value.SettlementState is not ("not-started" or "settled" or "unresolved-hold" or "failed-known" or "overrun")
            || value.ReplayState is not ("not-available" or "retained-response" or "audit-only"))
        {
            throw new InvalidOperationException("Provider operation contains an unsupported owner or terminal projection state.");
        }
        ValidateOperationStateShape(value);
    }

    public static void ValidateTransition(ProviderOperationState from, ProviderOperationState to)
    {
        bool legal = from == ProviderOperationState.Proposed && to == ProviderOperationState.InputBoundBlocked;
        if (!legal)
        {
            throw new InvalidOperationException($"Provider operation transition {from}->{to} is not in the closed lifecycle graph.");
        }
    }

    public static void Validate(ProviderResponseDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.ProviderResponseSchemaId);
        RequireExplicit(value.State, nameof(value.State));
        RequireExplicit(value.ValidationState, nameof(value.ValidationState));
        RequireExplicit(value.AdmissionState, nameof(value.AdmissionState));
        Validate(value.Usage);
        if (value.MaximumRawResponseBytes is <= 0 or > 1_048_576)
        {
            throw new InvalidOperationException("Provider response must retain the operation-specific raw-response ceiling.");
        }
        bool noResponse = value.State == ProviderResponseState.Cancelled;
        bool hasRetainedResponse = value.RawResponsePayload is not null && value.RawResponseBytes is > 0
            && value.RawResponseBytes <= value.MaximumRawResponseBytes;
        bool hasHeaderReceipt = value.ResponseHeadersPayload is not null && value.ResponseHeadersBytes is > 0 and <= 65_536;
        bool headerAvailabilityCoherent = value.ResponseHeadersAvailability == ProviderAvailabilityState.Available
            ? hasHeaderReceipt : value.ResponseHeadersAvailability == ProviderAvailabilityState.Unavailable && !hasHeaderReceipt;
        bool requestIdAvailabilityCoherent = value.ProviderRequestIdAvailability == ProviderAvailabilityState.Available
            ? !string.IsNullOrWhiteSpace(value.ProviderRequestId)
            : value.ProviderRequestIdAvailability == ProviderAvailabilityState.Unavailable && value.ProviderRequestId is null;
        bool completed = value.State == ProviderResponseState.Completed;
        bool failedSemantic = value.State is ProviderResponseState.Refusal or ProviderResponseState.Incomplete
            or ProviderResponseState.Failed or ProviderResponseState.Queued or ProviderResponseState.InProgress
            or ProviderResponseState.Malformed or ProviderResponseState.Oversized or ProviderResponseState.Mismatched
            or ProviderResponseState.Unknown;
        bool semanticStatesCoherent = completed
            ? value.ValidationState is ProposalAdmissionState.Proposed or ProposalAdmissionState.Admitted or ProposalAdmissionState.Rejected
                && value.AdmissionState is ProposalAdmissionState.Proposed or ProposalAdmissionState.Admitted or ProposalAdmissionState.Rejected
            : noResponse
                ? value.ValidationState == ProposalAdmissionState.Unavailable && value.AdmissionState == ProposalAdmissionState.Unavailable
                : value.ValidationState is ProposalAdmissionState.Rejected or ProposalAdmissionState.Abstained or ProposalAdmissionState.Unavailable
                    && value.AdmissionState is ProposalAdmissionState.Rejected or ProposalAdmissionState.Abstained or ProposalAdmissionState.Unavailable;
        bool noReasonCodes = value.RefusalCode is null && value.IncompleteReason is null && value.ErrorCode is null;
        bool cancelledUsage = value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 0 }
            && new[] { value.Usage.InputTokens, value.Usage.OutputTokens, value.Usage.TotalTokens, value.Usage.ReasoningTokens,
                value.Usage.CacheReadTokens, value.Usage.CacheWriteTokens, value.Usage.PricedToolCalls,
                value.Usage.CalculatedNanoUsd }.All(x => x.Availability is ProviderAvailabilityState.Unavailable
                    or ProviderAvailabilityState.NotApplicable && x.Value is null)
            && value.Usage.BillingAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.RateAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.CreditAvailability == ProviderAvailabilityState.Unavailable;
        bool completedUsage = value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 1 }
            && new[] { value.Usage.InputTokens, value.Usage.OutputTokens, value.Usage.TotalTokens, value.Usage.ReasoningTokens,
                value.Usage.CacheReadTokens, value.Usage.CacheWriteTokens, value.Usage.PricedToolCalls,
                value.Usage.CalculatedNanoUsd }.All(x => x.Availability == ProviderAvailabilityState.Available && x.Value is >= 0);
        completedUsage = completedUsage && value.Usage.TotalTokens.Value == checked(value.Usage.InputTokens.Value + value.Usage.OutputTokens.Value);
        bool dispatchedUsage = value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 1 };
        bool typedUnavailable = value.RateLimitFacts.Count == 0 && value.Usage.RateAvailability == ProviderAvailabilityState.Unavailable;
        bool typedRateFacts = value.RateLimitFacts.Count is > 0 and <= 16
            && value.RateLimitFacts.All(ValidRateLimitFact)
            && value.Usage.RateAvailability == ProviderAvailabilityState.Available;
        bool mayLackDecodedEnvelope = value.State is ProviderResponseState.Malformed or ProviderResponseState.Oversized
            or ProviderResponseState.Queued or ProviderResponseState.InProgress or ProviderResponseState.Unknown;
        if ((noResponse && (value.RawResponsePayload is not null || value.RawResponseBytes is not null
                || value.ResponseHeadersPayload is not null || value.ResponseHeadersBytes is not null || value.HttpStatus is not null
                || value.ProviderResponseId is not null || value.ProviderRequestId is not null
                || value.ReturnedModel is not null || value.ReturnedServiceTier is not null))
            || ((value.RawResponsePayload is null) != (value.RawResponseBytes is null))
            || ((value.ResponseHeadersPayload is null) != (value.ResponseHeadersBytes is null))
            || !headerAvailabilityCoherent || !requestIdAvailabilityCoherent
            || (value.RawResponsePayload is not null && !hasRetainedResponse)
            || (value.ResponseHeadersPayload is not null && !hasHeaderReceipt)
            || (!noResponse && !mayLackDecodedEnvelope && !hasRetainedResponse)
            || (!noResponse && !mayLackDecodedEnvelope && (value.ReturnedModel != "gpt-5.6-sol" || value.ReturnedServiceTier != "default"))
            || (noResponse && (!cancelledUsage || !noReasonCodes))
            || (completed && (!completedUsage || !noReasonCodes))
            || (!noResponse && !completed && !dispatchedUsage)
            || (value.State == ProviderResponseState.Refusal && (string.IsNullOrWhiteSpace(value.RefusalCode)
                || value.IncompleteReason is not null || value.ErrorCode is not null))
            || (value.State == ProviderResponseState.Incomplete && (string.IsNullOrWhiteSpace(value.IncompleteReason)
                || value.RefusalCode is not null || value.ErrorCode is not null))
            || (failedSemantic && value.State is not (ProviderResponseState.Refusal or ProviderResponseState.Incomplete)
                && (value.RefusalCode is not null || value.IncompleteReason is not null))
            || (!typedUnavailable && !typedRateFacts)
            || !semanticStatesCoherent
            || value.RequestedModel != "gpt-5.6-sol" || value.RequestedServiceTier != "default"
            || value.ReasoningContext != "current_turn" || value.ReasoningMode != "standard"
            || value.PromptCacheMode != "explicit")
        {
            throw new InvalidOperationException("Provider response does not retain the exact requested M1 profile.");
        }
    }

    public static void Validate(ProviderExecutionInputDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.ProviderExecutionInputSchemaId);
        Validate(value.OperationKind, value.Limits);
        Validate(value.CapabilitySnapshot);
        Validate(value.PriceSnapshot);
        ValidateBlockedInputBoundProof(value.InputBoundProof);
        if (value.DispatchAdmission != "blocked-authority-required")
        {
            throw new InvalidOperationException("Provider execution input must retain the authority-required dispatch block.");
        }
    }

    public static void RequireDispatchAdmission(ProviderExecutionInputDocument value)
    {
        Validate(value);
        RequireDispatchableInputBoundProof(value.InputBoundProof);
    }

    public static void Validate(RunOutputV2Document value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.RunOutputV2SchemaId);
        if (value.ContainsRawTransport || value.ContainsSecret || value.ProviderOperations.Count > 3
            || !Unique(value.ProviderOperations.Select(x => x.OperationId))
            || !Unique(value.EvidenceAcquisitionRunIds) || !Unique(value.CapabilityDriftIds)
            || !Unique(value.PriceDriftIds) || !BoundedUniqueText(value.ProviderGaps)
            || value.ProviderOperations.Any(x => x.Availability is not ("not-used" or "unavailable" or "blocked")))
        {
            throw new InvalidOperationException("Run output v2 cannot embed raw provider transport or secrets.");
        }
        foreach (ProviderPublicationReferenceContract publication in value.ProviderOperations)
        {
            bool none = publication.Availability is "not-used" or "unavailable";
            bool blocked = publication.Availability == "blocked";
            bool qualification = publication.OperationKind == ProviderOperationKind.TransportQualification;
            if (none != (publication.OperationId is null)
                || (none && (publication.Live || publication.OperationKind is not null || publication.AcquisitionRunId is not null
                    || publication.AuthorizationId is not null || publication.ResponseId is not null
                    || publication.AdmissionId is not null || publication.UsageEntryId is not null
                    || publication.SettlementId is not null || publication.ReplayEdgeId is not null))
                || publication.Live
                || (qualification && publication.AcquisitionRunId is not null)
                || (blocked && (publication.OperationKind is null || publication.AuthorizationId is not null
                    || publication.ResponseId is not null || publication.AdmissionId is not null
                    || publication.UsageEntryId is not null || publication.SettlementId is not null
                    || publication.ReplayEdgeId is not null)))
            {
                throw new InvalidOperationException("Run-output provider publication contradicts its availability or operation kind.");
            }
        }
    }

    public static void Validate(CliSummaryV2Document value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CliSummaryV2SchemaId);
        if (value.ContainsRawTransport || value.ContainsSecret
            || value.ProviderState is not ("not-used" or "unavailable" or "blocked")
            || value.ReplayState is not ("not-available" or "retained-response" or "audit-only")
            || !BoundedUniqueText(value.Gaps)
            || new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
                value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd }.Any(x => !ValidQuantity(x)))
        {
            throw new InvalidOperationException("CLI summary v2 contains an invalid provider projection.");
        }
        bool noProvider = value.ProviderState is "not-used" or "unavailable";
        bool anyValue = new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
            value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd }
            .Any(quantity => quantity.Value is not null);
        bool blockedShape = value.ProviderState == "blocked" && !value.UnresolvedHold
            && value.ReplayState == "not-available" && !anyValue;
        if ((noProvider && (anyValue
            || value.OutputTokens.Value is not null || value.ReasoningTokens.Value is not null
            || value.UnresolvedHold || value.ReplayState != "not-available"))
            || (value.ProviderState == "blocked" && !blockedShape))
        {
            throw new InvalidOperationException("Not-used and unavailable CLI projections cannot publish fabricated usage, hold, or replay values.");
        }
    }

    public static void Validate(SourceClaimExtractionDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.SourceClaimExtractionSchemaId);
        if (value.PassageIds.Count is 0 or > 64 || !Unique(value.PassageIds)
            || value.ClaimProposals.Count > 64 || !Unique(value.ClaimProposals.Select(x => x.ProposalId))
            || value.ClaimProposals.Any(x => !Enum.IsDefined(x.State) || x.State == ProposalAdmissionState.Unspecified
                || string.IsNullOrWhiteSpace(x.Claim) || string.IsNullOrWhiteSpace(x.Reason)
                || !value.PassageIds.Contains(x.PassageId) || !Unique(x.ConditionIds)
                || (x.State == ProposalAdmissionState.Admitted
                    && (value.ValidationIds.Count == 0 || value.ApplicationLinkIds.Count == 0)))
            || !BoundedUniqueText(value.Abstentions) || !BoundedUniqueText(value.Gaps)
            || !Unique(value.ContradictionEvidenceIds) || !Unique(value.ValidationIds)
            || !Unique(value.ApplicationLinkIds))
        {
            throw new InvalidOperationException("Source-claim extraction must retain unique passages and explicit proposal states.");
        }
    }

    public static void Validate(CandidateInvestigationDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CandidateInvestigationSchemaId);
        if (value.ParticipantIds.Count is 0 or > 32 || value.ParticipantIds.Count != value.ParticipantRoles.Count
            || !Unique(value.ParticipantIds) || value.ParticipantRoles.Any(string.IsNullOrWhiteSpace)
            || value.HypothesisProposals.Count > 64 || !Unique(value.HypothesisProposals.Select(x => x.ProposalId))
            || value.HypothesisProposals.Any(x => !Enum.IsDefined(x.State) || x.State == ProposalAdmissionState.Unspecified
                || string.IsNullOrWhiteSpace(x.Hypothesis) || string.IsNullOrWhiteSpace(x.Reason)
                || x.CandidateId != value.CandidateId
                || !Unique(x.SupportingEvidenceIds) || !Unique(x.ContradictingEvidenceIds)
                || x.SupportingEvidenceIds.Intersect(x.ContradictingEvidenceIds).Any()
                || x.SupportingEvidenceIds.Concat(x.ContradictingEvidenceIds).Any(id => !value.EvidenceIds.Contains(id))
                || !BoundedUniqueText(x.MissingInformation))
            || !Unique(value.CausalPathIds) || !Unique(value.EvidenceIds)
            || !BoundedUniqueText(value.Abstentions) || !BoundedUniqueText(value.Gaps)
            || !Unique(value.ValidationIds) || !Unique(value.AdmissionLinkIds))
        {
            throw new InvalidOperationException("Candidate investigation must retain paired participants/roles and explicit proposal states.");
        }
    }

    private static void Validate(ProviderUsageContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ProviderQuantityContract[] quantities = [value.DispatchCount, value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
            value.CacheReadTokens, value.CacheWriteTokens, value.PricedToolCalls, value.CalculatedNanoUsd];
        if (quantities.Any(x => !ValidQuantity(x))
            || value.DispatchCount.Value > 1 || value.InputTokens.Value > 73_728 || value.OutputTokens.Value > 4_096
            || value.TotalTokens.Value > 77_824
            || value.ReasoningTokens.Value > 4_096 || value.CalculatedNanoUsd.Value > 600_000_000
            || value.CacheReadTokens.Value is not (null or 0) || value.CacheWriteTokens.Value is not (null or 0)
            || value.PricedToolCalls.Value is not (null or 0))
        {
            throw new InvalidOperationException("The M1 cache-off/tool-free usage vector is invalid.");
        }
        RequireExplicit(value.BillingAvailability, nameof(value.BillingAvailability));
        RequireExplicit(value.RateAvailability, nameof(value.RateAvailability));
        RequireExplicit(value.CreditAvailability, nameof(value.CreditAvailability));
    }

    private static bool ValidQuantity(ProviderQuantityContract value) =>
        Enum.IsDefined(value.Availability)
        && value.Availability != ProviderAvailabilityState.Unspecified
        && (value.Availability == ProviderAvailabilityState.Available
            ? value.Value is >= 0
            : value.Value is null);

    private static void Validate(ProviderCapabilitySnapshotContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Provider != "openai" || value.Model != "gpt-5.6-sol" || value.ServiceTier != "default"
            || value.ReasoningEffort != "medium" || value.ReasoningContext != "current_turn"
            || value.ReasoningMode != "standard" || value.Store || value.Background || value.Stream
            || value.ToolChoice != "none" || value.ToolCount != 0 || value.Truncation != "disabled"
            || value.PromptCacheMode != "explicit" || value.HasPromptCacheKey || value.HasPromptCacheBreakpoint
            || value.MaximumContextTokens <= 0 || string.IsNullOrWhiteSpace(value.Revision))
        {
            throw new InvalidOperationException("Capability snapshot is not the closed provider-active M1 profile.");
        }
    }

    private static void Validate(ProviderPriceSnapshotContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Provider != "openai" || value.Model != "gpt-5.6-sol" || value.ServiceTier != "default"
            || value.Currency != "USD" || string.IsNullOrWhiteSpace(value.Revision)
            || value.Rules.Count is 0 or > 8 || !Unique(value.Rules.Select(x => x.RuleId)))
        {
            throw new InvalidOperationException("Price snapshot is not a closed finite M1 snapshot.");
        }
        foreach (ProviderPriceRuleContract rule in value.Rules)
        {
            RequireClosedPriceRule(rule);
        }
    }

    public static void ValidateOperationStateShape(ProviderOperationDocument value)
    {
        bool anyDownstream = value.AuthorizationId is not null || value.AttemptId is not null || value.RequestId is not null
            || value.ReservationId is not null || value.DispatchFenceId is not null || value.SettingsFingerprint is not null
            || value.OutputSchemaFingerprint is not null || value.RequestFingerprint is not null;
        bool blockedUsage = value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 0 }
            && new[] { value.Usage.InputTokens, value.Usage.OutputTokens, value.Usage.TotalTokens, value.Usage.ReasoningTokens,
                value.Usage.CacheReadTokens, value.Usage.CacheWriteTokens, value.Usage.PricedToolCalls,
                value.Usage.CalculatedNanoUsd }.All(x => x.Availability == ProviderAvailabilityState.Unavailable && x.Value is null)
            && value.Usage.BillingAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.RateAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.CreditAvailability == ProviderAvailabilityState.Unavailable;
        if (value.State != ProviderOperationState.InputBoundBlocked || anyDownstream || !blockedUsage
            || value.TransportState != "not-started" || value.ReceiptState != "not-available"
            || value.SettlementState != "not-started" || value.ReplayState != "not-available")
        {
            throw new InvalidOperationException("Provider operation state contradicts its reachable identities or terminal projections.");
        }
    }

    private static bool ValidRateLimitFact(ProviderRateLimitFactContract value) =>
        value.Scope is "request" or "project" or "organization" or "model"
        && value.Dimension is "requests" or "input-tokens" or "output-tokens" or "total-tokens"
        && Enum.IsDefined(value.Availability) && value.Availability != ProviderAvailabilityState.Unspecified
        && (value.Availability == ProviderAvailabilityState.Available
            ? value.Limit is >= 0 && value.Remaining is >= 0 && value.Remaining <= value.Limit
            : value.Limit is null && value.Remaining is null && value.ResetsAt is null);

    private static void RequireClosedPriceRule(ProviderPriceRuleContract rule)
    {
        if (rule.Provider != "openai" || rule.Model != "gpt-5.6-sol"
            || rule.ServiceTier != "default" || rule.ContextBand != "standard-under-272k"
            || rule.CacheClass is not ("ordinary-input" or "cache-write" or "cache-read" or "none")
            || rule.TokenClass is not ("input" or "output" or "reasoning")
            || rule.ToolClass != "none" || rule.Region != "global" || rule.Currency != "USD"
            || string.IsNullOrWhiteSpace(rule.Revision))
        {
            throw new InvalidOperationException("Price rules must use the closed M1 provider price dimensions.");
        }
    }

    private static void RequireHeader(string schemaId, string schemaVersion, string expected)
    {
        if (schemaId != expected || schemaVersion != "1")
        {
            throw new InvalidOperationException($"Contract must bind {expected} schema version 1.");
        }
    }

    private static void RequireExplicit<T>(T value, string name) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)
            || Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            throw new InvalidOperationException($"{name} must be explicit.");
        }
    }

    private static bool Unique<T>(IEnumerable<T> values) =>
        values.Distinct().Count() == values.Count();

    private static bool BoundedUniqueText(IReadOnlyList<string> values) =>
        values.Count <= 64 && values.All(x => !string.IsNullOrWhiteSpace(x))
        && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
}
