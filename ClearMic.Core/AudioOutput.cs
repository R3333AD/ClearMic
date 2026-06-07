using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace ClearMic.Core;

public sealed class AudioOutput : IDisposable
{
    private IWavePlayer? _output;
    private BufferedWaveProvider? _buffer;
    private int _deviceIndex;

    public bool IsPlaying { get; private set; }
    public int DeviceIndex => _deviceIndex;

    public void Start(WaveFormat format, int deviceIndex = -1)
    {
        _deviceIndex = deviceIndex;
        _buffer = new BufferedWaveProvider(format)
        {
            BufferDuration = TimeSpan.FromMilliseconds(300),
            DiscardOnBufferOverflow = true
        };

        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        MMDevice device;

        if (deviceIndex >= 0 && deviceIndex < devices.Count)
        {
            device = devices[deviceIndex];
        }
        else
        {
            device = devices.FirstOrDefault(d => d.FriendlyName.Contains("CABLE Input"))
                ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        }

        _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 20);
        _output.Init(_buffer);
        _output.Play();
        IsPlaying = true;
    }

    public void Write(byte[] data)
    {
        _buffer?.AddSamples(data, 0, data.Length);
    }

    public void Stop()
    {
        _output?.Stop();
        IsPlaying = false;
    }

    public void Dispose()
    {
        Stop();
        _output?.Dispose();
    }

    public static string[] EnumerateDevices()
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .ToArray();
    }
}
