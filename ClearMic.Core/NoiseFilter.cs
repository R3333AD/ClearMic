using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ClearMic.Core;

public sealed class NoiseFilter : IDisposable
{
    private const int FrameSize = 480;
    private const int StateCount = 12;

    private readonly InferenceSession _session;
    private bool _isEnabled = true;

    // Pre-allocated state arrays
    private float[] _erbNormState = [];
    private float[] _bandUnitNormState = [];
    private float[] _analysisMem = [];
    private float[] _synthesisMem = [];
    private float[] _rollingErbBuf = [];
    private float[] _rollingFeatSpecBuf = [];
    private float[] _rollingC0Buf = [];
    private float[] _rollingSpecBufX = [];
    private float[] _rollingSpecBufY = [];
    private float[] _encHidden = [];
    private float[] _erbDecHidden = [];
    private float[] _dfDecHidden = [];

    // Pre-allocated tensors (reused across frames)
    private readonly DenseTensor<float> _inputFrameTensor;
    private readonly DenseTensor<float>[] _stateTensors;
    private readonly List<NamedOnnxValue> _inputs;
    private readonly string[] _outputNames;

    private static readonly int[][] StateShapes =
    [
        [32],
        [1, 96, 1],
        [480],
        [480],
        [1, 1, 3, 32],
        [1, 2, 3, 96],
        [1, 64, 5, 96],
        [5, 481, 2],
        [7, 481, 2],
        [1, 1, 256],
        [2, 1, 256],
        [2, 1, 256],
    ];

    private static readonly string[] StateNames =
    [
        "erb_norm_state",
        "band_unit_norm_state",
        "analysis_mem",
        "synthesis_mem",
        "rolling_erb_buf",
        "rolling_feat_spec_buf",
        "rolling_c0_buf",
        "rolling_spec_buf_x",
        "rolling_spec_buf_y",
        "enc_hidden",
        "erb_dec_hidden",
        "df_dec_hidden",
    ];

    public event EventHandler<bool>? EnabledChanged;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            EnabledChanged?.Invoke(this, value);
        }
    }

    public NoiseFilter()
    {
        var modelPath = FindModelPath();
        var opts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
            EnableCpuMemArena = true,
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };
        _session = new InferenceSession(modelPath, opts);
        _outputNames = _session.OutputNames.ToArray();

        // Pre-allocate input tensor
        _inputFrameTensor = new DenseTensor<float>([FrameSize]);

        // Pre-allocate state tensors
        _stateTensors = new DenseTensor<float>[StateCount];
        for (int i = 0; i < StateCount; i++)
            _stateTensors[i] = new DenseTensor<float>(StateShapes[i]);

        // Pre-allocate NamedOnnxValue list (will fill with values)
        _inputs = new List<NamedOnnxValue>(StateCount + 1);

        ResetState();
    }

    public void ResetState()
    {
        _erbNormState = Linspace(-60f, -90f, 32);
        _bandUnitNormState = Linspace(0.001f, 0.0001f, 96);
        _analysisMem = new float[480];
        _synthesisMem = new float[480];
        _rollingErbBuf = new float[1 * 1 * 3 * 32];
        _rollingFeatSpecBuf = new float[1 * 2 * 3 * 96];
        _rollingC0Buf = new float[1 * 64 * 5 * 96];
        _rollingSpecBufX = new float[5 * 481 * 2];
        _rollingSpecBufY = new float[7 * 481 * 2];
        _encHidden = new float[1 * 1 * 256];
        _erbDecHidden = new float[2 * 1 * 256];
        _dfDecHidden = new float[2 * 1 * 256];
    }

    public float[] Process(float[] input)
    {
        if (!_isEnabled || input.Length == 0)
            return input;

        var output = new float[input.Length];
        int offset = 0;

        while (offset + FrameSize <= input.Length)
        {
            ProcessFrame(input, offset, output);
            offset += FrameSize;
        }

        if (offset < input.Length)
            Array.Copy(input, offset, output, offset, input.Length - offset);

        return output;
    }

    private void ProcessFrame(float[] input, int offset, float[] output)
    {
        for (int i = 0; i < FrameSize; i++)
            _inputFrameTensor.SetValue(i, input[offset + i]);

        _inputs.Clear();
        _inputs.Add(NamedOnnxValue.CreateFromTensor("input_frame", _inputFrameTensor));

        var arrays = GetStateArrays();
        for (int i = 0; i < StateCount; i++)
        {
            var t = _stateTensors[i];
            var d = arrays[i];
            for (int j = 0; j < d.Length; j++)
                t.SetValue(j, d[j]);
            _inputs.Add(NamedOnnxValue.CreateFromTensor(StateNames[i], t));
        }

        using var results = _session.Run(_inputs, _outputNames);

        var outFrame = results[0].AsTensor<float>();
        for (int i = 0; i < FrameSize; i++)
            output[offset + i] = outFrame.GetValue(i);

        for (int i = 0; i < StateCount; i++)
        {
            var outState = results[i + 1].AsTensor<float>();
            var d = arrays[i];
            for (int j = 0; j < d.Length; j++)
                d[j] = outState.GetValue(j);
        }
    }

    private float[][] GetStateArrays() =>
    [
        _erbNormState, _bandUnitNormState, _analysisMem, _synthesisMem,
        _rollingErbBuf, _rollingFeatSpecBuf, _rollingC0Buf,
        _rollingSpecBufX, _rollingSpecBufY,
        _encHidden, _erbDecHidden, _dfDecHidden,
    ];

    private static float[] Linspace(float start, float end, int count)
    {
        var result = new float[count];
        if (count == 1) { result[0] = start; return result; }
        float step = (end - start) / (count - 1);
        for (int i = 0; i < count; i++)
            result[i] = start + step * i;
        return result;
    }

    private static string FindModelPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "models", "denoiser_model.onnx"),
            Path.Combine(baseDir, "denoiser_model.onnx"),
            Path.Combine(Environment.CurrentDirectory, "models", "denoiser_model.onnx"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "..", "models", "denoiser_model.onnx"),
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }
        throw new FileNotFoundException(
            "denoiser_model.onnx not found. Place the model file in the output directory under 'models/' folder.");
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
