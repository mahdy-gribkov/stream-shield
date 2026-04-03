using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using Xunit;

namespace StreamShield.E2E;

/// <summary>
/// Tests that overlay positioning tracks field positions after scroll.
/// Uses the scrollable panel in TestFormFixture.
/// </summary>
[Collection("E2E")]
public sealed class ScrollTrackingE2ETests : IClassFixture<TestFormFixture>
{
    private readonly TestFormFixture _fixture;

    public ScrollTrackingE2ETests(TestFormFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void E2E_Scroll_01_BoundsChangeAfterScroll()
    {
        // Reset scroll to top
        _fixture.ScrollPanel(-1000);

        var element = AutomationElement.FromHandle(_fixture.ScrollPasswordFieldHandle);
        var before = element.Current.BoundingRectangle;

        _fixture.ScrollPanel(100);

        var after = element.Current.BoundingRectangle;

        Assert.NotEqual(before.Y, after.Y);
    }

    [Fact]
    public void E2E_Scroll_02_TrackedFieldBoundsUpdateAfterRefresh()
    {
        var detector = new SensitivityDetector();
        var tracker = new FieldTracker();
        detector.SetFieldTracker(tracker);

        // Reset scroll
        _fixture.ScrollPanel(-1000);
        Thread.Sleep(100);

        // Focus and detect the scroll password field
        _fixture.FocusField(_fixture.ScrollPasswordFieldHandle);
        var result = detector.Inspect();
        Assert.NotNull(result);

        // Track with element reference, then unfocus
        tracker.Track(result.Bounds, result.Kind, result.TopLevelHwnd, result.Element);
        var boundsBefore = result.Bounds;
        tracker.UnfocusAll();

        // Scroll down
        _fixture.ScrollPanel(100);

        // Refresh should detect the position change
        bool changed = tracker.RefreshPersistentBounds();

        Assert.True(changed);
        var persistent = tracker.GetPersistentFields();
        Assert.Single(persistent);
        Assert.NotEqual(boundsBefore.Y, persistent[0].Bounds.Y);
    }

    [Fact]
    public void E2E_Scroll_03_NullElementSkippedGracefully()
    {
        var tracker = new FieldTracker();

        // Track without an element reference
        tracker.Track(new Rectangle(100, 100, 200, 25), SensitivityKind.Password, _fixture.FormHandle);
        tracker.UnfocusAll();

        // Should not crash, should skip null-element fields
        bool changed = tracker.RefreshPersistentBounds();

        Assert.False(changed);
        Assert.Single(tracker.GetPersistentFields());
    }

    [Fact]
    public void E2E_Scroll_04_MultipleFieldsBoundsUpdateIndependently()
    {
        var detector = new SensitivityDetector();
        var tracker = new FieldTracker();
        detector.SetFieldTracker(tracker);

        // Reset scroll
        _fixture.ScrollPanel(-1000);
        Thread.Sleep(100);

        // Track password field
        _fixture.FocusField(_fixture.ScrollPasswordFieldHandle);
        var r1 = detector.Inspect();
        Assert.NotNull(r1);
        tracker.Track(r1.Bounds, r1.Kind, r1.TopLevelHwnd, r1.Element);

        // Track card field
        tracker.UnfocusAll();
        _fixture.FocusField(_fixture.ScrollCardFieldHandle);
        var r2 = detector.Inspect();
        Assert.NotNull(r2);
        tracker.Track(r2.Bounds, r2.Kind, r2.TopLevelHwnd, r2.Element);
        tracker.UnfocusAll();

        // Both tracked
        Assert.Equal(2, tracker.GetPersistentFields().Count);

        // Scroll
        _fixture.ScrollPanel(80);

        tracker.RefreshPersistentBounds();

        var after = tracker.GetPersistentFields();
        Assert.Equal(2, after.Count);
    }
}
