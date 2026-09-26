using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace KikitanTranslator.Subtitles;

public sealed class SubtitleWindow : Form
{
    private const int MaxEntries = 5;
    private readonly FlowLayoutPanel _history = new();
    private readonly CancellationTokenSource _readerCancellation = new();
    private readonly NotifyIcon _tray;
    private readonly int? _parentId;
    private SubtitleBlock? _current;
    private bool _allowClose;

    public SubtitleWindow(int? parentId)
    {
        _parentId = parentId;
        Text = "Desktop Subtitles";
        TopMost = true;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(450, 190);
        Size = new Size(740, 320);
        BackColor = Color.FromArgb(24, 27, 33);
        Opacity = 0.93;

        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 40);

        _history.Dock = DockStyle.Fill;
        _history.AutoScroll = true;
        _history.FlowDirection = FlowDirection.TopDown;
        _history.WrapContents = false;
        _history.Padding = new Padding(15, 10, 15, 10);
        _history.BackColor = BackColor;
        Controls.Add(_history);
        _history.Resize += (_, _) => ResizeBlocks();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Start translation", null, (_, _) => _ = SendCommandAsync("start"));
        menu.Items.Add("Stop translation", null, (_, _) => _ = SendCommandAsync("stop"));
        menu.Items.Add("Open settings", null, (_, _) => _ = SendCommandAsync("settings"));
        var showHide = menu.Items.Add("Hide subtitles", null, (_, _) =>
        {
            if (Visible) Hide(); else Show();
        });
        VisibleChanged += (_, _) => showHide.Text = Visible ? "Hide subtitles" : "Show subtitles";
        menu.Items.Add("Exit", null, async (_, _) =>
        {
            await SendCommandAsync("exit");
            _allowClose = true;
            Close();
        });
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Desktop Subtitles",
            ContextMenuStrip = menu,
            Visible = true
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

        Shown += (_, _) => _ = Task.Run(() => ReadSubtitlesAsync(_readerCancellation.Token));
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
                $"kikitan-desktop-control-{_parentId.Value}", PipeDirection.Out, PipeOptions.Asynchronous);
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
                await using var pipe = new NamedPipeServerStream("kikitan-desktop-subtitles",
                    PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
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
            catch (JsonException)
            {
                // Ignore a malformed message and accept the next one.
            }
        }
    }

    private void ShowSubtitle(DesktopSubtitleResult result)
    {
        if (string.IsNullOrWhiteSpace(result.OriginalText)) return;

        if (_current == null)
        {
            var wasAtBottom = IsAtBottom();
            _history.SuspendLayout();
            _current = new SubtitleBlock(result, BlockWidth());
            _history.Controls.Add(_current);
            while (_history.Controls.Count > MaxEntries)
            {
                var oldest = _history.Controls[0];
                _history.Controls.RemoveAt(0);
                oldest.Dispose();
            }
            _history.ResumeLayout(true);
            if (wasAtBottom) _history.ScrollControlIntoView(_current);
        }
        else
        {
            // Partials and the final translation belong to the same block.
            // Updating a block never moves the viewport.
            var scrollY = _history.VerticalScroll.Value;
            _history.SuspendLayout();
            _current.UpdateText(result, BlockWidth());
            _history.ResumeLayout(true);
            _history.AutoScrollPosition = new Point(0, scrollY);
        }

        if (result.IsFinal) _current = null;
    }

    private bool IsAtBottom()
    {
        var scroll = _history.VerticalScroll;
        return !scroll.Visible || scroll.Value >= scroll.Maximum - scroll.LargeChange - 3;
    }

    private int BlockWidth() => Math.Max(200, _history.ClientSize.Width - _history.Padding.Horizontal - 25);

    private void ResizeBlocks()
    {
        _history.SuspendLayout();
        foreach (SubtitleBlock block in _history.Controls) block.UpdateWidth(BlockWidth());
        _history.ResumeLayout(true);
    }

    private sealed class SubtitleBlock : Panel
    {
        private readonly Label _original = new();
        private readonly Label _translated = new();

        public SubtitleBlock(DesktopSubtitleResult result, int width)
        {
            BackColor = Color.Transparent;
            Margin = new Padding(0, 0, 0, 13);
            _original.ForeColor = Color.White;
            _original.Font = new Font("Segoe UI", 13);
            _original.AutoSize = true;
            _translated.ForeColor = Color.FromArgb(151, 228, 194);
            _translated.Font = new Font("Segoe UI Semibold", 14);
            _translated.AutoSize = true;
            Controls.Add(_original);
            Controls.Add(_translated);
            UpdateText(result, width);
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
            _translated.Location = new Point(0, _original.PreferredHeight + 4);
            Height = Math.Max(76, _translated.Top + Math.Max(_translated.PreferredHeight, 25));
        }
    }
}

internal sealed record DesktopSubtitleResult(string OriginalText, string TranslatedText, bool IsFinal);
