using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;

namespace KikitanTranslator.Base.Translators;

public sealed class DeepLTranslator : ITranslator
{
    private readonly HttpClient _client;
    public DeepLTranslator() : this(new HttpClient()) { }
    public DeepLTranslator(HttpClient client) => _client = client;
    public string? Translate(string text, string source, string target)
        => TranslateAsync(text, source, target, CancellationToken.None).GetAwaiter().GetResult();
    public async Task<string?> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        var key = AppConfig.ConfigObject.DeepLApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("DeepL API key is missing.");
        var host = key.EndsWith(":fx", StringComparison.OrdinalIgnoreCase) ? "https://api-free.deepl.com" : "https://api.deepl.com";
        var body = JsonSerializer.Serialize(new
        {
            text = new[] { text }, source_lang = source.ToUpperInvariant(), target_lang = target.ToUpperInvariant()
        });
        var json = await TranslationHttp.SendAsync(_client, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, host + "/v2/translate");
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return request;
        }, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
    }
    public void Dispose() => _client.Dispose();
}
