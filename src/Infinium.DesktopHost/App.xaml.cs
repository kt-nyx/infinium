using System.Windows;

namespace Infinium.DesktopHost;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (ProcessElevationPolicy.IsElevated())
        {
            MessageBox.Show("Infinium Desktop Diagnostics refuses to run elevated.", "Infinium", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(70);
            return;
        }

        try
        {
            DesktopHostStartup.Start(
                e.Args,
                DesktopRuntimePolicy.EnsureSafeBrowserEnvironment,
                DesktopLaunchOptions.Parse,
                options =>
                {
                    MainWindow = new MainWindow(options);
                    MainWindow.Show();
                });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(exception.Message, "Infinium", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(64);
            return;
        }

    }
}
