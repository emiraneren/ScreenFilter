using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenFilter.Services;

namespace ScreenFilter;

/// <summary>Small click-through message shown at the top of the screen (hotkey feedback while gaming).</summary>
public sealed class ToastWindow : Window
{
    private readonly TextBlock _text;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1600) };

    public ToastWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;

        _text = new TextBlock { Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.SemiBold };
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(225, 14, 17, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(61, 214, 208)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(22, 12, 22, 12),
            Child = _text,
        };
        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
        };
    }

    public void ShowMessage(string message)
    {
        _text.Text = message;
        if (!IsVisible) Show();
        UpdateLayout();
        Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
        Top = 48;
        _timer.Stop();
        _timer.Start();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
