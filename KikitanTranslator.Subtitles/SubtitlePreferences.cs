using System.Text.Json;

namespace KikitanTranslator.Subtitles;

internal sealed class SubtitlePreferences
{
    public int FontSize { get; set; } = 14;
    public int HistoryCount { get; set; } = 5;
    public double Opacity { get; set; } = 0.93;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ShowOriginal { get; set; } = true;
    public bool ShowTranslation { get; set; } = true;
    public bool LockPosition { get; set; }
    public bool ClickThrough { get; set; }
    public int X { get; set; } = int.MinValue;
    public int Y { get; set; }
    public int Width { get; set; } = 740;
    public int Height { get; set; } = 320;
    private static string PathName => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Kikitan Translator", "subtitles.json");
    public static SubtitlePreferences Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<SubtitlePreferences>(File.ReadAllText(PathName)) ?? new();
            settings.FontSize = Math.Clamp(settings.FontSize, 10, 36);
            settings.HistoryCount = Math.Clamp(settings.HistoryCount, 3, 50);
            settings.Opacity = double.IsFinite(settings.Opacity) ? Math.Clamp(settings.Opacity, 0.25, 1) : 0.93;
            return settings;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
            File.WriteAllText(PathName + ".tmp", JsonSerializer.Serialize(this));
            File.Move(PathName + ".tmp", PathName, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Could not save subtitle settings. Check access to your application data folder.", "Desktop Translator");
        }
    }
}
