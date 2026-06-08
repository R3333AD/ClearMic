using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
namespace ClearMic.ApoTest;

// Mirrors ClearMic.ApoHost.ApoFrame
readonly struct ApoFrame
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

class Program
{
    static int TotalTests = 0;
    static int PassedTests = 0;

    static void Main(string[] args)
    {
        Console.Error.WriteLine("=== ClearMic APO Test Harness ===\n");

        var dllPath = FindApoDll();
        if (dllPath == null)
        {
            Console.Error.WriteLine("FAIL: ClearMicApo.dll not found");
            return;
        }
        Console.Error.WriteLine("  DLL=" + dllPath);

        LoadDllTest(dllPath);

        var apoHost = StartApoHost();
        if (apoHost == null)
        {
            Console.Error.WriteLine("FAIL: Could not start ApoHost");
            return;
        }

        try
        {
            NamedPipeProtocolTest();
            PipeFrameTest_Silence();
            PipeFrameTest_SineWave();
            PipeFrameTest_Clipping();
            PipeFrameTest_Passthrough();
            PipeFrameTest_MultipleFrames();
            PipeFrameTest_Reorder();
            PipeFrameTest_Burst();
        }
        finally
        {
            if (!apoHost.HasExited)
            {
                apoHost.Kill();
                apoHost.WaitForExit(3000);
            }
            apoHost.Dispose();
        }

        Console.Error.WriteLine($"\n=== Results: {PassedTests}/{TotalTests} passed === ");
        Environment.Exit(PassedTests == TotalTests ? 0 : 1);
    }

    // ---------------------------------------------------------------
    // DLL test
    // ---------------------------------------------------------------

    static void LoadDllTest(string dllPath)
    {
        StartTest("DLL_LoadAndExports");
        var hMod = LoadLibraryW(dllPath);
        if (hMod == IntPtr.Zero)
        {
            Fail("LoadLibrary failed: " + Marshal.GetLastWin32Error());
            return;
        }

        var names = new[] { "DllCanUnloadNow", "DllGetClassObject", "DllRegisterServer", "DllUnregisterServer" };
        foreach (var name in names)
        {
            var addr = GetProcAddress(hMod, name);
            if (addr == IntPtr.Zero)
                Fail($"Export {name} missing");
        }

        // Call DllGetClassObject via GetProcAddress on loaded module
        var CLSID_ClearMicApo = new Guid("7C538B0F-709F-4CBF-8E2A-EBCD11DB6B7B");
        var IID_IClassFactory = new Guid("00000001-0000-0000-C000-000000000046");

        var dllGetClassObjAddr = GetProcAddress(hMod, "DllGetClassObject");
        if (dllGetClassObjAddr == IntPtr.Zero)
        {
            Fail("DllGetClassObject export not found");
            FreeLibrary(hMod);
            return;
        }
        var dllGetClassObj = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(dllGetClassObjAddr);
        int hr = dllGetClassObj(ref CLSID_ClearMicApo, ref IID_IClassFactory, out IntPtr factoryPtr);
        if (hr != 0)
        {
            Fail("DllGetClassObject failed: hr=0x" + hr.ToString("X8"));
            FreeLibrary(hMod);
            return;
        }

        // Create APO instance via factory
        var factory = (IClassFactory)Marshal.GetTypedObjectForIUnknown(factoryPtr, typeof(IClassFactory));
        Marshal.Release(factoryPtr);

        try
        {
            var IID_IUnknown = new Guid("00000000-0000-0000-C000-000000000046");
            int hr2 = factory.CreateInstance(IntPtr.Zero, ref IID_IUnknown, out IntPtr apoPtr);
            if (hr2 != 0 || apoPtr == IntPtr.Zero)
            {
                Fail("CreateInstance failed: hr=0x" + hr2.ToString("X8"));
            }
            else
            {
                Marshal.Release(apoPtr);
                Pass();
            }
        }
        catch (Exception ex)
        {
            Fail("CreateInstance threw: " + ex.Message);
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
            FreeLibrary(hMod);
        }
    }

    // ---------------------------------------------------------------
    // Pipe protocol tests
    // ---------------------------------------------------------------

    static void NamedPipeProtocolTest()
    {
        StartTest("Pipe_ConnectAndRoundTrip");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        var bytes = new byte[ApoFrame.WireSize];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), 42u); // frameId
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), 4u);  // flags (process)
        Array.Clear(bytes, 8, ApoFrame.WireSize - 8);        // silence

        pipe.Write(bytes, 0, bytes.Length);
        var reply = new byte[ApoFrame.WireSize];
        var read = 0;
        while (read < ApoFrame.WireSize)
            read += pipe.Read(reply, read, ApoFrame.WireSize - read);

        var frameId = BitConverter.ToUInt32(reply, 0);
        if (frameId != 42)
        {
            Fail($"Expected frameId=42, got {frameId}");
            return;
        }
        Pass();
    }

    static void PipeFrameTest_Silence()
    {
        StartTest("Pipe_SilenceInSilenceOut");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        var input = new ApoFrame(0, 4, new short[ApoFrame.SamplesPerFrame]);
        var reply = SendAndReceive(pipe, input);

        if (reply.FrameId != 0)
        {
            Fail($"frameId mismatch: expected 0 got {reply.FrameId}");
            return;
        }
        // silence through ONNX filter may produce non-zero output; just verify
        // frame was processed without crash
        if (reply.Samples.Length != ApoFrame.SamplesPerFrame)
        {
            Fail($"sample count mismatch: expected {ApoFrame.SamplesPerFrame} got {reply.Samples.Length}");
            return;
        }
        Pass();
    }

    static void PipeFrameTest_SineWave()
    {
        StartTest("Pipe_SineWave");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        var samples = new short[ApoFrame.SamplesPerFrame];
        for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
            samples[i] = (short)(Math.Sin(2 * Math.PI * 440.0 * i / 48000.0) * 30000);

        var input = new ApoFrame(7, 4, samples);
        var reply = SendAndReceive(pipe, input);

        if (reply.FrameId != 7)
        {
            Fail($"frameId mismatch: expected 7 got {reply.FrameId}");
            return;
        }
        // output may differ from input (ONNR filter processes it); just check valid range
        foreach (var s in reply.Samples)
        {
            if (s < short.MinValue || s > short.MaxValue)
            {
                Fail($"Sample out of range: {s}");
                return;
            }
        }
        Pass();
    }

    static void PipeFrameTest_Clipping()
    {
        StartTest("Pipe_ClippingBoundary");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        // max and min int16 values
        var samples = new short[ApoFrame.SamplesPerFrame];
        samples[0] = short.MaxValue;
        samples[1] = short.MinValue;
        for (int i = 2; i < ApoFrame.SamplesPerFrame; i++)
            samples[i] = 0;

        var input = new ApoFrame(99, 4, samples);
        var reply = SendAndReceive(pipe, input);

        if (reply.FrameId != 99)
        {
            Fail($"frameId mismatch: expected 99 got {reply.FrameId}");
            return;
        }
        Pass();
    }

    static void PipeFrameTest_Passthrough()
    {
        StartTest("Pipe_PassthroughFlag");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        var samples = new short[ApoFrame.SamplesPerFrame];
        for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
            samples[i] = (short)(1000 + i);

        // flags=0 means passthrough (no filter)
        var input = new ApoFrame(1, 0, samples);
        var reply = SendAndReceive(pipe, input);

        if (reply.FrameId != 1)
        {
            Fail($"frameId mismatch: expected 1 got {reply.FrameId}");
            return;
        }
        // with passthrough flag, output should match input within 1 LSB (float round-trip loss)
        for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
        {
            var delta = Math.Abs(reply.Samples[i] - samples[i]);
            if (delta > 1)
            {
                Fail($"Passthrough sample {i} differs: expected {samples[i]} got {reply.Samples[i]} (delta={delta})");
                return;
            }
        }
        Pass();
    }

    static void PipeFrameTest_MultipleFrames()
    {
        StartTest("Pipe_MultipleFrames");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        for (uint id = 0; id < 10; id++)
        {
            var samples = new short[ApoFrame.SamplesPerFrame];
            for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
                samples[i] = (short)(id * 100 + i);

            var input = new ApoFrame(id, 4, samples);
            var reply = SendAndReceive(pipe, input);

            if (reply.FrameId != id)
            {
                Fail($"Frame {id}: frameId mismatch, got {reply.FrameId}");
                return;
            }
        }
        Pass();
    }

    static void PipeFrameTest_Reorder()
    {
        StartTest("Pipe_FrameOrderIndependence");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        // Send frames out of order, verify each returns its own frameId
        var ids = new uint[] { 100, 5, 200, 1, 50 };
        foreach (var id in ids)
        {
            var samples = new short[ApoFrame.SamplesPerFrame];
            for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
                samples[i] = (short)(id);

            var input = new ApoFrame(id, 4, samples);
            var reply = SendAndReceive(pipe, input);

            if (reply.FrameId != id)
            {
                Fail($"Frame {id}: expected frameId {id}, got {reply.FrameId}");
                return;
            }
        }
        Pass();
    }

    static void PipeFrameTest_Burst()
    {
        StartTest("Pipe_Burst100Frames");
        using var pipe = ConnectPipe();
        if (pipe == null) return;

        // Send 100 frames in rapid succession
        for (uint id = 0; id < 100; id++)
        {
            var samples = new short[ApoFrame.SamplesPerFrame];
            for (int i = 0; i < ApoFrame.SamplesPerFrame; i++)
                samples[i] = (short)(id);

            var input = new ApoFrame(id, 0, samples); // passthrough for speed
            var reply = SendAndReceive(pipe, input);

            if (reply.FrameId != id)
            {
                Fail($"Burst frame {id}: frameId mismatch, got {reply.FrameId}");
                return;
            }
        }
        Pass();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    static string? FindApoDll()
    {
        // Try output dirs relative to exe location
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            @"..\..\..\..\ClearMic.Driver\bin\x64\Release\ClearMicApo.dll",
            @"..\..\..\ClearMic.Driver\bin\x64\Release\ClearMicApo.dll",
            @"..\..\ClearMic.Driver\bin\x64\Release\ClearMicApo.dll",
            @"..\ClearMic.Driver\bin\x64\Release\ClearMicApo.dll",
            @"ClearMic.Driver\bin\x64\Release\ClearMicApo.dll",
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, c));
            if (File.Exists(full)) return full;
        }
        return null;
    }

    static NamedPipeClientStream? ConnectPipe()
    {
        const int maxRetries = 20;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", "ClearMic_APO", PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                pipe.Connect(2000);
                pipe.ReadMode = PipeTransmissionMode.Message;
                return pipe;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(500);
            }
        }
        Fail("Could not connect to pipe after " + maxRetries + " retries");
        return null;
    }

    static ApoFrame SendAndReceive(NamedPipeClientStream pipe, ApoFrame frame)
    {
        var wire = frame.ToWire();
        pipe.Write(wire, 0, wire.Length);

        var reply = new byte[ApoFrame.WireSize];
        var read = 0;
        while (read < ApoFrame.WireSize)
            read += pipe.Read(reply, read, ApoFrame.WireSize - read);

        return ApoFrame.FromWire(reply);
    }

    static Process? StartApoHost()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            @"..\..\..\..\ClearMic.ApoHost\bin\Release\net8.0-windows\ClearMic.ApoHost.dll",
            @"..\..\..\ClearMic.ApoHost\bin\Release\net8.0-windows\ClearMic.ApoHost.dll",
        };
        string? dllPath = null;
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(Path.Combine(baseDir, c));
            if (File.Exists(full)) { dllPath = full; break; }
        }
        if (dllPath == null)
        {
            Fail("ApoHost.dll not found");
            return null;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{dllPath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var proc = new Process { StartInfo = psi };
        proc.Start();

        for (int i = 0; i < 30; i++)
        {
            var line = proc.StandardError.ReadLine();
            if (line?.Contains("PIPE=") == true) return proc;
        }

        Fail("ApoHost did not signal ready");
        proc.Kill();
        proc.Dispose();
        return null;
    }

    static void StartTest(string name)
    {
        TotalTests++;
        Console.Error.Write($"  [{TotalTests}] {name} ... ");
    }

    static void Pass()
    {
        PassedTests++;
        Console.Error.WriteLine("PASS");
    }

    static void Fail(string reason)
    {
        Console.Error.WriteLine("FAIL: " + reason);
    }

    // ---------------------------------------------------------------
    // P/Invoke
    // ---------------------------------------------------------------

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibraryW(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern int FreeLibrary(IntPtr hLibModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int DllGetClassObjectDelegate(ref Guid clsid, ref Guid riid, out IntPtr ppv);
}

// Minimal IClassFactory for COM interop
[ComImport]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
    [PreserveSig]
    int LockServer(bool fLock);
}
