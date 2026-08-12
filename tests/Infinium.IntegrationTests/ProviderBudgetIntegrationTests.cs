using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderBudgetIntegrationTests
{
    private static readonly DateTimeOffset BaseTime =
        DateTimeOffset.Parse("2026-08-10T00:00:00.0000000+00:00", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly string[] OutputGaps =
        ["provider-billing-unavailable", "prepaid-credit-unavailable"];
    private static readonly JsonSerializerOptions FaultEvidenceJsonOptions = new() { WriteIndented = true };

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationAndDispatchFenceUseTheRealAtomicSqlitePath()
    {
        using BudgetContext context = BudgetContext.Create();
        ProviderReservationAdmissionContract admission = context.Store.ReserveProviderBudget(1, context.Request);
        Assert.AreEqual("reservation-settlement", admission.ReservationId.Value);
        foreach (ProviderBudgetScopeContract scope in context.Scopes)
        {
            ProviderBudgetProjectionContract projection = context.Store.GetProviderBudgetProjection(scope.ScopeKind, scope.ScopeId.Value);
            Assert.AreEqual(context.Vector, projection.Reserved);
        }

        ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        Assert.IsTrue(fence.Authorized);
        Assert.AreEqual("exact-final-gate-authorized", fence.DecisionReason);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CredentialDispatchCoordinatorDerivesFinalGateAndAdoptsHelperResponseThroughWp2Path()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = new(
            helper,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))),
            Path.Combine(Path.GetTempPath(), "Infinium-Wp3-DispatchStore-" + Guid.NewGuid().ToString("N")));

        HelperPrivateFrameV2 enrollmentBootstrap = HelperTestFrames.Bootstrap(nonceSeed: 71);
        enrollmentBootstrap.Bootstrap.Credential.AccessProfileId.Value = "profile-restore";
        enrollmentBootstrap.Bootstrap.Credential.GenerationId.Value = "generation-restore";
        HelperPrivateFrameV2 enrollment = HelperTestFrames.Assignment();
        SetCredential(enrollment.Assignment, "profile-restore", "generation-restore");
        Assert.AreEqual(HelperOutcomeV2.Completed, (await launcher.ExecuteAsync(
            enrollmentBootstrap, enrollment, null, TimeSpan.FromSeconds(20), BaseTime.AddSeconds(5))).Receipt.Outcome);

        HelperPrivateFrameV2 bootstrap = HelperTestFrames.DispatchBootstrap(72);
        bootstrap.Bootstrap.CoordinatorFencingEpoch = 1;
        bootstrap.Bootstrap.ProviderDispatch.OperationId.Value = "operation-restore";
        bootstrap.Bootstrap.ProviderDispatch.AttemptId.Value = "attempt-settlement";
        HelperPrivateFrameV2 assignment = HelperTestFrames.DispatchAssignment();
        assignment.Assignment.ProviderDispatch.OperationId.Value = "operation-restore";
        assignment.Assignment.ProviderDispatch.AttemptId.Value = "attempt-settlement";
        assignment.Assignment.AccessProfileId.Value = "profile-restore";
        assignment.Assignment.GenerationId.Value = "generation-restore";
        assignment.Assignment.ProviderRequest.ReservationGroupId.Value = "reservation-settlement";
        assignment.Assignment.ProviderRequest.RequestId = "request-settlement";
        assignment.Assignment.GenerationOrdinal = 1;
        assignment.Assignment.AccountIdentityId.Value = "account-restore";
        assignment.Assignment.BillingScopeIdentityId.Value = "billing-restore";
        assignment.Assignment.OperationKind = ProviderOperationKindV2.SourceClaimExtraction;
        assignment.Assignment.EffectiveConfigurationId = "config-v2-restore";
        assignment.Assignment.ProviderRequest.CapabilitySnapshotId.Value = "cap-restore";
        assignment.Assignment.ProviderRequest.PriceSnapshotId.Value = "price-restore";
        byte[] requestBytes = new byte[1024];
        ByteString requestDigest = ByteString.CopyFrom(SHA256.HashData(requestBytes));
        assignment.Assignment.ProviderRequest.CanonicalRequestBytes = ByteString.CopyFrom(requestBytes);
        assignment.Assignment.ProviderRequest.CanonicalRequest.Value = requestDigest;
        assignment.Assignment.ProviderRequest.CanonicalRequest.SizeBytes = 1024;
        assignment.Assignment.ProviderRequest.RequestFingerprintSha256 = requestDigest;
        assignment.Assignment.ProviderRequest.InputBoundProof.PolicyId = "openai-responses-o200k-byte-envelope";
        assignment.Assignment.ProviderRequest.InputBoundProof.PolicyVersion = "v1";
        assignment.Assignment.ProviderRequest.ConfirmedAt = Instant(BaseTime.AddSeconds(1));
        assignment.Assignment.ProviderRequest.DispatchDeadline = Instant(BaseTime.AddMinutes(2));
        assignment.Assignment.Settings.Value = ByteString.CopyFrom(Convert.FromHexString(new string('9', 64)));
        assignment.Assignment.OutputSchema.Value = ByteString.CopyFrom(Convert.FromHexString(new string('e', 64)));
        assignment.Assignment.Limits.MaximumRequestBytes = 65_536;
        assignment.Assignment.Limits.MaximumInputTokens = 20;
        assignment.Assignment.Limits.MaximumOutputTokens = 10;
        assignment.Assignment.Limits.MaximumResponseBytes = 1_048_576;
        assignment.Assignment.Limits.MaximumStagedOutputBytes = 1_048_576;
        assignment.Assignment.Limits.MaximumCalculatedNanoUsd = 400_000;
        assignment.Assignment.Limits.MaximumDuration.Value = 120_000;

        CredentialHelperCoordinator coordinator = new(context.Store, launcher);
        HelperPrivateFrameV2 stale = assignment.Clone();
        stale.Assignment.GenerationId.Value = "fabricated-generation";
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.ExecuteAuthoritativeDispatchAsync(
                "attempt-fabricated", bootstrap, stale, BaseTime.AddSeconds(5)));

        using (BudgetContext crashContext = BudgetContext.Create())
        {
            _ = crashContext.Store.ReserveProviderBudget(1, crashContext.Request);
            CredentialHelperCoordinator crashCoordinator = new(crashContext.Store, launcher);
            HelperPrivateFrameV2 crashBootstrap = HelperTestFrames.DispatchBootstrap(73);
            crashBootstrap.Bootstrap.CoordinatorFencingEpoch = 1;
            crashBootstrap.Bootstrap.ProviderDispatch.OperationId.Value = "operation-restore";
            crashBootstrap.Bootstrap.ProviderDispatch.AttemptId.Value = "attempt-settlement";
            await Assert.ThrowsExactlyAsync<IOException>(() =>
                crashCoordinator.ExecuteAuthoritativeDispatchWithFaultAsync(
                    "attempt-crash-window",
                    crashBootstrap,
                    assignment.Clone(),
                    BaseTime.AddSeconds(5),
                    ProviderDispatchFaultPoint.AfterDurableMayHaveStartedBeforeHelper));
            foreach (ProviderBudgetScopeContract scope in crashContext.Scopes)
            {
                ProviderBudgetProjectionContract projection = crashContext.Store.GetProviderBudgetProjection(
                    scope.ScopeKind, scope.ScopeId.Value);
                Assert.AreEqual(crashContext.Vector, projection.Unresolved);
                Assert.AreEqual(ProviderBudgetVectorContract.Zero, projection.Reserved);
            }
        }

        (CoordinatedHelperReceipt helperResult,
            ProviderSimulationPersistenceReceipt persisted,
            ProviderBudgetSettlementReceipt settlement) = await coordinator.ExecuteAuthoritativeDispatchAsync(
            "attempt-settlement", bootstrap, assignment, BaseTime.AddSeconds(5));
        Assert.IsGreaterThan(0, helperResult.Process.StagedResponseBytes.Length);
        Assert.IsNotNull(helperResult.Staging.ResponseRelativePath);
        Assert.AreEqual("assignment-1:response", persisted.ResponseId);
        Assert.AreEqual(ProviderBudgetEventKind.SettledComplete, settlement.Kind);
        byte[] secretCanary = "WP3-REAL-CHILD-SECRET-CANARY"u8.ToArray();
        byte[] targetCanary = "WP3-REAL-CHILD-TARGET-CANARY"u8.ToArray();
        context.Store.Dispose();
        Assert.IsFalse(Directory.EnumerateFiles(context.Root, "*", SearchOption.AllDirectories)
            .Select(File.ReadAllBytes)
            .Any(bytes => bytes.AsSpan().IndexOf(secretCanary) >= 0
                || bytes.AsSpan().IndexOf(targetCanary) >= 0),
            "Real-child canaries must not enter the authoritative database, staging, output, or replay roots.");
        Assert.IsFalse(helperResult.Process.Receipt.ToString().Contains(
            "WP3-REAL-CHILD", StringComparison.Ordinal),
            "Real-child canaries must not enter helper diagnostics.");

        static void SetCredential(HelperAssignmentV2 value, string profile, string generation)
        {
            value.AccessProfileId.Value = profile;
            value.GenerationId.Value = generation;
            value.Credential.AccessProfileId.Value = profile;
            value.Credential.GenerationId.Value = generation;
        }

        static Infinium.Contracts.Protobuf.Common.V1.Instant Instant(DateTimeOffset value) => new()
        {
            UnixSeconds = value.ToUnixTimeSeconds(),
            Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
        };
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationRejectsCallerUnderReservationBeforeAnyDebit()
    {
        using BudgetContext context = BudgetContext.Create();
        ProviderBudgetVectorContract under = context.Vector with
        {
            InputTokens = context.Vector.InputTokens - 1,
            TotalTokens = context.Vector.TotalTokens - 1,
        };
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.ReserveProviderBudget(1, context.Request with { Reserved = under }));
        StringAssert.Contains(error.Message, "authoritative worst-case vector");
        Assert.AreEqual((0L, 0L), context.CountLedgerRoots());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationConcurrentConnectionsCommitExactlyOneVectorWithoutPartialDebit()
    {
        using BudgetContext context = BudgetContext.Create();
        using AuthoritativeStore contender = new(new StoragePaths(context.Root));
        using ManualResetEventSlim start = new(initialState: false);
        ProviderBudgetReservationRequest first = context.Request with { ReservationId = "reservation-race-a" };
        ProviderBudgetReservationRequest second = context.Request with { ReservationId = "reservation-race-b" };
        Exception? firstError = null;
        Exception? secondError = null;
        Task firstTask = Task.Run(() =>
        {
            start.Wait();
            try { _ = context.Store.ReserveProviderBudget(1, first); }
            catch (Exception error) { firstError = error; }
        });
        Task secondTask = Task.Run(() =>
        {
            start.Wait();
            try { _ = contender.ReserveProviderBudget(1, second); }
            catch (Exception error) { secondError = error; }
        });
        start.Set();
        Task.WaitAll(firstTask, secondTask);

        Assert.AreEqual(1, new[] { firstError, secondError }.Count(error => error is null));
        Exception loser = firstError ?? secondError!;
        StringAssert.Contains(loser.Message, "exhausted");
        foreach (ProviderBudgetScopeContract scope in context.Scopes)
        {
            Assert.AreEqual(context.Vector,
                context.Store.GetProviderBudgetProjection(scope.ScopeKind, scope.ScopeId.Value).Reserved);
        }
        using SqliteConnection database = new($"Data Source={context.Store.Paths.Database};Mode=ReadOnly;Pooling=False");
        database.Open();
        using SqliteCommand command = database.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM provider_budget_events WHERE event_kind='reserved';";
        Assert.AreEqual(8L, (long)command.ExecuteScalar()!);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementReleasesKnownUndispatchedAndRebuildsEqualProjection()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        _ = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
            new("settlement-undispatched", "reservation-settlement",
                ProviderBudgetEventKind.ReleasedUndispatched, null, null, BaseTime.AddSeconds(6)));
        Assert.AreEqual(context.Vector, receipt.Released);
        Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Settled);
        Assert.IsFalse(receipt.RetryPermitted);

        IReadOnlyList<ProviderBudgetProjectionContract> rebuilt =
            context.Store.RebuildProviderBudgetProjections(BaseTime.AddSeconds(7));
        Assert.AreEqual(8, rebuilt.Count);
        Assert.IsTrue(rebuilt.All(item => item.Reserved == ProviderBudgetVectorContract.Zero
            && item.Settled == ProviderBudgetVectorContract.Zero
            && item.Unresolved == ProviderBudgetVectorContract.Zero));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementRetainsAmbiguousStartInFullAndNeverRetries()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        context.Store.RecordProviderTransportStart(
            "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
            ambiguous: true, BaseTime.AddSeconds(6));
        ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
            new("settlement-ambiguous", "reservation-settlement",
                ProviderBudgetEventKind.RetainedAmbiguous, null, null, BaseTime.AddSeconds(7)));
        Assert.AreEqual(context.Vector, receipt.Unresolved);
        Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Released);
        Assert.IsFalse(receipt.RetryPermitted);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementCoversCompleteFailedPartialUnavailableAndOverrunWithoutFallbackOrRetry()
    {
        ProviderBudgetVectorContract expectedUsage = new(1, 8, 4, 12, 1, 0, 0, 0, 52_000);
        foreach (ProviderBudgetEventKind kind in new[]
                 {
                     ProviderBudgetEventKind.SettledComplete,
                     ProviderBudgetEventKind.SettledFailedKnown,
                 })
        {
            using BudgetContext context = BudgetContext.Create();
            _ = context.Store.ReserveProviderBudget(1, context.Request);
            ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
            context.Store.RecordProviderTransportStart(
                "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
                ambiguous: false, BaseTime.AddSeconds(6));
            context.SeedActualUsage(expectedUsage, failedKnown: kind == ProviderBudgetEventKind.SettledFailedKnown);
            ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
                new("settlement-" + kind, "reservation-settlement", kind,
                    "usage-settlement", expectedUsage, BaseTime.AddSeconds(10)));
            Assert.AreEqual(expectedUsage, receipt.Settled);
            Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Unresolved);
            Assert.IsFalse(receipt.RetryPermitted);
            Assert.AreEqual((1L, 1L), context.CountOwnedAndAttachedUsageRollups());
            Assert.ThrowsExactly<InvalidOperationException>(() => context.Store.SettleProviderBudget(
                new("settlement-duplicate", "reservation-settlement", kind,
                    "usage-settlement", expectedUsage, BaseTime.AddSeconds(11))));
        }

        foreach (ProviderBudgetEventKind kind in new[]
                 {
                     ProviderBudgetEventKind.RetainedPartial,
                     ProviderBudgetEventKind.RetainedUnavailable,
                 })
        {
            using BudgetContext context = BudgetContext.Create();
            _ = context.Store.ReserveProviderBudget(1, context.Request);
            ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
            context.Store.RecordProviderTransportStart(
                "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
                ambiguous: false, BaseTime.AddSeconds(6));
            ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
                new("settlement-" + kind, "reservation-settlement", kind,
                    null, null, BaseTime.AddSeconds(7)));
            Assert.AreEqual(context.Vector, receipt.Unresolved);
            Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Settled);
            Assert.IsFalse(receipt.RetryPermitted);
        }

        using (BudgetContext context = BudgetContext.Create())
        {
            ProviderBudgetVectorContract overrun = new(1, 21, 5, 26, 2, 0, 0, 0, 255_000);
            _ = context.Store.ReserveProviderBudget(1, context.Request);
            ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
            context.Store.RecordProviderTransportStart(
                "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
                ambiguous: false, BaseTime.AddSeconds(6));
            context.SeedActualUsage(overrun, failedKnown: false);
            ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
                new("settlement-overrun", "reservation-settlement", ProviderBudgetEventKind.SettledOverrun,
                    "usage-settlement", overrun, BaseTime.AddSeconds(10)));
            Assert.AreEqual(overrun, receipt.Settled);
            Assert.IsFalse(receipt.RetryPermitted);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementProductionSimulatorPersistsExactResponseUsageOwnershipAndSettlement()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt gate = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        ProviderAccountingCoordinator coordinator = new(context.Store);
        ProviderBudgetSettlementReceipt settlement = coordinator.SimulatePersistAndSettle(
            gate, "authorization-settlement", "operation-restore", "reservation-settlement",
            "attempt-settlement", "request-settlement", "production-simulator",
            ProviderSimulatorOutcome.Completed,
            new(65_536, 20, 10, 1_048_576, 1, 400_000, 120_000),
            BaseTime.AddSeconds(7));

        Assert.AreEqual(ProviderBudgetEventKind.SettledComplete, settlement.Kind);
        Assert.AreEqual(new ProviderBudgetVectorContract(1, 20, 10, 30, 8, 0, 0, 0, 400_000), settlement.Settled);
        using SqliteConnection database = new($"Data Source={context.Store.Paths.Database};Mode=ReadOnly;Pooling=False");
        database.Open();
        using SqliteCommand command = database.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*) FROM provider_responses response
            JOIN provider_usage_entries usage
              ON usage.response_record_id=response.response_record_id
             AND usage.operation_id=response.operation_id
             AND usage.provider_attempt_id=response.provider_attempt_id
             AND usage.request_id=response.request_id
             AND usage.dispatch_fence_id=response.dispatch_fence_id
            JOIN provider_settlements settlement
              ON settlement.operation_id=usage.operation_id
             AND settlement.provider_attempt_id=usage.provider_attempt_id
             AND settlement.request_id=usage.request_id
             AND settlement.usage_entry_id=usage.usage_entry_id
            JOIN provider_budget_settlement_receipts receipt
              ON receipt.settlement_id=settlement.settlement_id
             AND receipt.reservation_id=settlement.reservation_id
            WHERE response.response_record_id='production-simulator:response'
              AND usage.usage_entry_id='production-simulator:usage'
              AND settlement.settlement_id='production-simulator:settlement'
              AND response.operation_id='operation-restore'
              AND response.provider_attempt_id='attempt-settlement'
              AND response.request_id='request-settlement'
              AND response.dispatch_fence_id='fence-settlement';
            """;
        Assert.AreEqual(1L, (long)command.ExecuteScalar()!);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderBudgetReplayAndHumanJsonOutputCreateNoNewDebitOrDispatch()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        _ = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        _ = context.Store.SettleProviderBudget(new(
            "settlement-replay", "reservation-settlement", ProviderBudgetEventKind.ReleasedUndispatched,
            null, null, BaseTime.AddSeconds(6)));
        (long eventsBefore, long fencesBefore) = context.CountLedgerRoots();

        ProviderBudgetProjectionContract rebuilt = context.Store
            .RebuildProviderBudgetProjections(BaseTime.AddSeconds(7))
            .Single(item => item.ScopeKind == "operation");
        ProviderBudgetOutputDocument output = ProviderBudgetOutputRenderer.CreateDocument(
            rebuilt, OutputGaps);
        string json = ProviderBudgetOutputRenderer.RenderJson(output);
        string human = ProviderBudgetOutputRenderer.RenderHuman(output);
        (long eventsAfter, long fencesAfter) = context.CountLedgerRoots();

        Assert.AreEqual(eventsBefore, eventsAfter, "Replay must not append a new debit event.");
        Assert.AreEqual(fencesBefore, fencesAfter, "Replay must not authorize another dispatch.");
        StringAssert.Contains(json, "\"network_used\": false");
        StringAssert.Contains(json, "\"credential_accessed\": false");
        StringAssert.Contains(human, "Execution mode: simulated-nonnetwork");
        StringAssert.Contains(human, "provider-billing-unavailable");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationRejectsPausedOwnerBeforeAnyDebit()
    {
        using BudgetContext context = BudgetContext.Create("paused");
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => context.Store.ReserveProviderBudget(1, context.Request));
        StringAssert.Contains(error.Message, "authorized request and attempt root");
        Assert.AreEqual((0L, 0L), context.CountLedgerRoots());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationCatalogPublicationIsImmutableAndIdempotent()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp2-Catalog-" + Guid.NewGuid().ToString("N"));
        try
        {
            using AuthoritativeStore store = new(new StoragePaths(root));
            store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, BaseTime);
            store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, BaseTime.AddSeconds(1));
            Assert.ThrowsExactly<InvalidOperationException>(() => store.PublishProviderCatalog(
                M1ProviderCatalog.Capability with { Revision = "altered-same-identity-and-fingerprint" },
                M1ProviderCatalog.Price,
                BaseTime.AddSeconds(2)));
            ProviderPriceRuleContract[] alteredRules = M1ProviderCatalog.Price.Rules
                .Select((rule, index) => index == 0 ? rule with { NumeratorNanoUsd = rule.NumeratorNanoUsd + 1 } : rule)
                .ToArray();
            Assert.ThrowsExactly<InvalidOperationException>(() => store.PublishProviderCatalog(
                M1ProviderCatalog.Capability,
                M1ProviderCatalog.Price with { Rules = alteredRules },
                BaseTime.AddSeconds(3)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementRejectsCausalBackdatingAndReplayPreservesEventOrder()
    {
        using BudgetContext context = BudgetContext.Create();
        ProviderBudgetVectorContract expectedActual = new(1, 8, 4, 12, 1, 0, 0, 0, 52_000);
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        context.Store.RecordProviderTransportStart(
            "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
            ambiguous: false, BaseTime.AddSeconds(6));
        context.SeedActualUsage(expectedActual, failedKnown: false);
        InvalidOperationException backdated = Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.SettleProviderBudget(new(
                "settlement-backdated", "reservation-settlement", ProviderBudgetEventKind.SettledComplete,
                "usage-settlement", expectedActual, BaseTime.AddSeconds(8))));
        StringAssert.Contains(backdated.Message, "causally backdated");
        ProviderBudgetSettlementReceipt accepted = context.Store.SettleProviderBudget(new(
            "settlement-ordered", "reservation-settlement", ProviderBudgetEventKind.SettledComplete,
            "usage-settlement", expectedActual, BaseTime.AddSeconds(10)));
        Assert.AreEqual(expectedActual, accepted.Settled);
        IReadOnlyList<ProviderBudgetProjectionContract> rebuilt =
            context.Store.RebuildProviderBudgetProjections(BaseTime.AddSeconds(11));
        Assert.IsTrue(rebuilt.All(item => item.Settled == expectedActual));
        Assert.IsTrue(context.BudgetEventsAreCausallyOrdered());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementBudgetFaultScheduleExercisesRealSqliteBoundariesAndWritesDynamicEvidence()
    {
        bool rollback;
        using (BudgetContext context = BudgetContext.Create())
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => context.Store.ReserveProviderBudgetWithFault(
                1, context.Request, ProviderBudgetFaultPoint.AfterReservationRootBeforeScopeEvents));
            rollback = context.CountLedgerRoots() == (0L, 0L) && context.CountReservations() == 0;
        }

        int competingWinners;
        using (BudgetContext context = BudgetContext.Create())
        using (AuthoritativeStore contender = new(new StoragePaths(context.Root)))
        {
            Exception? first = null;
            Exception? second = null;
            using ManualResetEventSlim start = new(false);
            Task a = Task.Run(() => { start.Wait(); try { context.Store.ReserveProviderBudget(1, context.Request with { ReservationId = "fault-race-a" }); } catch (Exception error) { first = error; } });
            Task b = Task.Run(() => { start.Wait(); try { contender.ReserveProviderBudget(1, context.Request with { ReservationId = "fault-race-b" }); } catch (Exception error) { second = error; } });
            start.Set();
            Task.WaitAll(a, b);
            competingWinners = new[] { first, second }.Count(error => error is null);
        }

        bool staleEpoch;
        using (BudgetContext context = BudgetContext.Create())
        {
            _ = context.Store.AcquireCoordinatorAuthorityAfterProcessExclusion(
                "wp2-fault-new-owner", DateTimeOffset.UtcNow.AddSeconds(1), TimeSpan.FromMinutes(10));
            staleEpoch = Assert.ThrowsExactly<InvalidOperationException>(() =>
                context.Store.ReserveProviderBudget(1, context.Request)).Message.Contains("fencing epoch", StringComparison.Ordinal);
        }

        bool deadline;
        using (BudgetContext context = BudgetContext.Create())
        {
            ProviderBudgetReservationRequest expired = context.Request with
            {
                CreatedAt = BaseTime.AddMinutes(3),
                ExpiresAt = BaseTime.AddMinutes(4),
            };
            deadline = Assert.ThrowsExactly<InvalidOperationException>(() =>
                context.Store.ReserveProviderBudget(1, expired)).Message.Contains("authorized request", StringComparison.Ordinal);
        }

        bool reconstruction;
        using (BudgetContext context = BudgetContext.Create())
        {
            _ = context.Store.ReserveProviderBudget(1, context.Request);
            ProviderBudgetProjectionContract rebuilt = context.Store.RebuildProviderBudgetProjections(BaseTime.AddSeconds(12))
                .Single(item => item.ScopeKind == "operation");
            reconstruction = rebuilt.Reserved == context.Vector;
        }

        Assert.IsTrue(rollback);
        Assert.AreEqual(1, competingWinners);
        Assert.IsTrue(staleEpoch);
        Assert.IsTrue(deadline);
        Assert.IsTrue(reconstruction);
        string? evidencePath = Environment.GetEnvironmentVariable("INFINIUM_WP2_FAULT_EVIDENCE_PATH");
        if (!string.IsNullOrWhiteSpace(evidencePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
            {
                schema = "infinium.wp2.budget-fault-evidence/v1",
                rollback_after_reservation_root = rollback,
                competing_commit_winners = competingWinners,
                stale_epoch_rejected = staleEpoch,
                deadline_rejected = deadline,
                projection_reconstructed_from_events = reconstruction,
                network_operations = 0,
                credential_operations = 0,
            }, FaultEvidenceJsonOptions) + Environment.NewLine);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementBackupRestoreRetainsAuthoritativeEventsAndRebuildableProjection()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        _ = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        _ = context.Store.SettleProviderBudget(new(
            "settlement-backup", "reservation-settlement", ProviderBudgetEventKind.ReleasedUndispatched,
            null, null, BaseTime.AddSeconds(6)));
        BackupArtifact backup = context.Store.CreateBackup("Wp2Budget", BaseTime.AddSeconds(7));
        string restoredRoot = Path.Combine(Path.GetTempPath(), "Infinium-Wp2-Restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (StoragePaths targetPaths = new(restoredRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, targetPaths);
            }
            using (StoragePaths restoredPaths = new(restoredRoot))
            using (AuthoritativeStore restored = new(restoredPaths))
            {
                ProviderBudgetProjectionContract before = context.Store.GetProviderBudgetProjection(
                    "operation", "operation-restore");
                ProviderBudgetProjectionContract after = restored.GetProviderBudgetProjection(
                    "operation", "operation-restore");
                Assert.AreEqual(before.Reserved, after.Reserved);
                Assert.AreEqual(before.Settled, after.Settled);
                Assert.AreEqual(before.Unresolved, after.Unresolved);
                Assert.AreEqual(8, restored.RebuildProviderBudgetProjections(BaseTime.AddSeconds(8)).Count);
            }
        }
        finally
        {
            if (Directory.Exists(restoredRoot))
            {
                DeleteDirectoryWithRetry(restoredRoot);
            }
        }
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private sealed class BudgetContext : IDisposable
    {
        private BudgetContext(string root, AuthoritativeStore store, ProviderBudgetVectorContract vector,
            IReadOnlyList<ProviderBudgetScopeContract> scopes)
        {
            Root = root;
            Store = store;
            Vector = vector;
            Scopes = scopes;
            Request = new("reservation-settlement", "operation-restore", "attempt-settlement", "request-settlement",
                vector, scopes, BaseTime.AddSeconds(90), BaseTime.AddSeconds(4));
            GateRequest = new("fence-settlement", "authorization-settlement", "operation-restore",
                "reservation-settlement", "attempt-settlement", "request-settlement", "profile-restore",
                "generation-restore", 0, 1, BaseTime.AddSeconds(5));
        }

        public string Root { get; }
        public AuthoritativeStore Store { get; }
        public ProviderBudgetVectorContract Vector { get; }
        public IReadOnlyList<ProviderBudgetScopeContract> Scopes { get; }
        public ProviderBudgetReservationRequest Request { get; }
        public ProviderDispatchGateRequest GateRequest { get; }

        public static BudgetContext Create(string lifecycleState = "running")
        {
            string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp2-" + Guid.NewGuid().ToString("N"));
            AuthoritativeStore store = new(new StoragePaths(root));
            try
            {
                PersistenceAndLifecycleTests.SeedProviderAuthorityBlock(root, lifecycleState);
                using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = AuthorizationSql;
                Assert.AreEqual(3, command.ExecuteNonQuery());

                CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                    "wp2-integration", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
                Assert.AreEqual(1L, authority.FencingEpoch);
                ProviderBudgetVectorContract vector = new(1, 20, 10, 30, 10, 0, 0, 0, 400_000);
                string[] kinds = ["request", "operation", "evidence-acquisition-run", "analysis-run",
                    "provider-profile", "provider-account", "billing-scope", "global"];
                string[] ids = ["request-settlement", "operation-restore", "acquisition-restore", "run-restore",
                    "profile-restore", "account-restore", "billing-restore", "provider-global"];
                ProviderBudgetScopeContract[] scopes = kinds.Zip(ids,
                    (kind, id) => new ProviderBudgetScopeContract(kind, new OpaqueId(id), vector)).ToArray();
                store.ConfigureProviderBudgetScopes(1, scopes, BaseTime.AddSeconds(3));
                return new(root, store, vector, scopes);
            }
            catch
            {
                store.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Store.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        public (long Events, long Fences) CountLedgerRoots()
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Mode=ReadOnly;Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText =
                "SELECT (SELECT COUNT(*) FROM provider_budget_events), (SELECT COUNT(*) FROM provider_dispatch_fences);";
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.IsTrue(reader.Read());
            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        public (long Owner, long Attached) CountOwnedAndAttachedUsageRollups()
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Mode=ReadOnly;Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText =
                """
                SELECT
                  COUNT(CASE WHEN attribution_kind='owner' THEN 1 END),
                  COUNT(CASE WHEN attribution_kind='attached-pre-cutoff' AND dispatch_sequence_cutoff=2 THEN 1 END)
                FROM provider_usage_rollup_references WHERE usage_entry_id='usage-settlement';
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.IsTrue(reader.Read());
            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        public long CountReservations()
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Mode=ReadOnly;Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM provider_reservations;";
            return (long)command.ExecuteScalar()!;
        }

        public bool BudgetEventsAreCausallyOrdered()
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Mode=ReadOnly;Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*) FROM provider_budget_events settled
                JOIN provider_budget_events reserved
                  ON reserved.reservation_id=settled.reservation_id
                 AND reserved.scope_kind=settled.scope_kind AND reserved.scope_id=settled.scope_id
                 AND reserved.event_kind='reserved'
                WHERE settled.event_kind<>'reserved' AND settled.occurred_at < reserved.occurred_at;
                """;
            return (long)command.ExecuteScalar()! == 0;
        }

        public void SeedActualUsage(ProviderBudgetVectorContract actual, bool failedKnown)
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.Parameters.AddWithValue("$dispatch", actual.DispatchCount);
            command.Parameters.AddWithValue("$input", actual.InputTokens);
            command.Parameters.AddWithValue("$output", actual.OutputTokens);
            command.Parameters.AddWithValue("$total", actual.TotalTokens);
            command.Parameters.AddWithValue("$reasoning", actual.ReasoningTokens);
            command.Parameters.AddWithValue("$cache_read", actual.CacheReadTokens);
            command.Parameters.AddWithValue("$cache_write", actual.CacheWriteTokens);
            command.Parameters.AddWithValue("$tools", actual.PricedToolCalls);
            command.Parameters.AddWithValue("$nano", actual.NanoUsd);
            command.Parameters.AddWithValue("$response_state", failedKnown ? "failed" : "completed");
            command.Parameters.AddWithValue("$receipt_state", failedKnown ? "failed-known" : "complete");
            command.Parameters.AddWithValue("$error_availability", failedKnown ? "available" : "unavailable");
            command.Parameters.AddWithValue("$error_code", failedKnown ? "simulated-known-failure" : DBNull.Value);
            command.Parameters.AddWithValue("$validation", failedKnown ? "rejected" : "proposed");
            command.CommandText =
                """
                INSERT INTO provider_transport_events VALUES(
                  'fence-settlement:response','operation-restore','attempt-settlement','request-settlement','fence-settlement',
                  'response-staged',3,'2026-08-10T00:00:07.0000000+00:00');
                INSERT INTO provider_responses(
                  response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,
                  request_id,provider_attempt_id,reservation_id,dispatch_fence_id,operation_kind,maximum_input_tokens,maximum_output_tokens,
                  maximum_calculated_nano_usd,raw_response_availability,raw_response_payload_id,raw_response_fingerprint,
                  raw_response_bytes,maximum_raw_response_bytes,response_headers_availability,http_status_availability,
                  http_status,provider_response_id_availability,client_request_id,client_request_id_availability,
                  provider_request_id_availability,billing_evidence_availability,response_state,refusal_availability,
                  incomplete_availability,error_availability,error_code,requested_model,returned_model,returned_model_availability,
                  requested_service_tier,returned_service_tier,returned_service_tier_availability,reasoning_context,
                  reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,expected_rate_limit_fact_count,
                  credit_availability,validation_state,admission_state,created_at)
                SELECT 'response-settlement','available','available','authorization-settlement',operation_id,owner_kind,owner_id,
                  'request-settlement','attempt-settlement','reservation-settlement','fence-settlement',operation_kind,maximum_input_tokens,
                  maximum_output_tokens,maximum_calculated_nano_usd,'available','request-payload-restore',request_fingerprint,
                  1024,maximum_raw_response_bytes,'unavailable','available',200,'unavailable','client-request-settlement',
                  'available','unavailable','unavailable',$response_state,'unavailable','unavailable',$error_availability,$error_code,
                  'gpt-5.6-sol','gpt-5.6-sol','available','default','default','available','current_turn','standard','explicit','unavailable',
                  'unavailable',0,'unavailable',$validation,$validation,'2026-08-10T00:00:08.0000000+00:00'
                FROM provider_operation_authorizations WHERE authorization_id='authorization-settlement';
                INSERT INTO provider_usage_entries(
                  usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id,
                  dispatch_count_availability,dispatch_count,input_tokens_availability,input_tokens,output_tokens_availability,
                  output_tokens,total_tokens_availability,total_tokens,reasoning_tokens_availability,reasoning_tokens,
                  cache_read_tokens_availability,cache_read_tokens,cache_write_tokens_availability,cache_write_tokens,
                  priced_tool_calls_availability,priced_tool_calls,calculated_nano_usd_availability,calculated_nano_usd,
                  billing_availability,rate_availability,credit_availability,receipt_state,created_at)
                VALUES('usage-settlement','receipt-settlement','available','operation-restore','attempt-settlement','request-settlement','fence-settlement',
                  'response-settlement','available',$dispatch,'available',$input,'available',$output,'available',$total,
                  'available',$reasoning,'available',$cache_read,'available',$cache_write,'available',$tools,'available',$nano,
                  'unavailable','unavailable','unavailable',$receipt_state,'2026-08-10T00:00:09.0000000+00:00');
                """;
            Assert.AreEqual(3, command.ExecuteNonQuery());
        }
    }

    private const string AuthorizationSql =
        """
        INSERT INTO provider_operation_authorizations(
          authorization_id,operation_id,owner_kind,owner_id,evidence_acquisition_run_id,job_node_id,command_id,requested_at,
          profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
          effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
          output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
          price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
          coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
          maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
          dispatch_deadline_utc,confirmed_at)
        SELECT 'authorization-settlement',operation_id,owner_kind,owner_id,owner_id,job_node_id,command_id,requested_at,
          profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
          effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
          output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
          price_snapshot_id,settings_fingerprint,'openai-responses-o200k-byte-envelope','v1','proved',coordinator_fencing_epoch,
          maximum_request_bytes,20,10,maximum_raw_response_bytes,maximum_dispatch_count,400000,deadline_milliseconds,
          dispatch_deadline_utc,confirmed_at
        FROM provider_operation_blocks WHERE operation_id='operation-restore';
        INSERT INTO provider_operation_attempts VALUES(
          'attempt-settlement','operation-restore',1,'proposed',1,'2026-08-10T00:00:02.0000000+00:00');
        INSERT INTO provider_requests(
          request_id,client_request_id,operation_id,provider_attempt_id,request_fingerprint,
          canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
          input_bound_policy_version,input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
        SELECT 'request-settlement','client-request-settlement',operation_id,'attempt-settlement',request_fingerprint,
          canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
          input_bound_policy_version,input_bound_proof_status,'request-payload-restore',request_fingerprint,1024,
          '2026-08-10T00:00:03.0000000+00:00'
        FROM provider_operation_authorizations WHERE authorization_id='authorization-settlement';
        """;
}
