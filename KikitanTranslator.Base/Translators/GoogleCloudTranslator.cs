using System.Net;
using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;
using Serilog;

namespace KikitanTranslator.Base.Translators;

// Google Cloud Translation Basic (v2), not the legacy web Translate endpoint.
public sealed class GoogleCloudTranslator : ITranslator
{
    private readonly HttpClient _client = new();

    public string? Translate(string text, string source, string target)
        => TranslateAsync(text, source, target).GetAwaiter().GetResult();

    private async Task<string?> TranslateAsync(string text, string source, string target)
    {
        var key = AppConfig.ConfigObject.GoogleCloudApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            Log.Warning("[GOOGLE CLOUD] API key is not configured");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                "https://translation.googleapis.com/language/translate/v2?key=" + Uri.EscapeDataString(key));
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                q = text,
                source,
                target,
                format = "text"
            }), Encoding.UTF8, "application/json");
            using var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("[GOOGLE CLOUD] Translation failed with HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var result = json.RootElement.GetProperty("data").GetProperty("translations")[0]
                .GetProperty("translatedText").GetString();
            return result == null ? null : WebUtility.HtmlDecode(result);
        }
        catch (Exception e)
        {
            Log.Warning(e, "[GOOGLE CLOUD] Translation failed");
            return null;
        }
    }

    public void Dispose() => _client.Dispose();
}
