using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StreamShieldPoC;

static class Native
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint dwAffinity);

    public const uint WDA_NONE = 0x00000000;
    public const uint WDA_MONITOR = 0x00000001;
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
}

/// <summary>
/// Window A: the grey mask that screen capture SHOULD see.
/// No display affinity set. Click-through so it doesn't steal focus.
/// </summary>
sealed class MaskForm : Form
{
    public MaskForm(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(60, 60, 60);
        StartPosition = FormStartPosition.Manual;
        Size = bounds.Size;
        Location = bounds.Location;

        Controls.Add(new Label
        {
            Text = "CAPTURE SEES THIS (GREY MASK)",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        });
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}

/// <summary>
/// Window B: the green mirror that only YOU should see locally.
/// Has WDA_EXCLUDEFROMCAPTURE + WS_EX_TRANSPARENT (click-through).
/// </summary>
sealed class MirrorForm : Form
{
    public MirrorForm(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(0, 150, 0);
        StartPosition = FormStartPosition.Manual;
        Size = bounds.Size;
        Location = bounds.Location;

        Controls.Add(new Label
        {
            Text = "YOU SEE THIS LOCALLY\r\n(Should be INVISIBLE in capture)",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        });
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TRANSPARENT;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        bool ok = Native.SetWindowDisplayAffinity(Handle, Native.WDA_EXCLUDEFROMCAPTURE);
        if (ok)
        {
            Native.GetWindowDisplayAffinity(Handle, out uint actual);
            Console.WriteLine($"[OK] WDA_EXCLUDEFROMCAPTURE set. Verified: 0x{actual:X8}");
        }
        else
        {
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine($"[FAIL] SetWindowDisplayAffinity error: {err} (0x{err:X})");

            if (err == 5)
                Console.WriteLine("       ERROR_ACCESS_DENIED - try running as admin");
            else if (err == 8)
                Console.WriteLine("       ERROR_NOT_ENOUGH_MEMORY - likely WS_EX_LAYERED conflict");
            else
                Console.WriteLine("       WDA_EXCLUDEFROMCAPTURE may not be supported on this build");
        }
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Console.WriteLine("StreamShield PoC - Double Overlay Capture Test");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        Rectangle screen = Screen.PrimaryScreen?.WorkingArea
            ?? new Rectangle(0, 0, 1920, 1080);

        var rect = new Rectangle(
            (screen.Width - 420) / 2 + screen.Left,
            (screen.Height - 80) / 2 + screen.Top,
            420, 80);

        Console.WriteLine($"Overlay position: {rect.X},{rect.Y} size: {rect.Width}x{rect.Height}");
        Console.WriteLine();

        var mask = new MaskForm(rect);
        var mirror = new MirrorForm(rect);

        // Show mask first (below), then mirror (on top)
        mask.Show();
        mirror.Show();

        Console.WriteLine();
        Console.WriteLine("HOW TO TEST:");
        Console.WriteLine("  1. You should see a GREEN window on your screen");
        Console.WriteLine("  2. Start a Discord screen share, or use Snipping Tool (Win+Shift+S)");
        Console.WriteLine("  3. Check what the capture shows:");
        Console.WriteLine();
        Console.WriteLine("RESULTS:");
        Console.WriteLine("  Green locally + GREY in capture  = SUCCESS (approach works)");
        Console.WriteLine("  Green locally + GREEN in capture = FAIL (affinity not working)");
        Console.WriteLine("  Green locally + BLACK in capture = PARTIAL (WDA_MONITOR fallback)");
        Console.WriteLine("  Green locally + NOTHING there    = PARTIAL (both excluded)");
        Console.WriteLine();
        Console.WriteLine("Close the window or Ctrl+C to exit.");

        Application.Run(mirror);
        mask.Close();
    }
}
