using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Base.Translators;

public sealed class DeepLTranslator : ITranslator
{
    private readonly HttpClient _client = new();

    public string? Translate(string text, string source, string target)
        => TranslateAsync(text, source, target).GetAwaiter().GetResult();

    private async Task<string?> TranslateAsync(string text, string source, string target)
    {
        var key = AppConfig.ConfigObject.DeepLApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            Log.Warning("[DEEPL] API key is not configured");
            return null;
        }

        var host = key.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
            ? "https://api-free.deepl.com" : "https://api.deepl.com";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, host + "/v2/translate");
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                text = new[] { text },
                source_lang = source.ToUpperInvariant(),
                target_lang = target.ToUpperInvariant()
            }), Encoding.UTF8, "application/json");
            using var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("[DEEPL] Translation failed with HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
        }
        catch (Exception e)
        {
            Log.Warning(e, "[DEEPL] Translation failed");
            return null;
        }
    }

    public void Dispose() => _client.Dispose();
}
