using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamShield;

/// <summary>
/// Persistent application settings stored in %APPDATA%\StreamShield\settings.json.
/// Created with defaults on first run.
/// </summary>
sealed class AppSettings
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StreamShield");

    private static readonly string SettingsPath = Path.Combine(AppDataDir, "settings.json");
    private static readonly string LogPath = Path.Combine(AppDataDir, "log.txt");

    public bool Enabled { get; set; } = true;
    public int OverlayPadding { get; set; } = 2;
    public string MaskColorHex { get; set; } = "#323232";
    public string MirrorBgHex { get; set; } = "#FFFFFF";
    public string MirrorFgHex { get; set; } = "#000000";
    public string PersistentBgHex { get; set; } = "#1A1A2E";
    public string PersistentFgHex { get; set; } = "#C8C8C8";
    public List<string> CustomPatterns { get; set; } = [];

    // Detection category toggles
    public bool DetectFinancial { get; set; } = true;
    public bool DetectIdentity { get; set; } = true;
    public bool DetectAuthentication { get; set; } = true;
    public bool DetectMfaOtp { get; set; } = true;
    public bool DetectHebrew { get; set; } = true;

    [JsonIgnore]
    public Color MaskColor => ColorFromHex(MaskColorHex, Color.FromArgb(50, 50, 50));

    [JsonIgnore]
    public Color MirrorBg => ColorFromHex(MirrorBgHex, Color.White);

    [JsonIgnore]
    public Color MirrorFg => ColorFromHex(MirrorFgHex, Color.Black);

    [JsonIgnore]
    public Color PersistentBg => ColorFromHex(PersistentBgHex, Color.FromArgb(26, 26, 46));

    [JsonIgnore]
    public Color PersistentFg => ColorFromHex(PersistentFgHex, Color.FromArgb(200, 200, 200));

    [JsonIgnore]
    public static string LogFilePath => LogPath;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt file, use defaults
        }

        var defaults = new AppSettings();
        defaults.Save();
        return defaults;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Non-critical, settings just won't persist
        }
    }

    private static Color ColorFromHex(string hex, Color fallback)
    {
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return fallback;
        }
    }
}
