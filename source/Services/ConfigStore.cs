using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenFilter.Models;

namespace ScreenFilter.Services;

public enum CaptureMode { Fast, Monitor, Window }

public class AppConfig
{
    public string Language { get; set; } = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en";
    public CaptureMode Mode { get; set; } = CaptureMode.Monitor;
    public int CaptureFps { get; set; }
    public Dictionary<string, HotkeyBinding> Hotkeys { get; set; } = [];
    public string? LastPresetId { get; set; } = "default";
    public bool OnlyWhenForeground { get; set; } = true;
    public bool ExcludeFromCapture { get; set; } = true;
    public bool KeepInTrayOnClose { get; set; } = false;
    public FilterSettings Current { get; set; } = new();
    public List<Preset> CustomPresets { get; set; } = [];
}

public static class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string PathFile => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenFilter", "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(PathFile))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(PathFile), Options) ?? new AppConfig();
        }
        catch { /* corrupt config: start fresh */ }
        return new AppConfig();
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathFile)!);
            File.WriteAllText(PathFile, JsonSerializer.Serialize(cfg, Options));
        }
        catch { /* non-fatal */ }
    }
}

public class HotkeyBinding
{
    public const uint Alt = 1, Ctrl = 2, Shift = 4, Win = 8;

    public uint Mods { get; set; }
    public uint Vk { get; set; }

    public static readonly Dictionary<string, HotkeyBinding> Defaults = new()
    {
        ["toggle"] = new() { Mods = Ctrl | Alt, Vk = 0x46 },
        ["next"] = new() { Mods = Ctrl | Alt, Vk = 0x27 },
        ["prev"] = new() { Mods = Ctrl | Alt, Vk = 0x25 },
        ["up"] = new() { Mods = Ctrl | Alt, Vk = 0x26 },
        ["down"] = new() { Mods = Ctrl | Alt, Vk = 0x28 },
        ["reset"] = new() { Mods = Ctrl | Alt, Vk = 0x24 },
    };

    public bool SameAs(HotkeyBinding o) => Mods == o.Mods && Vk == o.Vk;

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Mods & Ctrl) != 0) parts.Add("Ctrl");
        if ((Mods & Alt) != 0) parts.Add("Alt");
        if ((Mods & Shift) != 0) parts.Add("Shift");
        if ((Mods & Win) != 0) parts.Add("Win");
        var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)Vk);
        parts.Add(key switch
        {
            System.Windows.Input.Key.Right => "→",
            System.Windows.Input.Key.Left => "←",
            System.Windows.Input.Key.Up => "↑",
            System.Windows.Input.Key.Down => "↓",
            >= System.Windows.Input.Key.D0 and <= System.Windows.Input.Key.D9 => ((int)key - (int)System.Windows.Input.Key.D0).ToString(),
            _ => key.ToString(),
        });
        return string.Join("+", parts);
    }
}
