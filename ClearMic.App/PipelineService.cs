using System.IO;
using System.Text.Json;
using ClearMic.Core;

namespace ClearMic.App;

public sealed class PipelineService : IDisposable
{
    private readonly AudioPipeline _pipeline;
    private readonly ProfileManager _profiles;
    private readonly ApoHostLauncher _apoHost;
    private readonly System.Timers.Timer _autoSwitchTimer;
    private bool _disposed;
    private bool _apoMode;
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClearMic");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public event EventHandler<bool>? StateChanged;
    public event EventHandler<LevelData>? LevelChanged;
    public event EventHandler<string>? ProfileChanged;

    public bool IsRunning => _apoMode ? _apoHost.IsRunning : _pipeline.IsRunning;
    public int InputDeviceIndex => _pipeline.InputDeviceIndex;
    public int OutputDeviceIndex => _pipeline.OutputDeviceIndex;
    public bool AecEnabled
    {
        get => _pipeline.AecEnabled;
        set => _pipeline.AecEnabled = value;
    }
    public bool ApoMode
    {
        get => _apoMode;
        set
        {
            if (_apoMode == value) return;
            _apoMode = value;
            if (value) { _pipeline.Stop(); _apoHost.Start(); }
            else { _apoHost.Stop(); }
        }
    }
    public ProfileManager Profiles => _profiles;
    public AudioPipeline Pipeline => _pipeline;

    public PipelineService()
    {
        _pipeline = new AudioPipeline();
        _profiles = new ProfileManager(SettingsDir);
        _apoHost = new ApoHostLauncher();
        _pipeline.LevelChanged += OnLevelChanged;

        // Restore active profile
        var active = _profiles.ActiveProfile;
        _profiles.Apply(active, _pipeline);

        // Auto-switch timer (every 2s)
        _autoSwitchTimer = new System.Timers.Timer(2000);
        _autoSwitchTimer.Elapsed += (_, _) => CheckAutoSwitch();
        _autoSwitchTimer.AutoReset = true;
        _autoSwitchTimer.Start();

        LoadSettings();
    }

    public void SetInputDevice(int index) => _pipeline.InputDeviceIndex = index;
    public void SetOutputDevice(int index) => _pipeline.OutputDeviceIndex = index;

    public void Toggle()
    {
        if (IsRunning)
            Stop();
        else
            Start();
    }

    public void Start()
    {
        if (_apoMode)
            _apoHost.Start();
        else
            _pipeline.Start();
        StateChanged?.Invoke(this, true);
    }

    public void Stop()
    {
        if (_apoMode)
            _apoHost.Stop();
        else
            _pipeline.Stop();
        StateChanged?.Invoke(this, false);
    }

    public void SwitchProfile(string name)
    {
        _profiles.SwitchTo(name, _pipeline);
        SaveSettings();
        ProfileChanged?.Invoke(this, name);
    }

    private void CheckAutoSwitch()
    {
        if (_apoMode || !_pipeline.IsRunning) return;
        var match = _profiles.MatchForegroundWindow();
        if (match is not null && match.Name != _profiles.ActiveName)
            SwitchProfile(match.Name);
    }

    public void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new Settings
            {
                InputDevice = _pipeline.InputDeviceIndex,
                OutputDevice = _pipeline.OutputDeviceIndex,
                AecEnabled = _pipeline.AecEnabled,
                ApoMode = _apoMode,
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s is not null)
                {
                    _pipeline.InputDeviceIndex = s.InputDevice;
                    _pipeline.OutputDeviceIndex = s.OutputDevice;
                    _pipeline.AecEnabled = s.AecEnabled;
                    _apoMode = s.ApoMode;
                }
            }
        }
        catch { }
    }

    private void OnLevelChanged(object? sender, LevelData level)
    {
        LevelChanged?.Invoke(this, level);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _autoSwitchTimer.Stop();
        _autoSwitchTimer.Dispose();
        _apoHost.Dispose();
        _profiles.Save();
        SaveSettings();
        _pipeline.Dispose();
    }

    private sealed class Settings
    {
        public int InputDevice { get; set; }
        public int OutputDevice { get; set; }
        public bool AecEnabled { get; set; }
        public bool ApoMode { get; set; }
    }
}
