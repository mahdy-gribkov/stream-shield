using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace StreamShield;

/// <summary>
/// Tracks sensitive fields across focus changes.
/// Solves: multi-field persistence, reveal-password detection, window-change cleanup.
/// </summary>
sealed class FieldTracker
{
    private readonly Dictionary<int, TrackedField> _fields = [];
    private IntPtr _currentTopLevelHwnd;
    internal const int PositionTolerance = 15;

    public IReadOnlyCollection<TrackedField> TrackedFields => _fields.Values;
    public int Count => _fields.Count;

    /// <summary>
    /// Register a sensitive field. Called when focus enters a sensitive element.
    /// Returns the tracked field (existing or newly created).
    /// </summary>
    public TrackedField Track(Rectangle bounds, SensitivityKind kind, IntPtr topLevelHwnd,
        AutomationElement? element = null)
    {
        int key = MakeKey(bounds, topLevelHwnd);

        // Compute parent offset for fast-path position tracking
        Point parentOffset = default;
        Rectangle parentRect = default;
        if (NativeMethods.GetWindowRect(topLevelHwnd, out NativeMethods.RECT rect))
        {
            parentRect = rect.ToRectangle();
            parentOffset = new Point(bounds.X - parentRect.X, bounds.Y - parentRect.Y);
        }

        if (_fields.TryGetValue(key, out TrackedField? existing))
        {
            existing.Bounds = bounds;
            existing.LastSeen = DateTime.UtcNow;
            existing.IsFocused = true;
            existing.Element = element;
            existing.ParentOffset = parentOffset;
            existing.LastParentRect = parentRect;
            return existing;
        }

        var field = new TrackedField
        {
            Kind = kind,
            Bounds = bounds,
            TopLevelHwnd = topLevelHwnd,
            LastSeen = DateTime.UtcNow,
            IsFocused = true,
            Element = element,
            ParentOffset = parentOffset,
            LastParentRect = parentRect
        };

        _fields[key] = field;
        return field;
    }

    /// <summary>
    /// Mark all fields as not focused. Called before setting the new focused field.
    /// </summary>
    public void UnfocusAll()
    {
        foreach (TrackedField field in _fields.Values)
            field.IsFocused = false;
    }

    /// <summary>
    /// Check if a position matches a previously tracked sensitive field.
    /// Used to catch the reveal-password case (IsPassword flips to false).
    /// </summary>
    public TrackedField? FindAtPosition(Rectangle bounds, IntPtr topLevelHwnd)
    {
        foreach (TrackedField field in _fields.Values)
        {
            if (field.TopLevelHwnd != topLevelHwnd)
                continue;

            if (Math.Abs(field.Bounds.X - bounds.X) <= PositionTolerance &&
                Math.Abs(field.Bounds.Y - bounds.Y) <= PositionTolerance &&
                Math.Abs(field.Bounds.Width - bounds.Width) <= PositionTolerance &&
                Math.Abs(field.Bounds.Height - bounds.Height) <= PositionTolerance)
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>
    /// Called when the top-level window changes (user switched app/tab).
    /// Clears all tracked fields for the previous window.
    /// </summary>
    public void OnTopLevelWindowChanged(IntPtr newHwnd)
    {
        if (_currentTopLevelHwnd == newHwnd)
            return;

        IntPtr oldHwnd = _currentTopLevelHwnd;
        _currentTopLevelHwnd = newHwnd;

        if (oldHwnd == IntPtr.Zero)
            return;

        List<int> toRemove = [];
        foreach (var (key, field) in _fields)
        {
            if (field.TopLevelHwnd == oldHwnd)
                toRemove.Add(key);
        }

        foreach (int key in toRemove)
            _fields.Remove(key);
    }

    /// <summary>Remove fields whose parent window no longer exists.</summary>
    public List<TrackedField> PruneStale()
    {
        List<TrackedField> removed = [];
        List<int> toRemove = [];

        foreach (var (key, field) in _fields)
        {
            if (!NativeMethods.IsWindow(field.TopLevelHwnd))
            {
                toRemove.Add(key);
                removed.Add(field);
            }
        }

        foreach (int key in toRemove)
            _fields.Remove(key);

        return removed;
    }

    /// <summary>Clear all tracked fields.</summary>
    public void Clear()
    {
        _fields.Clear();
        _currentTopLevelHwnd = IntPtr.Zero;
    }

    /// <summary>Get all non-focused tracked fields (for persistent overlays).</summary>
    public List<TrackedField> GetPersistentFields()
    {
        return _fields.Values.Where(f => !f.IsFocused).ToList();
    }

    /// <summary>
    /// Re-read UIA bounds for all non-focused tracked fields.
    /// Removes fields whose AutomationElement has gone stale.
    /// </summary>
    public bool RefreshPersistentBounds()
    {
        List<int> toRemove = [];
        List<(int oldKey, TrackedField field, Rectangle newBounds)> toRekey = [];
        bool changed = false;

        foreach (var (key, field) in _fields)
        {
            if (field.IsFocused || field.Element is null)
                continue;

            try
            {
                System.Windows.Rect wpfRect = field.Element.Current.BoundingRectangle;

                if (wpfRect.IsEmpty || wpfRect.Width < 10 || wpfRect.Height < 5)
                {
                    toRemove.Add(key);
                    continue;
                }

                var newBounds = new Rectangle(
                    (int)wpfRect.X, (int)wpfRect.Y,
                    (int)wpfRect.Width, (int)wpfRect.Height);

                if (newBounds != field.Bounds)
                {
                    int newKey = MakeKey(newBounds, field.TopLevelHwnd);
                    if (newKey != key)
                        toRekey.Add((key, field, newBounds));
                    else
                        field.Bounds = newBounds;
                    changed = true;
                }
            }
            catch (Exception ex) when (
                ex is ElementNotAvailableException or
                COMException or
                InvalidOperationException)
            {
                toRemove.Add(key);
            }
        }

        foreach (var (oldKey, field, newBounds) in toRekey)
        {
            _fields.Remove(oldKey);
            field.Bounds = newBounds;
            _fields[MakeKey(newBounds, field.TopLevelHwnd)] = field;
        }

        foreach (int key in toRemove)
            _fields.Remove(key);

        return changed;
    }

    /// <summary>
    /// Fast-path bounds refresh using GetWindowRect instead of UIA.
    /// Sub-millisecond per field vs 10-50ms for UIA BoundingRectangle.
    /// Returns true if any bounds changed.
    /// </summary>
    public bool RefreshPersistentBoundsFast()
    {
        List<(int oldKey, TrackedField field, Rectangle newBounds)> toRekey = [];
        bool changed = false;

        foreach (var (key, field) in _fields)
        {
            if (field.IsFocused || field.LastParentRect == default)
                continue;

            if (!NativeMethods.GetWindowRect(field.TopLevelHwnd, out NativeMethods.RECT rect))
                continue;

            var currentParent = rect.ToRectangle();
            if (currentParent == field.LastParentRect)
                continue;

            // Parent window moved/resized. Estimate new field position from stored offset.
            var estimated = new Rectangle(
                currentParent.X + field.ParentOffset.X,
                currentParent.Y + field.ParentOffset.Y,
                field.Bounds.Width, field.Bounds.Height);

            if (estimated != field.Bounds)
            {
                int newKey = MakeKey(estimated, field.TopLevelHwnd);
                if (newKey != key)
                    toRekey.Add((key, field, estimated));
                else
                    field.Bounds = estimated;

                field.LastParentRect = currentParent;
                changed = true;
            }
        }

        foreach (var (oldKey, field, newBounds) in toRekey)
        {
            _fields.Remove(oldKey);
            field.Bounds = newBounds;
            _fields[MakeKey(newBounds, field.TopLevelHwnd)] = field;
        }

        return changed;
    }

    private static int MakeKey(Rectangle bounds, IntPtr hwnd)
    {
        return HashCode.Combine(
            bounds.X / PositionTolerance,
            bounds.Y / PositionTolerance,
            hwnd);
    }
}

sealed class TrackedField
{
    public required SensitivityKind Kind { get; set; }
    public required Rectangle Bounds { get; set; }
    public required IntPtr TopLevelHwnd { get; set; }
    public required DateTime LastSeen { get; set; }
    public bool IsFocused { get; set; }
    public AutomationElement? Element { get; set; }

    /// <summary>Field position relative to parent window top-left. Set at Track() time.</summary>
    public Point ParentOffset { get; set; }

    /// <summary>Parent window rect when ParentOffset was last computed.</summary>
    public Rectangle LastParentRect { get; set; }
}
