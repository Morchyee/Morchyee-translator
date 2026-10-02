using Fleck;
using Photino.NET;
using Serilog;

namespace KikitanTranslator.Photino;

public delegate Task<string?> OnConnectorData(string data, Connector conn);

public sealed class Connector : IDisposable
{
    public PhotinoWindow? WindowHandle;
    private WebSocketServer? _websocketServer;
    private readonly List<IWebSocketConnection> _sockets = [];
    private readonly object _sync = new();
    public event OnConnectorData? OnConnectorData;
    public void StartWebsocket()
    {
        _websocketServer = new WebSocketServer("ws://127.0.0.1:18378");
        _websocketServer.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                // Headless clients are local desktop tools; browser origins cannot control the backend.
                if (!string.IsNullOrEmpty(socket.ConnectionInfo.Origin)) { socket.Close(); return; }
                lock (_sync) _sockets.Add(socket);
            };
            socket.OnClose = () => { lock (_sync) _sockets.Remove(socket); };
            socket.OnMessage = msg => _ = HandleAsync(msg, socket);
        });
    }
    private async Task HandleAsync(string msg, IWebSocketConnection socket)
    {
        try
        {
            if (OnConnectorData == null) return;
            var data = await OnConnectorData(msg, this);
            if (!string.IsNullOrEmpty(data)) await socket.Send(data);
        }
        catch (Exception e) { Log.Warning("[IPC] Client request failed: {ErrorType}", e.GetType().Name); }
    }
    public void Send(string data)
    {
        try
        {
            var window = WindowHandle;
            if (window != null) window.SendWebMessage(data);
            else
            {
                IWebSocketConnection[] sockets;
                lock (_sync) sockets = _sockets.ToArray();
                foreach (var socket in sockets) if (socket.IsAvailable) _ = socket.Send(data);
            }
        }
        catch (Exception e) { Log.Warning("[IPC] State delivery failed: {ErrorType}", e.GetType().Name); }
    }
    public void Dispose()
    {
        WindowHandle = null;
        lock (_sync) { foreach (var socket in _sockets.ToArray()) socket.Close(); _sockets.Clear(); }
        _websocketServer?.Dispose();
    }
}
