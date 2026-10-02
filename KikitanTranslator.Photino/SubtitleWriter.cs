using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Serilog;

namespace KikitanTranslator.Photino;

public sealed class SubtitleWriter : IDisposable
{
    private readonly Channel<DesktopSubtitleResult> _messages = Channel.CreateBounded<DesktopSubtitleResult>(128);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _worker;
    public SubtitleWriter() => _worker = Task.Run(WriteAsync);
    public void Write(DesktopSubtitleResult result)
    {
        if (!_messages.Writer.TryWrite(result) && result.IsFinal)
            Log.Warning("[SUBTITLE] IPC queue full; subtitle delivery delayed or unavailable");
    }
    public void Command(string command) => Write(new("", "", false, Guid.Empty, Command: command));
    public void Status(string state) => Write(new("", "", false, Guid.Empty, State: state));
    public void Error(string error) => Write(new("", "", false, Guid.Empty, Error: error));
    private async Task WriteAsync()
    {
        try
        {
            await foreach (var result in _messages.Reader.ReadAllAsync(_lifetime.Token))
            {
                try
                {
                    await using var pipe = new NamedPipeClientStream(".", $"kikitan-desktop-subtitles-{Environment.ProcessId}",
                        PipeDirection.Out, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(500, _lifetime.Token);
                    await using var writer = new StreamWriter(pipe, new UTF8Encoding(false));
                    await writer.WriteLineAsync(JsonSerializer.Serialize(result).AsMemory(), _lifetime.Token);
                }
                catch (Exception e) when (e is TimeoutException or IOException)
                {
                    if (result.IsFinal) Log.Warning("[SUBTITLE] Subtitle window is unavailable");
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
    public void Dispose()
    {
        _messages.Writer.TryComplete();
        _lifetime.Cancel();
        _worker.GetAwaiter().GetResult();
        _lifetime.Dispose();
    }
}
