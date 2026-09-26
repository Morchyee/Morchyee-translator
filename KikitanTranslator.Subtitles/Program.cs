using System.Diagnostics;

namespace KikitanTranslator.Subtitles;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var parentId = args.Length > 0 && int.TryParse(args[0], out var id) ? id : (int?)null;
        Application.Run(new SubtitleWindow(parentId));
    }
}
