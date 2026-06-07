using ClearMic.Core;
using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace ClearMic.App;

public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _tray;
    private readonly MainWindow _window;
    private readonly PipelineService _pipeline;
    private Icon? _currentIcon;

    public TrayIcon(PipelineService pipeline)
    {
        _pipeline = pipeline;
        _window = new MainWindow(pipeline);
        _currentIcon = IconFactory.CreateRed();
        _tray = new TaskbarIcon
        {
            Icon = _currentIcon,
            ToolTipText = "ClearMic — Arrêté"
        };

        _pipeline.StateChanged += OnStateChanged;
        _pipeline.LevelChanged += OnLevelChanged;
        _tray.TrayMouseDoubleClick += (_, _) => ToggleWindow();

        var menu = new System.Windows.Controls.ContextMenu();
        var toggleItem = new System.Windows.Controls.MenuItem { Header = "Activer/Désactiver" };
        toggleItem.Click += (_, _) => _pipeline.Toggle();
        menu.Items.Add(toggleItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Paramètres" };
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Quitter" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        _tray.ContextMenu = menu;
    }

    public void Show()
    {
        _window.Show();
        _window.Hide();
    }

    private void ToggleWindow()
    {
        if (_window.IsVisible)
            _window.Hide();
        else
            _window.Show();
    }

    private void OnStateChanged(object? sender, bool running)
    {
        UpdateIcon(running ? IconFactory.CreateGreen() : IconFactory.CreateRed());
        _tray.ToolTipText = running ? "ClearMic — Actif" : "ClearMic — Arrêté";
    }

    private void OnLevelChanged(object? sender, LevelData level)
    {
        var db = level.AttenuationDb;
        var newIcon = IconFactory.CreateAttenuationIcon(db);
        UpdateIcon(newIcon);
        _tray.ToolTipText = $"ClearMic — {db:+0.0;-0.0} dB de réduction";
    }

    private void UpdateIcon(Icon newIcon)
    {
        if (_currentIcon is not null)
        {
            var old = _currentIcon;
            _currentIcon = newIcon;
            _tray.Icon = newIcon;
            IconFactory.FreeIcon(old);
        }
        else
        {
            _currentIcon = newIcon;
            _tray.Icon = newIcon;
        }
    }

    private void ShowSettings()
    {
        var settings = new SettingsWindow(_pipeline);
        settings.Owner = _window;
        settings.ShowDialog();
    }

    public void Dispose()
    {
        _pipeline.StateChanged -= OnStateChanged;
        _pipeline.LevelChanged -= OnLevelChanged;
        _tray.Dispose();
        _window.Close();
        if (_currentIcon is not null)
            IconFactory.FreeIcon(_currentIcon);
    }
}
