using System.IO.Pipes;
using ClearMic.Core;

namespace ClearMic.ApoHost;

class Program
{
    private const string PipeName = "ClearMic_APO";

    static async Task Main(string[] args)
    {
        using var filter = new NoiseFilter();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Environment.Exit(0);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"[ApoHost] Fatal: {e.ExceptionObject}");
        };

        while (true)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                Console.Error.WriteLine($"[ApoHost] PIPE={PipeName}");

                await pipe.WaitForConnectionAsync();

                var buffer = new byte[ApoFrame.WireSize];

                while (true)
                {
                    var read = 0;
                    while (read < ApoFrame.WireSize)
                    {
                        var n = pipe.Read(buffer, read, ApoFrame.WireSize - read);
                        if (n == 0) throw new EndOfStreamException("disconnected");
                        read += n;
                    }

                    var frame = ApoFrame.FromWire(buffer);
                    var processed = ProcessFrame(frame.Samples, filter, frame.Flags);
                    var response = new ApoFrame(frame.FrameId, frame.Flags, processed);
                    var outBytes = response.ToWire();
                    pipe.Write(outBytes, 0, outBytes.Length);
                }
            }
            catch (Exception ex) when (ex is IOException or EndOfStreamException or InvalidOperationException)
            {
                Console.Error.WriteLine($"[ApoHost] Disconnected: {ex.Message}");
            }
        }
    }

    private static short[] ProcessFrame(short[] input, NoiseFilter filter, uint flags)
    {
        var applyFilter = (flags & 0x04) != 0;

        var floatIn = new float[ApoFrame.SamplesPerFrame];
        for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
            floatIn[i] = input[i] / 32768f;

        var floatOut = applyFilter && filter.IsEnabled
            ? filter.Process(floatIn)
            : floatIn;

        var output = new short[ApoFrame.SamplesPerFrame];
        for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
        {
            var clamped = Math.Clamp(floatOut[i], -1f, 1f);
            output[i] = (short)(clamped * 32767f);
        }
        return output;
    }
}
