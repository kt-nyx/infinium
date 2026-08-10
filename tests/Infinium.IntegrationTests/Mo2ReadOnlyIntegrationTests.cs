using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using Infinium.Mo2;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class Mo2ReadOnlyIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    [TestProperty("EvaluationCase", "EVAL-0046")]
    public void ExplicitCaptureIsReadOnlyAcrossEveryDeclaredProtectedRoot()
    {
        using IntegrationFixture fixture = new();
        IReadOnlyDictionary<string, string> before =
            FingerprintProtectedRoots(fixture.ProtectedRoots);
        IReadOnlyDictionary<string, string> sideEffectsBefore =
            FingerprintProtectedRoots(fixture.SideEffectRoots);
        ProcessEvidence processesBefore = CaptureProcessEvidence();
        Mo2SnapshotCapture capture = fixture.CreateCapture();
        IReadOnlyList<string> identitiesBefore;
        IReadOnlyList<string> identitiesAfter;
        Mo2SnapshotCaptureResult result;

        using (WindowsWriteAuthorityRegistry authority = new(fixture.ProtectedRoots))
        {
            identitiesBefore = authority.ProtectedRootIdentities;
            IReadOnlyDictionary<string, string> afterConstruction =
                FingerprintProtectedRoots(fixture.ProtectedRoots);
            CollectionAssert.AreEqual(before.ToArray(), afterConstruction.ToArray());

            result = capture.Capture(fixture.Request);
            identitiesAfter = authority.ProtectedRootIdentities;
        }

        IReadOnlyDictionary<string, string> afterCapture =
            FingerprintProtectedRoots(fixture.ProtectedRoots);
        IReadOnlyDictionary<string, string> sideEffectsAfter =
            FingerprintProtectedRoots(fixture.SideEffectRoots);
        ProcessEvidence processesAfter = CaptureProcessEvidence();
        string[] newDescendants = processesAfter.CurrentProcessDescendants
            .Except(processesBefore.CurrentProcessDescendants, StringComparer.Ordinal)
            .ToArray();
        string[] changedTargetProcesses = processesAfter.TargetProcesses
            .Except(processesBefore.TargetProcesses, StringComparer.Ordinal)
            .Concat(processesBefore.TargetProcesses.Except(
                processesAfter.TargetProcesses,
                StringComparer.Ordinal))
            .ToArray();

        CollectionAssert.AreEqual(before.ToArray(), afterCapture.ToArray());
        CollectionAssert.AreEqual(
            sideEffectsBefore.ToArray(),
            sideEffectsAfter.ToArray());
        CollectionAssert.AreEqual(
            identitiesBefore.ToArray(),
            identitiesAfter.ToArray());
        Assert.HasCount(0, newDescendants);
        Assert.HasCount(0, changedTargetProcesses);
        Assert.AreEqual(SnapshotCaptureState.Completed, result.State);
        Assert.IsNotNull(result.Snapshot);
        Assert.IsFalse(result.Snapshot.Mo2OrUsvfsLaunched);
        string reparseEvidence =
            WindowsProtectedRootCanary.DescribeReparseIdentity(fixture.ReparseCanary, directory: true);
        Assert.Contains("tag=A0000003", reparseEvidence);
        Assert.Contains(
            NormalizeCanaryPath(fixture.ReparseTarget),
            reparseEvidence,
            StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<string> releasedHandleEvidence =
            WindowsProtectedRootCanary.ObserveReleasedRootHandles(fixture.ProtectedRoots);

        TestContext.WriteLine(
            $"process_descendants_before={string.Join(',', processesBefore.CurrentProcessDescendants)}");
        TestContext.WriteLine(
            $"process_descendants_after={string.Join(',', processesAfter.CurrentProcessDescendants)}");
        TestContext.WriteLine(
            $"target_processes_before={string.Join(',', processesBefore.TargetProcesses)}");
        TestContext.WriteLine(
            $"target_processes_after={string.Join(',', processesAfter.TargetProcesses)}");
        TestContext.WriteLine(
            "launch_argument_environment_handle_evidence="
            + "not-applicable:no-new-descendant-process-observed");
        TestContext.WriteLine(
            $"side_effect_roots={string.Join(',', sideEffectsAfter.Keys)}");
        TestContext.WriteLine(
            $"released_handle_evidence={string.Join(',', releasedHandleEvidence)}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    [TestProperty("EvaluationCase", "EVAL-0046")]
    public void ReadOnlyCaptureIsTheOnlyPubliclyReachableAdapterOperation()
    {
        string[] operations = typeof(Mo2SnapshotCapture)
            .GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(Mo2SnapshotCapture.Capture) }, operations);
        Assert.IsFalse(operations.Any(name =>
            name.Contains("write", StringComparison.OrdinalIgnoreCase)
            || name.Contains("apply", StringComparison.OrdinalIgnoreCase)
            || name.Contains("set", StringComparison.OrdinalIgnoreCase)
            || name.Contains("sort", StringComparison.OrdinalIgnoreCase)
            || name.Contains("save", StringComparison.OrdinalIgnoreCase)
            || name.Contains("launch", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestProperty("Category", "Integration")]
    public void NoPassiveEventCreatesASnapshotOrStartsAProcess()
    {
        using IntegrationFixture fixture = new();
        int processCountBefore = System.Diagnostics.Process
            .GetProcessesByName("ModOrganizer")
            .Length;
        Mo2SnapshotCapture capture = fixture.CreateCapture();

        Assert.AreEqual(0, fixture.ProcessProbeCallCount);
        Assert.AreEqual(
            processCountBefore,
            System.Diagnostics.Process.GetProcessesByName("ModOrganizer").Length);

        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);

        Assert.AreEqual(2, fixture.ProcessProbeCallCount);
        Assert.IsNotNull(result.Snapshot);
        Assert.AreEqual(
            processCountBefore,
            System.Diagnostics.Process.GetProcessesByName("ModOrganizer").Length);
    }

    private static Dictionary<string, string> FingerprintProtectedRoots(
        IReadOnlyList<string> roots) => WindowsProtectedRootCanary.Capture(roots)
        .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static ProcessEvidence CaptureProcessEvidence()
    {
        using SafeFileHandle snapshot =
            CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot.IsInvalid)
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "The process-tree evidence snapshot could not be created.");
        }

        Dictionary<int, ProcessTreeEntry> entries = [];
        ProcessEntry32 native = new()
        {
            Size = checked((uint)Marshal.SizeOf<ProcessEntry32>()),
        };
        if (Process32FirstW(snapshot, ref native))
        {
            do
            {
                entries[checked((int)native.ProcessId)] = new ProcessTreeEntry(
                    checked((int)native.ProcessId),
                    checked((int)native.ParentProcessId),
                    native.ExecutableFile);
                native.Size = checked((uint)Marshal.SizeOf<ProcessEntry32>());
            }
            while (Process32NextW(snapshot, ref native));

            int error = Marshal.GetLastWin32Error();
            if (error != ErrorNoMoreFiles)
            {
                throw new System.ComponentModel.Win32Exception(
                    error,
                    "The process-tree evidence snapshot could not be completed.");
            }
        }

        HashSet<int> descendants = [Environment.ProcessId];
        bool added;
        do
        {
            added = false;
            foreach (ProcessTreeEntry entry in entries.Values)
            {
                if (descendants.Contains(entry.ParentProcessId)
                    && descendants.Add(entry.ProcessId))
                {
                    added = true;
                }
            }
        }
        while (added);

        string[] descendantEvidence = entries.Values
            .Where(entry => entry.ProcessId != Environment.ProcessId
                            && descendants.Contains(entry.ProcessId))
            .Select(ProcessIdentity)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> targetNames = new(
            [
                "modorganizer.exe",
                "skyrimse.exe",
                "usvfs_proxy_x86.exe",
                "usvfs_proxy_x64.exe",
                "usvfs_proxy.exe",
            ],
            StringComparer.OrdinalIgnoreCase);
        string[] targetEvidence = entries.Values
            .Where(entry => targetNames.Contains(entry.ExecutableFile))
            .Select(ProcessIdentity)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new ProcessEvidence(descendantEvidence, targetEvidence);
    }

    private static string ProcessIdentity(ProcessTreeEntry entry) =>
        FormattableString.Invariant(
            $"{entry.ProcessId}|{entry.ParentProcessId}|{entry.ExecutableFile}");

    private static string NormalizeCanaryPath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/');

    private sealed class IntegrationFixture : IDisposable
    {
        private readonly CountingProbe probe;

        internal IntegrationFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"Infinium-Snapshot-Integration-{Guid.NewGuid():N}");
            InstanceRoot = Directory.CreateDirectory(Path.Combine(Root, "instance")).FullName;
            string profiles = Directory.CreateDirectory(Path.Combine(InstanceRoot, "profiles")).FullName;
            string profile = Directory.CreateDirectory(Path.Combine(profiles, "Explicit")).FullName;
            string mods = Directory.CreateDirectory(Path.Combine(InstanceRoot, "mods")).FullName;
            string mod = Directory.CreateDirectory(Path.Combine(mods, "Provider")).FullName;
            string overwrite = Directory.CreateDirectory(Path.Combine(InstanceRoot, "overwrite")).FullName;
            GameRoot = Directory.CreateDirectory(Path.Combine(Root, "game")).FullName;
            string data = Directory.CreateDirectory(Path.Combine(GameRoot, "Data")).FullName;
            string generated = Directory.CreateDirectory(
                Path.Combine(Root, "generated-output")).FullName;
            ReparseTarget = Directory.CreateDirectory(
                Path.Combine(Root, "reparse-target")).FullName;
            File.WriteAllText(
                Path.Combine(ReparseTarget, "reparse-target-canary.bin"),
                "must-not-be-followed",
                Encoding.UTF8);
            ReparseCanary = Path.Combine(generated, "disposable-junction");
            CreateRequiredJunction(ReparseCanary, ReparseTarget);
            string alternateStreamHost = Path.Combine(generated, "canary-host.bin");
            File.WriteAllText(alternateStreamHost, "default-stream", Encoding.UTF8);
            File.WriteAllText(
                $"{alternateStreamHost}:infinium-protected-canary",
                "alternate-stream",
                Encoding.UTF8);
            string instanceIni = Path.Combine(InstanceRoot, "ModOrganizer.ini");
            string mo2Root = Directory.CreateDirectory(Path.Combine(Root, "mo2")).FullName;
            string mo2 = Path.Combine(mo2Root, "ModOrganizer.exe");
            string gamePluginDirectory =
                Directory.CreateDirectory(Path.Combine(mo2Root, "plugins")).FullName;
            File.WriteAllText(
                Path.Combine(gamePluginDirectory, "game_skyrimse.dll"),
                "synthetic game plugin",
                Encoding.UTF8);
            string skyrim = Path.Combine(GameRoot, "SkyrimSE.exe");

            File.WriteAllText(
                instanceIni,
                "[General]\n"
                + "selected_profile=Explicit\n"
                + "gameName=Skyrim Special Edition\n"
                + $"gamePath={GameRoot.Replace('\\', '/')}\n"
                + "[Settings]\n"
                + $"base_directory={InstanceRoot.Replace('\\', '/')}\n",
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(profile, "modlist.txt"), "+Provider\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(profile, "plugins.txt"), "*P.esp\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(profile, "loadorder.txt"), "P.esp\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(mod, "P.esp"), "plugin", Encoding.UTF8);
            File.WriteAllText(Path.Combine(data, "base.txt"), "base", Encoding.UTF8);
            File.WriteAllText(mo2, "synthetic", Encoding.UTF8);
            File.WriteAllText(skyrim, "synthetic", Encoding.UTF8);

            Request = new Mo2SnapshotCaptureRequest(
                mo2,
                InstanceRoot,
                instanceIni,
                profiles,
                mods,
                overwrite,
                data,
                skyrim,
                "Explicit",
                new RuntimeTargetContext("windows-x64", "steam", "489830"),
                [],
                []);
            ProtectedRoots =
            [
                mo2Root,
                InstanceRoot,
                profiles,
                profile,
                mods,
                overwrite,
                GameRoot,
                data,
                generated,
                ReparseTarget,
            ];
            string cacheRoot = Directory.CreateDirectory(
                Path.Combine(Root, "isolated-tool-cache")).FullName;
            string tempRoot = Directory.CreateDirectory(
                Path.Combine(Root, "isolated-product-temp")).FullName;
            File.WriteAllText(
                Path.Combine(cacheRoot, "cache-canary.bin"),
                "unchanged-cache",
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(tempRoot, "temp-canary.bin"),
                "unchanged-temp",
                Encoding.UTF8);
            SideEffectRoots = [cacheRoot, tempRoot];
            probe = new CountingProbe();
        }

        internal string Root { get; }

        internal string InstanceRoot { get; }

        internal string GameRoot { get; }

        internal IReadOnlyList<string> ProtectedRoots { get; }

        internal IReadOnlyList<string> SideEffectRoots { get; }

        internal string ReparseCanary { get; }

        internal string ReparseTarget { get; }

        internal int ProcessProbeCallCount => probe.CallCount;

        internal Mo2SnapshotCaptureRequest Request { get; }

        internal Mo2SnapshotCapture CreateCapture()
        {
            return new Mo2SnapshotCapture(
                new AcceptingManifests(),
                probe,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                null);
        }

        public void Dispose()
        {
            if (Directory.Exists(ReparseCanary)
                && (File.GetAttributes(ReparseCanary) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(ReparseCanary);
            }

            Directory.Delete(Root, recursive: true);
        }

        private static void CreateRequiredJunction(string link, string target)
        {
            ProcessStartInfo start = new()
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink");
            start.ArgumentList.Add("/J");
            start.ArgumentList.Add(link);
            start.ArgumentList.Add(target);
            using System.Diagnostics.Process process =
                System.Diagnostics.Process.Start(start)
                ?? throw new InvalidOperationException(
                    "The required disposable reparse-canary helper could not start.");
            process.WaitForExit();
            if (process.ExitCode != 0
                || !Directory.Exists(link)
                || (File.GetAttributes(link) & FileAttributes.ReparsePoint) == 0)
            {
                throw new InvalidOperationException(
                    "The required disposable junction could not be created: "
                    + process.StandardError.ReadToEnd());
            }
        }
    }

    private sealed class CountingProbe : IMo2ProcessProbe
    {
        internal int CallCount { get; private set; }

        public bool IsRunning(string exactExecutablePath)
        {
            Assert.IsTrue(File.Exists(exactExecutablePath));
            CallCount++;
            return false;
        }
    }

    private sealed class AcceptingManifests : IExecutableAdmissionService
    {
        public ExecutableAdmission AdmitMo2(string path)
        {
            return Accepted(SupportedExecutableManifests.Mo2ManifestId, path);
        }

        public ExecutableAdmission AdmitSkyrimGamePlugin(string path)
        {
            return Accepted(
                SupportedExecutableManifests.SkyrimGamePluginManifestId,
                path);
        }

        public ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context)
        {
            Assert.AreEqual("windows-x64", context.Platform);
            return Accepted(SupportedExecutableManifests.SkyrimManifestId, path);
        }

        private static ExecutableAdmission Accepted(string manifestId, string path)
        {
            return new ExecutableAdmission(
                AdmissionState.Accepted,
                manifestId,
                new ExecutableIdentity(
                    Path.GetFileName(path),
                    1,
                    new string('a', 64),
                    "synthetic",
                    null,
                    null,
                    null),
                []);
        }
    }

    private const int FindStreamInfoStandard = 0;
    private const int FileAttributeTagInfo = 9;
    private const int ErrorHandleEof = 38;
    private const int ErrorNoMoreFiles = 18;
    private const uint FileReadAttributes = 0x00000080;
    private const uint DeleteAccess = 0x00010000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);

    private sealed record ProcessTreeEntry(
        int ProcessId,
        int ParentProcessId,
        string ExecutableFile);

    private sealed record ProcessEvidence(
        IReadOnlyList<string> CurrentProcessDescendants,
        IReadOnlyList<string> TargetProcesses);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Win32FindStreamData
    {
        public long StreamSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        public string StreamName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInformation
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindFirstStreamW(
        string fileName,
        int infoLevel,
        out Win32FindStreamData findStreamData,
        uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(
        nint findStream,
        out Win32FindStreamData findStreamData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(nint findFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out FileAttributeTagInformation fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(
        uint flags,
        uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(
        SafeFileHandle snapshot,
        ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(
        SafeFileHandle snapshot,
        ref ProcessEntry32 entry);
}
