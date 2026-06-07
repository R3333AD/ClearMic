using ClearMic.Core;
using NAudio.Wave;
using NAudio.CoreAudioApi;

bool stressTest = args.Length > 0 && args[0] == "--stress";
bool snrTest = args.Length > 0 && args[0] == "--snr";
bool speechTest = args.Length > 0 && args[0] == "--speech";

if (stressTest)
{
    await RunStressTest();
}
else if (snrTest)
{
    SnrTest.Run();
}
else if (speechTest)
{
    SpeechTest.Run();
}
else
{
    await RunCaptureTest();
}

// ──────────────────────────────────────────────────────────
// Capture + Filter test (original)
// ──────────────────────────────────────────────────────────
async Task RunCaptureTest()
{
    Console.WriteLine("=== ClearMic Capture + Filter Test ===");
    Console.WriteLine();

    Console.WriteLine("Input devices:");
    var inputs = AudioCapture.EnumerateDevices();
    for (int i = 0; i < inputs.Length; i++)
        Console.WriteLine($"  [{i}] {inputs[i]}");

    Console.WriteLine();

    Console.WriteLine("Recording 5 seconds from microphone...");
    var capture = new AudioCapture();
    var rawBuffer = new List<byte>();

    capture.AudioDataAvailable += (_, data) => { rawBuffer.AddRange(data); };

    capture.Start();
    await Task.Delay(5000);
    capture.Stop();

    Console.WriteLine($"  Captured {rawBuffer.Count} bytes ({rawBuffer.Count / 2} samples)");

    var waveFormat = new WaveFormat(48000, 16, 1);
    using (var writer = new WaveFileWriter("raw.wav", waveFormat))
        writer.Write(rawBuffer.ToArray(), 0, rawBuffer.Count);
    Console.WriteLine("  Saved raw.wav");

    Console.WriteLine("Processing through ONNX noise filter...");
    var filter = new NoiseFilter { IsEnabled = true };
    var rawSamples = new float[rawBuffer.Count / 2];
    for (int i = 0; i < rawSamples.Length; i++)
        rawSamples[i] = BitConverter.ToInt16(rawBuffer.ToArray(), i * 2) / 32768f;

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var cleanSamples = filter.Process(rawSamples);
    sw.Stop();

    Console.WriteLine($"  Processed {cleanSamples.Length} samples in {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"  Real-time factor: {5000.0 / sw.ElapsedMilliseconds:F2}x");

    var cleanBytes = new byte[cleanSamples.Length * 2];
    for (int i = 0; i < cleanSamples.Length; i++)
    {
        var sample = (short)(cleanSamples[i] * 32768);
        cleanBytes[i * 2] = (byte)(sample & 0xFF);
        cleanBytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
    }
    using (var writer = new WaveFileWriter("filtered.wav", waveFormat))
        writer.Write(cleanBytes, 0, cleanBytes.Length);
    Console.WriteLine("  Saved filtered.wav");

    float rmsRaw = (float)Math.Sqrt(rawSamples.Average(s => s * s));
    float rmsClean = (float)Math.Sqrt(cleanSamples.Average(s => s * s));
    Console.WriteLine();
    Console.WriteLine($"  Raw RMS:      {rmsRaw:F6}");
    Console.WriteLine($"  Filtered RMS: {rmsClean:F6}");
    Console.WriteLine($"  Attenuation:  {20 * Math.Log10(rmsClean / (rmsRaw + 1e-10f)):F1} dB");
    Console.WriteLine();

    capture.Dispose();
    filter.Dispose();
}

// ──────────────────────────────────────────────────────────
// Real-time pipeline stress test
// ──────────────────────────────────────────────────────────
async Task RunStressTest()
{
    Console.WriteLine("=== Real-Time Pipeline Stress Test (30s) ===");
    Console.WriteLine();

    var capture = new AudioCapture();
    var filter = new NoiseFilter();
    int callbackCount = 0;
    long totalInputSamples = 0;
    long totalOutputSamples = 0;
    double maxElapsedMs = 0;
    var sw = new System.Diagnostics.Stopwatch();
    var timingViolations = 0; // callbacks taking > 18ms (90% of 20ms interval)

    capture.AudioDataAvailable += (_, data) =>
    {
        sw.Restart();

        // Simulate what AudioPipeline does
        var sampleCount = data.Length / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(data, i * 2) / 32768f;

        var clean = filter.Process(samples);

        sw.Stop();
        var elapsed = sw.Elapsed.TotalMilliseconds;

        Interlocked.Increment(ref callbackCount);
        Interlocked.Add(ref totalInputSamples, sampleCount);
        Interlocked.Add(ref totalOutputSamples, clean.Length);

        if (elapsed > maxElapsedMs)
            maxElapsedMs = elapsed;

        if (elapsed > 18)
            Interlocked.Increment(ref timingViolations);
    };

    Console.WriteLine("Starting 30-second pipeline stress test...");
    Console.WriteLine("  Capture: 20ms WASAPI buffers, 48kHz mono");
    Console.WriteLine("  Filter: DeepFilterNet3 ONNX (denoiser_model.onnx)");
    Console.WriteLine("  Threshold: 18ms per callback (90% of interval)");
    Console.WriteLine();

    capture.Start();

    for (int i = 0; i < 30; i++)
    {
        await Task.Delay(1000);
        Console.Write($"\r  Elapsed: {i + 1}s  Callbacks: {callbackCount,4}  "
            + $"Max processing: {maxElapsedMs,5:F1}ms  Violations: {timingViolations,2}  "
            + $"Input: {totalInputSamples / 48000,3}s  Output: {totalOutputSamples / 48000,3}s");
    }

    capture.Stop();
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine("=== Results ===");
    Console.WriteLine($"  Total callbacks:    {callbackCount}");
    Console.WriteLine($"  Input samples:      {totalInputSamples} ({totalInputSamples / 48000.0:F1}s)");
    Console.WriteLine($"  Output samples:     {totalOutputSamples} ({totalOutputSamples / 48000.0:F1}s)");
    Console.WriteLine($"  Avg processing:     {(callbackCount > 0 ? totalInputSamples / (double)callbackCount / 48000 * 1000 : 0):F1}ms per callback");
    Console.WriteLine($"  Max processing:     {maxElapsedMs:F1}ms");
    Console.WriteLine($"  Timing violations:  {timingViolations} / {callbackCount}");
    Console.WriteLine($"  Overrun ratio:      {(callbackCount > 0 ? (double)timingViolations / callbackCount * 100 : 0):F1}%");
    Console.WriteLine();
    Console.WriteLine(totalOutputSamples == totalInputSamples
        ? "  ✓ Sample count matches — no frame loss"
        : $"  ⚠ Sample count mismatch — delta: {totalInputSamples - totalOutputSamples}");
    Console.WriteLine(maxElapsedMs < 20
        ? "  ✓ Max callback under 20ms — no crackling risk"
        : "  ⚠ Some callbacks exceeded 20ms — potential crackling");

    capture.Dispose();
    filter.Dispose();
}
