using System.Windows;

namespace ClearMic.App;

public partial class App : Application
{
    private PipelineService? _pipeline;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _pipeline = new PipelineService();
        _trayIcon = new TrayIcon(_pipeline);
        _trayIcon.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _pipeline?.Dispose();
        base.OnExit(e);
    }
}
