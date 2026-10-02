using KikitanTranslator.Utility;
using Newtonsoft.Json;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class ConfigUpdate
{
    [JsonProperty("field")] public string Field;
    [JsonProperty("value")] public object Value;
    [JsonProperty("request_id")] public long? RequestId;
}

public class UpdateConfig(Manager manager) : IHandler
{
    public async Task<string?> OnDataReceived(string data)
    {
        if (AppConfig.LoadError != null) throw new InvalidOperationException(AppConfig.LoadError);
        bool doNotRestart = false;
        
        var d = JsonConvert.DeserializeObject<ConfigUpdate>(data);
        if (d == null) return "";
        Log.Information($"[CFG]  Configuration update requested: field={d.Field}");

        switch (d.Field)
        {
            case "ui_language":
                AppConfig.ConfigObject.UiLanguage = (string)d.Value;
                doNotRestart = true;
                break;
            case "auto_start":
                AppConfig.ConfigObject.AutoStart = (bool)d.Value;
                doNotRestart = true;
                break;
            case "language":
                AppConfig.ConfigObject.Language = (string) d.Value;
                doNotRestart = true;
                
                break;
            case "source_language":
                if (!Languages.SourceLanguages.ContainsKey((string)d.Value)) throw new InvalidOperationException("Unsupported source language.");
                AppConfig.ConfigObject.SourceLanguage = (string) d.Value;
                
                break;
            case "target_language":
                if (!Languages.TargetLanguages.ContainsKey((string)d.Value)) throw new InvalidOperationException("Unsupported target language.");
                AppConfig.ConfigObject.TargetLanguage = (string) d.Value;
                
                break;
            case "light_mode":
                AppConfig.ConfigObject.LightMode = (bool) d.Value;
                doNotRestart = true;
                
                break;
            case "speech_to_text_only":
                AppConfig.ConfigObject.SpeechToTextOnly = (bool) d.Value;
                
                break;
            case "microphone":
                AppConfig.ConfigObject.Microphone = (string) d.Value;
                
                break;
            case "translation_only":
                AppConfig.ConfigObject.TranslationOnly = (bool) d.Value;
                
                break;
            case "disable_when_muted":
                AppConfig.ConfigObject.DisableWhenMuted = (bool) d.Value;
                
                break;
            case "send_without_waiting_for_finish":
                AppConfig.ConfigObject.SendWithoutWaitingForFinish = (bool) d.Value;
                
                break;
            case "chatbox_wait_per_char_ms":
                AppConfig.ConfigObject.ChatboxWaitPerCharMs = Convert.ToInt32((long) d.Value);
                
                break;
            case "osc_port":
                AppConfig.ConfigObject.OscPort = Convert.ToInt32((long) d.Value);
                
                break;
            case "send_to_chatbox":
                AppConfig.ConfigObject.SendToChatbox = (bool) d.Value;
                
                break;
            case "send_user_data":
                AppConfig.ConfigObject.SendUserData = (bool) d.Value;
                
                break;
            case "recognizer":
                AppConfig.ConfigObject.Recognizer = Convert.ToInt32((long) d.Value);
                
                if (AppConfig.ConfigObject.Recognizer == 2) AppConfig.ConfigObject.Translator = 2;
                else if (AppConfig.ConfigObject.Translator == 2) AppConfig.ConfigObject.Translator = 0;
                
                break;
            case "translator":
                AppConfig.ConfigObject.Translator = Convert.ToInt32((long) d.Value);
                
                if (AppConfig.ConfigObject.Translator == 2) AppConfig.ConfigObject.Recognizer = 2;
                else if (AppConfig.ConfigObject.Recognizer == 2) AppConfig.ConfigObject.Recognizer = 0;
                
                break;
            case "desktop_translation":
                AppConfig.ConfigObject.DesktopTranslation = (bool) d.Value;
                
                break;
            case "desktop_translation_provider":
                var provider = (string)d.Value;
                if (provider is "google" or "deepl" or "groq")
                    AppConfig.ConfigObject.DesktopTranslationProvider = provider;
                else throw new InvalidOperationException("Unsupported translation provider.");
                break;
            case "google_cloud_api_key":
                AppConfig.ConfigObject.GoogleCloudApiKey = (string)d.Value;
                break;
            case "deepl_api_key":
                AppConfig.ConfigObject.DeepLApiKey = (string)d.Value;
                break;
            case "quickstart_viewed":
                AppConfig.ConfigObject.QuickstartViewed = (bool) d.Value;
                doNotRestart = true;
                
                break;
            case "groq_api_key":
                AppConfig.ConfigObject.GroqApiKey = (string) d.Value;
                
                break;
            case "gemini_api_key":
                AppConfig.ConfigObject.GeminiApiKey = (string) d.Value;
                
                break;
            case "last_version":
                AppConfig.ConfigObject.LastVersion = (string) d.Value;
                doNotRestart = true;
                
                break;
            default:
                Log.Warning($"[CFG]  Received an unknown field {d.Field} while trying to update the config!");
                throw new InvalidOperationException("Unsupported setting.");
        }
        
        // A retry with an unchanged in-memory value must still reach durable storage
        // before the settings UI receives its save acknowledgement.
        AppConfig.SaveConfig();
        if (!doNotRestart) manager.RestartIfRunning();

        return JsonConvert.SerializeObject(new { field = d.Field, request_id = d.RequestId, saved = true });
    }
}
