using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
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
    private static readonly string[] AllSourceClaimDecisionStates = ["admitted", "abstained", "abstained", "audit-only"];
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
    public async Task CredentialDeleteRevocationRejectsOldEpochFinalGateBeforeHelperRuns()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = new(
            helper,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))),
            Path.Combine(Path.GetTempPath(), "Infinium-Wp4-DeleteFenceStore-" + Guid.NewGuid().ToString("N")));
        CredentialHelperCoordinator coordinator = new(context.Store, launcher);
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap(nonceSeed: 70);
        bootstrap.Bootstrap.Credential.AccessProfileId.Value = "profile-restore";
        bootstrap.Bootstrap.Credential.GenerationId.Value = "generation-restore";
        HelperPrivateFrameV2 assignment = HelperTestFrames.Assignment(HelperAssignmentKindV2.Delete);
        assignment.Assignment.AccessProfileId.Value = "profile-restore";
        assignment.Assignment.GenerationId.Value = "generation-restore";
        assignment.Assignment.GenerationOrdinal = 1;
        assignment.Assignment.Credential.AccessProfileId.Value = "profile-restore";
        assignment.Assignment.Credential.GenerationId.Value = "generation-restore";

        await Assert.ThrowsExactlyAsync<IOException>(() => coordinator.ExecuteCredentialTransitionWithFaultAsync(
            "delete-before-helper-attempt",
            bootstrap,
            assignment,
            BaseTime.AddSeconds(4),
            CredentialLifecycleFaultPoint.AfterDeletePendingBeforeHelper));

        CredentialProfileProjection pending = context.Store.GetCredentialProfile("profile-restore");
        Assert.AreEqual("delete-pending", pending.LifecycleState);
        Assert.AreEqual(1, pending.RevocationEpoch);
        InvalidOperationException rejected = Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.AuthorizeProviderDispatch(context.GateRequest));
        StringAssert.Contains(rejected.Message, "final dispatch gate rejected");
        Assert.AreEqual(0, context.CountLedgerRoots().Fences);
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
        assignment.Assignment.ProviderRequest.InputBoundProof.PolicyVersion = "v2";
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
        ProviderOperationSummaryProjection projection = coordinator.QueryOperation(new(
            new OpaqueId("operation-restore"), IncludeUsage: true, IncludeSettlement: true, IncludeReplay: true));
        Assert.AreEqual(ProviderOperationState.Settled, projection.State);
        Assert.AreEqual(400_000, projection.CalculatedNanoUsd);
        Assert.AreEqual("retained-response", projection.ReplayState);
        OpenAiResponsesResult replay = coordinator.Replay(new(
            new OpaqueId("operation-restore"), new OpaqueId("production-simulator:response"), NetworkPermitted: false));
        Assert.AreEqual(ProviderResponseState.Malformed, replay.State);
        Assert.IsFalse(replay.NetworkUsed);
        Assert.AreEqual(0, replay.SendCount);
        Assert.ThrowsExactly<InvalidDataException>(() => coordinator.PublishTerminalV2(
            new("run-restore"), new("local-run-output-v1"), "local-output-v1"u8.ToArray(),
            "local-cli-v1"u8.ToArray(), new("operation-restore"), BaseTime.AddSeconds(11)));
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
            JOIN provider_replay_edges replay
              ON replay.response_record_id=response.response_record_id
             AND replay.operation_id=response.operation_id
             AND replay.replay_state='retained-response'
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
    public void SourceClaimAdmissionPersistsReadsBackAndPublishesExactAcquisitionOwnership()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt gate = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        ProviderAccountingCoordinator accounting = new(context.Store);
        _ = accounting.SimulatePersistAndSettle(
            gate, "authorization-settlement", "operation-restore", "reservation-settlement",
            "attempt-settlement", "request-settlement", "source-claim-persist",
            ProviderSimulatorOutcome.Completed, new(65_536, 20, 10, 1_048_576, 1, 400_000, 120_000),
            BaseTime.AddSeconds(7));

        using (SqliteConnection database = new($"Data Source={context.Store.Paths.Database};Pooling=False"))
        {
            database.Open();
            using SqliteCommand seed = database.CreateCommand();
            seed.CommandText =
                """
                INSERT INTO documentation_revisions VALUES(
                  'source-claim-revision','source-claim-doc','fixture','1',NULL,NULL,
                  'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc',0,
                  'unavailable','unavailable','unavailable','2026-08-10T00:00:06.0000000+00:00');
                """;
            Assert.AreEqual(1, seed.ExecuteNonQuery());
        }
        context.Store.RegisterSourceClaimAcquisition(new(
            "acquisition-restore", "install-restore", "context-restore", "config-restore", "manifest-restore",
            "run-restore", "application-restore", "cost-restore", "source-claim-job", "source-claim-command",
            "source-claim-parent", "source-claim-revision", BaseTime.AddSeconds(6)));
        Assert.IsEmpty(context.Store.ReadSourceClaimApplicationLinks("acquisition-restore"),
            "Acquisition registration must not pre-author provider proposal or application identities.");
        (SourceClaimExecutionInput fixtureInput, SourceClaimRetainedTranscript[] fixtureTranscripts) =
            SourceClaimAdmissionIntegrationTests.Load("S6-CLAIM-DEV-v1");
        const string deletedText = "This retained passage has been deleted and is audit-only.";
        SourceClaimExecutionInput input = fixtureInput with
        {
            AcquisitionRunId = "acquisition-restore",
            OperationId = "operation-restore",
            HostAuthorizationId = "authorization-settlement",
            OwnerId = "acquisition-restore",
            ParentAnalysisRunId = "run-restore",
            ApplicationScopeId = "application-restore",
            CostAttributionScopeId = "cost-restore",
            SourceRevisionId = "source-claim-revision",
            ApplicableConditionIds = ["condition-mode-copper"],
            Passages =
            [
                .. fixtureInput.Passages.Select(x => x with { SourceRevisionId = "source-claim-revision" }),
                new("deleted-provider-passage", "source-claim-revision", deletedText,
                    Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(deletedText))), true),
            ],
        };
        SourceClaimRetainedTranscript transcript = fixtureTranscripts[0] with
        {
            TranscriptId = "provider-returned-arbitrary-set",
            OperationId = "operation-restore",
            ResponseRecordId = "source-claim-persist:response",
            SourceRevisionId = "source-claim-revision",
            Proposals =
            [
                fixtureTranscripts[0].Proposals[0] with
                {
                    ProposalId = "provider-returned-arbitrary-omega",
                    ApplicationSemantics = "applicability-only",
                },
                fixtureTranscripts[1].Proposals[0] with
                {
                    ProposalId = "provider-returned-arbitrary-unsupported",
                    PassageId = fixtureInput.Passages[1].PassageId,
                },
                fixtureTranscripts[1].Proposals[0] with
                {
                    ProposalId = "provider-returned-arbitrary-abstained",
                    PassageId = fixtureInput.Passages[1].PassageId,
                    State = "abstained",
                },
                fixtureTranscripts[0].Proposals[0] with
                {
                    ProposalId = "provider-returned-arbitrary-deleted",
                    PassageId = "deleted-provider-passage",
                    ConditionIds = [],
                    ConditionScope = "unconditional",
                },
            ],
            Gaps = ["Provider returned non-admitted proposals alongside the admitted proposal."],
        };
        SourceClaimAcquisitionCoordinator coordinator = new(context.Store);
        Assert.ThrowsExactly<InvalidDataException>(() => coordinator.AdmitRetainedTranscript(
            input, transcript, "wrong-authorization", "attempt-settlement", "request-settlement",
            gate.DispatchFenceId, BaseTime.AddSeconds(8)));
        SourceClaimAdmissionPublication admission = coordinator.AdmitRetainedTranscript(
            input, transcript, "authorization-settlement", "attempt-settlement", "request-settlement",
            gate.DispatchFenceId, BaseTime.AddSeconds(8));
        SourceClaimPersistenceReceipt persisted = admission.Persistence;
        Assert.AreEqual(4, persisted.AdmissionCount);
        ProviderSemanticAdmissionReadModel[] admissions = context.Store
            .ReadSourceClaimAdmissions("acquisition-restore").ToArray();
        CollectionAssert.AreEquivalent(AllSourceClaimDecisionStates, admissions.Select(x => x.DecisionState).ToArray());
        Assert.IsTrue(admissions.All(x => !string.IsNullOrWhiteSpace(x.SupportState)
            && !string.IsNullOrWhiteSpace(x.ApplicabilityState)
            && !string.IsNullOrWhiteSpace(x.DecisionState)),
            "Persisted source admissions must expose every independent semantic axis.");
        Assert.IsEmpty(context.Store.ReadSourceClaimApplicationLinks("acquisition-restore"),
            "Persistence and admission must not imply later consuming-analysis application.");
        SourceClaimExtractionDocument document = context.Store.ReadSourceClaimExtraction(
            "acquisition-restore", "admission-provider-returned-arbitrary-omega");
        Assert.IsTrue(document.AdmissionCorrelations.All(
            x => x.AuthorizationId.Value == "authorization-settlement"));
        Assert.ThrowsExactly<InvalidDataException>(() => context.Store.PersistSourceClaimExtraction(new(
            document, "authorization-settlement", "wrong-response", "attempt-settlement", "request-settlement",
            gate.DispatchFenceId, BaseTime.AddSeconds(9))));
        foreach (ProviderSemanticAdmissionReadModel nonAdmitted in admissions.Where(x => x.DecisionState != "admitted"))
        {
            Assert.ThrowsExactly<InvalidDataException>(() => context.Store.ConsumeAdmittedSourceClaim(new(
                "consumer-link-" + nonAdmitted.ProposalId, "acquisition-restore", nonAdmitted.AdmissionId, "run-restore",
                "application-restore", "cost-restore", BaseTime.AddSeconds(9))));
        }
        Assert.IsEmpty(context.Store.ReadSourceClaimApplicationLinks("acquisition-restore"));
        Assert.ThrowsExactly<InvalidDataException>(() => context.Store.ConsumeAdmittedSourceClaim(new(
            "consumer-link-before-admission", "acquisition-restore",
            "admission-provider-returned-arbitrary-omega", "run-restore",
            "application-restore", "cost-restore", BaseTime.AddSeconds(7))));
        SourceClaimConsumptionReceipt consumed = context.Store.ConsumeAdmittedSourceClaim(new(
            "consumer-analysis-source-claim-link", "acquisition-restore",
            "admission-provider-returned-arbitrary-omega", "run-restore",
            "application-restore", "cost-restore", BaseTime.AddSeconds(10)));
        SourceClaimApplicationReadModel applied = context.Store
            .ReadSourceClaimApplicationLinks("acquisition-restore").Single();
        Assert.AreEqual(consumed.AdmittedArtifactId, applied.AdmittedArtifactId);
        Assert.AreEqual("admission-provider-returned-arbitrary-omega", applied.AdmissionId);
        Assert.AreEqual("consumer-analysis-source-claim-link", applied.ApplicationLinkId);
        ProviderTerminalPublicationArtifacts publication = accounting.PublishTerminalV2(
            new("run-restore"), new("local-run-output-v1"), "local-output-v1"u8.ToArray(),
            "local-cli-v1"u8.ToArray(), new("operation-restore"), BaseTime.AddSeconds(11));
        Assert.AreEqual("acquisition-restore", publication.RunOutputV2.EvidenceAcquisitionRunIds.Single().Value);
        Assert.AreEqual("admission-provider-returned-arbitrary-omega",
            publication.RunOutputV2.ProviderOperations.Single().AdmissionId?.Value);

        BackupArtifact backup = context.Store.CreateBackup("Wp6SourceClaim", BaseTime.AddSeconds(12));
        string restoredRoot = Path.Combine(Path.GetTempPath(), "Infinium-Wp6-Restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (StoragePaths targetPaths = new(restoredRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, targetPaths);
            }
            using StoragePaths restoredPaths = new(restoredRoot);
            using AuthoritativeStore restored = new(restoredPaths);
            Assert.AreEqual(4, restored.ReadSourceClaimAdmissions("acquisition-restore").Count);
            Assert.IsTrue(restored.ReadSourceClaimExtraction(
                "acquisition-restore", "admission-provider-returned-arbitrary-omega").ClaimProposals
                .Any(x => x.ProposalId.Value == "provider-returned-arbitrary-omega"));
            Assert.AreEqual("consumer-analysis-source-claim-link",
                restored.ReadSourceClaimApplicationLinks("acquisition-restore").Single().ApplicationLinkId);
            Assert.AreEqual("admission-provider-returned-arbitrary-omega",
                restored.ReadSourceClaimApplicationLinks("acquisition-restore").Single().AdmissionId);
            _ = restored.RebuildProviderBudgetProjections(BaseTime.AddSeconds(13));
        }
        finally
        {
            if (Directory.Exists(restoredRoot))
            {
                DeleteDirectoryWithRetry(restoredRoot);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void CandidateAdmissionPersistsReadsBackBacksUpAndRebuildsWithoutSending()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt gate = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        ProviderAccountingCoordinator accounting = new(context.Store);
        _ = accounting.SimulatePersistAndSettle(
            gate, "authorization-settlement", "operation-restore", "reservation-settlement",
            "attempt-settlement", "request-settlement", "candidate-seed",
            ProviderSimulatorOutcome.Completed, new(65_536, 20, 10, 1_048_576, 1, 400_000, 120_000),
            BaseTime.AddSeconds(7));

        RunBinding candidateRunBinding = new(
            "install-restore", "context-restore", "config-restore", "manifest-restore");
        using (SqliteConnection runDatabase = new($"Data Source={context.Store.Paths.Database};Pooling=False"))
        {
            runDatabase.Open();
            using SqliteCommand activateRun = runDatabase.CreateCommand();
            activateRun.CommandText =
                """
                UPDATE runs SET lifecycle_state='Running' WHERE run_id='run-restore';
                INSERT INTO job_nodes VALUES('run-restore-root','run-restore',NULL,'analysis-run-root','Running',0,
                  '2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
                """;
            Assert.AreEqual(2, activateRun.ExecuteNonQuery());
        }
        DateTimeOffset candidateAnalysisNow = DateTimeOffset.UtcNow;
        AttemptRecord candidateAnalysisAttempt = context.Store.CreateAttempt(
            "run-restore", context.Authority.FencingEpoch, TimeSpan.FromMinutes(5), candidateAnalysisNow);
        CausalJoinPopulationMember candidateMember = CandidatePipelineIntegrationTests.Member("wp7") with
        {
            SupportingEvidenceIds = [new OpaqueId("candidate-evidence")],
        };
        CausalJoinPopulationMember deletedCandidateMember = CandidatePipelineIntegrationTests.Member("wp7-deleted") with
        {
            SupportingEvidenceIds = [new OpaqueId("candidate-evidence-deleted")],
        };
        CausalJoinPopulationMember unavailableCandidateMember = CandidatePipelineIntegrationTests.Member("wp7-unavailable") with
        {
            SupportingEvidenceIds = [new OpaqueId("candidate-evidence-unavailable")],
        };
        TestCandidatePopulationSource candidateSource = new(
            new OpaqueId("analyzer-integration"), [candidateMember, deletedCandidateMember, unavailableCandidateMember]);
        CandidatePipelineRequest candidateAnalysisRequest = new(
            new("run-restore"), new("candidate-population"), new("candidate-policy"), new("candidate-threshold"),
            CandidateExecutionLimits.Default,
            new CandidatePopulationContext(null, new("run-restore"), new("install-restore"),
                new("context-restore"), new("config-restore")),
            [candidateSource],
            CandidatePipelineIntegrationTests.ExecutionInput(candidateSource, "run-restore", "install-restore",
                "context-restore", "config-restore", "manifest-restore"));
        CandidateAnalysisPhaseResult candidateAnalysis = CandidateAnalysisPhase.Execute(
            context.Store, candidateAnalysisRequest, candidateAnalysisAttempt, candidateRunBinding,
            candidateAnalysisNow);
        CandidateAnalysisEntryContract hostCandidate = candidateAnalysis.Pipeline.Analysis.Candidates.Single(item =>
            item.SupportingEvidenceIds.Contains(new("candidate-evidence")));
        CandidateHypothesisContract hostHypothesis = candidateAnalysis.Pipeline.Analysis.Hypotheses.Single(item =>
            item.CandidateId == hostCandidate.CandidateId);
        CandidateDecisionContract hostDecision = candidateAnalysis.Pipeline.Analysis.Decisions.Single(item =>
            item.DecisionId == hostCandidate.DecisionId);
        string hostCandidateId = hostCandidate.CandidateId.Value;
        string hostHypothesisId = hostHypothesis.HypothesisId.Value;

        byte[] candidateRequestBytes = Enumerable.Repeat((byte)0x5a, 1024).ToArray();
        string candidateRequestSha256 = Convert.ToHexStringLower(SHA256.HashData(candidateRequestBytes));
        string candidatePayloadDirectory = Path.Combine(context.Root, "payloads", candidateRequestSha256[..2], candidateRequestSha256[2..4]);
        Directory.CreateDirectory(candidatePayloadDirectory);
        File.WriteAllBytes(Path.Combine(candidatePayloadDirectory, candidateRequestSha256), candidateRequestBytes);

        using (SqliteConnection database = new($"Data Source={context.Store.Paths.Database};Pooling=False"))
        {
            database.Open();
            using (SqliteCommand seed = database.CreateCommand())
            {
                seed.Parameters.AddWithValue("$candidate_request_sha", candidateRequestSha256);
                seed.CommandText =
                    """
                    INSERT INTO payloads VALUES('candidate-request-payload',$candidate_request_sha,1024,'application/json','retained',
                      'payloads/' || substr($candidate_request_sha,1,2) || '/' || substr($candidate_request_sha,3,2) || '/' || $candidate_request_sha,
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO job_nodes VALUES('candidate-job','run-restore',NULL,'provider','created',0,
                      '2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
                    INSERT INTO durable_commands VALUES('candidate-command','provider','run-restore',1,'recorded','created',NULL,
                      '2026-08-10T00:00:00.0000000+00:00',NULL,NULL);
                    INSERT INTO provider_command_bindings VALUES('candidate-command','analysis-run','run-restore',
                      '2026-08-10T00:00:00.0000000+00:00');
                    INSERT INTO documentation_revisions VALUES('candidate-doc-revision','candidate-doc','fixture','1',NULL,NULL,
                      'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc',0,
                      'unavailable','unavailable','unavailable','2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO documentation_imports VALUES('candidate-import','run-restore','candidate-doc-revision','clean-import',NULL,
                      'candidate-import-closure','fixture-extractor','none','none','request-payload-restore','request-payload-restore',
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO documentation_passages VALUES('candidate-passage','candidate-doc-revision',0,1,
                      '1111111111111111111111111111111111111111111111111111111111111111',NULL,'unavailable',
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO documentation_passages VALUES('candidate-passage-deleted','candidate-doc-revision',1,2,
                      '2222222222222222222222222222222222222222222222222222222222222222',NULL,'unavailable',
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO documentation_passages VALUES('candidate-passage-unavailable','candidate-doc-revision',2,3,
                      '3333333333333333333333333333333333333333333333333333333333333333',NULL,'unavailable',
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO evidence_revisions VALUES('candidate-evidence','candidate-passage','candidate-import','fixture-evidence','1.0.0',
                      'documentation-claim','known-issue','test-result','applicable','observed','admitted','candidate-request-payload',NULL,
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO evidence_revisions VALUES('candidate-evidence-deleted','candidate-passage-deleted','candidate-import','fixture-evidence','1.0.0',
                      'documentation-claim','known-issue','test-result','applicable','observed','deleted','candidate-request-payload',NULL,
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO evidence_revisions VALUES('candidate-evidence-unavailable','candidate-passage-unavailable','candidate-import','fixture-evidence','1.0.0',
                      'documentation-claim','known-issue','test-result','applicable','observed','unavailable','candidate-request-payload',NULL,
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO documentation_application_bindings VALUES('candidate-binding','run-restore','install-restore','context-restore',
                      'manifest-restore','candidate-subject','installed-entity','candidate-closure',
                      '2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO evidence_application_links VALUES('evidence-application-d01','candidate-evidence','run-restore',
                      'candidate-binding','context-restore','candidate-subject','installed-entity','candidate-closure','applicable',
                      'request-payload-restore','2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO evidence_application_links VALUES('evidence-application-deleted','candidate-evidence-deleted','run-restore',
                      'candidate-binding','context-restore','candidate-subject','installed-entity','candidate-closure','applicable',
                      'request-payload-restore','2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO evidence_application_links VALUES('evidence-application-unavailable','candidate-evidence-unavailable','run-restore',
                      'candidate-binding','context-restore','candidate-subject','installed-entity','candidate-closure','applicable',
                      'request-payload-restore','2026-08-10T00:00:01.0000000+00:00');
                    INSERT INTO documentation_deletion_receipts VALUES('candidate-deletion-receipt','run-restore',
                      'candidate-doc-revision','dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd',
                      '["candidate-passage-deleted"]','["request-payload-restore"]','audit-only',
                      'request-payload-restore','candidate deleted fixture passage',
                      '2026-08-10T00:00:02.0000000+00:00');
                    """;
                Assert.AreEqual(17, seed.ExecuteNonQuery());
            }
            context.Store.RegisterSourceClaimAcquisition(new(
                "acquisition-restore", "install-restore", "context-restore", "config-restore", "manifest-restore",
                "run-restore", "application-restore", "cost-restore", "candidate-source-job", "candidate-source-command",
                "candidate-source-parent", "candidate-doc-revision", BaseTime.AddSeconds(6)));
            (SourceClaimExecutionInput sourceFixture, SourceClaimRetainedTranscript[] sourceTranscripts) =
                SourceClaimAdmissionIntegrationTests.Load("S6-CLAIM-DEV-v1");
            SourceClaimPassageInput sourcePassage = sourceFixture.Passages[0] with
            {
                PassageId = "candidate-passage",
                SourceRevisionId = "candidate-doc-revision",
            };
            SourceClaimPassageInput deletedSourcePassage = sourcePassage with
            {
                PassageId = "candidate-passage-deleted",
            };
            SourceClaimPassageInput unavailableSourcePassage = sourcePassage with
            {
                PassageId = "candidate-passage-unavailable",
            };
            SourceClaimExecutionInput sourceInput = sourceFixture with
            {
                AcquisitionRunId = "acquisition-restore",
                OperationId = "operation-restore",
                HostAuthorizationId = "authorization-settlement",
                OwnerId = "acquisition-restore",
                ParentAnalysisRunId = "run-restore",
                ApplicationScopeId = "application-restore",
                CostAttributionScopeId = "cost-restore",
                SourceRevisionId = "candidate-doc-revision",
                ApplicableConditionIds = ["condition-mode-copper"],
                Passages = [sourcePassage, deletedSourcePassage, unavailableSourcePassage],
            };
            SourceClaimRetainedTranscript sourceTranscript = sourceTranscripts[0] with
            {
                TranscriptId = "candidate-source-transcript",
                OperationId = "operation-restore",
                ResponseRecordId = "candidate-seed:response",
                SourceRevisionId = "candidate-doc-revision",
                Proposals = [sourceTranscripts[0].Proposals[0] with
                {
                    ProposalId = "candidate-source-proposal",
                    PassageId = "candidate-passage",
                    ApplicationSemantics = "applicability-only",
                }],
            };
            SourceClaimAdmissionPublication sourceAdmission = new SourceClaimAcquisitionCoordinator(context.Store)
                .AdmitRetainedTranscript(sourceInput, sourceTranscript, "authorization-settlement", "attempt-settlement",
                    "request-settlement", gate.DispatchFenceId, BaseTime.AddSeconds(8));
            Assert.AreEqual(1, sourceAdmission.Persistence.AdmissionCount);
            _ = context.Store.ConsumeAdmittedSourceClaim(new(
                "source-application-d01", "acquisition-restore", "admission-candidate-source-proposal", "run-restore",
                "application-restore", "cost-restore", BaseTime.AddSeconds(9)));
            CloneRow(database, "provider_operation_blocks", "operation_id='operation-restore'", new Dictionary<string, object?>()
            {
                ["operation_id"] = "candidate-operation",
                ["owner_kind"] = "analysis-run",
                ["owner_id"] = "run-restore",
                ["job_node_id"] = "candidate-job",
                ["command_id"] = "candidate-command",
                ["operation_kind"] = "candidate-investigation",
                ["prompt_id"] = CandidateInvestigationPromptV1.Id,
                ["prompt_fingerprint"] = CandidateInvestigationPromptV1.Fingerprint,
                ["request_fingerprint"] = candidateRequestSha256,
                ["canonical_request_payload_id"] = "candidate-request-payload",
                ["canonical_request_fingerprint"] = candidateRequestSha256,
            });
            CloneRow(database, "provider_operation_authorizations", "authorization_id='authorization-settlement'", new Dictionary<string, object?>()
            {
                ["authorization_id"] = "candidate-authorization",
                ["operation_id"] = "candidate-operation",
                ["owner_kind"] = "analysis-run",
                ["owner_id"] = "run-restore",
                ["analysis_run_id"] = "run-restore",
                ["evidence_acquisition_run_id"] = null,
                ["job_node_id"] = "candidate-job",
                ["command_id"] = "candidate-command",
                ["operation_kind"] = "candidate-investigation",
                ["prompt_id"] = CandidateInvestigationPromptV1.Id,
                ["prompt_fingerprint"] = CandidateInvestigationPromptV1.Fingerprint,
                ["request_fingerprint"] = candidateRequestSha256,
                ["canonical_request_fingerprint"] = candidateRequestSha256,
            });
            CloneRow(database, "provider_operation_attempts", "provider_attempt_id='attempt-settlement'", new Dictionary<string, object?>()
            {
                ["provider_attempt_id"] = "candidate-attempt",
                ["operation_id"] = "candidate-operation",
            });
            CloneRow(database, "provider_requests", "request_id='request-settlement'", new Dictionary<string, object?>()
            {
                ["request_id"] = "candidate-request",
                ["client_request_id"] = "candidate-client-request",
                ["operation_id"] = "candidate-operation",
                ["provider_attempt_id"] = "candidate-attempt",
                ["request_fingerprint"] = candidateRequestSha256,
                ["canonical_request_fingerprint"] = candidateRequestSha256,
                ["payload_id"] = "candidate-request-payload",
                ["payload_fingerprint"] = candidateRequestSha256,
            });
            CloneRow(database, "provider_reservations", "reservation_id='reservation-settlement'", new Dictionary<string, object?>()
            {
                ["reservation_id"] = "candidate-reservation",
                ["operation_id"] = "candidate-operation",
                ["provider_attempt_id"] = "candidate-attempt",
                ["request_id"] = "candidate-request",
            });
            using (SqliteCommand scopeItems = database.CreateCommand())
            {
                scopeItems.CommandText =
                    """
                    INSERT INTO provider_reservation_scope_items
                    SELECT 'candidate-' || reservation_scope_item_id,'candidate-reservation',scope_kind,scope_id,
                      usage_json,nano_usd
                    FROM provider_reservation_scope_items WHERE reservation_id='reservation-settlement';
                    """;
                Assert.IsGreaterThanOrEqualTo(1, scopeItems.ExecuteNonQuery());
            }
            CloneRow(database, "provider_dispatch_fences", "dispatch_fence_id='fence-settlement'", new Dictionary<string, object?>()
            {
                ["dispatch_fence_id"] = "candidate-fence",
                ["authorization_id"] = "candidate-authorization",
                ["operation_id"] = "candidate-operation",
                ["reservation_id"] = "candidate-reservation",
                ["provider_attempt_id"] = "candidate-attempt",
                ["request_id"] = "candidate-request",
            });
            using (SqliteCommand transport = database.CreateCommand())
            {
                transport.CommandText =
                    """
                    INSERT INTO provider_transport_events
                    SELECT 'candidate-' || transport_event_id,'candidate-operation','candidate-attempt','candidate-request',
                      'candidate-fence',event_kind,sequence,occurred_at
                    FROM provider_transport_events WHERE operation_id='operation-restore';
                    """;
                Assert.IsGreaterThanOrEqualTo(1, transport.ExecuteNonQuery());
            }
            CloneRow(database, "provider_responses", "response_record_id='candidate-seed:response'", new Dictionary<string, object?>()
            {
                ["response_record_id"] = "candidate-response",
                ["authorization_id"] = "candidate-authorization",
                ["operation_id"] = "candidate-operation",
                ["owner_kind"] = "analysis-run",
                ["owner_id"] = "run-restore",
                ["request_id"] = "candidate-request",
                ["provider_attempt_id"] = "candidate-attempt",
                ["reservation_id"] = "candidate-reservation",
                ["dispatch_fence_id"] = "candidate-fence",
                ["operation_kind"] = "candidate-investigation",
                ["client_request_id"] = "candidate-client-request",
            });
            CloneRow(database, "provider_usage_entries", "usage_entry_id='candidate-seed:usage'", new Dictionary<string, object?>()
            {
                ["usage_entry_id"] = "candidate-usage",
                ["receipt_id"] = "candidate-receipt",
                ["operation_id"] = "candidate-operation",
                ["provider_attempt_id"] = "candidate-attempt",
                ["request_id"] = "candidate-request",
                ["dispatch_fence_id"] = "candidate-fence",
                ["response_record_id"] = "candidate-response",
            });
            using (SqliteCommand rateFacts = database.CreateCommand())
            {
                rateFacts.CommandText =
                    """
                    INSERT INTO provider_rate_limit_facts
                    SELECT 'candidate-' || rate_limit_fact_id,'candidate-usage',scope,dimension,availability,
                      limit_value,remaining_value,observed_at,resets_at
                    FROM provider_rate_limit_facts WHERE usage_entry_id='candidate-seed:usage';
                    """;
                Assert.AreEqual(1, rateFacts.ExecuteNonQuery());
            }
            using (SqliteCommand diagnostic = database.CreateCommand())
            {
                diagnostic.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM provider_responses r JOIN provider_usage_entries u ON u.response_record_id=r.response_record_id
                    WHERE r.response_record_id='candidate-response' AND u.usage_entry_id='candidate-usage'
                      AND r.response_state='completed' AND u.dispatch_count=1
                      AND u.input_tokens <= r.maximum_input_tokens AND u.output_tokens <= r.maximum_output_tokens
                      AND u.cache_read_tokens=0 AND u.cache_write_tokens=0 AND u.priced_tool_calls=0
                      AND u.calculated_nano_usd <= r.maximum_calculated_nano_usd
                      AND ((u.rate_availability='available' AND r.expected_rate_limit_fact_count=(
                        SELECT COUNT(*) FROM provider_rate_limit_facts fact WHERE fact.usage_entry_id=u.usage_entry_id))
                        OR (u.rate_availability<>'available' AND NOT EXISTS(
                          SELECT 1 FROM provider_rate_limit_facts fact WHERE fact.usage_entry_id=u.usage_entry_id)));
                    """;
                Assert.AreEqual(1L, (long)diagnostic.ExecuteScalar()!);
            }
            using (SqliteCommand finalization = database.CreateCommand())
            {
                finalization.CommandText =
                    """
                    INSERT INTO provider_response_finalizations(
                      finalization_id,response_record_id,usage_entry_id,validation_state,admission_state,finalized_at)
                    VALUES(
                      'candidate-finalization','candidate-response','candidate-usage','admitted','admitted',
                      '2026-08-10T00:00:20.0000000+00:00');
                    """;
                Assert.AreEqual(1, finalization.ExecuteNonQuery());
            }
            CloneRow(database, "provider_replay_edges", "replay_edge_id='candidate-seed:response:replay'", new Dictionary<string, object?>()
            {
                ["replay_edge_id"] = "candidate-response:replay",
                ["operation_id"] = "candidate-operation",
                ["provider_attempt_id"] = "candidate-attempt",
                ["request_id"] = "candidate-request",
                ["response_record_id"] = "candidate-response",
                ["dispatch_fence_id"] = "candidate-fence",
            });
            CloneRow(database, "provider_settlements", "settlement_id='candidate-seed:settlement'", new Dictionary<string, object?>()
            {
                ["settlement_id"] = "candidate-settlement",
                ["operation_id"] = "candidate-operation",
                ["provider_attempt_id"] = "candidate-attempt",
                ["request_id"] = "candidate-request",
                ["reservation_id"] = "candidate-reservation",
                ["usage_entry_id"] = "candidate-usage",
                ["dispatch_fence_id"] = "candidate-fence",
            });
            using (SqliteCommand budgetEvents = database.CreateCommand())
            {
                budgetEvents.CommandText =
                    """
                    INSERT INTO provider_budget_events
                    SELECT 'candidate-' || budget_event_id,'candidate-reservation',
                      CASE WHEN usage_entry_id IS NULL THEN NULL ELSE 'candidate-usage' END,
                      scope_kind,scope_id,event_kind,dispatch_count,input_tokens,output_tokens,total_tokens,
                      reasoning_tokens,cache_read_tokens,cache_write_tokens,priced_tool_calls,nano_usd,sequence,occurred_at
                    FROM provider_budget_events WHERE reservation_id='reservation-settlement';
                    """;
                Assert.IsGreaterThanOrEqualTo(2, budgetEvents.ExecuteNonQuery());
            }
            CloneRow(database, "provider_budget_settlement_receipts", "settlement_id='candidate-seed:settlement'", new Dictionary<string, object?>()
            {
                ["settlement_id"] = "candidate-settlement",
                ["reservation_id"] = "candidate-reservation",
                ["usage_entry_id"] = "candidate-usage",
            });
        }

        string retainedResponseFingerprint;
        using (SqliteConnection responseDatabase = new($"Data Source={context.Store.Paths.Database};Mode=ReadOnly;Pooling=False"))
        {
            responseDatabase.Open();
            using SqliteCommand responseIdentity = responseDatabase.CreateCommand();
            responseIdentity.CommandText =
                "SELECT raw_response_fingerprint FROM provider_responses WHERE response_record_id='candidate-response';";
            retainedResponseFingerprint = (string)responseIdentity.ExecuteScalar()!;
        }
        (CandidateInvestigationExecutionInput fixtureInput, CandidateInvestigationRetainedTranscript[] fixtureTranscripts) =
            CandidateAdmissionProviderReplayIntegrationTests.Load("S6-CANDIDATE-DEV-v2");
        CandidateInvestigationContextInput fixtureContext = fixtureInput.Contexts[0];
        CandidateEvidenceInput durableEvidence = fixtureContext.Evidence[0] with
        {
            EvidenceId = "candidate-evidence",
            EvidenceApplicationLinkId = "evidence-application-d01",
            SourceAcquisitionId = "acquisition-restore",
            SourceAdmissionId = "admission-candidate-source-proposal",
            SourceApplicationLinkId = "source-application-d01",
            SourceRevisionId = "candidate-doc-revision",
            PassageId = "candidate-passage",
            ContentSha256 = candidateRequestSha256,
        };
        CandidateInvestigationContextInput durableContext = fixtureContext with
        {
            CandidateId = hostCandidateId,
            HypothesisId = hostHypothesisId,
            Hypothesis = hostHypothesis.ProposedExplanation,
            ParticipantIds = hostDecision.Participants.Select(item => item.ParticipantId.Value).ToArray(),
            ParticipantRoles = hostDecision.Participants.Select(item => item.Role).ToArray(),
            CausalPathIds = hostDecision.Path.Select(item => item.Value).ToArray(),
            DependencyClosureId = hostDecision.DependencyClosureId.Value,
            Evidence = [durableEvidence],
        };
        CandidateInvestigationExecutionInput input = fixtureInput with
        {
            OperationId = "candidate-operation",
            HostAuthorizationId = "candidate-authorization",
            OwnerId = "run-restore",
            AnalysisRunId = "run-restore",
            ApplicationScopeId = "application-restore",
            CostAttributionScopeId = "cost-restore",
            Contexts =
            [
                durableContext,
                fixtureInput.Contexts[1],
            ],
        };
        CandidateInvestigationRetainedTranscript transcript = fixtureTranscripts[0] with
        {
            OperationId = "candidate-operation",
            ResponseRecordId = "candidate-response",
            ResponseFingerprint = retainedResponseFingerprint,
            Proposals = [fixtureTranscripts[0].Proposals[0] with
            {
                CandidateId = hostCandidateId,
                HypothesisId = hostHypothesisId,
                Hypothesis = hostHypothesis.ProposedExplanation,
                SupportingEvidenceIds = ["candidate-evidence"],
            }],
        };
        DurableCandidateInvestigationCoordinator coordinator = new(context.Store);
        CandidateInvestigationScenarioResult directScenario =
            CandidateInvestigationEngine.Execute(input, [transcript]).Scenarios.Single();
        CandidateEvidenceProvenanceBinding directBinding = CandidateBinding(durableEvidence);
        CandidateEvidenceInput secondEvidence = durableEvidence with
        {
            EvidenceId = "candidate-evidence-second",
            EvidenceApplicationLinkId = "candidate-evidence-application-second",
            SourceApplicationLinkId = "candidate-source-application-second",
            Relationship = "neutral",
        };
        CandidateInvestigationExecutionInput partialInput = input with
        {
            Contexts = [durableContext with { Evidence = [durableEvidence, secondEvidence] }, fixtureInput.Contexts[1]],
        };
        CandidateInvestigationScenarioResult partialScenario =
            CandidateInvestigationEngine.Execute(partialInput, [transcript]).Scenarios.Single();
        CandidateEvidenceProvenanceBinding extraBinding = CandidateBinding(secondEvidence);
        CandidateInvestigationRetainedTranscript noModelTranscript = transcript with
        {
            ResponseState = "not-used",
            Proposals = [],
            Abstentions = ["Model execution was not selected."],
            Gaps = ["No model response is available."],
            ModelUsed = false,
        };
        CandidateInvestigationScenarioResult noModelScenario =
            CandidateInvestigationEngine.Execute(input, [noModelTranscript]).Scenarios.Single();
        CandidateInvestigationPersistenceRequest responseEnvelope =
            CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-envelope");
        CandidateInvestigationPersistenceRequest noModelEnvelope =
            CandidatePersistenceRequest(noModelScenario, input, noModelTranscript, [directBinding], "outcome-no-model-envelope");
        foreach (CandidateInvestigationPersistenceRequest invalidRequest in new[]
                 {
                     responseEnvelope with { ResponseRecordId = null },
                     responseEnvelope with { ProviderAttemptId = null },
                     responseEnvelope with { RequestId = null },
                     responseEnvelope with { DispatchFenceId = null },
                     responseEnvelope with
                     {
                         ResponseRecordId = null, ProviderAttemptId = null, RequestId = null, DispatchFenceId = null,
                     },
                     noModelEnvelope with { ResponseRecordId = "extra-response" },
                     noModelEnvelope with { ProviderAttemptId = "extra-attempt" },
                     noModelEnvelope with { RequestId = "extra-request" },
                     noModelEnvelope with { DispatchFenceId = "extra-fence" },
                     noModelEnvelope with
                     {
                         ResponseRecordId = "extra-response", ProviderAttemptId = "extra-attempt",
                         RequestId = "extra-request", DispatchFenceId = "extra-fence",
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [], "outcome-direct-empty"),
                     CandidatePersistenceRequest(partialScenario, partialInput, transcript, [directBinding], "outcome-direct-partial"),
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding, extraBinding], "outcome-direct-extra"),
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding, directBinding], "outcome-direct-duplicate"),
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-input-authorization") with
                     {
                         InputPayload = JsonSerializer.SerializeToUtf8Bytes(
                             input with { HostAuthorizationId = "poisoned-authorization" }, SourceClaimContextMinimizer.JsonOptions),
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-input-scope") with
                     {
                         InputPayload = JsonSerializer.SerializeToUtf8Bytes(
                             input with { ApplicationScopeId = "poisoned-application-scope" }, SourceClaimContextMinimizer.JsonOptions),
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-input-prompt") with
                     {
                         InputPayload = JsonSerializer.SerializeToUtf8Bytes(
                             input with { PromptFingerprint = new string('0', 64) }, SourceClaimContextMinimizer.JsonOptions),
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-transcript-envelope") with
                     {
                         TranscriptPayload = JsonSerializer.SerializeToUtf8Bytes(
                             transcript with { TranscriptId = "poisoned-transcript" }, SourceClaimContextMinimizer.JsonOptions),
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-transcript-proposal") with
                     {
                         TranscriptPayload = JsonSerializer.SerializeToUtf8Bytes(transcript with
                         {
                             Proposals = [transcript.Proposals[0] with { Hypothesis = transcript.Proposals[0].Hypothesis + " poisoned" }],
                         }, SourceClaimContextMinimizer.JsonOptions),
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-transcript-state") with
                     {
                         TranscriptPayload = JsonSerializer.SerializeToUtf8Bytes(transcript with
                         {
                             Proposals = [transcript.Proposals[0] with { State = "unsupported" }],
                         }, SourceClaimContextMinimizer.JsonOptions),
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-transcript-gap") with
                     {
                         TranscriptPayload = JsonSerializer.SerializeToUtf8Bytes(
                             transcript with { Gaps = [.. transcript.Gaps, "poisoned gap"] },
                             SourceClaimContextMinimizer.JsonOptions),
                     },
                     CandidatePersistenceRequest(directScenario, input, transcript, [directBinding], "outcome-direct-state") with
                     {
                         Disposition = "poisoned-disposition",
                     },
                 })
        {
            Assert.ThrowsExactly<InvalidDataException>(() => context.Store.PersistCandidateInvestigation(invalidRequest));
        }
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(
            context.Store.Paths.Database, "candidate_investigation_outcomes"));
        CandidateInvestigationPersistenceRequest batchFirst = noModelEnvelope with
        {
            OutcomeId = "outcome-batch-first",
        };
        CandidateInvestigationPersistenceRequest batchSecond = noModelEnvelope with
        {
            OutcomeId = "outcome-batch-second",
            ContextId = "missing-second-context",
        };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            context.Store.PersistCandidateInvestigationBatch([batchFirst, batchSecond]));
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(
            context.Store.Paths.Database, "candidate_investigation_outcomes"),
            "A second-context failure must roll back the first context and its semantic graph.");
        foreach (CandidateInvestigationExecutionInput invalid in new[]
                 {
                     input with { Contexts = [durableContext with { Evidence =
                         [durableEvidence with { SourceAdmissionId = "invented-admission" }] }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { Evidence =
                         [durableEvidence with { EvidenceApplicationLinkId = "invented-application" }] }, fixtureInput.Contexts[1]] },
                     input with { ApplicationScopeId = "cross-scope" },
                     input with { Contexts = [durableContext with { CandidateId = "cross-candidate" }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { HypothesisId = "cross-hypothesis" }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { Hypothesis = durableContext.Hypothesis + " drift" }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { ParticipantRoles = [.. durableContext.ParticipantRoles.Skip(1), "drift-role"] }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { CausalPathIds = [.. durableContext.CausalPathIds, "drift-path"] }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { DependencyClosureId = "drift-closure" }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { Evidence = [durableEvidence with { Availability = "deleted" }] }, fixtureInput.Contexts[1]] },
                     input with { Contexts = [durableContext with { Evidence = [durableEvidence with { Availability = "unavailable" }] }, fixtureInput.Contexts[1]] },
                 })
        {
            Assert.ThrowsExactly<InvalidDataException>(() => coordinator.AdmitRetainedTranscript(
                invalid, transcript, "candidate-authorization", "candidate-attempt", "candidate-request",
                "candidate-fence", candidateAnalysisNow.AddSeconds(1)));
        }
        CandidateInvestigationAdmissionPublication publication = coordinator.AdmitRetainedTranscript(
            input, transcript, "candidate-authorization", "candidate-attempt", "candidate-request",
            "candidate-fence", candidateAnalysisNow.AddSeconds(1));
        Assert.AreEqual(1, publication.Persistence.ProposalCount);
        ProviderSemanticAdmissionReadModel candidateAdmission = context.Store
            .ReadCandidateInvestigationAdmissions("run-restore", hostCandidateId).Single();
        Assert.AreEqual("admitted", candidateAdmission.State);
        Assert.AreEqual("supported", candidateAdmission.SupportState);
        Assert.AreEqual("applicable", candidateAdmission.ApplicabilityState);
        Assert.AreEqual("admitted", candidateAdmission.DecisionState);
        Assert.AreEqual("proposal-d01", context.Store.ReadCandidateInvestigation(
            "run-restore", hostCandidateId, "admission-proposal-d01").HypothesisProposals.Single().ProposalId.Value);
        CandidateInvestigationOutcomeReadModel retained = context.Store.ReadCandidateInvestigationOutcome(
            "run-restore", "candidate-operation", fixtureContext.ContextId);
        Assert.AreEqual("outcome-transcript-d01", retained.OutcomeId);
        Assert.AreEqual(hostHypothesisId, retained.HypothesisId);
        Assert.AreEqual(publication.Scenario.CanonicalInvestigationSha256,
            new DurableCandidateInvestigationCoordinator(context.Store).ReplayRetained(
                "run-restore", "candidate-operation", fixtureContext.ContextId).CanonicalInvestigationSha256);

        CandidateInvestigationContextInput explicitAbstentionContext = durableContext with
        {
            ContextId = "context-explicit-abstention",
        };
        CandidateInvestigationExecutionInput explicitAbstentionInput = input with
        {
            Contexts = [explicitAbstentionContext, fixtureInput.Contexts[1]],
        };
        CandidateInvestigationRetainedTranscript explicitAbstentionTranscript = transcript with
        {
            TranscriptId = "transcript-explicit-abstention",
            ContextId = explicitAbstentionContext.ContextId,
            Proposals = [transcript.Proposals[0] with
            {
                ProposalId = "proposal-explicit-abstention",
                State = "abstained",
            }],
        };
        CandidateInvestigationAdmissionPublication explicitAbstention = coordinator.AdmitRetainedTranscript(
            explicitAbstentionInput, explicitAbstentionTranscript, "candidate-authorization", "candidate-attempt",
            "candidate-request", "candidate-fence", candidateAnalysisNow.AddSeconds(1));
        Assert.AreEqual("abstained-explicit", explicitAbstention.Scenario.Disposition);
        Assert.AreEqual("abstained-explicit", coordinator.ReplayRetained(
            "run-restore", "candidate-operation", explicitAbstentionContext.ContextId).Disposition);
        ProviderSemanticAdmissionReadModel explicitAbstentionAdmission = context.Store
            .ReadCandidateInvestigationAdmissionsForOperation("run-restore", "candidate-operation")
            .Single(x => x.ProposalId == "proposal-explicit-abstention");
        Assert.AreEqual("not-evaluated", explicitAbstentionAdmission.SupportState);
        Assert.AreEqual("applicable", explicitAbstentionAdmission.ApplicabilityState);
        Assert.AreEqual("abstained", explicitAbstentionAdmission.DecisionState);

        foreach ((string Availability, string EvidenceId, string EvidenceApplicationId, string PassageId,
                     string ContextId, string TranscriptId, string ProposalId, string ExpectedDisposition,
                     string ExpectedReplay) evidenceCase in new[]
                 {
                     ("deleted", "candidate-evidence-deleted", "evidence-application-deleted",
                         "candidate-passage-deleted", "context-durable-deleted", "transcript-durable-deleted",
                         "proposal-durable-deleted", "rejected-deleted-audit-only", "audit-only"),
                     ("unavailable", "candidate-evidence-unavailable", "evidence-application-unavailable",
                         "candidate-passage-unavailable", "context-durable-unavailable", "transcript-durable-unavailable",
                         "proposal-durable-unavailable", "abstained-unavailable", "retained-response"),
                 })
        {
            CandidateAnalysisEntryContract terminalHostCandidate = candidateAnalysis.Pipeline.Analysis.Candidates.Single(item =>
                item.SupportingEvidenceIds.Contains(new(evidenceCase.EvidenceId)));
            CandidateHypothesisContract terminalHostHypothesis = candidateAnalysis.Pipeline.Analysis.Hypotheses.Single(item =>
                item.CandidateId == terminalHostCandidate.CandidateId);
            CandidateDecisionContract terminalHostDecision = candidateAnalysis.Pipeline.Analysis.Decisions.Single(item =>
                item.DecisionId == terminalHostCandidate.DecisionId);
            CandidateEvidenceInput terminalEvidence = durableEvidence with
            {
                EvidenceId = evidenceCase.EvidenceId,
                EvidenceApplicationLinkId = evidenceCase.EvidenceApplicationId,
                PassageId = evidenceCase.PassageId,
                Availability = evidenceCase.Availability,
            };
            CandidateInvestigationContextInput terminalEvidenceContext = durableContext with
            {
                ContextId = evidenceCase.ContextId,
                CandidateId = terminalHostCandidate.CandidateId.Value,
                HypothesisId = terminalHostHypothesis.HypothesisId.Value,
                Hypothesis = terminalHostHypothesis.ProposedExplanation,
                ParticipantIds = terminalHostDecision.Participants.Select(item => item.ParticipantId.Value).ToArray(),
                ParticipantRoles = terminalHostDecision.Participants.Select(item => item.Role).ToArray(),
                CausalPathIds = terminalHostDecision.Path.Select(item => item.Value).ToArray(),
                DependencyClosureId = terminalHostDecision.DependencyClosureId.Value,
                Evidence = [terminalEvidence],
            };
            CandidateInvestigationExecutionInput terminalEvidenceInput = input with
            {
                Contexts = [terminalEvidenceContext, fixtureInput.Contexts[1]],
            };
            CandidateInvestigationRetainedTranscript terminalEvidenceTranscript = transcript with
            {
                TranscriptId = evidenceCase.TranscriptId,
                ContextId = evidenceCase.ContextId,
                Proposals = [transcript.Proposals[0] with
                {
                    ProposalId = evidenceCase.ProposalId,
                    CandidateId = terminalHostCandidate.CandidateId.Value,
                    HypothesisId = terminalHostHypothesis.HypothesisId.Value,
                    Hypothesis = terminalHostHypothesis.ProposedExplanation,
                    SupportingEvidenceIds = [evidenceCase.EvidenceId],
                }],
            };
            CandidateInvestigationExecutionInput falseAvailabilityInput = terminalEvidenceInput with
            {
                Contexts = [terminalEvidenceContext with
                {
                    Evidence = [terminalEvidence with { Availability = "available" }],
                }, fixtureInput.Contexts[1]],
            };
            Assert.ThrowsExactly<InvalidDataException>(() => coordinator.AdmitRetainedTranscript(
                falseAvailabilityInput, terminalEvidenceTranscript, "candidate-authorization", "candidate-attempt",
                "candidate-request", "candidate-fence", candidateAnalysisNow.AddSeconds(1)));

            CandidateInvestigationAdmissionPublication terminalEvidencePublication = coordinator.AdmitRetainedTranscript(
                terminalEvidenceInput, terminalEvidenceTranscript, "candidate-authorization", "candidate-attempt",
                "candidate-request", "candidate-fence", candidateAnalysisNow.AddSeconds(1));
            Assert.AreEqual(evidenceCase.ExpectedDisposition, terminalEvidencePublication.Scenario.Disposition);
            Assert.AreEqual(evidenceCase.ExpectedReplay, terminalEvidencePublication.Scenario.ReplayState);
            Assert.AreEqual(evidenceCase.ExpectedDisposition, coordinator.ReplayRetained(
                "run-restore", "candidate-operation", evidenceCase.ContextId).Disposition);
            Assert.IsEmpty(context.Store.ReadCandidateInvestigationAdmissionsForOperation(
                "run-restore", "candidate-operation").Where(item => item.State == "admitted"
                    && item.RootSubjectId == terminalHostCandidate.CandidateId.Value));
            StringAssert.Contains(System.Text.Encoding.UTF8.GetString(terminalEvidencePublication.JsonTransparency),
                evidenceCase.Availability);
        }
        foreach ((string State, string Suffix) terminalCase in new[]
                 {
                     ("not-used", "d07"),
                     ("unavailable", "d08"),
                 })
        {
            string terminalOperation = "candidate-terminal-" + terminalCase.Suffix;
            string terminalAuthorization = terminalOperation + "-authorization";
            string terminalJob = terminalOperation + "-job";
            string terminalCommand = terminalOperation + "-command";
            using (SqliteConnection terminalDatabase = new($"Data Source={context.Store.Paths.Database};Pooling=False"))
            {
                terminalDatabase.Open();
                using (SqliteCommand roots = terminalDatabase.CreateCommand())
                {
                    roots.CommandText =
                        """
                        INSERT INTO job_nodes VALUES($job,'run-restore',NULL,'provider','created',0,
                          '2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
                        INSERT INTO durable_commands VALUES($command,'provider','run-restore',1,'recorded','created',NULL,
                          '2026-08-10T00:00:00.0000000+00:00',NULL,NULL);
                        INSERT INTO provider_command_bindings VALUES($command,'analysis-run','run-restore',
                          '2026-08-10T00:00:00.0000000+00:00');
                        """;
                    roots.Parameters.AddWithValue("$job", terminalJob);
                    roots.Parameters.AddWithValue("$command", terminalCommand);
                    Assert.AreEqual(3, roots.ExecuteNonQuery());
                }
                CloneRow(terminalDatabase, "provider_operation_blocks", "operation_id='candidate-operation'", new Dictionary<string, object?>()
                {
                    ["operation_id"] = terminalOperation,
                    ["job_node_id"] = terminalJob,
                    ["command_id"] = terminalCommand,
                });
                CloneRow(terminalDatabase, "provider_operation_authorizations", "authorization_id='candidate-authorization'", new Dictionary<string, object?>()
                {
                    ["authorization_id"] = terminalAuthorization,
                    ["operation_id"] = terminalOperation,
                    ["job_node_id"] = terminalJob,
                    ["command_id"] = terminalCommand,
                });
            }
            CandidateInvestigationRetainedTranscript terminalTranscript = fixtureTranscripts.Single(item =>
                item.ResponseState == terminalCase.State) with
            { OperationId = terminalOperation };
            CandidateInvestigationContextInput terminalContext = fixtureInput.Contexts.Single(item =>
                item.ContextId == terminalTranscript.ContextId) with
            {
                CandidateId = hostCandidateId,
                HypothesisId = hostHypothesisId,
                Hypothesis = hostHypothesis.ProposedExplanation,
                ParticipantIds = hostDecision.Participants.Select(item => item.ParticipantId.Value).ToArray(),
                ParticipantRoles = hostDecision.Participants.Select(item => item.Role).ToArray(),
                CausalPathIds = hostDecision.Path.Select(item => item.Value).ToArray(),
                DependencyClosureId = hostDecision.DependencyClosureId.Value,
                Evidence = [durableEvidence],
            };
            CandidateInvestigationExecutionInput terminalInput = fixtureInput with
            {
                OperationId = terminalOperation,
                HostAuthorizationId = terminalAuthorization,
                OwnerId = "run-restore",
                AnalysisRunId = "run-restore",
                ApplicationScopeId = "application-restore",
                CostAttributionScopeId = "cost-restore",
                Contexts = [terminalContext, fixtureInput.Contexts[0]],
            };
            CandidateInvestigationAdmissionPublication terminalPublication = coordinator.AdmitRetainedTranscript(
                terminalInput, terminalTranscript, terminalAuthorization, "unused-attempt", "unused-request",
                "unused-fence", candidateAnalysisNow.AddSeconds(1));
            Assert.AreEqual(0, terminalPublication.Persistence.ProposalCount);
            Assert.IsEmpty(context.Store.ReadCandidateInvestigationAdmissionsForOperation(
                "run-restore", terminalOperation));
            Assert.AreEqual(terminalPublication.Scenario.Disposition,
                coordinator.ReplayRetained("run-restore", terminalOperation, terminalContext.ContextId).Disposition);
            StringAssert.Contains(System.Text.Encoding.UTF8.GetString(terminalPublication.JsonTransparency),
                terminalPublication.Scenario.Disposition);
        }
        (CandidateInvestigationExecutionInput validationFixture, CandidateInvestigationRetainedTranscript[] validationTranscripts) =
            CandidateAdmissionProviderReplayIntegrationTests.Load("S6-CANDIDATE-VAL-v3");
        CandidateInvestigationRetainedTranscript driftTranscript = validationTranscripts.Single(item => item.ResponseState == "drift") with
        {
            OperationId = "candidate-operation",
            ResponseRecordId = "candidate-response",
            ResponseFingerprint = retainedResponseFingerprint,
        };
        CandidateInvestigationContextInput driftContext = validationFixture.Contexts.Single(item =>
            item.ContextId == driftTranscript.ContextId) with
        {
            CandidateId = hostCandidateId,
            HypothesisId = hostHypothesisId,
            Hypothesis = hostHypothesis.ProposedExplanation,
            ParticipantIds = hostDecision.Participants.Select(item => item.ParticipantId.Value).ToArray(),
            ParticipantRoles = hostDecision.Participants.Select(item => item.Role).ToArray(),
            CausalPathIds = hostDecision.Path.Select(item => item.Value).ToArray(),
            DependencyClosureId = hostDecision.DependencyClosureId.Value,
            Evidence = [durableEvidence],
        };
        CandidateInvestigationExecutionInput driftInput = validationFixture with
        {
            OperationId = "candidate-operation",
            HostAuthorizationId = "candidate-authorization",
            OwnerId = "run-restore",
            AnalysisRunId = "run-restore",
            ApplicationScopeId = "application-restore",
            CostAttributionScopeId = "cost-restore",
            Contexts = [driftContext, validationFixture.Contexts[0]],
        };
        CandidateInvestigationAdmissionPublication driftPublication = coordinator.AdmitRetainedTranscript(
            driftInput, driftTranscript, "candidate-authorization", "candidate-attempt", "candidate-request",
            "candidate-fence", candidateAnalysisNow.AddSeconds(1));
        Assert.AreEqual("rejected-identity-drift", driftPublication.Scenario.Disposition);
        Assert.AreEqual("rejected-identity-drift", coordinator.ReplayRetained(
            "run-restore", "candidate-operation", driftContext.ContextId).Disposition);
        StringAssert.Contains(System.Text.Encoding.UTF8.GetString(publication.JsonTransparency), "source_acquisition_links");
        StringAssert.Contains(publication.HumanTransparency, "no finding, case, taxonomy");

        BackupArtifact noResponseBackup = context.Store.CreateBackup(
            "Wp7CandidateNoResponsePublication", candidateAnalysisNow.AddSeconds(2));
        string noResponseRoot = Path.Combine(Path.GetTempPath(), "Infinium-Wp7-NoResponse-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (StoragePaths target = new(noResponseRoot))
            {
                AuthoritativeStore.RestoreBackup(noResponseBackup, target);
            }
            using StoragePaths noResponsePaths = new(noResponseRoot);
            using AuthoritativeStore noResponseStore = new(noResponsePaths);
            ProviderTerminalPublicationArtifacts noResponseTerminal = new ProviderAccountingCoordinator(noResponseStore)
                .PublishCandidateNoResponseV2(
                    new("run-restore"), new("local-run-output-v1"), "candidate-local-output-v1"u8.ToArray(),
                    "candidate-local-cli-v1"u8.ToArray(), new("candidate-terminal-d07"), candidateAnalysisNow.AddSeconds(3));
            ProviderPublicationReferenceContract noResponseReference = noResponseTerminal.RunOutputV2.ProviderOperations.Single();
            Assert.AreEqual("not-used", noResponseReference.Availability);
            Assert.IsNull(noResponseReference.OperationId);
            Assert.IsNull(noResponseReference.AdmissionId);
            Assert.AreEqual("not-used", noResponseTerminal.CliSummaryV2.ProviderState);
        }
        finally
        {
            if (Directory.Exists(noResponseRoot))
            {
                DeleteDirectoryWithRetry(noResponseRoot);
            }
        }

        ProviderTerminalPublicationArtifacts terminal = accounting.PublishTerminalV2(
            new("run-restore"), new("local-run-output-v1"), "candidate-local-output-v1"u8.ToArray(),
            "candidate-local-cli-v1"u8.ToArray(), new("candidate-operation"), candidateAnalysisNow.AddSeconds(4));
        Assert.AreEqual(ProviderOperationKind.CandidateInvestigation,
            terminal.RunOutputV2.ProviderOperations.Single().OperationKind);
        Assert.AreEqual("admission-proposal-d01",
            terminal.RunOutputV2.ProviderOperations.Single().AdmissionId?.Value);

        BackupArtifact backup = context.Store.CreateBackup("Wp7CandidateInvestigation", candidateAnalysisNow.AddSeconds(5));
        string restoredRoot = Path.Combine(Path.GetTempPath(), "Infinium-Wp7-Restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (StoragePaths target = new(restoredRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, target);
            }
            using StoragePaths restoredPaths = new(restoredRoot);
            using AuthoritativeStore restored = new(restoredPaths);
            Assert.AreEqual(2, restored.ReadCandidateInvestigationAdmissions("run-restore", hostCandidateId).Count);
            Assert.AreEqual("proposal-d01", restored.ReadCandidateInvestigation(
                "run-restore", hostCandidateId, "admission-proposal-d01").HypothesisProposals.Single().ProposalId.Value);
            Assert.AreEqual("accepted", restored.ReadCandidateInvestigationOutcome(
                "run-restore", "candidate-operation", fixtureContext.ContextId).Disposition);
            Assert.AreEqual("not-used", restored.ReadCandidateInvestigationOutcome(
                "run-restore", "candidate-terminal-d07", "context-d07").Disposition);
            Assert.AreEqual("unavailable-provider", restored.ReadCandidateInvestigationOutcome(
                "run-restore", "candidate-terminal-d08", "context-d08").Disposition);
            Assert.AreEqual("rejected-identity-drift", restored.ReadCandidateInvestigationOutcome(
                "run-restore", "candidate-operation", driftContext.ContextId).Disposition);
            Assert.AreEqual("rejected-deleted-audit-only", restored.ReadCandidateInvestigationOutcome(
                "run-restore", "candidate-operation", "context-durable-deleted").Disposition);
            Assert.AreEqual("abstained-unavailable", restored.ReadCandidateInvestigationOutcome(
                "run-restore", "candidate-operation", "context-durable-unavailable").Disposition);
            Assert.AreEqual("rejected-deleted-audit-only", new DurableCandidateInvestigationCoordinator(restored)
                .ReplayRetained("run-restore", "candidate-operation", "context-durable-deleted").Disposition);
            Assert.AreEqual("abstained-unavailable", new DurableCandidateInvestigationCoordinator(restored)
                .ReplayRetained("run-restore", "candidate-operation", "context-durable-unavailable").Disposition);
            Assert.AreEqual("not-used", new DurableCandidateInvestigationCoordinator(restored).ReplayRetained(
                "run-restore", "candidate-terminal-d07", "context-d07").Disposition);
            _ = restored.RebuildProviderBudgetProjections(candidateAnalysisNow.AddSeconds(6));
        }
        finally
        {
            if (Directory.Exists(restoredRoot))
            {
                DeleteDirectoryWithRetry(restoredRoot);
            }
        }
    }

    private static CandidateEvidenceProvenanceBinding CandidateBinding(CandidateEvidenceInput evidence) => new(
        evidence.EvidenceId, evidence.EvidenceApplicationLinkId, evidence.SourceAcquisitionId,
        evidence.SourceAdmissionId, evidence.SourceApplicationLinkId, evidence.SourceRevisionId,
        evidence.PassageId, evidence.Relationship, evidence.Availability, evidence.ContentSha256);

    private static CandidateInvestigationPersistenceRequest CandidatePersistenceRequest(
        CandidateInvestigationScenarioResult scenario,
        CandidateInvestigationExecutionInput input,
        CandidateInvestigationRetainedTranscript transcript,
        IReadOnlyList<CandidateEvidenceProvenanceBinding> evidenceBindings,
        string outcomeId) => new(
            scenario.Investigation, outcomeId, scenario.ContextId, scenario.HypothesisId, transcript.TranscriptId,
            transcript.ResponseFingerprint, transcript.ResponseState, scenario.Disposition, scenario.ReplayState,
            input.ApplicationScopeId, input.CostAttributionScopeId, evidenceBindings,
            JsonSerializer.SerializeToUtf8Bytes(input, SourceClaimContextMinimizer.JsonOptions),
            JsonSerializer.SerializeToUtf8Bytes(transcript, SourceClaimContextMinimizer.JsonOptions),
            JsonSerializer.SerializeToUtf8Bytes(scenario.Investigation),
            input.HostAuthorizationId, transcript.ModelUsed ? transcript.ResponseRecordId : null,
            transcript.ModelUsed ? "candidate-attempt" : null, transcript.ModelUsed ? "candidate-request" : null,
            transcript.ModelUsed ? "candidate-fence" : null, BaseTime.AddSeconds(20));

    [TestMethod]
    [TestCategory("Integration")]
    public void OversizedNullRawPayloadQueriesAndReplaysAsTypedOversizedReceipt()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt gate = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        context.Store.RecordProviderTransportStart("operation-restore", "attempt-settlement", "request-settlement",
            gate.DispatchFenceId, ambiguous: false, BaseTime.AddSeconds(6));
        ProviderQuantityContract absent = new(ProviderAvailabilityState.Unavailable, null);
        ProviderUsageContract partial = new(ProviderAvailabilityState.Available,
            new(ProviderAvailabilityState.Available, 1), absent, absent, absent, absent, absent, absent, absent, absent,
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, UsageReceiptState.Partial);
        OpenAiResponsesResult oversized = new(
            ProviderResponseState.Oversized, true, false, 200, null, null, "client-request-settlement", "provider-request",
            null, null, null, null, null, partial, [], false, "oversized", false, 0)
        { RequestedOutputSchemaBytes = ProviderAdapterTestData.OutputSchemaBytes };
        byte[] envelope = OpenAiStagedResponseEnvelope.Create(oversized);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(envelope, out byte[] raw, out byte[] headers));
        Assert.IsEmpty(raw);
        ProviderSimulationPersistenceReceipt persisted = context.Store.PersistProviderSimulation(new(
            "oversized:response", "oversized:usage", "oversized:receipt", "oversized:finalization",
            "authorization-settlement", "operation-restore", "reservation-settlement", "attempt-settlement",
            "request-settlement", gate.DispatchFenceId, ProviderResponseState.Oversized, 200, null, null,
            null, null, null, partial, [], null, BaseTime.AddSeconds(7), headers,
            ProviderRequestId: "provider-request", Admitted: false));
        _ = context.Store.SettleProviderBudget(new("oversized:settlement", "reservation-settlement",
            persisted.SettlementKind, null, null, BaseTime.AddSeconds(8)));
        ProviderAccountingCoordinator coordinator = new(context.Store);
        ProviderOperationSummaryProjection projection = coordinator.QueryOperation(new(
            new("operation-restore"), true, true, true));
        Assert.AreEqual("retained-response", projection.ReplayState);
        OpenAiResponsesResult replay = coordinator.Replay(new(new("operation-restore"), new("oversized:response"), false));
        Assert.AreEqual(ProviderResponseState.Oversized, replay.State);
        Assert.IsNull(replay.RawResponseBytes);
        Assert.IsFalse(replay.NetworkUsed);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderAuthoredCancelledStatusIsRetainedAndReplayedUnlikeLocalCancellation()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt gate = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        context.Store.RecordProviderTransportStart("operation-restore", "attempt-settlement", "request-settlement",
            gate.DispatchFenceId, ambiguous: false, BaseTime.AddSeconds(6));
        byte[] raw = "{\"id\":\"provider-cancelled\",\"status\":\"cancelled\",\"model\":\"gpt-5.6-sol\",\"service_tier\":\"default\"}"u8.ToArray();
        OpenAiResponsesResult parsed = OpenAiResponsesResponseCodec.Parse(
            raw, 200, "client-request-settlement", "provider-request", [], ProviderAdapterTestData.OutputSchemaBytes);
        Assert.AreEqual(ProviderResponseState.Cancelled, parsed.State);
        ProviderSimulationPersistenceReceipt persisted = context.Store.PersistProviderSimulation(new(
            "cancelled:response", "cancelled:usage", "cancelled:receipt", "cancelled:finalization",
            "authorization-settlement", "operation-restore", "reservation-settlement", "attempt-settlement",
            "request-settlement", gate.DispatchFenceId, parsed.State, 200, parsed.ReturnedModel,
            parsed.ReturnedServiceTier, parsed.ErrorCode, null, null, parsed.Usage, [], raw,
            BaseTime.AddSeconds(7), ProviderResponseId: parsed.ProviderResponseId,
            ProviderRequestId: parsed.ProviderRequestId, Admitted: false));
        _ = context.Store.SettleProviderBudget(new("cancelled:settlement", "reservation-settlement",
            persisted.SettlementKind, null, null, BaseTime.AddSeconds(8)));
        ProviderAccountingCoordinator coordinator = new(context.Store);
        OpenAiResponsesResult replay = coordinator.Replay(new(new("operation-restore"), new("cancelled:response"), false));
        Assert.AreEqual(ProviderResponseState.Cancelled, replay.State);
        CollectionAssert.AreEqual(raw, replay.RawResponseBytes!);
        Assert.IsFalse(replay.NetworkUsed);
        Assert.AreEqual(0, replay.SendCount);

        using CancellationTokenSource locallyCancelled = new();
        locallyCancelled.Cancel();
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(
            new("http://127.0.0.1:1/v1/responses"));
        Assert.ThrowsAsync<OperationCanceledException>(() => adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), "sk-local-cancel"u8.ToArray(), ProviderAdapterTestData.Limits(),
            "local-cancel", locallyCancelled.Token)).GetAwaiter().GetResult();
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
        private BudgetContext(string root, AuthoritativeStore store, CoordinatorAuthority authority,
            ProviderBudgetVectorContract vector, IReadOnlyList<ProviderBudgetScopeContract> scopes)
        {
            Root = root;
            Store = store;
            Authority = authority;
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
        public CoordinatorAuthority Authority { get; }
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
                return new(root, store, authority, vector, scopes);
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

    private static void CloneRow(
        SqliteConnection connection, string table, string where, IReadOnlyDictionary<string, object?> replacements)
    {
        Assert.AreEqual(1, CloneRows(connection, table, where, replacements));
    }

    private static int CloneRows(
        SqliteConnection connection, string table, string where, IReadOnlyDictionary<string, object?> replacements)
    {
        List<string> columns = [];
        using (SqliteCommand schema = connection.CreateCommand())
        {
            schema.CommandText = $"PRAGMA table_info(\"{table}\");";
            using SqliteDataReader reader = schema.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
        }
        using SqliteCommand clone = connection.CreateCommand();
        string[] expressions = columns.Select((column, index) =>
        {
            if (!replacements.TryGetValue(column, out object? value))
            {
                return $"\"{column}\"";
            }
            string parameter = "$replacement" + index;
            clone.Parameters.AddWithValue(parameter, value ?? DBNull.Value);
            return parameter;
        }).ToArray();
        clone.CommandText = $"INSERT INTO \"{table}\" ({string.Join(',', columns.Select(x => $"\"{x}\""))}) "
            + $"SELECT {string.Join(',', expressions)} FROM \"{table}\" WHERE {where};";
        return clone.ExecuteNonQuery();
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
          price_snapshot_id,settings_fingerprint,'openai-responses-o200k-byte-envelope','v2','proved',coordinator_fencing_epoch,
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
