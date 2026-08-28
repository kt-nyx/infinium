using System.Windows;
using Infinium.DesktopHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Infinium.Tests;

[TestClass]
public sealed class DesktopRuntimePolicyTests
{
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
        System.Windows.Application application = System.Windows.Application.Current ?? new System.Windows.Application();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        foreach (DesktopRuntimeStatus status in new[]
        {
            DesktopRuntimePolicy.ClassifyRuntimeVersion(null),
            DesktopRuntimePolicy.ClassifyRuntimeVersion("151.0.0.0"),
        })
        {
            MainWindow window = new(
                DesktopLaunchOptions.ForTest(Path.Combine(Path.GetTempPath(), $"infinium-runtime-fallback-{Guid.NewGuid():N}")),
                () => status);
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
        GC.KeepAlive(application);
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
        const string variable = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
        string? prior = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, "--remote-debugging-port=9222");
            Assert.ThrowsExactly<InvalidOperationException>(DesktopRuntimePolicy.EnsureSafeBrowserEnvironment);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, prior);
        }

        foreach (RegistryHive expectedHive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (RegistryView expectedView in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                bool rejected = DesktopRuntimePolicy.HasPrivilegedPolicyOverride((hive, view) =>
                    hive == expectedHive && view == expectedView
                        ? new Dictionary<string, object?> { ["Infinium.DesktopHost.exe"] = "--remote-debugging-port=9222" }
                        : new Dictionary<string, object?>());
                Assert.IsTrue(rejected, $"Policy override was not rejected for {expectedHive}/{expectedView}.");
            }
        }
        Assert.IsTrue(DesktopRuntimePolicy.HasPrivilegedPolicyOverride((_, _) =>
            new Dictionary<string, object?> { ["*"] = "--disable-web-security" }));
        Assert.IsFalse(DesktopRuntimePolicy.HasPrivilegedPolicyOverride((_, _) =>
            new Dictionary<string, object?> { ["Another.exe"] = "--disable-web-security" }));
    }

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
