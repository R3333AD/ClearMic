using System.Diagnostics;
using System.IO;

namespace ClearMic.App;

public sealed class ApoHostLauncher : IDisposable
{
    private Process? _process;
    private readonly string _exePath;

    public bool IsRunning => _process is { HasExited: false };

    public ApoHostLauncher()
    {
        var baseDir = AppContext.BaseDirectory;
        _exePath = Path.Combine(baseDir, "ClearMic.ApoHost.exe");
        if (!File.Exists(_exePath))
        {
            var dir = Path.GetDirectoryName(baseDir);
            _exePath = Path.Combine(dir ?? ".", "ClearMic.ApoHost",
                "bin", "Release", "net8.0-windows", "ClearMic.ApoHost.exe");
        }
    }

    public void Start()
    {
        if (IsRunning) return;
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        _process.Exited += (_, _) => _process = null;
        _process.Start();
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(2000);
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose() => Stop();
}
