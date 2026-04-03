using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StreamShield;

/// <summary>
/// Manages multiple overlay pairs (mask + mirror) for tracked sensitive fields.
/// Focused field: mirror renders field content (white bg, field value).
/// Persistent fields: mirror shows "Protected" label (dark bg).
/// </summary>
sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<int, OverlayPair> _overlays = [];
    private int _focusedKey = -1;
    private bool _disposed;
    private AppSettings _settings;

    public OverlayManager(AppSettings settings)
    {
        _settings = settings;
    }

    public int ActiveCount => _overlays.Count;

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    /// <summary>Show or update the focused field's overlay with content mirroring.</summary>
    public void ShowFocused(int fieldKey, Rectangle bounds, SensitivityKind kind, string? fieldValue)
    {
        if (_disposed) return;

        Rectangle padded = PadBounds(bounds);

        // If a different field was previously focused, convert it to persistent
        if (_focusedKey != -1 && _focusedKey != fieldKey && _overlays.ContainsKey(_focusedKey))
        {
            ConvertToPersistent(_focusedKey);
        }

        _focusedKey = fieldKey;

        if (!_overlays.TryGetValue(fieldKey, out OverlayPair? pair))
        {
            pair = CreatePair();
            _overlays[fieldKey] = pair;
        }

        // Style as focused: white bg, field content
        pair.Mask.SetBoundsAndShow(padded, _settings.MaskColor);
        pair.Mirror.SetBoundsAndShow(padded, _settings.MirrorBg, _settings.MirrorFg);

        string displayText = kind == SensitivityKind.Password
            ? "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022"
            : fieldValue ?? "Protected";

        pair.Mirror.UpdateText(displayText, leftAlign: true);
    }

    /// <summary>Add or update a persistent overlay for a non-focused tracked field.</summary>
    public void ShowPersistent(int fieldKey, Rectangle bounds)
    {
        if (_disposed) return;

        Rectangle padded = PadBounds(bounds);

        if (!_overlays.TryGetValue(fieldKey, out OverlayPair? pair))
        {
            pair = CreatePair();
            _overlays[fieldKey] = pair;
        }

        pair.Mask.SetBoundsAndShow(padded, _settings.MaskColor);
        pair.Mirror.SetBoundsAndShow(padded, _settings.PersistentBg, _settings.PersistentFg);
        pair.Mirror.UpdateText("Protected", leftAlign: false);
    }

    /// <summary>Remove a specific field's overlay.</summary>
    public void Remove(int fieldKey)
    {
        if (_overlays.TryGetValue(fieldKey, out OverlayPair? pair))
        {
            pair.Dispose();
            _overlays.Remove(fieldKey);
            if (_focusedKey == fieldKey) _focusedKey = -1;
        }
    }

    /// <summary>Remove all overlays.</summary>
    public void HideAll()
    {
        foreach (OverlayPair pair in _overlays.Values)
            pair.Dispose();
        _overlays.Clear();
        _focusedKey = -1;
    }

    /// <summary>Get all active overlay field keys.</summary>
    public IEnumerable<int> ActiveKeys => _overlays.Keys;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        HideAll();
    }

    private void ConvertToPersistent(int fieldKey)
    {
        if (_overlays.TryGetValue(fieldKey, out OverlayPair? pair))
        {
            pair.Mirror.SetColors(_settings.PersistentBg, _settings.PersistentFg);
            pair.Mirror.UpdateText("Protected", leftAlign: false);
        }
    }

    private Rectangle PadBounds(Rectangle bounds)
    {
        int pad = _settings.OverlayPadding;
        return new Rectangle(
            bounds.X - pad, bounds.Y - pad,
            bounds.Width + pad * 2, bounds.Height + pad * 2);
    }

    private static OverlayPair CreatePair()
    {
        var mask = new OverlayWindow(isMirror: false);
        var mirror = new OverlayWindow(isMirror: true);
        mask.Show();
        mirror.Show();
        return new OverlayPair(mask, mirror);
    }

    // ---------------------------------------------------------------

    private sealed class OverlayPair(OverlayWindow mask, OverlayWindow mirror) : IDisposable
    {
        public OverlayWindow Mask { get; } = mask;
        public OverlayWindow Mirror { get; } = mirror;

        public void Dispose()
        {
            if (!Mask.IsDisposed) { Mask.Close(); Mask.Dispose(); }
            if (!Mirror.IsDisposed) { Mirror.Close(); Mirror.Dispose(); }
        }
    }

    /// <summary>
    /// A single overlay window. When isMirror=true, has WDA_EXCLUDEFROMCAPTURE
    /// (invisible to capture, visible to user). When false, visible to capture (the mask).
    /// </summary>
    private sealed class OverlayWindow : Form
    {
        private static readonly Font FocusedFont = new("Segoe UI", 11f, FontStyle.Regular);
        private static readonly Font PersistentFont = new("Segoe UI", 9f, FontStyle.Bold);
        private readonly bool _isMirror;
        private readonly Label _label;

        public OverlayWindow(bool isMirror)
        {
            _isMirror = isMirror;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(10, 10);

            _label = new Label
            {
                Text = "",
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(6, 0, 6, 0)
            };
            Controls.Add(_label);
        }

        public void SetBoundsAndShow(Rectangle bounds, Color bg, Color? fg = null)
        {
            BackColor = bg;
            if (fg.HasValue) _label.ForeColor = fg.Value;
            SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        public void SetColors(Color bg, Color fg)
        {
            BackColor = bg;
            _label.ForeColor = fg;
        }

        public void UpdateText(string text, bool leftAlign)
        {
            _label.Text = text;
            _label.Font = leftAlign ? FocusedFont : PersistentFont;
            _label.TextAlign = leftAlign
                ? ContentAlignment.MiddleLeft
                : ContentAlignment.MiddleCenter;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT
                            | NativeMethods.WS_EX_TOOLWINDOW
                            | NativeMethods.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (_isMirror)
            {
                NativeMethods.SetWindowDisplayAffinity(
                    Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            }
        }
    }
}
