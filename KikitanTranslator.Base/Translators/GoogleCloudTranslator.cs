using System.Net;
using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;

namespace KikitanTranslator.Base.Translators;

public sealed class GoogleCloudTranslator : ITranslator
{
    private readonly HttpClient _client;
    public GoogleCloudTranslator() : this(new HttpClient()) { }
    public GoogleCloudTranslator(HttpClient client) => _client = client;
    public string? Translate(string text, string source, string target)
        => TranslateAsync(text, source, target, CancellationToken.None).GetAwaiter().GetResult();
    public async Task<string?> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        var key = AppConfig.ConfigObject.GoogleCloudApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Google Cloud API key is missing.");
        var body = JsonSerializer.Serialize(new { q = text, source, target, format = "text" });
        var json = await TranslationHttp.SendAsync(_client, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://translation.googleapis.com/language/translate/v2");
            request.Headers.Add("X-Goog-Api-Key", key);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return request;
        }, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("data").GetProperty("translations")[0].GetProperty("translatedText").GetString();
        return result == null ? null : WebUtility.HtmlDecode(result);
    }
    public void Dispose() => _client.Dispose();
}
