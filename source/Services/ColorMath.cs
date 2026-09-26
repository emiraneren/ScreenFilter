using ScreenFilter.Models;

namespace ScreenFilter.Services;

/// <summary>Affine RGB transform: out = A * in + b (column vectors, 0..1 range).</summary>
public readonly struct Affine
{
    public readonly float[] A; // 9, row-major
    public readonly float[] B; // 3

    public Affine(float[] a, float[] b) { A = a; B = b; }

    public static Affine Identity => new([1, 0, 0, 0, 1, 0, 0, 0, 1], [0, 0, 0]);

    public static Affine Diagonal(float r, float g, float b) => new([r, 0, 0, 0, g, 0, 0, 0, b], [0, 0, 0]);

    /// <summary>Applies this transform first, then <paramref name="next"/>.</summary>
    public Affine Then(Affine next)
    {
        var a = new float[9];
        var b = new float[3];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
                a[r * 3 + c] = next.A[r * 3] * A[c] + next.A[r * 3 + 1] * A[3 + c] + next.A[r * 3 + 2] * A[6 + c];
            b[r] = next.A[r * 3] * B[0] + next.A[r * 3 + 1] * B[1] + next.A[r * 3 + 2] * B[2] + next.B[r];
        }
        return new Affine(a, b);
    }
}

public static class ColorMath
{
    private const float Lr = 0.2126f, Lg = 0.7152f, Lb = 0.0722f;

    public static Affine Build(FilterSettings s)
    {
        var m = Affine.Identity;

        float exp = MathF.Pow(2f, s.Exposure);
        m = m.Then(Affine.Diagonal(exp * s.Red, exp * s.Green, exp * s.Blue));

        float t = s.Temperature, ti = s.Tint;
        m = m.Then(Affine.Diagonal(1f + 0.25f * t + 0.08f * ti, 1f - 0.22f * ti, 1f - 0.25f * t + 0.08f * ti));

        float c = s.Contrast;
        float off = 0.5f * (1f - c) + s.Brightness;
        m = m.Then(new Affine([c, 0, 0, 0, c, 0, 0, 0, c], [off, off, off]));

        m = m.Then(Saturation(s.Saturation));
        if (s.Grayscale > 0f) m = m.Then(Saturation(1f - s.Grayscale));
        if (MathF.Abs(s.Hue) > 0.01f) m = m.Then(HueRotate(s.Hue));
        if (s.ColorBlind != ColorBlindMode.None) m = m.Then(Daltonize(s.ColorBlind));
        if (s.Invert) m = m.Then(new Affine([-1, 0, 0, 0, -1, 0, 0, 0, -1], [1, 1, 1]));
        return m;
    }

    private static Affine Saturation(float s)
    {
        float i = 1f - s;
        return new Affine(
            [i * Lr + s, i * Lg, i * Lb,
             i * Lr, i * Lg + s, i * Lb,
             i * Lr, i * Lg, i * Lb + s],
            [0, 0, 0]);
    }

    private static Affine HueRotate(float deg)
    {
        float a = deg * MathF.PI / 180f, cos = MathF.Cos(a), sin = MathF.Sin(a);
        return new Affine(
            [Lr + cos * (1 - Lr) - sin * Lr, Lg - cos * Lg - sin * Lg, Lb - cos * Lb + sin * (1 - Lb),
             Lr - cos * Lr + sin * 0.143f, Lg + cos * (1 - Lg) + sin * 0.140f, Lb - cos * Lb - sin * 0.283f,
             Lr - cos * Lr - sin * (1 - Lr), Lg - cos * Lg + sin * Lg, Lb + cos * (1 - Lb) + sin * Lb],
            [0, 0, 0]);
    }

    /// <summary>Color-vision assist: shifts the information a deficient eye cannot see into channels it can.</summary>
    private static Affine Daltonize(ColorBlindMode mode)
    {
        float[] sim = mode switch
        {
            ColorBlindMode.Protanopia => [0.567f, 0.433f, 0f, 0.558f, 0.442f, 0f, 0f, 0.242f, 0.758f],
            ColorBlindMode.Deuteranopia => [0.625f, 0.375f, 0f, 0.7f, 0.3f, 0f, 0f, 0.3f, 0.7f],
            _ => [0.95f, 0.05f, 0f, 0f, 0.433f, 0.567f, 0f, 0.475f, 0.525f],
        };
        float[] shift = mode == ColorBlindMode.Tritanopia
            ? [1, 0, 0.7f, 0, 1, 0.7f, 0, 0, 0]
            : [0, 0, 0, 0.7f, 1, 0, 0.7f, 0, 1];

        // out = c + shift * (c - sim * c) = (I + shift * (I - sim)) * c
        var d = new float[9];
        for (int i = 0; i < 9; i++) d[i] = (i % 4 == 0 ? 1f : 0f) - sim[i];
        var a = new float[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                float v = shift[r * 3] * d[c] + shift[r * 3 + 1] * d[3 + c] + shift[r * 3 + 2] * d[6 + c];
                a[r * 3 + c] = (r == c ? 1f : 0f) + v;
            }
        return new Affine(a, [0, 0, 0]);
    }
}
