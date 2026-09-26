using System.Runtime.InteropServices;
using static ScreenFilter.Services.Native;

namespace ScreenFilter.Services;

/// <summary>Click-through, always-on-top native window that hosts the swap chain. Runs its own message loop thread.</summary>
public sealed class OverlayWindow : IDisposable
{
    private const string ClassName = "ScreenFilterOverlay";
    private static readonly WndProc Proc = (h, m, w, l) =>
    {
        if (m == 0x10) { DestroyWindow(h); return IntPtr.Zero; }
        if (m == WM_DESTROY) { PostQuitMessage(0); return IntPtr.Zero; }
        return DefWindowProc(h, m, w, l);
    };
    private static bool _classRegistered;

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private RECT _initial;
    private bool _excludeFromCapture;

    public IntPtr Handle { get; private set; }
    public bool Visible { get; private set; }

    public OverlayWindow(RECT bounds, bool excludeFromCapture)
    {
        _initial = bounds;
        _excludeFromCapture = excludeFromCapture;
        _thread = new Thread(Run) { IsBackground = true, Name = "ScreenFilter overlay" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(5000) || Handle == IntPtr.Zero) throw new InvalidOperationException("Overlay window could not be created");
    }

    private void Run()
    {
        var inst = GetModuleHandle(null);
        if (!_classRegistered)
        {
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Proc),
                hInstance = inst,
                hCursor = LoadCursor(IntPtr.Zero, 32512),
                lpszClassName = ClassName,
            };
            RegisterClassEx(ref wc);
            _classRegistered = true;
        }

        Handle = CreateWindowEx(
            WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
            ClassName, "ScreenFilter overlay", WS_POPUP,
            _initial.Left, _initial.Top, _initial.Width, _initial.Height, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);

        if (Handle != IntPtr.Zero)
        {
            SetLayeredWindowAttributes(Handle, 0, 255, LWA_ALPHA);
            if (_excludeFromCapture) SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
        }
        _ready.Set();
        if (Handle == IntPtr.Zero) return;

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    public void SetBounds(RECT r)
    {
        SetWindowPos(Handle, HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height, SWP_NOACTIVATE);
    }

    public void SetVisible(bool visible)
    {
        if (visible == Visible) return;
        Visible = visible;
        ShowWindow(Handle, visible ? SW_SHOWNOACTIVATE : SW_HIDE);
        if (visible) SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, 0x1 | 0x2 | SWP_NOACTIVATE);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            ShowWindow(Handle, SW_HIDE);
            PostMessage(Handle, 0x10, IntPtr.Zero, IntPtr.Zero);
        }
        _thread.Join(1000);
        Handle = IntPtr.Zero;
    }
}
