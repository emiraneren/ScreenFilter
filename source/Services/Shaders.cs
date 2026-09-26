namespace ScreenFilter.Services;

internal static class Shaders
{
    public const string Source = """
Texture2D Src : register(t0);
SamplerState Smp : register(s0);

cbuffer P : register(b0)
{
    float4 R0;   // matrix row 0 (xyz) + offset (w)
    float4 R1;
    float4 R2;
    float4 P0;   // gamma, shadowLift, highlights, vibrance
    float4 P1;   // sharpen, clarity, dehaze, targetAmount
    float4 P2;   // targetHue (0..1), targetRange (0..0.5), texel.x, texel.y
    float4 P3;   // darkBoost, -, -, -
};

struct VSOut { float4 pos : SV_Position; float2 uv : TEXCOORD0; };

VSOut VS(uint id : SV_VertexID)
{
    VSOut o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.uv = uv;
    o.pos = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}

static const float3 LUM = float3(0.2126, 0.7152, 0.0722);

float3 RgbToHsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 HsvToRgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

float4 PS(VSOut i) : SV_Target
{
    float2 t = P2.zw;
    float3 c = Src.Sample(Smp, i.uv).rgb;

    if (P1.x > 0.001 || P1.y > 0.001)
    {
        float3 n = Src.Sample(Smp, i.uv + float2(t.x, 0)).rgb
                 + Src.Sample(Smp, i.uv - float2(t.x, 0)).rgb
                 + Src.Sample(Smp, i.uv + float2(0, t.y)).rgb
                 + Src.Sample(Smp, i.uv - float2(0, t.y)).rgb;
        c += P1.x * 1.5 * (c - n * 0.25);

        if (P1.y > 0.001)
        {
            float2 d = t * 3.0;
            float3 w = Src.Sample(Smp, i.uv + float2(d.x, 0)).rgb
                     + Src.Sample(Smp, i.uv - float2(d.x, 0)).rgb
                     + Src.Sample(Smp, i.uv + float2(0, d.y)).rgb
                     + Src.Sample(Smp, i.uv - float2(0, d.y)).rgb
                     + Src.Sample(Smp, i.uv + d).rgb
                     + Src.Sample(Smp, i.uv - d).rgb
                     + Src.Sample(Smp, i.uv + float2(d.x, -d.y)).rgb
                     + Src.Sample(Smp, i.uv + float2(-d.x, d.y)).rgb;
            float l = dot(c, LUM);
            float mid = 1.0 - abs(2.0 * l - 1.0);
            c += P1.y * 1.6 * mid * (c - w * 0.125);
        }
        c = saturate(c);
    }

    float mn = min(c.r, min(c.g, c.b));
    c = (c - P1.z * mn) / max(1.0 - P1.z * mn, 0.05);

    float3 o = float3(dot(R0.xyz, c) + R0.w, dot(R1.xyz, c) + R1.w, dot(R2.xyz, c) + R2.w);
    o = saturate(o);

    if (P3.x > 0.001)
    {
        float lum = max(dot(o, LUM), 0.0001);
        float lifted = pow(lum, 1.0 / (1.0 + P3.x * 2.4));
        o = saturate(o * min(lifted / lum, 9.0));
        o += P3.x * 0.05 * pow(saturate(1.0 - lifted), 4.0);
    }

    float l2 = dot(o, LUM);
    float shadow = P0.y * pow(saturate(1.0 - l2), 3.0);
    o += shadow * (0.25 + o * 0.75);
    o *= 1.0 - 0.5 * P0.z * l2 * l2 * l2;
    o = saturate(o);
    o = pow(o, 1.0 / max(P0.x, 0.05));

    if (abs(P0.w) > 0.001)
    {
        float g = dot(o, LUM);
        float sat = max(o.r, max(o.g, o.b)) - min(o.r, min(o.g, o.b));
        o = saturate(lerp(float3(g, g, g), o, 1.0 + P0.w * (1.0 - sat)));
    }

    if (P1.w > 0.001)
    {
        float3 hsv = RgbToHsv(o);
        float dh = abs(hsv.x - P2.x);
        dh = min(dh, 1.0 - dh);
        float w = smoothstep(0.0, 1.0, saturate(1.0 - dh / max(P2.y, 0.001))) * saturate(hsv.y * 3.0);
        float a = P1.w;
        hsv.y = saturate(hsv.y * (1.0 + w * a * 0.9) * (1.0 - 0.45 * a * (1.0 - w)));
        hsv.z = saturate(hsv.z * (1.0 + w * a * 0.45) * (1.0 - 0.18 * a * (1.0 - w)));
        o = HsvToRgb(hsv);
    }

    return float4(saturate(o), 1.0);
}
""";
}
