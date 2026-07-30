using Infinium.Application.Evaluation;
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
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Contract")]
    public void IndependentSlice3EvaluatorPackagePassesStrictFixtureContracts()
    {
        string package = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "docs",
                "evaluation",
                "fixtures",
                "independent-slice3-evaluator-20260729"));

        EvaluationHarnessFixturePackage loaded =
            FixturePackageReader.ReadForEvaluationHarness(package);

        Assert.AreEqual(
            new OpaqueId("SLICE3-INDEPENDENT-VAL-20260729"),
            loaded.FixtureId);
        Assert.AreEqual(FixturePartition.Validation, loaded.Partition);
    }

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
    public void ExactBytesWithUnsupportedChannelAreRejectedBeforeAdmission()
    {
        string path = Environment.GetEnvironmentVariable("INFINIUM_M1_TARGET_1170_PATH")
            ?? Path.Combine(Path.GetTempPath(), "not-read-for-unsupported-context.exe");
        RuntimeTargetContext unsupported = new("windows-x64", "other-channel", "489830");

        ExecutableAdmission admission = new SupportedExecutableManifests().AdmitSkyrim(
            path,
            unsupported);

        Assert.AreEqual(AdmissionState.Unsupported, admission.State);
        Assert.IsNull(admission.ObservedIdentity);
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
}
