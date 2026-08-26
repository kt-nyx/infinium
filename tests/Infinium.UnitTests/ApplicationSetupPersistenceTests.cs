using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ApplicationSetupPersistenceTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void SetupMutationsAreRevisionedReplaySafeAndRestartDurable()
    {
        using TemporaryProductRoot temporary = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SetupMutationRequest first = new(
            "request-configuration-create",
            "create-configuration",
            "saved-scan-configuration",
            "configuration-local",
            0,
            "active",
            "{\"Name\":\"Local\"}",
            now);

        using (AuthoritativeStore store = temporary.Open())
        {
            SetupMutationReceipt accepted = store.ApplySetupMutation(first);
            Assert.AreEqual(1L, accepted.AcceptedRevision);
            Assert.IsFalse(accepted.Replayed);

            SetupMutationReceipt replayed = store.ApplySetupMutation(first);
            Assert.IsTrue(replayed.Replayed);
            Assert.AreEqual(accepted.RequestFingerprint, replayed.RequestFingerprint);
            Assert.ThrowsExactly<InvalidOperationException>(() => store.ApplySetupMutation(
                first with { PayloadJson = "{\"Name\":\"Rebound\"}" }));
            Assert.ThrowsExactly<SetupRevisionConflictException>(() => store.ApplySetupMutation(
                first with
                {
                    RequestId = "request-configuration-stale",
                    PayloadJson = "{\"Name\":\"Stale\"}",
                }));
        }

        using AuthoritativeStore reopened = temporary.Open();
        SetupObjectRecord retained = reopened.FindSetupObject(
            "saved-scan-configuration",
            "configuration-local")!;
        Assert.AreEqual(1L, retained.Revision);
        Assert.AreEqual("{\"Name\":\"Local\"}", retained.PayloadJson);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
    public void PreparedRunAndUserGestureBindingsAreImmutableAndReplaySafe()
    {
        using TemporaryProductRoot temporary = new();
        using AuthoritativeStore store = temporary.Open();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RunBinding binding = new("snapshot-a", "context-a", "effective-a", "manifest-a");
        PreparedRunRecord prepared = new(
            "preparation-a",
            "request-preparation-a",
            1,
            "profile-a",
            2,
            "configuration-a",
            3,
            "effective-a",
            "{\"LocalOnly\":true}",
            binding,
            "{\"AuthorityStatement\":\"local only\"}",
            now);
        Assert.AreEqual(prepared, store.CreatePreparedRun(prepared));
        Assert.AreEqual(prepared, store.CreatePreparedRun(prepared with { PreparedAt = now.AddMinutes(1) }));
        PreparedRunRecord secondPreparation = prepared with
        {
            PreparationId = "preparation-b",
            RequestId = "request-preparation-b",
            Binding = binding with { InstallationSnapshotId = "snapshot-b" },
            PreparedAt = now.AddSeconds(1),
        };
        Assert.AreEqual(secondPreparation, store.CreatePreparedRun(secondPreparation));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreatePreparedRun(
            prepared with
            {
                PreparationId = "preparation-rebound",
                EffectiveConfigurationJson = "{\"LocalOnly\":false}",
            }));

        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-setup-test",
            now,
            TimeSpan.FromMinutes(5));
        RunRecord accepted = store.CreateRun(
            "command-prepared-a",
            "run-prepared-a",
            binding,
            authority.FencingEpoch,
            now,
            "DesktopUserGesture",
            now.AddMinutes(1),
            startUserGestureId: "gesture-1234567890abcdef",
            startPreparationId: prepared.PreparationId);
        RunRecord replayed = store.CreateRun(
            "command-prepared-a",
            "run-rebound",
            binding,
            authority.FencingEpoch,
            now.AddSeconds(1),
            "DesktopUserGesture",
            now.AddMinutes(2),
            startUserGestureId: "gesture-1234567890abcdef",
            startPreparationId: prepared.PreparationId);
        Assert.AreEqual(accepted.RunId, replayed.RunId);

        DurableCommandRecord receipt = store.GetDurableCommand("command-prepared-a");
        Assert.AreEqual(prepared.PreparationId, receipt.StartPreparationId);
        Assert.AreEqual("gesture-1234567890abcdef", receipt.StartUserGestureId);
        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateRun(
            "command-prepared-a",
            "run-rebound",
            binding,
            authority.FencingEpoch,
            now.AddSeconds(2),
            "DesktopUserGesture",
            now.AddMinutes(2),
            startUserGestureId: "gesture-fedcba0987654321",
            startPreparationId: prepared.PreparationId));
    }

    private sealed class TemporaryProductRoot : IDisposable
    {
        public TemporaryProductRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), $"infinium-application-setup-{Guid.NewGuid():N}");
        }

        private string Root { get; }

        public AuthoritativeStore Open() => new(new StoragePaths(Root));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
