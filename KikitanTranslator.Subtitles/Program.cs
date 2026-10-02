using System.Diagnostics;

namespace KikitanTranslator.Subtitles;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--self-test"))
        {
            using var testWindow = new SubtitleWindow(null, true);
            Application.Run(testWindow);
            return testWindow.TestExitCode;
        }
        using var instance = new Mutex(true, @"Local\DesktopTranslator.Subtitles-" + Environment.UserDomainName + "-" + Environment.UserName, out var first);
        if (!first) return 0;
        var parentId = args.Length > 0 && int.TryParse(args[0], out var id) ? id : (int?)null;
        Application.Run(new SubtitleWindow(parentId));
        return 0;
    }
}
