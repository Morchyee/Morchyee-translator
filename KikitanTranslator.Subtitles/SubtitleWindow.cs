using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using KikitanTranslator.Resources;

namespace KikitanTranslator.Subtitles;

public sealed class SubtitleWindow : Form
{
    private readonly SubtitlePreferences _preferences;
    private readonly bool _selfTest;
    public int TestExitCode { get; private set; }
    private readonly Dictionary<Guid, SubtitleBlock> _blocks = new();
    private readonly SubtitleHistory _model;
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 700 };
    private readonly FlowLayoutPanel _history = new BufferedHistory();
    private readonly Label _emptyState = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SubtitlePalette.Original };
    private readonly CancellationTokenSource _readerCancellation = new();
    private readonly NotifyIcon _tray;
    private readonly int? _parentId;

    private bool _allowClose;
    private bool _readerStarted;
    private ToolStripItem? _startItem;
    private ToolStripItem? _stopItem;
    private SubtitleAppearanceDialog? _appearanceDialog;
    private string _locale;
    private readonly Dictionary<string, Font> _emptyFonts = new();
    private string _sessionState = "Stopped";
    private string T(string key) => DesktopText.Get(_locale, key);
    private ToolStripItem LocalizedItem(ContextMenuStrip menu, string key, EventHandler action)
    {
        var item = menu.Items.Add(T(key), null, action); item.Tag = key; return item;
    }

    public SubtitleWindow(int? parentId, bool selfTest = false, string? locale = null)
    {
        _locale = DesktopText.Resolve(locale);
        _selfTest = selfTest;
        _preferences = selfTest ? new SubtitlePreferences() : SubtitlePreferences.Load();
        _parentId = parentId;
        _model = new SubtitleHistory(_preferences.HistoryCount);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        Text = "Desktop Translator — " + T("navigation.subtitles");
        TopMost = _preferences.AlwaysOnTop;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(450, 190);
        Size = new Size(740, 320);
        BackColor = SubtitlePalette.Background;
        _emptyState.Font = new Font("Segoe UI", 12);
        _emptyFonts["en"] = _emptyState.Font;
        Opacity = _preferences.Opacity;

        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 40);

        if (_preferences.X != int.MinValue)
        {
            var saved = new Rectangle(_preferences.X, _preferences.Y, Math.Clamp(_preferences.Width, 450, 3000), Math.Clamp(_preferences.Height, 190, 1800));
            var screen = Screen.FromRectangle(saved).WorkingArea;
            Size = new Size(Math.Min(saved.Width, screen.Width), Math.Min(saved.Height, screen.Height));
            Location = new Point(Math.Clamp(saved.X, screen.Left, Math.Max(screen.Left, screen.Right - Width)),
                Math.Clamp(saved.Y, screen.Top, Math.Max(screen.Top, screen.Bottom - Height)));
        }
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SavePreferences(); };
        Move += (_, _) => ScheduleSave();
        ResizeEnd += (_, _) => ScheduleSave();
        _history.Dock = DockStyle.Fill;
        _history.AutoScroll = true;
        _history.FlowDirection = FlowDirection.TopDown;
        _history.WrapContents = false;
        _history.Padding = new Padding(22, 16, 22, 16);
        _history.BackColor = BackColor;
        Controls.Add(_history);
        Controls.Add(_emptyState);
        _emptyState.BringToFront();
        _history.Resize += (_, _) => ResizeBlocks();

        var menu = new ContextMenuStrip();
        _startItem = LocalizedItem(menu, "common.start", (_, _) => _ = SendCommandAsync("start"));
        _stopItem = LocalizedItem(menu, "common.stop", (_, _) => _ = SendCommandAsync("stop"));
        LocalizedItem(menu, "common.settings", (_, _) => _ = SendCommandAsync("settings"));
        var showHide = LocalizedItem(menu, "common.hideSubtitles", (_, _) =>
        {
            if (Visible) Hide(); else Show();
        });
        VisibleChanged += (_, _) => { showHide.Tag = Visible ? "common.hideSubtitles" : "common.showSubtitles"; showHide.Text = T((string)showHide.Tag); };
        LocalizedItem(menu, "native.appearance", (_, _) => EditPreferences());
        LocalizedItem(menu, "common.exit", async (_, _) =>
        {
            await SendCommandAsync("exit");
            _allowClose = true;
            Close();
        });
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Desktop Translator",
            ContextMenuStrip = menu,
            Visible = !selfTest
        };
        _tray.DoubleClick += (_, _) => { Show(); Activate(); };
        ApplyLocale(_locale);

        if (parentId.HasValue)
        {
            var parentTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            parentTimer.Tick += (_, _) =>
            {
                try
                {
                    using var parent = Process.GetProcessById(parentId.Value);
                    if (parent.HasExited) CloseForParent();
                }
                catch (ArgumentException)
                {
                    CloseForParent();
                }
            };
            parentTimer.Start();
            FormClosed += (_, _) => parentTimer.Dispose();
        }

        Shown += (_, _) =>
        {
            if (_selfTest)
            {
                BeginInvoke((Action)(() =>
                {
                    try { VerifyPresentation(); Console.WriteLine("PASS subtitle window rendering, update, history and scroll checks"); }
                    catch (Exception e) { TestExitCode = 1; Console.Error.WriteLine("FAIL subtitle window: " + e.Message); }
                    _allowClose = true; Close();
                }));
                return;
            }
            if (_readerStarted) return;
            _readerStarted = true;
            _ = Task.Run(() => ReadSubtitlesAsync(_readerCancellation.Token));
        };
        FormClosing += (_, e) =>
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        };
        FormClosed += (_, _) =>
        {
            _saveTimer.Stop();
            SavePreferences();
            _saveTimer.Dispose();
            _readerCancellation.Cancel();
            _tray.Visible = false;
            _tray.Dispose();
            menu.Dispose();
        };
    }

    private void CloseForParent()
    {
        _allowClose = true;
        Close();
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) foreach (var font in _emptyFonts.Values) font.Dispose();
    }

    private async Task SendCommandAsync(string command)
    {
        if (!_parentId.HasValue) return;
        try
        {
            await using var pipe = new NamedPipeClientStream(".",
                $"kikitan-desktop-control-{_parentId.Value}", PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(1000);
            await using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };
            await writer.WriteLineAsync(command);
        }
        catch (Exception e) when (e is IOException or TimeoutException)
        {
            // The parent may already be exiting.
        }
    }

    private async Task ReadSubtitlesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream($"kikitan-desktop-subtitles-{_parentId ?? 0}",
                    PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) continue;

                var result = JsonSerializer.Deserialize<DesktopSubtitleResult>(line);
                if (result != null && IsHandleCreated && !IsDisposed)
                    BeginInvoke((Action)(() => ShowSubtitle(result)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                // A disconnected writer does not stop the subtitle window.
            }
            catch (InvalidOperationException) when (IsDisposed || Disposing) { break; }
            catch (JsonException)
            {
                // Ignore a malformed message and accept the next one.
            }
        }
    }

    private void ApplyLocale(string locale)
    {
        _locale = DesktopText.Resolve(locale);
        Text = "Desktop Translator — " + T("navigation.subtitles");
        if (!_emptyFonts.TryGetValue(_locale, out var font))
            _emptyFonts[_locale] = font = new Font(DesktopText.FontFamily(_locale), 12);
        _emptyState.Font = font;
        _emptyState.AccessibleName = T("native.emptyName");
        foreach (ToolStripItem item in _tray.ContextMenuStrip!.Items)
            if (item.Tag is string key) item.Text = T(key);
        _appearanceDialog?.ApplyLocale(_locale);
        UpdateStatusText();
    }
    private void UpdateStatusText()
    {
        var key = _sessionState switch { "Listening" => "native.emptyListening", "Connecting" => "native.emptyConnecting", _ => "native.emptyStopped" };
        _emptyState.Text = T(key);
        var status = _sessionState switch { "Listening" => "common.listening", "Connecting" => "common.connecting", _ => "common.stopped" };
        _tray.Text = "Desktop Translator — " + T(status);
    }
    private void ShowSubtitle(DesktopSubtitleResult result)
    {
        if (result.UiLanguage != null && result.UiLanguage != _locale) ApplyLocale(result.UiLanguage);
        if (result.Command == "appearance") { EditPreferences(); return; }
        if (result.Command == "show") { Show(); Activate(); return; }
        if (result.Command == "hide") { Hide(); return; }
        if (result.Command == "close") { CloseForParent(); return; }
        if (result.State != null)
        {
            _sessionState = result.State;
            UpdateStatusText();
            if (_startItem != null) _startItem.Enabled = result.State == "Stopped";
            if (_stopItem != null) _stopItem.Enabled = result.State != "Stopped";
            return;
        }
        if (result.Error != null)
        {
            _tray.ShowBalloonTip(5000, "Desktop Translator", T(DesktopText.ErrorKey(result.Error)) + "\n" + result.Error, ToolTipIcon.Warning);
            return;
        }
        if (!_model.Apply(result)) return;
        _emptyState.Visible = false;
        var follow = IsAtBottom();
        var scrollY = _history.VerticalScroll.Value;
        var removedHeight = 0;
        _history.SuspendLayout();
        if (_blocks.TryGetValue(result.Id, out var existing)) existing.UpdateText(result, BlockWidth());
        else
        {
            var block = new SubtitleBlock(result, BlockWidth(), _preferences);
            _blocks.Add(result.Id, block);
            _history.Controls.Add(block);
        }
        foreach (var id in _blocks.Keys.Where(id => !_model.Entries.Any(e => e.Id == id)).ToArray())
        {
            var old = _blocks[id];
            removedHeight += old.Height + old.Margin.Vertical;
            _history.Controls.Remove(old);
            _blocks.Remove(id);
            old.Dispose();
        }
        _history.ResumeLayout(true);
        _history.AutoScrollPosition = new Point(0, follow
            ? Math.Max(0, _history.VerticalScroll.Maximum - _history.VerticalScroll.LargeChange + 1)
            : Math.Max(0, scrollY - removedHeight));
    }

    private void ScheduleSave()
    {
        if (!IsHandleCreated || WindowState != FormWindowState.Normal) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }
    private void SavePreferences()
    {
        if (_selfTest) return;
        if (WindowState == FormWindowState.Normal)
        {
            _preferences.X = Left; _preferences.Y = Top;
            _preferences.Width = Width; _preferences.Height = Height;
        }
        _preferences.Save(_locale);
    }
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_preferences?.ClickThrough == true) parameters.ExStyle |= 0x00000020 | 0x00080000;
            return parameters;
        }
    }
    protected override void WndProc(ref Message m)
    {
        const int WmSysCommand = 0x0112;
        var command = m.WParam.ToInt64() & 0xfff0;
        if (_preferences?.LockPosition == true && m.Msg == WmSysCommand && (command == 0xf000 || command == 0xf010)) return;
        base.WndProc(ref m);
    }
    private void EditPreferences()
    {
        if (_appearanceDialog != null) { _appearanceDialog.Activate(); return; }
        using var dialog = new SubtitleAppearanceDialog(_preferences, _locale);
        _appearanceDialog = dialog;
        try
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            dialog.ApplyTo(_preferences);
            _model.Capacity = _preferences.HistoryCount;
            Opacity = _preferences.Opacity; TopMost = _preferences.AlwaysOnTop;
            // Preserve controls and current scroll position while applying appearance changes.
            foreach (SubtitleBlock block in _history.Controls) block.ApplyPreferences();
            if (_model.Entries.LastOrDefault() is { } latest) ShowSubtitle(latest);
            SavePreferences();
            RecreateHandle();
        }
        finally { _appearanceDialog = null; }
    }

    private void VerifyPresentation()
    {
        if (!_emptyState.Visible) throw new Exception("Missing subtitle empty state");
        foreach (var state in new[] { "Connecting", "Listening", "Stopped" })
        {
            ShowSubtitle(new DesktopSubtitleResult("", "", false, Guid.Empty, State: state));
            var expected = T(state switch { "Listening" => "native.emptyListening", "Connecting" => "native.emptyConnecting", _ => "native.emptyStopped" });
            if (_emptyState.Text != expected || !_emptyState.Visible || _model.Entries.Count != 0)
                throw new Exception("Empty subtitle state does not reflect the active session");
        }
        using (var appearance = new SubtitleAppearanceDialog(new SubtitlePreferences(), _locale))
        {
            appearance.VerifyPreview();
            var settings = new SubtitlePreferences(); appearance.ApplyTo(settings);
            if (settings.FontSize != 14 || settings.HistoryCount != 5) throw new Exception("Appearance defaults changed");
        }
        var lastId = Guid.Empty;
        for (var i = 0; i < 8; i++)
        {
            lastId = Guid.NewGuid();
            ShowSubtitle(new DesktopSubtitleResult(string.Join(" ", Enumerable.Repeat("Long wrapping subtitle 你好", 12)), "", true, lastId));
        }
        if (_emptyState.Visible) throw new Exception("Empty state covered active subtitles");
        if (_history.Controls.Count != 5) throw new Exception("History was not bounded");
        var block = _blocks[lastId];
        ShowSubtitle(new DesktopSubtitleResult("Final original", "Translated text", true, lastId, true));
        if (!ReferenceEquals(_blocks[lastId], block) || _history.Controls.Count != 5) throw new Exception("Translation rebuilt or duplicated its block");
        _history.AutoScrollPosition = Point.Empty;
        ShowSubtitle(new DesktopSubtitleResult("Final original", "A longer translated text", true, lastId, true));
        if (_history.VerticalScroll.Value != 0) throw new Exception("Translation forced scroll to bottom");
        ShowSubtitle(new DesktopSubtitleResult("new source", "", true, Guid.NewGuid()));
        if (_history.VerticalScroll.Value != 0) throw new Exception("New source forced scroll to bottom");
        var entries = _model.Entries.ToArray();
        var originalLocale = _locale;
        foreach (var locale in DesktopText.Locales)
        {
            ShowSubtitle(new DesktopSubtitleResult("", "", false, Guid.Empty, State: "Listening", UiLanguage: locale));
            if (_history.VerticalScroll.Value != 0 || !entries.SequenceEqual(_model.Entries) || !ReferenceEquals(_blocks[lastId], block))
                throw new Exception("Interface localization changed subtitle content, controls, or scroll");
            if (_startItem!.Text != T("common.start") || _stopItem!.Text != T("common.stop")) throw new Exception("Tray localization did not update");
        }
        ApplyLocale(originalLocale);
        _history.AutoScrollPosition = new Point(0, Math.Max(0, _history.VerticalScroll.Maximum - _history.VerticalScroll.LargeChange + 1));
        ShowSubtitle(new DesktopSubtitleResult("latest source", "", true, Guid.NewGuid()));
        if (!IsAtBottom()) throw new Exception("New source failed to anchor at bottom");
        Width = 500; ResizeBlocks();
        Application.DoEvents(); // Flush native layout messages in this self-test only.
        _history.PerformLayout();
        foreach (SubtitleBlock entry in _history.Controls)
            if (entry.Width > _history.ClientSize.Width) throw new Exception("Subtitle overflows window width");
        if (_history.HorizontalScroll.Visible) throw new Exception("Subtitle history shows a horizontal scrollbar");
        var artifactDirectory = Environment.GetEnvironmentVariable("DESKTOP_TRANSLATOR_TEST_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(artifactDirectory))
        {
            Directory.CreateDirectory(artifactDirectory);
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
            bitmap.Save(Path.Combine(artifactDirectory, "subtitles.png"));
        }
    }

    private bool IsAtBottom()
    {
        var scroll = _history.VerticalScroll;
        return !scroll.Visible || scroll.Value >= scroll.Maximum - scroll.LargeChange - 3;
    }

    private int BlockWidth() => Math.Max(200, _history.ClientSize.Width - _history.Padding.Horizontal - 25);

    private void ResizeBlocks()
    {
        var follow = IsAtBottom();
        var y = _history.VerticalScroll.Value;
        _history.SuspendLayout();
        foreach (SubtitleBlock block in _history.Controls) block.UpdateWidth(BlockWidth());
        _history.ResumeLayout(true);
        _history.AutoScrollPosition = new Point(0, follow ? Math.Max(0, _history.VerticalScroll.Maximum - _history.VerticalScroll.LargeChange + 1) : y);
    }

    private sealed class BufferedHistory : FlowLayoutPanel
    {
        public BufferedHistory() { DoubleBuffered = true; }
        protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;
    }
    private sealed class SubtitleBlock : Panel
    {
        private readonly Label _original = new();
        private readonly Label _translated = new();

        private readonly SubtitlePreferences _preferences;
        public SubtitleBlock(DesktopSubtitleResult result, int width, SubtitlePreferences preferences)
        {
            _preferences = preferences;
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Margin = new Padding(0, 0, 0, 18);
            _original.ForeColor = SubtitlePalette.Original;
            _original.Font = new Font("Segoe UI", _preferences.FontSize);
            _original.AutoSize = true;
            _translated.ForeColor = SubtitlePalette.Translation;
            _translated.Font = new Font("Segoe UI Semibold", _preferences.FontSize + 2);
            _translated.AutoSize = true;
            Controls.Add(_original);
            Controls.Add(_translated);
            ApplyPreferences();
            UpdateText(result, width);
        }

        public void ApplyPreferences()
        {
            var oldOriginal = _original.Font; var oldTranslated = _translated.Font;
            _original.Font = new Font("Segoe UI", _preferences.FontSize);
            _translated.Font = new Font("Segoe UI Semibold", _preferences.FontSize + 2);
            oldOriginal.Dispose(); oldTranslated.Dispose();
            _original.Visible = _preferences.ShowOriginal;
            _translated.Visible = _preferences.ShowTranslation;
            UpdateWidth(Width);
        }
        protected override void Dispose(bool disposing)
        {
            var originalFont = _original.Font; var translatedFont = _translated.Font;
            base.Dispose(disposing);
            if (disposing) { originalFont.Dispose(); translatedFont.Dispose(); }
        }
        public void UpdateText(DesktopSubtitleResult result, int width)
        {
            SuspendLayout();
            _original.Text = result.OriginalText;
            _translated.Text = result.TranslatedText;
            UpdateWidth(width);
            ResumeLayout(true);
        }

        public void UpdateWidth(int width)
        {
            Width = width;
            _original.MaximumSize = new Size(width, 0);
            _translated.MaximumSize = new Size(width, 0);
            _original.Location = Point.Empty;
            _translated.Location = new Point(0, (_preferences.ShowOriginal ? _original.PreferredHeight + 4 : 0));
            Height = Math.Max(20, _translated.Top + (_preferences.ShowTranslation && _translated.Text.Length > 0 ? _translated.PreferredHeight : 0));
        }
    }
}
