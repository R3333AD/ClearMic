namespace ClearMic.Core;

public sealed class LevelData
{
    public float InputRms { get; init; }
    public float OutputRms { get; init; }
    public float AttenuationDb { get; init; }
}

public sealed class AudioPipeline : IDisposable
{
    private const int FrameSize = 480;

    private AudioCapture? _capture;
    private AudioOutput? _output;
    private AudioLoopbackCapture? _loopback;
    private readonly NoiseFilter _filter;
    private readonly AcousticEchoCanceler _aec;
    private readonly List<float> _ringBuffer = new(capacity: FrameSize * 4);

    private bool _isRunning;
    private int _inputDeviceIndex;
    private int _outputDeviceIndex = -1;
    private bool _aecEnabled;

    public event EventHandler<float[]>? AudioProcessed;
    public event EventHandler<LevelData>? LevelChanged;

    public bool IsRunning => _isRunning;
    public bool AecEnabled
    {
        get => _aecEnabled;
        set
        {
            if (_aecEnabled == value) return;
            _aecEnabled = value;
            _aec.IsEnabled = value;
            if (_isRunning)
            {
                Stop();
                Start();
            }
        }
    }
    public bool NoiseFilterEnabled
    {
        get => _filter.IsEnabled;
        set => _filter.IsEnabled = value;
    }
    public string InputDeviceName => _capture?.DeviceName ?? "";
    public int InputDeviceIndex
    {
        get => _inputDeviceIndex;
        set
        {
            if (_inputDeviceIndex == value) return;
            _inputDeviceIndex = value;
            if (_isRunning) { Stop(); Start(); }
        }
    }
    public int OutputDeviceIndex
    {
        get => _outputDeviceIndex;
        set
        {
            if (_outputDeviceIndex == value) return;
            _outputDeviceIndex = value;
            if (_isRunning) { Stop(); Start(); }
        }
    }

    public AudioPipeline()
    {
        _filter = new NoiseFilter();
        _aec = new AcousticEchoCanceler();
    }

    public void Start()
    {
        if (_isRunning) return;
        _capture = new AudioCapture(_inputDeviceIndex);
        _output = new AudioOutput();
        _capture.AudioDataAvailable += OnAudioData;
        _output.Start(_capture.WaveFormat, _outputDeviceIndex);
        _capture.Start();

        if (_aecEnabled)
        {
            _aec.Reset();
            _loopback = new AudioLoopbackCapture();
            _loopback.FrameAvailable += OnLoopbackFrame;
            _loopback.Start();
        }

        _isRunning = true;
    }

    public void Stop()
    {
        if (!_isRunning) return;
        if (_capture is not null)
            _capture.AudioDataAvailable -= OnAudioData;
        _capture?.Stop();
        _output?.Stop();

        if (_loopback is not null)
        {
            _loopback.FrameAvailable -= OnLoopbackFrame;
            _loopback.Stop();
            _loopback.Dispose();
            _loopback = null;
        }

        _isRunning = false;
    }

    private void OnLoopbackFrame(object? sender, float[] frame)
    {
        _aec.AddReference(frame);
    }

    private void OnAudioData(object? sender, byte[] rawData)
    {
        var samples = BytesToFloats(rawData);
        _ringBuffer.AddRange(samples);

        var outputFrames = new List<float>(FrameSize * 4);
        double inputRms = 0;
        double outputRms = 0;

        while (_ringBuffer.Count >= FrameSize)
        {
            var frame = _ringBuffer.GetRange(0, FrameSize);
            _ringBuffer.RemoveRange(0, FrameSize);

            inputRms = Math.Sqrt(frame.Average(s => s * s));

            var processed = _aecEnabled
                ? _aec.Process(frame.ToArray())
                : frame.ToArray();

            var clean = _filter.Process(processed);
            outputRms = Math.Sqrt(clean.Average(s => s * s));

            outputFrames.AddRange(clean);
        }

        if (outputFrames.Count > 0)
        {
            var outputArray = outputFrames.ToArray();
            AudioProcessed?.Invoke(this, outputArray);

            var level = new LevelData
            {
                InputRms = (float)inputRms,
                OutputRms = (float)outputRms,
                AttenuationDb = outputRms > 1e-10 && inputRms > 1e-10
                    ? (float)(20 * Math.Log10(outputRms / inputRms))
                    : 0f
            };
            LevelChanged?.Invoke(this, level);

            var outBytes = FloatsToBytes(outputArray);
            _output?.Write(outBytes);
        }
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var samples = new float[bytes.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
        return samples;
    }

    private static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * 2];
        for (int i = 0; i < floats.Length; i++)
        {
            var sample = (short)(floats[i] * 32768);
            bytes[i * 2] = (byte)(sample & 0xFF);
            bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return bytes;
    }

    public void Dispose()
    {
        Stop();
        _loopback?.Dispose();
        _capture?.Dispose();
        _output?.Dispose();
        _filter.Dispose();
    }
}
