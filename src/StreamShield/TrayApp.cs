using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace StreamShield;

/// <summary>
/// System tray application. Wires together: FocusMonitor, SensitivityDetector,
/// FieldTracker, OverlayManager, AppSettings, SettingsForm.
/// </summary>
sealed class TrayApp : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly FocusMonitor _focusMonitor;
    private readonly SensitivityDetector _detector;
    private readonly FieldTracker _fieldTracker;
    private readonly OverlayManager _overlays;
    private readonly Timer _positionTimer;
    private SettingsForm? _settingsForm;

    public TrayApp(AppSettings settings)
    {
        _settings = settings;
        _fieldTracker = new FieldTracker();
        _detector = new SensitivityDetector();
        _detector.SetFieldTracker(_fieldTracker);
        _overlays = new OverlayManager(settings);
        _focusMonitor = new FocusMonitor();
        _focusMonitor.FocusDebounced += OnFocusDebounced;

        SensitivityPatterns.LoadCustomPatterns(settings.CustomPatterns);

        // Position + content update timer: 50ms (20fps)
        _positionTimer = new Timer { Interval = 50 };
        _positionTimer.Tick += OnPositionTick;

        _trayIcon = new NotifyIcon
        {
            Icon = CreateCircleIcon(Color.FromArgb(0, 180, 0)),
            Text = "StreamShield - Active",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        if (_settings.Enabled)
        {
            _focusMonitor.Start();
            _positionTimer.Start();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _positionTimer.Stop();
            _positionTimer.Dispose();
            _focusMonitor.Dispose();
            _overlays.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _settingsForm?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnFocusDebounced()
    {
        if (!_settings.Enabled) return;
        HandleInspection();
    }

    private void OnPositionTick(object? sender, EventArgs e)
    {
        if (!_settings.Enabled) return;

        // Prune overlays for closed windows
        List<TrackedField> stale = _fieldTracker.PruneStale();
        foreach (TrackedField field in stale)
        {
            int key = HashCode.Combine(
                field.Bounds.X / 15, field.Bounds.Y / 15, field.TopLevelHwnd);
            _overlays.Remove(key);
        }

        // Re-inspect focused element for position/content updates
        HandleInspection();
    }

    private void HandleInspection()
    {
        InspectionResult? result = _detector.Inspect();

        if (result is not null)
        {
            // Track window changes
            _fieldTracker.OnTopLevelWindowChanged(result.TopLevelHwnd);

            // Unfocus all, then mark the current one
            _fieldTracker.UnfocusAll();
            TrackedField tracked = _fieldTracker.Track(
                result.Bounds, result.Kind, result.TopLevelHwnd);

            int fieldKey = HashCode.Combine(
                result.Bounds.X / 15, result.Bounds.Y / 15, result.TopLevelHwnd);

            // Show focused overlay with content mirror
            _overlays.ShowFocused(fieldKey, result.Bounds, result.Kind, result.FieldValue);

            // Show persistent overlays for previously-filled fields
            foreach (TrackedField persistent in _fieldTracker.GetPersistentFields())
            {
                int pKey = HashCode.Combine(
                    persistent.Bounds.X / 15, persistent.Bounds.Y / 15, persistent.TopLevelHwnd);
                _overlays.ShowPersistent(pKey, persistent.Bounds);
            }
        }
        else
        {
            // Focus is on a non-sensitive element
            _fieldTracker.UnfocusAll();

            // Keep persistent overlays, but check for window change
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
                _fieldTracker.OnTopLevelWindowChanged(fgHwnd);

            // Update persistent overlays (some may have been removed by window change)
            SyncPersistentOverlays();
        }

        UpdateTrayText();
    }

    private void SyncPersistentOverlays()
    {
        // Get current persistent fields
        List<TrackedField> persistent = _fieldTracker.GetPersistentFields();
        HashSet<int> validKeys = [];

        foreach (TrackedField field in persistent)
        {
            int key = HashCode.Combine(
                field.Bounds.X / 15, field.Bounds.Y / 15, field.TopLevelHwnd);
            validKeys.Add(key);
            _overlays.ShowPersistent(key, field.Bounds);
        }

        // Remove overlays that no longer have a tracked field
        List<int> toRemove = [];
        foreach (int key in _overlays.ActiveKeys)
        {
            if (!validKeys.Contains(key))
                toRemove.Add(key);
        }

        foreach (int key in toRemove)
            _overlays.Remove(key);
    }

    private void UpdateTrayText()
    {
        int count = _overlays.ActiveCount;
        _trayIcon.Text = count > 0
            ? $"StreamShield - Protecting {count} field(s)"
            : "StreamShield - Active";
    }

    private void ShowSettings()
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.BringToFront();
            return;
        }

        _settingsForm = new SettingsForm(_settings, _fieldTracker, OnSettingsChanged);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
    }

    private void OnSettingsChanged()
    {
        _overlays.UpdateSettings(_settings);
        SensitivityPatterns.LoadCustomPatterns(_settings.CustomPatterns);

        if (_settings.Enabled)
        {
            _focusMonitor.Start();
            _positionTimer.Start();
            _trayIcon.Icon = CreateCircleIcon(Color.FromArgb(0, 180, 0));
        }
        else
        {
            _focusMonitor.Stop();
            _positionTimer.Stop();
            _overlays.HideAll();
            _fieldTracker.Clear();
            _trayIcon.Icon = CreateCircleIcon(Color.FromArgb(180, 0, 0));
            _trayIcon.Text = "StreamShield - Disabled";
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var toggleItem = new ToolStripMenuItem("Enabled")
        {
            Checked = _settings.Enabled,
            CheckOnClick = true
        };
        toggleItem.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = toggleItem.Checked;
            _settings.Save();
            OnSettingsChanged();
        };

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => ShowSettings();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _overlays.HideAll();
            _fieldTracker.Clear();
            ExitThread();
        };

        menu.Items.Add(toggleItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private static Icon CreateCircleIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
