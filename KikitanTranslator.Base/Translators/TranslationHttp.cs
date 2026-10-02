using System.Net;

namespace KikitanTranslator.Base.Translators;

internal static class TranslationHttp
{
    // One budget covers retries and response parsing. Never include URLs, bodies or credentials in errors.
    public static async Task<string> SendAsync(HttpClient client, Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
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
            if (!retryable || attempt >= 2)
                throw new InvalidOperationException(status switch
                {
                    401 or 403 => "Provider rejected credentials or access. Check the API key and account permissions.",
                    429 => "Provider rate limit reached. Try again later.",
                    456 => "Provider quota exhausted. Check account usage.",
                    400 => "Provider rejected the request. Check the selected languages and model.",
                    _ => $"Translation provider returned HTTP {status}."
                });

            var delay = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromSeconds(attempt + 1);
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 0.2, 10)), budget.Token);
        }
    }
}
