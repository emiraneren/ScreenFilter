using System.Windows;

namespace ScreenFilter;

public partial class App : Application
{
    public static string[] Args { get; private set; } = [];

    protected override void OnStartup(StartupEventArgs e)
    {
        Args = e.Args;
        base.OnStartup(e);
    }
}
