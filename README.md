# ClearMic

Real-time AI microphone noise suppression for Windows.

Removes background noise from your mic (fans, keyboard, traffic) before it reaches your calls — Discord, Teams, Zoom, or any app using your default input device.

## How it works

ClearMic captures your microphone through WASAPI, filters noise with a DeepFilterNet3 ONNX model, and outputs clean audio to a virtual device.

```
Mic → [WASAPI Capture] → [DeepFilterNet3 ONNX] → [WASAPI Output] → Virtual Cable → Your App
```

## Prerequisites

- Windows 10/11 (x64)
- [VB-Cable Virtual Audio Cable](https://vb-audio.com/Cable/) (free)
- .NET 8 Runtime (auto-installed by the setup, or get it from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0))

## Quick Start

1. Install VB-Cable and set "CABLE Input" as your mic in Discord/Teams/Zoom
2. Install ClearMic
3. Launch ClearMic, select your real mic as input, "CABLE Output" as output
4. Toggle noise reduction on

## Building from source

```bash
dotnet restore
dotnet build -c Release
```

The compiled binary will be at `ClearMic.App/bin/Release/net8.0-windows/`.

## Roadmap

| Phase | Status |
|-------|--------|
| 1 — WASAPI pipeline + spectral gate | ✅ |
| 2 — DeepFilterNet3 ONNX integration | ✅ |
| 3 — Custom APO driver (remove VB-Cable dependency) | ⏳ |
| 4 — AEC, profiles, UI polish | ⏳ |

## License

MIT
