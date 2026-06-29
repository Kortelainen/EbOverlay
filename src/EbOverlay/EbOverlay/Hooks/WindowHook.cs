using System.Runtime.InteropServices;

namespace EbOverlay.Hooks;

/// <summary>
/// Wraps SetWinEventHook for EVENT_SYSTEM_FOREGROUND.
/// Fires ForegroundWindowChanged whenever the active window changes.
/// </summary>
public sealed class WindowHook : IDisposable
{
    public event Action<IntPtr>? ForegroundWindowChanged;

    private readonly WinEventDelegate _delegate;
    private readonly IntPtr _hook;

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT   = 0x0000;

    public WindowHook()
    {
        // Keep delegate alive — GC would collect it otherwise and crash the hook callback
        _delegate = OnWinEvent;
        _hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _delegate,
            0, 0, WINEVENT_OUTOFCONTEXT);
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd != IntPtr.Zero)
            ForegroundWindowChanged?.Invoke(hwnd);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            UnhookWinEvent(_hook);
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
}
