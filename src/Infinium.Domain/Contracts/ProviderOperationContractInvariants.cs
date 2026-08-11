namespace Infinium.Domain.Contracts;

public static class ProviderOperationContractInvariants
{
    public const long MaximumCanonicalRequestBytes = 65_536;
    public const long MaximumLocallyAdmittedInputTokens = 73_728;
    // One token per canonical UTF-8 byte is the deliberately pessimistic base.
    // The fixed 4,096-token allowance covers the provider-owned structured
    // response envelope without relying on a tokenizer or a network call.
    public const long StructuralTokenMargin = 4_096;

    public static long ConservativeUtf8TokenUpperBound(long canonicalRequestBytes)
    {
        if (canonicalRequestBytes <= 0 || canonicalRequestBytes > MaximumCanonicalRequestBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(canonicalRequestBytes));
        }

        // Every UTF-8 token consumes at least one request byte. The fixed margin
        // covers provider framing without depending on a provider preflight.
        return checked(canonicalRequestBytes + StructuralTokenMargin);
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

    public static void Validate(ProviderFiniteLimitsContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.MaximumRequestBytes is <= 0 or > MaximumCanonicalRequestBytes
            || value.MaximumInputTokens is <= 0 or > MaximumLocallyAdmittedInputTokens
            || value.MaximumOutputTokens is <= 0 or > 4_096
            || value.MaximumRawResponseBytes is <= 0 or > 1_048_576
            || value.MaximumDispatchCount != 1
            || value.MaximumCalculatedNanoUsd is <= 0 or > 600_000_000
            || value.DeadlineMilliseconds is <= 0 or > 120_000)
        {
            throw new InvalidOperationException("Provider limits must be finite, positive, and within the accepted M1 ceilings.");
        }
        if (ConservativeUtf8TokenUpperBound(value.MaximumRequestBytes) > value.MaximumInputTokens)
        {
            throw new InvalidOperationException("The local input-token reservation does not cover the conservative UTF-8 bound.");
        }
    }

    public static void Validate(EffectiveScanConfigurationV2Document value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.EffectiveScanConfigurationV2SchemaId);
        Validate(value.Limits);
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
    }

    public static void Validate(ProviderOperationDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.ProviderOperationSchemaId);
        Validate(value.Limits);
        Validate(value.Usage);
        RequireExplicit(value.State, nameof(value.State));
        if (value.RevocationEpoch < 0
            || value.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || value.TransportState is not ("not-started" or "may-have-started" or "started" or "completed" or "failed-known" or "ambiguous")
            || value.ReceiptState is not ("not-available" or "staged" or "validated" or "rejected" or "unresolved")
            || value.SettlementState is not ("not-started" or "settled" or "unresolved-hold" or "failed-known" or "overrun")
            || value.ReplayState is not ("not-available" or "retained-response" or "audit-only"))
        {
            throw new InvalidOperationException("Provider operation contains an unsupported owner or terminal projection state.");
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
        if (value.RawResponseBytes is <= 0 or > 1_048_576 || value.HttpStatus is < 100 or > 599
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
        Validate(value.Limits);
        if (value.OperationKind is not ("transport-qualification" or "source-claim-extraction" or "candidate-investigation"))
        {
            throw new InvalidOperationException("Provider execution input operation kind is unsupported.");
        }
    }

    public static void Validate(RunOutputV2Document value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.RunOutputV2SchemaId);
        if (value.ContainsRawTransport || value.ContainsSecret || value.ProviderOperations.Count > 3
            || !Unique(value.ProviderOperations.Select(x => x.OperationId))
            || !Unique(value.EvidenceAcquisitionRunIds) || !Unique(value.CapabilityDriftIds)
            || !Unique(value.PriceDriftIds) || !BoundedUniqueText(value.ProviderGaps)
            || value.ProviderOperations.Any(x => x.Availability is not ("not-used" or "unavailable" or "live" or "retained" or "failed" or "unresolved")))
        {
            throw new InvalidOperationException("Run output v2 cannot embed raw provider transport or secrets.");
        }
    }

    public static void Validate(CliSummaryV2Document value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CliSummaryV2SchemaId);
        if (value.ContainsRawTransport || value.ContainsSecret || value.DispatchCount > 3
            || value.ProviderState is not ("not-used" or "unavailable" or "live" or "failed" or "unresolved")
            || value.ReplayState is not ("not-available" or "retained-response" or "audit-only")
            || !BoundedUniqueText(value.Gaps)
            || new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
                value.CacheReadTokens, value.CacheWriteTokens, value.CalculatedNanoUsd, value.ReservedNanoUsd }.Any(x => x < 0))
        {
            throw new InvalidOperationException("CLI summary v2 contains an invalid provider projection.");
        }
    }

    public static void Validate(SourceClaimExtractionDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.SourceClaimExtractionSchemaId);
        if (value.PassageIds.Count is 0 or > 64 || !Unique(value.PassageIds)
            || value.ClaimProposals.Count > 64 || !Unique(value.ClaimProposals.Select(x => x.ProposalId))
            || value.ClaimProposals.Any(x => x.State == ProposalAdmissionState.Unspecified
                || string.IsNullOrWhiteSpace(x.Claim) || string.IsNullOrWhiteSpace(x.Reason)
                || !Unique(x.ConditionIds))
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
            || value.HypothesisProposals.Any(x => x.State == ProposalAdmissionState.Unspecified
                || string.IsNullOrWhiteSpace(x.Hypothesis) || string.IsNullOrWhiteSpace(x.Reason)
                || !Unique(x.SupportingEvidenceIds) || !Unique(x.ContradictingEvidenceIds)
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
        if (new[] { value.DispatchCount, value.InputTokens, value.OutputTokens, value.ReasoningTokens,
                value.CacheReadTokens, value.CacheWriteTokens, value.PricedToolCalls, value.CalculatedNanoUsd }.Any(x => x < 0)
            || value.DispatchCount > 1 || value.InputTokens > 73_728 || value.OutputTokens > 4_096
            || value.ReasoningTokens > 4_096 || value.CalculatedNanoUsd > 600_000_000
            || value.CacheReadTokens != 0 || value.CacheWriteTokens != 0 || value.PricedToolCalls != 0)
        {
            throw new InvalidOperationException("The M1 cache-off/tool-free usage vector is invalid.");
        }
        RequireExplicit(value.BillingAvailability, nameof(value.BillingAvailability));
        RequireExplicit(value.RateAvailability, nameof(value.RateAvailability));
        RequireExplicit(value.CreditAvailability, nameof(value.CreditAvailability));
    }

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
        if (Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) == 0)
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
