using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class OperationalContractTests
{
    private static readonly UtcTimestamp Epoch = At(0);
    private static readonly ContractVersion V1 = new(1, 0, 0);
    private static readonly AnalysisRunOwnerContract AnalysisOwner = new(Id("analysis-run-1"));

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void LifecycleTransitionRequiresExplicitHistoryKindPolicyCasAndFence()
    {
        LifecycleTransitionContract valid = new(
            Id("transition-1"),
            AnalysisOwner,
            Id("job-1"),
            LifecycleTransitionRecordKind.Requested,
            V1,
            LifecycleState.Running,
            LifecycleState.Pausing,
            4,
            5,
            7,
            Epoch,
            "user requested pause");

        OperationalContractInvariants.Validate(valid);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                RecordKind = LifecycleTransitionRecordKind.Unknown,
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                PolicyVersion = new ContractVersion(0, 0, 0),
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                NewGeneration = valid.ExpectedGeneration,
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                CoordinatorFencingEpoch = 0,
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                From = LifecycleState.Completed,
            }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void AttemptsCarryTypedOwnerLeaseFenceGenerationRetryAndOutcome()
    {
        AttemptContract valid = CreateAttempt(
            new EvidenceAcquisitionRunOwnerContract(Id("acquisition-run-1")));
        CoordinatorLeaseContract coordinatorLease = new(
            Id("coordinator-1"),
            valid.CoordinatorFencingEpoch,
            Epoch,
            At(60));

        OperationalContractInvariants.Validate(valid, coordinatorLease);
        Assert.IsInstanceOfType<EvidenceAcquisitionRunOwnerContract>(valid.Owner);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid,
                coordinatorLease with { FencingEpoch = coordinatorLease.FencingEpoch + 1 }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                Lease = valid.Lease with { AttemptFencingToken = 0 },
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                Lease = valid.Lease with { ExpiresAt = valid.Lease.AcquiredAt },
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                RetrySafety = RetrySafety.Unspecified,
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                Outcome = AttemptOutcome.Unspecified,
            }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void CheckpointRequiresDependencyVersionsProgressAndDeclaredWorkState()
    {
        CheckpointContract valid = new(
            Id("checkpoint-1"),
            AnalysisOwner,
            Id("job-1"),
            Id("attempt-1"),
            Id("snapshot-1"),
            Id("context-1"),
            Id("configuration-1"),
            [Id("source-revision-1")],
            [new VersionedComponentContract(Id("tool-1"), V1)],
            [],
            [new VersionedComponentContract(Id("analyzer-1"), V1)],
            [new VersionedComponentContract(Id("schema-1"), V1)],
            Id("closure-1"),
            [Id("upstream-1")],
            ["partition-1"],
            ["network-unavailable"],
            3,
            [Id("reservation-1")],
            Fingerprint('a'),
            At(3));

        OperationalContractInvariants.Validate(valid);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                ProgressPopulationRevision = 0,
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                CompletedPartitions = [],
                PendingAndGapStates = [],
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                SchemaVersions =
                [
                    new VersionedComponentContract(
                        Id("schema-1"),
                        new ContractVersion(0, 0, 0)),
                ],
            }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderAssignmentRejectsAccountBillingGenerationAndReservationSubstitution()
    {
        ProviderAccessProfileContract profile = CreateProfile();
        BudgetReservationContract reservation = CreateReservation();
        ProviderRequestAssignmentContract assignment = CreateAssignment(reservation);

        OperationalContractInvariants.Validate(assignment, profile, reservation);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                assignment with { ProviderAccountIdentityId = Id("account-other") },
                profile,
                reservation));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                assignment with { BillingScopeIdentityId = Id("billing-other") },
                profile,
                reservation));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                assignment with { CredentialGeneration = profile.CredentialGeneration + 1 },
                profile,
                reservation));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                assignment,
                profile with { LifecycleState = ProviderProfileLifecycleState.Revoked },
                reservation));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                assignment with { RequestIdentity = Id("request-other") },
                profile,
                reservation));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                assignment,
                profile,
                reservation with
                {
                    ApplicableLimitScopes = reservation.ApplicableLimitScopes
                        .Where(scope => scope.Kind != BudgetLimitScopeKind.ProviderAccount)
                        .ToArray(),
                }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ReservationsRejectZeroUnknownNegativeAndDuplicateScopeRepresentations()
    {
        BudgetReservationContract valid = CreateReservation();

        OperationalContractInvariants.Validate(valid);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                WorstCaseUsage = valid.WorstCaseUsage with { DispatchCount = 0 },
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                WorstCaseCalculatedCost = new CalculatedCostContract(-1),
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                ApplicableLimitScopes =
                [
                    new BudgetLimitScopeContract((BudgetLimitScopeKind)999, Id("scope-1")),
                ],
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                ApplicableLimitScopes =
                [
                    valid.ApplicableLimitScopes[0],
                    valid.ApplicableLimitScopes[0],
                ],
            }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void UsageKeepsProviderQuantitiesCalculatedCostAndProviderFactsDistinct()
    {
        BudgetReservationContract reservation = CreateReservation();
        UsageLedgerEntryContract valid = CreateUsage(reservation);

        OperationalContractInvariants.Validate(valid, reservation);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                UsageReceiptState = UsageReceiptState.NotDispatched,
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(valid with
            {
                ProviderBilling = new ProviderBillingFactContract(
                    OperationalFactAvailability.Unavailable,
                    50),
            }));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid with
                {
                    CalculatedCost = new CalculatedCostContract(101),
                },
                reservation));

        UsageLedgerEntryContract overrun = valid with
        {
            CalculatedCost = new CalculatedCostContract(101),
            Settlement = SettlementState.Overrun,
        };
        OperationalContractInvariants.Validate(overrun, reservation);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void DispatchFenceRejectsStaleAttemptAndCredentialAuthority()
    {
        AttemptContract attempt = CreateAttempt(AnalysisOwner);
        ProviderAccessProfileContract profile = CreateProfile();
        BudgetReservationContract reservation = CreateReservation();
        DispatchFenceContract valid = new(
            Id("dispatch-fence-1"),
            reservation.ReservationId,
            reservation.Owner,
            reservation.JobNodeId,
            reservation.AttemptId,
            attempt.CoordinatorFencingEpoch,
            attempt.AttemptGeneration,
            attempt.Lease.AttemptFencingToken,
            profile.CredentialGeneration,
            profile.RevocationEpoch,
            At(30),
            true,
            "authorized exact bounded request",
            At(10));

        OperationalContractInvariants.Validate(valid, attempt, profile, reservation);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid with { AttemptFencingToken = valid.AttemptFencingToken + 1 },
                attempt,
                profile,
                reservation));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid with { CredentialGeneration = valid.CredentialGeneration + 1 },
                attempt,
                profile,
                reservation));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void StagedOutputCanOnlyFillExactAssignedSlotsWithinBounds()
    {
        WorkerAssignmentContract assignment = CreateWorkerAssignment();
        StagedOutputManifestContract valid = CreateManifest(assignment);

        OperationalContractInvariants.Validate(valid, assignment);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid with
                {
                    Outputs =
                    [
                        valid.Outputs[0] with { StagedArtifactId = Id("unassigned-output") },
                    ],
                },
                assignment));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid with
                {
                    Outputs =
                    [
                        valid.Outputs[0] with { Kind = StagedArtifactKind.Diagnostic },
                    ],
                },
                assignment));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid with
                {
                    Outputs =
                    [
                        valid.Outputs[0] with { ByteLength = 1025 },
                    ],
                },
                assignment));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => OperationalContractInvariants.Validate(
                valid with { Outputs = [] },
                assignment));
    }

    private static AttemptContract CreateAttempt(OperationOwnerContract owner)
    {
        return new AttemptContract(
            Id("attempt-1"),
            owner,
            Id("job-1"),
            1,
            2,
            new AttemptLeaseContract(3, Epoch, At(30)),
            Id("dispatch-1"),
            Id("idempotency-1"),
            RetrySafety.SafeWithNewAttempt,
            AttemptOutcome.Pending,
            Epoch);
    }

    private static ProviderAccessProfileContract CreateProfile()
    {
        return new ProviderAccessProfileContract(
            Id("profile-1"),
            V1,
            ProviderKind.OpenAi,
            CredentialPurpose.OpenAiResponses,
            "bounded test profile",
            2,
            3,
            Id("account-1"),
            Id("billing-1"),
            ProviderProfileLifecycleState.Active,
            ProviderVerificationState.Verified,
            Id("capability-1"));
    }

    private static BudgetReservationContract CreateReservation()
    {
        return new BudgetReservationContract(
            Id("reservation-1"),
            AnalysisOwner,
            Id("job-1"),
            Id("attempt-1"),
            Id("request-1"),
            Id("configuration-1"),
            Id("capability-1"),
            Id("price-1"),
            new ProviderUsageQuantitiesContract(1, 100, 50, 25, 0),
            new CalculatedCostContract(100),
            [
                new BudgetLimitScopeContract(BudgetLimitScopeKind.Request, Id("request-1")),
                new BudgetLimitScopeContract(BudgetLimitScopeKind.ProviderProfile, Id("profile-1")),
                new BudgetLimitScopeContract(BudgetLimitScopeKind.ProviderAccount, Id("account-1")),
                new BudgetLimitScopeContract(BudgetLimitScopeKind.BillingScope, Id("billing-1")),
            ],
            Epoch,
            At(60));
    }

    private static ProviderRequestAssignmentContract CreateAssignment(
        BudgetReservationContract reservation)
    {
        return new ProviderRequestAssignmentContract(
            Id("provider-assignment-1"),
            reservation.Owner,
            reservation.JobNodeId,
            reservation.AttemptId,
            Id("profile-1"),
            2,
            3,
            ProviderKind.OpenAi,
            CredentialPurpose.OpenAiResponses,
            ProviderEndpoint.OpenAiResponsesV1,
            Id("account-1"),
            Id("billing-1"),
            reservation.RequestIdentity,
            Fingerprint('b'),
            reservation.EffectiveScanConfigurationId,
            reservation.CapabilitySnapshotId,
            reservation.PriceSnapshotId,
            reservation.ReservationId,
            new ProviderResponseBoundsContract(4096, 100, 75, 0, 100),
            At(30),
            Id("staging-1"));
    }

    private static UsageLedgerEntryContract CreateUsage(BudgetReservationContract reservation)
    {
        return new UsageLedgerEntryContract(
            Id("usage-1"),
            reservation.Owner,
            reservation.JobNodeId,
            reservation.AttemptId,
            reservation.RequestIdentity,
            reservation.EffectiveScanConfigurationId,
            new ProviderUsageQuantitiesContract(1, 90, 45, 20, 0),
            UsageReceiptState.Complete,
            new CalculatedCostContract(90),
            new ProviderBillingFactContract(OperationalFactAvailability.Unavailable, null),
            new RateLimitFactContract(OperationalFactAvailability.Unavailable, null, null),
            new ProviderCreditFactContract(OperationalFactAvailability.Unavailable, null),
            SettlementState.Completed,
            reservation.CapabilitySnapshotId,
            reservation.PriceSnapshotId,
            At(40));
    }

    private static WorkerAssignmentContract CreateWorkerAssignment()
    {
        return new WorkerAssignmentContract(
            Id("worker-assignment-1"),
            AnalysisOwner,
            Id("job-1"),
            Id("attempt-1"),
            2,
            3,
            Id("manifest-1"),
            Id("staging-1"),
            [
                new StagedOutputSlotContract(
                    Id("staged-output-1"),
                    StagedArtifactKind.TypedResult,
                    "result.bin",
                    1024,
                    true),
            ],
            At(60));
    }

    private static StagedOutputManifestContract CreateManifest(
        WorkerAssignmentContract assignment)
    {
        return new StagedOutputManifestContract(
            assignment.AssignmentId,
            assignment.AttemptId,
            assignment.StagingAreaId,
            assignment.CoordinatorFencingEpoch,
            assignment.AttemptFencingToken,
            [
                new StagedOutputContract(
                    Id("staged-output-1"),
                    StagedArtifactKind.TypedResult,
                    "result.bin",
                    Fingerprint('c'),
                    512,
                    V1),
            ],
            Fingerprint('d'));
    }

    private static OpaqueId Id(string value) => new(value);

    private static UtcTimestamp At(int seconds) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(seconds));

    private static Sha256Fingerprint Fingerprint(char character) =>
        new(new string(character, 64));
}
