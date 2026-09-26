using System.IO.Pipes;
using System.Text;
using Serilog;

namespace KikitanTranslator.Photino;

internal sealed class DesktopControlServer : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _worker;

    public DesktopControlServer(Action<string> onCommand)
    {
        _worker = Task.Run(async () =>
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(
                        $"kikitan-desktop-control-{Environment.ProcessId}", PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await pipe.WaitForConnectionAsync(_cancellation.Token);
                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    var command = await reader.ReadLineAsync(_cancellation.Token);
                    if (command != null) onCommand(command);
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    Log.Warning(e, "[TRAY] Control command failed");
                }
            }
        });
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _worker.GetAwaiter().GetResult();
        _cancellation.Dispose();
    }
}
