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
