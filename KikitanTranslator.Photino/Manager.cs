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

    private Loopback _loopback;
    private Microphone _mic;
    private AppState _appState = new () { Microphones = [] };
    private IErrorHandler _errorHandler;
    private bool _running;
    private readonly object _lifecycleLock = new();

    private OverlayWriter? _writer;

    private Connector _connector;

    public DeviceInfo[] GetMicrophones() => _mic.GetCaptureDevices();

    public Manager(bool noUI, Connector connector)
    {
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

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !noUI)
        {
            _writer = new();
            
            if (!Path.Exists("KikitanTranslator.Overlay.exe"))
            {
                Log.Warning("Kikitan Overlay doesn't exist! Perhaps a debug build?");
            }
            else
            {
                Process proc = new Process();
                proc.StartInfo.FileName = "KikitanTranslator.Overlay.exe";
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.UseShellExecute = false;
                proc.Start();
            }
        }

        var oscWatcher = new OscWatcher();
        oscWatcher.MuteStatusChanged += muted =>
        {
            _appState.IsMuted = muted;
            
            SendUpdateToUI();
        };
        oscWatcher.Start();;
        
        Task.Run(async () =>
        {
            var engine = new MiniAudioEngine(backendPriority: [MiniAudioBackend.Wasapi, MiniAudioBackend.Oss]);

            List<Mic> mics = new();

            while (true)
            {
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

        if (AppConfig.ConfigObject.Translator == 1)
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

            if (AppConfig.ConfigObject.DesktopTranslation && !_appState.IsLinux)
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
            }

            _running = _microphoneKikitan != null || _desktopKikitan != null;
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
        IRecognizer rDesktop;
        if (AppConfig.ConfigObject.Recognizer == 0) rDesktop = new Bing(_loopback);
        else if (AppConfig.ConfigObject.Recognizer == 1) rDesktop = new GroqRecognizer(_loopback);
        else rDesktop = new Gemini(_loopback);

        _desktopKikitan = new Kikitan(rDesktop, CreateTranslator(), new ErrorHandler(_connector), true);
        Log.Information($"[APP] Desktop pipeline starting: recognizer={rDesktop.GetType().Name}, overlayAvailable={_writer != null}");
        if (_writer != null)
        {
            _desktopKikitan.AddOutput(new Custom((recognized, translated, final) =>
            {
                var text = AppConfig.ConfigObject.SpeechToTextOnly ? recognized : translated;
                var time = text.Length * AppConfig.ConfigObject.ChatboxWaitPerCharMs;

                if (text.Trim().Length == 0) return;
                Log.Debug($"[LOOP] Writing overlay output: chars={text.Length}, final={final}, durationMs={Math.Max(5000, time)}");

                _writer.Write(new OverlayPipeData { Text = text, NoLanguageSpace =
                    (AppConfig.ConfigObject.SourceLanguage == "ja" || AppConfig.ConfigObject.SourceLanguage == "ko" ||
                     AppConfig.ConfigObject.SourceLanguage == "cn"), Time = time < 5000 ? 5000 : time});
            }, false));
        }

        if (AppConfig.ConfigObject.SendUserData)
        {
            _desktopKikitan.AddOutput(new OSC("/desktop"));
        }

        _desktopKikitan.Start();
        if (rDesktop.Status() == RecognizerStatus.NotStarted)
        {
            _desktopKikitan.Dispose();
            _desktopKikitan = null;
        }
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
        }
        SendUpdateToUI();
    }

    private static ITranslator CreateTranslator() => AppConfig.ConfigObject.Translator switch
    {
        0 => new GoogleTranslate(),
        1 => new GroqTranslator(),
        _ => new GeminiStub()
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
