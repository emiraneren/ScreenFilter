using static ScreenFilter.Services.Native;

namespace ScreenFilter.Services;

/// <summary>
/// Watches whether a specific window is the foreground window, using the real OS focus-change event
/// (no polling). Used by Fast mode's "only while the game is focused" option, so the system-wide color
/// effect turns on the instant you switch into the game and off the instant you switch away — the same
/// scoping GamePP appears to use for its own screenshot-visible filter.
/// </summary>
public sealed class GameFocusWatcher : IDisposable
{
    private readonly IntPtr _target;
    private readonly Action<bool> _onFocusChanged;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private WinEventDelegate? _proc;
    private IntPtr _hookForeground, _hookMinimize;
    private uint _threadId;
    private bool _lastFocused;
    private bool _disposed;

    public GameFocusWatcher(IntPtr target, Action<bool> onFocusChanged)
    {
        _target = target;
        _onFocusChanged = onFocusChanged;
        _thread = new Thread(Run) { IsBackground = true, Name = "ScreenFilter focus watcher" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(2000);
        Check();
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        _proc = (_, _, _, idObject, _, _, _) =>
        {
            if (idObject != 0) return;
            Check();
        };
        _hookForeground = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
        _hookMinimize = SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
        _ready.Set();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookForeground != IntPtr.Zero) UnhookWinEvent(_hookForeground);
        if (_hookMinimize != IntPtr.Zero) UnhookWinEvent(_hookMinimize);
    }

    private void Check()
    {
        if (_disposed) return;
        bool focused = IsWindow(_target) && !IsIconic(_target) && GetForegroundWindow() == _target;
        if (focused == _lastFocused) return;
        _lastFocused = focused;
        _onFocusChanged(focused);
    }

    public void Dispose()
    {
        _disposed = true;
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
    }
}
