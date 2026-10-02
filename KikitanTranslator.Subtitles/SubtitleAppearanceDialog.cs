using KikitanTranslator.Resources;

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
    private Font _headingFont = new("Segoe UI", 11, FontStyle.Bold);
    private Font _dialogFont = new("Segoe UI", 10);
    private readonly Dictionary<string, (Font Body, Font Heading)> _localeFonts = new();

    private string _locale;
    private string T(string key) => DesktopText.Get(_locale, key);

    public SubtitleAppearanceDialog(SubtitlePreferences settings, string? locale = null)
    {
        _locale = DesktopText.Resolve(locale);
        _localeFonts["en"] = (_dialogFont, _headingFont);
        Text = "Desktop Translator — " + T("native.appearanceTitle");
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
        void Heading(string text) => Full(new Label { Text = T(text), Tag = text, AutoSize = true, Font = _headingFont, Margin = new Padding(0, 16, 0, 10) });
        Heading("subtitles.bilingual");
        _preview = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoSize = true, Dock = DockStyle.Top, BackColor = SubtitlePalette.Background, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 6) };
        _preview.Controls.Add(_sampleOriginal); _preview.Controls.Add(_sampleTranslation); Full(_preview);
        Full(new Label { Text = T("native.sampleHelp"), Tag = "native.sampleHelp", AutoSize = true, ForeColor = SystemColors.GrayText });
        Heading("native.textHistory");
        NumericUpDown Number(string text, decimal value, decimal min, decimal max)
        {
            var row = content.RowCount++;
            content.Controls.Add(new Label { Text = T(text), Tag = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 8, 7) }, 0, row);
            var number = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Fill, AccessibleName = T(text), Tag = text, Margin = new Padding(0, 5, 0, 5) };
            content.Controls.Add(number, 1, row); return number;
        }
        CheckBox Toggle(string text, bool value)
        {
            var toggle = new CheckBox { Text = T(text), Tag = text, Checked = value, AutoSize = true, Margin = new Padding(0, 6, 0, 6), AccessibleName = T(text) };
            Full(toggle); return toggle;
        }
        _font = Number("native.font", settings.FontSize, 10, 36);
        _history = Number("native.history", settings.HistoryCount, 3, 50);
        _original = Toggle("native.original", settings.ShowOriginal);
        _translation = Toggle("native.translation", settings.ShowTranslation);
        Heading("subtitles.behavior");
        _opacity = Number("native.opacity", (decimal)(settings.Opacity * 100), 25, 100);
        Full(new Label { Text = T("native.opacityHelp"), Tag = "native.opacityHelp", AutoSize = true, ForeColor = SystemColors.GrayText });
        _top = Toggle("native.top", settings.AlwaysOnTop);
        _locked = Toggle("native.lock", settings.LockPosition);
        _click = Toggle("native.click", settings.ClickThrough);
        Full(new Label { Text = T("native.unlockHelp"), Tag = "native.unlockHelp", AutoSize = true, ForeColor = SystemColors.GrayText });
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 0, 0) };
        var save = new Button { Text = T("common.saveChanges"), Tag = "common.saveChanges", AutoSize = true, DialogResult = DialogResult.OK, Padding = new Padding(8, 3, 8, 3) };
        var cancel = new Button { Text = T("common.cancel"), Tag = "common.cancel", AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(8, 3, 8, 3) };
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
        ApplyLocale(_locale);
        UpdatePreview();
    }
    public void ApplyLocale(string locale)
    {
        _locale = DesktopText.Resolve(locale);
        Text = "Desktop Translator — " + T("native.appearanceTitle");
        // WinForms can retain the original Font when an equal value is assigned.
        // Keep every assigned font alive until this dialog is disposed.
        if (!_localeFonts.TryGetValue(_locale, out var fonts))
        {
            fonts = (new Font(DesktopText.FontFamily(_locale), 10), new Font(DesktopText.FontFamily(_locale), 11, FontStyle.Bold));
            _localeFonts[_locale] = fonts;
        }
        _headingFont = fonts.Heading; _dialogFont = fonts.Body;
        SuspendLayout();
        Font = _dialogFont;
        void RefreshText(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Tag is string key)
                {
                    if (control is not NumericUpDown) control.Text = T(key);
                    control.AccessibleName = T(key);
                    if (key is "subtitles.bilingual" or "native.textHistory" or "subtitles.behavior") control.Font = _headingFont;
                }
                RefreshText(control);
            }
        }
        RefreshText(this); ResumeLayout(true);
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
        var initialLocale = _locale;
        foreach (var locale in DesktopText.Locales)
        {
            ApplyLocale(locale);
            if (_original.Text != T("native.original") || _font.AccessibleName != T("native.font") || _font.Value != 14)
                throw new Exception("Appearance localization changed preferences or missed accessible names");
        }
        ApplyLocale(initialLocale);
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
        if (disposing)
        {
            originalFont.Dispose(); translationFont.Dispose();
            foreach (var fonts in _localeFonts.Values) { fonts.Body.Dispose(); fonts.Heading.Dispose(); }
        }
    }
}
