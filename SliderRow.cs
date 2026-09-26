using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenFilter.Models;
using ScreenFilter.Services;

namespace ScreenFilter;

public sealed class SliderRow
{
    private readonly FilterSettings _target;
    private readonly string _labelKey;
    private readonly float _default;
    private readonly string _format;
    private readonly Func<FilterSettings, float> _get;
    private readonly Action<FilterSettings, float> _set;
    private readonly TextBlock _label = new() { FontSize = 12.5 };
    private readonly TextBlock _value = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, Foreground = (Brush)Application.Current.Resources["AccentBrush"] };
    private readonly Slider _slider = new() { SmallChange = 0.01, LargeChange = 0.1 };
    private bool _updating;

    public FrameworkElement Root { get; }
    public bool FastSupported { get; }

    public event Action<SliderRow>? Changed;

    public SliderRow(FilterSettings target, string labelKey, float min, float max, float def, string format, bool fastSupported,
        Func<FilterSettings, float> get, Action<FilterSettings, float> set, Brush? track = null)
    {
        _target = target;
        _labelKey = labelKey;
        _default = def;
        _format = format;
        _get = get;
        _set = set;
        FastSupported = fastSupported;

        _slider.Minimum = min;
        _slider.Maximum = max;
        _slider.Value = def;
        _slider.SmallChange = (max - min) / 200.0;
        _slider.LargeChange = (max - min) / 20.0;
        if (track != null) _slider.Background = track;

        var head = new Grid();
        head.Children.Add(_label);
        head.Children.Add(_value);
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12), Cursor = Cursors.Arrow };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_slider, 1);
        grid.Children.Add(head);
        grid.Children.Add(_slider);

        var host = new Border { Width = 320, Margin = new Thickness(0, 0, 30, 0), Child = grid };
        Root = host;

        _slider.ValueChanged += (_, e) =>
        {
            _value.Text = _slider.Value.ToString(_format);
            if (_updating) return;
            _set(_target, (float)_slider.Value);
            Changed?.Invoke(this);
        };
        _label.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) _slider.Value = _default;
        };
        _label.ToolTip = null;

        Relabel();
    }

    public void Relabel() => _label.Text = Loc.T(_labelKey);

    public void Refresh(FilterSettings s)
    {
        _updating = true;
        _slider.Value = _get(s);
        _value.Text = _slider.Value.ToString(_format);
        _updating = false;
    }

    public void SetDimmed(bool dimmed)
    {
        Root.Opacity = dimmed ? 0.35 : 1.0;
        _slider.IsEnabled = !dimmed;
        Root.ToolTip = dimmed ? Loc.T("s.unsupported") : null;
    }
}
