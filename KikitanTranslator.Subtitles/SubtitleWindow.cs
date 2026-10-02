using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

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
    private readonly CancellationTokenSource _readerCancellation = new();
    private readonly NotifyIcon _tray;
    private readonly int? _parentId;

    private bool _allowClose;
    private bool _readerStarted;
    private ToolStripItem? _startItem;
    private ToolStripItem? _stopItem;

    public SubtitleWindow(int? parentId, bool selfTest = false)
    {
        _selfTest = selfTest;
        _preferences = selfTest ? new SubtitlePreferences() : SubtitlePreferences.Load();
        _parentId = parentId;
        _model = new SubtitleHistory(_preferences.HistoryCount);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        Text = "Desktop Translator — Subtitles";
        TopMost = _preferences.AlwaysOnTop;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(450, 190);
        Size = new Size(740, 320);
        BackColor = Color.FromArgb(24, 27, 33);
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
        _history.Padding = new Padding(15, 10, 15, 10);
        _history.BackColor = BackColor;
        Controls.Add(_history);
        _history.Resize += (_, _) => ResizeBlocks();

        var menu = new ContextMenuStrip();
        _startItem = menu.Items.Add("Start translation", null, (_, _) => _ = SendCommandAsync("start"));
        _stopItem = menu.Items.Add("Stop translation", null, (_, _) => _ = SendCommandAsync("stop"));
        menu.Items.Add("Open settings", null, (_, _) => _ = SendCommandAsync("settings"));
        var showHide = menu.Items.Add("Hide subtitles", null, (_, _) =>
        {
            if (Visible) Hide(); else Show();
        });
        VisibleChanged += (_, _) => showHide.Text = Visible ? "Hide subtitles" : "Show subtitles";
        menu.Items.Add("Subtitle appearance…", null, (_, _) => EditPreferences());
        menu.Items.Add("Exit", null, async (_, _) =>
        {
            await SendCommandAsync("exit");
            _allowClose = true;
            Close();
        });
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Desktop Translator — Subtitles",
            ContextMenuStrip = menu,
            Visible = !selfTest
        };
        _tray.DoubleClick += (_, _) => { Show(); Activate(); };

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

    private void ShowSubtitle(DesktopSubtitleResult result)
    {
        if (result.Command == "show") { Show(); Activate(); return; }
        if (result.Command == "hide") { Hide(); return; }
        if (result.Command == "close") { CloseForParent(); return; }
        if (result.State != null)
        {
            _tray.Text = "Desktop Translator — " + result.State;
            if (_startItem != null) _startItem.Enabled = result.State == "Stopped";
            if (_stopItem != null) _stopItem.Enabled = result.State != "Stopped";
            return;
        }
        if (result.Error != null)
        {
            _tray.ShowBalloonTip(5000, "Desktop Translator", result.Error, ToolTipIcon.Warning);
            return;
        }
        if (!_model.Apply(result)) return;
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
        _preferences.Save();
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
        using var dialog = new Form { Text = "Subtitle appearance", Size = new Size(380, 430),
            StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, AutoScaleMode = AutoScaleMode.Dpi, TopMost = true };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(18) };
        dialog.Controls.Add(layout);
        NumericUpDown Number(string label, decimal value, decimal min, decimal max)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true });
            var number = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 300 };
            layout.Controls.Add(number); return number;
        }
        CheckBox Toggle(string label, bool value)
        {
            var toggle = new CheckBox { Text = label, Checked = value, AutoSize = true };
            layout.Controls.Add(toggle); return toggle;
        }
        var font = Number("Font size", _preferences.FontSize, 10, 36);
        var history = Number("History entries (3–50)", _preferences.HistoryCount, 3, 50);
        var opacity = Number("Window opacity (%)", (decimal)(_preferences.Opacity * 100), 25, 100);
        var top = Toggle("Always on top", _preferences.AlwaysOnTop);
        var original = Toggle("Show original", _preferences.ShowOriginal);
        var translation = Toggle("Show translation", _preferences.ShowTranslation);
        var locked = Toggle("Lock position and size", _preferences.LockPosition);
        var click = Toggle("Click through (use tray to change)", _preferences.ClickThrough);
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 300 };
        layout.Controls.Add(save); dialog.AcceptButton = save;
        if (dialog.ShowDialog() != DialogResult.OK) return;
        _preferences.FontSize = (int)font.Value; _preferences.HistoryCount = (int)history.Value;
        _model.Capacity = _preferences.HistoryCount;
        _preferences.Opacity = (double)opacity.Value / 100; _preferences.AlwaysOnTop = top.Checked;
        _preferences.ShowOriginal = original.Checked; _preferences.ShowTranslation = translation.Checked;
        _preferences.LockPosition = locked.Checked; _preferences.ClickThrough = click.Checked;
        Opacity = _preferences.Opacity; TopMost = _preferences.AlwaysOnTop;
        // Preserve controls and current scroll position while applying appearance changes.
        foreach (SubtitleBlock block in _history.Controls) block.ApplyPreferences();
        if (_model.Entries.LastOrDefault() is { } latest) ShowSubtitle(latest);
        SavePreferences();
        RecreateHandle();
    }

    private void VerifyPresentation()
    {
        var lastId = Guid.Empty;
        for (var i = 0; i < 8; i++)
        {
            lastId = Guid.NewGuid();
            ShowSubtitle(new DesktopSubtitleResult(string.Join(" ", Enumerable.Repeat("Long wrapping subtitle 你好", 12)), "", true, lastId));
        }
        if (_history.Controls.Count != 5) throw new Exception("History was not bounded");
        var block = _blocks[lastId];
        ShowSubtitle(new DesktopSubtitleResult("Final original", "Translated text", true, lastId, true));
        if (!ReferenceEquals(_blocks[lastId], block) || _history.Controls.Count != 5) throw new Exception("Translation rebuilt or duplicated its block");
        _history.AutoScrollPosition = Point.Empty;
        ShowSubtitle(new DesktopSubtitleResult("Final original", "A longer translated text", true, lastId, true));
        if (_history.VerticalScroll.Value != 0) throw new Exception("Translation forced scroll to bottom");
        ShowSubtitle(new DesktopSubtitleResult("new source", "", true, Guid.NewGuid()));
        if (_history.VerticalScroll.Value != 0) throw new Exception("New source forced scroll to bottom");
        _history.AutoScrollPosition = new Point(0, Math.Max(0, _history.VerticalScroll.Maximum - _history.VerticalScroll.LargeChange + 1));
        ShowSubtitle(new DesktopSubtitleResult("latest source", "", true, Guid.NewGuid()));
        if (!IsAtBottom()) throw new Exception("New source failed to anchor at bottom");
        Width = 500; ResizeBlocks();
        foreach (SubtitleBlock entry in _history.Controls)
            if (entry.Width > _history.ClientSize.Width) throw new Exception("Subtitle overflows window width");
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
            Margin = new Padding(0, 0, 0, 13);
            _original.ForeColor = Color.White;
            _original.Font = new Font("Segoe UI", _preferences.FontSize);
            _original.AutoSize = true;
            _translated.ForeColor = Color.FromArgb(151, 228, 194);
            _translated.Font = new Font("Segoe UI Semibold", _preferences.FontSize + 1);
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
            _translated.Font = new Font("Segoe UI Semibold", _preferences.FontSize + 1);
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
