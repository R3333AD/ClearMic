# Changelog

## v0.3.0 (2026-06-08)

### Highlights
- **No VB-Cable required** — ClearMic now installs as a Windows Audio Processing Object (APO)
- MSI installer for one-click setup
- Full CI/CD pipeline

### Added
- Custom APO COM DLL (`ClearMicApo.dll`) — C++ COM object, named pipe IPC to C# host
- `ClearMic.ApoHost` — .NET 8 named pipe server running DeepFilterNet3 ONNX inference
- MSI installer (WiX v7) — registers APO, deploys ApoHost + model
- GitHub Actions workflow — builds APO DLL (MSVC+WDK) + MSI on every tag

### Changed
- Architecture: VB-Cable dependency removed for v0.3.0+
- Quick Start updated for MSI-based installation
- Roadmap updated to reflect all phases complete

## v0.2.0 (2026-06-07)

### Added
- Acoustic Echo Cancellation (NLMS, 4096 taps, double-talk detection)
- Per-app profiles with automatic foreground window switching
- WASAPI loopback capture for AEC reference signal
- Settings persistence for ApoMode toggle

### Fixed
- Pipeline audio discontinuities under heavy CPU load
- Device selection not persisting across restarts

## v0.1.0 (2026-06-07)

### Added
- WASAPI real-time audio capture/output pipeline
- DeepFilterNet3 ONNX noise suppression (2.1× real-time, −30 dB attenuation)
- 30-second stress-tested stability (0 timing violations)
- Device selection UI (input/output combo boxes, settings persistence)
- Tray icon with toggle and quick access to settings

### Prerequisites
- VB-Cable Virtual Audio Cable required
- .NET 8 Runtime
