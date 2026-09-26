using System.Globalization;
using System.Net.WebSockets;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using KikitanTranslator.Capture;
using KikitanTranslator.Recognizers;
using KikitanTranslator.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Websocket.Client;

namespace KikitanTranslator.Base.Recognizers;

public class Gemini(ICapture capture) : IRecognizer
{
    private WebsocketClient? _client;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<Task> _callbackTasks = [];
    private bool _audioSubscribed;
    private volatile bool _disposed;
    private bool _stopping;

    private RecognizerStatus _status;

    private string currentInput = "";
    private string currentOutput = "";

    private int _lastTranscription;
    
    public void Start(string language, IErrorHandler errorHandler)
    {
        if (_disposed) return;
        Resources.ErrorMessages.messages.Culture = new CultureInfo(AppConfig.ConfigObject.Language == "jp" ? "ja" : AppConfig.ConfigObject.Language);
        
        Log.Information($"[GEMI] Starting live translator: capture={capture.GetType().Name}, language={language}, status={_status}");
        
        if (string.IsNullOrEmpty(AppConfig.ConfigObject.GeminiApiKey))
        {
            Log.Error("[GEMI] No API key is defined!");
            errorHandler.OnError("GEMINI_NO_API_KEY");
            
            ChangeRecognizerStatus(RecognizerStatus.NotStarted);

            return;
        }

        if (!ValidateKey().GetAwaiter().GetResult())
        {
            Log.Error("[GEMI] Invalid API key!");
            errorHandler.OnError("GEMINI_INVALID_API_KEY");
            
            return;
        }
        
        var url = $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={AppConfig.ConfigObject.GeminiApiKey}";
        _client = new WebsocketClient(new Uri(url));
        
        _client.ReconnectionHappened.Subscribe(info => RunCallback(async () =>
        {
            if (_disposed) return;
            Log.Information($"[GEMI] Connection event: type={info.Type}, status={_status}, capture={capture.GetType().Name}");
            if (_status == RecognizerStatus.Connecting || _status == RecognizerStatus.Running) return;
            
            Log.Verbose("[GEMI] Websocket connection established");
            try { await Task.Delay(100, _lifetime.Token); }
            catch (OperationCanceledException) { return; }
            if (_disposed) return;
            
            var configPayload = new
            {
                setup = new
                {
                    model = "models/gemini-3.5-live-translate-preview",
                    inputAudioTranscription = new {},
                    outputAudioTranscription = new {},
                    generationConfig = new
                    {
                        responseModalities = (string[])["TEXT"],
                        translationConfig = new
                        {
                            targetLanguageCode = language == AppConfig.ConfigObject.SourceLanguage ? AppConfig.ConfigObject.TargetLanguage : AppConfig.ConfigObject.SourceLanguage,
                            echoTargetLanguage = false
                        }
                    }
                }
            };

            _client.Send(JsonConvert.SerializeObject(configPayload));
            
            ChangeRecognizerStatus(RecognizerStatus.Connecting);
            Log.Information("[GEMI] Payload has been sent!");
        }));
        
        _client.MessageReceived.Subscribe(message =>
        {
            if (_disposed) return;
            var msg = Encoding.UTF8.GetString(message.Binary).Trim();
            dynamic data = JObject.Parse(msg);

            if (_status != RecognizerStatus.Running)
            {
                bool started;
                lock (_callbackTasks)
                {
                    if (_disposed || _stopping) return;
                    if (!_audioSubscribed)
                    {
                        capture.OnDataReceived += OnAudioData;
                        _audioSubscribed = true;
                    }
                    started = capture.Start();
                }

                if (!started)
                {
                    Log.Error("[GEMI] Unable to start capture!");
                    Stop();

                    return;
                }
                if (_disposed) return;
            
                ChangeRecognizerStatus(RecognizerStatus.Running);
                Log.Information("[GEMI] Gemini recognizer has started");
            }

            try
            {
                if (data.serverContent?.inputTranscription != null && data.serverContent?.inputTranscription.text != null)
                {
                    currentInput += data.serverContent.inputTranscription.text;
                    
                    OnRecognitionReceived?.Invoke($"{currentInput}|{currentOutput}", false);
                }

                if (data.serverContent?.outputTranscription != null)
                {
                    if (DateTime.Now.Millisecond - _lastTranscription > 5000)
                    {
                        currentInput = "";
                        currentOutput = "";
                    }

                    _lastTranscription = DateTime.Now.Millisecond;
                    
                    if (data.serverContent?.outputTranscription.text != null)
                    {
                        currentOutput += data.serverContent.outputTranscription.text;
                        
                        OnRecognitionReceived?.Invoke($"{currentInput}|{currentOutput}".Trim(), false);
                    } else if (currentInput.Length != 0)
                    {
                        OnRecognitionReceived?.Invoke($"{currentInput}|{currentOutput}".Trim(), true);

                        currentInput = "";
                        currentOutput = "";
                    }
                }
                    
            }
            catch (Exception e)
            {
                Log.Warning($"[GEMI] Transcription message handling failed: type={e.GetType().Name}, hresult=0x{e.HResult:X8}, stack={e.StackTrace}, capture={capture.GetType().Name}");
            }
        });
        
        _client.DisconnectionHappened.Subscribe(info =>
        {
            if (_disposed) return;
            Log.Error($"[GEMI] Websocket connection has closed. Reason: {info.Type}");
            ChangeRecognizerStatus(RecognizerStatus.NotStarted);
        });

        _client.Start();
    }

    public void Stop()
    {
        lock (_callbackTasks)
        {
            _stopping = true;
            if (_audioSubscribed)
            {
                capture.OnDataReceived -= OnAudioData;
                _audioSubscribed = false;
            }
        }
        capture.Stop();
        _client?.Stop(WebSocketCloseStatus.NormalClosure, "User request");
        ChangeRecognizerStatus(RecognizerStatus.NotStarted);
         
        Log.Information("[GEMI] Gemini translator has stopped");
    }

    public RecognizerStatus Status() => _status;
    
    private void ChangeRecognizerStatus(RecognizerStatus status)
    {
        Log.Information($"[GEMI] Status changed: {_status} -> {status}, capture={capture.GetType().Name}");
        _status = status;
        OnRecognizerStatusChanged?.Invoke(status);
    }
    
    private void OnAudioData(float[] samples, bool speech)
    {
        if (_disposed) return;
        var payload = new
        {
            realtimeInput = new
            {
                audio = new
                {
                    data =  FloatArrayToBase64(speech ? samples : new float[1600]),
                    mimeType = "audio/pcm;rate=16000"
                }
            }
        };

        _client?.Send(JsonConvert.SerializeObject(payload));
    }

    private void RunCallback(Func<Task> callback)
    {
        lock (_callbackTasks)
        {
            if (_disposed) return;
            _callbackTasks.RemoveAll(task => task.IsCompleted);
            _callbackTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await callback();
                }
                catch (OperationCanceledException) when (_disposed)
                {
                }
                catch (Exception e)
                {
                    Log.Error(e, "[GEMI] WebSocket callback failed");
                }
            }));
        }
    }
    
    
    private string FloatArrayToBase64(float[] audioSamples)
    {
        if (audioSamples.Length == 0) return string.Empty;

        byte[] pcm16 = new byte[audioSamples.Length * 2];
        for (int i = 0; i < audioSamples.Length; i++)
        {
            float clamped = Math.Clamp(audioSamples[i], -1f, 1f);
            short sample = (short)(clamped * short.MaxValue);
            pcm16[i * 2]     = (byte)(sample & 0xFF);
            pcm16[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return Convert.ToBase64String(pcm16);
    }

    private async Task<bool> ValidateKey()
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                await client.GetStringAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models?key={AppConfig.ConfigObject.GeminiApiKey}");

                return true;
            }
            catch (HttpRequestException e)
            {
                Log.Warning($"[GEMI] API-key validation request failed: status={e.StatusCode}, hresult=0x{e.HResult:X8}");
                return false;
            }
        }
    }
    
    public void Dispose()
    {
        Task[] callbacks;
        lock (_callbackTasks)
        {
            if (_disposed) return;
            _disposed = true;
            callbacks = _callbackTasks.ToArray();
        }
        _lifetime.Cancel();
        Stop();
        _client?.Dispose();
        Task.WhenAll(callbacks).GetAwaiter().GetResult();
    }
    
    public event OnRecognition? OnRecognitionReceived;
    public event OnRecognizerStatus? OnRecognizerStatusChanged;
}
