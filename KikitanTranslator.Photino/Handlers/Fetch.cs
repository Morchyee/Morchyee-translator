using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class Fetch : IHandler
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    public async Task<string?> OnDataReceived(string data)
    {
        try
        {
            if (!Uri.TryCreate(data, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                uri.Host != "raw.githubusercontent.com") return "";
            return await Client.GetStringAsync(uri);
        } catch (Exception e)
        {
            Log.Warning("[MSGH] Fetch failed: {ErrorType}", e.GetType().Name);

            return "";
        }
    }
}