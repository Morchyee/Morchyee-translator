using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KikitanTranslator.Utility;

namespace KikitanTranslator.Base.Translators;

public sealed class GroqTranslator : ITranslator
{
    private readonly HttpClient _client;
    public GroqTranslator() : this(new HttpClient()) { }
    public GroqTranslator(HttpClient client) => _client = client;
    public string? Translate(string text, string source, string target)
        => TranslateAsync(text, source, target, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<string?> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        var key = AppConfig.ConfigObject.GroqApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Groq API key is missing.");
        var body = JsonSerializer.Serialize(new
        {
            model = Constants.GROQ_MODEL, temperature = 0.2, max_completion_tokens = 2048,
            messages = new[]
            {
                new { role = "system", content = Constants.GROQ_PROMPT.Replace("LANG_SRC", source).Replace("LANG_TARGET", target) },
                new { role = "user", content = text }
            }
        });
        var json = await TranslationHttp.SendAsync(_client, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return request;
        }, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
    }
    public void Dispose() => _client.Dispose();
}
