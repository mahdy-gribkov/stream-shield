using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StreamShield;

/// <summary>
/// Manages Windows startup registration via HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run.
/// No admin elevation required.
/// </summary>
static class StartupHelper
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "StreamShield";

    public static void SetStartup(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;

            if (enable)
                key.SetValue(AppName, Application.ExecutablePath);
            else
                key.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch
        {
            // Non-critical: startup preference just won't persist
        }
    }

    public static bool IsRegistered()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppName) is not null;
        }
        catch
        {
            return false;
        }
    }
}
