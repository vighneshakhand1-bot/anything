using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ImGuiNET;

namespace Fluensia.Core
{
    /// <summary>One rasterised Inter weight at one pixel size, with a cached text measure.</summary>
    public sealed class Font
    {
        public ImFontPtr Ptr;
        public float Size;
        private readonly Dictionary<string, Vector2> _cache = new();

        public Vector2 Measure(string s)
        {
            if (string.IsNullOrEmpty(s)) return Vector2.Zero;
            if (_cache.TryGetValue(s, out var v)) return v;
            if (Ui.Rec != null) { var g = Ui.Rec.Glyph.Measure(s, Size); _cache[s] = g; return g; }
            var size = Ptr.CalcTextSizeA(Size, float.MaxValue, -1f, s);
            _cache[s] = size;
            return size;
        }

        public float Width(string s) => Measure(s).X;

        // UTF-16 glyph indices per string (AddText requires a codepoint run, not a UTF-8 string)
        private readonly Dictionary<string, ushort[]> _glyphs = new();

        public ushort[] Run(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<ushort>();
            if (_glyphs.TryGetValue(s, out var g)) return g;
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            int max = bytes.Length + 1;
            var idx = new ushort[max];
            unsafe
            {
                fixed (byte* b = bytes)
                fixed (ushort* o = idx)
                {
                    var font = (ImFont*)Ptr.NativePtr;
                    int count = 0;
                    byte* p = b;
                    byte* end = b + bytes.Length;
                    while (p < end)
                    {
                        uint c = 0;
                        if (*p < 0x80) { c = *p; p += 1; }
                        else if ((*p & 0xE0) == 0xC0) { c = (uint)((*p & 0x1F) << 6 | (p[1] & 0x3F)); p += 2; }
                        else if ((*p & 0xF0) == 0xE0) { c = (uint)((*p & 0x0F) << 12 | (p[1] & 0x3F) << 6 | (p[2] & 0x3F)); p += 3; }
                        else { c = (uint)((*p & 0x07) << 18 | (p[1] & 0x3F) << 12 | (p[2] & 0x3F) << 6 | (p[3] & 0x3F)); p += 4; }
                        var glyph = ImGuiNative.ImFont_FindGlyphNoFallback(font, (ushort)c);
                        idx[count++] = glyph != null && glyph->X1 > glyph->X0 ? (ushort)c : (ushort)'?';
                    }
                    var run = new ushort[count];
                    Array.Copy(idx, run, count);
                    _glyphs[s] = run;
                    return run;
                }
            }
        }
    }

    /// <summary>
    /// Loads Inter (400/500/600/700) at the sizes the reference uses, with 3x oversampling and a
    /// rasterizer density tied to the UI scale so text stays crisp at 125/150/200 % DPI.
    /// </summary>
    public sealed class Fonts
    {
        public Font H1;      // 26 px / 600  — window title ("Игры", "Мастерская", "Аккаунт")
        public Font H2;      // 19 px / 600  — section headers ("Подборка", "Легит")
        public Font H3;      // 15 px / 600  — card titles, row titles
        public Font Body;    // 13 px / 400  — body copy
        public Font BodyM;   // 13 px / 500  — emphasised body
        public Font Small;   // 11 px / 500  — chips, scope pills, meta
        public Font Tiny;    // 10 px / 500  — badges, captions
        public Font Big;     // 44 px / 600  — "23" counter on the account page
        public Font Price;   // 11 px / 600  — price figures
        public Font Micro;   // 4.5 px / 500 — the tiniest captions

        private readonly List<Font> _all = new();
        private readonly string _dir;
        public float Density { get; private set; } = 1f;

        /// <summary>Preview constructor: no atlas, no device — sizes come from the same ladder.</summary>
        public Fonts()
        {
            H1 = new Font { Size = 12.50f * 1.38f };
            H2 = new Font { Size = 9.50f * 1.38f };
            H3 = new Font { Size = 7.60f * 1.38f };
            Body = new Font { Size = 6.40f * 1.38f };
            BodyM = new Font { Size = 6.40f * 1.38f };
            Small = new Font { Size = 5.70f * 1.38f };
            Tiny = new Font { Size = 5.20f * 1.38f };
            Micro = new Font { Size = 4.70f * 1.38f };
            Big = new Font { Size = 20.00f * 1.38f };
            Price = new Font { Size = 11.50f * 1.38f };
        }

        public Fonts(string assetsDir, float dpiScale)
        {
            _dir = Path.Combine(assetsDir, "fonts");
            Density = Math.Clamp(dpiScale, 1f, 3f);
            var io = ImGui.GetIO();
            io.Fonts.Clear();
            io.Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;
            IntPtr ranges = io.Fonts.GetGlyphRangesCyrillic();

            // reference is a 2x capture: 26 px title @2x == 13 design px, so sizes here are design px
            H1 = Add("Inter-SemiBold.ttf", 12.50f, ranges, 1.38);
            H2 = Add("Inter-SemiBold.ttf", 9.50f, ranges, 1.38);
            H3 = Add("Inter-SemiBold.ttf", 7.60f, ranges, 1.38);
            Body = Add("Inter-Regular.ttf", 6.40f, ranges, 1.38);
            BodyM = Add("Inter-Medium.ttf", 6.40f, ranges, 1.38);
            Small = Add("Inter-Medium.ttf", 5.70f, ranges, 1.38);
            Tiny = Add("Inter-Medium.ttf", 5.20f, ranges, 1.38);
            Micro = Add("Inter-Medium.ttf", 4.70f, ranges, 1.38);
            Big = Add("Inter-SemiBold.ttf", 20.00f, ranges, 1.38);
            Price = Add("Inter-SemiBold.ttf", 11.50f, ranges, 1.38);
        }

        /// <summary>Every font is rasterised at its design size x EmScale: the reference's type is
        /// small relative to the window, but at 576x360 that would be sub-pixel, so the whole
        /// type ladder is scaled by one constant (documented in brief.md, [EST]).</summary>
        private Font Add(string file, float px, IntPtr ranges, float emScale)
        {
            var cfg = ImGuiNative.ImFontConfig_ImFontConfig();
            var cfgPtr = new ImFontConfigPtr(cfg);
            cfgPtr.OversampleH = 2;
            cfgPtr.OversampleV = 2;
            cfgPtr.PixelSnapH = 0;
            // rasterise at the DRAWN size so glyphs are never upscaled (px * emScale * DPI * UI scale)
            cfgPtr.RasterizerDensity = Density * emScale;
            cfgPtr.GlyphMinAdvanceX = 0;
            var path = Path.Combine(_dir, file);
            var f = ImGui.GetIO().Fonts.AddFontFromFileTTF(path, px, cfgPtr, ranges);
            var font = new Font { Ptr = f, Size = px * emScale };
            _all.Add(font);
            return font;
        }

        public void Build()
        {
            ImGui.GetIO().Fonts.Build();
        }
    }
}
