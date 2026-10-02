namespace KikitanTranslator.Subtitles;

internal static class SubtitlePalette
{
    public static readonly Color Background = Color.FromArgb(23, 27, 34);
    public static readonly Color Original = Color.FromArgb(186, 197, 212);
    public static readonly Color Translation = Color.FromArgb(241, 244, 249);
}

// Native controls preserve keyboard navigation and DPI behavior. Preview text is entirely local.
internal sealed class SubtitleAppearanceDialog : Form
{
    private readonly NumericUpDown _font;
    private readonly NumericUpDown _history;
    private readonly NumericUpDown _opacity;
    private readonly CheckBox _top;
    private readonly CheckBox _original;
    private readonly CheckBox _translation;
    private readonly CheckBox _locked;
    private readonly CheckBox _click;
    private readonly Label _sampleOriginal = new() { AutoSize = true, ForeColor = SubtitlePalette.Original, Text = "A little clarity makes all the difference." };
    private readonly Label _sampleTranslation = new() { AutoSize = true, ForeColor = SubtitlePalette.Translation, Text = "少しの明瞭さが、大きな違いを生みます。" };
    private readonly FlowLayoutPanel _preview;
    private readonly Font _headingFont = new("Segoe UI", 11, FontStyle.Bold);
    private readonly Font _dialogFont = new("Segoe UI", 10);

    public SubtitleAppearanceDialog(SubtitlePreferences settings)
    {
        Text = "Desktop Translator — Subtitle appearance";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = _dialogFont;
        Size = new Size(640, 700);
        MinimumSize = new Size(560, 580);
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false; TopMost = true;
        BackColor = SystemColors.Window;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(22) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 16, 12) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        scroll.Controls.Add(content); root.Controls.Add(scroll, 0, 0);
        void Full(Control control)
        {
            var row = content.RowCount++; content.Controls.Add(control, 0, row); content.SetColumnSpan(control, 2);
        }
        void Heading(string text) => Full(new Label { Text = text, AutoSize = true, Font = _headingFont, Margin = new Padding(0, 16, 0, 10) });
        Heading("Bilingual subtitles");
        _preview = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoSize = true, Dock = DockStyle.Top, BackColor = SubtitlePalette.Background, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 6) };
        _preview.Controls.Add(_sampleOriginal); _preview.Controls.Add(_sampleTranslation); Full(_preview);
        Full(new Label { Text = "Local sample · font size and text visibility update here as you edit.", AutoSize = true, ForeColor = SystemColors.GrayText });
        Heading("Text and history");
        NumericUpDown Number(string text, decimal value, decimal min, decimal max)
        {
            var row = content.RowCount++;
            content.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 8, 7) }, 0, row);
            var number = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Fill, AccessibleName = text, Margin = new Padding(0, 5, 0, 5) };
            content.Controls.Add(number, 1, row); return number;
        }
        CheckBox Toggle(string text, bool value)
        {
            var toggle = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 6, 0, 6), AccessibleName = text };
            Full(toggle); return toggle;
        }
        _font = Number("Original font size (points)", settings.FontSize, 10, 36);
        _history = Number("Subtitle history entries", settings.HistoryCount, 3, 50);
        _original = Toggle("Show original text", settings.ShowOriginal);
        _translation = Toggle("Show translation", settings.ShowTranslation);
        Heading("Window behavior");
        _opacity = Number("Window opacity (%)", (decimal)(settings.Opacity * 100), 25, 100);
        Full(new Label { Text = "Opacity affects the entire window, including text. Preview stays opaque for readability.", AutoSize = true, ForeColor = SystemColors.GrayText });
        _top = Toggle("Always on top", settings.AlwaysOnTop);
        _locked = Toggle("Lock position and size", settings.LockPosition);
        _click = Toggle("Click through to the app behind subtitles", settings.ClickThrough);
        Full(new Label { Text = "Use the tray menu or settings to unlock the window or disable click through.", AutoSize = true, ForeColor = SystemColors.GrayText });
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 0, 0) };
        var save = new Button { Text = "Save changes", AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(8, 3, 8, 3) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
        actions.Controls.Add(save); actions.Controls.Add(cancel); root.Controls.Add(actions, 0, 1);
        AcceptButton = save; CancelButton = cancel;
        _font.ValueChanged += (_, _) => UpdatePreview();
        _original.CheckedChanged += (_, _) => UpdatePreview();
        _translation.CheckedChanged += (_, _) => UpdatePreview();
        _preview.SizeChanged += (_, _) => ResizePreview();
        content.SizeChanged += (_, _) => {
            foreach (Control control in content.Controls)
                if (control is Label && content.GetColumnSpan(control) == 2) control.MaximumSize = new Size(Math.Max(100, content.ClientSize.Width - content.Padding.Horizontal), 0);
        };
        UpdatePreview();
    }
    private void ResizePreview()
    {
        var width = Math.Max(100, _preview.ClientSize.Width - _preview.Padding.Horizontal);
        _sampleOriginal.MaximumSize = new Size(width, 0); _sampleTranslation.MaximumSize = new Size(width, 0);
    }
    private void UpdatePreview()
    {
        var originalFont = _sampleOriginal.Font; var translationFont = _sampleTranslation.Font;
        _sampleOriginal.Font = new Font("Segoe UI", (float)_font.Value);
        _sampleTranslation.Font = new Font("Segoe UI Semibold", (float)_font.Value + 2);
        // These preview fonts are created and owned by this dialog.
        if (originalFont != Font) originalFont.Dispose();
        if (translationFont != Font && translationFont != originalFont) translationFont.Dispose();
        _sampleOriginal.Visible = _original.Checked; _sampleTranslation.Visible = _translation.Checked;
        ResizePreview();
    }
    public void ApplyTo(SubtitlePreferences settings)
    {
        settings.FontSize = (int)_font.Value; settings.HistoryCount = (int)_history.Value;
        settings.Opacity = (double)_opacity.Value / 100; settings.AlwaysOnTop = _top.Checked;
        settings.ShowOriginal = _original.Checked; settings.ShowTranslation = _translation.Checked;
        settings.LockPosition = _locked.Checked; settings.ClickThrough = _click.Checked;
    }
    internal void VerifyPreview()
    {
        Show();
        _font.Value = 24;
        if (_sampleOriginal.Font.Size != 24 || _sampleTranslation.Font.Size != 26)
            throw new Exception("Subtitle preview did not update font size");
        _original.Checked = false;
        if (_sampleOriginal.Visible) throw new Exception("Subtitle preview did not hide original text");
        _original.Checked = true; _font.Value = 14;
        Size = MinimumSize;
        PerformLayout();
        if (_sampleTranslation.Width > _preview.ClientSize.Width)
            throw new Exception("Subtitle preview overflows its window");
        var artifactDirectory = Environment.GetEnvironmentVariable("DESKTOP_TRANSLATOR_TEST_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(artifactDirectory))
        {
            Directory.CreateDirectory(artifactDirectory);
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
            bitmap.Save(Path.Combine(artifactDirectory, "subtitle-appearance.png"));
        }
        Hide();
    }
    protected override void Dispose(bool disposing)
    {
        var originalFont = _sampleOriginal.Font; var translationFont = _sampleTranslation.Font;
        base.Dispose(disposing);
        if (disposing) { originalFont.Dispose(); translationFont.Dispose(); _headingFont.Dispose(); _dialogFont.Dispose(); }
    }
}
