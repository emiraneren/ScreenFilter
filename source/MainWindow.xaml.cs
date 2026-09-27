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
    private GameFocusWatcher? _gameFocusWatcher;
    private DispatcherTimer? _gameWaitTimer;
    private IntPtr _watchedGameHwnd;
    private bool _gameFocused;
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
        BuildFpsCombo();

        _suppress = true;
        (_cfg.Language == "tr" ? LangTr : LangEn).IsChecked = true;
        (_cfg.Mode switch { CaptureMode.Fast => ModeFast, CaptureMode.Window => ModeWindow, CaptureMode.Game => ModeGame, _ => ModeMonitor }).IsChecked = true;
        OptForeground.IsChecked = _cfg.OnlyWhenForeground;
        OptExclude.IsChecked = _cfg.ExcludeFromCapture;
        OptTray.IsChecked = _cfg.KeepInTrayOnClose;
        (_cfg.SimpleView ? ViewBasic : ViewDetailed).IsChecked = true;
        _suppress = false;

        BuildPresetChips();
        ApplyView();
        RefreshAll();
        RefreshSources();
        RefreshGamePanel();
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

        Add(LightPanel, "s.darkboost", 0f, 1f, 0f, "0.00", false, s => s.DarkBoost, (s, v) => s.DarkBoost = v);
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

        Add(BasicPanel, "s.darkboost", 0f, 1f, 0f, "0.00", false, s => s.DarkBoost, (s, v) => s.DarkBoost = v);
        Add(BasicPanel, "s.brightness", -0.5f, 0.5f, 0f, "0.00", true, s => s.Brightness, (s, v) => s.Brightness = v);
        Add(BasicPanel, "s.contrast", 0.5f, 2f, 1f, "0.00", true, s => s.Contrast, (s, v) => s.Contrast = v);
        Add(BasicPanel, "s.saturation", 0f, 2.5f, 1f, "0.00", true, s => s.Saturation, (s, v) => s.Saturation = v);
        Add(BasicPanel, "s.temperature", -1f, 1f, 0f, "0.00", true, s => s.Temperature, (s, v) => s.Temperature = v);
        Add(BasicPanel, "s.sharpen", 0f, 2f, 0f, "0.00", false, s => s.Sharpen, (s, v) => s.Sharpen = v);

        Add(DetailPanel, "s.sharpen", 0f, 2f, 0f, "0.00", false, s => s.Sharpen, (s, v) => s.Sharpen = v);
        Add(DetailPanel, "s.clarity", 0f, 1f, 0f, "0.00", false, s => s.Clarity, (s, v) => s.Clarity = v);
        Add(DetailPanel, "s.dehaze", 0f, 1f, 0f, "0.00", false, s => s.Dehaze, (s, v) => s.Dehaze = v);

        Add(FilmPanel, "s.vignette", 0f, 1f, 0f, "0.00", false, s => s.Vignette, (s, v) => s.Vignette = v);
        Add(FilmPanel, "s.grain", 0f, 1f, 0f, "0.00", false, s => s.Grain, (s, v) => s.Grain = v);

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
        foreach (var r in _rows) if (!ReferenceEquals(r, row)) r.Refresh(_cur);
        ClearPresetSelection();
        ApplyLive();
        ScheduleSave();
    }

    private static readonly int[] FpsChoices = [0, 30, 60, 75, 90, 120, 144, 165, 180, 240, 360, 1000];

    private void BuildFpsCombo()
    {
        _suppress = true;
        FpsCombo.Items.Clear();
        foreach (int v in FpsChoices)
        {
            string label = v == 0 ? Loc.T("fps.auto") : v == 1000 ? Loc.T("fps.unlimited") : $"{v} Hz";
            var item = new ComboBoxItem { Content = label, Tag = v };
            FpsCombo.Items.Add(item);
            if (v == _cfg.CaptureFps) FpsCombo.SelectedItem = item;
        }
        if (FpsCombo.SelectedItem == null) FpsCombo.SelectedIndex = 0;
        _suppress = false;
    }

    private void FpsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || FpsCombo.SelectedItem is not ComboBoxItem { Tag: int v }) return;
        _cfg.CaptureFps = v;
        _engine?.SetMaxFps(v);
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
        _suppress = false;
        UpdateDimming();
    }

    private void View_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppress || sender is not RadioButton { Tag: string tag }) return;
        _cfg.SimpleView = tag == "basic";
        ApplyView();
        ScheduleSave();
    }

    private void ApplyView()
    {
        BasicView.Visibility = _cfg.SimpleView ? Visibility.Visible : Visibility.Collapsed;
        DetailedView.Visibility = _cfg.SimpleView ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateDimming()
    {
        bool fast = _cfg.Mode is CaptureMode.Fast or CaptureMode.Game;
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

        bool fast = _cfg.Mode is CaptureMode.Fast or CaptureMode.Game;
        var missing = fast && p != null
            ? _rows.Where(r => !r.FastSupported && r.IsNonDefault(p.Settings)).Select(r => r.Label).Distinct().ToList()
            : [];
        PresetWarning.Visibility = missing.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (missing.Count > 0) PresetWarning.Text = Loc.F("preset.fastwarn", string.Join(", ", missing));
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
        RefreshGamePanel();
        UpdateModeUi();
        UpdateDimming();
        ScheduleSave();
        if (Running) Start();
    }

    private void UpdateModeUi()
    {
        ModeDesc.Text = Loc.T(_cfg.Mode switch { CaptureMode.Fast => "mode.fast.desc", CaptureMode.Window => "mode.window.desc", CaptureMode.Game => "mode.game.desc", _ => "mode.monitor.desc" });
        SourcePanel.Visibility = _cfg.Mode is CaptureMode.Fast or CaptureMode.Game ? Visibility.Collapsed : Visibility.Visible;
        GamePanel.Visibility = _cfg.Mode == CaptureMode.Game ? Visibility.Visible : Visibility.Collapsed;
        SourceLabel.Text = Loc.T(_cfg.Mode == CaptureMode.Window ? "source.window" : "source.monitor").ToUpperInvariant();
        OptForeground.Visibility = _cfg.Mode == CaptureMode.Window ? Visibility.Visible : Visibility.Collapsed;
        OptExclude.Visibility = _cfg.Mode == CaptureMode.Window ? Visibility.Visible : Visibility.Collapsed;
        StartBtn.Content = Loc.T(Running ? "btn.stop" : "btn.start");
        StartBtn.Style = (Style)FindResource(Running ? "DangerButton" : "PrimaryButton");
    }

    private void RefreshSources()
    {
        if (_cfg.Mode is CaptureMode.Fast or CaptureMode.Game) return;
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

    private void RefreshGamePanel()
    {
        if (_cfg.Mode != CaptureMode.Game) return;
        bool hasSaved = !string.IsNullOrEmpty(_cfg.GameProcessName);
        GamePickCombo.Visibility = hasSaved ? Visibility.Collapsed : Visibility.Visible;
        GameChangeBtn.Visibility = hasSaved ? Visibility.Visible : Visibility.Collapsed;

        if (!hasSaved)
        {
            GameStatusText.Text = Loc.T("game.pick") + " — " + Loc.T("game.pick.hint");
            GamePickCombo.Items.Clear();
            foreach (var w in Sources.Windows(_hwnd)) GamePickCombo.Items.Add(w);
            return;
        }

        var found = Sources.FindGameWindow(_cfg.GameProcessName!);
        GameStatusText.Text = found != null ? Loc.F("game.detected", found.Title) : Loc.T("game.notrunning");
    }

    private void GamePickCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || GamePickCombo.SelectedItem is not WindowSource win) return;
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)win.ProcessId);
            _cfg.GameProcessName = p.ProcessName;
        }
        catch { return; }
        _cfg.GameWindowTitle = win.Title;
        ScheduleSave();
        RefreshGamePanel();
        if (Running) Start();
    }

    private void GameChange_Click(object sender, RoutedEventArgs e)
    {
        _cfg.GameProcessName = null;
        _cfg.GameWindowTitle = null;
        ScheduleSave();
        if (Running) Stop();
        RefreshGamePanel();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshSources();
        RefreshGamePanel();
    }

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

                case CaptureMode.Game:
                    if (string.IsNullOrEmpty(_cfg.GameProcessName)) { SetStatus("game.pick", StatusKind.Error); return; }
                    _fastActive = true;
                    StartGameWatch();
                    break;

                case CaptureMode.Monitor:
                    {
                        var mon = SourceCombo.SelectedItem as MonitorSource ?? Sources.Monitors().First();
                        _engine = new CaptureEngine(mon, null, _cur, false, true, _hwnd, _cfg.CaptureFps);
                        SetStatus("status.on.monitor", StatusKind.Ok, mon.ToString());
                        break;
                    }

                case CaptureMode.Window:
                    {
                        if (SourceCombo.SelectedItem is not WindowSource win) { SetStatus("status.nowindow", StatusKind.Error); return; }
                        _engine = new CaptureEngine(null, win, _cur, OptForeground.IsChecked == true, OptExclude.IsChecked == true, _hwnd, _cfg.CaptureFps);
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
                engine.Failed += msg => Dispatcher.BeginInvoke(() =>
                {
                    if (_engine != engine) return;
                    Stop();
                    SetStatus("status.error", StatusKind.Error, msg);
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
        StopGameWatch();
        if (_fastActive) { MagnifierEffect.Reset(); _fastActive = false; }
        FpsText.Text = "";
        if (_statusKey.StartsWith("status.on") || _statusKey.StartsWith("game.")) SetStatus("status.off");
        UpdateModeUi();
    }

    private void ApplyLive()
    {
        if (_fastActive)
        {
            if (_cfg.Mode != CaptureMode.Game || _gameFocused) MagnifierEffect.Apply(_cur);
        }
        else _engine?.UpdateSettings(_cur);
    }

    /// <summary>
    /// Polls for the remembered game every 2s (cheap: just IsWindow once attached) so "Oyun" mode can
    /// start before the game is even open, and re-detect it if it's closed and relaunched.
    /// </summary>
    private void StartGameWatch()
    {
        StopGameWatch();
        _gameWaitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _gameWaitTimer.Tick += (_, _) => PollGameWindow();
        PollGameWindow();
        _gameWaitTimer.Start();
    }

    private void StopGameWatch()
    {
        _gameWaitTimer?.Stop();
        _gameWaitTimer = null;
        _gameFocusWatcher?.Dispose();
        _gameFocusWatcher = null;
        _watchedGameHwnd = IntPtr.Zero;
        _gameFocused = false;
    }

    private void PollGameWindow()
    {
        if (_watchedGameHwnd != IntPtr.Zero && Native.IsWindow(_watchedGameHwnd)) return;

        _gameFocusWatcher?.Dispose();
        _gameFocusWatcher = null;
        _watchedGameHwnd = IntPtr.Zero;
        MagnifierEffect.Reset();

        var found = Sources.FindGameWindow(_cfg.GameProcessName!);
        if (found == null) { SetStatus("game.notrunning", StatusKind.Neutral); return; }

        _watchedGameHwnd = found.Handle;
        // Applied directly on the watcher's own thread, not marshaled through the WPF Dispatcher —
        // Dispatcher.Invoke would queue behind whatever the UI thread happens to be doing at that
        // instant (layout, a pending timer tick, ...), and that queueing is exactly what made
        // switching into the game feel like it lagged a beat before the filter caught up.
        _gameFocusWatcher = new GameFocusWatcher(found.Handle, focused =>
        {
            _gameFocused = focused;
            if (!_fastActive) return;
            if (focused) MagnifierEffect.Apply(_cur); else MagnifierEffect.Reset();
        });
        SetStatus("game.detected", StatusKind.Ok, found.Title);
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
        BuildFpsCombo();
        if (_hwnd != IntPtr.Zero) BuildHotkeyPanel();
        BuildPresetChips();
        UpdateModeUi();
        RefreshGamePanel();
        UpdateDimming();
        StatusText.Text = Loc.F(_statusKey, _statusArgs);
        if (_tray != null) BuildTrayMenu();
    }

    // ---------------------------------------------------------------- hotkeys

    private static readonly string[] HkActions = ["toggle", "next", "prev", "up", "down", "reset"];
    private const int HkToggle = 1, HkNext = 2, HkPrev = 3, HkUp = 4, HkDown = 5, HkReset = 6;
    private readonly Dictionary<string, Button> _hkButtons = [];
    private string? _capturing;

    private HotkeyBinding Binding(string action) =>
        _cfg.Hotkeys.TryGetValue(action, out var b) ? b : HotkeyBinding.Defaults[action];

    private bool RegisterOne(string action, HotkeyBinding b)
    {
        int id = Array.IndexOf(HkActions, action) + 1;
        Native.UnregisterHotKey(_hwnd, id);
        uint mods = b.Mods | (action is "up" or "down" ? 0u : Native.MOD_NOREPEAT);
        return Native.RegisterHotKey(_hwnd, id, mods, b.Vk);
    }

    private void RegisterAll()
    {
        bool ok = true;
        foreach (var a in HkActions) ok &= RegisterOne(a, Binding(a));
        SetHotkeyMessage(ok ? null : "hotkeys.fail");
    }

    private void SetHotkeyMessage(string? key)
    {
        HotkeyWarn.Text = key == null ? "" : Loc.T(key);
        HotkeyWarn.Visibility = key == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BuildHotkeyPanel()
    {
        HotkeyPanel.Children.Clear();
        _hkButtons.Clear();
        foreach (var action in HkActions)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = Loc.T("hk." + action), Style = (Style)FindResource("Muted"), VerticalAlignment = VerticalAlignment.Center });
            var btn = new Button { Content = Binding(action).ToString(), Style = (Style)FindResource("GhostButton"), MinWidth = 120, Padding = new Thickness(0), Tag = action };
            btn.Click += (_, _) => StartCapture(action);
            btn.LostKeyboardFocus += (_, _) => { if (_capturing == action) EndCapture(); };
            Grid.SetColumn(btn, 1);
            row.Children.Add(btn);
            _hkButtons[action] = btn;
            HotkeyPanel.Children.Add(row);
        }
        var reset = new Button { Content = Loc.T("hk.default"), Style = (Style)FindResource("GhostButton"), Margin = new Thickness(0, 6, 0, 0) };
        reset.Click += (_, _) =>
        {
            _cfg.Hotkeys.Clear();
            ScheduleSave();
            RegisterAll();
            BuildHotkeyPanel();
        };
        HotkeyPanel.Children.Add(reset);
        HotkeyPanel.Children.Add(new TextBlock { Text = Loc.T("hk.hint"), Style = (Style)FindResource("Muted"), Foreground = (Brush)FindResource("FaintTextBrush"), Margin = new Thickness(0, 8, 0, 0) });
    }

    private void StartCapture(string action)
    {
        EndCapture();
        _capturing = action;
        _hkButtons[action].Content = Loc.T("hk.press");
        SetHotkeyMessage(null);
    }

    private void EndCapture()
    {
        if (_capturing == null) return;
        var action = _capturing;
        _capturing = null;
        if (_hkButtons.TryGetValue(action, out var b)) b.Content = Binding(action).ToString();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (_capturing == null) { base.OnPreviewKeyDown(e); return; }
        e.Handled = true;
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (key == System.Windows.Input.Key.Escape) { EndCapture(); return; }
        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftAlt
            or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin or System.Windows.Input.Key.None) return;

        var m = System.Windows.Input.Keyboard.Modifiers;
        uint mods = 0;
        if (m.HasFlag(System.Windows.Input.ModifierKeys.Control)) mods |= HotkeyBinding.Ctrl;
        if (m.HasFlag(System.Windows.Input.ModifierKeys.Alt)) mods |= HotkeyBinding.Alt;
        if (m.HasFlag(System.Windows.Input.ModifierKeys.Shift)) mods |= HotkeyBinding.Shift;
        if (m.HasFlag(System.Windows.Input.ModifierKeys.Windows)) mods |= HotkeyBinding.Win;
        if (mods == 0) { SetHotkeyMessage("hk.needmod"); return; }

        var action = _capturing;
        var nb = new HotkeyBinding { Mods = mods, Vk = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key) };
        if (HkActions.Any(a => a != action && Binding(a).SameAs(nb))) { SetHotkeyMessage("hk.dup"); return; }

        var old = Binding(action);
        if (!RegisterOne(action, nb)) { RegisterOne(action, old); SetHotkeyMessage("hk.taken"); return; }
        _cfg.Hotkeys[action] = nb;
        ScheduleSave();
        SetHotkeyMessage(null);
        EndCapture();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        BuildHotkeyPanel();
        RegisterAll();
        RefreshSources();
        RefreshGamePanel();
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
        _toast.Close();
        Application.Current.Shutdown();
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
            var gIdx = Array.IndexOf(App.Args, "--game");
            _cfg.Mode = App.Args.Contains("--fast") ? CaptureMode.Fast : gIdx >= 0 ? CaptureMode.Game : wIdx >= 0 ? CaptureMode.Window : CaptureMode.Monitor;
            var pIdx = Array.IndexOf(App.Args, "--preset");
            SelectPresetChip(_builtIn.First(p => p.Id == (pIdx >= 0 ? App.Args[pIdx + 1] : "competitive")));
            if (wIdx >= 0)
            {
                var wanted = App.Args[wIdx + 1];
                var win = Sources.Windows(_hwnd).FirstOrDefault(w => w.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("window not found: " + wanted + " in " + string.Join(" | ", Sources.Windows(_hwnd).Select(w => w.Title)));
                RefreshSources();
                SourceCombo.SelectedItem = SourceCombo.Items.OfType<WindowSource>().First(w => w.Handle == win.Handle);
            }
            if (gIdx >= 0) _cfg.GameProcessName = App.Args[gIdx + 1];
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
            var holdIdx = Array.IndexOf(App.Args, "--holdms");
            if (holdIdx >= 0)
            {
                await Task.Delay(int.Parse(App.Args[holdIdx + 1]));
                long f1 = _engine?.FrameCount ?? 0; await Task.Delay(1000); double fps2 = ((_engine?.FrameCount ?? 0) - f1);
                result += $" afterHold_fps={fps2} running={Running} status='{StatusText.Text}'";
                if (_engine != null) await Task.Run(() => _engine.SaveSnapshot(outPath + "_end.png"));
            }

            var toggleIdx = Array.IndexOf(App.Args, "--togglecount");
            if (toggleIdx >= 0)
            {
                int n = int.Parse(App.Args[toggleIdx + 1]);
                for (int i = 0; i < n; i++)
                {
                    await Task.Delay(400);
                    Stop();
                    if (Running) { result += $" TOGGLE-FAIL-STILLRUNNING@{i}"; break; }
                    await Task.Delay(400);
                    Start();
                    if (!Running) { result += $" TOGGLE-FAIL-NOTSTARTED@{i} status='{StatusText.Text}'"; break; }
                }
                result += $" afterToggles running={Running} frames={_engine?.FrameCount ?? -1} status='{StatusText.Text}'";
                await Task.Delay(2000);
                result += $" +2s frames={_engine?.FrameCount ?? -1}";
            }
            File.WriteAllText(outPath + ".txt", result);
        }
        catch (Exception ex) { File.WriteAllText(outPath + ".txt", ex.ToString()); }
        finally { ExitApp(); }
    }
}
