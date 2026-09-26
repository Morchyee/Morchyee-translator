using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using KikitanTranslator.Capture;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Recognizers;

public class GroqRecognizer : IRecognizer
{
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    public event OnRecognition? OnRecognitionReceived;
    public event OnRecognizerStatus? OnRecognizerStatusChanged;

    private readonly ICapture _capture;
    private readonly HttpClient _httpClient = new();

    private RecognizerStatus _status = RecognizerStatus.NotStarted;

    private readonly List<float> _speechBuffer = [];
    private bool _isCollectingSpeech;

    private readonly Queue<(float[] samples, bool speech)> _frameQueue = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<Task> _transcriptions = [];
    private volatile bool _stopping;
    private bool _disposed;

    private string _language;

    public GroqRecognizer(ICapture capture)
    {
        _capture = capture;
        _capture.OnDataReceived += OnDataReceived;
    }

    public void Start(string language, IErrorHandler errorHandler)
    {
        if (_disposed) return;
        Log.Information($"[GROQ] Start requested: capture={_capture.GetType().Name}, language={language}, status={_status}");
        if (string.IsNullOrEmpty(AppConfig.ConfigObject.GroqApiKey))
        {
            Log.Error("[GROQ] No API key is configured!");
            errorHandler.OnError("GROQ_NO_API_KEY");

            return;
        }
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppConfig.ConfigObject.GroqApiKey}");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");

        using var response = client.Send(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Log.Error("[GROQ] Invalid Groq API key!");
            errorHandler.OnError("GROQ_INVALID_API_KEY");

            return;
        }
        
        if (_status == RecognizerStatus.Running) return;
        _status = RecognizerStatus.Connecting;
        
        _speechBuffer.Clear();
        _isCollectingSpeech = false;
        if (!_capture.Start())
        {
            Log.Error("[GROQ] Unable to start capture!");
            Stop();

            return;
        }

        _language = language;

        SetStatus(RecognizerStatus.Running);
        Log.Information("[GROQ] Started Groq recognizer");
    }

    public void Stop()
    {
        _stopping = true;
        _capture.Stop();
        _processingSemaphore.Wait();
        try
        {
            _speechBuffer.Clear();
            _isCollectingSpeech = false;
            lock (_frameQueue) _frameQueue.Clear();
        }
        finally
        {
            _processingSemaphore.Release();
        }
        _cancellation.Cancel();
        Task[] pending;
        lock (_transcriptions) pending = _transcriptions.ToArray();
        Task.WhenAll(pending).GetAwaiter().GetResult();
        SetStatus(RecognizerStatus.NotStarted);
        Log.Information("[GROQ] Stopped Groq recognizer");
    }

    public RecognizerStatus Status() => _status;

    private void OnDataReceived(float[] samples, bool speech)
    {
        if (_stopping) return;
        lock (_frameQueue) _frameQueue.Enqueue((samples, speech));
        DrainQueue();
    }

    private void DrainQueue()
    {
        if (!_processingSemaphore.Wait(0)) return;

        try
        {
            while (!_stopping)
            {
                (float[] samples, bool speech) frame;
                lock (_frameQueue)
                {
                    if (_frameQueue.Count == 0) break;
                    frame = _frameQueue.Dequeue();
                }
                ProcessFrame(frame.samples, frame.speech);
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private void ProcessFrame(float[] samples, bool speech)
    {
        if (speech)
        {
            if (!_isCollectingSpeech)
            {
                _isCollectingSpeech = true;
                _speechBuffer.Clear();
                
                OnRecognitionReceived?.Invoke("", false);
            }
            
            _speechBuffer.AddRange(samples);
        }
        else if (_isCollectingSpeech)
        {
            var audio = _speechBuffer.ToArray();
            _speechBuffer.Clear();
            _isCollectingSpeech = false;
            OnRecognitionReceived?.Invoke("", true);

            if (audio.Length < 3840)
            {
                Log.Debug($"[GROQ] Speech discarded: samples={audio.Length}, minimumSamples=3840, capture={_capture.GetType().Name}");
                return;
            }
            StartTranscription(audio);
        }
    }

    private void StartTranscription(float[] samples)
    {
        lock (_transcriptions)
        {
            if (_stopping) return;
            _transcriptions.RemoveAll(task => task.IsCompleted);
            _transcriptions.Add(TranscribeAsync(samples, _capture.GetSampleRate(), _cancellation.Token));
        }
    }

    private async Task TranscribeAsync(float[] samples, uint sampleRate, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var requestTimer = System.Diagnostics.Stopwatch.StartNew();
        Log.Debug($"[GROQ] Transcription started: request={requestId}, samples={samples.Length}, sampleRate={sampleRate}, capture={_capture.GetType().Name}");
        var apiKey = AppConfig.ConfigObject.GroqApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("[GROQ] No API key configured");
            return;
        }

        try
        {
            var fileContent = new ByteArrayContent(EncodeWav(samples, sampleRate));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            
            using var content = new MultipartFormDataContent
            {
                { fileContent, "file", "audio.wav" },
                { new StringContent("whisper-large-v3"), "model" },
                { new StringContent("0"), "temperature" },
                { new StringContent("verbose_json"), "response_format" },
                { new StringContent(_language), "language" },
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = content;

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Log.Debug($"[GROQ] Transcription response: request={requestId}, status={(int)response.StatusCode}, elapsedMs={requestTimer.ElapsedMilliseconds}");

            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"[GROQ] Whisper API error {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
                
                return;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var transcript = doc.RootElement.TryGetProperty("text", out var textProp) ? textProp.GetString()?.Trim() : null;

            Log.Debug($"[GROQ] Transcription parsed: request={requestId}, chars={transcript?.Length}, elapsedMs={requestTimer.ElapsedMilliseconds}");
            if (string.IsNullOrWhiteSpace(transcript)) return;
            if (!cancellationToken.IsCancellationRequested) OnRecognitionReceived?.Invoke(transcript, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"[GROQ] Transcription failed: request={requestId}, elapsedMs={requestTimer.ElapsedMilliseconds}");
        }
    }

    private static byte[] EncodeWav(float[] samples, uint sampleRate)
    {
        int dataBytes = samples.Length * (BitsPerSample / 8);
        uint byteRate = sampleRate * Channels * (BitsPerSample / 8);

        using var ms = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(ms);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)Channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((ushort)(Channels * (BitsPerSample / 8)));
        writer.Write((ushort)BitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataBytes);

        foreach (var sample in samples)
        {
            var clamped = Math.Max(-1f, Math.Min(1f, sample));
            writer.Write((short)(clamped < 0 ? clamped * 0x8000 : clamped * 0x7FFF));
        }

        return ms.ToArray();
    }

    private void SetStatus(RecognizerStatus status)
    {
        _status = status;
        OnRecognizerStatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _capture.OnDataReceived -= OnDataReceived;
        _httpClient.Dispose();
        _processingSemaphore.Dispose();
        _cancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
