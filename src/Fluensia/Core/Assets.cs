using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using Veldrid;

namespace Fluensia.Core
{
    /// <summary>A texture registered with the ImGui renderer.</summary>
    public sealed class Tex
    {
        public IntPtr Id;
        public int Width, Height;
        public bool Valid => Id != IntPtr.Zero;
    }

    /// <summary>
    /// Loads the bundled art (wallpaper + cover art + avatars). Decoding happens in-process with a
    /// small PNG reader, so the app has no image-library dependency and behaves the same on
    /// Windows, Linux and macOS.
    /// </summary>
    public sealed class Assets
    {
        private readonly Dictionary<string, Tex> _cache = new Dictionary<string, Tex>();
        private readonly List<IDisposable> _owned = new List<IDisposable>();

        public Tex Wall { get; private set; }
        public Tex ArtCs2 { get; private set; }
        public Tex ArtValorant { get; private set; }
        public Tex ArtDota { get; private set; }
        public Tex ArtCod { get; private set; }
        public Tex AvIsadora { get; private set; }
        public Tex AvHimto { get; private set; }
        public Tex AvMagical { get; private set; }
        public Tex AvKayn { get; private set; }
        public Tex AvCatgirl { get; private set; }

        private Func<Texture, IntPtr> _bind;

        public void Load(string baseDir, Func<Texture, IntPtr> bind)
        {
            _bind = bind;
            var dir = Path.Combine(baseDir, "Assets");
            Wall = Get(dir, "wall.png");
            ArtCs2 = Get(dir, "art_cs2.png");
            ArtValorant = Get(dir, "art_valorant.png");
            ArtDota = Get(dir, "art_dota.png");
            ArtCod = Get(dir, "art_cod.png");
            AvIsadora = Get(dir, "av_isadora.png");
            AvHimto = Get(dir, "av_himto.png");
            AvMagical = Get(dir, "av_magical.png");
            AvKayn = Get(dir, "av_kayn.png");
            AvCatgirl = Get(dir, "av_catgirl.png");
        }

        public Tex Get(string dir, string file)
        {
            if (_cache.TryGetValue(file, out var cached)) return cached;
            Tex tex = null;
            var path = Path.Combine(dir, file);
            if (File.Exists(path))
            {
                try
                {
                    var img = Png.Decode(path);
                    tex = Upload(img.Rgba, img.Width, img.Height);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("asset failed: " + path + " -> " + ex.Message);
                }
            }
            if (tex == null) tex = Upload(Placeholder(), 2, 2);
            _cache[file] = tex;
            return tex;
        }

        private static byte[] Placeholder()
        {
            // obvious magenta tile: a missing asset is loud, never silently blank
            return new byte[] { 220, 40, 180, 255, 120, 20, 100, 255, 120, 20, 100, 255, 220, 40, 180, 255 };
        }

        private Tex Upload(byte[] rgba, int w, int h)
        {
            var gd = Program.Gd;
            var tex = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                (uint)w, (uint)h, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));
            tex.Name = "asset";
            gd.UpdateTexture(tex, rgba, 0, 0, 0, (uint)w, (uint)h, 1, 0, 0);
            _owned.Add(tex);
            var id = _bind != null ? _bind(tex) : IntPtr.Zero;
            return new Tex { Id = id, Width = w, Height = h };
        }

        public void DisposeAll()
        {
            foreach (var r in _owned) r.Dispose();
            _owned.Clear();
            _cache.Clear();
        }

        // ---------------------------------------------------------------------------------------
        //  .flut: our tiny container — "FLU1", u8 version, u16 w, u16 h, 768-byte palette, deflate indices.
        //  Keeps 10 bitmap assets at ~2.1 MB instead of ~9 MB of PNGs, and decodes in a few ms.
        // ---------------------------------------------------------------------------------------
        // ---------------------------------------------------------------------------------------
        //  PNG decoder: 8-bit, gray / RGB / palette / gray+alpha / RGBA, non-interlaced
        // ---------------------------------------------------------------------------------------
        public static class Png
        {
            public sealed class Image
            {
                public int Width;
                public int Height;
                public byte[] Rgba;
            }

            public static Image Decode(string path)
            {
                using var fs = File.OpenRead(path);
                var sig = new byte[8];
                fs.ReadExactly(sig, 0, 8);
                if (sig[1] != 'P' || sig[2] != 'N' || sig[3] != 'G') throw new InvalidDataException("not a PNG");

                int w = 0, h = 0, bitDepth = 8, colorType = 6;
                byte[] palette = null;
                var idat = new MemoryStream();
                var buf = new byte[4];
                while (true)
                {
                    if (fs.Read(buf, 0, 4) < 4) break;
                    uint len = (uint)(buf[0] << 24 | buf[1] << 16 | buf[2] << 8 | buf[3]);
                    var type = new byte[4];
                    fs.ReadExactly(type, 0, 4);
                    var data = new byte[len];
                    if (len > 0) fs.ReadExactly(data, 0, (int)len);
                    fs.ReadExactly(buf, 0, 4); // CRC (unchecked)
                    string ts = System.Text.Encoding.ASCII.GetString(type);
                    if (ts == "IHDR")
                    {
                        w = (int)((uint)(data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3]));
                        h = (int)((uint)(data[4] << 24 | data[5] << 16 | data[6] << 8 | data[7]));
                        bitDepth = data[8];
                        colorType = data[9];
                        if (data[12] != 0) throw new NotSupportedException("interlaced PNG");
                    }
                    else if (ts == "PLTE") palette = data;
                    else if (ts == "IDAT") idat.Write(data, 0, data.Length);
                    else if (ts == "IEND") break;
                }
                if (bitDepth != 8) throw new NotSupportedException("bit depth " + bitDepth);
                if (colorType == 3 && (palette == null || palette.Length < 3)) throw new InvalidDataException("palette PNG without PLTE");
                int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new NotSupportedException("color type " + colorType) };

                idat.Position = 2; // skip the zlib header, DeflateStream handles raw deflate
                using var inflater = new DeflateStream(idat, CompressionMode.Decompress);
                int stride = w * channels;
                var raw = new byte[(stride + 1) * h];
                int read = 0;
                while (read < raw.Length)
                {
                    int r = inflater.Read(raw, read, raw.Length - read);
                    if (r <= 0) break;
                    read += r;
                }

                // filter units are in bytes; a palette index is one byte per pixel
                int bpp = Math.Max(1, channels);
                var px = new byte[stride * h];
                int prevRow = -1;
                for (int y = 0; y < h; y++)
                {
                    int ft = raw[y * (stride + 1)];
                    int src = y * (stride + 1) + 1;
                    int dst = y * stride;
                    for (int x = 0; x < stride; x++)
                    {
                        int a = x >= bpp ? px[dst + x - bpp] : 0;
                        int b = prevRow >= 0 ? px[prevRow + x] : 0;
                        int c = (x >= bpp && prevRow >= 0) ? px[prevRow + x - bpp] : 0;
                        int v = raw[src + x];
                        switch (ft)
                        {
                            case 0: break;
                            case 1: v += a; break;
                            case 2: v += b; break;
                            case 3: v += (a + b) >> 1; break;
                            case 4:
                            {
                                int p = a + b - c;
                                int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                                v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
                                break;
                            }
                            default: throw new InvalidDataException("filter " + ft);
                        }
                        px[dst + x] = (byte)v;
                    }
                    prevRow = dst;
                }

                var rgba = new byte[w * h * 4];
                for (int i = 0, s = 0, d = 0; i < w * h; i++, s += channels, d += 4)
                {
                    if (colorType == 3)   // palette indices, expanded through PLTE
                    {
                        int pi = px[s] * 3;
                        rgba[d] = palette[pi]; rgba[d + 1] = palette[pi + 1]; rgba[d + 2] = palette[pi + 2]; rgba[d + 3] = 255;
                        continue;
                    }
                    switch (channels)
                    {
                        case 4:
                            rgba[d] = px[s]; rgba[d + 1] = px[s + 1]; rgba[d + 2] = px[s + 2]; rgba[d + 3] = px[s + 3];
                            break;
                        case 3:
                            rgba[d] = px[s]; rgba[d + 1] = px[s + 1]; rgba[d + 2] = px[s + 2]; rgba[d + 3] = 255;
                            break;
                        case 1:
                            rgba[d] = rgba[d + 1] = rgba[d + 2] = px[s]; rgba[d + 3] = 255;
                            break;
                        case 2:
                            rgba[d] = rgba[d + 1] = rgba[d + 2] = px[s]; rgba[d + 3] = px[s + 1];
                            break;
                    }
                }
                return new Image { Width = w, Height = h, Rgba = rgba };
            }
        }
    }
}
