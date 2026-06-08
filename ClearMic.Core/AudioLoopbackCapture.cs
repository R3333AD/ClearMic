using NAudio.Wave;

namespace ClearMic.Core;

public sealed class AudioLoopbackCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;

    public event EventHandler<float[]>? FrameAvailable;
    public bool IsCapturing { get; private set; }

    public void Start()
    {
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();
        IsCapturing = true;
    }

    public void Stop()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.StopRecording();
        }
        IsCapturing = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var format = _capture!.WaveFormat;
        int channels = format.Channels;
        int sampleCount = e.BytesRecorded / (format.BitsPerSample / 8) / channels;

        if (sampleCount == 0) return;

        // Convert bytes to mono float at native sample rate
        var buffer = new float[sampleCount];
        int byteOffset = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                short s = BitConverter.ToInt16(e.Buffer, byteOffset);
                sum += s / 32768f;
                byteOffset += 2;
            }
            buffer[i] = sum / channels;
        }

        // If not 48kHz, resample via linear interpolation
        if (format.SampleRate != 48000)
            buffer = Resample(buffer, format.SampleRate, 48000);

        FrameAvailable?.Invoke(this, buffer);
    }

    private static float[] Resample(float[] input, int inRate, int outRate)
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

    public void Dispose()
    {
        Stop();
        _capture?.Dispose();
    }
}
