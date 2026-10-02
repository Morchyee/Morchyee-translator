using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Serilog;

namespace KikitanTranslator.Utility;

public class ConfigObject : INotifyPropertyChanged
{
    // Missing preference follows Windows UI culture. An explicit choice is persisted independently of audio languages.
    [JsonProperty("ui_language")] private string? _uiLanguage;
    [JsonIgnore]
    public string UiLanguage
    {
        get => KikitanTranslator.Resources.DesktopText.Resolve(_uiLanguage);
        set
        {
            if (!KikitanTranslator.Resources.DesktopText.Locales.Contains(value)) throw new ArgumentException("Unsupported interface language.");
            if (_uiLanguage == value) return;
            _uiLanguage = value;
            OnPropertyChanged();
        }
    }
    [JsonProperty("quickstart_viewed")] private bool _quickstartViewed;

    [JsonIgnore]
    public bool QuickstartViewed
    {
        get => _quickstartViewed;
        set
        {
            if (_quickstartViewed != value)
            {
                _quickstartViewed = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("language")] private string _language = "en";

    [JsonIgnore]
    public string Language
    {
        get => _language;
        set
        {
            if (_language != value)
            {
                _language = value;
                OnPropertyChanged();
            }
        }
    }
    [JsonProperty("last_version")] private string _lastVersion = "en";

    [JsonIgnore]
    public string LastVersion
    {
        get => _lastVersion;
        set
        {
            if (_lastVersion != value)
            {
                _lastVersion = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("source_language")] private string _sourceLanguage = "en";

    [JsonIgnore]
    public string SourceLanguage
    {
        get => _sourceLanguage;
        set
        {
            if (_sourceLanguage != value)
            {
                _sourceLanguage = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("target_language")] private string _targetLanguage = "ja";

    [JsonIgnore]
    public string TargetLanguage
    {
        get => _targetLanguage;
        set
        {
            if (_targetLanguage != value)
            {
                _targetLanguage = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("light_mode")] private bool _lightMode = false;

    [JsonIgnore]
    public bool LightMode
    {
        get => _lightMode;
        set
        {
            if (_lightMode != value)
            {
                _lightMode = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("speech_to_text_only")] private bool _speechToTextOnly = false;

    [JsonIgnore]
    public bool SpeechToTextOnly
    {
        get => _speechToTextOnly;
        set
        {
            if (_speechToTextOnly != value)
            {
                _speechToTextOnly = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("microphone")] private string _microphone = string.Empty;

    [JsonIgnore]
    public string Microphone
    {
        get => _microphone;
        set
        {
            if (_microphone != value)
            {
                _microphone = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("translation_only")] private bool _translationOnly = false;

    [JsonIgnore]
    public bool TranslationOnly
    {
        get => _translationOnly;
        set
        {
            if (_translationOnly != value)
            {
                _translationOnly = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("disable_when_muted")] private bool _disableWhenMuted = false;

    [JsonIgnore]
    public bool DisableWhenMuted
    {
        get => _disableWhenMuted;
        set
        {
            if (_disableWhenMuted != value)
            {
                _disableWhenMuted = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("chatbox_wait_per_char_ms")] private int _chatboxWaitPerCharMs = 30;

    [JsonIgnore]
    public int ChatboxWaitPerCharMs
    {
        get => _chatboxWaitPerCharMs;
        set
        {
            if (_chatboxWaitPerCharMs != value)
            {
                _chatboxWaitPerCharMs = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("osc_port")] private int _oscPort = 9000;

    [JsonIgnore]
    public int OscPort
    {
        get => _oscPort;
        set
        {
            if (_oscPort != value)
            {
                _oscPort = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("send_to_chatbox")] private bool _sendToChatbox = true;

    [JsonIgnore]
    public bool SendToChatbox
    {
        get => _sendToChatbox;
        set
        {
            if (_sendToChatbox != value)
            {
                _sendToChatbox = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("send_user_data")] private bool _sendUserData = false;

    [JsonIgnore]
    public bool SendUserData
    {
        get => _sendUserData;
        set
        {
            if (_sendUserData != value)
            {
                _sendUserData = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("recognizer")] private int _recognizer = 0;

    [JsonIgnore]
    public int Recognizer
    {
        get => _recognizer;
        set
        {
            if (_recognizer != value)
            {
                _recognizer = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("translator")] private int _translator = 0;

    [JsonIgnore]
    public int Translator
    {
        get => _translator;
        set
        {
            if (_translator != value)
            {
                _translator = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("desktop_translation")] private bool _desktopTranslation = false;

    [JsonIgnore]
    public bool DesktopTranslation
    {
        get => _desktopTranslation;
        set
        {
            if (_desktopTranslation != value)
            {
                _desktopTranslation = value;
                OnPropertyChanged();
            }
        }
    }

    // Desktop providers are independent from the legacy microphone translator setting.
    [JsonProperty("desktop_translation_provider")] private string _desktopTranslationProvider = "groq";

    [JsonIgnore]
    public string DesktopTranslationProvider
    {
        get => _desktopTranslationProvider;
        set
        {
            if (_desktopTranslationProvider != value)
            {
                _desktopTranslationProvider = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("google_cloud_api_key")] private string _googleCloudApiKey = "";

    [JsonIgnore]
    public string GoogleCloudApiKey
    {
        get => _googleCloudApiKey;
        set
        {
            if (_googleCloudApiKey != value)
            {
                _googleCloudApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("deepl_api_key")] private string _deepLApiKey = "";

    [JsonIgnore]
    public string DeepLApiKey
    {
        get => _deepLApiKey;
        set
        {
            if (_deepLApiKey != value)
            {
                _deepLApiKey = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("send_without_waiting_for_finish")] private bool _sendWithoutWaitingForFinish = false;

    [JsonIgnore]
    public bool SendWithoutWaitingForFinish
    {
        get => _sendWithoutWaitingForFinish;
        set
        {
            if (_sendWithoutWaitingForFinish != value)
            {
                _sendWithoutWaitingForFinish = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("groq_api_key")] private string _groqApiKey = "";

    [JsonIgnore]
    public string GroqApiKey
    {
        get => _groqApiKey;
        set
        {
            if (_groqApiKey != value)
            {
                _groqApiKey = value;
                OnPropertyChanged();
            }
        }
    }
    
    [JsonProperty("gemini_api_key")] private string _geminiApiKey = "";

    [JsonIgnore]
    public string GeminiApiKey
    {
        get => _geminiApiKey;
        set
        {
            if (_geminiApiKey != value)
            {
                _geminiApiKey = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonProperty("auto_start")] private bool _autoStart = true;
    [JsonIgnore]
    public bool AutoStart
    {
        get => _autoStart;
        set { if (_autoStart != value) { _autoStart = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public delegate void OnConfigUpdate();

public static class AppConfig
{
    public static ConfigObject ConfigObject = new();
    public static event OnConfigUpdate? OnUpdate;
    private static string _currentConfigPath = "";
    private static readonly object SaveLock = new();
    public static string? LoadError { get; private set; }
    public static readonly string[] SecretFields = ["groq_api_key", "google_cloud_api_key", "deepl_api_key", "gemini_api_key"];

    public static string GetAppFolder()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kikitan Translator");
        Directory.CreateDirectory(folder);
        return folder; // Keep the existing location for seamless upgrades.
    }
    public static void Load() => Load(Path.Combine(GetAppFolder(), "config.json"));
    public static void Load(string configPath)
    {
        ConfigObject.PropertyChanged -= OnConfigPropertyChanged;
        _currentConfigPath = configPath;
        LoadError = null;
        ConfigObject = new();
        try
        {
            if (File.Exists(configPath))
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(configPath));
                foreach (var field in SecretFields)
                    if (json[field]?.Type == Newtonsoft.Json.Linq.JTokenType.String)
                        json[field] = CredentialProtection.Unprotect((string)json[field]!);
                ConfigObject = json.ToObject<ConfigObject>() ?? throw new JsonException("Empty configuration");
            }
            SaveConfig(); // Atomically migrate plaintext credentials only after successful load/protection.
        }
        catch (Exception e)
        {
            LoadError = "Could not load or migrate configuration. The original file was preserved. Check file permissions and Windows credentials before editing settings.";
            Log.Error("[CFG] Configuration unavailable: {ErrorType}. Original file preserved.", e.GetType().Name);
        }
        ConfigObject.PropertyChanged += OnConfigPropertyChanged;
    }
    public static Newtonsoft.Json.Linq.JObject PublicConfig()
    {
        var config = Newtonsoft.Json.Linq.JObject.FromObject(ConfigObject);
        config["ui_language"] = ConfigObject.UiLanguage;
        foreach (var field in SecretFields)
        {
            config[field + "_configured"] = !string.IsNullOrWhiteSpace((string?)config[field]);
            config[field] = "";
        }
        return config;
    }
    public static void SetDesktopModeForSession(bool enabled)
    {
        ConfigObject.PropertyChanged -= OnConfigPropertyChanged;
        ConfigObject.DesktopTranslation = enabled;
        ConfigObject.PropertyChanged += OnConfigPropertyChanged;
    }
    public static void SaveConfig()
    {
        lock (SaveLock)
        {
            if (LoadError != null) throw new InvalidOperationException(LoadError);
            var json = Newtonsoft.Json.Linq.JObject.FromObject(ConfigObject);
            foreach (var field in SecretFields)
                json[field] = CredentialProtection.Protect((string?)json[field] ?? "");
            var directory = Path.GetDirectoryName(Path.GetFullPath(_currentConfigPath))!;
            Directory.CreateDirectory(directory);
            var temp = _currentConfigPath + ".tmp";
            File.WriteAllText(temp, json.ToString(Formatting.Indented));
            File.Move(temp, _currentConfigPath, true);
        }
    }
    private static void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SaveConfig();
        OnUpdate?.Invoke();
    }
}
