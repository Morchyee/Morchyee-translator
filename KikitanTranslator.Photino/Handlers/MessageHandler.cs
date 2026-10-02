using Newtonsoft.Json;
using Photino.NET;
using Serilog;

namespace KikitanTranslator.Photino.Handlers;

public class Message
{
    [JsonProperty("method")] public string Method;
    [JsonProperty("data")] public string Data;
}

public class MessageHandler
{
    private Dictionary<string, IHandler> _handlers = new();

    public void RegisterHandler(string methodName, IHandler handler) => _handlers.Add(methodName, handler);

    public async Task<string?> HandleMessage(string msg, Connector conn)
    {
        try
        {
            Message? m = JsonConvert.DeserializeObject<Message>(msg);
            if (m == null)
            {
                Log.Error($"[MSGH] Unable to deserialize message");
                
                return null;
            }

            foreach (var handler in _handlers)
            {
                if (handler.Key == m.Method)
                {
                    var d = await handler.Value.OnDataReceived(m.Data);
                    if (!string.IsNullOrEmpty(d)) conn.Send(JsonConvert.SerializeObject(new Message { Method = m.Method, Data = d }));
                    
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("[MSGH] Message handling failed: {ErrorType}", e.GetType().Name);
            new ErrorHandler(conn).OnError(e is InvalidOperationException ? e.Message : "Could not apply settings. Check configuration and file permissions.");
        }

        return null;
    }
}