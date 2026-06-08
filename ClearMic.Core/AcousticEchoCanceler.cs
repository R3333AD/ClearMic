namespace ClearMic.Core;

public sealed class AcousticEchoCanceler
{
    private const int FilterLen = 4096;
    private const int FrameSize = 480;
    private const int RefBufferLen = 8192;
    private const float Mu = 0.35f;
    private const float DoubleTalkRatio = 1.5f;
    private const float SilenceThreshold = 1e-5f;

    private readonly float[] _w;
    private readonly float[] _refRing;
    private int _refPos;

    public bool IsEnabled { get; set; } = true;

    public AcousticEchoCanceler()
    {
        _w = new float[FilterLen];
        _refRing = new float[RefBufferLen];
        _refPos = 0;
    }

    public void AddReference(float[] frame)
    {
        foreach (var sample in frame)
        {
            _refRing[_refPos] = sample;
            _refPos = (_refPos + 1) % RefBufferLen;
        }
    }

    public float[] Process(float[] micFrame)
    {
        if (!IsEnabled || micFrame.Length != FrameSize)
            return micFrame;

        // --- build reference buffer covering all positions needed ---
        // Each output sample n needs ref at positions startPos+n ... startPos+n+FilterLen-1
        // So we need samples from startPos to startPos + FrameSize + FilterLen - 1
        int basePos = (_refPos - FilterLen + RefBufferLen) % RefBufferLen;
        int refTotal = FrameSize + FilterLen;
        var refBuf = new float[refTotal];
        for (int i = 0; i < refTotal; i++)
            refBuf[i] = _refRing[(basePos + i) % RefBufferLen];

        // --- reference RMS (full window for double-talk) ---
        float refRms = 0;
        for (int i = 0; i < FilterLen; i++)
            refRms += refBuf[i] * refBuf[i];
        refRms = MathF.Sqrt(refRms / FilterLen);

        float micRms = 0;
        for (int i = 0; i < FrameSize; i++)
            micRms += micFrame[i] * micFrame[i];
        micRms = MathF.Sqrt(micRms / FrameSize);

        bool freeze = refRms < SilenceThreshold
            || micRms > DoubleTalkRatio * refRms;

        var output = new float[FrameSize];

        // --- block NLMS ---
        // Phase 1: compute echo estimate for each sample
        float refEnergy = 0;
        for (int k = 0; k < FilterLen; k++)
            refEnergy += refBuf[k] * refBuf[k];
        float norm = refEnergy + 1e-10f;

        for (int n = 0; n < FrameSize; n++)
        {
            float echo = 0;
            for (int k = 0; k < FilterLen; k++)
                echo += refBuf[n + k] * _w[k];
            output[n] = micFrame[n] - echo;
        }

        // Phase 2: adapt filter (only if not double-talk)
        if (!freeze)
        {
            float mu = Mu / norm;
            for (int k = 0; k < FilterLen; k++)
            {
                float grad = 0;
                for (int n = 0; n < FrameSize; n++)
                    grad += output[n] * refBuf[n + k];
                _w[k] += mu * grad;
            }
        }

        return output;
    }

    public void Reset()
    {
        Array.Clear(_w);
    }
}
