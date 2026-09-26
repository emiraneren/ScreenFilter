using static ScreenFilter.Services.Native;

namespace ScreenFilter.Services;

public sealed record MonitorSource(IntPtr Handle, RECT Bounds, string Device, bool Primary)
{
    public override string ToString() =>
        $"{Device.TrimStart('\\', '.')}  {Bounds.Width}x{Bounds.Height}" + (Primary ? $"  ({Loc.T("source.primary")})" : "");
}

public sealed record WindowSource(IntPtr Handle, string Title, uint ProcessId)
{
    public override string ToString() => Title.Length > 70 ? Title[..70] + "…" : Title;
}

public static class Sources
{
    public static List<MonitorSource> Monitors()
    {
        var list = new List<MonitorSource>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(h, ref info))
                list.Add(new MonitorSource(h, info.rcMonitor, info.szDevice, (info.dwFlags & 1) != 0));
            return true;
        }, IntPtr.Zero);
        return list.OrderByDescending(m => m.Primary).ToList();
    }

    public static List<WindowSource> Windows(IntPtr exclude)
    {
        var list = new List<WindowSource>();
        EnumWindows((h, _) =>
        {
            if (h == exclude || !IsWindowVisible(h) || IsIconic(h)) return true;
            if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return true;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return true;
            string title = GetTitle(h);
            if (title.Length == 0) return true;
            var b = GetVisibleBounds(h);
            if (b.Width < 200 || b.Height < 150) return true;
            GetWindowThreadProcessId(h, out uint pid);
            list.Add(new WindowSource(h, title, pid));
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
