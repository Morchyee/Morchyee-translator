namespace KikitanTranslator.Photino.Handlers;

public class Control(Manager manager) : IHandler
{
    public async Task<string?>  OnDataReceived(string data)
    {
        if (data == "OFF") manager.Stop();
        else if (data == "ON") manager.Start();
        else if (data == "SHOW") manager.ShowSubtitles();
        else if (data == "APPEARANCE") manager.ShowSubtitleAppearance();
        
        manager.SendUpdateToUI();

        return "";
    }
}
