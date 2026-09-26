using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenFilter.Models;
using ScreenFilter.Services;

namespace ScreenFilter;

public partial class MainWindow : Window
{
    private readonly AppConfig _cfg = ConfigStore.Load();
    private readonly FilterSettings _cur;
    private readonly List<Preset> _builtIn = BuiltInPresets.Create();
    private readonly List<SliderRow> _rows = [];
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ToastWindow _toast = new();
    private System.Windows.Forms.NotifyIcon? _tray;

    private CaptureEngine? _engine;
    private bool _fastActive;
    private bool _exiting;
    private bool _suppress;
    private long _lastFrames;
    private IntPtr _hwnd;
    private string _statusKey = "status.off";
    private object[] _statusArgs = [];

    private IEnumerable<Preset> AllPresets => _builtIn.Concat(_cfg.CustomPresets);
    private bool Running => _engine != null || _fastActive;

    public MainWindow()
    {
        _cur = _cfg.Current;
        Loc.Instance.Language = _cfg.Language;
        InitializeComponent();
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/AppIcon.ico"));

        BuildSliders();
        BuildColorBlindCombo();

        _suppress = true;
        (_cfg.Language == "tr" ? LangTr : LangEn).IsChecked = true;
        (_cfg.Mode switch { CaptureMode.Fast => ModeFast, CaptureMode.Window => ModeWindow, _ => ModeMonitor }).IsChecked = true;
        OptForeground.IsChecked = _cfg.OnlyWhenForeground;
        OptExclude.IsChecked = _cfg.ExcludeFromCapture;
        OptTray.IsChecked = _cfg.KeepInTrayOnClose;
        _suppress = false;

        BuildPresetChips();
        RefreshAll();
        RefreshSources();
        UpdateModeUi();
        SetStatus("status.off");

        Loc.Instance.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Loc.Language)) OnLanguageChanged(); };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); ConfigStore.Save(_cfg); };
        _statusTimer.Tick += (_, _) => UpdateFps();
        _statusTimer.Start();

        SourceInitialized += OnSourceInitialized;
        ContentRendered += (_, _) =>
        {
            var idx = Array.IndexOf(App.Args, "--selftest");
            if (idx >= 0 && idx + 1 < App.Args.Length) RunSelfTest(App.Args[idx + 1]);
            var shot = Array.IndexOf(App.Args, "--uishot");
            if (shot >= 0 && shot + 1 < App.Args.Length) RunUiShot(App.Args[shot + 1]);
        };
        SetupTray();
    }

    // ---------------------------------------------------------------- sliders

    private void BuildSliders()
    {
        var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++)
            rainbow.GradientStops.Add(new GradientStop(HsvColor(i * 60.0), i / 6.0));

        void Add(Panel panel, string key, float min, float max, float def, string fmt, bool fast,
            Func<FilterSettings, float> get, Action<FilterSettings, float> set, Brush? track = null)
        {
            var row = new SliderRow(_cur, key, min, max, def, fmt, fast, get, set, track);
            row.Changed += OnSliderChanged;
            _rows.Add(row);
            panel.Children.Add(row.Root);
        }

        Add(LightPanel, "s.brightness", -0.5f, 0.5f, 0f, "0.00", true, s => s.Brightness, (s, v) => s.Brightness = v);
        Add(LightPanel, "s.contrast", 0.5f, 2f, 1f, "0.00", true, s => s.Contrast, (s, v) => s.Contrast = v);
        Add(LightPanel, "s.gamma", 0.4f, 2.5f, 1f, "0.00", false, s => s.Gamma, (s, v) => s.Gamma = v);
        Add(LightPanel, "s.exposure", -1f, 1.5f, 0f, "0.00", true, s => s.Exposure, (s, v) => s.Exposure = v);
        Add(LightPanel, "s.shadow", 0f, 1f, 0f, "0.00", false, s => s.ShadowLift, (s, v) => s.ShadowLift = v);
        Add(LightPanel, "s.highlights", 0f, 1f, 0f, "0.00", false, s => s.Highlights, (s, v) => s.Highlights = v);

        Add(ColorPanel, "s.saturation", 0f, 2.5f, 1f, "0.00", true, s => s.Saturation, (s, v) => s.Saturation = v);
        Add(ColorPanel, "s.vibrance", -1f, 1f, 0f, "0.00", false, s => s.Vibrance, (s, v) => s.Vibrance = v);
        Add(ColorPanel, "s.hue", -180f, 180f, 0f, "0'°'", true, s => s.Hue, (s, v) => s.Hue = v);
        Add(ColorPanel, "s.temperature", -1f, 1f, 0f, "0.00", true, s => s.Temperature, (s, v) => s.Temperature = v);
        Add(ColorPanel, "s.tint", -1f, 1f, 0f, "0.00", true, s => s.Tint, (s, v) => s.Tint = v);
        Add(ColorPanel, "s.grayscale", 0f, 1f, 0f, "0.00", true, s => s.Grayscale, (s, v) => s.Grayscale = v);
        Add(ColorPanel, "s.red", 0.4f, 1.6f, 1f, "0.00", true, s => s.Red, (s, v) => s.Red = v);
        Add(ColorPanel, "s.green", 0.4f, 1.6f, 1f, "0.00", true, s => s.Green, (s, v) => s.Green = v);
        Add(ColorPanel, "s.blue", 0.4f, 1.6f, 1f, "0.00", true, s => s.Blue, (s, v) => s.Blue = v);

        Add(DetailPanel, "s.sharpen", 0f, 2f, 0f, "0.00", false, s => s.Sharpen, (s, v) => s.Sharpen = v);
        Add(DetailPanel, "s.clarity", 0f, 1f, 0f, "0.00", false, s => s.Clarity, (s, v) => s.Clarity = v);
        Add(DetailPanel, "s.dehaze", 0f, 1f, 0f, "0.00", false, s => s.Dehaze, (s, v) => s.Dehaze = v);

        Add(TargetPanel, "s.targetamount", 0f, 1f, 0f, "0.00", false, s => s.TargetAmount, (s, v) => s.TargetAmount = v);
        Add(TargetPanel, "s.targethue", 0f, 360f, 0f, "0'°'", false, s => s.TargetHue, (s, v) => s.TargetHue = v, rainbow);
        Add(TargetPanel, "s.targetrange", 5f, 90f, 30f, "0'°'", false, s => s.TargetRange, (s, v) => s.TargetRange = v);
    }

    private static Color HsvColor(double h)
    {
        h %= 360.0;
        double c = 1.0, x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x),
            < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x),
        };
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private void OnSliderChanged(SliderRow row)
    {
        if (_suppress) return;
        ClearPresetSelection();
        ApplyLive();
        ScheduleSave();
    }

    private void BuildColorBlindCombo()
    {
        int sel = Math.Max(ColorBlindCombo.SelectedIndex, 0);
        _suppress = true;
        ColorBlindCombo.Items.Clear();
        foreach (var k in new[] { "cb.none", "cb.protan", "cb.deutan", "cb.tritan" }) ColorBlindCombo.Items.Add(Loc.T(k));
        ColorBlindCombo.SelectedIndex = sel;
        _suppress = false;
    }

    private void ColorBlind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || ColorBlindCombo.SelectedIndex < 0) return;
        _cur.ColorBlind = (ColorBlindMode)ColorBlindCombo.SelectedIndex;
        ClearPresetSelection();
        ApplyLive();
        ScheduleSave();
    }

    private void Invert_Click(object sender, RoutedEventArgs e)
    {
        _cur.Invert = InvertCheck.IsChecked == true;
        ClearPresetSelection();
        ApplyLive();
        ScheduleSave();
    }

    /// <summary>Pushes the current settings to the sliders and extra controls.</summary>
    private void RefreshAll()
    {
        _suppress = true;
        foreach (var r in _rows) r.Refresh(_cur);
        InvertCheck.IsChecked = _cur.Invert;
        ColorBlindCombo.SelectedIndex = (int)_cur.ColorBlind;
        _suppress = false;
        UpdateDimming();
    }

    private void UpdateDimming()
    {
        bool fast = _cfg.Mode == CaptureMode.Fast;
        foreach (var r in _rows) r.SetDimmed(fast && !r.FastSupported);
    }

    // ---------------------------------------------------------------- presets

    private void BuildPresetChips()
    {
        PresetPanel.Children.Clear();
        foreach (var p in AllPresets)
        {
            var chip = new RadioButton
            {
                GroupName = "preset",
                Style = (Style)FindResource("Chip"),
                Content = p.IsBuiltIn ? p.Name : "★ " + p.Name,
                Tag = p,
                ToolTip = p.IsBuiltIn ? p.Description : null,
                IsChecked = p.Id == _cfg.LastPresetId,
            };
            chip.Checked += Chip_Checked;
            PresetPanel.Children.Add(chip);
        }
        UpdatePresetInfo();
    }

    private void Chip_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppress || sender is not RadioButton { Tag: Preset p }) return;
        ApplyPreset(p);
    }

    private void ApplyPreset(Preset p)
    {
        _cur.CopyFrom(p.Settings);
        _cfg.LastPresetId = p.Id;
        RefreshAll();
        ApplyLive();
        UpdatePresetInfo();
        ScheduleSave();
    }

    private void ClearPresetSelection()
    {
        if (_cfg.LastPresetId == null) return;
        _cfg.LastPresetId = null;
        _suppress = true;
        foreach (RadioButton c in PresetPanel.Children) c.IsChecked = false;
        _suppress = false;
        UpdatePresetInfo();
    }

    private void UpdatePresetInfo()
    {
        var p = AllPresets.FirstOrDefault(x => x.Id == _cfg.LastPresetId);
        PresetDesc.Text = p?.Description ?? "";
        DeletePresetBtn.Visibility = p is { IsBuiltIn: false } ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectPresetChip(Preset p)
    {
        _suppress = true;
        foreach (RadioButton c in PresetPanel.Children) c.IsChecked = ((Preset)c.Tag).Id == p.Id;
        _suppress = false;
        ApplyPreset(p);
    }

    private void CyclePreset(int dir)
    {
        var list = AllPresets.ToList();
        int i = list.FindIndex(x => x.Id == _cfg.LastPresetId);
        i = i < 0 ? (dir > 0 ? 0 : list.Count - 1) : (i + dir + list.Count) % list.Count;
        SelectPresetChip(list[i]);
        _toast.ShowMessage(list[i].Name);
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetNameBox.Text.Trim();
        if (name.Length == 0) { PresetNameBox.Focus(); return; }
        var existing = _cfg.CustomPresets.FirstOrDefault(p => string.Equals(p.CustomName, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) { existing.Settings = _cur.Clone(); _cfg.LastPresetId = existing.Id; }
        else
        {
            var p = new Preset { Id = "custom:" + Guid.NewGuid().ToString("N"), CustomName = name, Settings = _cur.Clone() };
            _cfg.CustomPresets.Add(p);
            _cfg.LastPresetId = p.Id;
        }
        PresetNameBox.Text = "";
        BuildPresetChips();
        ScheduleSave();
        SetStatus("presets.saved");
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        var p = _cfg.CustomPresets.FirstOrDefault(x => x.Id == _cfg.LastPresetId);
        if (p == null) return;
        _cfg.CustomPresets.Remove(p);
        _cfg.LastPresetId = null;
        BuildPresetChips();
        ScheduleSave();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => SelectPresetChip(_builtIn[0]);

    // ---------------------------------------------------------------- mode / sources

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppress || sender is not RadioButton { Tag: string tag }) return;
        _cfg.Mode = Enum.Parse<CaptureMode>(tag);
        RefreshSources();
        UpdateModeUi();
        UpdateDimming();
        ScheduleSave();
        if (Running) Start();
    }

    private void UpdateModeUi()
    {
        ModeDesc.Text = Loc.T(_cfg.Mode switch { CaptureMode.Fast => "mode.fast.desc", CaptureMode.Window => "mode.window.desc", _ => "mode.monitor.desc" });
        SourcePanel.Visibility = _cfg.Mode == CaptureMode.Fast ? Visibility.Collapsed : Visibility.Visible;
        SourceLabel.Text = Loc.T(_cfg.Mode == CaptureMode.Window ? "source.window" : "source.monitor").ToUpperInvariant();
        OptForeground.Visibility = _cfg.Mode == CaptureMode.Window ? Visibility.Visible : Visibility.Collapsed;
        OptExclude.Visibility = _cfg.Mode == CaptureMode.Window ? Visibility.Visible : Visibility.Collapsed;
        StartBtn.Content = Loc.T(Running ? "btn.stop" : "btn.start");
        StartBtn.Style = (Style)FindResource(Running ? "DangerButton" : "PrimaryButton");
    }

    private void RefreshSources()
    {
        if (_cfg.Mode == CaptureMode.Fast) return;
        object? previous = SourceCombo.SelectedItem;
        SourceCombo.Items.Clear();
        if (_cfg.Mode == CaptureMode.Monitor)
        {
            foreach (var m in Sources.Monitors()) SourceCombo.Items.Add(m);
            SourceCombo.SelectedIndex = 0;
        }
        else
        {
            foreach (var w in Sources.Windows(_hwnd)) SourceCombo.Items.Add(w);
            var keep = previous is WindowSource pw ? SourceCombo.Items.OfType<WindowSource>().FirstOrDefault(x => x.Handle == pw.Handle) : null;
            if (keep != null) SourceCombo.SelectedItem = keep;
            else if (SourceCombo.Items.Count > 0) SourceCombo.SelectedIndex = 0;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshSources();

    private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    // ---------------------------------------------------------------- start / stop

    private void Start_Click(object sender, RoutedEventArgs e) => Toggle();

    private void Toggle()
    {
        if (Running) Stop();
        else Start();
    }

    private void Start()
    {
        Stop();
        try
        {
            switch (_cfg.Mode)
            {
                case CaptureMode.Fast:
                    if (!MagnifierEffect.Apply(_cur)) throw new InvalidOperationException("Magnification API");
                    _fastActive = true;
                    SetStatus("status.on.fast", StatusKind.Ok);
                    break;

                case CaptureMode.Monitor:
                    {
                        var mon = SourceCombo.SelectedItem as MonitorSource ?? Sources.Monitors().First();
                        _engine = new CaptureEngine(mon, null, _cur, false, true, _hwnd);
                        SetStatus("status.on.monitor", StatusKind.Ok, mon.ToString());
                        break;
                    }

                case CaptureMode.Window:
                    {
                        if (SourceCombo.SelectedItem is not WindowSource win) { SetStatus("status.nowindow", StatusKind.Error); return; }
                        _engine = new CaptureEngine(null, win, _cur, OptForeground.IsChecked == true, OptExclude.IsChecked == true, _hwnd);
                        SetStatus("status.on.window", StatusKind.Ok, win.ToString());
                        break;
                    }
            }

            if (_engine != null)
            {
                var engine = _engine;
                _lastFrames = 0;
                engine.TargetClosed += () => Dispatcher.BeginInvoke(() =>
                {
                    if (_engine != engine) return;
                    Stop();
                    SetStatus("status.windowgone", StatusKind.Error);
                });
            }
        }
        catch (Exception ex)
        {
            _engine?.Dispose();
            _engine = null;
            _fastActive = false;
            SetStatus("status.error", StatusKind.Error, ex.Message);
        }
        UpdateModeUi();
    }

    private void Stop()
    {
        var engine = _engine;
        _engine = null;
        engine?.Dispose();
        if (_fastActive) { MagnifierEffect.Reset(); _fastActive = false; }
        FpsText.Text = "";
        if (_statusKey.StartsWith("status.on")) SetStatus("status.off");
        UpdateModeUi();
    }

    private void ApplyLive()
    {
        if (_fastActive) MagnifierEffect.Apply(_cur);
        else _engine?.UpdateSettings(_cur);
    }

    private void UpdateFps()
    {
        if (_engine == null) return;
        long f = _engine.FrameCount;
        FpsText.Text = Loc.F("status.fps", f - _lastFrames);
        _lastFrames = f;
    }

    private enum StatusKind { Neutral, Ok, Error }

    private void SetStatus(string key, StatusKind kind = StatusKind.Neutral, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        StatusDot.Fill = (Brush)FindResource(kind switch { StatusKind.Error => "DangerBrush", StatusKind.Ok => "SuccessBrush", _ => "FaintTextBrush" });
        StatusText.Text = Loc.F(key, args);
    }

    // ---------------------------------------------------------------- options / language

    private void Option_Click(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        _cfg.OnlyWhenForeground = OptForeground.IsChecked == true;
        _cfg.ExcludeFromCapture = OptExclude.IsChecked == true;
        _cfg.KeepInTrayOnClose = OptTray.IsChecked == true;
        ScheduleSave();
        if (Running && (ReferenceEquals(sender, OptForeground) || ReferenceEquals(sender, OptExclude))) Start();
    }

    private void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppress || sender is not RadioButton { Tag: string lang }) return;
        _cfg.Language = lang;
        Loc.Instance.Language = lang;
        ScheduleSave();
    }

    private void OnLanguageChanged()
    {
        foreach (var r in _rows) r.Relabel();
        BuildColorBlindCombo();
        BuildPresetChips();
        UpdateModeUi();
        UpdateDimming();
        StatusText.Text = Loc.F(_statusKey, _statusArgs);
        if (_tray != null) BuildTrayMenu();
    }

    // ---------------------------------------------------------------- hotkeys

    private const int HkToggle = 1, HkNext = 2, HkPrev = 3, HkUp = 4, HkDown = 5, HkReset = 6;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        const uint mods = Native.MOD_CONTROL | Native.MOD_ALT;
        bool ok = true;
        ok &= Native.RegisterHotKey(_hwnd, HkToggle, mods | Native.MOD_NOREPEAT, 0x46);
        ok &= Native.RegisterHotKey(_hwnd, HkNext, mods | Native.MOD_NOREPEAT, 0x27);
        ok &= Native.RegisterHotKey(_hwnd, HkPrev, mods | Native.MOD_NOREPEAT, 0x25);
        ok &= Native.RegisterHotKey(_hwnd, HkUp, mods, 0x26);
        ok &= Native.RegisterHotKey(_hwnd, HkDown, mods, 0x28);
        ok &= Native.RegisterHotKey(_hwnd, HkReset, mods | Native.MOD_NOREPEAT, 0x24);
        if (!ok) HotkeyWarn.Visibility = Visibility.Visible;
        RefreshSources();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != (int)Native.WM_HOTKEY) return IntPtr.Zero;
        handled = true;
        switch (wParam.ToInt32())
        {
            case HkToggle:
                Toggle();
                _toast.ShowMessage(Loc.T(Running ? "toast.on" : "toast.off"));
                break;
            case HkNext: CyclePreset(1); break;
            case HkPrev: CyclePreset(-1); break;
            case HkUp: NudgeBrightness(0.02f); break;
            case HkDown: NudgeBrightness(-0.02f); break;
            case HkReset: SelectPresetChip(_builtIn[0]); _toast.ShowMessage(_builtIn[0].Name); break;
        }
        return IntPtr.Zero;
    }

    private void NudgeBrightness(float delta)
    {
        _cur.Brightness = Math.Clamp(_cur.Brightness + delta, -0.5f, 0.5f);
        RefreshAll();
        ClearPresetSelection();
        ApplyLive();
        ScheduleSave();
        _toast.ShowMessage($"{Loc.T("s.brightness")}: {_cur.Brightness:0.00}");
    }

    // ---------------------------------------------------------------- tray / lifetime

    private void SetupTray()
    {
        try
        {
            using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/AppIcon.ico")).Stream;
            _tray = new System.Windows.Forms.NotifyIcon { Icon = new System.Drawing.Icon(stream), Text = "Screen Filter", Visible = true };
            _tray.DoubleClick += (_, _) => ShowFromTray();
            BuildTrayMenu();
        }
        catch { _tray = null; }
    }

    private void BuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.T("tray.show"), null, (_, _) => ShowFromTray());
        menu.Items.Add(Loc.T("tray.toggle"), null, (_, _) => Dispatcher.Invoke(Toggle));
        menu.Items.Add("-");
        menu.Items.Add(Loc.T("tray.exit"), null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray!.ContextMenuStrip?.Dispose();
        _tray.ContextMenuStrip = menu;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && _cfg.KeepInTrayOnClose && _tray != null)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private void ExitApp()
    {
        _exiting = true;
        Stop();
        ConfigStore.Save(_cfg);
        for (int i = HkToggle; i <= HkReset; i++) Native.UnregisterHotKey(_hwnd, i);
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        _toast.Close();
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        OnClosedCore(e);
    }

    private void OnClosedCore(EventArgs e)
    {
        Stop();
        ConfigStore.Save(_cfg);
        base.OnClosed(e);
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void RunUiShot(string outPath)
    {
        var lang = Array.IndexOf(App.Args, "--lang");
        if (lang >= 0) Loc.Instance.Language = App.Args[lang + 1];
        await Task.Delay(600);
        var dpi = VisualTreeHelper.GetDpi(this);
        var root = (FrameworkElement)Content;
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(root);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        using (var fs = File.Create(outPath)) enc.Save(fs);
        ExitApp();
    }

    // ---------------------------------------------------------------- self test (used to verify capture + shader without a display)

    private async void RunSelfTest(string outPath)
    {
        try
        {
            var wIdx = Array.IndexOf(App.Args, "--window");
            _cfg.Mode = App.Args.Contains("--fast") ? CaptureMode.Fast : wIdx >= 0 ? CaptureMode.Window : CaptureMode.Monitor;
            SelectPresetChip(_builtIn.First(p => p.Id == "competitive"));
            if (wIdx >= 0)
            {
                var wanted = App.Args[wIdx + 1];
                var win = Sources.Windows(_hwnd).FirstOrDefault(w => w.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("window not found: " + wanted + " in " + string.Join(" | ", Sources.Windows(_hwnd).Select(w => w.Title)));
                RefreshSources();
                SourceCombo.SelectedItem = SourceCombo.Items.OfType<WindowSource>().First(w => w.Handle == win.Handle);
            }
            Start();
            if (_engine == null && !_fastActive) throw new InvalidOperationException(StatusText.Text);
            await Task.Delay(1000); long f0 = _engine?.FrameCount ?? 0; await Task.Delay(2000); double fps = ((_engine?.FrameCount ?? 0) - f0) / 2.0;
            string result = "OK";
            if (_engine != null)
            {
                var engine = _engine;
                bool ok = await Task.Run(() => engine.SaveSnapshot(outPath));
                result = ok ? $"OK fps={fps}" : "SNAPSHOT TIMEOUT";
            }
            File.WriteAllText(outPath + ".txt", result);
        }
        catch (Exception ex) { File.WriteAllText(outPath + ".txt", ex.ToString()); }
        finally { ExitApp(); }
    }
}
