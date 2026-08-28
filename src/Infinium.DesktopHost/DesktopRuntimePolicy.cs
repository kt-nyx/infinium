using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Infinium.DesktopHost;

public enum DesktopRuntimeAvailability
{
    Available,
    Missing,
    Outdated,
    Unsupported,
}

public sealed record DesktopRuntimeStatus(DesktopRuntimeAvailability Availability, string? Version, string InertReason);

internal sealed class DesktopBrowserPreflightException : InvalidOperationException
{
    internal DesktopBrowserPreflightException(string message)
        : base(message)
    {
    }

}

internal sealed record DesktopBrowserPolicyIdentities(string? ApplicationUserModelId, string ExecutableFileName);

public static class DesktopRuntimePolicy
{
    public const string ApplicationOrigin = "https://app.infinium.invalid";
    public const string ApplicationHost = "app.infinium.invalid";
    public const int MinimumRuntimeMajor = 151;
    public const string MinimumRuntimeVersion = "151.0.4129.50";
    internal const string ProductionExecutableFileName = "Infinium.DesktopHost.exe";

    internal static readonly string[] PrivilegedEnvironmentVariables =
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

    internal static readonly DesktopBrowserPolicyFamily[] PrivilegedPolicyFamilies =
    [
        new("BrowserExecutableFolder", SupportsWildcard: true),
        new("UserDataFolder", SupportsWildcard: true),
        new("AdditionalBrowserArguments", SupportsWildcard: true),
        new("ReleaseChannelPreference", SupportsWildcard: true),
        new("ChannelSearchKind", SupportsWildcard: true),
        new("ReleaseChannels", SupportsWildcard: true),
        new("DowngradeVersion", SupportsWildcard: false),
    ];

    private const string RefusedConfigurationText = "The desktop renderer cannot start because inherited browser configuration is present.";
    private const string UnverifiableConfigurationText = "The desktop renderer cannot verify local browser policy and will not start.";

    public static void EnsureSafeBrowserEnvironment()
    {
#if !DEBUG
        EnsureSafeBrowserEnvironment(
            Environment.GetEnvironmentVariable,
            ReadPolicyValues,
            ReadCurrentPolicyIdentities);
#endif
    }

    internal static void EnsureSafeBrowserEnvironment(
        Func<string, string?> readEnvironment,
        Func<RegistryHive, RegistryView, string, IReadOnlyDictionary<string, object?>> readPolicy,
        Func<DesktopBrowserPolicyIdentities> readIdentities)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(readPolicy);
        ArgumentNullException.ThrowIfNull(readIdentities);

        foreach (string variable in PrivilegedEnvironmentVariables)
        {
            string? value;
            try
            {
                value = readEnvironment(variable);
            }
            catch (Exception)
            {
                throw new DesktopBrowserPreflightException(UnverifiableConfigurationText);
            }

            if (value is not null && value.Length != 0)
            {
                throw new DesktopBrowserPreflightException(RefusedConfigurationText);
            }
        }

        DesktopBrowserPolicyIdentities policyIdentities;
        try
        {
            policyIdentities = readIdentities();
        }
        catch (Exception)
        {
            throw new DesktopBrowserPreflightException(UnverifiableConfigurationText);
        }

        List<string> exactIdentities = BuildExactPolicyIdentities(policyIdentities);
        foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (DesktopBrowserPolicyFamily family in PrivilegedPolicyFamilies)
                {
                    IReadOnlyDictionary<string, object?> values;
                    try
                    {
                        values = readPolicy(hive, view, family.Name);
                    }
                    catch (Exception)
                    {
                        throw new DesktopBrowserPreflightException(UnverifiableConfigurationText);
                    }

                    foreach ((string name, object? value) in values)
                    {
                        bool identityMatches = exactIdentities.Contains(name, StringComparer.OrdinalIgnoreCase)
                            || (family.SupportsWildcard && StringComparer.Ordinal.Equals(name, "*"));
                        string? configuredValue = value?.ToString();
                        if (identityMatches && configuredValue is not null && configuredValue.Length != 0)
                        {
                            throw new DesktopBrowserPreflightException(RefusedConfigurationText);
                        }
                    }
                }
            }
        }
    }

    public static CoreWebView2EnvironmentOptions CreateEnvironmentOptions()
        => new()
        {
            AdditionalBrowserArguments = string.Empty,
            AllowSingleSignOnUsingOSPrimaryAccount = false,
            AreBrowserExtensionsEnabled = false,
            ExclusiveUserDataFolderAccess = true,
            IsCustomCrashReportingEnabled = false,
            ReleaseChannels = CoreWebView2ReleaseChannels.Stable,
            ChannelSearchKind = CoreWebView2ChannelSearchKind.MostStable,
        };

    public static DesktopRuntimeStatus InspectRuntime(CoreWebView2EnvironmentOptions environmentOptions)
    {
        ArgumentNullException.ThrowIfNull(environmentOptions);
        try
        {
            string version = CoreWebView2Environment.GetAvailableBrowserVersionString(null, environmentOptions);
            return ClassifyRuntimeVersion(version);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return new(DesktopRuntimeAvailability.Missing, null, "The stable Evergreen WebView2 runtime is not installed.");
        }
    }

    public static DesktopRuntimeStatus InspectRuntime()
        => InspectRuntime(CreateEnvironmentOptions());

    internal static DesktopRuntimeStatus ClassifyRuntimeVersion(string? version)
        => string.IsNullOrWhiteSpace(version)
            ? new(DesktopRuntimeAvailability.Missing, null, "The stable Evergreen WebView2 runtime is not installed.")
            : !IsStableRuntimeVersion(version)
            ? new(DesktopRuntimeAvailability.Unsupported, version, "A stable Evergreen WebView2 runtime is required.")
            : CoreWebView2Environment.CompareBrowserVersions(version, MinimumRuntimeVersion) < 0
            ? new(DesktopRuntimeAvailability.Outdated, version, $"WebView2 runtime {MinimumRuntimeVersion} or newer is required.")
            : new(DesktopRuntimeAvailability.Available, version, "The local stable Evergreen WebView2 runtime is available.");

    internal static bool IsStableRuntimeVersion(string? version)
        => !string.IsNullOrWhiteSpace(version)
            && Version.TryParse(version, out _);

    internal static void EnsureCreatedEnvironmentIsSupported(CoreWebView2Environment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        DesktopRuntimeStatus status = ClassifyRuntimeVersion(environment.BrowserVersionString);
        if (status.Availability != DesktopRuntimeAvailability.Available)
        {
            throw new InvalidDataException("The created desktop renderer runtime is not an allowed stable Evergreen version.");
        }
    }

    internal static double NormalizeZoom(double value) => Math.Clamp(Math.Round(value, 1), 0.5, 2.0);

    public static bool IsExactApplicationOrigin(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals(ApplicationHost, StringComparison.Ordinal)
            && uri.IsDefaultPort
            && string.IsNullOrEmpty(uri.UserInfo);

    public static bool IsAllowedTopLevelNavigation(string? value)
        => IsExactApplicationOrigin(value)
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && uri.AbsolutePath == "/index.html"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);

    private static List<string> BuildExactPolicyIdentities(DesktopBrowserPolicyIdentities identities)
    {
        List<string> values = [];
        AddIdentity(values, identities.ApplicationUserModelId);
        AddIdentity(values, ProductionExecutableFileName);
        AddIdentity(values, identities.ExecutableFileName);
        return values;
    }

    private static void AddIdentity(List<string> identities, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !identities.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            identities.Add(value);
        }
    }

    private static DesktopBrowserPolicyIdentities ReadCurrentPolicyIdentities()
    {
        string? executableFileName = Path.GetFileName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(executableFileName))
        {
            throw new InvalidOperationException("The current executable identity is unavailable.");
        }

        return new(ReadCurrentApplicationUserModelId(), executableFileName);
    }

    private static string? ReadCurrentApplicationUserModelId()
    {
        uint length = 0;
        int result = GetCurrentApplicationUserModelId(ref length, null);
        if (result == AppModelErrorNoApplication)
        {
            return null;
        }
        if (result != ErrorInsufficientBuffer || length == 0)
        {
            throw new Win32Exception(result);
        }

        char[] value = new char[checked((int)length)];
        result = GetCurrentApplicationUserModelId(ref length, value);
        if (result == AppModelErrorNoApplication)
        {
            return null;
        }
        if (result != 0)
        {
            throw new Win32Exception(result);
        }
        int terminator = Array.IndexOf(value, '\0');
        return new string(value, 0, terminator < 0 ? value.Length : terminator);
    }

    private static Dictionary<string, object?> ReadPolicyValues(RegistryHive hive, RegistryView view, string family)
    {
        string policyPath = $@"SOFTWARE\Policies\Microsoft\Edge\WebView2\{family}";
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? policy = baseKey.OpenSubKey(policyPath, writable: false);
        return policy is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : policy.GetValueNames().ToDictionary(name => name, policy.GetValue, StringComparer.OrdinalIgnoreCase);
    }

    private const int ErrorInsufficientBuffer = 122;
    private const int AppModelErrorNoApplication = 15703;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentApplicationUserModelId(ref uint applicationUserModelIdLength, [Out] char[]? applicationUserModelId);
}

internal sealed record DesktopBrowserPolicyFamily(string Name, bool SupportsWildcard);
