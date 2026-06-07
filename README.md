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

```
Mic → [WASAPI Capture] → [DeepFilterNet3 ONNX] → [WASAPI Output] → VB-Cable → Your App
```

ClearMic captures your microphone through WASAPI, filters noise in real-time with a DeepFilterNet3 model (ONNX Runtime), and outputs clean audio to VB-Cable.

**Performance**: 2.1× real-time, −30 dB noise reduction, 0 frame drops under 30s stress test.

## Prerequisites

- Windows 10/11 (x64)
- [VB-Cable Virtual Audio Cable](https://vb-audio.com/Cable/) (free)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Quick Start

1. Install VB-Cable
2. In Discord/Teams/Zoom, set mic to "CABLE Input"
3. Launch ClearMic, select your real mic → "CABLE Output"
4. Toggle on — your background noise is suppressed

## Building from source

```bash
dotnet restore
dotnet build -c Release
```

Binary at `ClearMic.App/bin/Release/net8.0-windows/ClearMic.App.exe`.

## Roadmap

| Phase | Status |
|-------|--------|
| 1 — WASAPI pipeline | ✅ |
| 2 — DeepFilterNet3 ONNX | ✅ |
| 3 — Custom APO driver (no VB-Cable needed) | ⏳ |
| 4 — AEC, profiles, UI polish | ⏳ |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT
