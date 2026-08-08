using System.Runtime.Versioning;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class SupportedExecutableAdmissionEvaluationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void PrivateExactSkyrimRuntimeIsAdmitted()
    {
        string? path = Environment.GetEnvironmentVariable("INFINIUM_M1_TARGET_1170_PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            Assert.Inconclusive(
                "Set INFINIUM_M1_TARGET_1170_PATH to the evaluator-private exact executable.");
        }

        ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
            path,
            SupportedRuntime());

        Assert.AreEqual(AdmissionState.Accepted, admission.State);
        Assert.IsNotNull(admission.ObservedIdentity);
        Assert.AreEqual(
            SupportedExecutableManifests.SupportedSkyrimSha256,
            admission.ObservedIdentity.Sha256);
        Assert.AreEqual((ushort)0x8664, admission.ObservedIdentity.PeMachine);
        Assert.AreEqual((ushort)0x020b, admission.ObservedIdentity.PeOptionalHeaderMagic);
        Assert.AreEqual((ushort)2, admission.ObservedIdentity.PeSubsystem);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("EvaluationCase", "EVAL-0051")]
    public void PrivateExactMo2BuildIsAdmitted()
    {
        string? path = Environment.GetEnvironmentVariable("INFINIUM_M1_MO2_252_PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            Assert.Inconclusive(
                "Set INFINIUM_M1_MO2_252_PATH to the evaluator-private exact executable.");
        }

        ExecutableAdmission admission = new SupportedExecutableManifests().AdmitMo2(path);

        Assert.AreEqual(AdmissionState.Accepted, admission.State);
        Assert.IsNotNull(admission.ObservedIdentity);
        Assert.AreEqual(
            SupportedExecutableManifests.SupportedMo2Sha256,
            admission.ObservedIdentity.Sha256);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("EvaluationCase", "EVAL-0051")]
    public void PrivateExactSkyrimGamePluginIsAdmitted()
    {
        string mo2 = GetPrivateVariable("INFINIUM_M1_MO2_252_PATH");
        string path = Path.Combine(
            Path.GetDirectoryName(mo2)!,
            "plugins",
            "game_skyrimse.dll");

        ExecutableAdmission admission =
            new SupportedExecutableManifests().AdmitSkyrimGamePlugin(path);

        Assert.AreEqual(AdmissionState.Accepted, admission.State);
        Assert.IsNotNull(admission.ObservedIdentity);
        Assert.AreEqual(
            SupportedExecutableManifests.SupportedSkyrimGamePluginSha256,
            admission.ObservedIdentity.Sha256);
        Assert.AreEqual(440_320, admission.ObservedIdentity.ByteLength);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("EvaluationCase", "EVAL-0051")]
    public void PrivateConfiguredInstanceCapturesAnExplicitProfileWithoutLaunch()
    {
        string instanceRoot = GetPrivateVariable("INFINIUM_M1_MO2_INSTANCE_ROOT");
        string baseDirectory = GetPrivateVariable("INFINIUM_M1_MO2_BASE_DIRECTORY");
        string profile = GetPrivateVariable("INFINIUM_M1_MO2_PROFILE");
        string mo2 = GetPrivateVariable("INFINIUM_M1_MO2_252_PATH");
        string skyrim = GetPrivateVariable("INFINIUM_M1_TARGET_1170_PATH");

        Mo2SnapshotCaptureRequest request = new(
            mo2,
            instanceRoot,
            Path.Combine(instanceRoot, "ModOrganizer.ini"),
            Path.Combine(baseDirectory, "profiles"),
            Path.Combine(baseDirectory, "mods"),
            Path.Combine(baseDirectory, "overwrite"),
            Path.Combine(Path.GetDirectoryName(skyrim)!, "Data"),
            skyrim,
            profile,
            SupportedRuntime(),
            [],
            []);

        Mo2SnapshotCaptureResult result = new Mo2SnapshotCapture().Capture(request);

        TestContext.WriteLine($"capture_state={result.State}");
        TestContext.WriteLine($"gap_count={result.Gaps.Count}");
        foreach (IGrouping<string, SnapshotGap> group in result.Gaps
                     .GroupBy(gap => gap.Code, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            TestContext.WriteLine($"gap_summary={group.Key}|{group.Count()}");
        }

        foreach (SnapshotGap gap in result.Gaps.Take(25))
        {
            TestContext.WriteLine($"gap_sample={gap.Code}|{gap.Population}|{gap.Reason}");
        }

        Assert.IsNotNull(
            result.Snapshot,
            string.Join(
                Environment.NewLine,
                result.Gaps.Select(gap => $"{gap.Code}: {gap.Reason}")));
        Assert.AreNotEqual(SnapshotCaptureState.Failed, result.State);
        Assert.AreNotEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.AreEqual(profile, Path.GetFileName(result.Snapshot.ProfileRoot));
        Assert.IsFalse(result.Snapshot.Mo2OrUsvfsLaunched);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("EvaluationCase", "EVAL-0046")]
    [SupportedOSPlatform("windows")]
    public void PrivateExactCapturePreservesAllProtectedRootsAndLaunchState()
    {
        Mo2SnapshotCaptureRequest request = PrivateCaptureRequest();
        string instanceBase = GetPrivateVariable("INFINIUM_M1_MO2_BASE_DIRECTORY");
        string gameRoot = Path.GetDirectoryName(
            GetPrivateVariable("INFINIUM_M1_TARGET_1170_PATH"))!;
        IReadOnlyList<string> protectedRoots =
        [
            Path.GetDirectoryName(request.Mo2ExecutablePath)!,
            instanceBase,
            gameRoot,
        ];
        IReadOnlyDictionary<string, string> before =
            WindowsProtectedRootCanary.Capture(protectedRoots);
        IReadOnlyList<string> processesBefore =
            WindowsProtectedRootCanary.CaptureTargetProcesses();

        Mo2SnapshotCaptureResult result = new Mo2SnapshotCapture().Capture(request);

        IReadOnlyDictionary<string, string> after =
            WindowsProtectedRootCanary.Capture(protectedRoots);
        IReadOnlyList<string> processesAfter =
            WindowsProtectedRootCanary.CaptureTargetProcesses();
        IReadOnlyList<string> releasedHandles =
            WindowsProtectedRootCanary.ObserveReleasedRootHandles(protectedRoots);

        CollectionAssert.AreEqual(before.ToArray(), after.ToArray());
        CollectionAssert.AreEqual(
            processesBefore.ToArray(),
            processesAfter.ToArray());
        Assert.IsNotNull(result.Snapshot);
        Assert.AreNotEqual(SnapshotCaptureState.Failed, result.State);
        Assert.AreNotEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.IsFalse(result.Snapshot.Mo2OrUsvfsLaunched);
        Assert.AreEqual(protectedRoots.Count, releasedHandles.Count);
        foreach (string evidence in releasedHandles)
        {
            TestContext.WriteLine($"released_handle={evidence}");
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void MissingRuntimeIsIndeterminateAndNeverBestEffortAdmitted()
    {
        string missing = Path.Combine(
            Path.GetTempPath(),
            $"Infinium-missing-runtime-{Guid.NewGuid():N}.exe");

        ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
            missing,
            SupportedRuntime());

        Assert.AreEqual(AdmissionState.Indeterminate, admission.State);
        Assert.AreNotEqual(AdmissionState.Accepted, admission.State);
        Assert.IsTrue(admission.Reasons.Count > 0);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void KnownUnsupportedGogChannelIsRejectedBeforeAdmission()
    {
        string path = Environment.GetEnvironmentVariable("INFINIUM_M1_TARGET_1170_PATH")
            ?? Path.Combine(Path.GetTempPath(), "not-read-for-unsupported-context.exe");
        RuntimeTargetContext unsupported = new("windows-x64", "gog", "489830");

        ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
            path,
            unsupported);

        Assert.AreEqual(AdmissionState.Unsupported, admission.State);
        Assert.IsNull(admission.ObservedIdentity);
        CollectionAssert.Contains(
            admission.Reasons.ToArray(),
            "runtime platform, distribution channel, or application ID is unsupported");
    }

    [TestMethod]
    [DataRow("windows-x86", "steam", "489830")]
    [DataRow("linux-x64", "steam", "489830")]
    [DataRow("windows-x64", "steam", "not-489830")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void UnsupportedPlatformArchitectureOrApplicationIsRejectedBeforeAdmission(
        string platform,
        string channel,
        string applicationId)
    {
        string unreadPath = Path.Combine(
            Path.GetTempPath(),
            $"must-not-be-read-{Guid.NewGuid():N}.exe");
        RuntimeTargetContext unsupported = new(platform, channel, applicationId);

        ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
            unreadPath,
            unsupported);

        Assert.AreEqual(AdmissionState.Unsupported, admission.State);
        Assert.IsNull(admission.ObservedIdentity);
        Assert.IsFalse(File.Exists(unreadPath));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void ProjectAuthoredMalformedExecutableIsIndeterminate()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"Infinium-EVAL-0054-malformed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "SkyrimSE.exe");
        try
        {
            File.WriteAllBytes(path, "not-a-portable-executable"u8.ToArray());

            ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
                path,
                SupportedRuntime());

            Assert.AreEqual(AdmissionState.Indeterminate, admission.State);
            CollectionAssert.Contains(
                admission.Reasons.ToArray(),
                "executable PE headers are malformed or truncated");
            Assert.IsNotNull(admission.ObservedIdentity);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void SameVersionOneByteMutationIsUnrecognized()
    {
        string? source = Environment.GetEnvironmentVariable("INFINIUM_M1_TARGET_1170_PATH");
        if (string.IsNullOrWhiteSpace(source))
        {
            Assert.Inconclusive(
                "Set INFINIUM_M1_TARGET_1170_PATH to derive the private one-byte mutation.");
        }

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"Infinium-EVAL-0054-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string mutated = Path.Combine(directory, "SkyrimSE.exe");
        try
        {
            File.Copy(source, mutated);
            using (FileStream stream = new(
                       mutated,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                stream.Position = stream.Length - 1;
                int original = stream.ReadByte();
                stream.Position = stream.Length - 1;
                stream.WriteByte((byte)(original ^ 0x01));
            }

            ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
                mutated,
                SupportedRuntime());

            Assert.AreEqual(AdmissionState.Unrecognized, admission.State);
            Assert.IsNotNull(admission.ObservedIdentity);
            Assert.AreEqual("1.6.1170.0", admission.ObservedIdentity.ProductVersion);
            Assert.AreNotEqual(
                SupportedExecutableManifests.SupportedSkyrimSha256,
                admission.ObservedIdentity.Sha256);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void UnreadableRuntimeIsIndeterminateWithAnExactReason()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"Infinium-EVAL-0054-unreadable-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "SkyrimSE.exe");
        try
        {
            File.WriteAllBytes(path, "locked"u8.ToArray());
            using FileStream exclusive = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
                path,
                SupportedRuntime());

            Assert.AreEqual(AdmissionState.Indeterminate, admission.State);
            Assert.IsNull(admission.ObservedIdentity);
            Assert.AreEqual(
                "executable could not be read: IOException",
                admission.Reasons.Single());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void ExactMetadataWithConflictingByteLengthIsInconsistent()
    {
        string source = GetPrivateVariable("INFINIUM_M1_TARGET_1170_PATH");
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"Infinium-EVAL-0054-inconsistent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "SkyrimSE.exe");
        try
        {
            File.Copy(source, path);
            using (FileStream stream = new(
                       path,
                       FileMode.Open,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.SetLength(stream.Length - 1);
            }

            ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
                path,
                SupportedRuntime());

            Assert.AreEqual(AdmissionState.Inconsistent, admission.State);
            Assert.IsNotNull(admission.ObservedIdentity);
            Assert.AreEqual("1.6.1170.0", admission.ObservedIdentity.ProductVersion);
            CollectionAssert.Contains(
                admission.Reasons.ToArray(),
                "unexpected executable byte length");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void ExecutableIdentityCaptureRaceInvalidatesWithoutSnapshotOutput()
    {
        Mo2SnapshotCaptureRequest request = PrivateCaptureRequest();
        Mo2SnapshotCapture capture = new(
            new ChangingMo2AdmissionService(new SupportedExecutableManifests()),
            new WindowsMo2ProcessProbe(),
            SupportedExecutableManifests.QualifiedMapperSha256s,
            betweenStructuralCaptures: null);

        Mo2SnapshotCaptureResult result = capture.Capture(request);

        Assert.AreEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.IsTrue(result.Gaps.Any(gap => gap.Code == "changed-during-capture"));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("EvaluationCase", "EVAL-0054")]
    public void UnsupportedManagerFailsBeforePathResolutionWithoutSnapshotOutput()
    {
        string missingRoot = Path.Combine(
            Path.GetTempPath(),
            $"must-not-be-resolved-{Guid.NewGuid():N}");
        Mo2SnapshotCaptureRequest request = new(
            Path.Combine(missingRoot, "ModOrganizer.exe"),
            missingRoot,
            Path.Combine(missingRoot, "ModOrganizer.ini"),
            Path.Combine(missingRoot, "profiles"),
            Path.Combine(missingRoot, "mods"),
            Path.Combine(missingRoot, "overwrite"),
            Path.Combine(missingRoot, "Data"),
            Path.Combine(missingRoot, "SkyrimSE.exe"),
            "not-read",
            SupportedRuntime(),
            [],
            [],
            ManagerId: "vortex");

        Mo2SnapshotCaptureResult result = new Mo2SnapshotCapture().Capture(request);

        Assert.AreEqual(SnapshotCaptureState.Failed, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.AreEqual("unsupported-manager", result.Gaps.Single().Code);
        Assert.IsFalse(Directory.Exists(missingRoot));
    }

    private static RuntimeTargetContext SupportedRuntime()
    {
        return new RuntimeTargetContext("windows-x64", "steam", "489830");
    }

    private static string GetPrivateVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Inconclusive($"Set evaluator-private variable {name}.");
        }

        return value!;
    }

    private static Mo2SnapshotCaptureRequest PrivateCaptureRequest()
    {
        string instanceRoot = GetPrivateVariable("INFINIUM_M1_MO2_INSTANCE_ROOT");
        string baseDirectory = GetPrivateVariable("INFINIUM_M1_MO2_BASE_DIRECTORY");
        string mo2 = GetPrivateVariable("INFINIUM_M1_MO2_252_PATH");
        string skyrim = GetPrivateVariable("INFINIUM_M1_TARGET_1170_PATH");
        return new Mo2SnapshotCaptureRequest(
            mo2,
            instanceRoot,
            Path.Combine(instanceRoot, "ModOrganizer.ini"),
            Path.Combine(baseDirectory, "profiles"),
            Path.Combine(baseDirectory, "mods"),
            Path.Combine(baseDirectory, "overwrite"),
            Path.Combine(Path.GetDirectoryName(skyrim)!, "Data"),
            skyrim,
            GetPrivateVariable("INFINIUM_M1_MO2_PROFILE"),
            SupportedRuntime(),
            [],
            []);
    }

    private sealed class ChangingMo2AdmissionService(
        IExecutableAdmissionService inner) : IExecutableAdmissionService
    {
        private int mo2Calls;

        public ExecutableAdmission AdmitMo2(string path)
        {
            ExecutableAdmission admission = inner.AdmitMo2(path);
            mo2Calls++;
            if (mo2Calls != 2 || admission.ObservedIdentity is null)
            {
                return admission;
            }

            return admission with
            {
                ObservedIdentity = admission.ObservedIdentity with
                {
                    Sha256 = new string('0', 64),
                },
            };
        }

        public ExecutableAdmission AdmitSkyrimGamePlugin(string path)
        {
            return inner.AdmitSkyrimGamePlugin(path);
        }

        public ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context)
        {
            return inner.AdmitSkyrim(path, context);
        }
    }
}
