using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace StreamShield;

sealed record InspectionResult(
    SensitivityKind Kind,
    Rectangle Bounds,
    string Description,
    IntPtr TopLevelHwnd,
    string? FieldValue);

sealed class SensitivityDetector
{
    private readonly CacheRequest _cacheRequest;
    private FieldTracker? _fieldTracker;

    public SensitivityDetector()
    {
        _cacheRequest = new CacheRequest();
        _cacheRequest.Add(AutomationElement.IsPasswordProperty);
        _cacheRequest.Add(AutomationElement.ControlTypeProperty);
        _cacheRequest.Add(AutomationElement.NameProperty);
        _cacheRequest.Add(AutomationElement.AutomationIdProperty);
        _cacheRequest.Add(AutomationElement.BoundingRectangleProperty);
        _cacheRequest.Add(AutomationElement.NativeWindowHandleProperty);
        _cacheRequest.Add(AutomationElement.LabeledByProperty);
        _cacheRequest.Add(AutomationElement.HelpTextProperty);
        _cacheRequest.Add(AutomationElement.ClassNameProperty);
        _cacheRequest.TreeScope = TreeScope.Element;
    }

    public void SetFieldTracker(FieldTracker tracker) => _fieldTracker = tracker;

    public InspectionResult? Inspect()
    {
        try
        {
            AutomationElement? focused;
            using (_cacheRequest.Activate())
            {
                focused = AutomationElement.FocusedElement;
            }

            if (focused is null)
                return null;

            // Validate bounds first (applies to all detection paths)
            System.Windows.Rect wpfRect = focused.Cached.BoundingRectangle;

            if (wpfRect.IsEmpty || wpfRect.Width < 10 || wpfRect.Height < 5)
                return null;

            var bounds = new Rectangle(
                (int)wpfRect.X, (int)wpfRect.Y,
                (int)wpfRect.Width, (int)wpfRect.Height);

            // Get top-level HWND
            IntPtr nativeHwnd = new((int)focused.Cached.NativeWindowHandle);
            IntPtr topLevelHwnd = nativeHwnd != IntPtr.Zero
                ? NativeMethods.GetAncestor(nativeHwnd, NativeMethods.GA_ROOT)
                : NativeMethods.GetForegroundWindow();

            // Skip browser chrome UI elements (address bar, search bar)
            string? className = GetCachedString(focused, AutomationElement.ClassNameProperty);
            if (className is "OmniboxViewViews" or "Chrome_OmniboxView")
                return null;

            // IsPassword check FIRST, regardless of ControlType.
            // Chrome may expose password fields as Custom, Group, or ComboBox.
            if (focused.Cached.IsPassword)
            {
                return new InspectionResult(
                    SensitivityKind.Password, bounds, "Password field",
                    topLevelHwnd, null);
            }

            // ControlType filter for keyword-based detection only.
            // Password fields already returned above.
            ControlType controlType = focused.Cached.ControlType;

            if (controlType != ControlType.Edit &&
                controlType != ControlType.Document &&
                controlType != ControlType.ComboBox &&
                controlType != ControlType.Custom &&
                controlType != ControlType.List &&
                controlType != ControlType.Text)
                return null;

            // Gather all text properties for keyword matching
            string? name = GetCachedString(focused, AutomationElement.NameProperty);
            string? automationId = GetCachedString(focused, AutomationElement.AutomationIdProperty);
            string? helpText = GetCachedString(focused, AutomationElement.HelpTextProperty);

            string? labeledByName = null;
            var labeledBy = focused.GetCachedPropertyValue(AutomationElement.LabeledByProperty)
                as AutomationElement;
            if (labeledBy is not null)
                try { labeledByName = labeledBy.Current.Name; } catch { }

            if (SensitivityPatterns.IsSensitiveField(name, automationId, labeledByName, helpText))
            {
                string desc = !string.IsNullOrEmpty(name) ? name
                    : !string.IsNullOrEmpty(labeledByName) ? labeledByName
                    : automationId ?? "Sensitive field";
                string? value = ReadFieldValue(focused);
                return new InspectionResult(
                    SensitivityKind.SensitiveText, bounds, desc,
                    topLevelHwnd, value);
            }

            // Reveal-password fallback: position match against FieldTracker
            if (_fieldTracker is not null)
            {
                TrackedField? tracked = _fieldTracker.FindAtPosition(bounds, topLevelHwnd);
                if (tracked is not null)
                {
                    string? value = ReadFieldValue(focused);
                    return new InspectionResult(
                        tracked.Kind, bounds, "Tracked field (revealed)",
                        topLevelHwnd, value);
                }
            }

            return null;
        }
        catch (Exception ex) when (
            ex is ElementNotAvailableException or
            COMException or
            InvalidOperationException or
            NullReferenceException)
        {
            return null;
        }
    }

    private static string? ReadFieldValue(AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object? pattern))
            {
                string value = ((ValuePattern)pattern).Current.Value;
                return string.IsNullOrEmpty(value) ? null : value;
            }
        }
        catch
        {
            // ValuePattern not supported or element stale
        }

        return null;
    }

    private static string? GetCachedString(AutomationElement element, AutomationProperty property)
    {
        object? val = element.GetCachedPropertyValue(property);
        return val as string;
    }
}
