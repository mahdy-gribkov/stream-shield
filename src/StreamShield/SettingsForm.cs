using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace StreamShield;

/// <summary>
/// Native WinForms settings window. Opened via tray icon double-click or "Settings..." menu.
/// Dark theme. Tabs: Status, Patterns, Appearance, About.
/// </summary>
sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly FieldTracker _fieldTracker;
    private readonly Action _onSettingsChanged;
    private TextBox? _patternsBox;

    public SettingsForm(AppSettings settings, FieldTracker fieldTracker, Action onSettingsChanged)
    {
        _settings = settings;
        _fieldTracker = fieldTracker;
        _onSettingsChanged = onSettingsChanged;

        Text = "StreamShield Settings";
        Size = new Size(520, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.FormBg;
        ForeColor = Theme.TextPrimary;
        Font = Theme.FormFont;

        using var iconStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("StreamShield.app.ico");
        if (iconStream is not null)
            Icon = new Icon(iconStream);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6)
        };

        tabs.TabPages.Add(BuildStatusTab());
        tabs.TabPages.Add(BuildPatternsTab());
        tabs.TabPages.Add(BuildAppearanceTab());
        tabs.TabPages.Add(BuildAboutTab());

        foreach (TabPage page in tabs.TabPages)
        {
            page.BackColor = Theme.FormBg;
            page.ForeColor = Theme.TextPrimary;
        }

        Controls.Add(tabs);
    }

    private TabPage BuildStatusTab()
    {
        var page = new TabPage("Status");
        int y = 20;

        var enabledCheck = new CheckBox
        {
            Text = "Protection Enabled",
            Checked = _settings.Enabled,
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = Theme.TextPrimary
        };
        enabledCheck.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = enabledCheck.Checked;
            _settings.Save();
            _onSettingsChanged();
        };
        y += 40;

        var fieldsLabel = new Label
        {
            Text = $"Currently tracking {_fieldTracker.Count} sensitive field(s)",
            Location = new Point(20, y),
            AutoSize = true
        };
        y += 30;

        var refreshBtn = new Button
        {
            Text = "Refresh",
            Location = new Point(20, y),
            Size = new Size(100, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.ButtonBg,
            ForeColor = Color.White
        };
        refreshBtn.Click += (_, _) =>
        {
            fieldsLabel.Text = $"Currently tracking {_fieldTracker.Count} sensitive field(s)";
        };
        y += 50;

        // Detection category toggles
        var categoryLabel = new Label
        {
            Text = "Detection Categories",
            Location = new Point(20, y),
            AutoSize = true,
            Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold)
        };
        page.Controls.Add(categoryLabel);
        y += 28;

        AddCategoryToggle(page, "Financial (cards, IBAN, account numbers)",
            _settings.DetectFinancial, v => _settings.DetectFinancial = v, ref y);
        AddCategoryToggle(page, "Identity (SSN, passport, driver license)",
            _settings.DetectIdentity, v => _settings.DetectIdentity = v, ref y);
        AddCategoryToggle(page, "Authentication (passwords, tokens, API keys)",
            _settings.DetectAuthentication, v => _settings.DetectAuthentication = v, ref y);
        AddCategoryToggle(page, "MFA / OTP (PIN, 2FA, verification codes)",
            _settings.DetectMfaOtp, v => _settings.DetectMfaOtp = v, ref y);
        AddCategoryToggle(page, "Hebrew (\u05ea.\u05d6, \u05db\u05e8\u05d8\u05d9\u05e1 \u05d0\u05e9\u05e8\u05d0\u05d9, \u05e1\u05d9\u05e1\u05de\u05d4)",
            _settings.DetectHebrew, v => _settings.DetectHebrew = v, ref y);

        page.Controls.AddRange([enabledCheck, fieldsLabel, refreshBtn]);
        return page;
    }

    private void AddCategoryToggle(TabPage page, string label, bool currentValue,
        Action<bool> setter, ref int y)
    {
        var cb = new CheckBox
        {
            Text = label,
            Checked = currentValue,
            Location = new Point(40, y),
            AutoSize = true,
            ForeColor = Theme.TextPrimary
        };
        cb.CheckedChanged += (_, _) =>
        {
            setter(cb.Checked);
            _settings.Save();
            _onSettingsChanged();
        };
        page.Controls.Add(cb);
        y += 26;
    }

    private TabPage BuildPatternsTab()
    {
        var page = new TabPage("Patterns");

        var infoLabel = new Label
        {
            Text = $"Add custom keywords (one per line) to detect additional fields.\n"
                 + $"{SensitivityPatterns.BuiltInCount} built-in patterns included. Matched case-insensitively.",
            Location = new Point(20, 15),
            AutoSize = true,
            MaximumSize = new Size(440, 0)
        };
        page.Controls.Add(infoLabel);

        // Dynamic Y from info label height
        int y = infoLabel.PreferredHeight + 25;

        var examplesLabel = new Label
        {
            Text = "Examples:  bank-login  |  my-company-id  |  internal-token",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = Theme.TextSubtle,
            Font = Theme.SmallItalic
        };
        y += 22;

        _patternsBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, y),
            Size = new Size(440, 280 - y),
            BackColor = Theme.InputBg,
            ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Text = string.Join(Environment.NewLine, _settings.CustomPatterns)
        };

        var statusLabel = new Label
        {
            Text = "",
            Location = new Point(160, 290),
            AutoSize = true,
            ForeColor = Theme.Amber
        };

        var saveBtn = new Button
        {
            Text = "Save Patterns",
            Location = new Point(20, 285),
            Size = new Size(130, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Amber,
            ForeColor = Theme.Dark
        };
        saveBtn.Click += (_, _) =>
        {
            string[] lines = _patternsBox.Text.Split(
                ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _settings.CustomPatterns = [..lines];
            _settings.Save();
            SensitivityPatterns.LoadCustomPatterns(_settings.CustomPatterns);
            _onSettingsChanged();
            statusLabel.Text = lines.Length == 0
                ? "Cleared custom patterns"
                : $"Saved {lines.Length} custom pattern{(lines.Length == 1 ? "" : "s")}";
        };

        page.Controls.AddRange([examplesLabel, _patternsBox, saveBtn, statusLabel]);
        return page;
    }

    private TabPage BuildAppearanceTab()
    {
        var page = new TabPage("Appearance");

        int y = 20;

        AddColorRow(page, "Mask color (stream sees):", _settings.MaskColorHex,
            v => { _settings.MaskColorHex = v; _settings.Save(); _onSettingsChanged(); }, ref y);
        AddColorRow(page, "Mirror background (you see):", _settings.MirrorBgHex,
            v => { _settings.MirrorBgHex = v; _settings.Save(); _onSettingsChanged(); }, ref y);
        AddColorRow(page, "Mirror text color:", _settings.MirrorFgHex,
            v => { _settings.MirrorFgHex = v; _settings.Save(); _onSettingsChanged(); }, ref y);
        AddColorRow(page, "Persistent overlay background:", _settings.PersistentBgHex,
            v => { _settings.PersistentBgHex = v; _settings.Save(); _onSettingsChanged(); }, ref y);

        var paddingLabel = new Label
        {
            Text = "Overlay padding (px):",
            Location = new Point(20, y + 3),
            AutoSize = true
        };

        var paddingInput = new NumericUpDown
        {
            Location = new Point(200, y),
            Size = new Size(60, 28),
            Minimum = 0,
            Maximum = 10,
            Value = Math.Clamp(_settings.OverlayPadding, 0, 10),
            BackColor = Theme.InputBg,
            ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };
        paddingInput.ValueChanged += (_, _) =>
        {
            _settings.OverlayPadding = (int)paddingInput.Value;
            _settings.Save();
            _onSettingsChanged();
        };

        page.Controls.AddRange([paddingLabel, paddingInput]);
        return page;
    }

    private static void AddColorRow(TabPage page, string label, string currentHex,
        Action<string> onChanged, ref int y)
    {
        var lbl = new Label { Text = label, Location = new Point(20, y), AutoSize = true };

        var txt = new TextBox
        {
            Text = currentHex,
            Location = new Point(300, y - 3),
            Size = new Size(80, 25),
            BackColor = Theme.InputBg,
            ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var preview = new Panel
        {
            Location = new Point(385, y - 2),
            Size = new Size(24, 24),
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };

        try { preview.BackColor = ColorTranslator.FromHtml(currentHex); }
        catch { preview.BackColor = Color.Gray; }

        // Click preview to open color picker
        var pickBtn = new Button
        {
            Text = "...",
            Location = new Point(415, y - 3),
            Size = new Size(30, 25),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.ButtonBg,
            ForeColor = Theme.TextPrimary
        };
        pickBtn.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = preview.BackColor, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK)
                txt.Text = ColorTranslator.ToHtml(dlg.Color);
        };

        txt.TextChanged += (_, _) =>
        {
            try
            {
                preview.BackColor = ColorTranslator.FromHtml(txt.Text);
                onChanged(txt.Text);
            }
            catch { }
        };

        page.Controls.AddRange([lbl, txt, preview, pickBtn]);
        y += 35;
    }

    private static TabPage BuildAboutTab()
    {
        var page = new TabPage("About");

        var title = new Label
        {
            Text = "StreamShield v1.0",
            Font = Theme.TitleFont,
            Location = new Point(20, 20),
            AutoSize = true
        };

        var desc = new Label
        {
            Text = "The first tool that auto-detects sensitive input fields\n"
                 + "and hides them from screen capture in real-time.\n\n"
                 + "No data collection. No network calls. No telemetry.\n"
                 + "Field values are displayed locally and never stored.\n\n"
                 + "MIT License",
            Location = new Point(20, 60),
            Size = new Size(440, 160)
        };

        var linkLabel = new LinkLabel
        {
            Text = "github.com/medy-gribkov/stream-shield",
            Location = new Point(20, 230),
            AutoSize = true,
            LinkColor = Theme.LinkColor
        };
        linkLabel.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/medy-gribkov/stream-shield",
                    UseShellExecute = true
                });
            }
            catch { }
        };

        page.Controls.AddRange([title, desc, linkLabel]);
        return page;
    }
}
