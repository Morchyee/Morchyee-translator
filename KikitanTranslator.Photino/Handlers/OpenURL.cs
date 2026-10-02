using System.Diagnostics;
using KikitanTranslator.Utility;

namespace KikitanTranslator.Photino.Handlers;

public class OpenURL : IHandler
{
    public async Task<string?> OnDataReceived(string data)
    {
        if (data == "LOGFILES")
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine([AppConfig.GetAppFolder(), "logs"]) + "\\",
                UseShellExecute = true,
                Verb = "open"
            });

            return null;
        }
        
        if (!Uri.TryCreate(data, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !new[] { "buymeacoffee.com", "booth.pm", "github.com", "twitter.com", "discord.gg" }.Any(
                host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase))) return null;

        Process.Start(new ProcessStartInfo
        {
            FileName = data,
            UseShellExecute = true
        });

        return null;
    }
}