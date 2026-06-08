using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ClearMic.Core;

public sealed class ProfileManager
{
    private readonly string _filePath;
    private List<Profile> _profiles;
    private string _activeName;

    public event EventHandler<Profile>? ProfileSwitched;

    public IReadOnlyList<Profile> Profiles => _profiles.AsReadOnly();
    public Profile ActiveProfile => _profiles.First(p => p.Name == _activeName);
    public string ActiveName => _activeName;

    public ProfileManager(string directory)
    {
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "profiles.json");
        _profiles = [];
        _activeName = "Default";
        Load();
    }

    public void Apply(Profile profile, AudioPipeline pipeline)
    {
        pipeline.NoiseFilterEnabled = profile.NoiseFilter;
        pipeline.AecEnabled = profile.AecEnabled;
        pipeline.InputDeviceIndex = profile.InputDevice;
        pipeline.OutputDeviceIndex = profile.OutputDevice;
        _activeName = profile.Name;
        Save();
        ProfileSwitched?.Invoke(this, profile);
    }

    public void SwitchTo(string name, AudioPipeline pipeline)
    {
        var profile = _profiles.FirstOrDefault(p => p.Name == name);
        if (profile is not null)
            Apply(profile, pipeline);
    }

    public void Add(Profile profile, AudioPipeline? pipeline = null)
    {
        _profiles.Add(profile);
        if (pipeline is not null && profile.Name == _activeName)
            Apply(profile, pipeline);
        Save();
    }

    public void Remove(string name)
    {
        var profile = _profiles.FirstOrDefault(p => p.Name == name);
        if (profile is null || profile.IsDefault) return;
        _profiles.Remove(profile);
        if (_activeName == name)
        {
            var fallback = _profiles.First(p => p.IsDefault);
            _activeName = fallback.Name;
        }
        Save();
    }

    public Profile? MatchForegroundWindow()
    {
        var title = GetForegroundWindowTitle();
        if (string.IsNullOrEmpty(title)) return null;

        var process = GetForegroundProcessName();

        foreach (var p in _profiles)
        {
            if (p.AutoSwitchProcesses.Count == 0) continue;
            foreach (var keyword in p.AutoSwitchProcesses)
            {
                if (title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || process.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
        }
        return null;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<ProfileData>(json);
                if (data is not null)
                {
                    _profiles = data.Profiles;
                    _activeName = data.ActiveProfile;
                    if (!_profiles.Any(p => p.Name == _activeName))
                        _activeName = _profiles.FirstOrDefault()?.Name ?? "Default";
                    return;
                }
            }
        }
        catch { }

        _profiles = [new Profile { Name = "Default", IsDefault = true }];
        _activeName = "Default";
    }

    public void Save()
    {
        try
        {
            var data = new ProfileData
            {
                Profiles = _profiles,
                ActiveProfile = _activeName,
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    private static string GetForegroundWindowTitle()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            int len = GetWindowTextLengthW(hwnd) + 1;
            var sb = new System.Text.StringBuilder(len);
            GetWindowTextW(hwnd, sb, len);
            return sb.ToString();
        }
        catch { return ""; }
    }

    private static string GetForegroundProcessName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            _ = GetWindowThreadProcessId(hwnd, out int pid);
            return Process.GetProcessById(pid).ProcessName;
        }
        catch { return ""; }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int pid);

    private sealed class ProfileData
    {
        public List<Profile> Profiles { get; set; } = [];
        public string ActiveProfile { get; set; } = "Default";
    }
}
