using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Infinium.DesktopHost;

public enum DesktopRuntimeAvailability
{
    Available,
    Missing,
    Outdated,
}

public sealed record DesktopRuntimeStatus(DesktopRuntimeAvailability Availability, string? Version, string InertReason);

public static class DesktopRuntimePolicy
{
    public const string ApplicationOrigin = "https://app.infinium.invalid";
    public const string ApplicationHost = "app.infinium.invalid";
    public const int MinimumRuntimeMajor = 151;
    public const string MinimumRuntimeVersion = "151.0.4129.50";

    private static readonly string[] PrivilegedEnvironmentVariables =
    [
        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
        "WEBVIEW2_BROWSER_EXECUTABLE_FOLDER",
        "WEBVIEW2_USER_DATA_FOLDER",
        "WEBVIEW2_RELEASE_CHANNEL_PREFERENCE",
    ];

    public static void EnsureSafeBrowserEnvironment()
    {
#if !DEBUG
        string? inherited = PrivilegedEnvironmentVariables.FirstOrDefault(
            name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));
        if (inherited is not null)
        {
            throw new InvalidOperationException($"The release desktop host refuses inherited WebView2 override '{inherited}'.");
        }
        if (HasPrivilegedPolicyOverride())
        {
            throw new InvalidOperationException("The release desktop host refuses inherited WebView2 browser-argument policy.");
        }
#endif
    }

    public static DesktopRuntimeStatus InspectRuntime()
    {
        try
        {
            string version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return ClassifyRuntimeVersion(version);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return new(DesktopRuntimeAvailability.Missing, null, "The Evergreen WebView2 runtime is not installed.");
        }
    }

    internal static DesktopRuntimeStatus ClassifyRuntimeVersion(string? version)
        => string.IsNullOrWhiteSpace(version)
            ? new(DesktopRuntimeAvailability.Missing, null, "The Evergreen WebView2 runtime is not installed.")
            : CoreWebView2Environment.CompareBrowserVersions(version, MinimumRuntimeVersion) < 0
            ? new(DesktopRuntimeAvailability.Outdated, version, $"WebView2 runtime {MinimumRuntimeVersion} or newer is required.")
            : new(DesktopRuntimeAvailability.Available, version, "The local Evergreen WebView2 runtime is available.");

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

    internal static bool HasPrivilegedPolicyOverride(
        Func<RegistryHive, RegistryView, IReadOnlyDictionary<string, object?>>? readPolicy = null)
    {
        readPolicy ??= ReadPolicyValues;
        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                IReadOnlyDictionary<string, object?> values = readPolicy(hive, view);
                foreach ((string name, object? value) in values)
                {
                    if ((name == "*" || name.Equals("Infinium.DesktopHost.exe", StringComparison.OrdinalIgnoreCase))
                        && !string.IsNullOrWhiteSpace(value?.ToString()))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Dictionary<string, object?> ReadPolicyValues(RegistryHive hive, RegistryView view)
    {
        const string policyPath = @"SOFTWARE\Policies\Microsoft\Edge\WebView2\AdditionalBrowserArguments";
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
        using RegistryKey? policy = baseKey.OpenSubKey(policyPath, writable: false);
        return policy is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : policy.GetValueNames().ToDictionary(name => name, policy.GetValue, StringComparer.OrdinalIgnoreCase);
    }
}
