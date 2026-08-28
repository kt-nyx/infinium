namespace Infinium.DesktopHost;

internal static class DesktopHostStartup
{
    internal static void Start(
        IReadOnlyList<string> arguments,
        Action ensureSafeBrowserEnvironment,
        Func<IReadOnlyList<string>, DesktopLaunchOptions> parseLaunchOptions,
        Action<DesktopLaunchOptions> showMainWindow)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(ensureSafeBrowserEnvironment);
        ArgumentNullException.ThrowIfNull(parseLaunchOptions);
        ArgumentNullException.ThrowIfNull(showMainWindow);

        ensureSafeBrowserEnvironment();
        DesktopLaunchOptions options = parseLaunchOptions(arguments);
        showMainWindow(options);
    }
}
