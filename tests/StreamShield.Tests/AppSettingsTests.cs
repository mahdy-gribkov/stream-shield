using System.Drawing;
using System.IO;
using System.Text.Json;
using Xunit;

namespace StreamShield.Tests;

public class AppSettingsTests
{
    // --- Default values ---

    [Fact]
    public void Defaults_Enabled()
    {
        var settings = new AppSettings();
        Assert.True(settings.Enabled);
    }

    [Fact]
    public void Defaults_OverlayPadding()
    {
        var settings = new AppSettings();
        Assert.Equal(2, settings.OverlayPadding);
    }

    [Fact]
    public void Defaults_MaskColorHex()
    {
        var settings = new AppSettings();
        Assert.Equal("#323232", settings.MaskColorHex);
    }

    [Fact]
    public void Defaults_MirrorColors()
    {
        var settings = new AppSettings();
        Assert.Equal("#FFFFFF", settings.MirrorBgHex);
        Assert.Equal("#000000", settings.MirrorFgHex);
    }

    [Fact]
    public void Defaults_PersistentColors()
    {
        var settings = new AppSettings();
        Assert.Equal("#1A1A2E", settings.PersistentBgHex);
        Assert.Equal("#C8C8C8", settings.PersistentFgHex);
    }

    [Fact]
    public void Defaults_CustomPatternsEmpty()
    {
        var settings = new AppSettings();
        Assert.Empty(settings.CustomPatterns);
    }

    // --- Color parsing ---

    [Fact]
    public void MaskColor_ValidHex_ParsesCorrectly()
    {
        var settings = new AppSettings { MaskColorHex = "#FF0000" };
        Assert.Equal(Color.FromArgb(255, 0, 0), settings.MaskColor);
    }

    [Fact]
    public void MaskColor_InvalidHex_ReturnsFallback()
    {
        var settings = new AppSettings { MaskColorHex = "not-a-color" };
        // Fallback is Color.FromArgb(50, 50, 50)
        Assert.Equal(Color.FromArgb(50, 50, 50), settings.MaskColor);
    }

    [Fact]
    public void MirrorBg_ValidHex_ParsesCorrectly()
    {
        var settings = new AppSettings { MirrorBgHex = "#00FF00" };
        Assert.Equal(Color.FromArgb(0, 255, 0), settings.MirrorBg);
    }

    [Fact]
    public void PersistentBg_ValidHex_ParsesCorrectly()
    {
        var settings = new AppSettings { PersistentBgHex = "#0000FF" };
        Assert.Equal(Color.FromArgb(0, 0, 255), settings.PersistentBg);
    }

    // --- JSON serialization round-trip ---

    [Fact]
    public void Serialization_RoundTrip_PreservesValues()
    {
        var original = new AppSettings
        {
            Enabled = false,
            OverlayPadding = 5,
            MaskColorHex = "#AABBCC",
            MirrorBgHex = "#112233",
            MirrorFgHex = "#445566",
            PersistentBgHex = "#778899",
            PersistentFgHex = "#AABB00",
            CustomPatterns = ["pattern-one", "pattern-two"]
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Equal(original.Enabled, deserialized.Enabled);
        Assert.Equal(original.OverlayPadding, deserialized.OverlayPadding);
        Assert.Equal(original.MaskColorHex, deserialized.MaskColorHex);
        Assert.Equal(original.MirrorBgHex, deserialized.MirrorBgHex);
        Assert.Equal(original.MirrorFgHex, deserialized.MirrorFgHex);
        Assert.Equal(original.PersistentBgHex, deserialized.PersistentBgHex);
        Assert.Equal(original.PersistentFgHex, deserialized.PersistentFgHex);
        Assert.Equal(original.CustomPatterns, deserialized.CustomPatterns);
    }

    [Fact]
    public void Serialization_JsonIgnoreProperties_NotSerialized()
    {
        var settings = new AppSettings();
        string json = JsonSerializer.Serialize(settings);

        // JsonIgnore Color properties should not appear (check with quotes to avoid substring match)
        Assert.DoesNotContain("\"MaskColor\"", json);
        Assert.DoesNotContain("\"MirrorBg\"", json);
        Assert.DoesNotContain("\"MirrorFg\"", json);
        Assert.DoesNotContain("\"PersistentBg\"", json);
        Assert.DoesNotContain("\"PersistentFg\"", json);
        Assert.DoesNotContain("\"LogFilePath\"", json);

        // But the Hex versions should be present
        Assert.Contains("MaskColorHex", json);
        Assert.Contains("MirrorBgHex", json);
    }

    [Fact]
    public void Deserialization_CorruptJson_Throws()
    {
        // JsonSerializer throws on corrupt JSON. AppSettings.Load() catches this
        // and returns defaults. Here we verify the throw behavior itself.
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AppSettings>("not valid json {{{"));
    }

    [Fact]
    public void Deserialization_EmptyObject_UsesDefaults()
    {
        var result = JsonSerializer.Deserialize<AppSettings>("{}")!;

        Assert.True(result.Enabled); // default
        Assert.Equal(2, result.OverlayPadding); // default
        Assert.Equal("#323232", result.MaskColorHex); // default
    }

    // --- LogFilePath ---

    [Fact]
    public void LogFilePath_EndsWithExpectedFilename()
    {
        Assert.EndsWith("log.txt", AppSettings.LogFilePath);
    }

    [Fact]
    public void LogFilePath_ContainsStreamShieldDirectory()
    {
        Assert.Contains("StreamShield", AppSettings.LogFilePath);
    }

    // --- RunOnStartup ---

    [Fact]
    public void Defaults_RunOnStartup_False()
    {
        var settings = new AppSettings();
        Assert.False(settings.RunOnStartup);
    }

    [Fact]
    public void RunOnStartup_RoundTrips()
    {
        var original = new AppSettings { RunOnStartup = true };
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.True(deserialized.RunOnStartup);
    }
}
