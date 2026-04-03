using System;
using System.Drawing;
using Xunit;

namespace StreamShield.Tests;

public class FieldTrackerTests
{
    private static readonly IntPtr Hwnd1 = new(0x1001);
    private static readonly IntPtr Hwnd2 = new(0x2002);
    private static readonly Rectangle Bounds1 = new(100, 200, 300, 30);
    private static readonly Rectangle Bounds2 = new(100, 260, 300, 30);

    [Fact]
    public void Track_NewField_IncreasesCount()
    {
        var tracker = new FieldTracker();

        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Track_SameFieldTwice_DoesNotDuplicate()
    {
        var tracker = new FieldTracker();

        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Track_DifferentFields_TrackedSeparately()
    {
        var tracker = new FieldTracker();

        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);
        tracker.Track(Bounds2, SensitivityKind.SensitiveText, Hwnd1);

        Assert.Equal(2, tracker.Count);
    }

    [Fact]
    public void Track_ReturnsFocusedField()
    {
        var tracker = new FieldTracker();

        TrackedField field = tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        Assert.True(field.IsFocused);
        Assert.Equal(SensitivityKind.Password, field.Kind);
        Assert.Equal(Bounds1, field.Bounds);
    }

    [Fact]
    public void Track_ExistingField_UpdatesLastSeen()
    {
        var tracker = new FieldTracker();
        TrackedField first = tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);
        DateTime firstSeen = first.LastSeen;

        // Small delay to ensure different timestamp
        System.Threading.Thread.Sleep(1);

        TrackedField second = tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        Assert.True(second.LastSeen >= firstSeen);
        Assert.True(second.IsFocused);
    }

    [Fact]
    public void UnfocusAll_MarksAllAsNotFocused()
    {
        var tracker = new FieldTracker();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);
        tracker.Track(Bounds2, SensitivityKind.SensitiveText, Hwnd1);

        tracker.UnfocusAll();

        foreach (TrackedField field in tracker.TrackedFields)
            Assert.False(field.IsFocused);
    }

    // --- FindAtPosition ---

    [Fact]
    public void FindAtPosition_ExactMatch_ReturnsField()
    {
        var tracker = new FieldTracker();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        TrackedField? found = tracker.FindAtPosition(Bounds1, Hwnd1);

        Assert.NotNull(found);
        Assert.Equal(SensitivityKind.Password, found.Kind);
    }

    [Fact]
    public void FindAtPosition_WithinTolerance_ReturnsField()
    {
        var tracker = new FieldTracker();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        // Offset by 10px (within 15px tolerance)
        var nearby = new Rectangle(110, 210, 310, 35);
        TrackedField? found = tracker.FindAtPosition(nearby, Hwnd1);

        Assert.NotNull(found);
    }

    [Fact]
    public void FindAtPosition_OutsideTolerance_ReturnsNull()
    {
        var tracker = new FieldTracker();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        // Offset by 50px (outside 15px tolerance)
        var far = new Rectangle(150, 250, 350, 80);
        TrackedField? found = tracker.FindAtPosition(far, Hwnd1);

        Assert.Null(found);
    }

    [Fact]
    public void FindAtPosition_DifferentHwnd_ReturnsNull()
    {
        var tracker = new FieldTracker();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        TrackedField? found = tracker.FindAtPosition(Bounds1, Hwnd2);

        Assert.Null(found);
    }

    [Fact]
    public void FindAtPosition_EmptyTracker_ReturnsNull()
    {
        var tracker = new FieldTracker();

        TrackedField? found = tracker.FindAtPosition(Bounds1, Hwnd1);

        Assert.Null(found);
    }

    // --- Reveal-password scenario ---

    [Fact]
    public void RevealPassword_SamePosition_StillDetected()
    {
        var tracker = new FieldTracker();

        // User focused a password field
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        // User clicks "reveal password" -> IsPassword flips to false
        // Detector calls FindAtPosition to check if it was previously tracked
        TrackedField? found = tracker.FindAtPosition(Bounds1, Hwnd1);

        Assert.NotNull(found);
        Assert.Equal(SensitivityKind.Password, found.Kind);
    }

    // --- OnTopLevelWindowChanged ---

    [Fact]
    public void OnTopLevelWindowChanged_ClearsOldWindowFields()
    {
        var tracker = new FieldTracker();

        // Set current window context
        tracker.OnTopLevelWindowChanged(Hwnd1);
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);
        tracker.Track(Bounds2, SensitivityKind.SensitiveText, Hwnd1);
        Assert.Equal(2, tracker.Count);

        // Switch to different window
        tracker.OnTopLevelWindowChanged(Hwnd2);

        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void OnTopLevelWindowChanged_SameWindow_DoesNotClear()
    {
        var tracker = new FieldTracker();

        tracker.OnTopLevelWindowChanged(Hwnd1);
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        // Same window again
        tracker.OnTopLevelWindowChanged(Hwnd1);

        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void OnTopLevelWindowChanged_FromZero_DoesNotClear()
    {
        var tracker = new FieldTracker();

        // First call, _currentTopLevelHwnd is IntPtr.Zero
        tracker.OnTopLevelWindowChanged(Hwnd1);
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        // Should not have cleared anything
        Assert.Equal(1, tracker.Count);
    }

    // --- GetPersistentFields ---

    [Fact]
    public void GetPersistentFields_ReturnsOnlyUnfocused()
    {
        var tracker = new FieldTracker();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);
        tracker.Track(Bounds2, SensitivityKind.SensitiveText, Hwnd1);

        // Unfocus all, then re-focus just one
        tracker.UnfocusAll();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        var persistent = tracker.GetPersistentFields();

        Assert.Single(persistent);
        Assert.Equal(SensitivityKind.SensitiveText, persistent[0].Kind);
    }

    [Fact]
    public void GetPersistentFields_AllFocused_ReturnsEmpty()
    {
        var tracker = new FieldTracker();
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);

        var persistent = tracker.GetPersistentFields();

        Assert.Empty(persistent);
    }

    // --- Clear ---

    [Fact]
    public void Clear_RemovesEverything()
    {
        var tracker = new FieldTracker();
        tracker.OnTopLevelWindowChanged(Hwnd1);
        tracker.Track(Bounds1, SensitivityKind.Password, Hwnd1);
        tracker.Track(Bounds2, SensitivityKind.SensitiveText, Hwnd1);

        tracker.Clear();

        Assert.Equal(0, tracker.Count);
        Assert.Empty(tracker.GetPersistentFields());
    }
}
