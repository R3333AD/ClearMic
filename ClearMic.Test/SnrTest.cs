using ClearMic.Core;
using NAudio.Wave;

sealed class SnrTest
{
    const int SampleRate = 48000;
    const double Duration = 5.0;

    // Speech-like vowel signal: fundamental + formants
    static readonly (float Freq, float Amp)[] Harmonics = [
        (120f, 1.0f),   // F0 (fundamental)
        (240f, 0.6f),   // H2
        (480f, 0.4f),   // H3
        (720f, 0.8f),   // H4 (F1 region)
        (1080f, 0.5f),  // H5
        (1440f, 0.6f),  // H6 (F2 region)
        (2160f, 0.3f),  // H7
        (2880f, 0.4f),  // H8 (F3 region)
        (3600f, 0.2f),  // H9
        (4320f, 0.15f), // H10
    ];

    public static void Run()
    {
        Console.WriteLine("=== Objective SNR Test (Speech-like signal) ===");
        Console.WriteLine($"  Harmonics: {Harmonics.Length} (F0={Harmonics[0].Freq}Hz)");
        Console.WriteLine($"  Target SNR: 0 dB");
        Console.WriteLine($"  Duration: {Duration:F0}s");
        Console.WriteLine();

        int totalSamples = (int)(SampleRate * Duration);

        // Generate speech-like clean signal
        var clean = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / SampleRate;
            double sample = 0;
            foreach (var (freq, amp) in Harmonics)
                sample += amp * Math.Sin(2 * Math.PI * freq * t);
            clean[i] = (float)(0.3 * sample);
        }

        // Generate noise
        var rng = new Random(42);
        var noise = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            noise[i] = (float)(rng.NextDouble() * 2 - 1) * 0.5f;

        // Scale noise to achieve 0 dB SNR
        float rmsClean = MathF.Sqrt(clean.Average(s => s * s));
        float rmsNoise = MathF.Sqrt(noise.Average(s => s * s));
        float noiseScale = rmsClean / (rmsNoise + 1e-10f);
        for (int i = 0; i < totalSamples; i++)
            noise[i] *= noiseScale;

        // Mix
        var noisy = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            noisy[i] = clean[i] + noise[i];

        // Measure pre-filter segmental SNR (accounts for phase shift)
        float preSegSnr = MeasureSegmentalSnr(clean, noisy);
        Console.WriteLine($"  Pre-filter SegSNR: {preSegSnr:F1} dB");

        // Process through filter
        var filter = new NoiseFilter { IsEnabled = true };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var enhanced = filter.Process(noisy);
        sw.Stop();

        // Measure post-filter segmental SNR
        float postSegSnr = MeasureSegmentalSnr(clean, enhanced);
        float segSnrImprovement = postSegSnr - preSegSnr;

        Console.WriteLine($"  Post-filter SegSNR: {postSegSnr:F1} dB");
        Console.WriteLine($"  SegSNR Improvement: {segSnrImprovement:F1} dB");

        // Full-band SNR (absolute difference, less meaningful)
        float preSnr = MeasureSnr(clean, noisy);
        float postSnr = MeasureSnr(clean, enhanced);
        Console.WriteLine();
        Console.WriteLine($"  Full-band SNR (before): {preSnr:F1} dB");
        Console.WriteLine($"  Full-band SNR (after):  {postSnr:F1} dB");
        Console.WriteLine($"  Processing: {sw.ElapsedMilliseconds}ms ({Duration / sw.Elapsed.TotalSeconds:F1}× RT)");

        // Save WAVs
        var fmt = new WaveFormat(SampleRate, 16, 1);
        SaveWav("clean.wav", clean, fmt);
        SaveWav("noisy.wav", noisy, fmt);
        SaveWav("enhanced.wav", enhanced, fmt);
        Console.WriteLine();
        Console.WriteLine("  Saved: clean.wav, noisy.wav, enhanced.wav");

        // Per-frequency noise floor reduction
        Console.WriteLine();
        Console.WriteLine("  Per-frequency noise floor:");
        Console.WriteLine($"    {"Freq",6} {"Clean",8} {"Noisy",8} {"Enhanced",10} {"Reduction",10}");
        foreach (var (freq, _) in Harmonics)
        {
            float cMag = ComputeGoertzel(clean, freq, SampleRate);
            float nMag = ComputeGoertzel(noisy, freq, SampleRate);
            float eMag = ComputeGoertzel(enhanced, freq, SampleRate);
            float nFloor = ComputeNoiseFloor(noisy, freq, SampleRate);
            float eFloor = ComputeNoiseFloor(enhanced, freq, SampleRate);
            float reduction = 20 * MathF.Log10((nFloor + 1e-10f) / (eFloor + 1e-10f));
            float sigDropDb = 20 * MathF.Log10(eMag / (cMag + 1e-10f));
            string sigPres = sigDropDb switch
            {
                > -3f => "ok",
                > -6f => "fair",
                _     => "weak"
            };
            Console.WriteLine(
                $"    {freq,5:F0}Hz {20 * MathF.Log10(cMag):+0.0;-0.0} dBFS " +
                $"{20 * MathF.Log10(nMag):+0.0;-0.0} dBFS " +
                $"{20 * MathF.Log10(eMag):+0.0;-0.0} dBFS " +
                $"{reduction,+5:F1} dB  sig={sigPres}");
        }

        filter.Dispose();
    }

    // Segmental SNR: align by energy in 20ms frames, allows phase shift
    static float MeasureSegmentalSnr(float[] clean, float[] processed)
    {
        int frameLen = SampleRate / 50; // 20ms
        int halfFrame = frameLen / 2;
        float totalSNR = 0;
        int frames = 0;

        for (int start = 0; start + frameLen <= clean.Length; start += halfFrame)
        {
            float sigPow = 0, noisePow = 1e-10f;
            int bestOffset = 0;
            float bestNoisePow = float.MaxValue;

            // Find best alignment ±2 samples (accounts for phase shift)
            for (int offset = -2; offset <= 2; offset++)
            {
                float np = 0;
                for (int j = 0; j < frameLen; j++)
                {
                    int idx = start + j + offset;
                    if (idx < 0 || idx >= processed.Length) { np = float.MaxValue; break; }
                    float diff = processed[idx] - clean[start + j];
                    np += diff * diff;
                }
                if (np < bestNoisePow) { bestNoisePow = np; bestOffset = offset; }
            }

            for (int j = 0; j < frameLen; j++)
            {
                int idx = start + j + bestOffset;
                if (idx < 0 || idx >= processed.Length) break;
                sigPow += clean[start + j] * clean[start + j];
                float diff = processed[idx] - clean[start + j];
                noisePow += diff * diff;
            }

            float frameSnr = 10 * MathF.Log10(sigPow / noisePow);
            if (frameSnr > -20) // clamp very bad frames
            {
                totalSNR += frameSnr;
                frames++;
            }
        }

        return frames > 0 ? totalSNR / frames : 0;
    }

    static float MeasureSnr(float[] clean, float[] processed)
    {
        float signalPower = 0, noisePower = 0;
        for (int i = 0; i < clean.Length; i++)
        {
            signalPower += clean[i] * clean[i];
            float diff = processed[i] - clean[i];
            noisePower += diff * diff;
        }
        signalPower /= clean.Length;
        noisePower /= clean.Length;
        return 10 * MathF.Log10(signalPower / (noisePower + 1e-10f));
    }

    static float ComputeGoertzel(float[] samples, float targetFreq, int sampleRate)
    {
        int N = samples.Length;
        float omega = 2 * MathF.PI * targetFreq / sampleRate;
        float coeff = 2 * MathF.Cos(omega);
        float s1 = 0, s2 = 0;
        for (int i = 0; i < N; i++)
        {
            float s0 = samples[i] + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }
        float power = s1 * s1 + s2 * s2 - coeff * s1 * s2;
        return MathF.Sqrt(power / N);
    }

    static float ComputeNoiseFloor(float[] samples, float excludeFreq, int sampleRate)
    {
        var rng = new Random(1);
        float totalPower = 0;
        int count = 0;
        for (int i = 0; i < 100; i++)
        {
            float testFreq = 100 + (float)rng.NextDouble() * (sampleRate / 2f - 200);
            if (MathF.Abs(testFreq - excludeFreq) < 80) continue;
            float mag = ComputeGoertzel(samples, testFreq, sampleRate);
            totalPower += mag * mag;
            count++;
        }
        return MathF.Sqrt(totalPower / count);
    }

    static void SaveWav(string path, float[] samples, WaveFormat format)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var s = (short)(samples[i] * 32768);
            bytes[i * 2] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        using var writer = new WaveFileWriter(path, format);
        writer.Write(bytes, 0, bytes.Length);
    }
}
