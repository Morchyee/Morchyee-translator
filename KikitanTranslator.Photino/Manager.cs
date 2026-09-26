using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using KikitanTranslator.Base;
using KikitanTranslator.Base.Outputs;
using KikitanTranslator.Base.Recognizers;
using KikitanTranslator.Base.Translators;
using KikitanTranslator.Capture;
using KikitanTranslator.Photino.Handlers;
using KikitanTranslator.Recognizers;
using KikitanTranslator.Utility;
using Newtonsoft.Json;
using Photino.NET;
using Serilog;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Structs;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace KikitanTranslator.Photino;

public class Mic
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("default")] public bool Default;
}

public class AppState
{
    [JsonProperty("microphones")] public Mic[] Microphones;
    [JsonProperty("config")] public ConfigObject Config;
    [JsonProperty("status")] public int Status;
    [JsonProperty("app_version")] public string AppVersion = "Developer Build";
    [JsonProperty("server_version")] public string ServerVersion;
    [JsonProperty("is_linux")] public bool IsLinux;
    [JsonProperty("is_appimage")] public bool IsAppimage;
    [JsonProperty("is_muted")] public bool IsMuted;
}

public class RecognitionData
{
    [JsonProperty("transcription")] public string Transcription;
    [JsonProperty("translation")] public string Translation;
    [JsonProperty("final")] public bool Final;
}

public class Manager
{
    private Kikitan? _microphoneKikitan;
    private Kikitan? _desktopKikitan;

    private SystemLoopback _loopback;
    private Microphone _mic;
    private AppState _appState = new () { Microphones = [] };
    private IErrorHandler _errorHandler;
    private bool _running;
    private readonly object _lifecycleLock = new();

    private readonly SubtitleWriter _subtitleWriter = new();
    private Process? _subtitleProcess;
    private readonly bool _noUI;
    private OscWatcher? _oscWatcher;

    private Connector _connector;

    public DeviceInfo[] GetMicrophones() => _mic.GetCaptureDevices();

    public Manager(bool noUI, Connector connector)
    {
        _noUI = noUI;
        _appState.Config = AppConfig.ConfigObject;

        _errorHandler = new ErrorHandler(connector);
        
        #if DEBUG
        _loopback = new("Resources/wwwroot/silero_vad.onnx");
        _mic = new("Resources/wwwroot/silero_vad.onnx", _errorHandler);
        #else
        _appState.AppVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split("+")[0];
        _loopback = new(Path.Combine(AppContext.BaseDirectory, "wwwroot", "silero_vad.onnx"));
        _mic = new(Path.Combine(AppContext.BaseDirectory, "wwwroot", "silero_vad.onnx"), _errorHandler);
        #endif
        
        _appState.IsLinux = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _appState.IsAppimage = _appState.IsLinux && Environment.GetEnvironmentVariable("KIKITAN_NOT_APPIMAGE") == null;
        _connector = connector;

        Task.Run(async () =>
        {
            var mgr = new UpdateManager(new GithubSource("https://github.com/YusufOzmen01/kikitan-translator", null, true));

            var newVersion = await mgr.CheckForUpdatesAsync();
            _appState.ServerVersion = newVersion?.TargetFullRelease.Version.ToString();
            
            SendUpdateToUI();
        });
        
        AppConfig.OnUpdate += () =>
        {
            _appState.Config = AppConfig.ConfigObject;

            SendUpdateToUI();
        };

        Task.Run(async () =>
        {
            MiniAudioEngine? engine = null;

            List<Mic> mics = new();

            while (true)
            {
                if (AppConfig.ConfigObject.DesktopTranslation && !_appState.IsLinux)
                {
                    await Task.Delay(500);
                    continue;
                }

                engine ??= new MiniAudioEngine(backendPriority: [MiniAudioBackend.Wasapi, MiniAudioBackend.Oss]);
                engine.UpdateAudioDevicesInfo();
                foreach (var mic in engine.CaptureDevices)
                {
                    mics.Add(new Mic { Name = mic.Name, Default = mic.IsDefault });
                }
                
                if (_appState.Microphones.Length != 0 && mics.Count != _appState.Microphones.Length)
                {
                    _appState.Microphones = mics.ToArray();
                    
                    SendUpdateToUI();
                }

                if (_appState.Config.Microphone.Length != 0 && !mics.Exists(m => m.Name == _appState.Config.Microphone))
                {
                    var device = engine.CaptureDevices.FirstOrDefault(d => d.IsDefault);
                    if (device == null) device = engine.CaptureDevices[0];
                    
                    Log.Warning($"[MIC]  The selected mic ({AppConfig.ConfigObject.Microphone}) is not available. Switching to the system default ({device.Name})");
                    
                    AppConfig.ConfigObject.Microphone = device.Name;
                    
                    SendMicChanged();
                    RestartIfRunning();
                }
                
                
                _appState.Microphones = mics.ToArray();
                
                mics.Clear();

                await Task.Delay(500);
            }
        });
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_running)
            {
                RestartCore();
                return;
            }

            StartCore();
        }
    }

    private void StartCore()
    {
        Log.Information($"[APP] Start requested: running={_running}, recognizer={AppConfig.ConfigObject.Recognizer}, translator={AppConfig.ConfigObject.Translator}, desktop={AppConfig.ConfigObject.DesktopTranslation}, chatbox={AppConfig.ConfigObject.SendToChatbox}");
        var desktopMode = AppConfig.ConfigObject.DesktopTranslation && !_appState.IsLinux;

        if (AppConfig.ConfigObject.Translator == 1 && !desktopMode)
        {
            if (string.IsNullOrEmpty(AppConfig.ConfigObject.GroqApiKey))
            {
                Log.Error("[GROQ] No API key is configured!");
                _errorHandler.OnError("GROQ_NO_API_KEY");

                return;
            }
        
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppConfig.ConfigObject.GroqApiKey}");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");

            using var response = client.Send(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.Error("[GROQ] Invalid Groq API key!");
                _errorHandler.OnError("GROQ_INVALID_API_KEY");

                return;
            }
            
        }

        if (desktopMode)
        {
            try
            {
                StartDesktopCore();
            }
            catch (Exception e)
            {
                Log.Error(e, "[APP] Desktop pipeline failed to start");
                _desktopKikitan?.Dispose();
                _desktopKikitan = null;
                _errorHandler.OnError($"Desktop pipeline failed to start: {e.Message}");
            }

            _running = _desktopKikitan != null;
            Log.Information("[APP] Startup complete.");
            SendUpdateToUI();
            return;
        }

        _oscWatcher = new OscWatcher();
        _oscWatcher.MuteStatusChanged += muted =>
        {
            _appState.IsMuted = muted;
            SendUpdateToUI();
        };
        _oscWatcher.Start();

        IRecognizer rMic;
        if (AppConfig.ConfigObject.Recognizer == 0) rMic = new Bing(_mic);
        else if (AppConfig.ConfigObject.Recognizer == 1) rMic = new GroqRecognizer(_mic);
        else rMic = new Gemini(_mic);

        _microphoneKikitan = new Kikitan(rMic, CreateTranslator(), new ErrorHandler(_connector), false);
        _microphoneKikitan.AddOutput(new Custom(SendRecognitionData, false));
        if (AppConfig.ConfigObject.SendToChatbox)
        {
            var chatbox = new Chatbox();
            
            _microphoneKikitan.AddOutput(new Custom((r, t, f) =>
            {
                if (AppConfig.ConfigObject.DisableWhenMuted && _appState.IsMuted)
                {
                    if (f) Log.Debug("[OSC]  Final chatbox output skipped: VRChat microphone is muted");
                    return;
                }
                
                chatbox.Send(r, t, f);
            }, true));
        }
        
        if (AppConfig.ConfigObject.SendUserData)
        {
            _microphoneKikitan.AddOutput(new OSC("/microphone"));
        }
        
        _microphoneKikitan.OnRecognizerStatusChanged += s =>
        {
            _appState.Status = (int)s;
            
            SendUpdateToUI();
        };
        
        try
        {
            _microphoneKikitan.Start();
            if (rMic.Status() == RecognizerStatus.NotStarted)
            {
                _microphoneKikitan.Dispose();
                _microphoneKikitan = null;
            }

            _running = _microphoneKikitan != null;
            if (!_running)
            {
                _oscWatcher.Dispose();
                _oscWatcher = null;
            }
            Log.Information($"[APP] Startup complete.");
            SendUpdateToUI();
        }
        catch
        {
            StopCore();
            throw;
        }
    }

    private void StartDesktopCore()
    {
        var provider = AppConfig.ConfigObject.DesktopTranslationProvider;
        var key = provider switch
        {
            "google" => AppConfig.ConfigObject.GoogleCloudApiKey,
            "deepl" => AppConfig.ConfigObject.DeepLApiKey,
            "groq" => AppConfig.ConfigObject.GroqApiKey,
            _ => throw new InvalidOperationException($"Unknown desktop translation provider: {provider}")
        };
        if (!AppConfig.ConfigObject.SpeechToTextOnly && string.IsNullOrWhiteSpace(key))
            Log.Warning("[SUBTITLE] {Provider} API key is not configured; recognized text will still be shown", provider);

        if (!_noUI) StartSubtitleWindow();

        IRecognizer rDesktop;
        if (AppConfig.ConfigObject.Recognizer == 0) rDesktop = new Bing(_loopback);
        else if (AppConfig.ConfigObject.Recognizer == 1) rDesktop = new GroqRecognizer(_loopback);
        else rDesktop = new Gemini(_loopback);

        _desktopKikitan = new Kikitan(rDesktop, CreateDesktopTranslator(provider), new ErrorHandler(_connector), true);
        Log.Information($"[APP] Desktop pipeline starting: recognizer={rDesktop.GetType().Name}, subtitleWindow={_subtitleProcess != null}");
        _desktopKikitan.OnRecognizerStatusChanged += s =>
        {
            _appState.Status = (int)s;
            SendUpdateToUI();
        };
        if (!_noUI)
        {
            _desktopKikitan.AddOutput(new Custom((recognized, translated, final) =>
            {
                if (string.IsNullOrWhiteSpace(recognized)) return;
                _subtitleWriter.Write(new DesktopSubtitleResult(recognized, translated, final));
            }, false));
        }

        _desktopKikitan.Start();
        if (rDesktop.Status() == RecognizerStatus.NotStarted)
        {
            _desktopKikitan.Dispose();
            _desktopKikitan = null;
        }
    }

    private void StartSubtitleWindow()
    {
        if (_subtitleProcess is { HasExited: false }) return;

        var path = Path.Combine(AppContext.BaseDirectory, "KikitanTranslator.Subtitles.exe");
        if (!File.Exists(path))
        {
            Log.Warning("[SUBTITLE] Subtitle window executable not found: {Path}", path);
            return;
        }

        var startInfo = new ProcessStartInfo(path) { UseShellExecute = false };
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        _subtitleProcess = Process.Start(startInfo);
    }

    public void Stop()
    {
        lock (_lifecycleLock) StopCore();
    }

    public void RestartIfRunning()
    {
        lock (_lifecycleLock)
        {
            if (_running) RestartCore();
        }
    }

    private void RestartCore()
    {
        Log.Information("[APP] Restart requested");
        _appState.Status = 1;
        StopCore();
        StartCore();
    }

    private void StopCore()
    {
        Log.Information("[APP] Stopping...");
        _running = false;
        var desktop = _desktopKikitan;
        var microphone = _microphoneKikitan;
        _desktopKikitan = null;
        _microphoneKikitan = null;
        try
        {
            microphone?.Dispose();
        }
        finally
        {
            desktop?.Dispose();
            _oscWatcher?.Dispose();
            _oscWatcher = null;
        }
        SendUpdateToUI();
    }

    private static ITranslator CreateTranslator() => AppConfig.ConfigObject.Translator switch
    {
        0 => new GoogleTranslate(),
        1 => new GroqTranslator(),
        _ => new GeminiStub()
    };

    private static ITranslator CreateDesktopTranslator(string provider) => provider switch
    {
        "google" => new GoogleCloudTranslator(),
        "deepl" => new DeepLTranslator(),
        "groq" => new GroqTranslator(),
        _ => throw new InvalidOperationException($"Unknown desktop translation provider: {provider}")
    };

    public void ManualTranslate(string text) => _microphoneKikitan?.ManualTranslate(text);

    public void SendUpdateToUI()
    {
        _connector.Send(
            JsonConvert.SerializeObject(new Message
            {
                Method = "state",
                Data = JsonConvert.SerializeObject(_appState)
            }));
    }

    private void SendRecognitionData(string recognized, string translated, bool final)
    {
        _connector.Send(
            JsonConvert.SerializeObject(new Message
            {
                Method = "recognition",
                Data = JsonConvert.SerializeObject(new RecognitionData { Transcription = recognized, Translation = translated, Final = final })
            }));
    }

    private void SendMicChanged()
    {
        _connector.Send(
            JsonConvert.SerializeObject(new Message
            {
                Method = "mic_changed",
                Data = ""
            }));
    }
        
}
