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

        // Note: ImGui.NET's AddText takes a C# string (it converts to UTF-8 internally), so no
        // glyph-index mapping is needed here — unknown characters fall back through the atlas.
    }
}
