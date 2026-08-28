using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Infinium.DesktopHost;

public partial class MainWindow : Window, IAsyncDisposable
{
    private readonly DesktopLaunchOptions options;
    private readonly string assetRoot;
    private readonly Action ensureSafeBrowserEnvironment;
    private readonly Func<CoreWebView2EnvironmentOptions, DesktopRuntimeStatus> inspectRuntime;
    private CoreWebView2Environment? environment;
    private CoreWebView2? configuredCore;
    private RendererBridgeSession? bridge;
    private IReadOnlySet<string> allowedAssets = new HashSet<string>();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, byte> deniedTopNavigations = new();
    private int recreating;
    private int browserPreflightCheckCount;
    private long bridgeGeneration;

    internal RendererBridgeMetrics BridgeMetrics { get; } = new();
    internal DesktopSecurityEventMetrics SecurityEvents { get; } = new();
    internal string? BridgeSessionId => bridge?.SessionId;
    internal int BrowserPreflightCheckCount => Volatile.Read(ref browserPreflightCheckCount);

    public MainWindow(DesktopLaunchOptions options)
        : this(options, DesktopRuntimePolicy.EnsureSafeBrowserEnvironment, DesktopRuntimePolicy.InspectRuntime)
    {
    }

    internal MainWindow(
        DesktopLaunchOptions options,
        Action ensureSafeBrowserEnvironment,
        Func<CoreWebView2EnvironmentOptions, DesktopRuntimeStatus> inspectRuntime)
    {
        this.options = options;
        this.ensureSafeBrowserEnvironment = ensureSafeBrowserEnvironment;
        this.inspectRuntime = inspectRuntime;
        InitializeComponent();
        assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets");
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeBrowserAsync().ConfigureAwait(true);
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            RunBrowserPreflight();
            CoreWebView2EnvironmentOptions environmentOptions = DesktopRuntimePolicy.CreateEnvironmentOptions();
            DesktopRuntimeStatus runtime = inspectRuntime(environmentOptions);
            if (runtime.Availability != DesktopRuntimeAvailability.Available)
            {
                ShowInertRuntimeNotice(runtime.InertReason);
                return;
            }

            allowedAssets = AssetManifestVerifier.Verify(assetRoot);
            string userData = options.IsQualification
                ? Path.Combine(options.ProductRoot, "desktop-webview-user-data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Infinium", "DesktopHost", "WebView2");
            if (environment is null)
            {
                RunBrowserPreflight();
                environment = await CoreWebView2Environment.CreateAsync(null, userData, environmentOptions).ConfigureAwait(true);
                environment.BrowserProcessExited += OnBrowserProcessExited;
            }
            DesktopRuntimePolicy.EnsureCreatedEnvironmentIsSupported(environment);
            CoreWebView2ControllerOptions controllerOptions = environment.CreateCoreWebView2ControllerOptions();
            controllerOptions.IsInPrivateModeEnabled = true;
            await Browser.EnsureCoreWebView2Async(environment, controllerOptions).ConfigureAwait(true);
            ConfigureCore(Browser.CoreWebView2);
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                DesktopRuntimePolicy.ApplicationHost,
                assetRoot,
                CoreWebView2HostResourceAccessKind.Deny);
            Browser.Source = new Uri($"{DesktopRuntimePolicy.ApplicationOrigin}/index.html", UriKind.Absolute);
            RuntimeNotice.Visibility = Visibility.Collapsed;
            Browser.Visibility = Visibility.Visible;
        }
        catch (DesktopBrowserPreflightException exception)
        {
            ShowInertRuntimeNotice(exception.Message);
        }
        catch (Exception exception) when (exception is WebView2RuntimeNotFoundException or InvalidDataException or UnauthorizedAccessException or IOException)
        {
            ShowInertRuntimeNotice("The protected local diagnostic renderer could not be initialized.");
        }
    }

    internal Task InitializeBrowserForTestAsync() => InitializeBrowserAsync();

    private void RunBrowserPreflight()
    {
        Interlocked.Increment(ref browserPreflightCheckCount);
        ensureSafeBrowserEnvironment();
    }

    private void ShowInertRuntimeNotice(string text)
    {
        RuntimeNotice.Text = text;
        RuntimeNotice.Visibility = Visibility.Visible;
        Browser.Visibility = Visibility.Collapsed;
    }

    private void ConfigureCore(CoreWebView2 core)
    {
        DetachCore();
        configuredCore = core;
        CoreWebView2Settings settings = core.Settings;
        settings.AreHostObjectsAllowed = false;
        settings.IsScriptEnabled = true;
        settings.IsWebMessageEnabled = true;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsZoomControlEnabled = true;
        settings.IsPinchZoomEnabled = true;
        settings.IsNonClientRegionSupportEnabled = false;
        Browser.AllowExternalDrop = false;
        Browser.ZoomFactor = 1.0;

        core.NavigationStarting += OnNavigationStarting;
        core.FrameNavigationStarting += OnFrameNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.DownloadStarting += OnDownloadStarting;
        core.PermissionRequested += OnPermissionRequested;
        core.BasicAuthenticationRequested += OnBasicAuthenticationRequested;
        core.ClientCertificateRequested += OnClientCertificateRequested;
        core.ServerCertificateErrorDetected += OnServerCertificateErrorDetected;
        core.ProcessFailed += OnProcessFailed;
        core.NavigationCompleted += OnNavigationCompleted;
        core.WebMessageReceived += OnWebMessageReceived;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
    }

    private void DetachCore()
    {
        if (configuredCore is null)
        {
            return;
        }

        CoreWebView2 prior = configuredCore;
        configuredCore = null;
        try
        {
            prior.NavigationStarting -= OnNavigationStarting;
            prior.FrameNavigationStarting -= OnFrameNavigationStarting;
            prior.NewWindowRequested -= OnNewWindowRequested;
            prior.DownloadStarting -= OnDownloadStarting;
            prior.PermissionRequested -= OnPermissionRequested;
            prior.BasicAuthenticationRequested -= OnBasicAuthenticationRequested;
            prior.ClientCertificateRequested -= OnClientCertificateRequested;
            prior.ServerCertificateErrorDetected -= OnServerCertificateErrorDetected;
            prior.ProcessFailed -= OnProcessFailed;
            prior.NavigationCompleted -= OnNavigationCompleted;
            prior.WebMessageReceived -= OnWebMessageReceived;
            prior.WebResourceRequested -= OnWebResourceRequested;
        }
        catch (InvalidOperationException)
        {
            // A renderer/browser crash may dispose the COM controller before
            // WPF observes ProcessFailed. Its handlers are already unreachable.
        }
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args) { SecurityEvents.FrameNavigationDenied(); args.Cancel = true; }
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs args) { SecurityEvents.NewWindowDenied(); args.Handled = true; }
    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs args) { SecurityEvents.DownloadDenied(); args.Cancel = true; args.Handled = true; }
    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs args) { SecurityEvents.PermissionDenied(); args.State = CoreWebView2PermissionState.Deny; args.SavesInProfile = false; args.Handled = true; }
    private static void OnBasicAuthenticationRequested(object? sender, CoreWebView2BasicAuthenticationRequestedEventArgs args) => args.Cancel = true;
    private static void OnClientCertificateRequested(object? sender, CoreWebView2ClientCertificateRequestedEventArgs args) { args.Cancel = true; args.Handled = true; }
    private static void OnServerCertificateErrorDetected(object? sender, CoreWebView2ServerCertificateErrorDetectedEventArgs args) => args.Action = CoreWebView2ServerCertificateErrorAction.Cancel;

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!DesktopRuntimePolicy.IsAllowedTopLevelNavigation(args.Uri))
        {
            SecurityEvents.TopNavigationDenied();
            deniedTopNavigations.TryAdd(args.NavigationId, 0);
            args.Cancel = true;
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out Uri? uri)
            || !DesktopRuntimePolicy.IsExactApplicationOrigin(args.Request.Uri)
            || !allowedAssets.Contains(uri.AbsolutePath)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            SecurityEvents.ResourceDenied();
            args.Response = environment!.CreateWebResourceResponse(null, 403, "Forbidden", "Content-Type: text/plain\r\nCache-Control: no-store");
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (deniedTopNavigations.TryRemove(args.NavigationId, out _))
        {
            return;
        }
        if (!args.IsSuccess || !DesktopRuntimePolicy.IsAllowedTopLevelNavigation(Browser.Source?.AbsoluteUri))
        {
            await RecreateBrowserAsync().ConfigureAwait(true);
            return;
        }
        try
        {
            RunBrowserPreflight();
        }
        catch (DesktopBrowserPreflightException exception)
        {
            Interlocked.Increment(ref bridgeGeneration);
            if (bridge is not null)
            {
                await bridge.DisposeAsync().ConfigureAwait(true);
                bridge = null;
            }
            ShowInertRuntimeNotice(exception.Message);
            return;
        }
        await RotateBridgeAsync().ConfigureAwait(true);
        await bridge!.SendSessionInitializationAsync().ConfigureAwait(true);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            string serialized = args.TryGetWebMessageAsString();
            await bridge!.HandleRendererMessageAsync(args.Source, serialized).ConfigureAwait(true);
        }
        catch
        {
            await RecreateBrowserAsync().ConfigureAwait(true);
        }
    }

    private async void OnCancelTransportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (bridge is null || !await bridge.GrantCancellationGestureAsync().ConfigureAwait(true))
            {
                RuntimeNotice.Text = "No cancellable renderer operation is currently active.";
                RuntimeNotice.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            RuntimeNotice.Text = "The cancellation gesture could not be delivered; the active operation was not changed.";
            RuntimeNotice.Visibility = Visibility.Visible;
        }
    }

    private async Task RotateBridgeAsync()
    {
        long generation = Interlocked.Increment(ref bridgeGeneration);
        if (bridge is not null)
        {
            await bridge.DisposeAsync().ConfigureAwait(true);
        }

        bridge = new RendererBridgeSession(
            new DesktopApplicationClient(options.ProductRoot),
            serialized => Dispatcher.InvokeAsync(() =>
            {
                if (generation == Volatile.Read(ref bridgeGeneration))
                {
                    Browser.CoreWebView2.PostWebMessageAsString(serialized);
                }
            }).Task,
            () => generation == Volatile.Read(ref bridgeGeneration)
                ? Dispatcher.InvokeAsync(RecreateBrowserAsync).Task.Unwrap()
                : Task.CompletedTask,
            BridgeMetrics);
    }

    private async void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs args)
        => await RecreateBrowserAsync().ConfigureAwait(true);

    private async void OnBrowserProcessExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs args)
        => await Dispatcher.InvokeAsync(RecreateBrowserAsync).Task.Unwrap().ConfigureAwait(true);

    private async Task RecreateBrowserAsync()
    {
        if (Interlocked.Exchange(ref recreating, 1) != 0)
        {
            return;
        }

        try
        {
            Interlocked.Increment(ref bridgeGeneration);
            if (bridge is not null)
            {
                await bridge.DisposeAsync().ConfigureAwait(true);
            }

            bridge = null;
            RuntimeNotice.Text = "The renderer is restarting and will resynchronize from the local application service.";
            RuntimeNotice.Visibility = Visibility.Visible;
            Browser.Visibility = Visibility.Collapsed;
            WebView2 previous = Browser;
            DetachCore();
            previous.Visibility = Visibility.Collapsed;
            RootGrid.Children.Remove(previous);
            Browser = new WebView2
            {
                Visibility = Visibility.Collapsed,
                Focusable = true,
                AllowExternalDrop = false,
            };
            Grid.SetRow(Browser, 1);
            System.Windows.Automation.AutomationProperties.SetName(Browser, "Infinium diagnostic application");
            RootGrid.Children.Add(Browser);
            previous.Dispose();
            await InitializeBrowserAsync().ConfigureAwait(true);
        }
        finally { Volatile.Write(ref recreating, 0); }
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        await DisposeAsync().ConfigureAwait(true);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        Interlocked.Increment(ref bridgeGeneration);
        DetachCore();
        if (environment is not null)
        {
            environment.BrowserProcessExited -= OnBrowserProcessExited;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || Browser.CoreWebView2 is null)
        {
            return;
        }
        double next = e.Key switch
        {
            Key.Add or Key.OemPlus => Browser.ZoomFactor + 0.1,
            Key.Subtract or Key.OemMinus => Browser.ZoomFactor - 0.1,
            Key.D0 or Key.NumPad0 => 1.0,
            _ => Browser.ZoomFactor,
        };
        next = DesktopRuntimePolicy.NormalizeZoom(next);
        if (Math.Abs(next - Browser.ZoomFactor) > double.Epsilon)
        {
            Browser.ZoomFactor = next;
            e.Handled = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (bridge is not null)
        {
            await bridge.DisposeAsync().ConfigureAwait(true);
            bridge = null;
        }
        DetachCore();
        if (environment is not null)
        {
            environment.BrowserProcessExited -= OnBrowserProcessExited;
            environment = null;
        }
        Browser.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal sealed class DesktopSecurityEventMetrics
{
    private int topNavigationDenied;
    private int frameNavigationDenied;
    private int resourceDenied;
    private int newWindowDenied;
    private int downloadDenied;
    private int permissionDenied;

    internal int TopNavigationDeniedCount => Volatile.Read(ref topNavigationDenied);
    internal int FrameNavigationDeniedCount => Volatile.Read(ref frameNavigationDenied);
    internal int ResourceDeniedCount => Volatile.Read(ref resourceDenied);
    internal int NewWindowDeniedCount => Volatile.Read(ref newWindowDenied);
    internal int DownloadDeniedCount => Volatile.Read(ref downloadDenied);
    internal int PermissionDeniedCount => Volatile.Read(ref permissionDenied);

    internal void TopNavigationDenied() => Interlocked.Increment(ref topNavigationDenied);
    internal void FrameNavigationDenied() => Interlocked.Increment(ref frameNavigationDenied);
    internal void ResourceDenied() => Interlocked.Increment(ref resourceDenied);
    internal void NewWindowDenied() => Interlocked.Increment(ref newWindowDenied);
    internal void DownloadDenied() => Interlocked.Increment(ref downloadDenied);
    internal void PermissionDenied() => Interlocked.Increment(ref permissionDenied);
}
