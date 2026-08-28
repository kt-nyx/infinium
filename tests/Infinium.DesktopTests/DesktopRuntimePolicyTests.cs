using System.Windows;
using Infinium.DesktopHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Infinium.Tests;

[TestClass]
public sealed class DesktopRuntimePolicyTests
{
    private static readonly string[] ExpectedPrivilegedEnvironmentVariables =
    [
        "WEBVIEW2_BROWSER_EXECUTABLE_FOLDER",
        "WEBVIEW2_USER_DATA_FOLDER",
        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
        "WEBVIEW2_RELEASE_CHANNEL_PREFERENCE",
        "WEBVIEW2_CHANNEL_SEARCH_KIND",
        "WEBVIEW2_RELEASE_CHANNELS",
        "WEBVIEW2_WAIT_FOR_SCRIPT_DEBUGGER",
        "WEBVIEW2_PIPE_FOR_SCRIPT_DEBUGGER",
    ];

    private static readonly (string Name, bool SupportsWildcard)[] ExpectedPrivilegedPolicyFamilies =
    [
        ("BrowserExecutableFolder", true),
        ("UserDataFolder", true),
        ("AdditionalBrowserArguments", true),
        ("ReleaseChannelPreference", true),
        ("ChannelSearchKind", true),
        ("ReleaseChannels", true),
        ("DowngradeVersion", false),
    ];

    private static readonly string[] CaseVariedPolicyIdentities =
    [
        "iNfInIuM.PaCkAgE!dEsKtOp",
        "iNfInIuM.dEsKtOpHoSt.ExE",
        "TeStHoSt.ExE",
    ];

    [TestMethod]
    public void ExactRuntimeFloorIsAcceptedAndEarlierBuildIsRejected()
    {
        Assert.AreEqual(
            DesktopRuntimeAvailability.Available,
            DesktopRuntimePolicy.ClassifyRuntimeVersion("151.0.4129.50").Availability);
        Assert.AreEqual(
            DesktopRuntimeAvailability.Outdated,
            DesktopRuntimePolicy.ClassifyRuntimeVersion("151.0.0.0").Availability);
        Assert.AreEqual(
            DesktopRuntimeAvailability.Missing,
            DesktopRuntimePolicy.ClassifyRuntimeVersion(null).Availability);
    }

    [STATestMethod]
    public void MissingAndOutdatedRuntimeShowAnInertWpfFallbackWithoutCreatingWebView()
    {
        foreach (DesktopRuntimeStatus status in new[]
        {
            DesktopRuntimePolicy.ClassifyRuntimeVersion(null),
            DesktopRuntimePolicy.ClassifyRuntimeVersion("151.0.0.0"),
        })
        {
            MainWindow window = new(
                DesktopLaunchOptions.ForTest(Path.Combine(Path.GetTempPath(), $"infinium-runtime-fallback-{Guid.NewGuid():N}")),
                () => { },
                _ => status);
            try
            {
                window.Show();
                Assert.AreEqual(Visibility.Visible, window.RuntimeNotice.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.Browser.Visibility);
                Assert.AreEqual(status.InertReason, window.RuntimeNotice.Text);
                Assert.IsNull(window.Browser.CoreWebView2);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [TestMethod]
    public void TopLevelNavigationIsExactAndOriginValidationRejectsAuthorityChanges()
    {
        Assert.IsTrue(DesktopRuntimePolicy.IsAllowedTopLevelNavigation("https://app.infinium.invalid/index.html"));
        Assert.IsFalse(DesktopRuntimePolicy.IsAllowedTopLevelNavigation("https://app.infinium.invalid/"));
        Assert.IsFalse(DesktopRuntimePolicy.IsAllowedTopLevelNavigation("https://app.infinium.invalid/index.html?state=1"));
        Assert.IsFalse(DesktopRuntimePolicy.IsAllowedTopLevelNavigation("https://app.infinium.invalid/index.html#fragment"));
        Assert.IsFalse(DesktopRuntimePolicy.IsExactApplicationOrigin("https://app.infinium.invalid.attacker.example/index.html"));
        Assert.IsFalse(DesktopRuntimePolicy.IsExactApplicationOrigin("http://app.infinium.invalid/index.html"));
    }

    [TestMethod]
    public void ProductionLaunchDoesNotAcceptFilesystemAuthority()
    {
        Assert.ThrowsExactly<ArgumentException>(() => DesktopLaunchOptions.Parse([@"C:\arbitrary"]));
        Assert.ThrowsExactly<ArgumentException>(() => DesktopLaunchOptions.Parse(["--qualification-session", @"C:\arbitrary"]));
        DesktopLaunchOptions options = DesktopLaunchOptions.Parse([]);
        StringAssert.EndsWith(options.ProductRoot, Path.Combine("Infinium"));
        DesktopLaunchOptions qualification = DesktopLaunchOptions.Parse(["--qualification-session", "0123456789abcdef0123456789abcdef"]);
        Assert.IsTrue(qualification.IsQualification);
        StringAssert.EndsWith(qualification.ProductRoot, "infinium-desktop-qualification-0123456789abcdef0123456789abcdef");
    }

    [TestMethod]
    public void PackagedAssetManifestMatchesCompiledProvenanceAnchor()
    {
        string root = RepositoryRoot();
        IReadOnlySet<string> assets = AssetManifestVerifier.Verify(Path.Combine(root, "src", "Infinium.DesktopHost", "Assets"));
        Assert.HasCount(10, assets);
        Assert.IsTrue(assets.Contains("/index.html"));
    }

    [TestMethod]
    public void RendererAssetsPreserveReflowAndEnforceClosedContentPolicy()
    {
        string assets = Path.Combine(RepositoryRoot(), "src", "Infinium.DesktopHost", "Assets");
        string styles = File.ReadAllText(Path.Combine(assets, "app.css"));
        string document = File.ReadAllText(Path.Combine(assets, "index.html"));
        StringAssert.Contains(styles, "overflow-wrap: anywhere");
        Assert.IsFalse(styles.Contains("white-space: nowrap", StringComparison.Ordinal));
        StringAssert.Contains(document, "default-src 'none'");
        StringAssert.Contains(document, "require-trusted-types-for 'script'; trusted-types 'none'");
        Assert.AreEqual(0.5, DesktopRuntimePolicy.NormalizeZoom(0.4));
        Assert.AreEqual(2.0, DesktopRuntimePolicy.NormalizeZoom(2.1));
        Assert.AreEqual(1.3, DesktopRuntimePolicy.NormalizeZoom(1.26));
    }

    [TestMethod]
    public void ReleaseEnvironmentAndEveryPolicyHiveViewRejectBrowserArgumentOverrides()
    {
        const string secretValue = "OVERRIDE-SECRET-MUST-NOT-ECHO";
        foreach (string variable in ExpectedPrivilegedEnvironmentVariables)
        {
            DesktopBrowserPreflightException exception = Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
                DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                    name => StringComparer.Ordinal.Equals(name, variable) ? secretValue : null,
                    EmptyPolicy,
                    TestIdentities));
            Assert.IsFalse(exception.Message.Contains(secretValue, StringComparison.Ordinal));
            Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
                DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                    name => StringComparer.Ordinal.Equals(name, variable) ? " \t " : null,
                    EmptyPolicy,
                    TestIdentities));
        }

        DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
            name => StringComparer.Ordinal.Equals(name, ExpectedPrivilegedEnvironmentVariables[0]) ? null : string.Empty,
            (_, _, family) => family == "AdditionalBrowserArguments"
                ? new Dictionary<string, object?> { [DesktopRuntimePolicy.ProductionExecutableFileName] = string.Empty }
                : new Dictionary<string, object?>(),
            TestIdentities);

        Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
            DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                _ => null,
                (_, _, family) => family == "UserDataFolder"
                    ? new Dictionary<string, object?> { [DesktopRuntimePolicy.ProductionExecutableFileName] = " " }
                    : new Dictionary<string, object?>(),
                TestIdentities));
    }

    [TestMethod]
    public void PrivilegedOverrideSurfaceMatchesIndependentExpectedLists()
    {
        CollectionAssert.AreEqual(
            ExpectedPrivilegedEnvironmentVariables,
            DesktopRuntimePolicy.PrivilegedEnvironmentVariables);
        CollectionAssert.AreEqual(
            ExpectedPrivilegedPolicyFamilies.Select(value => $"{value.Name}:{value.SupportsWildcard}").ToArray(),
            DesktopRuntimePolicy.PrivilegedPolicyFamilies.Select(value => $"{value.Name}:{value.SupportsWildcard}").ToArray());
    }

    [TestMethod]
    public void EveryPolicyFamilyHiveViewAndExactIdentityRejectsNonemptyOverrides()
    {
        DesktopBrowserPolicyIdentities identities = TestIdentities();
        string[] exactIdentities = [identities.ApplicationUserModelId!, DesktopRuntimePolicy.ProductionExecutableFileName, identities.ExecutableFileName];
        foreach ((string familyName, _) in ExpectedPrivilegedPolicyFamilies)
        {
            foreach (RegistryHive expectedHive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                foreach (RegistryView expectedView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    foreach (string expectedIdentity in exactIdentities)
                    {
                        const string secretValue = "REGISTRY-SECRET-MUST-NOT-ECHO";
                        DesktopBrowserPreflightException exception = Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
                            DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                                _ => null,
                                (hive, view, name) => hive == expectedHive && view == expectedView && name == familyName
                                    ? new Dictionary<string, object?> { [expectedIdentity] = secretValue }
                                    : new Dictionary<string, object?>(),
                                TestIdentities));
                        Assert.IsFalse(exception.Message.Contains(secretValue, StringComparison.Ordinal));
                    }
                }
            }
        }
    }

    [TestMethod]
    public void WildcardSupportAndExactExecutableIdentityRulesMatchWebViewPolicyFamilies()
    {
        foreach ((string familyName, bool supportsWildcard) in ExpectedPrivilegedPolicyFamilies)
        {
            Action inspect = () => DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                _ => null,
                (_, _, name) => name == familyName
                    ? new Dictionary<string, object?> { ["*"] = "configured" }
                    : new Dictionary<string, object?>(),
                TestIdentities);
            if (supportsWildcard)
            {
                Assert.ThrowsExactly<DesktopBrowserPreflightException>(inspect, familyName);
            }
            else
            {
                inspect();
            }
        }

        DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
            _ => null,
            (_, _, family) => family == "AdditionalBrowserArguments"
                ? new Dictionary<string, object?>
                {
                    ["Infinium.DesktopHost"] = "extensionless-must-not-match",
                    ["Another.exe"] = "unrelated",
                }
                : new Dictionary<string, object?>(),
            TestIdentities);

        foreach (string variedIdentity in CaseVariedPolicyIdentities)
        {
            Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
                DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                    _ => null,
                    (_, _, family) => family == "UserDataFolder"
                        ? new Dictionary<string, object?> { [variedIdentity] = "configured" }
                        : new Dictionary<string, object?>(),
                    TestIdentities));
        }
    }

    [TestMethod]
    public void PolicyScanOrderIsMachineThenUserAnd64Then32ForEveryFamily()
    {
        List<string> visited = [];
        DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
            _ => null,
            (hive, view, family) =>
            {
                visited.Add($"{hive}/{view}/{family}");
                return new Dictionary<string, object?>();
            },
            TestIdentities);

        List<string> expected = [];
        foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                expected.AddRange(ExpectedPrivilegedPolicyFamilies.Select(family => $"{hive}/{view}/{family.Name}"));
            }
        }
        CollectionAssert.AreEqual(expected, visited);
    }

    [TestMethod]
    public void UnreadablePolicyOrIdentityFailsClosedWithoutEchoingValues()
    {
        const string secretValue = "UNREADABLE-SECRET-MUST-NOT-ECHO";
        DesktopBrowserPreflightException policyFailure = Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
            DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                _ => null,
                (_, _, _) => throw new IOException(secretValue),
                TestIdentities));
        Assert.IsFalse(policyFailure.Message.Contains(secretValue, StringComparison.Ordinal));
        Assert.IsFalse(policyFailure.ToString().Contains(secretValue, StringComparison.Ordinal));

        DesktopBrowserPreflightException identityFailure = Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
            DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                _ => null,
                EmptyPolicy,
                () => throw new InvalidOperationException(secretValue)));
        Assert.IsFalse(identityFailure.Message.Contains(secretValue, StringComparison.Ordinal));
        Assert.IsFalse(identityFailure.ToString().Contains(secretValue, StringComparison.Ordinal));

        bool policyRead = false;
        DesktopBrowserPreflightException environmentFailure = Assert.ThrowsExactly<DesktopBrowserPreflightException>(() =>
            DesktopRuntimePolicy.EnsureSafeBrowserEnvironment(
                _ => throw new IOException(secretValue),
                (_, _, _) =>
                {
                    policyRead = true;
                    return new Dictionary<string, object?>();
                },
                TestIdentities));
        Assert.IsFalse(policyRead);
        Assert.IsFalse(environmentFailure.Message.Contains(secretValue, StringComparison.Ordinal));
        Assert.IsFalse(environmentFailure.ToString().Contains(secretValue, StringComparison.Ordinal));
    }

    [TestMethod]
    public void StartupPreflightRejectsBeforeOptionsOrWindowConstruction()
    {
        List<string> calls = [];
        Assert.ThrowsExactly<DesktopBrowserPreflightException>(() => DesktopHostStartup.Start(
            [],
            () =>
            {
                calls.Add("preflight");
                throw new DesktopBrowserPreflightException("safe refusal");
            },
            arguments =>
            {
                calls.Add("options");
                return DesktopLaunchOptions.Parse(arguments);
            },
            _ => calls.Add("window")));
        Assert.HasCount(1, calls);
        Assert.AreEqual("preflight", calls[0]);
    }

    [STATestMethod]
    public void DirectWindowPreflightRejectsBeforeRuntimeProbeOrCoreCreation()
    {
        int runtimeProbes = 0;
        MainWindow window = new(
            DesktopLaunchOptions.ForTest(Path.Combine(Path.GetTempPath(), $"infinium-runtime-preflight-{Guid.NewGuid():N}")),
            () => throw new DesktopBrowserPreflightException("safe refusal"),
            _ =>
            {
                runtimeProbes++;
                return DesktopRuntimePolicy.ClassifyRuntimeVersion(DesktopRuntimePolicy.MinimumRuntimeVersion);
            });
        try
        {
            window.InitializeBrowserForTestAsync().GetAwaiter().GetResult();
            Assert.AreEqual(0, runtimeProbes);
            Assert.AreEqual(1, window.BrowserPreflightCheckCount);
            Assert.IsNull(window.Browser.CoreWebView2);
            Assert.AreEqual("safe refusal", window.RuntimeNotice.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void PreflightRepeatsImmediatelyBeforeEnvironmentCreation()
    {
        int checks = 0;
        int runtimeProbes = 0;
        MainWindow window = new(
            DesktopLaunchOptions.ForTest(Path.Combine(Path.GetTempPath(), $"infinium-runtime-repeat-{Guid.NewGuid():N}")),
            () =>
            {
                checks++;
                if (checks == 2)
                {
                    throw new DesktopBrowserPreflightException("safe second refusal");
                }
            },
            options =>
            {
                runtimeProbes++;
                Assert.AreEqual(CoreWebView2ReleaseChannels.Stable, options.ReleaseChannels);
                Assert.AreEqual(CoreWebView2ChannelSearchKind.MostStable, options.ChannelSearchKind);
                return DesktopRuntimePolicy.ClassifyRuntimeVersion(DesktopRuntimePolicy.MinimumRuntimeVersion);
            });
        try
        {
            window.InitializeBrowserForTestAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, checks);
            Assert.AreEqual(2, window.BrowserPreflightCheckCount);
            Assert.AreEqual(1, runtimeProbes);
            Assert.IsNull(window.Browser.CoreWebView2);
            Assert.AreEqual("safe second refusal", window.RuntimeNotice.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [STATestMethod]
    public void EveryBrowserInitializationRevalidatesPreflight()
    {
        int checks = 0;
        MainWindow window = new(
            DesktopLaunchOptions.ForTest(Path.Combine(Path.GetTempPath(), $"infinium-runtime-revalidate-{Guid.NewGuid():N}")),
            () => checks++,
            _ => DesktopRuntimePolicy.ClassifyRuntimeVersion(null));
        try
        {
            window.InitializeBrowserForTestAsync().GetAwaiter().GetResult();
            window.InitializeBrowserForTestAsync().GetAwaiter().GetResult();
            window.InitializeBrowserForTestAsync().GetAwaiter().GetResult();
            Assert.AreEqual(3, checks);
            Assert.AreEqual(3, window.BrowserPreflightCheckCount);
            Assert.IsNull(window.Browser.CoreWebView2);
        }
        finally
        {
            window.Close();
        }
    }

    [TestMethod]
    public void RuntimeProbeAndCreationOptionsAreStableOnlyAndExactFloorBound()
    {
        CoreWebView2EnvironmentOptions options = DesktopRuntimePolicy.CreateEnvironmentOptions();
        Assert.AreEqual(CoreWebView2ReleaseChannels.Stable, options.ReleaseChannels);
        Assert.AreEqual(CoreWebView2ChannelSearchKind.MostStable, options.ChannelSearchKind);
        Assert.AreEqual(string.Empty, options.AdditionalBrowserArguments);
        Assert.IsFalse(options.AllowSingleSignOnUsingOSPrimaryAccount);
        Assert.IsFalse(options.AreBrowserExtensionsEnabled);
        Assert.IsTrue(options.ExclusiveUserDataFolderAccess);
        Assert.IsFalse(options.IsCustomCrashReportingEnabled);
        Assert.AreEqual(DesktopRuntimeAvailability.Unsupported, DesktopRuntimePolicy.ClassifyRuntimeVersion("151.0.4129.107 dev").Availability);
        Assert.AreEqual(DesktopRuntimeAvailability.Unsupported, DesktopRuntimePolicy.ClassifyRuntimeVersion("malformed").Availability);
        Assert.AreEqual(DesktopRuntimeAvailability.Available, DesktopRuntimePolicy.ClassifyRuntimeVersion(DesktopRuntimePolicy.MinimumRuntimeVersion).Availability);
    }

    private static IReadOnlyDictionary<string, object?> EmptyPolicy(RegistryHive hive, RegistryView view, string family)
        => new Dictionary<string, object?>();

    private static DesktopBrowserPolicyIdentities TestIdentities()
        => new("Infinium.Package!Desktop", "testhost.exe");

    private static string RepositoryRoot()
    {
        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "Infinium.sln")))
        {
            cursor = cursor.Parent;
        }

        return cursor?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
