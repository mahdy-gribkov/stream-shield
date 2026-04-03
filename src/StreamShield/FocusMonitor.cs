using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StreamShield;

/// <summary>
/// Monitors system-wide focus changes via SetWinEventHook(EVENT_OBJECT_FOCUS).
/// Debounces rapid focus changes (50ms) before raising the FocusDebounced event.
/// </summary>
sealed class FocusMonitor : IDisposable
{
    private IntPtr _hookHandle;
    private NativeMethods.WinEventProc? _winEventProc;
    private GCHandle _delegateHandle;
    private readonly Timer _debounceTimer;
    private bool _disposed;

    /// <summary>Fires on the UI thread after 50ms debounce following a focus change.</summary>
    public event Action? FocusDebounced;

    public bool IsHooked => _hookHandle != IntPtr.Zero;

    public FocusMonitor()
    {
        _debounceTimer = new Timer { Interval = 50 };
        _debounceTimer.Tick += OnDebounceTick;
    }

    /// <summary>Install the global focus hook. Must be called from the UI thread.</summary>
    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _winEventProc = OnWinEvent;
        _delegateHandle = GCHandle.Alloc(_winEventProc);

        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_FOCUS,
            NativeMethods.EVENT_OBJECT_FOCUS,
            IntPtr.Zero,
            _winEventProc,
            0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        if (_hookHandle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"SetWinEventHook failed with error {err}");
        }
    }

    /// <summary>Remove the hook and stop monitoring.</summary>
    public void Stop()
    {
        _debounceTimer.Stop();

        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        if (_delegateHandle.IsAllocated)
            _delegateHandle.Free();

        _winEventProc = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _debounceTimer.Dispose();
    }

    private void OnWinEvent(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Reset debounce: each new focus event restarts the 50ms window.
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        FocusDebounced?.Invoke();
    }
}
