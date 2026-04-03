using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace StreamShield;

static class Program
{
    private static Mutex? _singleInstance;

    [STAThread]
    static void Main()
    {
        _singleInstance = new Mutex(true, @"Global\StreamShield_SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show(
                "StreamShield is already running.\nCheck the system tray.",
                "StreamShield",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        AppSettings settings = AppSettings.Load();
        SetupLogging();

        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, args) =>
            Log($"UI exception: {args.Exception}");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log($"Fatal exception: {args.ExceptionObject}");

        Log("StreamShield starting");

        using var app = new TrayApp(settings);
        Application.Run(app);

        Log("StreamShield exiting");
        _singleInstance.ReleaseMutex();
    }

    private static void SetupLogging()
    {
        try
        {
            string dir = Path.GetDirectoryName(AppSettings.LogFilePath)!;
            Directory.CreateDirectory(dir);
        }
        catch { /* non-critical */ }
    }

    internal static void Log(string message)
    {
        try
        {
            string line = $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}";
            File.AppendAllText(AppSettings.LogFilePath, line);
        }
        catch { /* non-critical */ }
    }
}
