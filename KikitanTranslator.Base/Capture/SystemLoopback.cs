using System.Buffers.Binary;
using System.Diagnostics;
using KikitanTranslator.Utility;
using NAudio.Wave;
using Serilog;

namespace KikitanTranslator.Capture;

public sealed class SystemLoopback : ICapture
{
    private const int SampleRate = 16000;
    private const int VadFrameSamples = 480;
    private const int OutputFrameSamples = VadFrameSamples * 2;

    private readonly string _sileroModelPath;
    private readonly object _sync = new();
    private WasapiLoopbackCapture? _capture;
    private SileroVad? _vad;
    private Timer? _silenceTimer;
    private WaveFormat? _format;
    private float[] _frame = new float[OutputFrameSamples];
    private int _frameOffset;
    private long _inputFrameIndex;
    private double _nextOutputPosition;
    private float _previousSample;
    private bool _hasPrevious;
    private long _lastPacketTimestamp;
    private bool _running;
    private bool _paused;
    private bool _reportedAudio;

    public event OnData? OnDataReceived;

    public SystemLoopback(string sileroModelPath) => _sileroModelPath = sileroModelPath;
    public bool IsRunning { get { lock (_sync) return _running; } }
    public uint GetSampleRate() => SampleRate;

    public bool Start()
    {
        lock (_sync)
        {
            if (_running) return true;
        }

        Stop();
        try
        {
            lock (_sync)
            {
                _capture = new WasapiLoopbackCapture();
                _format = _capture.WaveFormat;
                ValidateFormat(_format);
                _vad = new SileroVad(_sileroModelPath);
                ResetAudio();
                _vad.ResetState();
                _paused = false;
                _running = true;
                _reportedAudio = false;
                _lastPacketTimestamp = Stopwatch.GetTimestamp();
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
                _silenceTimer = new Timer(InsertSilence, null, 60, 60);
                Log.Information("[LOOP] Capturing default Windows playback device: {Format}", _format);
            }
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "[LOOP] Unable to start system audio capture");
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        WasapiLoopbackCapture? capture;
        Timer? timer;
        lock (_sync)
        {
            _running = false;
            capture = _capture;
            timer = _silenceTimer;
            _capture = null;
            _silenceTimer = null;
        }

        timer?.Dispose();
        if (capture != null)
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose(); // NAudio waits for its capture thread here.
        }

        lock (_sync)
        {
            _vad?.Dispose();
            _vad = null;
            _format = null;
            ResetAudio();
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            _paused = true;
            ResetAudio();
            _vad?.ResetState();
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            ResetAudio();
            _vad?.ResetState();
            _lastPacketTimestamp = Stopwatch.GetTimestamp();
            _paused = false;
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(sender, _capture)) return;
            if (e.Exception != null) Log.Error(e.Exception, "[LOOP] System audio capture stopped");
            else if (_running) Log.Warning("[LOOP] System audio capture stopped unexpectedly");
            _running = false;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            if (!_running || _paused || _format == null || e.BytesRecorded == 0) return;

            _lastPacketTimestamp = Stopwatch.GetTimestamp();
            var bytes = e.Buffer.AsSpan(0, e.BytesRecorded);
            var bytesPerSample = _format.BitsPerSample / 8;
            var inputFrames = bytes.Length / _format.BlockAlign;
            var peak = 0f;
            for (var i = 0; i < inputFrames; i++)
            {
                var mono = 0f;
                var offset = i * _format.BlockAlign;
                for (var channel = 0; channel < _format.Channels; channel++)
                    mono += ReadSample(bytes.Slice(offset + channel * bytesPerSample, bytesPerSample), _format);

                mono /= _format.Channels;
                peak = Math.Max(peak, Math.Abs(mono));
                AddInputSample(mono, _format.SampleRate);
            }
            if (!_reportedAudio && peak > 0.01f)
            {
                _reportedAudio = true;
                Log.Debug("[LOOP] Received non-silent system audio: peak={Peak:0.000}", peak);
            }
            _lastPacketTimestamp = Stopwatch.GetTimestamp();
        }
    }

    private void InsertSilence(object? state)
    {
        lock (_sync)
        {
            if (!_running || _paused || _vad == null ||
                Stopwatch.GetElapsedTime(_lastPacketTimestamp).TotalMilliseconds < 120) return;

            // WASAPI loopback may stop sending packets when the output device is silent.
            // Continue VAD with silence so a final speech segment can be completed.
            ResetAudio();
            EmitFrame(new float[OutputFrameSamples]);
        }
    }

    private void AddInputSample(float sample, int inputSampleRate)
    {
        if (!_hasPrevious)
        {
            _previousSample = sample;
            _hasPrevious = true;
            _inputFrameIndex = 0;
            _nextOutputPosition = 0;
            return;
        }

        _inputFrameIndex++;
        while (_nextOutputPosition <= _inputFrameIndex)
        {
            var fraction = _nextOutputPosition - (_inputFrameIndex - 1);
            AddOutputSample(_previousSample + (sample - _previousSample) * (float)fraction);
            _nextOutputPosition += (double)inputSampleRate / SampleRate;
        }
        _previousSample = sample;
    }

    private void AddOutputSample(float sample)
    {
        _frame[_frameOffset++] = sample;
        if (_frameOffset != OutputFrameSamples) return;

        var completed = _frame;
        _frame = new float[OutputFrameSamples];
        _frameOffset = 0;
        EmitFrame(completed);
    }

    private void EmitFrame(float[] frame)
    {
        if (_vad == null) return;
        var speech = _vad.SpeechDetection(frame.AsSpan(0, VadFrameSamples)) |
                     _vad.SpeechDetection(frame.AsSpan(VadFrameSamples, VadFrameSamples));
        try
        {
            OnDataReceived?.Invoke(frame, speech);
        }
        catch (Exception e)
        {
            Log.Error(e, "[LOOP] Audio subscriber failed");
        }
    }

    private void ResetAudio()
    {
        _frame = new float[OutputFrameSamples];
        _frameOffset = 0;
        _inputFrameIndex = 0;
        _nextOutputPosition = 0;
        _hasPrevious = false;
    }

    private static void ValidateFormat(WaveFormat format)
    {
        var bits = format.BitsPerSample;
        var supported = format.Encoding switch
        {
            WaveFormatEncoding.IeeeFloat => bits is 32 or 64,
            WaveFormatEncoding.Pcm => bits is 8 or 16 or 24 or 32,
            _ => false
        };
        if (!supported || format.SampleRate <= 0 || format.Channels <= 0 ||
            format.BlockAlign != format.Channels * bits / 8)
            throw new NotSupportedException($"Unsupported Windows playback format: {format}");
    }

    private static float ReadSample(ReadOnlySpan<byte> bytes, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var value = format.BitsPerSample == 32
                ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes))
                : (float)BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes));
            return float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
        }

        return format.BitsPerSample switch
        {
            8 => (bytes[0] - 128) / 128f,
            16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,
            24 => ((bytes[0] | bytes[1] << 8 | bytes[2] << 16) << 8 >> 8) / 8388608f,
            32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648f,
            _ => 0f
        };
    }
}
