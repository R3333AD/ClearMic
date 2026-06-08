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
    private readonly System.Windows.Controls.MenuItem _profileMenu;

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
        _pipeline.ProfileChanged += OnProfileChanged;
        _pipeline.Profiles.ProfileSwitched += OnProfileSwitched;
        _tray.TrayMouseDoubleClick += (_, _) => ToggleWindow();

        var menu = new System.Windows.Controls.ContextMenu();

        var toggleItem = new System.Windows.Controls.MenuItem { Header = "Activer/Désactiver" };
        toggleItem.Click += (_, _) => _pipeline.Toggle();
        menu.Items.Add(toggleItem);

        _profileMenu = new System.Windows.Controls.MenuItem { Header = "Profil" };
        RebuildProfileMenu();
        menu.Items.Add(_profileMenu);

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

    private void RebuildProfileMenu()
    {
        _profileMenu.Items.Clear();

        foreach (var profile in _pipeline.Profiles.Profiles)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = profile.Name,
                IsChecked = profile.Name == _pipeline.Profiles.ActiveName,
                IsCheckable = true,
            };
            var name = profile.Name; // capture for closure
            item.Click += (_, _) => _pipeline.SwitchProfile(name);
            _profileMenu.Items.Add(item);
        }

        _profileMenu.Items.Add(new System.Windows.Controls.Separator());

        var manageItem = new System.Windows.Controls.MenuItem { Header = "Gérer les profils..." };
        manageItem.Click += (_, _) => ShowProfileSettings();
        _profileMenu.Items.Add(manageItem);
    }

    private void OnProfileSwitched(object? sender, Profile profile)
    {
        RebuildProfileMenu();
    }

    private void OnProfileChanged(object? sender, string name)
    {
        RebuildProfileMenu();
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
        RebuildProfileMenu();
    }

    private void ShowProfileSettings()
    {
        var win = new ProfileSettingsWindow(_pipeline);
        win.Owner = _window;
        win.ShowDialog();
        RebuildProfileMenu();
    }

    public void Dispose()
    {
        _pipeline.StateChanged -= OnStateChanged;
        _pipeline.LevelChanged -= OnLevelChanged;
        _pipeline.ProfileChanged -= OnProfileChanged;
        _pipeline.Profiles.ProfileSwitched -= OnProfileSwitched;
        _tray.Dispose();
        _window.Close();
        if (_currentIcon is not null)
            IconFactory.FreeIcon(_currentIcon);
    }
}
