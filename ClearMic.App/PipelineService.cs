using System.IO;
using System.Text.Json;
using ClearMic.Core;

namespace ClearMic.App;

public sealed class PipelineService : IDisposable
{
    private readonly AudioPipeline _pipeline;
    private bool _disposed;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClearMic", "settings.json");

    public event EventHandler<bool>? StateChanged;
    public event EventHandler<LevelData>? LevelChanged;
    public bool IsRunning => _pipeline.IsRunning;
    public int InputDeviceIndex => _pipeline.InputDeviceIndex;
    public int OutputDeviceIndex => _pipeline.OutputDeviceIndex;
    public bool AecEnabled
    {
        get => _pipeline.AecEnabled;
        set => _pipeline.AecEnabled = value;
    }

    public PipelineService()
    {
        _pipeline = new AudioPipeline();
        _pipeline.LevelChanged += OnLevelChanged;
        LoadSettings();
    }

    public void SetInputDevice(int index) => _pipeline.InputDeviceIndex = index;
    public void SetOutputDevice(int index) => _pipeline.OutputDeviceIndex = index;

    public void Toggle()
    {
        if (_pipeline.IsRunning)
            Stop();
        else
            Start();
    }

    public void Start()
    {
        _pipeline.Start();
        StateChanged?.Invoke(this, true);
    }

    public void Stop()
    {
        _pipeline.Stop();
        StateChanged?.Invoke(this, false);
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
        SaveSettings();
        _pipeline.Dispose();
    }

    private sealed class Settings
    {
        public int InputDevice { get; set; }
        public int OutputDevice { get; set; }
        public bool AecEnabled { get; set; }
    }
}
