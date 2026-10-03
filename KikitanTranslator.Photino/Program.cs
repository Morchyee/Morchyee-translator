using System.Drawing;
using KikitanTranslator.Base;
using KikitanTranslator.Photino;
using KikitanTranslator.Photino.Handlers;
using KikitanTranslator.Utility;
using Photino.NET;
using Photino.NET.Server;
using Serilog;
using Velopack;
using Velopack.Sources;

public class Program
{
    private static int width = 900;
    private static int height = 600;
    private static int minWidth = 900;
    private static int minHeight = 600;

    private static double oldRatio;
    private static bool windowInitialized;

    [STAThread]
    public static void Main(string[] args)
    {
        using var instance = new Mutex(true, @"Local\DesktopTranslator.Main-" + Environment.UserDomainName + "-" + Environment.UserName, out var firstInstance);
        if (!firstInstance) return;
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#if DEBUG
        string appUrl = "http://localhost:1420";
#else
        var exeDir = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(exeDir);
        PhotinoServer.CreateStaticFileServer(args, out string baseUrl).RunAsync();
        // WebView can reuse a cached index from an older build at the same localhost URL.
        // Fingerprint frontend content independently of product/release version numbers.
        var frontendIndex = Path.Combine(exeDir, "wwwroot", "index.html");
        var frontendId = File.Exists(frontendIndex)
            ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(frontendIndex)))
            : "";
        string appUrl = $"{baseUrl}/index.html?ui={frontendId}";
#endif
        bool noUI = args.Contains("--no-ui");
        
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Logger.Initialize();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Log.CloseAndFlush();
        AppConfig.Load();

        if (OperatingSystem.IsWindows() && !noUI)
            AppConfig.SetDesktopModeForSession(!args.Contains("--legacy-mode"));

        VelopackApp.Build().Run();
        
        using var connector = new Connector();
        using var manager = new Manager(noUI, connector);
        var messageHandler = new MessageHandler();

        messageHandler.RegisterHandler("manual_translate", new ManualTranslate(manager));
        messageHandler.RegisterHandler("control", new Control(manager));
        messageHandler.RegisterHandler("update_config", new UpdateConfig(manager));
        messageHandler.RegisterHandler("send_app_state", new SendState(manager));
        messageHandler.RegisterHandler("quit", new Quit(() => { exit.TrySetResult(); connector.WindowHandle?.Close(); }));
        messageHandler.RegisterHandler("open_url", new OpenURL());
        messageHandler.RegisterHandler("update", new UpdateApp());
        messageHandler.RegisterHandler("fetch", new Fetch());

        if (noUI)
        {
            connector.OnConnectorData += messageHandler.HandleMessage;

            Log.Information("[APP] No UI requested, starting the websocket");
            connector.StartWebsocket();


            
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; 
                exit.TrySetResult();
            };

            exit.Task.GetAwaiter().GetResult();
            return;
        }

        if (!noUI && OperatingSystem.IsWindows() && AppConfig.ConfigObject.DesktopTranslation)
        {

            Thread? settingsThread = null;
            void HandleTrayCommand(string command)
            {
                switch (command)
                {
                    case "start": manager.Start(); break;
                    case "stop": manager.Stop(); break;
                    case "show": manager.ShowSubtitles(); break;
                    case "settings":
                        if (settingsThread is not { IsAlive: true })
                        {
                            settingsThread = new Thread(() =>
                            {
                                try { ShowSettingsWindow(appUrl, connector, messageHandler); }
                                catch (Exception e) { Log.Error("[SETTINGS] Window failed: {ErrorType}", e.GetType().Name); }
                            });
                            settingsThread.IsBackground = true;
                            settingsThread.SetApartmentState(ApartmentState.STA);
                            settingsThread.Start();
                        }
                        break;
                    case "exit": exit.TrySetResult(); break;
                }
            }
            using var trayControl = new DesktopControlServer(HandleTrayCommand);
            try { manager.StartSubtitleWindow(); }
            catch (Exception e)
            {
                Log.Error("[STARTUP] Subtitle window unavailable: {ErrorType}. Opening settings for recovery.", e.GetType().Name);
                ShowSettingsWindow(appUrl, connector, messageHandler);
                return;
            }
            // Support safe product inspection without changing the user's saved startup preference.
            if (AppConfig.ConfigObject.AutoStart && !args.Contains("--start-stopped")) manager.Start();
            if (args.Contains("--settings")) HandleTrayCommand("settings");
            exit.Task.GetAwaiter().GetResult();
            manager.Stop();
            connector.WindowHandle?.Close();
            settingsThread?.Join(1500);
            return;
        }

        ShowSettingsWindow(appUrl, connector, messageHandler);
    }

    private static void ShowSettingsWindow(string appUrl, Connector connector, MessageHandler messageHandler)
    {
        string windowTitle = AppConfig.ConfigObject.DesktopTranslation ? "Desktop Translator" : "Kikitan Translator (Legacy)";

        var iconFile = OperatingSystem.IsWindows() ? "kikitan_logo.ico" : "icon.png";

        var window = new PhotinoWindow()
            .SetTitle(windowTitle)
            .SetUseOsDefaultSize(false)
            .SetMinSize(minWidth, minHeight)
#if DEBUG
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "wwwroot", iconFile))
#else
            .SetContextMenuEnabled(false)
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "wwwroot", iconFile))
#endif
            .SetSize(new Size(minWidth, minHeight))
            .Center()
            .SetResizable(true)
            .SetLogVerbosity(0)
            .RegisterWebMessageReceivedHandler((sender, s) =>
            {
                connector.WindowHandle = (PhotinoWindow)sender!;

                messageHandler.HandleMessage(s, connector);
            })
            .Load(appUrl);
        
        window.WindowCreated += (_, _) =>
        {
            UpdateResolution(window);
            
            window.Center();
            windowInitialized = true;
        };
        
        window.WindowSizeChanged += (_, _) => {
            if (windowInitialized) UpdateResolution(window);
        };

        window.WindowLocationChanged += (_, _) => {
            if (windowInitialized) UpdateResolution(window);
        };

        window.WaitForClose();
        connector.WindowHandle = null;
    }

    static void UpdateResolution(PhotinoWindow window)
    {
        double ratio = (double)window.ScreenDpi / 96;
        if (Math.Abs(ratio - oldRatio) < 0.001) return;
        
        window.MinSize = new Point((int)(minWidth * ratio), (int)(minHeight * ratio));
        window.Size = new Size((int)(width * ratio), (int)(height * ratio));

        oldRatio = ratio;
    }
}
