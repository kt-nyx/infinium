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
        if (string.IsNullOrWhiteSpace(value.ConfigurationId.Value)
            || string.IsNullOrWhiteSpace(value.LocalConfigurationV1Id.Value)
            || value.LocalConfigurationV1Fingerprint.Value.Length != 64
            || value.LocalConfigurationV1Provenance != "asserted-retained-v1-identity"
            || string.IsNullOrWhiteSpace(value.AccessProfileId.Value)
            || string.IsNullOrWhiteSpace(value.GenerationId.Value)
            || value.Model != "gpt-5.6-sol" || value.ReasoningEffort != "medium"
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
        if (string.IsNullOrWhiteSpace(value.ProfileId.Value) || string.IsNullOrWhiteSpace(value.GenerationId.Value)
            || value.GenerationOrdinal <= 0 || value.RevocationEpoch < 0
            || value.Provider != "openai" || value.Purpose != "responses"
            || string.IsNullOrWhiteSpace(value.DisplayLabel)
            || value.RecordedAt.Value == default
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
            ProviderProfileState.ActiveVerified => idsPresent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Available
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileState.Replacing or ProviderProfileState.Disabled => idsPresent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required" && value.CleanupDisposition == "not-requested",
            ProviderProfileState.DeletePending => idsPresent && value.IntentId is not null
                && value.VerificationState == ProviderAvailabilityState.Unavailable
                && value.RecoveryDisposition == "not-required"
                && value.CleanupDisposition is "pending" or "failed",
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
        if (string.IsNullOrWhiteSpace(value.OperationId.Value)
            || string.IsNullOrWhiteSpace(value.OwnerId.Value)
            || string.IsNullOrWhiteSpace(value.JobNodeId.Value)
            || string.IsNullOrWhiteSpace(value.ProfileId.Value)
            || string.IsNullOrWhiteSpace(value.GenerationId.Value)
            || value.RevocationEpoch < 0
            || value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.OperationKind == ProviderOperationKind.SourceClaimExtraction && value.OwnerKind != "evidence-acquisition-run"
            || value.OperationKind is ProviderOperationKind.TransportQualification or ProviderOperationKind.CandidateInvestigation
                && value.OwnerKind != "analysis-run"
            || string.IsNullOrWhiteSpace(value.CommandId.Value)
            || string.IsNullOrWhiteSpace(value.InstallationSnapshotId.Value)
            || string.IsNullOrWhiteSpace(value.AnalysisContextId.Value)
            || string.IsNullOrWhiteSpace(value.EffectiveConfigurationId.Value)
            || string.IsNullOrWhiteSpace(value.ResolvedInputManifestId.Value)
            || string.IsNullOrWhiteSpace(value.PromptId.Value)
            || value.PromptFingerprint.Value.Length != 64
            || string.IsNullOrWhiteSpace(value.OutputSchemaId.Value)
            || value.OutputSchemaFingerprint.Value.Length != 64
            || value.RequestFingerprint.Value.Length != 64
            || string.IsNullOrWhiteSpace(value.CanonicalRequestPayload.Identity.Value)
            || value.CanonicalRequestPayload.Fingerprint.Value.Length != 64
            || value.SettingsFingerprint.Value.Length != 64
            || value.CanonicalRequestBytes <= 0 || value.CanonicalRequestBytes > value.Limits.MaximumRequestBytes
            || value.CanonicalRequestPayload.Fingerprint != value.RequestFingerprint
            || value.RequestedAt.Value > value.ConfirmedAt.Value
            || value.ConfirmedAt.Value >= value.DispatchDeadline.Value
            || value.DispatchDeadline.Value - value.ConfirmedAt.Value
                > TimeSpan.FromMilliseconds(value.Limits.DeadlineMilliseconds)
            || value.DispatchDeadline.Value - value.RequestedAt.Value
                > TimeSpan.FromMilliseconds(value.Limits.DeadlineMilliseconds)
            || value.RecordedAt.Value == default
            || value.CoordinatorFencingEpoch <= 0
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
        if (value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.OperationKind == ProviderOperationKind.SourceClaimExtraction && value.OwnerKind != "evidence-acquisition-run"
            || value.OperationKind is ProviderOperationKind.TransportQualification or ProviderOperationKind.CandidateInvestigation
                && value.OwnerKind != "analysis-run")
        {
            throw new InvalidOperationException("Provider response must bind one exact durable operation owner kind.");
        }
        Validate(value.OperationKind, value.Limits);
        RequireExplicit(value.Availability, nameof(value.Availability));
        RequireExplicit(value.State, nameof(value.State));
        RequireExplicit(value.ValidationState, nameof(value.ValidationState));
        RequireExplicit(value.AdmissionState, nameof(value.AdmissionState));
        ProviderAvailabilityState[] factAvailabilities =
        [
            value.RawResponseAvailability, value.ResponseHeadersAvailability, value.HttpStatusAvailability,
            value.ProviderResponseIdAvailability, value.ClientRequestIdAvailability,
            value.ProviderRequestIdAvailability, value.RefusalAvailability, value.IncompleteAvailability,
            value.ErrorAvailability, value.ReturnedModelAvailability, value.ReturnedServiceTierAvailability,
            value.BillingEvidenceAvailability,
        ];
        foreach (ProviderAvailabilityState availability in factAvailabilities)
        {
            RequireExplicit(availability, "provider response fact availability");
        }
        Validate(value.Usage);
        ValidateAvailability(value.RawResponseAvailability, value.RawResponsePayload, value.RawResponseBytes);
        ValidateAvailability(value.ResponseHeadersAvailability, value.ResponseHeadersPayload, value.ResponseHeadersBytes);
        ValidateAvailability(value.HttpStatusAvailability, value.HttpStatus);
        ValidateAvailability(value.ProviderResponseIdAvailability, value.ProviderResponseId);
        ValidateAvailability(value.ClientRequestIdAvailability, value.ClientRequestId);
        ValidateAvailability(value.ProviderRequestIdAvailability, value.ProviderRequestId);
        ValidateAvailability(value.RefusalAvailability, value.RefusalCode);
        ValidateAvailability(value.IncompleteAvailability, value.IncompleteReason);
        ValidateAvailability(value.ErrorAvailability, value.ErrorCode);
        ValidateAvailability(value.ReturnedModelAvailability, value.ReturnedModel);
        ValidateAvailability(value.ReturnedServiceTierAvailability, value.ReturnedServiceTier);
        ValidateAvailability(value.BillingEvidenceAvailability, value.BillingEvidencePayload);
        if (value.MaximumRawResponseBytes != value.Limits.MaximumRawResponseBytes
            || value.RawResponseBytes is not null && (value.RawResponseBytes <= 0
                || value.RawResponseBytes > value.MaximumRawResponseBytes)
            || value.OverflowObservedExcessBytes is not null
                && value.OverflowObservedExcessBytes != 1
            || value.ResponseHeadersBytes is not null && value.ResponseHeadersBytes is <= 0 or > 65_536)
        {
            throw new InvalidOperationException("Provider payload sizes must be positive and use the exact retained operation limit.");
        }
        if (value.RateLimitFacts.Count > 64 || value.RateLimitFacts.Any(x => !ValidRateLimitFact(x)
                || x.ObservedAt.Value < value.RecordedAt.Value)
            || value.RateLimitFacts.GroupBy(x => (x.Scope, x.Dimension)).Any(group => group.Count() != 1)
            || (value.Usage.RateAvailability == ProviderAvailabilityState.Available) != (value.RateLimitFacts.Count != 0)
            || (value.Usage.BillingAvailability == ProviderAvailabilityState.Available)
                != (value.BillingEvidenceAvailability == ProviderAvailabilityState.Available))
        {
            throw new InvalidOperationException("Provider rate and billing evidence must be exact, unique, and availability-bound.");
        }
        ValidateObservedUsage(value.OperationKind, value.Limits, value.Usage);
        if (value.Usage.Availability != value.Availability)
        {
            throw new InvalidOperationException("Provider response and usage availability must be identical.");
        }
        bool blockedUsage = value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 0 }
            && new[] { value.Usage.InputTokens, value.Usage.OutputTokens, value.Usage.TotalTokens, value.Usage.ReasoningTokens,
                value.Usage.CacheReadTokens, value.Usage.CacheWriteTokens, value.Usage.PricedToolCalls,
                value.Usage.CalculatedNanoUsd }.All(x => x.Availability == ProviderAvailabilityState.Unavailable && x.Value is null)
            && value.Usage.BillingAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.RateAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.CreditAvailability == ProviderAvailabilityState.Unavailable;
        if (string.IsNullOrWhiteSpace(value.ResponseRecordId.Value)
            || string.IsNullOrWhiteSpace(value.OperationId.Value)
            || value.MaximumRawResponseBytes <= 0 || value.MaximumRawResponseBytes > 1_048_576
            || value.RecordedAt.Value == default
            || value.RequestedModel != "gpt-5.6-sol" || value.RequestedServiceTier != "default"
            || value.ReasoningContext != "current_turn" || value.ReasoningMode != "standard"
            || value.PromptCacheMode != "explicit")
        {
            throw new InvalidOperationException("Provider response identity and requested profile are invalid.");
        }
        if (value.InputBoundProof.Status == ProviderInputBoundProofState.AuthorityRequired)
        {
            ValidateBlockedInputBoundProof(value.InputBoundProof);
            if (value.Availability != ProviderAvailabilityState.Unavailable
            || value.State != ProviderResponseState.Unknown
            || value.AuthorizationId is not null || value.RequestId is not null || value.DispatchFenceId is not null
            || value.RawResponsePayload is not null || value.RawResponseBytes is not null
            || value.ResponseHeadersPayload is not null || value.ResponseHeadersBytes is not null
            || factAvailabilities.Any(x => x != ProviderAvailabilityState.Unavailable)
            || value.HttpStatus is not null || value.ProviderResponseId is not null || value.ClientRequestId is not null
            || value.ProviderRequestId is not null
            || value.RefusalCode is not null || value.IncompleteReason is not null || value.ErrorCode is not null
            || value.ReturnedModel is not null || value.ReturnedServiceTier is not null
            || !blockedUsage || value.RateLimitFacts.Count != 0 || value.BillingEvidencePayload is not null
                || value.ValidationState != ProposalAdmissionState.Unavailable
                || value.AdmissionState != ProposalAdmissionState.Unavailable)
            {
                throw new InvalidOperationException("Before input-bound authority, provider response may retain only a truthful unavailable marker with no transport evidence.");
            }
            return;
        }
        bool cancelled = value.State == ProviderResponseState.Cancelled;
        if (value.InputBoundProof.Status != ProviderInputBoundProofState.Proved
            || string.IsNullOrWhiteSpace(value.InputBoundProof.PolicyId)
            || string.IsNullOrWhiteSpace(value.InputBoundProof.PolicyVersion)
            || (!cancelled && (value.AuthorizationId is null || value.RequestId is null || value.DispatchFenceId is null
                || value.Availability != ProviderAvailabilityState.Available))
            || (cancelled && (value.AuthorizationId is not null || value.RequestId is not null || value.DispatchFenceId is not null
                || value.Availability != ProviderAvailabilityState.Unavailable)))
        {
            throw new InvalidOperationException("A future provider response requires a proved policy and exact authorization/request/fence binding.");
        }
        ValidateFutureResponseState(value);
        throw new NotSupportedException("Proof-qualified provider responses are structurally modeled but unreachable until accepted input-bound authority changes the current runtime maturity gate.");
    }

    public static void Validate(ProviderExecutionInputDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.ProviderExecutionInputSchemaId);
        Validate(value.OperationKind, value.Limits);
        Validate(value.CapabilitySnapshot);
        Validate(value.PriceSnapshot);
        ValidateBlockedInputBoundProof(value.InputBoundProof);
        if (string.IsNullOrWhiteSpace(value.OperationId.Value)
            || value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || string.IsNullOrWhiteSpace(value.OwnerId.Value)
            || string.IsNullOrWhiteSpace(value.JobNodeId.Value)
            || string.IsNullOrWhiteSpace(value.CommandId.Value)
            || value.OperationKind == ProviderOperationKind.SourceClaimExtraction && value.OwnerKind != "evidence-acquisition-run"
            || value.OperationKind is ProviderOperationKind.TransportQualification or ProviderOperationKind.CandidateInvestigation
                && value.OwnerKind != "analysis-run"
            || string.IsNullOrWhiteSpace(value.InstallationSnapshotId.Value)
            || string.IsNullOrWhiteSpace(value.AnalysisContextId.Value)
            || string.IsNullOrWhiteSpace(value.EffectiveConfigurationId.Value)
            || string.IsNullOrWhiteSpace(value.ResolvedInputManifestId.Value)
            || string.IsNullOrWhiteSpace(value.ProfileId.Value)
            || string.IsNullOrWhiteSpace(value.GenerationId.Value)
            || string.IsNullOrWhiteSpace(value.PromptId.Value)
            || value.PromptFingerprint.Value.Length != 64
            || string.IsNullOrWhiteSpace(value.OutputSchemaId.Value)
            || value.OutputSchemaFingerprint.Value.Length != 64
            || value.CanonicalRequestFingerprint.Value.Length != 64
            || value.DispatchAdmission != "blocked-authority-required")
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
        if (string.IsNullOrWhiteSpace(value.RunId.Value)
            || string.IsNullOrWhiteSpace(value.LocalRunOutputV1.Identity.Value)
            || value.LocalRunOutputV1.Fingerprint.Value.Length != 64
            || string.IsNullOrWhiteSpace(value.EffectiveConfigurationV2Id.Value)
            || value.ContainsRawTransport || value.ContainsSecret || value.ProviderOperations.Count > 3
            || !Unique(value.ProviderOperations.Select(x => x.OperationId))
            || !Unique(value.ProviderOperations.Where(x => x.OperationKind is not null).Select(x => x.OperationKind))
            || !Unique(value.EvidenceAcquisitionRunIds) || !Unique(value.CapabilityDriftIds)
            || !Unique(value.PriceDriftIds) || !BoundedUniqueText(value.ProviderGaps)
            || value.ProviderOperations.Any(x => x.Availability is not ("not-used" or "unavailable" or "blocked" or "live")))
        {
            throw new InvalidOperationException("Run output v2 cannot embed raw provider transport or secrets.");
        }
        foreach (ProviderPublicationReferenceContract publication in value.ProviderOperations)
        {
            bool none = publication.Availability is "not-used" or "unavailable";
            bool blocked = publication.Availability == "blocked";
            bool live = publication.Availability == "live";
            bool sourceClaim = publication.OperationKind == ProviderOperationKind.SourceClaimExtraction;
            if (none != (publication.OperationId is null)
                || (none && (publication.Live || publication.OperationKind is not null || publication.AcquisitionRunId is not null
                    || publication.AuthorizationId is not null || publication.ResponseId is not null
                    || publication.AdmissionId is not null || publication.UsageEntryId is not null
                    || publication.SettlementId is not null || publication.ReplayEdgeId is not null))
                || (publication.Live != live)
                || (blocked && sourceClaim != (publication.AcquisitionRunId is not null))
                || (blocked && (publication.OperationKind is null || publication.AuthorizationId is not null
                    || publication.ResponseId is not null || publication.AdmissionId is not null
                    || publication.UsageEntryId is not null || publication.SettlementId is not null
                    || publication.ReplayEdgeId is not null
                    || publication.AcceptedInputBoundPolicyId is not null
                    || publication.AcceptedInputBoundPolicyVersion is not null
                    || publication.LiveAuthorizationId is not null))
                || (live && (publication.OperationId is null || publication.OperationKind is null
                    || publication.AuthorizationId is null || publication.LiveAuthorizationId is null
                    || publication.AuthorizationId != publication.LiveAuthorizationId
                    || string.IsNullOrWhiteSpace(publication.AcceptedInputBoundPolicyId)
                    || string.IsNullOrWhiteSpace(publication.AcceptedInputBoundPolicyVersion))))
            {
                throw new InvalidOperationException("Run-output provider publication contradicts its availability or operation kind.");
            }
            if (live)
            {
                throw new NotSupportedException("Live run-output publication is modeled but unreachable until an accepted input-bound policy and exact live authorization exist.");
            }
        }
    }

    public static void Validate(CliSummaryV2Document value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CliSummaryV2SchemaId);
        if (string.IsNullOrWhiteSpace(value.RunId.Value)
            || value.LocalCliSummaryV1Fingerprint.Value.Length != 64
            || value.ContainsRawTransport || value.ContainsSecret
            || value.ProviderState is not ("not-used" or "unavailable" or "blocked" or "live")
            || value.ReplayState is not ("not-available" or "retained-response" or "audit-only")
            || !BoundedUniqueText(value.Gaps)
            || new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
                value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd }.Any(x => !ValidQuantity(x)))
        {
            throw new InvalidOperationException("CLI summary v2 contains an invalid provider projection.");
        }
        if (value.ProviderState == "live")
        {
            if (string.IsNullOrWhiteSpace(value.AcceptedInputBoundPolicyId)
                || string.IsNullOrWhiteSpace(value.AcceptedInputBoundPolicyVersion)
                || value.LiveAuthorizationId is null
                || string.IsNullOrWhiteSpace(value.LiveAuthorizationId.Value))
            {
                throw new InvalidOperationException("Live CLI summary shape requires an accepted proof policy and exact live authorization binding.");
            }
            throw new NotSupportedException("Live CLI summary is modeled but unreachable while WP1 input-bound authority is deferred.");
        }
        if (value.AcceptedInputBoundPolicyId is not null || value.AcceptedInputBoundPolicyVersion is not null
            || value.LiveAuthorizationId is not null)
        {
            throw new InvalidOperationException("Non-live CLI summaries cannot carry live authorization or accepted-proof bindings.");
        }
        ProviderAvailabilityState expectedAvailability = value.ProviderState switch
        {
            "not-used" => ProviderAvailabilityState.NotUsed,
            "unavailable" or "blocked" => ProviderAvailabilityState.Unavailable,
            _ => ProviderAvailabilityState.Unspecified,
        };
        bool anyValue = new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
            value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd }
            .Any(quantity => quantity.Value is not null);
        bool exactAvailability = new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
            value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd }
            .All(quantity => quantity.Availability == expectedAvailability);
        if (anyValue || !exactAvailability || value.UnresolvedHold || value.ReplayState != "not-available")
        {
            throw new InvalidOperationException("Not-used and unavailable CLI projections cannot publish fabricated usage, hold, or replay values.");
        }
    }

    public static void Validate(SourceClaimExtractionDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.SourceClaimExtractionSchemaId);
        if (string.IsNullOrWhiteSpace(value.AcquisitionRunId.Value)
            || string.IsNullOrWhiteSpace(value.OperationId.Value)
            || string.IsNullOrWhiteSpace(value.SourceRevisionId.Value)
            || string.IsNullOrWhiteSpace(value.DeclaredPurpose)
            || value.PassageIds.Count is 0 or > 64 || !Unique(value.PassageIds) || !ValidIds(value.PassageIds)
            || value.OwnerKind != "evidence-acquisition-run" || value.OwnerId != value.AcquisitionRunId
            || string.IsNullOrWhiteSpace(value.ParentAnalysisRunId.Value)
            || string.IsNullOrWhiteSpace(value.ApplicationScopeId.Value)
            || string.IsNullOrWhiteSpace(value.CostAttributionScopeId.Value)
            || value.ClaimProposals.Count > 64 || !Unique(value.ClaimProposals.Select(x => x.ProposalId))
            || value.ClaimProposals.Any(x => !Enum.IsDefined(x.State) || x.State == ProposalAdmissionState.Unspecified
                || string.IsNullOrWhiteSpace(x.Claim) || string.IsNullOrWhiteSpace(x.Reason)
                || !value.PassageIds.Contains(x.PassageId) || !Unique(x.ConditionIds)
                || (x.State == ProposalAdmissionState.Admitted
                    && (value.ValidationIds.Count == 0 || value.ApplicationLinkIds.Count == 0)))
            || !BoundedUniqueText(value.Abstentions) || !BoundedUniqueText(value.Gaps)
            || !Unique(value.ContradictionEvidenceIds) || !Unique(value.ValidationIds)
            || !Unique(value.ApplicationLinkIds)
            || !AdmissionStatesMatch(value.AdmissionLinks,
                value.ClaimProposals.Select(x => new KeyValuePair<OpaqueId, ProposalAdmissionState>(x.ProposalId, x.State)))
            || !ValidAdmissionLinks(value.AdmissionLinks, value.OperationId, value.OwnerKind,
                value.OwnerId, value.SourceRevisionId, value.ClaimProposals.Select(x => x.ProposalId),
                value.ValidationIds, value.ApplicationLinkIds, declaredIdsAreAdmissions: false))
        {
            throw new InvalidOperationException("Source-claim extraction must retain unique passages and explicit proposal states.");
        }
    }

    public static void Validate(CandidateInvestigationDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CandidateInvestigationSchemaId);
        if (string.IsNullOrWhiteSpace(value.OperationId.Value)
            || string.IsNullOrWhiteSpace(value.OwnerId.Value)
            || string.IsNullOrWhiteSpace(value.AnalysisRunId.Value)
            || string.IsNullOrWhiteSpace(value.CandidateId.Value)
            || string.IsNullOrWhiteSpace(value.DependencyClosureId.Value)
            || value.ParticipantIds.Count is 0 or > 32 || value.ParticipantIds.Count != value.ParticipantRoles.Count
            || value.OwnerKind != "analysis-run" || value.OwnerId != value.AnalysisRunId
            || !Unique(value.ParticipantIds) || value.ParticipantRoles.Any(string.IsNullOrWhiteSpace)
            || value.HypothesisProposals.Count > 64 || !Unique(value.HypothesisProposals.Select(x => x.ProposalId))
            || value.HypothesisProposals.Any(x => !Enum.IsDefined(x.State) || x.State == ProposalAdmissionState.Unspecified
                || string.IsNullOrWhiteSpace(x.Hypothesis) || string.IsNullOrWhiteSpace(x.Reason)
                || x.CandidateId != value.CandidateId
                || !Unique(x.SupportingEvidenceIds) || !Unique(x.ContradictingEvidenceIds)
                || x.SupportingEvidenceIds.Intersect(x.ContradictingEvidenceIds).Any()
                || x.SupportingEvidenceIds.Concat(x.ContradictingEvidenceIds).Any(id => !value.EvidenceIds.Contains(id))
                || !BoundedUniqueText(x.MissingInformation)
                || (x.State == ProposalAdmissionState.Admitted
                    && (value.ValidationIds.Count == 0 || value.AdmissionLinkIds.Count == 0)))
            || !Unique(value.CausalPathIds) || !Unique(value.EvidenceIds)
            || !BoundedUniqueText(value.Abstentions) || !BoundedUniqueText(value.Gaps)
            || !Unique(value.ValidationIds) || !Unique(value.AdmissionLinkIds)
            || !AdmissionStatesMatch(value.AdmissionLinks,
                value.HypothesisProposals.Select(x => new KeyValuePair<OpaqueId, ProposalAdmissionState>(x.ProposalId, x.State)))
            || !ValidAdmissionLinks(value.AdmissionLinks, value.OperationId, value.OwnerKind,
                value.OwnerId, value.CandidateId, value.HypothesisProposals.Select(x => x.ProposalId),
                value.ValidationIds, value.AdmissionLinkIds, declaredIdsAreAdmissions: true))
        {
            throw new InvalidOperationException("Candidate investigation must retain paired participants/roles and explicit proposal states.");
        }
    }

    private static void Validate(ProviderUsageContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireExplicit(value.Availability, nameof(value.Availability));
        RequireExplicit(value.ReceiptState, nameof(value.ReceiptState));
        ProviderQuantityContract[] quantities = [value.DispatchCount, value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
            value.CacheReadTokens, value.CacheWriteTokens, value.PricedToolCalls, value.CalculatedNanoUsd];
        if (quantities.Any(x => !ValidQuantity(x))
            || value.DispatchCount.Value > 2 || value.InputTokens.Value > 147_456 || value.OutputTokens.Value > 8_192
            || value.TotalTokens.Value > 155_648
            || value.ReasoningTokens.Value > 8_192 || value.CalculatedNanoUsd.Value > 1_200_000_000
            || value.CacheReadTokens.Value > 147_456 || value.CacheWriteTokens.Value > 147_456
            || value.PricedToolCalls.Value > 64
            || value.TotalTokens.Availability == ProviderAvailabilityState.Available
                && (value.InputTokens.Availability != ProviderAvailabilityState.Available
                    || value.OutputTokens.Availability != ProviderAvailabilityState.Available
                    || value.TotalTokens.Value != checked(value.InputTokens.Value + value.OutputTokens.Value))
            || value.ReasoningTokens.Availability == ProviderAvailabilityState.Available
                && (value.OutputTokens.Availability != ProviderAvailabilityState.Available
                    || value.ReasoningTokens.Value > value.OutputTokens.Value))
        {
            throw new InvalidOperationException("The retained provider usage vector exceeds its closed post-fact bounds or is internally inconsistent.");
        }
        RequireExplicit(value.BillingAvailability, nameof(value.BillingAvailability));
        RequireExplicit(value.RateAvailability, nameof(value.RateAvailability));
        RequireExplicit(value.CreditAvailability, nameof(value.CreditAvailability));
        if (value.CreditAvailability == ProviderAvailabilityState.Available)
        {
            throw new InvalidOperationException("Provider credit remains unavailable until separately evidenced authority exists.");
        }
        bool allQuantitiesAvailable = quantities.All(x => x.Availability == ProviderAvailabilityState.Available);
        bool noQuantitiesExceptZeroDispatch = value.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 0 }
            && quantities.Skip(1).All(x => x.Availability != ProviderAvailabilityState.Available);
        bool validReceipt = value.ReceiptState switch
        {
            UsageReceiptState.NotDispatched => value.Availability == ProviderAvailabilityState.Unavailable
                && noQuantitiesExceptZeroDispatch,
            UsageReceiptState.Complete => value.Availability == ProviderAvailabilityState.Available
                && value.DispatchCount.Value is >= 1 && allQuantitiesAvailable,
            UsageReceiptState.Partial or UsageReceiptState.FailedKnown or UsageReceiptState.Ambiguous =>
                value.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: >= 1 },
            UsageReceiptState.Unavailable => value.Availability == ProviderAvailabilityState.Unavailable,
            _ => false,
        };
        if (!validReceipt)
        {
            throw new InvalidOperationException("Provider usage receipt state contradicts the retained availability vector.");
        }
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
        if (string.IsNullOrWhiteSpace(value.Identity.Value) || value.Fingerprint.Value.Length != 64
            || value.Provider != "openai" || value.Model != "gpt-5.6-sol" || value.ServiceTier != "default"
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
        if (string.IsNullOrWhiteSpace(value.Identity.Value) || value.Fingerprint.Value.Length != 64
            || value.Provider != "openai" || value.Model != "gpt-5.6-sol" || value.ServiceTier != "default"
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
        bool blockedUsage = value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 0 }
            && new[] { value.Usage.InputTokens, value.Usage.OutputTokens, value.Usage.TotalTokens, value.Usage.ReasoningTokens,
                value.Usage.CacheReadTokens, value.Usage.CacheWriteTokens, value.Usage.PricedToolCalls,
                value.Usage.CalculatedNanoUsd }.All(x => x.Availability == ProviderAvailabilityState.Unavailable && x.Value is null)
            && value.Usage.BillingAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.RateAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.CreditAvailability == ProviderAvailabilityState.Unavailable;
        if (value.State != ProviderOperationState.InputBoundBlocked || !blockedUsage
            || value.TransportState != "not-started" || value.ReceiptState != "not-available"
            || value.SettlementState != "not-started" || value.ReplayState != "not-available"
            || value.AuthorizationId is not null || value.AttemptId is not null || value.RequestId is not null
            || value.ReservationId is not null || value.DispatchFenceId is not null
            || value.TransportEventId is not null || value.ReceiptId is not null || value.ResponseId is not null
            || value.UsageEntryId is not null || value.SettlementId is not null || value.ReplayEdgeId is not null)
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
                && (value.ResetsAt is null || value.ResetsAt.Value >= value.ObservedAt.Value)
            : value.Limit is null && value.Remaining is null && value.ResetsAt is null);

    private static void ValidateAvailability<T>(ProviderAvailabilityState availability, T? value)
    {
        bool hasValue = value is not null;
        if ((availability == ProviderAvailabilityState.Available) != hasValue)
        {
            throw new InvalidOperationException("Provider fact availability contradicts presence.");
        }
    }

    private static void ValidateAvailability<T1, T2>(ProviderAvailabilityState availability, T1? first, T2? second)
    {
        bool both = first is not null && second is not null;
        if ((availability == ProviderAvailabilityState.Available) != both
            || (first is null) != (second is null))
        {
            throw new InvalidOperationException("Provider payload availability contradicts its identity/size pair.");
        }
    }

    private static void ValidateObservedUsage(
        ProviderOperationKind kind,
        ProviderFiniteLimitsContract limits,
        ProviderUsageContract usage)
    {
        // Limits remain pre-dispatch admission ceilings. Provider receipts are
        // post-fact accounting evidence and must retain overruns truthfully.
        Validate(kind, limits);
        if (usage.TotalTokens.Availability == ProviderAvailabilityState.Available
            && (usage.InputTokens.Availability != ProviderAvailabilityState.Available
                || usage.OutputTokens.Availability != ProviderAvailabilityState.Available
                || usage.TotalTokens.Value != checked(usage.InputTokens.Value + usage.OutputTokens.Value))
            || usage.ReasoningTokens.Availability == ProviderAvailabilityState.Available
                && (usage.OutputTokens.Availability != ProviderAvailabilityState.Available
                    || usage.ReasoningTokens.Value > usage.OutputTokens.Value))
        {
            throw new InvalidOperationException("Provider usage totals and reasoning tokens must be exact and internally bounded.");
        }
    }

    private static void ValidateFutureResponseState(ProviderResponseDocument value)
    {
        bool raw = value.RawResponseAvailability == ProviderAvailabilityState.Available;
        bool http = value.HttpStatusAvailability == ProviderAvailabilityState.Available;
        bool returnedModel = value.ReturnedModelAvailability == ProviderAvailabilityState.Available;
        bool returnedTier = value.ReturnedServiceTierAvailability == ProviderAvailabilityState.Available;
        bool refusal = value.RefusalAvailability == ProviderAvailabilityState.Available;
        bool incomplete = value.IncompleteAvailability == ProviderAvailabilityState.Available;
        bool error = value.ErrorAvailability == ProviderAvailabilityState.Available;
        bool admitted = value.ValidationState == ProposalAdmissionState.Admitted
            && value.AdmissionState == ProposalAdmissionState.Admitted;
        bool nonSuccessAdmission = value.ValidationState is ProposalAdmissionState.Rejected
                or ProposalAdmissionState.Abstained or ProposalAdmissionState.Unavailable
                or ProposalAdmissionState.Unsupported
            && value.AdmissionState is ProposalAdmissionState.Rejected
                or ProposalAdmissionState.Abstained or ProposalAdmissionState.Unavailable
                or ProposalAdmissionState.Unsupported;
        bool transport = value.AuthorizationId is not null && value.RequestId is not null && value.DispatchFenceId is not null;
        bool noTransport = value.AuthorizationId is null && value.RequestId is null && value.DispatchFenceId is null;
        bool semanticFactsAbsent = !refusal && !incomplete && !error;
        bool completeUsage = value.Usage.Availability == ProviderAvailabilityState.Available
            && new[] { value.Usage.DispatchCount, value.Usage.InputTokens, value.Usage.OutputTokens,
                value.Usage.TotalTokens, value.Usage.ReasoningTokens, value.Usage.CacheReadTokens,
                value.Usage.CacheWriteTokens, value.Usage.PricedToolCalls, value.Usage.CalculatedNanoUsd }
                .All(quantity => quantity.Availability == ProviderAvailabilityState.Available)
            && value.Usage.DispatchCount.Value >= 1
            && value.Usage.ReceiptState == UsageReceiptState.Complete;
        bool dispatchedUsage = value.Usage.Availability == ProviderAvailabilityState.Available
            && value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: >= 1 };
        bool cancelledUsage = value.Usage.Availability == ProviderAvailabilityState.Unavailable
            && value.Usage.DispatchCount is { Availability: ProviderAvailabilityState.Available, Value: 0 }
            && new[] { value.Usage.InputTokens, value.Usage.OutputTokens, value.Usage.TotalTokens,
                value.Usage.ReasoningTokens, value.Usage.CacheReadTokens, value.Usage.CacheWriteTokens,
                value.Usage.PricedToolCalls, value.Usage.CalculatedNanoUsd }
                .All(quantity => quantity.Availability != ProviderAvailabilityState.Available);
        bool policyCompliant = value.Usage.DispatchCount.Value <= value.Limits.MaximumDispatchCount
            && value.Usage.InputTokens.Value <= value.Limits.MaximumInputTokens
            && value.Usage.OutputTokens.Value <= value.Limits.MaximumOutputTokens
            && value.Usage.CacheReadTokens.Value is null or 0
            && value.Usage.CacheWriteTokens.Value is null or 0
            && value.Usage.PricedToolCalls.Value is null or 0
            && value.Usage.CalculatedNanoUsd.Value <= value.Limits.MaximumCalculatedNanoUsd;
        bool allProviderFactsUnavailable = new[]
            {
                value.RawResponseAvailability, value.ResponseHeadersAvailability, value.HttpStatusAvailability,
                value.ProviderResponseIdAvailability, value.ClientRequestIdAvailability,
                value.ProviderRequestIdAvailability, value.RefusalAvailability, value.IncompleteAvailability,
                value.ErrorAvailability, value.ReturnedModelAvailability, value.ReturnedServiceTierAvailability,
                value.BillingEvidenceAvailability,
            }.All(availability => availability == ProviderAvailabilityState.Unavailable)
            && value.BillingEvidencePayload is null && value.RateLimitFacts.Count == 0
            && value.Usage.BillingAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.RateAvailability == ProviderAvailabilityState.Unavailable
            && value.Usage.CreditAvailability == ProviderAvailabilityState.Unavailable;
        bool boundedOverflow = value.RawResponseAvailability == ProviderAvailabilityState.Unavailable
            && value.RawResponsePayload is null && value.RawResponseBytes is null
            && value.OverflowObservedExcessBytes == 1;
        bool noOverflow = value.OverflowObservedExcessBytes is null;
        bool valid = value.State switch
        {
            ProviderResponseState.Completed => transport && raw && http && returnedModel && returnedTier
                && value.ReturnedModel == "gpt-5.6-sol" && value.ReturnedServiceTier == "default"
                && semanticFactsAbsent && noOverflow && completeUsage
                && (policyCompliant ? admitted : nonSuccessAdmission),
            ProviderResponseState.Refusal => transport && raw && http && refusal && !incomplete && !error
                && noOverflow && dispatchedUsage && value.Usage.ReceiptState == UsageReceiptState.Complete && nonSuccessAdmission,
            ProviderResponseState.Incomplete => transport && raw && http && !refusal && incomplete && !error
                && noOverflow && dispatchedUsage && value.Usage.ReceiptState == UsageReceiptState.Partial && nonSuccessAdmission,
            ProviderResponseState.Failed => transport && raw && http && !refusal && !incomplete && error
                && noOverflow && dispatchedUsage && value.Usage.ReceiptState == UsageReceiptState.FailedKnown && nonSuccessAdmission,
            ProviderResponseState.Queued or ProviderResponseState.InProgress => transport && raw && http
                && semanticFactsAbsent && noOverflow && dispatchedUsage && value.Usage.ReceiptState == UsageReceiptState.Partial && nonSuccessAdmission,
            ProviderResponseState.Malformed => transport && raw && http
                && !refusal && !incomplete && !error && noOverflow && dispatchedUsage
                && value.Usage.ReceiptState == UsageReceiptState.Complete && nonSuccessAdmission,
            ProviderResponseState.Oversized => transport && boundedOverflow && http
                && !refusal && !incomplete && !error && dispatchedUsage
                && value.Usage.ReceiptState is UsageReceiptState.Complete or UsageReceiptState.Partial && nonSuccessAdmission,
            ProviderResponseState.Mismatched => transport && raw && http && returnedModel && returnedTier
                && dispatchedUsage && (value.ReturnedModel != "gpt-5.6-sol"
                    || value.ReturnedServiceTier != "default") && noOverflow
                && value.Usage.ReceiptState == UsageReceiptState.Complete && nonSuccessAdmission,
            ProviderResponseState.Unknown => transport && raw && http && semanticFactsAbsent
                && noOverflow && dispatchedUsage && value.Usage.ReceiptState == UsageReceiptState.Ambiguous && nonSuccessAdmission,
            ProviderResponseState.Cancelled => noTransport && !raw && !http && semanticFactsAbsent
                && allProviderFactsUnavailable && noOverflow && cancelledUsage
                && value.Usage.ReceiptState == UsageReceiptState.NotDispatched && nonSuccessAdmission,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidOperationException("Provider response state contradicts its typed facts, usage, validation, or admission outcome.");
        }
    }

    private static void RequireClosedPriceRule(ProviderPriceRuleContract rule)
    {
        if (string.IsNullOrWhiteSpace(rule.RuleId.Value)
            || rule.Provider != "openai" || rule.Model != "gpt-5.6-sol"
            || rule.ServiceTier != "default" || rule.ContextBand != "standard-under-272k"
            || rule.CacheClass is not ("ordinary-input" or "cache-write" or "cache-read" or "none")
            || rule.TokenClass is not ("input" or "output" or "reasoning")
            || rule.ToolClass != "none" || rule.Region != "global" || rule.Currency != "USD"
            || rule.NumeratorNanoUsd is < 0 or > 600_000_000
            || rule.DenominatorTokens is <= 0 or > 1_000_000_000
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

    private static bool ValidIds(IEnumerable<OpaqueId> values) =>
        values.All(value => !string.IsNullOrWhiteSpace(value.Value));

    private static bool ValidAdmissionLinks(
        IReadOnlyList<ProviderSemanticAdmissionLinkContract> links,
        OpaqueId operationId,
        string ownerKind,
        OpaqueId ownerId,
        OpaqueId rootSubjectId,
        IEnumerable<OpaqueId> proposals,
        IReadOnlyList<OpaqueId> validationIds,
        IReadOnlyList<OpaqueId> declaredLinkIds,
        bool declaredIdsAreAdmissions)
    {
        HashSet<OpaqueId> proposalIds = proposals.ToHashSet();
        return links.Count <= 64
            && Unique(links.Select(x => x.AdmissionId))
            && links.All(link => !string.IsNullOrWhiteSpace(link.AdmissionId.Value)
                && !string.IsNullOrWhiteSpace(link.ResponseRecordId.Value)
                && !string.IsNullOrWhiteSpace(link.AuthorizationId.Value)
                && link.OperationId == operationId
                && link.OwnerKind == ownerKind && link.OwnerId == ownerId
                && link.RootSubjectId == rootSubjectId
                && proposalIds.Contains(link.ProposalId)
                && validationIds.Contains(link.ValidationId)
                && (declaredIdsAreAdmissions
                    ? declaredLinkIds.Contains(link.AdmissionId)
                    : declaredLinkIds.Contains(link.ApplicationLinkId))
                && link.State is ProposalAdmissionState.Admitted or ProposalAdmissionState.Rejected
                    or ProposalAdmissionState.Abstained or ProposalAdmissionState.Unavailable
                    or ProposalAdmissionState.Unsupported or ProposalAdmissionState.Deleted);
    }

    private static bool AdmissionStatesMatch(
        IReadOnlyList<ProviderSemanticAdmissionLinkContract> links,
        IEnumerable<KeyValuePair<OpaqueId, ProposalAdmissionState>> proposals)
    {
        Dictionary<OpaqueId, ProposalAdmissionState> states = proposals.ToDictionary();
        return links.All(link => states.TryGetValue(link.ProposalId, out ProposalAdmissionState state)
                && state == link.State)
            && states.All(proposal => proposal.Value != ProposalAdmissionState.Admitted
                || links.Count(link => link.ProposalId == proposal.Key && link.State == ProposalAdmissionState.Admitted) == 1);
    }

    private static bool BoundedUniqueText(IReadOnlyList<string> values) =>
        values.Count <= 64 && values.All(x => !string.IsNullOrWhiteSpace(x))
        && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
}
