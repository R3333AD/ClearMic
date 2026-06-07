# Contributing

Thanks for checking out ClearMic! Here's how to help.

## Quick start

```bash
git clone https://github.com/R3333AD/ClearMic
cd ClearMic
dotnet restore
dotnet build -c Release
```

Make sure you have:
- .NET 8 SDK
- Windows 10/11 (WASAPI + WPF)
- VB-Cable (for output routing)

## Project structure

```
ClearMic.Core/        # Audio pipeline, noise filter, WASAPI capture/output
ClearMic.App/         # WPF UI, tray icon, settings
ClearMic.Test/        # Capture test, stress test, SNR tests
ClearMic.Driver/      # (future) APO driver
installer/            # Inno Setup script
```

## Sending a PR

1. Fork the repo
2. Create a branch: `git checkout -b feat/your-feature`
3. Make your changes
4. Run `dotnet build -c Release` — must compile with 0 errors
5. Run the tests: `dotnet run --project ClearMic.Test` (capture test) or `dotnet run --project ClearMic.Test -- --stress` (stress test)
6. Commit with a clear message
7. Open a PR against `main`

## Code conventions

- Follow existing patterns (file-scoped namespaces, nullable enabled)
- No comments in production code unless the logic is genuinely non-obvious
- Use `float` for audio samples, `short` for byte conversion

## Reporting issues

Use the issue templates. For bugs, include:
- OS version
- VB-Cable version
- ClearMic version
- Audio device model
- Steps to reproduce

## License

By contributing, you agree that your contributions will be licensed under MIT.
