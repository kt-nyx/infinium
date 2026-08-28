using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;
using Infinium.Application.Runtime;
using Infinium.DesktopHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DesktopWebViewQualificationTests
{
    private static readonly JsonSerializerOptions MeasurementJsonOptions = new() { WriteIndented = true };

    [STATestMethod]
    [TestCategory("DesktopQualification")]
    public void ProtectedRendererLoadsWithAccessibleLandmarksAndBoundSession()
    {
        string productRoot = RequireQualificationRoot();
        System.Windows.Application application = System.Windows.Application.Current ?? new System.Windows.Application();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        bool populated = true;
        Dictionary<string, List<double>> milliseconds = new(StringComparer.Ordinal);
        List<MemorySample> idleMemory = [];
        List<MemorySample> activeMemory = [];
        using Process coordinator = StartCoordinator(productRoot);
        MainWindow? window = null;
        try
        {
            PumpUntil(() => DescriptorMatchesProcess(productRoot, coordinator.Id), TimeSpan.FromSeconds(20));
            window = new MainWindow(DesktopLaunchOptions.ForTest(productRoot));
            Stopwatch bootstrapTimer = Stopwatch.StartNew();
            window.Show();
            PumpUntil(() => window.Browser.CoreWebView2 is not null, TimeSpan.FromSeconds(20));
            PumpUntil(() => EvaluateBoolean(window, "document.querySelector('main') !== null"), TimeSpan.FromSeconds(20));
            PumpUntil(() => EvaluateBoolean(window, "document.getElementById('status')?.textContent === 'Local application service connected.'"), TimeSpan.FromSeconds(20));
            Record(milliseconds, "window_show_to_bootstrap", bootstrapTimer.Elapsed.TotalMilliseconds);
            idleMemory.AddRange(CaptureMemorySamples(window, 5));

            Assert.IsTrue(EvaluateBoolean(window, "document.getElementById('root')?.hasAttribute('aria-busy') === false"));
            Assert.IsTrue(EvaluateBoolean(window, "document.querySelectorAll('main section[aria-labelledby]').length >= 4"));
            Assert.IsTrue(EvaluateBoolean(window, "[...document.querySelectorAll('button')].every(button => button.textContent?.trim().length > 0)"));
            Assert.IsTrue(EvaluateBoolean(window, "document.querySelector('.result-viewport')?.getAttribute('role') === 'list'"));
            Assert.IsTrue(EvaluateBoolean(window, "location.href === 'https://app.infinium.invalid/index.html'"));
            Assert.IsTrue(EvaluateBoolean(window, "document.querySelector('meta[http-equiv=Content-Security-Policy]')?.content.includes(\"trusted-types 'none'\") === true"));
            AssertProtectedLiveSettings(window);

            string runId = populated ? "run-candidate" : "missing_opaque_run";
            Assert.IsTrue(EvaluateBoolean(window, $"(() => {{ const input = document.getElementById('run-id'); const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set; setter?.call(input, '{runId}'); input?.dispatchEvent(new Event('input', {{ bubbles: true }})); return true; }})()"));
            int samples = populated ? 10 : 1;
            for (int sample = 0; sample < samples; sample++)
            {
                Record(milliseconds, "finding_page_bridge", MeasureCompletedOperation(window, "Query first page"));
            }
            Assert.IsTrue(EvaluateBoolean(window, $"document.getElementById('status')?.textContent === 'Result query state: {(populated ? "accepted" : "rejected")}.'"));
            if (populated)
            {
                Assert.IsTrue(EvaluateBoolean(window, "document.querySelector('.result-viewport img') === null"));
                Assert.IsTrue(EvaluateBoolean(window, "document.querySelector('.result-viewport')?.textContent?.includes(\"<img src=x onerror=alert('inert')>\") === true"));
                for (int sample = 0; sample < samples; sample++)
                {
                    Record(milliseconds, "finding_detail_bridge", MeasureCompletedOperation(window, "Read first result detail"));
                }
                Assert.AreEqual("Detail state: accepted.", EvaluateString(window, "document.getElementById('status')?.textContent ?? ''"));
                Record(milliseconds, "second_page_bridge", MeasureCompletedOperation(window, "Load next bounded page"));
                Assert.IsTrue(EvaluateBoolean(window, "document.querySelector('[aria-posinset=\"101\"]') !== null"));
            }
            for (int sample = 0; sample < samples; sample++)
            {
                Record(milliseconds, "progress_bridge", MeasureCompletedOperation(window, "Read progress"));
            }
            Assert.AreEqual(
                $"Progress state: {(populated ? "accepted" : "rejected")}.",
                EvaluateString(window, "document.getElementById('status')?.textContent ?? ''"));
            if (populated)
            {
                Stopwatch cancelTimer = Stopwatch.StartNew();
                Assert.IsTrue(Click(window, "Subscribe to progress"));
                try
                {
                    PumpUntil(() => EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === true"), TimeSpan.FromSeconds(20));
                }
                catch (TimeoutException exception)
                {
                    throw new TimeoutException($"The renderer did not retain an active subscription. Final inert status: {EvaluateString(window, "document.getElementById('status')?.textContent ?? ''")}", exception);
                }
                activeMemory.AddRange(CaptureMemorySamples(window, 5));
                window.CancelTransportButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                PumpUntil(() => EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === false"), TimeSpan.FromSeconds(20));
                Record(milliseconds, "subscription_cancel_bridge", cancelTimer.Elapsed.TotalMilliseconds);
                Assert.IsTrue(Click(window, "Subscribe to progress"));
                PumpUntil(() => EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === true"), TimeSpan.FromSeconds(20));
                for (int sample = 0; sample < 5; sample++)
                {
                    Record(milliseconds, "authoritative_resync_bridge", MeasureCompletedOperation(window, "Authoritative resync"));
                    Assert.IsTrue(EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === true"));
                }
                window.CancelTransportButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                PumpUntil(() => EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === false"), TimeSpan.FromSeconds(20));
                for (int sample = 0; sample < 3; sample++)
                {
                    Assert.IsTrue(EvaluateBoolean(window, "document.body.dataset.qualificationOld = 'true'; true"));
                    Stopwatch reloadTimer = Stopwatch.StartNew();
                    window.Browser.Reload();
                    PumpUntil(() => TryEvaluateBoolean(window, "document.body.dataset.qualificationOld === undefined && document.getElementById('status')?.textContent === 'Local application service connected.'"), TimeSpan.FromSeconds(20));
                    Record(milliseconds, "reload_to_bootstrap", reloadTimer.Elapsed.TotalMilliseconds);
                }
            }

            string accessibility = WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Accessibility.getFullAXTree",
                "{}"));
            StringAssert.Contains(accessibility, "Infinium desktop consumption proof");
            StringAssert.Contains(accessibility, "Diagnostic operations");
            StringAssert.Contains(accessibility, "Virtualized result summaries");
            StringAssert.Contains(accessibility, "status");

            AutomationElement windowElement = AutomationElement.FromHandle(new WindowInteropHelper(window).Handle);
            AutomationElement cancelElement = windowElement.FindFirst(TreeScope.Descendants, new PropertyCondition(
                AutomationElement.NameProperty,
                "Cancel active renderer operation"));
            Assert.IsNotNull(cancelElement);
            Assert.AreEqual(ControlType.Button, cancelElement.Current.ControlType);
            AutomationElement browserElement = windowElement.FindFirst(TreeScope.Descendants, new PropertyCondition(
                AutomationElement.NameProperty,
                "Infinium diagnostic application"));
            Assert.IsNotNull(browserElement);
            Assert.AreEqual(ControlType.Pane, browserElement.Current.ControlType);
            AutomationElement? rendererStatus = null;
            PumpUntil(() => (rendererStatus = FindRendererStatus(windowElement)) is not null, TimeSpan.FromSeconds(5));
            Assert.IsNotNull(rendererStatus, DescribeAutomationTree(windowElement));
            Assert.AreEqual(ControlType.Text, rendererStatus.Current.ControlType);

            string initialFocus = EvaluateString(window, "document.activeElement?.id ?? ''");
            Assert.IsTrue(initialFocus is "main" or "result-viewport");
            WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyDown\",\"key\":\"Tab\",\"code\":\"Tab\"}"));
            WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", "{\"type\":\"keyUp\",\"key\":\"Tab\",\"code\":\"Tab\"}"));
            Assert.IsTrue(EvaluateBoolean(window, "document.activeElement !== document.body && document.activeElement?.id !== 'result-viewport'"));
            Assert.IsFalse((AutomationElement.FocusedElement?.Current.Name ?? string.Empty).Contains("SECRET-CANARY", StringComparison.Ordinal));

            Assert.IsTrue(EvaluateBoolean(window, "(() => { const e = document.body; const s = getComputedStyle(e); const parse = value => value.match(/\\d+(?:\\.\\d+)?/g).slice(0,3).map(Number); const lum = rgb => { const c = rgb.map(v => v / 255).map(v => v <= .04045 ? v / 12.92 : Math.pow((v + .055) / 1.055, 2.4)); return .2126*c[0] + .7152*c[1] + .0722*c[2]; }; const fg = lum(parse(s.color)); const bg = lum(parse(s.backgroundColor)); return (Math.max(fg,bg)+.05)/(Math.min(fg,bg)+.05) >= 7; })()"));
            window.Browser.ZoomFactor = 2.0;
            PumpEvents(TimeSpan.FromMilliseconds(200));
            Assert.AreEqual(2.0, window.Browser.ZoomFactor, 0.001);
            Assert.IsTrue(EvaluateBoolean(window, "document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1 && [...document.querySelectorAll('.result-row')].every(row => row.scrollHeight >= row.clientHeight)"));
            window.Browser.ZoomFactor = 1.0;
            WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", "{\"features\":[{\"name\":\"prefers-reduced-motion\",\"value\":\"reduce\"}]}"));
            Assert.IsTrue(EvaluateBoolean(window, "matchMedia('(prefers-reduced-motion: reduce)').matches && getComputedStyle(document.querySelector('button')).transitionDuration === '0s'"));
            WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", "{\"features\":[]}"));
            ExerciseDeniedBrowserCapabilities(window);
            string canary = Environment.GetEnvironmentVariable("INFINIUM_DESKTOP_SECRET_CANARY") ?? string.Empty;
            if (canary.Length > 0)
            {
                Assert.IsFalse(EvaluateString(window, "document.documentElement.textContent ?? ''").Contains(canary, StringComparison.Ordinal));
                Assert.IsFalse(window.RuntimeNotice.Text.Contains(canary, StringComparison.Ordinal));
            }
            WriteMeasurements(window, milliseconds, idleMemory, activeMemory);
        }
        finally
        {
            if (window is not null)
            {
                window.Close();
                PumpEvents(TimeSpan.FromMilliseconds(500));
            }
            if (!coordinator.HasExited)
            {
                coordinator.Kill(entireProcessTree: true);
                coordinator.WaitForExit(5_000);
            }
            AssertProcessOutputExcludesCanary(coordinator);
            AssertFilesExcludeCanary(productRoot);
            GC.KeepAlive(application);
        }
    }

    private static void AssertProtectedLiveSettings(MainWindow window)
    {
        Microsoft.Web.WebView2.Core.CoreWebView2Settings settings = window.Browser.CoreWebView2.Settings;
        Assert.IsFalse(settings.AreHostObjectsAllowed);
        Assert.IsTrue(settings.IsScriptEnabled);
        Assert.IsTrue(settings.IsWebMessageEnabled);
        Assert.IsFalse(settings.AreDevToolsEnabled);
        Assert.IsFalse(settings.AreDefaultContextMenusEnabled);
        Assert.IsFalse(settings.AreDefaultScriptDialogsEnabled);
        Assert.IsFalse(settings.AreBrowserAcceleratorKeysEnabled);
        Assert.IsFalse(settings.IsBuiltInErrorPageEnabled);
        Assert.IsFalse(settings.IsGeneralAutofillEnabled);
        Assert.IsFalse(settings.IsPasswordAutosaveEnabled);
        Assert.IsFalse(settings.IsStatusBarEnabled);
        Assert.IsFalse(settings.IsSwipeNavigationEnabled);
        Assert.IsTrue(settings.IsZoomControlEnabled);
        Assert.IsTrue(settings.IsPinchZoomEnabled);
        Assert.IsFalse(settings.IsNonClientRegionSupportEnabled);
        Assert.IsFalse(window.Browser.AllowExternalDrop);
    }

    private static void ExerciseDeniedBrowserCapabilities(MainWindow window)
    {
        DesktopSecurityEventMetrics metrics = window.SecurityEvents;
        int top = metrics.TopNavigationDeniedCount;
        int frame = metrics.FrameNavigationDeniedCount;
        int resource = metrics.ResourceDeniedCount;
        int newWindow = metrics.NewWindowDeniedCount;
        int download = metrics.DownloadDeniedCount;
        int permission = metrics.PermissionDeniedCount;
        string downloadName = $"infinium-denied-{Guid.NewGuid():N}.html";
        string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", downloadName);
        Assert.IsFalse(File.Exists(downloadPath));

        Assert.IsTrue(EvaluateBooleanWithUserGesture(window, $"(() => {{ fetch('/not-allowlisted.json').then(response => document.body.dataset.fetchResult=response.ok?'unexpected':'blocked').catch(() => document.body.dataset.fetchResult='blocked'); const image=new Image(); image.src='/not-allowlisted.png'; document.body.append(image); const frame=document.createElement('iframe'); frame.src='https://external.invalid/frame'; document.body.append(frame); window.open('https://external.invalid/window'); const link=document.createElement('a'); link.href='/index.html'; link.download={JsonSerializer.Serialize(downloadName)}; document.body.append(link); link.click(); navigator.geolocation.getCurrentPosition(() => document.body.dataset.permissionResult='unexpected', () => document.body.dataset.permissionResult='blocked'); location.assign('https://external.invalid/navigation'); return true; }})()"));
        PumpEvents(TimeSpan.FromSeconds(3));
        Assert.IsTrue(metrics.TopNavigationDeniedCount > top, $"top navigation denial count: {metrics.TopNavigationDeniedCount}");
        Assert.IsTrue(metrics.ResourceDeniedCount > resource, $"resource denial count: {metrics.ResourceDeniedCount}");
        Assert.IsTrue(metrics.NewWindowDeniedCount > newWindow, $"new-window denial count: {metrics.NewWindowDeniedCount}");
        Assert.IsTrue(metrics.DownloadDeniedCount > download || !File.Exists(downloadPath), "The live download attempt must be denied either before or at the host event boundary.");
        Assert.IsFalse(File.Exists(downloadPath));
        Assert.IsTrue(metrics.PermissionDeniedCount > permission, $"permission denial count: {metrics.PermissionDeniedCount}");
        Assert.AreEqual("https://app.infinium.invalid/index.html", window.Browser.Source.AbsoluteUri);
        Assert.IsTrue(EvaluateBoolean(window, "(() => { const frame=document.querySelector('iframe'); try { return frame?.contentDocument?.URL !== 'https://external.invalid/frame'; } catch { return true; } })()"), "The CSP or frame-navigation guard must keep the external frame inert.");
        Assert.IsTrue(metrics.FrameNavigationDeniedCount > frame || EvaluateBoolean(window, "document.querySelector('iframe')?.contentDocument?.URL !== 'https://external.invalid/frame'"));
    }

    [STATestMethod]
    [TestCategory("DesktopLifecycleQualification")]
    public void RendererBrowserCoordinatorAndShellFailuresRecreateAuthoritativeSession()
    {
        string productRoot = RequireQualificationRoot();
        System.Windows.Application application = System.Windows.Application.Current ?? new System.Windows.Application();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Process coordinator = StartCoordinator(productRoot);
        MainWindow? window = null;
        try
        {
            PumpUntil(() => DescriptorMatchesProcess(productRoot, coordinator.Id), TimeSpan.FromSeconds(20));
            window = ShowConnectedWindow(productRoot);

            string rendererSession = window.BridgeSessionId!;
            Microsoft.Web.WebView2.Wpf.WebView2 rendererControl = window.Browser;
            Task rendererCrash = window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.crash", "{}");
            _ = rendererCrash.ContinueWith(_ => { }, TaskScheduler.Default);
            PumpUntil(() => window.Browser != rendererControl
                && window.BridgeSessionId is not null
                && !StringComparer.Ordinal.Equals(window.BridgeSessionId, rendererSession)
                && TryEvaluateBoolean(window, "document.getElementById('status')?.textContent === 'Local application service connected.'"), TimeSpan.FromSeconds(30));

            rendererSession = window.BridgeSessionId!;
            rendererControl = window.Browser;
            uint browserProcessId = window.Browser.CoreWebView2.BrowserProcessId;
            Assert.IsTrue(window.Browser.CoreWebView2.Environment.GetProcessInfos().Any(info => info.ProcessId == browserProcessId));
            using (Process browser = Process.GetProcessById(checked((int)browserProcessId)))
            {
                browser.Kill(entireProcessTree: true);
                browser.WaitForExit(10_000);
            }
            PumpUntil(() => window.Browser != rendererControl
                && window.BridgeSessionId is not null
                && !StringComparer.Ordinal.Equals(window.BridgeSessionId, rendererSession)
                && TryEvaluateBoolean(window, "document.getElementById('status')?.textContent === 'Local application service connected.'"), TimeSpan.FromSeconds(30));

            SetRunId(window, "run-candidate");
            Assert.AreEqual("Result query state: accepted.", CompleteOperation(window, "Query first page"));
            Assert.AreEqual("Progress state: accepted.", CompleteOperation(window, "Read progress"));

            coordinator.Kill(entireProcessTree: true);
            coordinator.WaitForExit(10_000);
            AssertProcessOutputExcludesCanary(coordinator);
            coordinator.Dispose();
            coordinator = StartCoordinator(productRoot);
            PumpUntil(() => DescriptorMatchesProcess(productRoot, coordinator.Id), TimeSpan.FromSeconds(20));
            rendererSession = window.BridgeSessionId!;
            Assert.AreEqual(
                "Authoritative bootstrap, progress, and first result page were refreshed.",
                CompleteOperation(window, "Authoritative resync"));
            Assert.AreEqual(rendererSession, window.BridgeSessionId, "Coordinator recovery must not require a renderer-session rotation.");
            Assert.IsTrue(Click(window, "Subscribe to progress"));
            PumpUntil(() => EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === true"), TimeSpan.FromSeconds(20));
            Assert.IsTrue(EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === true"));

            Assert.IsTrue(EvaluateBoolean(window, "document.body.dataset.qualificationOld = 'true'; true"));
            window.Browser.Reload();
            PumpUntil(() => window.BridgeSessionId is not null
                && !StringComparer.Ordinal.Equals(window.BridgeSessionId, rendererSession)
                && TryEvaluateBoolean(window, "document.body.dataset.qualificationOld === undefined && document.getElementById('status')?.textContent === 'Local application service connected.'"), TimeSpan.FromSeconds(30));
            Assert.AreEqual(
                "Authoritative bootstrap, progress, and first result page were refreshed.",
                CompleteOperation(window, "Authoritative resync"));
            Assert.IsTrue(Click(window, "Subscribe to progress"));
            PumpUntil(() => EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === true"), TimeSpan.FromSeconds(20));

            window.Close();
            PumpEvents(TimeSpan.FromMilliseconds(500));
            window = ShowConnectedWindow(productRoot);
            Assert.IsFalse(StringComparer.Ordinal.Equals(window.BridgeSessionId, rendererSession));
            Assert.AreEqual(
                "Authoritative bootstrap, progress, and first result page were refreshed.",
                CompleteOperation(window, "Authoritative resync"));
            Assert.IsTrue(Click(window, "Subscribe to progress"));
            PumpUntil(() => EvaluateBoolean(window, "[...document.querySelectorAll('button')].find(value => value.textContent === 'Subscribe to progress')?.disabled === true"), TimeSpan.FromSeconds(20));
        }
        finally
        {
            try
            {
                if (window is not null)
                {
                    window.Close();
                    PumpEvents(TimeSpan.FromMilliseconds(500));
                }
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                if (!coordinator.HasExited)
                {
                    coordinator.Kill(entireProcessTree: true);
                    coordinator.WaitForExit(10_000);
                }
                AssertProcessOutputExcludesCanary(coordinator);
                AssertFilesExcludeCanary(productRoot);
                coordinator.Dispose();
            }
        }
    }

    private static string RequireQualificationRoot()
    {
        string? root = Environment.GetEnvironmentVariable("INFINIUM_DESKTOP_QUALIFICATION_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            Assert.Inconclusive("Live WebView qualification requires the exclusive root supplied by eng/qualify-desktop.ps1.");
        }
        return Path.GetFullPath(root!);
    }

    private static MainWindow ShowConnectedWindow(string productRoot)
    {
        MainWindow window = new(DesktopLaunchOptions.ForTest(productRoot));
        window.Show();
        PumpUntil(() => window.Browser.CoreWebView2 is not null, TimeSpan.FromSeconds(20));
        PumpUntil(() => TryEvaluateBoolean(window, "document.getElementById('status')?.textContent === 'Local application service connected.'"), TimeSpan.FromSeconds(20));
        return window;
    }

    private static void SetRunId(MainWindow window, string runId)
    {
        Assert.IsTrue(EvaluateBoolean(window, $"(() => {{ const input = document.getElementById('run-id'); const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set; setter?.call(input, {JsonSerializer.Serialize(runId)}); input?.dispatchEvent(new Event('input', {{ bubbles: true }})); return true; }})()"));
    }

    private static string CompleteOperation(MainWindow window, string button)
    {
        _ = MeasureCompletedOperation(window, button);
        return EvaluateString(window, "document.getElementById('status')?.textContent ?? ''");
    }

    private static void WriteMeasurements(
        MainWindow window,
        IReadOnlyDictionary<string, List<double>> milliseconds,
        IReadOnlyList<MemorySample> idleMemory,
        IReadOnlyList<MemorySample> activeMemory)
    {
        string? path = Environment.GetEnvironmentVariable("INFINIUM_DESKTOP_QUALIFICATION_MEASUREMENTS");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        DesktopRuntimeStatus runtime = DesktopRuntimePolicy.InspectRuntime();
        File.WriteAllText(fullPath, JsonSerializer.Serialize(new
        {
            schema = "infinium.desktop-qualification/v1",
            recorded_at = DateTimeOffset.UtcNow,
            os = Environment.OSVersion.VersionString,
            processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
            logical_processors = Environment.ProcessorCount,
            webview2_runtime = runtime.Version,
            renderer_contract = ProtocolConstants.RendererContractVersion,
            registry_version = GeneratedRendererOperationCatalog.RegistryVersion,
            registry_sha256 = GeneratedRendererOperationCatalog.RegistrySha256,
            maximum_message_bytes = ProtocolConstants.MaximumMessageBytes,
            maximum_chunk_bytes = ProtocolConstants.MaximumChunkBytes,
            maximum_queue_items = ProtocolConstants.MaximumStreamQueueItems,
            observed_message_bytes = new
            {
                request = window.BridgeMetrics.MaximumInboundRequestBytes,
                response = window.BridgeMetrics.MaximumOutboundResponseBytes,
                @event = window.BridgeMetrics.MaximumOutboundEventBytes,
            },
            milliseconds,
            private_working_set_bytes = new { idle = idleMemory, active = activeMemory },
        }, MeasurementJsonOptions));
    }

    private static double MeasureCompletedOperation(MainWindow window, string button)
    {
        string prior = EvaluateString(window, "document.getElementById('main')?.getAttribute('data-completed-operation-sequence') ?? ''");
        Stopwatch timer = Stopwatch.StartNew();
        Assert.IsTrue(Click(window, button));
        PumpUntil(() => EvaluateString(window, "document.getElementById('main')?.getAttribute('data-completed-operation-sequence') ?? ''") != prior, TimeSpan.FromSeconds(20));
        return timer.Elapsed.TotalMilliseconds;
    }

    private static void Record(Dictionary<string, List<double>> values, string name, double milliseconds)
    {
        if (!values.TryGetValue(name, out List<double>? samples))
        {
            samples = [];
            values.Add(name, samples);
        }
        samples.Add(milliseconds);
    }

    private static List<MemorySample> CaptureMemorySamples(MainWindow window, int count)
    {
        List<MemorySample> samples = [];
        for (int sample = 0; sample < count; sample++)
        {
            using Process host = Process.GetCurrentProcess();
            host.Refresh();
            long browserBytes = 0;
            int browserCount = 0;
            foreach (Microsoft.Web.WebView2.Core.CoreWebView2ProcessInfo info in window.Browser.CoreWebView2.Environment.GetProcessInfos())
            {
                try
                {
                    using Process browser = Process.GetProcessById(info.ProcessId);
                    browser.Refresh();
                    browserBytes += browser.PrivateMemorySize64;
                    browserCount++;
                }
                catch (ArgumentException)
                {
                }
            }
            samples.Add(new MemorySample(host.PrivateMemorySize64, browserBytes, browserCount, host.PrivateMemorySize64 + browserBytes));
            PumpEvents(TimeSpan.FromMilliseconds(50));
        }
        return samples;
    }

    private static bool Click(MainWindow window, string text)
        => EvaluateBoolean(window, $"(() => {{ const button = [...document.querySelectorAll('button')].find(value => value.textContent === {JsonSerializer.Serialize(text)}); button?.click(); return button !== undefined; }})()");

    private static bool TryEvaluateBoolean(MainWindow window, string expression)
    {
        try { return EvaluateBoolean(window, expression); }
        catch (KeyNotFoundException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private static Process StartCoordinator(string productRoot)
    {
        string executable = Path.Combine(RepositoryRoot(), "src", "Infinium.Coordinator", "bin", "Release", "net10.0", "Infinium.Coordinator.exe");
        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        start.ArgumentList.Add("--root");
        start.ArgumentList.Add(productRoot);
        start.ArgumentList.Add("--quiet");
        return Process.Start(start) ?? throw new InvalidOperationException("The qualification coordinator did not start.");
    }

    private static void AssertProcessOutputExcludesCanary(Process process)
    {
        string canary = Environment.GetEnvironmentVariable("INFINIUM_DESKTOP_SECRET_CANARY") ?? string.Empty;
        if (canary.Length == 0 || !process.HasExited)
        {
            return;
        }
        Assert.IsFalse(process.StandardOutput.ReadToEnd().Contains(canary, StringComparison.Ordinal));
        Assert.IsFalse(process.StandardError.ReadToEnd().Contains(canary, StringComparison.Ordinal));
    }

    private static void AssertFilesExcludeCanary(string root)
    {
        string canary = Environment.GetEnvironmentVariable("INFINIUM_DESKTOP_SECRET_CANARY") ?? string.Empty;
        if (canary.Length == 0 || !Directory.Exists(root))
        {
            return;
        }
        byte[] needle = System.Text.Encoding.UTF8.GetBytes(canary);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                Assert.IsFalse(StreamContains(stream, needle), $"A desktop qualification artifact contained the secret canary: {path}");
            }
            catch (FileNotFoundException)
            {
                // WebView2 may retire an empty transient while its exclusive
                // user-data tree is being enumerated; no artifact remains.
            }
            catch (DirectoryNotFoundException)
            {
                // The containing transient tree was retired after enumeration.
            }
            catch (IOException)
            {
                Assert.Fail($"A desktop qualification artifact could not be inspected for the secret canary: {path}");
            }
        }
    }

    private static bool StreamContains(Stream stream, byte[] needle)
    {
        byte[] buffer = new byte[64 * 1024 + needle.Length];
        int retained = 0;
        while (true)
        {
            int read = stream.Read(buffer, retained, buffer.Length - retained);
            if (read == 0)
            {
                return false;
            }
            int available = retained + read;
            if (buffer.AsSpan(0, available).IndexOf(needle) >= 0)
            {
                return true;
            }
            retained = Math.Min(needle.Length - 1, available);
            buffer.AsSpan(available - retained, retained).CopyTo(buffer);
        }
    }

    private static string DescribeAutomationTree(AutomationElement root)
    {
        List<string> values = [];
        AutomationElementCollection descendants = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
        foreach (AutomationElement element in descendants)
        {
            string name = element.Current.Name;
            if (name.Length > 0)
            {
                values.Add($"{element.Current.ControlType.ProgrammaticName}:{name}");
            }
        }
        return string.Join(" | ", values.Take(100));
    }

    private static AutomationElement? FindRendererStatus(AutomationElement root)
    {
        AutomationElementCollection descendants = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
        foreach (AutomationElement element in descendants)
        {
            string name = element.Current.Name;
            if (StringComparer.Ordinal.Equals(element.Current.ControlType.ProgrammaticName, "ControlType.Text")
                && (name.StartsWith("Progress state:", StringComparison.Ordinal)
                    || StringComparer.Ordinal.Equals(name, "Local application service connected.")))
            {
                return element;
            }
        }
        return null;
    }

    private static bool DescriptorMatchesProcess(string productRoot, int processId)
    {
        string path = Path.Combine(productRoot, "runtime", "coordinator.v1.json");
        if (!File.Exists(path))
        {
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.GetProperty("ProcessId").GetInt32() == processId;
        }
        catch (IOException) { return false; }
        catch (JsonException) { return false; }
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

    private static bool EvaluateBoolean(MainWindow window, string expression)
    {
        string response = WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "Runtime.evaluate",
            JsonSerializer.Serialize(new { expression, returnByValue = true })));
        using JsonDocument document = JsonDocument.Parse(response);
        JsonElement result = document.RootElement.GetProperty("result");
        return result.GetProperty("value").GetBoolean();
    }

    private static bool EvaluateBooleanWithUserGesture(MainWindow window, string expression)
    {
        string response = WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "Runtime.evaluate",
            JsonSerializer.Serialize(new { expression, returnByValue = true, userGesture = true })));
        using JsonDocument document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("result").GetProperty("value").GetBoolean();
    }

    private static string EvaluateString(MainWindow window, string expression)
    {
        string response = WaitFor(window.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "Runtime.evaluate",
            JsonSerializer.Serialize(new { expression, returnByValue = true })));
        using JsonDocument document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("result").GetProperty("value").GetString()!;
    }

    private static T WaitFor<T>(Task<T> task)
    {
        PumpUntil(() => task.IsCompleted, TimeSpan.FromSeconds(20));
        return task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The protected renderer did not reach its expected state.");
            }

            PumpEvents(TimeSpan.FromMilliseconds(20));
        }
    }

    private static void PumpEvents(TimeSpan duration)
    {
        DispatcherFrame frame = new();
        DispatcherTimer timer = new(DispatcherPriority.Background)
        {
            Interval = duration,
        };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed record MemorySample(long Host, long Browser, int BrowserProcessCount, long Total);
}
