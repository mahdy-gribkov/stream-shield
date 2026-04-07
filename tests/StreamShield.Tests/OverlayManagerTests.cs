using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace StreamShield.Tests;

public class OverlayManagerTests
{
    [Fact]
    public void ShowFocused_CreatesOverlay()
    {
        Exception? ex = null;
        int count = -1;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var manager = new OverlayManager(new AppSettings());
                manager.ShowFocused(1, new Rectangle(100, 100, 200, 30), SensitivityKind.SensitiveText, "test");
                count = manager.ActiveCount;
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);
        Assert.Null(ex);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ShowFocused_DifferentKey_ConvertsPrevious()
    {
        Exception? ex = null;
        int count = -1;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var manager = new OverlayManager(new AppSettings());
                manager.ShowFocused(1, new Rectangle(100, 100, 200, 30), SensitivityKind.SensitiveText, "first");
                manager.ShowFocused(2, new Rectangle(100, 200, 200, 30), SensitivityKind.Password, "second");
                count = manager.ActiveCount;
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);
        Assert.Null(ex);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ShowPersistent_CreatesOverlay()
    {
        Exception? ex = null;
        int count = -1;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var manager = new OverlayManager(new AppSettings());
                manager.ShowPersistent(1, new Rectangle(100, 100, 200, 30));
                count = manager.ActiveCount;
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);
        Assert.Null(ex);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Remove_DecreasesCount()
    {
        Exception? ex = null;
        int count = -1;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var manager = new OverlayManager(new AppSettings());
                manager.ShowFocused(1, new Rectangle(100, 100, 200, 30), SensitivityKind.SensitiveText, "test");
                manager.Remove(1);
                count = manager.ActiveCount;
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);
        Assert.Null(ex);
        Assert.Equal(0, count);
    }

    [Fact]
    public void HideAll_ClearsAll()
    {
        Exception? ex = null;
        int count = -1;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var manager = new OverlayManager(new AppSettings());
                manager.ShowFocused(1, new Rectangle(100, 100, 200, 30), SensitivityKind.SensitiveText, "focused");
                manager.ShowPersistent(2, new Rectangle(100, 200, 200, 30));
                manager.HideAll();
                count = manager.ActiveCount;
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);
        Assert.Null(ex);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ShowFocused_SameKey_NoDuplicate()
    {
        Exception? ex = null;
        int count = -1;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var manager = new OverlayManager(new AppSettings());
                manager.ShowFocused(1, new Rectangle(100, 100, 200, 30), SensitivityKind.SensitiveText, "first");
                manager.ShowFocused(1, new Rectangle(100, 100, 200, 30), SensitivityKind.Password, "second");
                count = manager.ActiveCount;
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);
        Assert.Null(ex);
        Assert.Equal(1, count);
    }

    [Fact]
    public void UpdateSettings_DoesNotThrow()
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var manager = new OverlayManager(new AppSettings());
                manager.UpdateSettings(new AppSettings());
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);
        Assert.Null(ex);
    }
}
