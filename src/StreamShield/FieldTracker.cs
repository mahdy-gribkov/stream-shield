using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace StreamShield;

/// <summary>
/// Tracks sensitive fields across focus changes.
/// Solves: multi-field persistence, reveal-password detection, window-change cleanup.
/// </summary>
sealed class FieldTracker
{
    private readonly Dictionary<int, TrackedField> _fields = [];
    private IntPtr _currentTopLevelHwnd;
    private const int PositionTolerance = 15;

    public IReadOnlyCollection<TrackedField> TrackedFields => _fields.Values;
    public int Count => _fields.Count;

    /// <summary>
    /// Register a sensitive field. Called when focus enters a sensitive element.
    /// Returns the tracked field (existing or newly created).
    /// </summary>
    public TrackedField Track(Rectangle bounds, SensitivityKind kind, IntPtr topLevelHwnd)
    {
        int key = MakeKey(bounds, topLevelHwnd);

        if (_fields.TryGetValue(key, out TrackedField? existing))
        {
            existing.Bounds = bounds;
            existing.LastSeen = DateTime.UtcNow;
            existing.IsFocused = true;
            return existing;
        }

        var field = new TrackedField
        {
            Kind = kind,
            Bounds = bounds,
            TopLevelHwnd = topLevelHwnd,
            LastSeen = DateTime.UtcNow,
            IsFocused = true
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
}
