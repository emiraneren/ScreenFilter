using System.Windows;

namespace ScreenFilter;

public partial class App : Application
{
    private Mutex? _single;

    public static string[] Args { get; private set; } = [];

    protected override void OnStartup(StartupEventArgs e)
    {
        Args = e.Args;
        bool testMode = e.Args.Any(a => a.StartsWith("--selftest") || a.StartsWith("--uishot"));
        _single = new Mutex(true, "ScreenFilter.SingleInstance", out bool first);
        if (!first && !testMode)
        {
            MessageBox.Show("Screen Filter is already running (check the system tray).\nScreen Filter zaten çalışıyor (sistem tepsisine bak).",
                "Screen Filter", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }
}
