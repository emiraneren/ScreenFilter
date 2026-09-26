using ScreenFilter.Models;

namespace ScreenFilter.Services;

public static class BuiltInPresets
{
    public static List<Preset> Create() =>
    [
        P("default", new FilterSettings()),

        P("competitive", new FilterSettings
        {
            ShadowLift = 0.35f, Contrast = 1.08f, Saturation = 1.25f, Vibrance = 0.25f,
            Sharpen = 0.5f, Gamma = 1.08f, Highlights = 0.15f,
        }),

        P("darkreveal", new FilterSettings
        {
            ShadowLift = 0.85f, Gamma = 1.25f, Contrast = 1.05f, Brightness = 0.03f,
            Saturation = 1.1f, Sharpen = 0.3f, Highlights = 0.25f,
        }),

        P("enemyred", new FilterSettings
        {
            TargetAmount = 0.85f, TargetHue = 5f, TargetRange = 32f,
            Contrast = 1.1f, ShadowLift = 0.2f, Sharpen = 0.4f, Saturation = 1.1f,
        }),

        P("enemypurple", new FilterSettings
        {
            TargetAmount = 0.85f, TargetHue = 295f, TargetRange = 38f,
            Contrast = 1.1f, ShadowLift = 0.2f, Sharpen = 0.4f, Saturation = 1.1f,
        }),

        P("vibrant", new FilterSettings
        {
            Saturation = 1.35f, Vibrance = 0.4f, Contrast = 1.1f, Sharpen = 0.25f,
        }),

        P("sharp", new FilterSettings
        {
            Sharpen = 1.3f, Clarity = 0.55f, Contrast = 1.06f, Saturation = 1.08f,
        }),

        P("fog", new FilterSettings
        {
            Dehaze = 0.6f, Contrast = 1.18f, Saturation = 1.2f, Clarity = 0.4f,
            ShadowLift = 0.15f, Sharpen = 0.35f, Gamma = 0.96f,
        }),

        P("nightvision", new FilterSettings
        {
            Green = 1.25f, Red = 0.75f, Blue = 0.7f, Gamma = 1.6f, ShadowLift = 0.9f,
            Contrast = 1.15f, Sharpen = 0.4f, Saturation = 0.8f,
        }),

        P("cinematic", new FilterSettings
        {
            Temperature = 0.25f, Tint = -0.05f, Saturation = 0.85f, Contrast = 1.18f,
            Highlights = 0.2f, Vibrance = 0.15f, Clarity = 0.2f,
        }),

        P("bw", new FilterSettings
        {
            Grayscale = 1f, Contrast = 1.35f, ShadowLift = 0.3f, Sharpen = 0.6f, Gamma = 1.05f,
        }),

        P("nightmode", new FilterSettings
        {
            Temperature = 0.55f, Brightness = -0.06f, Blue = 0.78f, Contrast = 0.95f, Saturation = 0.9f,
        }),

        P("protan", new FilterSettings { ColorBlind = ColorBlindMode.Protanopia, Contrast = 1.05f, Saturation = 1.1f }),
        P("deutan", new FilterSettings { ColorBlind = ColorBlindMode.Deuteranopia, Contrast = 1.05f, Saturation = 1.1f }),
        P("tritan", new FilterSettings { ColorBlind = ColorBlindMode.Tritanopia, Contrast = 1.05f, Saturation = 1.1f }),
    ];

    private static Preset P(string id, FilterSettings s) => new() { Id = id, Settings = s };
}
