using ClearMic.Core;
using NAudio.Wave;

sealed class SpeechTest
{
    const int SampleRate = 48000;
    const float TargetSnrDb = 5f; // moderate noise

    public static void Run()
    {
        // Check multiple locations for the speech file
        var possiblePaths = new[]
        {
            "speech_clean.wav",
            Path.Combine("ClearMic.Test", "speech_clean.wav"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "speech_clean.wav"),
        };
        var inputPath = possiblePaths.FirstOrDefault(File.Exists);

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"  File not found: {inputPath}");
            Console.WriteLine("  Download from: https://samplelib.com/wav/sample-speech-1m.wav");
            return;
        }

        // Load speech WAV (might be stereo or different sample rate)
        float[] speech;
        int speechRate;
        using (var reader = new AudioFileReader(inputPath))
        {
            speechRate = reader.WaveFormat.SampleRate;
            var buffer = new float[reader.Length / 4]; // float = 4 bytes
            int read = reader.Read(buffer, 0, buffer.Length);
            // Convert to mono if stereo
            if (reader.WaveFormat.Channels == 2)
            {
                var mono = new float[read / 2];
                for (int i = 0; i < mono.Length; i++)
                    mono[i] = (buffer[i * 2] + buffer[i * 2 + 1]) * 0.5f;
                speech = mono;
            }
            else
            {
                speech = buffer[..read];
            }
        }

        // Resample to 48kHz if needed
        if (speechRate != SampleRate)
        {
            Console.WriteLine($"  Resampling from {speechRate}Hz to {SampleRate}Hz...");
            speech = Resample(speech, speechRate, SampleRate);
        }

        // Normalize peak to 0.5
        float peak = speech.Max(s => MathF.Abs(s));
        if (peak > 0.01f)
        {
            float scale = 0.5f / peak;
            for (int i = 0; i < speech.Length; i++)
                speech[i] *= scale;
        }

        // Trim to first ~10s for faster test
        int trimSamples = SampleRate * 10;
        if (speech.Length > trimSamples)
            speech = speech[..trimSamples];

        int totalSamples = speech.Length;
        double durationSec = (double)totalSamples / SampleRate;

        Console.WriteLine("=== Speech Enhancement Test ===");
        Console.WriteLine($"  Source: sample-speech-1m.wav");
        Console.WriteLine($"  Duration: {durationSec:F1}s ({totalSamples} samples @ {SampleRate}Hz)");
        Console.WriteLine($"  Target SNR: {TargetSnrDb:F0} dB");
        Console.WriteLine();

        // Generate noise
        var rng = new Random(42);
        var noise = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            noise[i] = (float)(rng.NextDouble() * 2 - 1);

        // Scale noise to achieve target SNR (relative to active speech regions)
        float speechRms = MathF.Sqrt(speech.Average(s => s * s));
        float noiseRms = MathF.Sqrt(noise.Average(s => s * s));
        float noiseScale = (speechRms / (noiseRms + 1e-10f)) / MathF.Pow(10f, TargetSnrDb / 20f);
        for (int i = 0; i < totalSamples; i++)
            noise[i] *= noiseScale;

        // Mix
        var noisy = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            noisy[i] = speech[i] + noise[i];

        // Measure pre-filter segmental SNR
        float preSegSnr = MeasureSegmentalSnr(speech, noisy, SampleRate);
        Console.WriteLine($"  Pre-filter SegSNR:  {preSegSnr:F1} dB");

        // Process through filter
        var filter = new NoiseFilter { IsEnabled = true };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var enhanced = filter.Process(noisy);
        sw.Stop();

        // Measure post-filter segmental SNR
        float postSegSnr = MeasureSegmentalSnr(speech, enhanced, SampleRate);
        Console.WriteLine($"  Post-filter SegSNR: {postSegSnr:F1} dB");
        Console.WriteLine($"  SegSNR Improvement: {postSegSnr - preSegSnr:F1} dB");
        Console.WriteLine($"  Processing: {sw.ElapsedMilliseconds}ms ({durationSec / sw.Elapsed.TotalSeconds:F1}× RT)");

        // Save output WAVs
        var fmt = new WaveFormat(SampleRate, 16, 1);
        SaveWav("speech_noisy.wav", noisy, fmt);
        SaveWav("speech_enhanced.wav", enhanced, fmt);
        SaveWav("speech_original.wav", speech, fmt);

        Console.WriteLine();
        Console.WriteLine("  Saved: speech_original.wav, speech_noisy.wav, speech_enhanced.wav");

        // Resonance metric: noise floor reduction
        Console.WriteLine();
        Console.WriteLine("  Broadband noise floor:");
        float nfNoisy = ComputeNoiseFloorBroadband(noisy, SampleRate);
        float nfEnhanced = ComputeNoiseFloorBroadband(enhanced, SampleRate);
        float nfOrig = ComputeNoiseFloorBroadband(speech, SampleRate);
        Console.WriteLine($"    Original:  {20 * MathF.Log10(nfOrig + 1e-10f):F1} dBFS");
        Console.WriteLine($"    Noisy:     {20 * MathF.Log10(nfNoisy + 1e-10f):F1} dBFS");
        Console.WriteLine($"    Enhanced:  {20 * MathF.Log10(nfEnhanced + 1e-10f):F1} dBFS");
        Console.WriteLine($"    Reduction: {20 * MathF.Log10(nfNoisy / (nfEnhanced + 1e-10f)):F1} dB");

        // RMS comparison
        float rmsNoisy = MathF.Sqrt(noisy.Average(s => s * s));
        float rmsEnhanced = MathF.Sqrt(enhanced.Average(s => s * s));
        float rmsOrig = MathF.Sqrt(speech.Average(s => s * s));
        Console.WriteLine();
        Console.WriteLine("  RMS levels:");
        Console.WriteLine($"    Original:  {rmsOrig:F4}");
        Console.WriteLine($"    Noisy:     {rmsNoisy:F4}");
        Console.WriteLine($"    Enhanced:  {rmsEnhanced:F4}");

        filter.Dispose();
    }

    static float MeasureSegmentalSnr(float[] clean, float[] processed, int sampleRate)
    {
        int frameLen = sampleRate / 50; // 20ms
        int halfFrame = frameLen / 2;
        float totalSNR = 0;
        int frames = 0;

        for (int start = 0; start + frameLen <= clean.Length; start += halfFrame)
        {
            float sigPow = 0, noisePow = 1e-10f;
            int bestOffset = 0;
            float bestNoisePow = float.MaxValue;

            for (int offset = -3; offset <= 3; offset++)
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

            if (bestNoisePow >= float.MaxValue / 2) continue;

            for (int j = 0; j < frameLen; j++)
            {
                int idx = start + j + bestOffset;
                if (idx < 0 || idx >= processed.Length) break;
                sigPow += clean[start + j] * clean[start + j];
                float diff = processed[idx] - clean[start + j];
                noisePow += diff * diff;
            }

            float frameSnr = 10 * MathF.Log10(sigPow / (noisePow + 1e-10f));
            if (frameSnr > -15 && frameSnr < 50)
            {
                totalSNR += frameSnr;
                frames++;
            }
        }

        return frames > 0 ? totalSNR / frames : 0;
    }

    static float ComputeNoiseFloorBroadband(float[] samples, int sampleRate)
    {
        var rng = new Random(1);
        float totalPower = 0;
        int count = 0;
        for (int i = 0; i < 50; i++)
        {
            float testFreq = 100 + (float)rng.NextDouble() * (sampleRate / 2f - 200);
            float mag = ComputeGoertzel(samples, testFreq, sampleRate);
            totalPower += mag * mag;
            count++;
        }
        return MathF.Sqrt(totalPower / count);
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

    static float[] Resample(float[] input, int inRate, int outRate)
    {
        double ratio = (double)outRate / inRate;
        int outLen = (int)(input.Length * ratio);
        var output = new float[outLen];
        for (int i = 0; i < outLen; i++)
        {
            double srcPos = i / ratio;
            int srcIdx = (int)srcPos;
            double frac = srcPos - srcIdx;
            if (srcIdx + 1 < input.Length)
                output[i] = (float)(input[srcIdx] * (1 - frac) + input[srcIdx + 1] * frac);
            else
                output[i] = input[Math.Min(srcIdx, input.Length - 1)];
        }
        return output;
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
