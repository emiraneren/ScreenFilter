namespace ScreenFilter.Models;


public class FilterSettings
{
    public float Brightness { get; set; }
    public float Contrast { get; set; } = 1f;
    public float Gamma { get; set; } = 1f;
    public float Exposure { get; set; }
    public float ShadowLift { get; set; }
    public float Highlights { get; set; }

    public float Saturation { get; set; } = 1f;
    public float Vibrance { get; set; }
    public float Hue { get; set; }
    public float Temperature { get; set; }
    public float Tint { get; set; }
    public float Red { get; set; } = 1f;
    public float Green { get; set; } = 1f;
    public float Blue { get; set; } = 1f;
    public float Grayscale { get; set; }
    public bool Invert { get; set; }

    public float Sharpen { get; set; }
    public float Clarity { get; set; }
    public float Dehaze { get; set; }

    public float TargetAmount { get; set; }
    public float TargetHue { get; set; }
    public float TargetRange { get; set; } = 30f;

    public FilterSettings Clone() => (FilterSettings)MemberwiseClone();

    public void CopyFrom(FilterSettings o)
    {
        foreach (var p in typeof(FilterSettings).GetProperties())
            if (p.CanWrite) p.SetValue(this, p.GetValue(o));
    }
}
