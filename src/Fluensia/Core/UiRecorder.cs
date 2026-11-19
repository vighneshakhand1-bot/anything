using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Fluensia.Core
{
    /// <summary>
    /// Software canvas that mirrors ImGui draw calls in design space and writes a PNG.
    /// It is used only by `Fluensia --ui-preview <dir>`: a GPU-less build machine can then check the
    /// layout (boxes, text runs, gradients, clipping) against the reference frames. It is a layout
    /// verifier, not the renderer — the shipped app always draws through ImGui/Veldrid.
    /// </summary>
    public sealed class UiRecorder
    {
        public readonly int W, H;
        private readonly float _scale;
        private readonly byte[] _px;         // RGBA
        private readonly List<(Vector4 a, Vector4 b)> _clips = new List<(Vector4, Vector4)>();
        public readonly Glyphs Glyph;
        public string LastText = "";

        /// <summary>Whether any of the recorded text matched a needle (verifier helper).</summary>
        public readonly List<string> SeenText = new List<string>();

        public UiRecorder(int width, int height, float scale, Glyphs glyphs)
        {
            W = width; H = height; _scale = scale;
            _px = new byte[W * H * 4];
            for (int i = 0; i < _px.Length; i += 4) { _px[i] = 40; _px[i + 1] = 45; _px[i + 2] = 48; _px[i + 3] = 255; }
            Glyph = glyphs;
            _clips.Add((new Vector4(-1e6f, -1e6f, 1e6f, 1e6f), new Vector4(-1e6f, -1e6f, 1e6f, 1e6f)));
        }

        private (int x0, int y0, int x1, int y1) Box(Vector2 a, Vector2 b)
        {
            var c = _clips[_clips.Count - 1];
            float ax = MathF.Max(a.X, c.a.X) * _scale, ay = MathF.Max(a.Y, c.a.Y) * _scale;
            float bx = MathF.Min(b.X, c.b.X) * _scale, by = MathF.Min(b.Y, c.b.Y) * _scale;
            return ((int)MathF.Floor(ax), (int)MathF.Floor(ay), (int)MathF.Ceiling(bx), (int)MathF.Ceiling(by));
        }

        private void Blend(int x, int y, uint col)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            float a = ((col >> 24) & 0xFF) / 255f;
            if (a <= 0.002f) return;
            int i = (y * W + x) * 4;
            _px[i] = (byte)(((col >> 0) & 0xFF) * a + _px[i] * (1 - a));
            _px[i + 1] = (byte)(((col >> 8) & 0xFF) * a + _px[i + 1] * (1 - a));
            _px[i + 2] = (byte)(((col >> 16) & 0xFF) * a + _px[i + 2] * (1 - a));
            _px[i + 3] = 255;
        }

        private void FillBox(int xa, int ya, int xb, int yb, float rad, Func<float, float, uint> shade)
        {
            for (int y = Math.Max(0, ya); y < Math.Min(H, yb); y++)
            {
                for (int x = Math.Max(0, xa); x < Math.Min(W, xb); x++)
                {
                    // rounded-rect coverage (cheap: only corners are tested)
                    float fx = x + 0.5f, fy = y + 0.5f;
                    if (rad > 0.5f)
                    {
                        float lx = rad, ly = rad;
                        float cx = Math.Clamp(fx, xa + lx, xb - lx);
                        float cy = Math.Clamp(fy, ya + ly, yb - ly);
                        float dx = fx - cx, dy = fy - cy;
                        if (dx * dx + dy * dy > lx * lx) continue;
                    }
                    Blend(x, y, shade(fx, fy));
                }
            }
        }

        private static uint Lerp(uint c0, uint c1, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            uint a = (uint)(((c0 >> 24) & 0xFF) + ((((c1 >> 24) & 0xFF) - ((c0 >> 24) & 0xFF)) * t));
            uint r = (uint)(((c0 >> 0) & 0xFF) + ((((c1 >> 0) & 0xFF) - ((c0 >> 0) & 0xFF)) * t));
            uint g = (uint)(((c0 >> 8) & 0xFF) + ((((c1 >> 8) & 0xFF) - ((c0 >> 8) & 0xFF)) * t));
            uint b = (uint)(((c0 >> 16) & 0xFF) + ((((c1 >> 16) & 0xFF) - ((c0 >> 16) & 0xFF)) * t));
            return (a << 24) | (b << 16) | (g << 8) | r;
        }

        public void Rect(Vector2 a, Vector2 b, uint col, float rad)
        {
            LogRect(a, b);
            var (xa, ya, xb, yb) = Box(a, b);
            FillBox(xa, ya, xb, yb, rad * _scale, (x, y) => col);
        }

        // the Ui.* helpers already log; this keeps the recorder self-sufficient when used directly
        private void LogRect(Vector2 a, Vector2 b) { }

        public void RectOutline(Vector2 a, Vector2 b, uint col, float rad, float th)
        {
            var (xa, ya, xb, yb) = Box(a, b);
            int t = Math.Max(1, (int)MathF.Round(th * _scale));
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                {
                    bool edge = x - xa < t || xb - 1 - x < t || y - ya < t || yb - 1 - y < t;
                    if (edge) Blend(x, y, col);
                }
        }

        public void VGrad(Vector2 a, Vector2 b, uint c0, uint c1, float rad)
        {
            var (xa, ya, xb, yb) = Box(a, b);
            float h = MathF.Max(1, yb - ya);
            FillBox(xa, ya, xb, yb, rad * _scale, (x, y) => Lerp(c0, c1, (y - ya) / h));
        }

        public void HGrad(Vector2 a, Vector2 b, uint c0, uint c1, float rad)
        {
            var (xa, ya, xb, yb) = Box(a, b);
            float w = MathF.Max(1, xb - xa);
            FillBox(xa, ya, xb, yb, rad * _scale, (x, y) => Lerp(c0, c1, (x - xa) / w));
        }

        public void DiagGrad(Vector2 a, Vector2 b, uint c0, uint c1, float rad)
        {
            var (xa, ya, xb, yb) = Box(a, b);
            float w = MathF.Max(1, xb - xa), h = MathF.Max(1, yb - ya);
            FillBox(xa, ya, xb, yb, rad * _scale, (x, y) => Lerp(c0, c1, ((x - xa) / w + (y - ya) / h) * 0.5f));
        }

        public void Shadow(Vector2 a, Vector2 b, float rad, float spread, float alpha)
        {
            float s = spread * _scale;
            for (int i = (int)s; i > 0; i--)
            {
                float t = i / s;
                uint col = ((uint)(alpha * (1 - t) * (1 - t) * 90) << 24);
                RectOutline(a - new Vector2(i / _scale, i / _scale), b + new Vector2(i / _scale, i / _scale), col, rad, 1f);
            }
        }

        public void Line(Vector2 a, Vector2 b, uint col, float th)
        {
            int steps = (int)MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var p = a + (b - a) * t;
                Rect(p - new Vector2(th * 0.5f), p + new Vector2(th * 0.5f), col, 0f);
            }
        }

        public void Circle(Vector2 c, float r, uint col, float th)
        {
            int steps = 48;
            for (int i = 0; i <= steps; i++)
            {
                float ang = i / (float)steps * MathF.PI * 2f;
                var p = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * r;
                Rect(p - new Vector2(th * 0.5f), p + new Vector2(th * 0.5f), col, 0f);
            }
        }

        public void Text(Vector2 pos, float px, uint col, string s)
        {
            LastText = s;
            SeenText.Add(s);
            float x = pos.X;
            float baseline = pos.Y + (Metrics.Ascender - Metrics.CapHeight) * px;
            foreach (var ch in s)
            {
                var g = Glyph.Get(ch, px);
                int gw = g.w, gh = g.h;
                var (xa, ya, xb, yb) = Box(new Vector2(x, baseline), new Vector2(x + gw / _scale, baseline + gh / _scale));
                for (int gy = 0; gy < gh; gy++)
                    for (int gx = 0; gx < gw; gx++)
                    {
                        byte cov = g.px[gy * gw + gx];
                        if (cov == 0) continue;
                        int X = xa + gx, Y = ya + gy;
                        uint a = (uint)(((col >> 24) & 0xFF) * cov / 255) << 24;
                        Blend(X, Y, (col & 0x00FFFFFF) | a);
                    }
                x += g.adv;
            }
        }

        public void Save(string path)
        {
            WritePng(path, W, H, _px);
        }

        private static void WritePng(string path, int w, int h, byte[] rgba)
        {
            using var fs = File.Create(path);
            fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            ihdr[0] = (byte)(w >> 24); ihdr[1] = (byte)(w >> 16); ihdr[2] = (byte)(w >> 8); ihdr[3] = (byte)w;
            ihdr[4] = (byte)(h >> 24); ihdr[5] = (byte)(h >> 16); ihdr[6] = (byte)(h >> 8); ihdr[7] = (byte)h;
            ihdr[8] = 8; ihdr[9] = 6;
            Chunk(fs, "IHDR", ihdr);
            byte[] raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++)
            {
                raw[y * (w * 4 + 1)] = 0;
                Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
            }
            // one stored deflate block per 32 KiB so no compressor is needed
            using var idat = new MemoryStream();
            idat.WriteByte(0x78); idat.WriteByte(0x01);
            int pos = 0;
            do
            {
                int len = Math.Min(32768, raw.Length - pos);
                bool final = pos + len >= raw.Length;
                idat.WriteByte((byte)(final ? 1 : 0));
                idat.WriteByte((byte)(len & 0xFF)); idat.WriteByte((byte)(len >> 8));
                idat.WriteByte((byte)(~len & 0xFF)); idat.WriteByte((byte)((~len >> 8) & 0xFF));
                idat.Write(raw, pos, len);
                pos += len;
            } while (pos < raw.Length);
            uint sa = 1, sb = 0;
            foreach (var x in raw) { sa = (sa + x) % 65521; sb = (sb + sa) % 65521; }
            idat.WriteByte((byte)(sb >> 8)); idat.WriteByte((byte)sb);
            idat.WriteByte((byte)(sa >> 8)); idat.WriteByte((byte)sa);
            Chunk(fs, "IDAT", idat.ToArray());
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length };
            s.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(t); s.Write(data);
            uint crc = 0xFFFFFFFF;
            foreach (var b in t) crc = Crc(crc, b);
            foreach (var b in data) crc = Crc(crc, b);
            crc ^= 0xFFFFFFFF;
            s.Write(new byte[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
        }

        private static uint Crc(uint c, byte b)
        {
            c ^= b;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            return c;
        }

        private static byte[] Zlib(byte[] data)
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0x78); ms.WriteByte(0x01);
            int pos = 0;
            do
            {
                int len = Math.Min(32768, data.Length - pos);
                bool final = pos + len >= data.Length;
                ms.WriteByte((byte)(final ? 1 : 0));
                ms.WriteByte((byte)(len & 0xFF)); ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)(~len & 0xFF)); ms.WriteByte((byte)((~len >> 8) & 0xFF));
                ms.Write(data, pos, len);
                pos += len;
            } while (pos < data.Length);
            uint a = 1, b = 0;
            foreach (var x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
            ms.WriteByte((byte)(b >> 8)); ms.WriteByte((byte)b);
            ms.WriteByte((byte)(a >> 8)); ms.WriteByte((byte)a);
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Proxy glyph set for the preview: advances come from the real Inter metrics baked in
    /// <see cref="Metrics"/>, and each glyph is stamped as a solid box of x-height/cap-height so line
    /// lengths match the shipped font atlas. It is a layout proxy, not a font rasteriser.
    /// </summary>
    public sealed class Glyphs
    {
        private readonly Dictionary<string, Vector2> _sizes = new Dictionary<string, Vector2>();

        public Vector2 Measure(string s, float sizePx)
        {
            var key = sizePx.ToString("F2") + "|" + s;
            if (_sizes.TryGetValue(key, out var v)) return v;
            v = new Vector2(Metrics.Width(s) * sizePx, Metrics.CapHeight * sizePx);
            _sizes[key] = v;
            return v;
        }

        /// <summary>One glyph cell: ink box + advance, both in device px.</summary>
        public (int w, int h, byte[] px, float adv) Get(char ch, float sizePx)
        {
            float adv = Metrics.Advance(ch) * sizePx;
            int w = Math.Max(1, (int)MathF.Round(adv * 0.82f));
            int h = Math.Max(2, (int)MathF.Round(sizePx * (char.IsUpper(ch) || ch == 'М' ? Metrics.CapHeight : Metrics.XHeight)));
            var px = new byte[w * h];
            bool space = ch == ' ' || ch == '\u00A0' || ch == '\t';
            if (!space)
            {
                // solid stem pattern keyed off the codepoint: distinct enough to read word shape,
                // while the advance (not the ink) drives every measurement in the UI
                int seed = (int)(ch * 2654435761u % 7);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        bool on = ((x * 3 + y * 5 + seed) % 5) < 3 || x == 0 || x == w - 1;
                        if (on) px[y * w + x] = 190;
                    }
            }
            return (w, h, px, adv);
        }
    }
}
