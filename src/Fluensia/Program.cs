using System;
using System.Diagnostics;
using System.Numerics;
using Fluensia.Core;
using Fluensia.Views;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Fluensia
{
    internal static class Program
    {
        // Design space the whole UI is authored in (matches the reference recording).
        public const float DesignW = 576f;
        public const float DesignH = 360f;

        public static Sdl2Window Window;
        public static GraphicsDevice Gd;
        public static CommandList Cl;
        public static ImGuiController Controller;
        public static App App;

        public static float Scale = 1f;          // window px per design px
        public static float UiScale = 1f;        // DPI x Scale, known before the font atlas exists
        public static Vector2 Offset;            // top-left of the design rect inside the window
        public static float Dpi = 1f;
        private static float _fontScale = 1f;

        private static void Main(string[] args)
        {
            // ---- headless snapshot mode: renders N frames into PNGs (used by tools/snapshot.py)
            string shotDir = null;
            string shotState = "games";
            int shotFrames = 1;
            float shotT = 0f;
            bool autoplay = true;
            string previewDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--ui-preview": previewDir = args[++i]; break;
                    case "--snapshot": shotDir = args[++i]; break;
                    case "--state": shotState = args[++i]; break;
                    case "--frames": shotFrames = int.Parse(args[++i]); break;
                    case "--time": shotT = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--no-autoplay": autoplay = false; break;
                }
            }

            int winW = shotDir != null ? shotW : (int)(DesignW * 2);
            int winH = shotDir != null ? shotH : (int)(DesignH * 2);
            UiScale = shotDir != null ? 2f : 1f;
            if (previewDir != null) { RunPreview(previewDir, shotState, shotT); return; }
            if (Environment.GetEnvironmentVariable("FLUENSIA_PREVIEW") == "1") { RunPreview("/tmp/flu_preview", shotState, shotT); return; }

            var windowCI = new WindowCreateInfo(60, 40, winW, winH, WindowState.Normal, "Fluensia");
            var options = new GraphicsDeviceOptions(false, null, true, ResourceBindingModel.Improved, true, true)
            {
                SyncToVerticalBlank = true,
            };
            try
            {
                VeldridStartup.CreateWindowAndGraphicsDevice(windowCI, options, GraphicsBackend.OpenGL, out Window, out Gd);
            }
            catch (Exception)
            {
                VeldridStartup.CreateWindowAndGraphicsDevice(windowCI, options, out Window, out Gd);
            }

            try { Window.BorderVisible = false; } catch { }
            try { Window.CursorVisible = true; } catch { }

            Cl = Gd.ResourceFactory.CreateCommandList();
            Controller = new ImGuiController(Gd, Gd.MainSwapchain.Framebuffer.OutputDescription, Window.Width, Window.Height);

            ImGui.StyleColorsDark();
            var style = ImGui.GetStyle();
            style.WindowPadding = Vector2.Zero;
            style.WindowBorderSize = 0;
            style.WindowRounding = 0;
            style.FramePadding = Vector2.Zero;
            style.ItemSpacing = Vector2.Zero;
            style.AntiAliasedLines = true;
            style.AntiAliasedLinesUseTex = true;
            style.AntiAliasedFill = true;
            style.CurveTessellationTol = 0.12f;
            style.CircleTessellationMaxError = 0.12f;
            var io = ImGui.GetIO();
            io.ConfigWindowsMoveFromTitleBarOnly = true;

            Dpi = Platform.DpiScale(Window.SdlWindowHandle);
            if (shotDir != null) Dpi = 2f;
            Scale = shotDir != null ? 2f : 1f;
            Ui.Dt = 1f / 60f;
            if (shotDir != null) Ui.Layout = new System.Collections.Generic.List<string>();

            var sw = Stopwatch.StartNew();
            double last = 0;
            int frame = 0;
            bool initialised = false;

            while (Window.Exists)
            {
                var snapshot = Window.PumpEvents();
                double now = sw.Elapsed.TotalSeconds;
                float dt = (float)Math.Min(now - last, 0.1);
                last = now;
                Ui.Dt = Math.Max(dt, 1e-4f);

                float w = Window.Width, h = Window.Height;
                if (shotDir != null) { w = shotW; h = shotH; }
                UpdateViewport(w, h, Dpi);
            UiScale = Scale * Dpi;
                Controller.WindowResized((int)w, (int)h);
                // ImGui works in framebuffer pixels: no extra scale factor, we do the scaling ourselves
                Controller.SetScaleFactor(Vector2.One);
                Controller.Density = Dpi * Scale;

                ImGui.GetIO().DeltaTime = Math.Max(dt, 1e-4f);
                Controller.Update(dt, shotDir == null ? snapshot : null);

                if (initialised && Math.Abs(UiScale - _fontScale) > 0.02f && shotDir == null)
                {
                    _fontScale = UiScale;
                    App.F.InvalidateMeasurements();      // cached string sizes depend on Program.Scale
                    App.F = new Fonts(AppContext.BaseDirectory, UiScale);
                    App.F.Build();
                    Controller.RecreateFontDeviceTexture(Gd);
                }

                if (!initialised)
                {
                    // fonts + art need a live ImGui context and device, so they come after the first frame begins
                    App = new App();
                    var fonts = new Fonts(AppContext.BaseDirectory, UiScale);
                    fonts.Build();
                    App.Init(fonts);
                    _fontScale = UiScale;
                    Controller.RecreateFontDeviceTexture(Gd);
                    if (shotDir != null) App.PlayState(shotState, shotT);
                    initialised = true;
                }

                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(w, h));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.Begin("##root", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings);
                ImGui.SetCursorScreenPos(Vector2.Zero);
                App.Frame(dt, autoplay);
                ImGui.End();
                ImGui.PopStyleVar();

                Cl.Begin();
                Cl.SetFramebuffer(Gd.MainSwapchain.Framebuffer);
                Cl.ClearColorTarget(0, new RgbaFloat(0.26f, 0.31f, 0.32f, 1f));
                Cl.SetFullViewports();
                Controller.Render(Gd, Cl);
                Cl.End();
                Gd.SubmitCommands(Cl);
                Gd.SwapBuffers();

                if (shotDir != null)
                {
                    Snapshot.Png(Gd.MainSwapchain.Framebuffer, System.IO.Path.Combine(shotDir, $"f{frame:000}.png"));
                    frame++;
                    if (frame >= shotFrames)
                    {
                        System.IO.Directory.CreateDirectory(shotDir);
                        Tools.DumpLayout(System.IO.Path.Combine(shotDir, "layout.txt"));
                        break;
                    }
                }
            }
        }

        public static int shotW = (int)(DesignW * 2), shotH = (int)(DesignH * 2);

        /// <summary>
        /// GPU-less layout preview: builds the same widget tree with the software canvas mirroring
        /// every draw call, then writes a PNG. Used on build machines / CI to eyeball the layout.
        /// </summary>
        private static void RunPreview(string dir, string state, float time)
        {
            System.IO.Directory.CreateDirectory(dir);
            const float s = 2f;
            Scale = s; UiScale = s; Offset = Vector2.Zero;
            ImGui.CreateContext();
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(DesignW, DesignH);
            io.DisplayFramebufferScale = Vector2.One;
            io.DeltaTime = 1f / 60f;
            io.Fonts.AddFontDefault();
            io.Fonts.Build();
            ImGui.StyleColorsDark();

            Ui.Rec = new UiRecorder((int)(DesignW * s), (int)(DesignH * s), s, new Glyphs(8));
            Ui.Layout = new System.Collections.Generic.List<string>();
            App = new App();
            App.Init(new Fonts(), loadArt: false);
            App.PlayState(state, time);

            // warm-up frames: let the hover/toggle springs settle, then capture the frame we keep
            for (int frame = 0; frame < 4; frame++)
            {
                Ui.Layout?.Clear();
                Ui.Dt = 0.2f;
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(DesignW, DesignH));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.Begin("##preview", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
                App.Frame(0.2f, false);
                ImGui.End();
                ImGui.PopStyleVar();
                ImGui.Render();
            }
            var outPath = System.IO.Path.Combine(dir, "preview_" + state + ".png");
            Ui.Rec.Save(outPath);
            Console.WriteLine("preview written: " + outPath);
            var report = System.IO.Path.Combine(dir, "preview_" + state + ".txt");
            System.IO.File.WriteAllLines(report, Ui.Rec.SeenText);
            Console.WriteLine("text dump: " + report + " (" + Ui.Rec.SeenText.Count + " runs)");
            // the layout log carries only the last frame (earlier warm-up frames are cleared above)
            Console.WriteLine("layout runs: " + (Ui.Layout?.Count ?? 0));
            if (Ui.Layout != null) System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "preview_" + state + ".layout.txt"), Ui.Layout);
        }

        /// <summary>Uniform "contain" fit of the 576x360 design into the window, snapped to whole pixels.</summary>
        public static void UpdateViewport(float w, float h, float dpi)
        {
            float byWidth = w / DesignW;
            float byHeight = h / DesignH;
            Scale = MathF.Min(byWidth, byHeight);
            // integer scale keeps hairlines and icon strokes pixel-perfect, like the recording
            if (Scale > 1.4f) Scale = MathF.Floor(Scale * 2f) / 2f;
            Offset = new Vector2(MathF.Round((w - DesignW * Scale) * 0.5f), MathF.Round((h - DesignH * Scale) * 0.5f));
            _ = dpi;
        }
    }

    /// <summary>Reads back the swapchain and writes a PNG (headless verification).</summary>
    internal static class Snapshot
    {
        public static void Png(Framebuffer fb, string path)
        {
            uint w = fb.Width, h = fb.Height;
            byte[] rgba = new byte[w * h * 4];
            var gd = Program.Gd;
            var tex = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(w, h, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Staging));
            var cl = gd.ResourceFactory.CreateCommandList();
            cl.Begin();
            cl.CopyTexture(fb.ColorTargets[0].Target, tex);
            cl.End();
            gd.SubmitCommands(cl);
            gd.WaitForIdle();
            // Map<T> needs an explicit MapMode; Data/RowPitch live on the inner MappedResource
            var map = gd.Map<byte>(tex, MapMode.Read);
            var raw = map.MappedResource;
            for (int y = 0; y < h; y++)
                System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(raw.Data, (int)(y * raw.RowPitch)), rgba, y * (int)w * 4, (int)w * 4);
            gd.Unmap(tex);
            tex.Dispose();
            // BGRA -> RGBA for the PNG encoder
            for (int i = 0; i < rgba.Length; i += 4)
            {
                byte b0 = rgba[i];
                rgba[i] = rgba[i + 2];
                rgba[i + 2] = b0;
            }
            WritePng(path, (int)w, (int)h, rgba);
        }

        // tiny PNG writer (RGBA8, no compression dependency)
        private static void WritePng(string path, int w, int h, byte[] rgba)
        {
            using var fs = System.IO.File.Create(path);
            fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            BigEndian(ihdr, 0, (uint)w); BigEndian(ihdr, 4, (uint)h);
            ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(fs, "IHDR", ihdr);
            byte[] raw = new byte[(w * 4 + 1) * h];
            for (int y = 0; y < h; y++)
            {
                raw[y * (w * 4 + 1)] = 0;
                Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
            }
            Chunk(fs, "IDAT", Zlib.Compress(raw));
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        private static void BigEndian(byte[] b, int off, uint v)
        {
            b[off] = (byte)(v >> 24); b[off + 1] = (byte)(v >> 16); b[off + 2] = (byte)(v >> 8); b[off + 3] = (byte)v;
        }

        private static void Chunk(System.IO.Stream s, string type, byte[] data)
        {
            var len = new byte[4]; BigEndian(len, 0, (uint)data.Length); s.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t); s.Write(data);
            var crc = new Crc32(); crc.Update(t); crc.Update(data);
            var c = new byte[4]; BigEndian(c, 0, crc.Value); s.Write(c);
        }

        private sealed class Crc32
        {
            private static readonly uint[] Table = Build();
            private uint _v = 0xFFFFFFFF;
            public uint Value => _v ^ 0xFFFFFFFF;
            public void Update(byte[] d) { foreach (var b in d) _v = Table[(_v ^ b) & 0xFF] ^ (_v >> 8); }
            private static uint[] Build()
            {
                var t = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                    t[i] = c;
                }
                return t;
            }
        }

        /// <summary>zlib (RFC1950) wrapper around stored deflate blocks — 5 bytes + 5/32KiB overhead.</summary>
        private static class Zlib
        {
            public static byte[] Compress(byte[] data)
            {
                using var ms = new System.IO.MemoryStream();
                ms.WriteByte(0x78); ms.WriteByte(0x01);
                int pos = 0;
                const int block = 32768;
                do
                {
                    int len = Math.Min(block, data.Length - pos);
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
    }
}
