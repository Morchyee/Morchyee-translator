using System.Net;

namespace KikitanTranslator.Base.Translators;

internal static class TranslationHttp
{
    private sealed class Cooldown
    {
        public DateTimeOffset Until;
        public string Message = "";
    }
    // Weak ownership: disposed/replaced provider clients must not accumulate in a static registry.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HttpClient, Cooldown> Cooldowns = new();
    // One budget covers retries and response parsing. Never include URLs, bodies or credentials in errors.
    public static async Task<string> SendAsync(HttpClient client, Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        var cooldown = Cooldowns.GetOrCreateValue(client);
        lock (cooldown)
            if (cooldown.Until > DateTimeOffset.UtcNow) throw new InvalidOperationException(cooldown.Message);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        for (var attempt = 0; ; attempt++)
        {
            using var request = createRequest();
            using var response = await client.SendAsync(request, budget.Token);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(budget.Token);

            var status = (int)response.StatusCode;
            var retryable = status == 429 || status >= 500;
            var delay = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(attempt + 1);
            // Long cooldowns are unsuitable for realtime captions. Fail rather than retrying before
            // the provider's Retry-After or keeping stale speech in flight for its entire budget.
            if (retryable && delay > TimeSpan.FromSeconds(2))
            {
                var message = status == 429
                    ? "Provider rate limit reached. Try again later."
                    : "Translation provider is temporarily unavailable. Try again later.";
                lock (cooldown)
                {
                    cooldown.Until = DateTimeOffset.UtcNow + delay;
                    cooldown.Message = message;
                }
                throw new InvalidOperationException(message);
            }
            if (!retryable || attempt >= 2)
                throw new InvalidOperationException(status switch
                {
                    401 or 403 => "Provider rejected credentials or access. Check the API key and account permissions.",
                    429 => "Provider rate limit reached. Try again later.",
                    456 => "Provider quota exhausted. Check account usage.",
                    400 => "Provider rejected the request. Check the selected languages and model.",
                    _ => $"Translation provider returned HTTP {status}."
                });
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(delay.TotalSeconds, 0.2)), budget.Token);
        }
    }
}
