using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Serilog;

namespace KikitanTranslator.Photino;

public sealed class SubtitleWriter
{
    private readonly object _writeLock = new();

    public void Write(DesktopSubtitleResult result)
    {
        lock (_writeLock)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", "kikitan-desktop-subtitles", PipeDirection.Out);
                pipe.Connect(250);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false));
                writer.WriteLine(JsonSerializer.Serialize(result));
            }
            catch (Exception e) when (e is TimeoutException or IOException)
            {
                if (result.IsFinal) Log.Warning(e, "[SUBTITLE] Subtitle window is unavailable");
            }
        }
    }
}
