namespace Infinium.Domain.Contracts;

public static class OperationalContractInvariants
{
    private static readonly LifecycleState[] TerminalLifecycleStates =
    [
        LifecycleState.Cancelled,
        LifecycleState.Completed,
        LifecycleState.CompletedWithGaps,
        LifecycleState.Failed,
        LifecycleState.LimitReached,
        LifecycleState.InvalidatedByChangedInput,
    ];

    public static void Validate(LifecycleTransitionContract transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        Validate(transition.Owner);
        if (transition.RecordKind is not LifecycleTransitionRecordKind.Requested
            and not LifecycleTransitionRecordKind.Observed)
        {
            throw new InvalidOperationException(
                "Lifecycle transition history kind must be requested or observed.");
        }
        RequireNonZeroVersion(transition.PolicyVersion, nameof(transition.PolicyVersion));
        RequireExplicit(transition.From, nameof(transition.From));
        RequireExplicit(transition.To, nameof(transition.To));

        if (transition.From == transition.To)
        {
            throw new InvalidOperationException("A lifecycle transition must change state.");
        }

        if (TerminalLifecycleStates.Contains(transition.From))
        {
            throw new InvalidOperationException("Terminal lifecycle states cannot transition.");
        }

        if (transition.ExpectedGeneration < 0
            || transition.NewGeneration != checked(transition.ExpectedGeneration + 1))
        {
            throw new InvalidOperationException(
                "A lifecycle transition must advance a non-negative compare-and-swap generation exactly once.");
        }

        RequirePositive(transition.CoordinatorFencingEpoch, nameof(transition.CoordinatorFencingEpoch));
        RequireText(transition.Reason, nameof(transition.Reason));

        // The accepted architecture makes the edge graph versioned policy.
        // This value-object layer deliberately does not invent an incomplete
        // graph; the policy identified by PolicyVersion owns edge admission.
    }

    public static void Validate(CoordinatorLeaseContract lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        RequirePositive(lease.FencingEpoch, nameof(lease.FencingEpoch));
        RequireOrderedInterval(lease.AcquiredAt, lease.ExpiresAt, "Coordinator lease");
    }

    public static void Validate(AttemptContract attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        Validate(attempt.Owner);
        ArgumentNullException.ThrowIfNull(attempt.Lease);
        RequirePositive(attempt.AttemptGeneration, nameof(attempt.AttemptGeneration));
        RequirePositive(attempt.CoordinatorFencingEpoch, nameof(attempt.CoordinatorFencingEpoch));
        RequirePositive(attempt.Lease.AttemptFencingToken, nameof(attempt.Lease.AttemptFencingToken));
        RequireOrderedInterval(attempt.Lease.AcquiredAt, attempt.Lease.ExpiresAt, "Attempt lease");
        RequireExplicit(attempt.RetrySafety, nameof(attempt.RetrySafety));
        RequireExplicit(attempt.Outcome, nameof(attempt.Outcome));

        if (attempt.CreatedAt.Value > attempt.Lease.AcquiredAt.Value)
        {
            throw new InvalidOperationException("An attempt cannot be created after its lease is acquired.");
        }
    }

    public static void Validate(AttemptContract attempt, CoordinatorLeaseContract coordinatorLease)
    {
        Validate(attempt);
        Validate(coordinatorLease);
        if (attempt.CoordinatorFencingEpoch != coordinatorLease.FencingEpoch
            || attempt.Lease.AcquiredAt.Value < coordinatorLease.AcquiredAt.Value
            || attempt.Lease.ExpiresAt.Value > coordinatorLease.ExpiresAt.Value)
        {
            throw new InvalidOperationException(
                "An attempt lease must be issued by and remain within the current coordinator lease and fencing epoch.");
        }
    }

    public static void Validate(CheckpointContract checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        Validate(checkpoint.Owner);
        RequireCollection(checkpoint.SourceRevisionIds, nameof(checkpoint.SourceRevisionIds));
        RequireCollection(checkpoint.ToolVersions, nameof(checkpoint.ToolVersions));
        RequireCollection(checkpoint.ModelVersions, nameof(checkpoint.ModelVersions));
        RequireCollection(checkpoint.AnalyzerVersions, nameof(checkpoint.AnalyzerVersions));
        RequireCollection(checkpoint.SchemaVersions, nameof(checkpoint.SchemaVersions));
        RequireCollection(checkpoint.UpstreamArtifactIds, nameof(checkpoint.UpstreamArtifactIds));
        RequireCollection(checkpoint.CompletedPartitions, nameof(checkpoint.CompletedPartitions));
        RequireCollection(checkpoint.PendingAndGapStates, nameof(checkpoint.PendingAndGapStates));
        RequireCollection(checkpoint.AccountingReferences, nameof(checkpoint.AccountingReferences));
        RequirePositive(checkpoint.ProgressPopulationRevision, nameof(checkpoint.ProgressPopulationRevision));

        RequireUniqueIds(checkpoint.SourceRevisionIds, nameof(checkpoint.SourceRevisionIds));
        RequireUniqueVersions(checkpoint.ToolVersions, nameof(checkpoint.ToolVersions));
        RequireUniqueVersions(checkpoint.ModelVersions, nameof(checkpoint.ModelVersions));
        RequireUniqueVersions(checkpoint.AnalyzerVersions, nameof(checkpoint.AnalyzerVersions));
        RequireUniqueVersions(checkpoint.SchemaVersions, nameof(checkpoint.SchemaVersions));
        RequireUniqueIds(checkpoint.UpstreamArtifactIds, nameof(checkpoint.UpstreamArtifactIds));
        RequireUniqueIds(checkpoint.AccountingReferences, nameof(checkpoint.AccountingReferences));
        RequireUniqueText(checkpoint.CompletedPartitions, nameof(checkpoint.CompletedPartitions));
        RequireUniqueText(checkpoint.PendingAndGapStates, nameof(checkpoint.PendingAndGapStates));

        if (checkpoint.CompletedPartitions.Count == 0 && checkpoint.PendingAndGapStates.Count == 0)
        {
            throw new InvalidOperationException(
                "A checkpoint must declare completed work or an explicit pending/gap state.");
        }
    }

    public static void Validate(ProviderAccessProfileContract profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        RequireNonZeroVersion(profile.SchemaVersion, nameof(profile.SchemaVersion));
        RequireExplicit(profile.Provider, nameof(profile.Provider));
        RequireExplicit(profile.Purpose, nameof(profile.Purpose));
        RequireText(profile.DisplayLabel, nameof(profile.DisplayLabel));
        RequirePositive(profile.CredentialGeneration, nameof(profile.CredentialGeneration));
        RequireNonNegative(profile.RevocationEpoch, nameof(profile.RevocationEpoch));
        RequireExplicit(profile.LifecycleState, nameof(profile.LifecycleState));
        RequireExplicit(profile.VerificationState, nameof(profile.VerificationState));
        RequireAcceptedProviderBinding(profile.Provider, profile.Purpose, ProviderEndpoint.OpenAiResponsesV1);
    }

    public static void Validate(ProviderRequestAssignmentContract assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        Validate(assignment.Owner);
        RequirePositive(assignment.CredentialGeneration, nameof(assignment.CredentialGeneration));
        RequireNonNegative(assignment.RevocationEpoch, nameof(assignment.RevocationEpoch));
        RequireExplicit(assignment.Provider, nameof(assignment.Provider));
        RequireExplicit(assignment.Purpose, nameof(assignment.Purpose));
        RequireExplicit(assignment.Endpoint, nameof(assignment.Endpoint));
        RequireAcceptedProviderBinding(assignment.Provider, assignment.Purpose, assignment.Endpoint);
        Validate(assignment.ResponseBounds);
    }

    public static void Validate(
        ProviderRequestAssignmentContract assignment,
        ProviderAccessProfileContract profile,
        BudgetReservationContract reservation)
    {
        Validate(assignment);
        Validate(profile);
        Validate(reservation);

        if (profile.LifecycleState != ProviderProfileLifecycleState.Active
            || profile.VerificationState != ProviderVerificationState.Verified)
        {
            throw new InvalidOperationException(
                "Provider dispatch requires the exact active, verified access-profile generation.");
        }

        if (assignment.ProviderProfileId != profile.ProfileId
            || assignment.CredentialGeneration != profile.CredentialGeneration
            || assignment.RevocationEpoch != profile.RevocationEpoch
            || assignment.Provider != profile.Provider
            || assignment.Purpose != profile.Purpose
            || assignment.ProviderAccountIdentityId != profile.ProviderAccountIdentityId
            || assignment.BillingScopeIdentityId != profile.BillingScopeIdentityId
            || assignment.CapabilitySnapshotId != profile.CapabilitySnapshotId)
        {
            throw new InvalidOperationException(
                "Provider assignment must retain the exact profile, generation, revocation, account, billing scope, purpose, and capability binding.");
        }

        if (assignment.BudgetReservationId != reservation.ReservationId
            || assignment.Owner != reservation.Owner
            || assignment.JobNodeId != reservation.JobNodeId
            || assignment.AttemptId != reservation.AttemptId
            || assignment.RequestIdentity != reservation.RequestIdentity
            || assignment.EffectiveScanConfigurationId != reservation.EffectiveScanConfigurationId
            || assignment.CapabilitySnapshotId != reservation.CapabilitySnapshotId
            || assignment.PriceSnapshotId != reservation.PriceSnapshotId
            || assignment.DispatchDeadline.Value > reservation.ExpiresAt.Value)
        {
            throw new InvalidOperationException(
                "Provider assignment must retain the exact owner, job, attempt, request, configuration, reservation, capability, price, and deadline binding.");
        }

        RequireScope(
            reservation,
            BudgetLimitScopeKind.Request,
            reservation.RequestIdentity,
            "request");
        RequireScope(
            reservation,
            BudgetLimitScopeKind.ProviderProfile,
            profile.ProfileId,
            "provider profile");
        RequireScope(
            reservation,
            BudgetLimitScopeKind.ProviderAccount,
            profile.ProviderAccountIdentityId,
            "provider account");
        RequireScope(
            reservation,
            BudgetLimitScopeKind.BillingScope,
            profile.BillingScopeIdentityId,
            "billing scope");
    }

    public static void Validate(BudgetReservationContract reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        Validate(reservation.Owner);
        Validate(reservation.WorstCaseUsage, requireDispatch: true);
        Validate(reservation.WorstCaseCalculatedCost);
        RequireCollection(reservation.ApplicableLimitScopes, nameof(reservation.ApplicableLimitScopes));
        RequireOrderedInterval(reservation.CreatedAt, reservation.ExpiresAt, "Budget reservation");

        if (reservation.ApplicableLimitScopes.Count == 0)
        {
            throw new InvalidOperationException("A budget reservation must bind every applicable typed limit scope.");
        }

        foreach (BudgetLimitScopeContract scope in reservation.ApplicableLimitScopes)
        {
            ArgumentNullException.ThrowIfNull(scope);
            RequireExplicit(scope.Kind, nameof(scope.Kind));
        }

        int distinctScopes = reservation.ApplicableLimitScopes
            .Select(scope => (scope.Kind, scope.ScopeId.Value))
            .Distinct()
            .Count();
        if (distinctScopes != reservation.ApplicableLimitScopes.Count)
        {
            throw new InvalidOperationException("Budget limit scopes must be unique by kind and identity.");
        }
    }

    public static void Validate(DispatchFenceContract fence)
    {
        ArgumentNullException.ThrowIfNull(fence);
        Validate(fence.Owner);
        RequirePositive(fence.CoordinatorFencingEpoch, nameof(fence.CoordinatorFencingEpoch));
        RequirePositive(fence.AttemptGeneration, nameof(fence.AttemptGeneration));
        RequirePositive(fence.AttemptFencingToken, nameof(fence.AttemptFencingToken));
        RequirePositive(fence.CredentialGeneration, nameof(fence.CredentialGeneration));
        RequireNonNegative(fence.RevocationEpoch, nameof(fence.RevocationEpoch));
        RequireText(fence.DecisionReason, nameof(fence.DecisionReason));

        if (fence.Authorized && fence.Deadline.Value <= fence.EvaluatedAt.Value)
        {
            throw new InvalidOperationException("An authorized dispatch fence must precede its deadline.");
        }
    }

    public static void Validate(
        DispatchFenceContract fence,
        AttemptContract attempt,
        ProviderAccessProfileContract profile,
        BudgetReservationContract reservation)
    {
        Validate(fence);
        Validate(attempt);
        Validate(profile);
        Validate(reservation);

        if (fence.ReservationId != reservation.ReservationId
            || fence.Owner != reservation.Owner
            || fence.JobNodeId != reservation.JobNodeId
            || fence.AttemptId != reservation.AttemptId
            || fence.AttemptId != attempt.AttemptId
            || fence.Owner != attempt.Owner
            || fence.JobNodeId != attempt.JobNodeId
            || fence.CoordinatorFencingEpoch != attempt.CoordinatorFencingEpoch
            || fence.AttemptGeneration != attempt.AttemptGeneration
            || fence.AttemptFencingToken != attempt.Lease.AttemptFencingToken
            || fence.CredentialGeneration != profile.CredentialGeneration
            || fence.RevocationEpoch != profile.RevocationEpoch)
        {
            throw new InvalidOperationException(
                "Dispatch authorization must retain the current reservation, owner, job, attempt, coordinator fence, attempt fence, and credential generation.");
        }

        if (fence.Authorized
            && (profile.LifecycleState != ProviderProfileLifecycleState.Active
                || profile.VerificationState != ProviderVerificationState.Verified
                || attempt.Outcome is not AttemptOutcome.Pending and not AttemptOutcome.Running
                || fence.EvaluatedAt.Value >= reservation.ExpiresAt.Value
                || fence.Deadline.Value > reservation.ExpiresAt.Value))
        {
            throw new InvalidOperationException(
                "An authorized dispatch requires an eligible attempt, active verified profile, and unexpired reservation deadline.");
        }
    }

    public static void Validate(UsageLedgerEntryContract entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Validate(entry.Owner);
        RequireExplicit(entry.UsageReceiptState, nameof(entry.UsageReceiptState));
        RequireExplicit(entry.Settlement, nameof(entry.Settlement));
        Validate(entry.ProviderUsage, requireDispatch: false);
        Validate(entry.CalculatedCost);
        Validate(entry.ProviderBilling);
        Validate(entry.RateLimit);
        Validate(entry.ProviderCredit);

        bool hasUsage = entry.ProviderUsage.DispatchCount != 0
            || entry.ProviderUsage.InputTokens != 0
            || entry.ProviderUsage.OutputTokens != 0
            || entry.ProviderUsage.ReasoningTokens != 0
            || entry.ProviderUsage.PricedToolCalls != 0;
        if (entry.UsageReceiptState == UsageReceiptState.NotDispatched && hasUsage)
        {
            throw new InvalidOperationException("A not-dispatched receipt cannot report provider usage.");
        }

        if (entry.UsageReceiptState is UsageReceiptState.Complete or UsageReceiptState.Partial
            && entry.ProviderUsage.DispatchCount == 0)
        {
            throw new InvalidOperationException("A provider usage receipt must identify a dispatch.");
        }
    }

    public static void Validate(UsageLedgerEntryContract entry, BudgetReservationContract reservation)
    {
        Validate(entry);
        Validate(reservation);

        if (entry.Owner != reservation.Owner
            || entry.JobNodeId != reservation.JobNodeId
            || entry.AttemptId != reservation.AttemptId
            || entry.RequestIdentity != reservation.RequestIdentity
            || entry.EffectiveScanConfigurationId != reservation.EffectiveScanConfigurationId
            || entry.CapabilitySnapshotId != reservation.CapabilitySnapshotId
            || entry.PriceSnapshotId != reservation.PriceSnapshotId)
        {
            throw new InvalidOperationException(
                "Usage must retain the reservation's exact owner, job, attempt, request, configuration, capability, and price binding.");
        }

        bool exceedsReservation =
            entry.ProviderUsage.DispatchCount > reservation.WorstCaseUsage.DispatchCount
            || entry.ProviderUsage.InputTokens > reservation.WorstCaseUsage.InputTokens
            || entry.ProviderUsage.OutputTokens > reservation.WorstCaseUsage.OutputTokens
            || entry.ProviderUsage.ReasoningTokens > reservation.WorstCaseUsage.ReasoningTokens
            || entry.ProviderUsage.PricedToolCalls > reservation.WorstCaseUsage.PricedToolCalls
            || entry.CalculatedCost.NanoUsd > reservation.WorstCaseCalculatedCost.NanoUsd;

        if (entry.Settlement == SettlementState.Overrun != exceedsReservation)
        {
            throw new InvalidOperationException(
                "Only a settlement that exceeds its finite reservation may be represented as an overrun.");
        }
    }

    public static void Validate(WorkerAssignmentContract assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        Validate(assignment.Owner);
        RequirePositive(assignment.CoordinatorFencingEpoch, nameof(assignment.CoordinatorFencingEpoch));
        RequirePositive(assignment.AttemptFencingToken, nameof(assignment.AttemptFencingToken));
        RequireCollection(assignment.AllowedOutputs, nameof(assignment.AllowedOutputs));

        if (assignment.AllowedOutputs.Count == 0)
        {
            throw new InvalidOperationException("A worker assignment must declare at least one staged-output slot.");
        }
        if (assignment.AllowedOutputs.Count > 16)
        {
            throw new InvalidOperationException("A worker assignment cannot exceed 16 staged-output slots.");
        }

        foreach (StagedOutputSlotContract slot in assignment.AllowedOutputs)
        {
            ArgumentNullException.ThrowIfNull(slot);
            RequireExplicit(slot.Kind, nameof(slot.Kind));
            RequireTypedRelativeName(slot.TypedRelativeName);
            RequirePositive(slot.MaximumBytes, nameof(slot.MaximumBytes));
        }

        if (assignment.AllowedOutputs
            .Select(slot => slot.StagedArtifactId.Value)
            .Distinct(StringComparer.Ordinal)
            .Count() != assignment.AllowedOutputs.Count)
        {
            throw new InvalidOperationException("Staged-output slot identities must be unique.");
        }
    }

    public static void Validate(
        StagedOutputManifestContract manifest,
        WorkerAssignmentContract assignment)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(assignment);
        RequireCollection(manifest.Outputs, nameof(manifest.Outputs));
        if (manifest.Outputs.Count > 16)
        {
            throw new InvalidOperationException("A staged-output manifest cannot exceed 16 outputs.");
        }

        if (manifest.AssignmentId != assignment.AssignmentId
            || manifest.AttemptId != assignment.AttemptId
            || manifest.StagingAreaId != assignment.StagingAreaId
            || manifest.CoordinatorFencingEpoch != assignment.CoordinatorFencingEpoch
            || manifest.AttemptFencingToken != assignment.AttemptFencingToken)
        {
            throw new InvalidOperationException(
                "A staged-output manifest must retain the assigned staging area, attempt, and fences.");
        }

        if (manifest.Outputs
            .Select(output => output.StagedArtifactId.Value)
            .Distinct(StringComparer.Ordinal)
            .Count() != manifest.Outputs.Count)
        {
            throw new InvalidOperationException("A staged-output manifest cannot repeat an output slot.");
        }

        Dictionary<string, StagedOutputSlotContract> slots = assignment.AllowedOutputs
            .ToDictionary(slot => slot.StagedArtifactId.Value, StringComparer.Ordinal);
        foreach (StagedOutputContract output in manifest.Outputs)
        {
            ArgumentNullException.ThrowIfNull(output);
            RequireExplicit(output.Kind, nameof(output.Kind));
            RequireTypedRelativeName(output.TypedRelativeName);
            RequireNonNegative(output.ByteLength, nameof(output.ByteLength));
            RequireNonZeroVersion(output.SchemaVersion, nameof(output.SchemaVersion));

            if (!slots.TryGetValue(output.StagedArtifactId.Value, out StagedOutputSlotContract? slot)
                || output.Kind != slot.Kind
                || !StringComparer.Ordinal.Equals(output.TypedRelativeName, slot.TypedRelativeName)
                || output.ByteLength > slot.MaximumBytes)
            {
                throw new InvalidOperationException(
                    "A staged output must exactly match an assigned slot and remain within its finite byte bound.");
            }
        }

        HashSet<string> outputIds = manifest.Outputs
            .Select(output => output.StagedArtifactId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (assignment.AllowedOutputs.Any(slot => slot.Required && !outputIds.Contains(slot.StagedArtifactId.Value)))
        {
            throw new InvalidOperationException("A staged-output manifest is missing a required assigned slot.");
        }
    }

    private static void Validate(OperationOwnerContract owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(owner.OwnerId);
        if (owner is not AnalysisRunOwnerContract
            and not EvidenceAcquisitionRunOwnerContract
            and not MaintenanceOperationOwnerContract)
        {
            throw new InvalidOperationException("Operation owner has an unsupported union variant.");
        }
    }

    private static void Validate(ProviderResponseBoundsContract bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        RequirePositive(bounds.MaximumResponseBytes, nameof(bounds.MaximumResponseBytes));
        RequirePositive(bounds.MaximumInputTokens, nameof(bounds.MaximumInputTokens));
        RequirePositive(bounds.MaximumOutputAndReasoningTokens, nameof(bounds.MaximumOutputAndReasoningTokens));
        RequireNonNegative(bounds.MaximumPricedToolCalls, nameof(bounds.MaximumPricedToolCalls));
        RequireNonNegative(bounds.MaximumCalculatedNanoUsd, nameof(bounds.MaximumCalculatedNanoUsd));
    }

    private static void Validate(ProviderUsageQuantitiesContract usage, bool requireDispatch)
    {
        ArgumentNullException.ThrowIfNull(usage);
        RequireNonNegative(usage.DispatchCount, nameof(usage.DispatchCount));
        RequireNonNegative(usage.InputTokens, nameof(usage.InputTokens));
        RequireNonNegative(usage.OutputTokens, nameof(usage.OutputTokens));
        RequireNonNegative(usage.ReasoningTokens, nameof(usage.ReasoningTokens));
        RequireNonNegative(usage.PricedToolCalls, nameof(usage.PricedToolCalls));
        if (requireDispatch && usage.DispatchCount == 0)
        {
            throw new InvalidOperationException("A provider reservation must reserve at least one dispatch.");
        }
    }

    private static void Validate(CalculatedCostContract cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        RequireNonNegative(cost.NanoUsd, nameof(cost.NanoUsd));
    }

    private static void Validate(ProviderBillingFactContract billing)
    {
        ArgumentNullException.ThrowIfNull(billing);
        RequireAvailableValueCoherence(billing.Availability, billing.BilledNanoUsd, "Provider billing");
    }

    private static void Validate(RateLimitFactContract rateLimit)
    {
        ArgumentNullException.ThrowIfNull(rateLimit);
        RequireAvailableValueCoherence(rateLimit.Availability, rateLimit.RemainingRequests, "Rate limit");
        if (rateLimit.Availability == OperationalFactAvailability.Available && rateLimit.ResetsAt is null)
        {
            throw new InvalidOperationException("An available rate-limit fact requires its reset time.");
        }
        if (rateLimit.Availability == OperationalFactAvailability.Unavailable && rateLimit.ResetsAt is not null)
        {
            throw new InvalidOperationException("An unavailable rate-limit fact cannot carry a reset time.");
        }
    }

    private static void Validate(ProviderCreditFactContract credit)
    {
        ArgumentNullException.ThrowIfNull(credit);
        RequireAvailableValueCoherence(credit.Availability, credit.RemainingNanoUsd, "Provider credit");
    }

    private static void RequireAvailableValueCoherence(
        OperationalFactAvailability availability,
        long? value,
        string factName)
    {
        RequireExplicit(availability, nameof(availability));
        if (availability == OperationalFactAvailability.Available && value is null)
        {
            throw new InvalidOperationException($"{factName} is available but has no value.");
        }
        if (availability == OperationalFactAvailability.Unavailable && value is not null)
        {
            throw new InvalidOperationException($"{factName} is unavailable but carries a value.");
        }
        if (value < 0)
        {
            throw new InvalidOperationException($"{factName} cannot be negative.");
        }
    }

    private static void RequireAcceptedProviderBinding(
        ProviderKind provider,
        CredentialPurpose purpose,
        ProviderEndpoint endpoint)
    {
        if (provider != ProviderKind.OpenAi
            || purpose != CredentialPurpose.OpenAiResponses
            || endpoint != ProviderEndpoint.OpenAiResponsesV1)
        {
            throw new InvalidOperationException("The provider binding is not an accepted closed analysis combination.");
        }
    }

    private static void RequireScope(
        BudgetReservationContract reservation,
        BudgetLimitScopeKind kind,
        OpaqueId identity,
        string name)
    {
        if (!reservation.ApplicableLimitScopes.Any(
            scope => scope.Kind == kind && scope.ScopeId == identity))
        {
            throw new InvalidOperationException(
                $"Budget reservation is missing its exact typed {name} limit scope.");
        }
    }

    private static void RequireOrderedInterval(UtcTimestamp start, UtcTimestamp end, string name)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        if (end.Value <= start.Value)
        {
            throw new InvalidOperationException($"{name} must have a positive finite interval.");
        }
    }

    private static void RequireTypedRelativeName(string value)
    {
        RequireText(value, "TypedRelativeName");
        if (Path.IsPathRooted(value)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("A staged output name must be a single safe assignment-local name.");
        }
    }

    private static void RequireUniqueVersions(
        IReadOnlyList<VersionedComponentContract> values,
        string name)
    {
        foreach (VersionedComponentContract value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireNonZeroVersion(value.Version, name);
        }

        if (values
            .Select(value => value.Identity.Value)
            .Distinct(StringComparer.Ordinal)
            .Count() != values.Count)
        {
            throw new InvalidOperationException($"{name} must contain unique component identities.");
        }
    }

    private static void RequireUniqueIds(IReadOnlyList<OpaqueId> values, string name)
    {
        if (values.Select(value => value.Value).Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new InvalidOperationException($"{name} must contain unique identities.");
        }
    }

    private static void RequireUniqueText(IReadOnlyList<string> values, string name)
    {
        foreach (string value in values)
        {
            RequireText(value, name);
        }

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new InvalidOperationException($"{name} must contain unique values.");
        }
    }

    private static void RequireNonZeroVersion(ContractVersion version, string name)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Major == 0 && version.Minor == 0 && version.Patch == 0)
        {
            throw new InvalidOperationException($"{name} cannot be the zero version.");
        }
    }

    private static void RequireExplicit<T>(T value, string name)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value)
            || Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            throw new InvalidOperationException($"{name} must be an explicit supported value.");
        }
    }

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"{name} must be positive.");
        }
    }

    private static void RequireNonNegative(long value, string name)
    {
        if (value < 0)
        {
            throw new InvalidOperationException($"{name} cannot be negative.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} must be non-empty.");
        }
    }

    private static void RequireCollection<T>(IReadOnlyList<T> values, string name)
    {
        if (values is null)
        {
            throw new InvalidOperationException($"{name} must be present.");
        }
    }
}
