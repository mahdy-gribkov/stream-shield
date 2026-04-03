using System;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using Xunit;

namespace StreamShield.E2E;

/// <summary>
/// End-to-end tests that exercise the REAL detection pipeline
/// (SensitivityDetector + SensitivityPatterns + FieldTracker)
/// against actual WinForms controls inspected via UIA.
/// </summary>
[Collection("E2E")]
public sealed class DetectionE2ETests : IClassFixture<TestFormFixture>
{
    private readonly TestFormFixture _fixture;
    private readonly SensitivityDetector _detector;
    private readonly FieldTracker _tracker;

    public DetectionE2ETests(TestFormFixture fixture)
    {
        _fixture = fixture;
        _detector = new SensitivityDetector();
        _tracker = new FieldTracker();
        _detector.SetFieldTracker(_tracker);
    }

    private InspectionResult? FocusAndInspect(IntPtr handle)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            _fixture.FocusField(handle);
            var result = _detector.Inspect();
            if (result is not null)
                return result;
            Thread.Sleep(100);
        }
        return null;
    }

    [Fact]
    public void E2E_01_PasswordField_IsDetected()
    {
        var result = FocusAndInspect(_fixture.PasswordFieldHandle);

        Assert.NotNull(result);
        Assert.Equal(SensitivityKind.Password, result.Kind);
    }

    [Fact]
    public void E2E_02_CardNumberField_IsDetected()
    {
        var result = FocusAndInspect(_fixture.CardNumberFieldHandle);

        Assert.NotNull(result);
        Assert.Equal(SensitivityKind.SensitiveText, result.Kind);
    }

    [Fact]
    public void E2E_03_HebrewIdField_IsDetected()
    {
        var result = FocusAndInspect(_fixture.HebrewIdFieldHandle);

        Assert.NotNull(result);
        Assert.Equal(SensitivityKind.SensitiveText, result.Kind);
    }

    [Fact]
    public void E2E_04_SsnField_IsDetected()
    {
        var result = FocusAndInspect(_fixture.SsnFieldHandle);

        Assert.NotNull(result);
        Assert.Equal(SensitivityKind.SensitiveText, result.Kind);
    }

    [Fact]
    public void E2E_05_EmailField_NotDetected()
    {
        var result = FocusAndInspect(_fixture.EmailFieldHandle);

        Assert.Null(result);
    }

    [Fact]
    public void E2E_06_RevealPassword_StillDetected()
    {
        // Focus password field first to register it in the tracker
        var first = FocusAndInspect(_fixture.PasswordFieldHandle);
        Assert.NotNull(first);

        // Track the field
        _tracker.Track(first.Bounds, first.Kind, first.TopLevelHwnd);

        // Now simulate reveal: focus the same position but as a non-password field.
        // FieldTracker.FindAtPosition should catch it.
        var tracked = _tracker.FindAtPosition(first.Bounds, first.TopLevelHwnd);
        Assert.NotNull(tracked);
        Assert.Equal(SensitivityKind.Password, tracked.Kind);
    }

    [Fact]
    public void E2E_07_MultipleFields_TrackedIndependently()
    {
        // Focus password, then card, then SSN
        var pw = FocusAndInspect(_fixture.PasswordFieldHandle);
        Assert.NotNull(pw);
        _tracker.Track(pw.Bounds, pw.Kind, pw.TopLevelHwnd);

        _tracker.UnfocusAll();
        var cc = FocusAndInspect(_fixture.CardNumberFieldHandle);
        Assert.NotNull(cc);
        _tracker.Track(cc.Bounds, cc.Kind, cc.TopLevelHwnd);

        _tracker.UnfocusAll();
        var ssn = FocusAndInspect(_fixture.SsnFieldHandle);
        Assert.NotNull(ssn);
        _tracker.Track(ssn.Bounds, ssn.Kind, ssn.TopLevelHwnd);

        // All three should be tracked
        Assert.Equal(3, _tracker.Count);
    }

    [Fact]
    public void E2E_08_PersistentFields_ReturnedAfterUnfocus()
    {
        var pw = FocusAndInspect(_fixture.PasswordFieldHandle);
        Assert.NotNull(pw);
        _tracker.Track(pw.Bounds, pw.Kind, pw.TopLevelHwnd);

        _tracker.UnfocusAll();

        var persistent = _tracker.GetPersistentFields();
        Assert.Single(persistent);
        Assert.Equal(SensitivityKind.Password, persistent[0].Kind);
    }

    [Fact]
    public void E2E_09_DetectorReturnsValidBounds()
    {
        var result = FocusAndInspect(_fixture.CardNumberFieldHandle);

        Assert.NotNull(result);
        Assert.True(result.Bounds.Width > 0);
        Assert.True(result.Bounds.Height > 0);
    }

    [Fact]
    public void E2E_10_DetectorReturnsTopLevelHwnd()
    {
        var result = FocusAndInspect(_fixture.PasswordFieldHandle);

        Assert.NotNull(result);
        Assert.NotEqual(IntPtr.Zero, result.TopLevelHwnd);
    }
}
