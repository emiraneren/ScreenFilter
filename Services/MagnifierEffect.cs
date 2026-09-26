using ScreenFilter.Models;

namespace ScreenFilter.Services;

/// <summary>Fast mode: sets a system-wide color matrix through the Magnification API (no capture, no added latency).</summary>
public static class MagnifierEffect
{
    private static bool _initialized;

    public static bool Apply(FilterSettings s)
    {
        if (!_initialized)
        {
            if (!Native.MagInitialize()) return false;
            _initialized = true;
        }

        var m = ColorMath.Build(s);
        var t = new float[25];
        // Row-vector convention: [r g b a 1] * M
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++) t[c * 5 + r] = m.A[r * 3 + c];
            t[4 * 5 + r] = m.B[r];
        }
        t[3 * 5 + 3] = 1f;
        t[4 * 5 + 4] = 1f;

        var effect = new Native.MAGCOLOREFFECT { transform = t };
        return Native.MagSetFullscreenColorEffect(ref effect);
    }

    public static void Reset()
    {
        if (!_initialized) return;
        var id = new float[25];
        for (int i = 0; i < 5; i++) id[i * 5 + i] = 1f;
        var effect = new Native.MAGCOLOREFFECT { transform = id };
        Native.MagSetFullscreenColorEffect(ref effect);
        Native.MagUninitialize();
        _initialized = false;
    }
}
