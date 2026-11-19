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

        /// <summary>Design-space line height (line box). Drawn size = Size * Program.Scale.</summary>
        public float Size;

        /// <summary>Devices px per window px (DPI). Part of the rasterisation density, not the layout.</summary>
        public float Density = 1f;

        private readonly Dictionary<string, Vector2> _cache = new();

        /// <summary>On-screen glyph size in framebuffer px (the size ImGui needs at draw time).</summary>
        public float DrawSize => Size * Program.Scale;

        public Vector2 Measure(string s)
        {
            if (string.IsNullOrEmpty(s)) return Vector2.Zero;
            if (_cache.TryGetValue(s, out var v)) return v;
            if (Ui.Rec != null) { var g = Ui.Rec.Glyph.Measure(s, DrawSize); _cache[s] = g; return g; }
            var size = Ptr.CalcTextSizeA(DrawSize, float.MaxValue, -1f, s);
            _cache[s] = size;
            return size;
        }

        public float Width(string s) => Measure(s).X;

        public void ClearCache() { _cache.Clear(); }

        // Note: AddText takes a C# string (it converts to UTF-8 internally), so no
        // glyph-index mapping is needed here — unknown characters fall back through the atlas.
    }

    /// <summary>
    /// The type ladder of the reference UI. Every size is the *line box* in design px; the em box is
    /// derived from Inter's vertical metrics so cap heights land on the measured values.
    /// </summary>
    public sealed class Fonts
    {
        public Font H1;      // 14 px  600 — screen titles ("Игры", "Мастерская")
        public Font H2;      // 13 px  600 — modal / card headings
        public Font H3;      // 11 px  600 — offer names, card titles
        public Font Body;    //  9.5 px 400
        public Font BodyM;   //  9.5 px 500
        public Font Small;   //  9 px  500 — chips, captions, CTA labels
        public Font Tiny;    //  8 px  500 — meta rows
        public Font Big;     // 44 px  600 — account counters
        public Font Price;   // 11 px  600 — price figures
        public Font Micro;   //  5.8 px 500 — the tiniest captions

        private readonly List<Font> _all = new();
        private readonly string _dir;

        /// <summary>Devices px per design px (DPI x window scale) used as the atlas rasterisation density.</summary>
        public float Density { get; private set; } = 1f;

        /// <summary>Type-scale multiplier applied to the whole ladder (design px -> line height).</summary>
        public float EmScale { get; private set; } = EmK;

        /// <summary>
        /// The reference is a 2x capture of a 576x360 UI, so a 13 px title is really ~6.5 design px of
        /// *line box*. ImGui reports CalcTextSizeA(s) as `s` tall and scales glyphs by size/bakedSize,
        /// so we pass line-box heights and bake the em box separately: em = line * DesignEm.
        /// DesignEm = 1 / (lineHeight/em) with Inter's 1.796 line height factor.
        /// </summary>
        public const float DesignEm = 0.5568f;
        public const float EmK = DesignEm * 1.38f;

        /// <summary>Preview constructor: no atlas, no device — sizes come from the same ladder.</summary>
        public Fonts()
        {
            H1 = new Font { Size = 14.00f };
            H2 = new Font { Size = 13.00f };
            H3 = new Font { Size = 11.00f };
            Body = new Font { Size = 9.50f };
            BodyM = new Font { Size = 9.50f };
            Small = new Font { Size = 9.00f };
            Tiny = new Font { Size = 8.00f };
            Micro = new Font { Size = 5.80f };
            Big = new Font { Size = 44.00f };
            Price = new Font { Size = 11.00f };
        }

        public Fonts(string assetsDir, float dpiScale)
        {
            _dir = Path.Combine(assetsDir, "fonts");
            Density = Math.Clamp(dpiScale, 1f, 3f);
            Ladder(ImGui.GetIO().Fonts.GetGlyphRangesCyrillic());
            ImGui.GetIO().Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;
        }

        private void Ladder(IntPtr ranges)
        {
            H1 = Add("Inter-SemiBold.ttf", 14.00f, ranges);
            H2 = Add("Inter-SemiBold.ttf", 13.00f, ranges);
            H3 = Add("Inter-SemiBold.ttf", 11.00f, ranges);
            Body = Add("Inter-Regular.ttf", 9.50f, ranges);
            BodyM = Add("Inter-Medium.ttf", 9.50f, ranges);
            Small = Add("Inter-Medium.ttf", 9.00f, ranges);
            Tiny = Add("Inter-Medium.ttf", 8.00f, ranges);
            Micro = Add("Inter-Medium.ttf", 5.80f, ranges);
            Big = Add("Inter-SemiBold.ttf", 44.00f, ranges);
            Price = Add("Inter-SemiBold.ttf", 11.00f, ranges);
        }

        /// <summary>
        /// Adds one face. The ImFont is loaded at the *design line height* and the atlas bakes glyphs at
        /// `size * RasterizerDensity`; AddText then scales by DrawSize/bakedSize, so DPI and window
        /// scale never change the layout.
        /// </summary>
        private Font Add(string file, float linePx, IntPtr ranges)
        {
            var cfg = ImGuiNative.ImFontConfig_ImFontConfig();
            var cfgPtr = new ImFontConfigPtr(cfg);
            cfgPtr.OversampleH = 4;          // maxed-out AA: no glyph is ever upscaled
            cfgPtr.OversampleV = 4;
            cfgPtr.PixelSnapH = 0;
            cfgPtr.RasterizerDensity = Density;
            cfgPtr.GlyphMinAdvanceX = 0;
            var path = Path.Combine(_dir, file);
            var ptr = ImGui.GetIO().Fonts.AddFontFromFileTTF(path, linePx, cfgPtr, ranges);
            var font = new Font { Ptr = ptr, Size = linePx, Density = Density };
            _all.Add(font);
            return font;
        }

        /// <summary>Drops every cached string measurement (call when the window scale changes).</summary>
        public void InvalidateMeasurements()
        {
            foreach (var f in _all) f.ClearCache();
        }

        /// <summary>Builds the atlas; must run inside a live ImGui context (first frame).</summary>
        public void Build()
        {
            ImGui.GetIO().Fonts.Build();
        }
    }
}
