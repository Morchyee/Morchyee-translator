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
#if DEBUG
        string appUrl = "http://localhost:1420";
#else
        var exeDir = AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(exeDir);
        PhotinoServer.CreateStaticFileServer(args, out string baseUrl).RunAsync();
        string appUrl = $"{baseUrl}/index.html";
#endif
        bool noUI = Array.Exists(args, e => e.Trim().Contains("--no-ui"));
        
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Logger.Initialize();
        AppConfig.Load();

        VelopackApp.Build().Run();
        
        var connector = new Connector();
        var manager = new Manager(noUI, connector);
        var messageHandler = new MessageHandler();

        messageHandler.RegisterHandler("manual_translate", new ManualTranslate(manager));
        messageHandler.RegisterHandler("control", new Control(manager));
        messageHandler.RegisterHandler("update_config", new UpdateConfig(manager));
        messageHandler.RegisterHandler("send_app_state", new SendState(manager));
        messageHandler.RegisterHandler("quit", new Quit());
        messageHandler.RegisterHandler("open_url", new OpenURL());
        messageHandler.RegisterHandler("update", new UpdateApp());
        messageHandler.RegisterHandler("fetch", new Fetch());

        if (noUI)
        {
            connector.OnConnectorData += messageHandler.HandleMessage;

            Log.Information("[APP] No UI requested, starting the websocket");
            connector.StartWebsocket();

            var tcs = new TaskCompletionSource();
            
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; 
                tcs.TrySetResult();
            };

            tcs.Task.GetAwaiter().GetResult();
        }

        if (!noUI && OperatingSystem.IsWindows() && AppConfig.ConfigObject.DesktopTranslation)
        {
            var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Thread? settingsThread = null;
            using var trayControl = new DesktopControlServer(command =>
            {
                switch (command)
                {
                    case "start": manager.Start(); break;
                    case "stop": manager.Stop(); break;
                    case "settings":
                        if (settingsThread is not { IsAlive: true })
                        {
                            settingsThread = new Thread(() => ShowSettingsWindow(appUrl, connector, messageHandler));
                            settingsThread.IsBackground = true;
                            settingsThread.SetApartmentState(ApartmentState.STA);
                            settingsThread.Start();
                        }
                        break;
                    case "exit": exit.TrySetResult(); break;
                }
            });
            manager.Start();
            exit.Task.GetAwaiter().GetResult();
            manager.Stop();
            return;
        }

        ShowSettingsWindow(appUrl, connector, messageHandler);
    }

    private static void ShowSettingsWindow(string appUrl, Connector connector, MessageHandler messageHandler)
    {
        string windowTitle = "Kikitan Translator";

        var iconFile = OperatingSystem.IsWindows() ? "kikitan_logo.ico" : "icon.png";

        var window = new PhotinoWindow()
            .SetTitle(windowTitle)
            .SetUseOsDefaultSize(false)
            .SetMinSize(minWidth, minHeight)
#if DEBUG
            .SetIconFile($"Resources/wwwroot/{iconFile}")
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
