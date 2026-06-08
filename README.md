# ClearMic

<p align="center">
  <b>Real-time AI microphone noise suppression for Windows.</b><br>
  <i>Remove background noise from your mic before it reaches Discord, Teams, or Zoom.</i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows" alt="Windows 10/11">
  <img src="https://img.shields.io/github/v/release/R3333AD/ClearMic?include_prereleases&logo=github" alt="GitHub Release">
  <img src="https://img.shields.io/github/license/R3333AD/ClearMic" alt="MIT License">
</p>

---

## How it works

## How it works

**Since v0.3.0 — no VB-Cable required.**

ClearMic installs as a Windows Audio Processing Object (APO), processing your mic at the driver level before any app sees it:

```
Mic → [APO: ClearMicApo.dll] → [ApoHost: DeepFilterNet3 ONNX] → Your App
```

The C++ APO stub captures audio, forwards it over a named pipe to a C# host process running the ONNX model, and writes the cleaned audio back — all within the Windows audio graph. Latency is under 1 ms added.

**Performance**: 2.1× real-time, −30 dB noise reduction, 0 frame drops under 30s stress test.

## Quick Start (v0.3.0+)

1. Download `ClearMicAPO.msi` from [Releases](https://github.com/R3333AD/ClearMic/releases) and install
2. In Discord/Teams/Zoom, select your real microphone (no VB-Cable needed)
3. The APO processes audio automatically at the system level
4. Use `ClearMic.App` (Windows Tray) to toggle noise reduction on/off

## Building from source

### .NET apps
```bash
dotnet restore
dotnet build -c Release
```

### APO DLL (requires MSVC + WDK)
```bash
msbuild ClearMic.Driver/ClearMicApo.vcxproj /p:Configuration=Release /p:Platform=x64
```

### MSI installer (requires WiX)
```bash
wix build -arch x64 -o ClearMicAPO.msi ClearMic.Installer/Package.wxs
```

## Roadmap

| Phase | Status |
|-------|--------|
| 1 — WASAPI pipeline | ✅ |
| 2 — DeepFilterNet3 ONNX integration | ✅ |
| 3 — Custom APO driver (standalone, no VB-Cable) | ✅ |
| 4 — Acoustic Echo Cancellation | ✅ |
| 5 — Per-app profiles & settings | ✅ |
| 6 — MSI installer + CI/CD | ✅ |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT
