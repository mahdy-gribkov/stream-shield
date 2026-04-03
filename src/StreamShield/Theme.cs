using System.Drawing;

namespace StreamShield;

/// <summary>Centralized colors, fonts, and strings for the UI.</summary>
static class Theme
{
    // Brand
    public static readonly Color Amber = Color.FromArgb(212, 148, 58);
    public static readonly Color Dark = Color.FromArgb(12, 11, 14);

    // Surfaces
    public static readonly Color FormBg = Color.FromArgb(30, 30, 40);
    public static readonly Color InputBg = Color.FromArgb(40, 40, 55);
    public static readonly Color ButtonBg = Color.FromArgb(50, 50, 70);

    // Text
    public static readonly Color TextPrimary = Color.FromArgb(220, 220, 220);
    public static readonly Color TextSubtle = Color.FromArgb(150, 150, 150);
    public static readonly Color LinkColor = Color.FromArgb(100, 160, 255);

    // State
    public static readonly Color DisabledRed = Color.FromArgb(180, 0, 0);

    // Fonts
    public const string FontFamily = "Segoe UI";
    public static readonly Font FormFont = new(FontFamily, 10f);
    public static readonly Font SmallItalic = new(FontFamily, 9f, FontStyle.Italic);
    public static readonly Font TitleFont = new(FontFamily, 16f, FontStyle.Bold);
    public static readonly Font OverlayFocused = new(FontFamily, 11f, FontStyle.Regular);
    public static readonly Font OverlayPersistent = new(FontFamily, 9f, FontStyle.Bold);

    // Strings
    public const string ProtectedLabel = "Protected";
}
