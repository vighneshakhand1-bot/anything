using System;
using System.Numerics;
using ImGuiNET;

namespace Fluensia.Core
{
    // ---------------------------------------------------------------------------------------------
    //  Palette — every value sampled off the reference frames (see analysis/brief.md, tag [OBS]).
    // ---------------------------------------------------------------------------------------------
    public static class Palette
    {
        // window / chrome
        public static Vector4 WallTop    = Hex("#6E858C");   // wallpaper gradient top
        public static Vector4 WallBottom = Hex("#A8BAB8");   // wallpaper gradient bottom (cream haze)
        public static Vector4 Win        = Hex("#7E939A");   // window body glass @ 1.0
        public static Vector4 WinDeep    = Hex("#54686C");   // sidebar column
        public static Vector4 WinBorder  = Hex("#434F52");   // outer hairline

        // text
        public static Vector4 Text       = Hex("#F3F5F4");
        public static Vector4 TextDim    = Hex("#C3CDCB");
        public static Vector4 TextMute   = Hex("#9FAEB0");

        // accents
        public static Vector4 Yellow     = Hex("#F5D451");
        public static Vector4 YellowDeep = Hex("#E9C244");
        public static Vector4 YellowInk  = Hex("#2A2415");
        public static Vector4 Purple     = Hex("#8A6DE0");
        public static Vector4 PurpleDim  = Hex("#5F4A9E");

        // cards
        public static Vector4 CardFoot   = Hex("#131316");
        public static Vector4 CardFoot2  = Hex("#0C0C0E");
        public static Vector4 ChipDark   = Hex("#0A0A0B");
        public static Vector4 Pill       = Hex("#232326");
        public static Vector4 PillHover  = Hex("#33333A");

        // detail panel (olive-gradient glass, sampled #8E845D -> #2F221A overlay over glass)
        public static Vector4 PanelHdr   = Hex("#4E5642");
        public static Vector4 PanelBody  = Hex("#39402F");
        public static Vector4 PanelOlive = Hex("#847A4E");

        // dark settings theme
        public static Vector4 DarkBg     = Hex("#0B0B0C");
        public static Vector4 DarkCard   = Hex("#141416");
        public static Vector4 DarkCard2  = Hex("#1A1A1D");
        public static Vector4 DarkLine   = Hex("#26262A");

        // studio screen (dead-UI page shown during in-game capture)
        public static Vector4 StudioBg   = Hex("#121416");
        public static Vector4 StudioCard = Hex("#17181B");
        public static Vector4 StudioLine = Hex("#24262A");

        /// <summary>#RRGGBB or #RRGGBBAA -> linear-ish Vector4 (we keep it gamma, ImGui blends in sRGB).</summary>
        public static Vector4 Hex(string hex)
        {
            hex = hex.TrimStart('#');
            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);
            int a = hex.Length >= 8 ? Convert.ToInt32(hex.Substring(6, 2), 16) : 255;
            return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        public static Vector4 Mul(Vector4 c, float f) => new Vector4(c.X * f, c.Y * f, c.Z * f, c.W * f);
        public static Vector4 A(Vector4 c, float a) => new Vector4(c.X, c.Y, c.Z, a);
        public static Vector4 Mix(Vector4 a, Vector4 b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Vector4(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);
        }
        public static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);
        /// <summary>premultiply-safe helper: color with an extra alpha multiply</summary>
        public static uint U32(Vector4 c, float alpha) => ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, c.W * alpha));
    }

    // ---------------------------------------------------------------------------------------------
    //  Easing — named curves, durations measured off the 30 fps source (see brief.md).
    // ---------------------------------------------------------------------------------------------
    public static class Ease
    {
        public static float Linear(float t) => Math.Clamp(t, 0f, 1f);
        public static float OutCubic(float t) { t = Math.Clamp(t, 0f, 1f); float u = 1 - t; return 1 - u * u * u; }
        public static float InCubic(float t) { t = Math.Clamp(t, 0f, 1f); return t * t * t; }
        public static float InOutCubic(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
        }
        public static float OutQuint(float t) { t = Math.Clamp(t, 0f, 1f); float u = 1 - t; return 1 - u * u * u * u * u; }
        public static float OutExpo(float t) { t = Math.Clamp(t, 0f, 1f); return t >= 1f ? 1f : 1 - MathF.Pow(2, -10 * t); }
        public static float InOutQuint(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t < 0.5f ? 16 * t * t * t * t * t : 1 - MathF.Pow(-2 * t + 2, 5) / 2;
        }
        /// <summary>ease-out-back, s = 1.70158 (small overshoot on card pop-in)</summary>
        public static float OutBack(float t, float s = 1.35f)
        {
            t = Math.Clamp(t, 0f, 1f);
            float c3 = s + 1f, u = t - 1f;
            return 1f + c3 * u * u * u + s * u * u;
        }
        /// <summary>spring-ish settle used by the ImGui backend for in-flight panel moves</summary>
        public static float SpringLerp(float current, float target, float stiffness, float damping, float dt)
        {
            // critically-damped-ish approach; dt clamped so alt-tab spikes don't explode
            dt = Math.Clamp(dt, 0f, 0.1f);
            float delta = target - current;
            float omega = stiffness;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            return current + delta * (1f - exp) * Math.Min(1f, damping);
        }
        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, Math.Clamp(t, 0f, 1f));
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => Palette.Mix(a, b, t);
    }
}
