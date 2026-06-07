using ClearMic.Core;
using System.Windows;
using System.Windows.Media;

namespace ClearMic.App;

public partial class MainWindow : Window
{
    private readonly PipelineService _pipeline;
    private DateTime _lastLevelUpdate = DateTime.MinValue;

    public MainWindow(PipelineService pipeline)
    {
        InitializeComponent();
        _pipeline = pipeline;
        _pipeline.StateChanged += OnPipelineStateChanged;
        _pipeline.LevelChanged += OnLevelChanged;
    }

    private void OnToggleChecked(object sender, RoutedEventArgs e)
    {
        _pipeline.Start();
        ToggleButton.Content = "Actif";
    }

    private void OnToggleUnchecked(object sender, RoutedEventArgs e)
    {
        _pipeline.Stop();
        ToggleButton.Content = "Démarrer";
        InputLevelBar.Value = 0;
        InputLevelText.Text = "— dB";
        OutputLevelBar.Value = 0;
        OutputLevelText.Text = "— dB";
        ReductionText.Text = "— dB réduction";
    }

    private void OnPipelineStateChanged(object? sender, bool running)
    {
        Dispatcher.Invoke(() =>
        {
            ToggleButton.IsChecked = running;
            ToggleButton.Content = running ? "Actif" : "Démarrer";
            StatusText.Text = running
                ? "Suppression de bruit active"
                : "Prêt — VB-Cable requis";
        });
    }

    private void OnLevelChanged(object? sender, LevelData level)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastLevelUpdate).TotalMilliseconds < 50)
            return;
        _lastLevelUpdate = now;

        Dispatcher.Invoke(() =>
        {
            double inDb = level.InputRms > 1e-10 ? 20 * Math.Log10(level.InputRms) : -90;
            double outDb = level.OutputRms > 1e-10 ? 20 * Math.Log10(level.OutputRms) : -90;
            double inNorm = Math.Clamp((inDb + 60) / 60.0, 0, 1);
            double outNorm = Math.Clamp((outDb + 60) / 60.0, 0, 1);

            InputLevelBar.Value = inNorm;
            InputLevelText.Text = $"{inDb,5:F1} dB";
            OutputLevelBar.Value = outNorm;
            OutputLevelText.Text = $"{outDb,5:F1} dB";

            var att = level.AttenuationDb;
            ReductionText.Text = $"{att:+0.0;-0.0} dB réduction";
            ReductionText.Foreground = att switch
            {
                > -10 => new SolidColorBrush(Color.FromRgb(0xf9, 0xe2, 0xaf)),
                > -30 => new SolidColorBrush(Color.FromRgb(0xa6, 0xe3, 0xa1)),
                _     => new SolidColorBrush(Color.FromRgb(0x89, 0xb4, 0xfa)),
            };
        });
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(_pipeline);
        settings.Owner = this;
        settings.ShowDialog();
    }
}
