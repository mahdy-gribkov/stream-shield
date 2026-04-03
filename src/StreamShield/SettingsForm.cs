using System;
using System.Drawing;
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
        Size = new Size(520, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 40);
        ForeColor = Color.FromArgb(220, 220, 220);
        Font = new Font("Segoe UI", 10f);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6)
        };

        tabs.TabPages.Add(BuildStatusTab());
        tabs.TabPages.Add(BuildPatternsTab());
        tabs.TabPages.Add(BuildAppearanceTab());
        tabs.TabPages.Add(BuildAboutTab());

        // Dark theme the tab control
        foreach (TabPage page in tabs.TabPages)
        {
            page.BackColor = Color.FromArgb(30, 30, 40);
            page.ForeColor = Color.FromArgb(220, 220, 220);
        }

        Controls.Add(tabs);
    }

    private TabPage BuildStatusTab()
    {
        var page = new TabPage("Status");

        var enabledCheck = new CheckBox
        {
            Text = "Protection Enabled",
            Checked = _settings.Enabled,
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        enabledCheck.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = enabledCheck.Checked;
            _settings.Save();
            _onSettingsChanged();
        };

        var fieldsLabel = new Label
        {
            Text = $"Currently tracking {_fieldTracker.Count} sensitive field(s)",
            Location = new Point(20, 60),
            AutoSize = true
        };

        var refreshBtn = new Button
        {
            Text = "Refresh",
            Location = new Point(20, 100),
            Size = new Size(100, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(50, 50, 70),
            ForeColor = Color.White
        };
        refreshBtn.Click += (_, _) =>
        {
            fieldsLabel.Text = $"Currently tracking {_fieldTracker.Count} sensitive field(s)";
        };

        page.Controls.AddRange([enabledCheck, fieldsLabel, refreshBtn]);
        return page;
    }

    private TabPage BuildPatternsTab()
    {
        var page = new TabPage("Patterns");

        var infoLabel = new Label
        {
            Text = "Custom sensitivity keywords (one per line).\nFields matching these will be protected.",
            Location = new Point(20, 15),
            AutoSize = true,
            MaximumSize = new Size(440, 0)
        };

        _patternsBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 65),
            Size = new Size(440, 195),
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.FixedSingle,
            Text = string.Join(Environment.NewLine, _settings.CustomPatterns)
        };

        var saveBtn = new Button
        {
            Text = "Save Patterns",
            Location = new Point(20, 270),
            Size = new Size(130, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 0),
            ForeColor = Color.White
        };
        saveBtn.Click += (_, _) =>
        {
            string[] lines = _patternsBox.Text.Split(
                ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _settings.CustomPatterns = [..lines];
            _settings.Save();
            SensitivityPatterns.LoadCustomPatterns(_settings.CustomPatterns);
            _onSettingsChanged();
        };

        page.Controls.AddRange([infoLabel, _patternsBox, saveBtn]);
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
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.FromArgb(220, 220, 220),
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
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.FixedSingle
        };

        var preview = new Panel
        {
            Location = new Point(390, y - 2),
            Size = new Size(24, 24),
            BorderStyle = BorderStyle.FixedSingle
        };

        try { preview.BackColor = ColorTranslator.FromHtml(currentHex); }
        catch { preview.BackColor = Color.Gray; }

        txt.TextChanged += (_, _) =>
        {
            try
            {
                preview.BackColor = ColorTranslator.FromHtml(txt.Text);
                onChanged(txt.Text);
            }
            catch { /* invalid color, ignore */ }
        };

        page.Controls.AddRange([lbl, txt, preview]);
        y += 35;
    }

    private static TabPage BuildAboutTab()
    {
        var page = new TabPage("About");

        var title = new Label
        {
            Text = "StreamShield v1.0",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
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
            LinkColor = Color.FromArgb(100, 160, 255)
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
            catch { /* browser not available */ }
        };

        page.Controls.AddRange([title, desc, linkLabel]);
        return page;
    }
}
