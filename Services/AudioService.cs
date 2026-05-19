using Silk.NET.OpenAL;

namespace VoidRunner.Services;

// Short synthesized cues via OpenAL Soft. Silent if the device fails to initialize.

public sealed unsafe class AudioService : IDisposable
{
    private const int SampleRate = 22050;

    private ALContext? _alc;
    private AL? _al;

    private nint _deviceNative;
    private nint _contextNative;

    private uint[] _buffers = [];
    private uint[] _sources = [];
    private int _spin;
    private bool _alive;

    public bool IsMuted { get; set; }

    public AudioService() => TryInit();

    private void TryInit()
    {
        try
        {
            _alc = ALContext.GetApi(true);
            Device* device = _alc.OpenDevice("");
            if ((nint)device == 0)
            {
                _alc.Dispose();
                _alc = null;
                return;
            }

            Context* ctx = _alc.CreateContext(device, null);
            if ((nint)ctx == 0)
            {
                _alc.CloseDevice(device);
                _alc.Dispose();
                _alc = null;
                return;
            }

            if (!_alc.MakeContextCurrent(ctx))
            {
                _alc.DestroyContext(ctx);
                _alc.CloseDevice(device);
                _alc.Dispose();
                _alc = null;
                return;
            }

            _deviceNative = (nint)device;
            _contextNative = (nint)ctx;

            _al = AL.GetApi(true);

            short[] pcmHit = sineTone(520f, 0.065f, 0.55f);
            short[] pcmMenu = sineTone(900f, 0.068f, 0.52f);
            short[] pcmPause = sineTone(420f, 0.098f, 0.42f);
            short[] pcmGameOver = GameOverChime();

            uint[] buffers = _al.GenBuffers(4);
            _al.BufferData(buffers[0], BufferFormat.Mono16, pcmHit, SampleRate);
            _al.BufferData(buffers[1], BufferFormat.Mono16, pcmMenu, SampleRate);
            _al.BufferData(buffers[2], BufferFormat.Mono16, pcmPause, SampleRate);
            _al.BufferData(buffers[3], BufferFormat.Mono16, pcmGameOver, SampleRate);
            _buffers = buffers;

            _sources = _al.GenSources(4);
            foreach (uint s in _sources)
            {
                _al.SetSourceProperty(s, SourceFloat.Gain, 1f);
                _al.SetSourceProperty(s, SourceBoolean.Looping, false);
                _al.SetSourceProperty(s, SourceVector3.Position, 0f, 0f, 0f);
            }

            _alive = true;
        }
        catch
        {
            TearDownIncomplete();
        }
    }

    private void TearDownIncomplete()
    {
        _al?.DeleteSources(_sources);
        _sources = [];
        _al?.DeleteBuffers(_buffers);
        _buffers = [];

        _al?.Dispose();
        _al = null;

        if (_alc is not null)
        {
            if (_contextNative != 0)
            {
                _alc.DestroyContext((Context*)_contextNative);
                _contextNative = 0;
            }

            if (_deviceNative != 0)
            {
                _alc.CloseDevice((Device*)_deviceNative);
                _deviceNative = 0;
            }

            _alc.Dispose();
            _alc = null;
        }
    }

    public void PlayHit() => TryPlay(0);

    public void PlayMenuSelect() => TryPlay(1);

    public void PlayPause() => TryPlay(2);

    public void PlayGameOver() => TryPlay(3);

    private void TryPlay(int bufferIndex)
    {
        if (!_alive || IsMuted || _al is null || bufferIndex >= _buffers.Length || _buffers.Length == 0)
            return;

        uint buf = _buffers[bufferIndex];
        uint src = _sources[_spin++ % _sources.Length];

        _al.SourceStop(src);
        _al.SetSourceProperty(src, SourceInteger.Buffer, buf);
        _al.SourcePlay(src);
    }

    private static short[] sineTone(float frequencyHz, float durationSec, float volume)
    {
        int samples = Math.Max(1, (int)(durationSec * SampleRate));
        var pcm = new short[samples];

        int edgeSamples = Math.Max(1, SampleRate / 800);
        float vol = volume;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = RampEnds(i, samples, edgeSamples) * vol;
            float wave = envelope * MathF.Sin(MathF.PI * 2f * frequencyHz * t);
            pcm[i] = (short)Math.Clamp(wave * 32767f, -32767f, 32767f);
        }

        return pcm;
    }

    private static short[] GameOverChime()
    {
        short[] burst0 = sineTone(330f, 0.088f, 0.42f);
        short[] burst1 = sineTone(247f, 0.092f, 0.42f);
        var delaySamples = (int)(0.22f * SampleRate);
        var totalSamples = Math.Max(burst0.Length, delaySamples + burst1.Length);
        var mix = new short[totalSamples];

        Overlay(mix, burst0, 0);
        Overlay(mix, burst1, delaySamples);

        var fadeTail = Math.Min(totalSamples / 5, SampleRate / 25);
        for (int i = totalSamples - fadeTail; i < totalSamples; i++)
            mix[i] = (short)(mix[i] * (totalSamples - 1 - i) / Math.Max(fadeTail, 1));

        return mix;

        static void Overlay(short[] dst, short[] burst, int at)
        {
            int n = burst.Length;
            for (int i = 0; i < n; i++)
            {
                var j = at + i;
                if ((uint)j < (uint)dst.Length)
                    dst[j] = SaturateAdd(dst[j], burst[i]);
            }
        }
    }

    private static short SaturateAdd(short a, short b)
    {
        int s = a + b;
        return (short)Math.Clamp(s, -32767, 32767);
    }

    private static float RampEnds(int index, int total, int edge)
    {
        float up = edge > 0 ? MathF.Min(index / (float)edge, 1f) : 1f;
        float down = edge > 0 ? MathF.Min((total - 1 - index) / (float)edge, 1f) : 1f;
        return MathF.Min(up, down);
    }

    public void Dispose()
    {
        if (!_alive)
        {
            TearDownIncomplete();
            GC.SuppressFinalize(this);
            return;
        }

        _alive = false;

        _al?.DeleteSources(_sources);
        _sources = [];
        _al?.DeleteBuffers(_buffers);
        _buffers = [];

        _al?.Dispose();
        _al = null;

        if (_alc is not null && _deviceNative != 0)
        {
            _alc.MakeContextCurrent((Context*)null);

            if (_contextNative != 0)
                _alc.DestroyContext((Context*)_contextNative);

            _alc.CloseDevice((Device*)_deviceNative);
            _contextNative = 0;
            _deviceNative = 0;
        }

        _alc?.Dispose();
        _alc = null;

        GC.SuppressFinalize(this);
    }
}
