namespace KikitanTranslator.Photino.Handlers;

public class Quit(Action requestExit) : IHandler
{
    public Task<string?> OnDataReceived(string data)
    {
        requestExit();
        return Task.FromResult<string?>(null);
    }
}
