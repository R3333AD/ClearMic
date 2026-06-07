using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace ClearMic.Core;

public sealed class AudioCapture : IDisposable
{
    private WasapiCapture? _capture;
    private readonly MMDevice _device;

    public event EventHandler<byte[]>? AudioDataAvailable;
    public bool IsRecording { get; private set; }
    public WaveFormat WaveFormat { get; }
    public string DeviceName => _device.FriendlyName;
    public int DeviceIndex { get; }

    public AudioCapture(int deviceIndex = 0)
    {
        DeviceIndex = deviceIndex;
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        _device = deviceIndex < devices.Count ? devices[deviceIndex] : devices[0];
        WaveFormat = new WaveFormat(48000, 16, 1);
    }

    public void Start()
    {
        _capture = new WasapiCapture(_device, true, 20);
        _capture.WaveFormat = WaveFormat;
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();
        IsRecording = true;
    }

    public void Stop()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.StopRecording();
        }
        IsRecording = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
        AudioDataAvailable?.Invoke(this, buffer);
    }

    public void Dispose()
    {
        Stop();
        _capture?.Dispose();
        _device.Dispose();
    }

    public static string[] EnumerateDevices()
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .ToArray();
    }
}
