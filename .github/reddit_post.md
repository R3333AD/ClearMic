## Reddit — r/selfhosted

**Title:** ClearMic — open-source AI noise suppression for your mic, real-time on Windows

**Text:**

I built ClearMic, a free/open-source tool that removes background noise from your microphone in real-time using AI (DeepFilterNet3 via ONNX Runtime).

**How it works:**
Microphone → WASAPI capture → DeepFilterNet3 ONNX → VB-Cable → Discord/Teams/Zoom

**Performance:**
- 2.1× real-time processing
- ~30 dB noise reduction (fans, keyboard, traffic disappear)
- 0 frame drops in 30-minute stress test
- .NET 8 WPF app, tray icon with live reduction display

**Why selfhosted?** No cloud, no subscriptions. Runs 100% local on your Windows machine. VB-Cable required for routing (free).

https://github.com/R3333AD/ClearMic

Looking for feedback — what's missing? AEC? APO driver? Better UI?

---

## Reddit — r/windows

**Title:** ClearMic — free AI noise suppression for your Windows mic (open source)

**Text:**

Tired of your keyboard, fan, or room noise ruining your Discord/Teams calls?

I made ClearMic — an open-source Windows app that removes background noise from your mic in real-time using AI. It sits between your microphone and your apps:

Your mic → ClearMic → VB-Cable → Discord/Teams/Zoom

- No cloud, 100% local
- 2.1× real-time, -30 dB noise reduction
- Tray icon, device selection UI
- .NET 8, WPF

Just install VB-Cable (free), point Discord to "CABLE Input", launch ClearMic, and toggle on.

GitHub: https://github.com/R3333AD/ClearMic

First release, looking for beta testers and feedback!

---

## Hacker News

**Title:** ClearMic – Open-source real-time AI mic noise suppression for Windows

**Text:**

I built ClearMic, an open-source Windows app that removes microphone background noise in real-time with a DeepFilterNet3 ONNX model.

Stack: C# (.NET 8), NAudio (WASAPI), ONNX Runtime, WPF.

Why not another noise suppression tool? Most are either cloud-based, paid, or closed-source. ClearMic is MIT, runs fully local, and has measurable performance:

- 2.1× real-time factor
- −30 dB noise attenuation
- 0 timing violations in 30s stress test (~3000 frames)
- Device selection + settings persistence

VB-Cable is currently required for output routing (free); a custom APO driver to remove that dependency is planned.

Would love feedback, especially from Windows audio devs. GitHub: https://github.com/R3333AD/ClearMic
