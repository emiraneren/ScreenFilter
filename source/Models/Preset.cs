using System.Text.Json.Serialization;
using ScreenFilter.Services;

namespace ScreenFilter.Models;

public class Preset
{
    public string Id { get; set; } = "";
    public string? CustomName { get; set; }
    public FilterSettings Settings { get; set; } = new();

    [JsonIgnore] public bool IsBuiltIn => CustomName == null;
    [JsonIgnore] public string Name => CustomName ?? Loc.T("preset." + Id);
    [JsonIgnore] public string Description => CustomName == null ? Loc.T("preset." + Id + ".desc") : "";
}
