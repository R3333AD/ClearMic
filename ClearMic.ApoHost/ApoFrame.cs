namespace ClearMic.ApoHost;

internal readonly struct ApoFrame
{
    public const int SamplesPerFrame = 480;
    public const int WireSize = 4 + 4 + SamplesPerFrame * 2;

    public uint FrameId { get; }
    public uint Flags { get; }
    public short[] Samples { get; }

    public ApoFrame(uint frameId, uint flags, short[] samples)
    {
        FrameId = frameId;
        Flags = flags;
        Samples = samples;
    }

    public byte[] ToWire()
    {
        var bytes = new byte[WireSize];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), FrameId);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), Flags);
        for (int i = 0; i < SamplesPerFrame; i++)
            BitConverter.TryWriteBytes(bytes.AsSpan(8 + i * 2, 2), Samples[i]);
        return bytes;
    }

    public static ApoFrame FromWire(byte[] bytes)
    {
        var frameId = BitConverter.ToUInt32(bytes, 0);
        var flags = BitConverter.ToUInt32(bytes, 4);
        var samples = new short[SamplesPerFrame];
        for (int i = 0; i < SamplesPerFrame; i++)
            samples[i] = BitConverter.ToInt16(bytes, 8 + i * 2);
        return new ApoFrame(frameId, flags, samples);
    }
}
