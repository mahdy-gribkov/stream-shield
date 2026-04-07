using System;
using Microsoft.Win32;
using Xunit;

namespace StreamShield.Tests;

public class StartupHelperTests : IDisposable
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "StreamShield";

    public StartupHelperTests()
    {
        // Clean up before each test
        CleanupRegistryKey();
    }

    public void Dispose()
    {
        CleanupRegistryKey();
    }

    [Fact]
    public void SetStartup_True_CreatesRegistryKey()
    {
        StartupHelper.SetStartup(true);
        Assert.True(StartupHelper.IsRegistered());
    }

    [Fact]
    public void SetStartup_False_RemovesRegistryKey()
    {
        StartupHelper.SetStartup(true);
        Assert.True(StartupHelper.IsRegistered());

        StartupHelper.SetStartup(false);
        Assert.False(StartupHelper.IsRegistered());
    }

    [Fact]
    public void IsRegistered_WhenNotSet_ReturnsFalse()
    {
        Assert.False(StartupHelper.IsRegistered());
    }

    private static void CleanupRegistryKey()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
