using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace Fluensia.Core
{
    /// <summary>
    /// Tiny immediate-mode widget kit drawn straight into ImDrawList.
    /// Why not ImGui::Button/Text? Because the reference UI is a bespoke glass design: we need
    /// exact rounding, per-corner radii, gradient fills, icon glyphs and measured hover/press
    /// timings. Everything below is stateless except the per-widget animation cache.
    /// </summary>
    public static class Ui
    {
        // cached hover/press animation values, keyed by a stable string id
        private static readonly Dictionary<string, float> _hover = new();
        private static readonly Dictionary<string, float> _press = new();
        private static readonly Dictionary<string, float> _toggle = new();
        private static readonly Dictionary<string, float> _scroll = new();

        public static float Dt;

        public static float Hover(string id, bool over)
        {
            float v = _hover.TryGetValue(id, out var x) ? x : 0f;
            v = Ease.SpringLerp(v, over ? 1f : 0f, 18f, 1f, Dt);
            _hover[id] = v;
            return v;
        }

        public static float Press(string id, bool down)
        {
            float v = _press.TryGetValue(id, out var x) ? x : 0f;
            v = Ease.SpringLerp(v, down ? 1f : 0f, 26f, 1f, Dt);
            _press[id] = v;
            return v;
        }

        /// <summary>0..1 animated boolean (tabs, toggles, chips). 0.22s in/out, ease-in-out.</summary>
        public static float Toggle(string id, bool on, float speed = 4.6f)
        {
            float v = _toggle.TryGetValue(id, out var x) ? x : (on ? 1f : 0f);
            v = Ease.SpringLerp(v, on ? 1f : 0f, speed, 1f, Dt);
            _toggle[id] = v;
            return v;
        }

        public static float Scroll(string id, float target)
        {
            float v = _scroll.TryGetValue(id, out var x) ? x : target;
            v = Ease.SpringLerp(v, target, 12f, 1f, Dt);
            _scroll[id] = v;
            return v;
        }

        /// <summary>
        /// Optional runtime draw-list mirror: when a UI preview recorder is attached, every
        /// primitive/text/image is also emitted into a software canvas (design units -> image px).
        /// Used by `--ui-preview` to render the layout without a GPU; costs two null checks per call.
        /// </summary>
        public static UiRecorder Rec;

        /// <summary>Optional design-space layout log (one line per widget) for the verification tools.</summary>
        public static List<string> Layout;

        /// <summary>Records one widget in *design* units (window px are divided back down by Program.Scale).</summary>
        private static void Log(string kind, Vector2 a, Vector2 b, string extra = null)
        {
            if (Layout == null) return;
            float s = Program.Scale <= 0f ? 1f : Program.Scale;
            Layout.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0,-6} {1,8:F2} {2,8:F2} {3,8:F2} {4,8:F2} {5}",
                kind, a.X / s, a.Y / s, b.X / s, b.Y / s, extra));
        }

        public static void Reset()
        {
            _hover.Clear(); _press.Clear(); _toggle.Clear(); _scroll.Clear();
        }

        // -----------------------------------------------------------------------------------------
        //  primitives
        // -----------------------------------------------------------------------------------------
        public static void Rect(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float rounding = 0f)
        {
            dl.AddRectFilled(a, b, col, rounding, ImDrawFlags.RoundCornersAll);
            Rec?.Rect(a, b, col, rounding);
            Log("rect", a, b, col.ToString("X8"));
        }

        public static void Rect(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 col, float rounding = 0f)
            => Rect(dl, a, b, Palette.U32(col), rounding);

        /// <summary>rect with per-corner radius: tl, tr, br, bl</summary>
        public static void RectC(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float tl, float tr, float br, float bl)
        {
            var f = (ImDrawFlags)((tl > 0 ? 1 : 0) | (tr > 0 ? 2 : 0) | (br > 0 ? 4 : 0) | (bl > 0 ? 8 : 0));
            float rad = MathF.Max(MathF.Max(tl, tr), MathF.Max(br, bl));
            dl.AddRectFilled(a, b, col, rad, f);
            Rec?.Rect(a, b, col, rad);
        }

        public static void RectC(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 col, float tl, float tr, float br, float bl)
            => RectC(dl, a, b, Palette.U32(col), tl, tr, br, bl);

        public static void Outline(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float rounding = 0f, float th = 1f)
        {
            dl.AddRect(a, b, col, rounding, ImDrawFlags.RoundCornersAll, th);
            Rec?.RectOutline(a, b, col, rounding, th);
            Log("outl", a, b, th.ToString("F2"));
        }

        /// <summary>Line with preview mirroring (the corner ticks / dividers all go through here).</summary>
        public static void Ln(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float th = 1f)
        {
            dl.AddLine(a, b, col, th);
            Rec?.Line(a, b, col, th);
        }

        public static void VGrad(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 top, Vector4 bot, float rounding = 0f)
        {
            uint c0 = Palette.U32(top), c1 = Palette.U32(bot);
            dl.AddRectFilledMultiColor(a, b, c0, c0, c1, c1);
            Rec?.VGrad(a, b, c0, c1, rounding);
            // cheap rounding mask: overlay 4 corner wedges with the background is impossible in IM,
            // so instead we draw the rounded border with the top colour and rely on rounded clip.
            if (rounding > 0f) dl.AddRect(a, b, c0, rounding, ImDrawFlags.RoundCornersAll, 1f);
        }

        /// <summary>vertical gradient inside a rounded rect — drawn as N slices so corners stay clean.</summary>
        public static void VGradRounded(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 top, Vector4 bot, float rounding)
        {
            if (rounding <= 0.5f) { VGrad(dl, a, b, top, bot); return; }
            if (Rec != null) { Rec.VGrad(a, b, Palette.U32(top), Palette.U32(bot), rounding); return; }
            int slices = 24;
            dl.PushClipRect(a - new Vector2(2), b + new Vector2(2), false);
            for (int i = 0; i < slices; i++)
            {
                float t0 = i / (float)slices, t1 = (i + 1) / (float)slices;
                var p0 = new Vector2(a.X, a.Y + (b.Y - a.Y) * t0);
                var p1 = new Vector2(b.X, a.Y + (b.Y - a.Y) * t1);
                dl.AddRectFilled(p0, p1 + new Vector2(0, 1), Palette.U32(Palette.Mix(top, bot, (t0 + t1) * 0.5f)));
            }
            // (each slice is solid, so no extra hooks are needed here)
            dl.PopClipRect();
            // re-add corner rounding on top with a soft ring so edges don't look chopped
            dl.AddRect(a, b, Palette.U32(top, 0.0f), rounding, ImDrawFlags.RoundCornersAll, 1f);
        }

        public static void HGrad(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 left, Vector4 right, float rounding = 0f)
        {
            uint c0 = Palette.U32(left), c1 = Palette.U32(right);
            Rec?.HGrad(a, b, c0, c1, rounding);
            if (Rec != null) return;
            if (rounding <= 0.5f) { dl.AddRectFilledMultiColor(a, b, c0, c1, c1, c0); return; }
            int slices = 20;
            dl.PushClipRect(a - new Vector2(2), b + new Vector2(2), false);
            for (int i = 0; i < slices; i++)
            {
                float t0 = i / (float)slices, t1 = (i + 1) / (float)slices;
                var p0 = new Vector2(a.X + (b.X - a.X) * t0, a.Y);
                var p1 = new Vector2(a.X + (b.X - a.X) * t1 + 1, b.Y);
                dl.AddRectFilled(p0, p1, Palette.U32(Palette.Mix(left, right, (t0 + t1) * 0.5f)));
            }
            dl.PopClipRect();
        }

        /// <summary>diagonal gradient (used by the olive detail panel header)</summary>
        public static void DiagGrad(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 tl, Vector4 br, float rounding = 0f)
        {
            if (Rec != null) { Rec.DiagGrad(a, b, Palette.U32(tl), Palette.U32(br), rounding); return; }
            dl.PushClipRect(a - new Vector2(2), b + new Vector2(2), false);
            uint c0 = Palette.U32(tl), c1 = Palette.U32(Palette.Mix(tl, br, 0.5f)), c2 = Palette.U32(br);
            dl.AddRectFilledMultiColor(a, b, c0, c1, c2, c1);
            dl.PopClipRect();
        }

        public static void Shadow(ImDrawListPtr dl, Vector2 a, Vector2 b, float rounding, float spread, float alpha)
        {
            if (Rec != null) { Rec.Shadow(a, b, rounding, spread, alpha); return; }
            // stacked soft edges (radial-ish): 1px rings with quadratic falloff, rounded corners kept
            int steps = (int)MathF.Ceiling(spread);
            for (int i = steps; i >= 1; i--)
            {
                float t = i / (float)steps;
                float alphaI = alpha * (1f - t) * (1f - t) * 0.55f;
                var p0 = a - new Vector2(i, i);
                var p1 = b + new Vector2(i, i);
                dl.AddRect(p0, p1, Palette.U32(new Vector4(0, 0, 0, alphaI)), rounding + i, ImDrawFlags.RoundCornersAll, 1f);
            }
        }

        // -----------------------------------------------------------------------------------------
        //  text
        // -----------------------------------------------------------------------------------------
        public static Vector2 Text(ImDrawListPtr dl, Font f, Vector2 pos, string s, Vector4 col, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(s)) return pos;
            var at = Snap(pos);
            Log("text", at, at + f.Measure(s), s.Replace(' ', '_'));
            if (Rec != null) { Rec.Text(at, f.DrawSize, Palette.U32(col, alpha), s); return new Vector2(pos.X + f.Measure(s).X, pos.Y); }
            if (f.Loaded) dl.AddText(f.Ptr, f.DrawSize, at, Palette.U32(col, alpha), s);
            else dl.AddText(at, Palette.U32(col, alpha), s);   // last-resort default font
            return new Vector2(pos.X + f.Measure(s).X, pos.Y);
        }

        public static Vector2 TextC(ImDrawListPtr dl, Font f, Vector2 center, string s, Vector4 col, float alpha = 1f)
        {
            var size = f.Measure(s);
            var p = new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f);
            if (!string.IsNullOrEmpty(s))
            {
                if (Rec != null) Rec.Text(Snap(p), f.DrawSize, Palette.U32(col, alpha), s);
                else if (f.Loaded) dl.AddText(f.Ptr, f.DrawSize, Snap(p), Palette.U32(col, alpha), s);
                else dl.AddText(Snap(p), Palette.U32(col, alpha), s);
            }
            return p;
        }

        /// <summary>Centred text that shrinks (never wraps) to fit a max width — used for chips/labels.</summary>
        public static Vector2 TextCFit(ImDrawListPtr dl, Font f, Vector2 center, string s, float maxW, Vector4 col, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(s)) return center;
            float w = f.Measure(s).X;
            if (w <= maxW || w <= 0f) return TextC(dl, f, center, s, col, alpha);
            // draw with the same font but a smaller line size so the run fits the pill
            float k = maxW / w;
            var size = f.DrawSize * k;
            var p = new Vector2(center.X - w * k * 0.5f, center.Y - f.Size * k * 0.5f);
            if (Rec != null) Rec.Text(Snap(p), size, Palette.U32(col, alpha), s);
            else if (f.Loaded) dl.AddText(f.Ptr, size, Snap(p), Palette.U32(col, alpha), s);
            else dl.AddText(Snap(p), Palette.U32(col, alpha), s);
            return p;
        }

        public static Vector2 TextR(ImDrawListPtr dl, Font f, Vector2 right, string s, Vector4 col, float alpha = 1f)
        {
            var size = f.Measure(s);
            var p = new Vector2(right.X - size.X, right.Y);
            if (!string.IsNullOrEmpty(s))
            {
                if (Rec != null) Rec.Text(Snap(p), f.DrawSize, Palette.U32(col, alpha), s);
                else if (f.Loaded) dl.AddText(f.Ptr, f.DrawSize, Snap(p), Palette.U32(col, alpha), s);
                else dl.AddText(Snap(p), Palette.U32(col, alpha), s);
            }
            return p;
        }

        /// <summary>Snap glyph origins to whole pixels: the single biggest crispness win on a
        /// non-integer UI scale (matches the reference's pixel-sharp 2x text).</summary>
        public static Vector2 Snap(Vector2 p) => new Vector2(MathF.Round(p.X), MathF.Round(p.Y));

        // -----------------------------------------------------------------------------------------
        //  vector icon set (24x24 design box, drawn with polylines/arcs -> resolution independent)
        // -----------------------------------------------------------------------------------------
        public enum Icon
        {
            None, Grid, Diamond, Cloud, Bell, Clipboard, CheckBadge, Gear, Chat, Search,
            Expand, Minimize, Close, Bolt, ArrowRight, ArrowLeft, Monitor, Refresh, Plus, Minus,
            Download, External, Cart, Shield, Discord, User, Dot, Target, Sliders, Crosshair, Keyboard, Lock,
            Chevrons, Folder
        }

        public static void DrawIcon(ImDrawListPtr dl, Icon icon, Vector2 c, float size, Vector4 col, float th = 1.6f)
        {
            uint u = Palette.U32(col);
            if (Rec != null)
            {
                // preview: a simple ring/dot stand-in keeps the layout readable without the path code
                Rec.Circle(c, size * 0.42f, u, th);
                return;
            }
            float s = size * 0.5f;
            Vector2 P(float x, float y) => new Vector2(c.X + x * s, c.Y + y * s);
            switch (icon)
            {
                case Icon.Grid:
                {
                    float g = 0.12f, r = 0.20f;
                    for (int i = 0; i < 4; i++)
                    {
                        float dx = (i % 2 == 0) ? -1 : 1;
                        float dy = (i / 2 == 0) ? -1 : 1;
                        var a = P(dx * g - (dx < 0 ? 0 : r), dy * g - (dy < 0 ? 0 : r));
                        var b = P(dx * (g + r) + (dx < 0 ? 0 : 0), dy * (g + r));
                        var cc = new Vector2(dx < 0 ? a.X : b.X, dy < 0 ? a.Y : b.Y);
                        var dd = new Vector2(dx < 0 ? b.X : a.X, dy < 0 ? b.Y : a.Y);
                        dl.AddRectFilled(Vector2.Min(cc, dd), Vector2.Max(cc, dd), u, 2f);
                    }
                    break;
                }
                case Icon.Diamond:
                {
                    var pts = new[] { P(0, -0.72f), P(0.72f, 0), P(0, 0.72f), P(-0.72f, 0) };
                    for (int i = 0; i < 4; i++) dl.AddLine(pts[i], pts[(i + 1) % 4], u, th);
                    break;
                }
                case Icon.Cloud:
                {
                    dl.PathArcTo(P(-0.25f, 0.30f), 0.40f, MathF.PI * 0.55f, MathF.PI * 1.95f, 14);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    dl.PathArcTo(P(0.12f, 0.12f), 0.44f, MathF.PI * 1.05f, MathF.PI * 2.05f, 14);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    dl.PathArcTo(P(0.42f, 0.34f), 0.30f, MathF.PI * 1.45f, MathF.PI * 2.45f, 12);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    dl.AddLine(P(-0.62f, 0.44f), P(0.66f, 0.44f), u, th);
                    break;
                }
                case Icon.Bell:
                {
                    dl.PathArcTo(P(0, 0.10f), 0.50f, MathF.PI, MathF.PI * 2f, 16);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    dl.AddLine(P(-0.50f, 0.10f), P(-0.60f, 0.55f), u, th);
                    dl.AddLine(P(0.50f, 0.10f), P(0.60f, 0.55f), u, th);
                    dl.AddLine(P(-0.60f, 0.55f), P(0.60f, 0.55f), u, th);
                    dl.PathArcTo(P(0, 0.60f), 0.16f, 0f, MathF.PI, 8);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    break;
                }
                case Icon.Clipboard:
                {
                    dl.AddRect(P(-0.52f, -0.62f), P(0.52f, 0.70f), u, 3f, ImDrawFlags.RoundCornersAll, th);
                    dl.AddRectFilled(P(-0.26f, -0.80f), P(0.26f, -0.46f), u, 2f);
                    break;
                }
                case Icon.CheckBadge:
                {
                    dl.AddCircleFilled(c, s * 0.86f, u, 28);
                    dl.AddLine(P(-0.36f, 0.02f), P(-0.08f, 0.30f), Palette.U32(new Vector4(0.08f, 0.09f, 0.08f, 1f)), th + 0.4f);
                    dl.AddLine(P(-0.08f, 0.30f), P(0.38f, -0.26f), Palette.U32(new Vector4(0.08f, 0.09f, 0.08f, 1f)), th + 0.4f);
                    break;
                }
                case Icon.Gear:
                {
                    dl.AddCircle(c, s * 0.44f, u, 24, th);
                    for (int i = 0; i < 8; i++)
                    {
                        float a0 = i * MathF.PI / 4f;
                        var d = new Vector2(MathF.Cos(a0), MathF.Sin(a0));
                        dl.AddLine(c + d * s * 0.62f, c + d * s * 0.92f, u, th);
                    }
                    break;
                }
                case Icon.Chevrons:
                {
                    // two stacked chevrons (the "collapse/open" affordance in the rail)
                    for (int k = 0; k < 2; k++)
                    {
                        float dy = (k == 0 ? -0.22f : 0.22f);
                        dl.AddLine(P(-0.55f, dy - 0.18f), P(0f, dy + 0.18f), u, th);
                        dl.AddLine(P(0f, dy + 0.18f), P(0.55f, dy - 0.18f), u, th);
                    }
                    break;
                }
                case Icon.Folder:
                {
                    dl.AddLine(P(-0.75f, -0.35f), P(-0.2f, -0.35f), u, th);
                    dl.AddLine(P(-0.2f, -0.35f), P(-0.05f, -0.62f), u, th);
                    dl.AddLine(P(-0.05f, -0.62f), P(0.75f, -0.62f), u, th);
                    dl.AddRect(P(-0.75f, -0.35f), P(0.75f, 0.62f), u, s * 0.14f, ImDrawFlags.RoundCornersAll, th);
                    break;
                }
                case Icon.Chat:
                {
                    dl.PathArcTo(P(0, 0.02f), 0.60f, MathF.PI * 0.15f, MathF.PI * 1.85f, 20);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    dl.AddLine(P(-0.30f, 0.50f), P(-0.44f, 0.82f), u, th);
                    dl.AddLine(P(-0.44f, 0.82f), P(0.02f, 0.58f), u, th);
                    break;
                }
                case Icon.Search:
                {
                    dl.AddCircle(P(-0.12f, -0.12f), s * 0.56f, u, 20, th);
                    dl.AddLine(P(0.30f, 0.30f), P(0.62f, 0.62f), u, th);
                    break;
                }
                case Icon.Expand:
                {
                    dl.AddLine(P(-0.62f, 0.06f), P(-0.62f, -0.62f), u, th);
                    dl.AddLine(P(-0.62f, -0.62f), P(0.06f, -0.62f), u, th);
                    dl.AddLine(P(0.62f, -0.06f), P(0.62f, 0.62f), u, th);
                    dl.AddLine(P(0.62f, 0.62f), P(-0.06f, 0.62f), u, th);
                    break;
                }
                case Icon.Minimize:
                    dl.AddLine(P(-0.55f, 0f), P(0.55f, 0f), u, th);
                    break;
                case Icon.Close:
                    dl.AddLine(P(-0.45f, -0.45f), P(0.45f, 0.45f), u, th);
                    dl.AddLine(P(0.45f, -0.45f), P(-0.45f, 0.45f), u, th);
                    break;
                case Icon.Bolt:
                {
                    var pts = new[] { P(0.10f, -0.85f), P(-0.55f, 0.10f), P(-0.05f, 0.10f), P(-0.10f, 0.85f), P(0.55f, -0.12f), P(0.05f, -0.12f) };
                    dl.AddConvexPolyFilled(ref pts[0], 6, u);
                    break;
                }
                case Icon.ArrowRight:
                {
                    dl.AddLine(P(-0.5f, 0f), P(0.5f, 0f), u, th);
                    dl.AddLine(P(0.1f, -0.42f), P(0.52f, 0f), u, th);
                    dl.AddLine(P(0.1f, 0.42f), P(0.52f, 0f), u, th);
                    break;
                }
                case Icon.ArrowLeft:
                {
                    dl.AddLine(P(0.5f, 0f), P(-0.5f, 0f), u, th);
                    dl.AddLine(P(-0.1f, -0.42f), P(-0.52f, 0f), u, th);
                    dl.AddLine(P(-0.1f, 0.42f), P(-0.52f, 0f), u, th);
                    break;
                }
                case Icon.Monitor:
                {
                    dl.AddRect(P(-0.72f, -0.58f), P(0.72f, 0.34f), u, 3f, ImDrawFlags.RoundCornersAll, th);
                    dl.AddLine(P(-0.26f, 0.70f), P(0.26f, 0.70f), u, th);
                    dl.AddLine(P(0f, 0.36f), P(0f, 0.68f), u, th);
                    break;
                }
                case Icon.Refresh:
                {
                    dl.PathArcTo(c, s * 0.62f, MathF.PI * 0.55f, MathF.PI * 1.95f, 20);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    dl.AddLine(P(0.10f, -0.70f), P(0.55f, -0.50f), u, th);
                    dl.AddLine(P(0.10f, -0.70f), P(0.28f, -0.26f), u, th);
                    break;
                }
                case Icon.Plus:
                    dl.AddLine(P(-0.5f, 0f), P(0.5f, 0f), u, th);
                    dl.AddLine(P(0f, -0.5f), P(0f, 0.5f), u, th);
                    break;
                case Icon.Minus:
                    dl.AddLine(P(-0.5f, 0f), P(0.5f, 0f), u, th);
                    break;
                case Icon.Download:
                {
                    dl.AddLine(P(0f, -0.6f), P(0f, 0.25f), u, th);
                    dl.AddLine(P(-0.34f, -0.06f), P(0f, 0.28f), u, th);
                    dl.AddLine(P(0.34f, -0.06f), P(0f, 0.28f), u, th);
                    dl.AddLine(P(-0.55f, 0.62f), P(0.55f, 0.62f), u, th);
                    break;
                }
                case Icon.External:
                {
                    dl.AddLine(P(-0.35f, 0.35f), P(0.45f, -0.45f), u, th);
                    dl.AddLine(P(-0.10f, -0.45f), P(0.45f, -0.45f), u, th);
                    dl.AddLine(P(0.45f, -0.45f), P(0.45f, 0.12f), u, th);
                    break;
                }
                case Icon.Cart:
                {
                    dl.AddRect(P(-0.6f, -0.35f), P(0.6f, 0.42f), u, 3f, ImDrawFlags.RoundCornersAll, th);
                    dl.AddLine(P(-0.6f, -0.02f), P(0.6f, -0.02f), u, th * 0.8f);
                    break;
                }
                case Icon.Shield:
                {
                    dl.PathArcTo(P(0, -0.15f), 0.60f, MathF.PI * 0.10f, MathF.PI * 0.90f, 14);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    dl.AddLine(P(-0.55f, 0.10f), P(0f, 0.78f), u, th);
                    dl.AddLine(P(0.55f, 0.10f), P(0f, 0.78f), u, th);
                    break;
                }
                case Icon.User:
                {
                    dl.AddCircle(P(0, -0.35f), s * 0.40f, u, 20, th);
                    dl.PathArcTo(P(0, 0.62f), 0.62f, MathF.PI, MathF.PI * 2f, 18);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    break;
                }
                case Icon.Dot:
                    dl.AddCircleFilled(c, s * 0.30f, u, 12);
                    break;
                case Icon.Target:
                {
                    dl.AddCircle(c, s * 0.62f, u, 22, th);
                    dl.AddCircleFilled(c, s * 0.14f, u, 10);
                    break;
                }
                case Icon.Sliders:
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float y = -0.55f + i * 0.55f;
                        dl.AddLine(P(-0.62f, y), P(0.62f, y), u, th * 0.85f);
                        dl.AddCircleFilled(P(i == 1 ? 0.25f : -0.25f, y), s * 0.20f, u, 12);
                    }
                    break;
                }
                case Icon.Crosshair:
                {
                    dl.AddCircle(c, s * 0.50f, u, 20, th);
                    dl.AddLine(P(-0.75f, 0), P(-0.30f, 0), u, th);
                    dl.AddLine(P(0.30f, 0), P(0.75f, 0), u, th);
                    dl.AddLine(P(0, -0.75f), P(0, -0.30f), u, th);
                    dl.AddLine(P(0, 0.30f), P(0, 0.75f), u, th);
                    break;
                }
                case Icon.Keyboard:
                {
                    dl.AddRect(P(-0.72f, -0.44f), P(0.72f, 0.44f), u, 3f, ImDrawFlags.RoundCornersAll, th);
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 5; j++)
                        {
                            var p = P(-0.50f + j * 0.25f, -0.18f + i * 0.18f);
                            dl.AddRectFilled(p, p + new Vector2(s * 0.10f, s * 0.08f), u, 1f);
                        }
                    break;
                }
                case Icon.Lock:
                {
                    dl.AddRect(P(-0.52f, -0.05f), P(0.52f, 0.62f), u, 3f, ImDrawFlags.RoundCornersAll, th);
                    dl.PathArcTo(P(0, -0.10f), 0.30f, MathF.PI, MathF.PI * 2f, 12);
                    dl.PathStroke(u, ImDrawFlags.None, th);
                    break;
                }
            }
        }

        // -----------------------------------------------------------------------------------------
        //  hit-testing / interaction helpers
        // -----------------------------------------------------------------------------------------
        /// <summary>
        /// Hit test. The recorded reference drives all input from the OS cursor, so we simply mirror it;
        /// when Rec is attached (headless preview, no input backend) a mouse at -FLT_MAX is ignored.
        /// </summary>
        public static bool Over(Vector2 a, Vector2 b)
        {
            var m = ImGui.GetMousePos();
            return m.X >= a.X && m.Y >= a.Y && m.X <= b.X && m.Y <= b.Y;
        }

        /// <summary>Invisible hit box that returns the same animation triple as real widgets.</summary>
        public static bool Hit(string id, Vector2 a, Vector2 b, out float hov, out float press)
        {
            bool over = Over(a, b);
            hov = Hover(id, over);
            bool down = over && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            press = Press(id + "#p", down);
            ImGui.SetCursorScreenPos(a);
            ImGui.InvisibleButton(id, Vector2.Max(b - a, new Vector2(1, 1)));
            if (over && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) { ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); }
            return over && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && ImGui.IsItemHovered();
        }

        /// <summary>click with proper press/release pairing (release must land on the widget)</summary>
        /// <summary>Buttons are inert in preview mode (there is no input backend behind the recorder).</summary>
        public static bool Button(string id, Vector2 a, Vector2 b)
        {
            if (Rec != null) return false;
            ImGui.SetCursorScreenPos(a);
            ImGui.InvisibleButton(id, Vector2.Max(b - a, new Vector2(1, 1)));
            bool clicked = ImGui.IsItemActive() && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && ImGui.IsItemHovered();
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            return clicked;
        }

        // -----------------------------------------------------------------------------------------
        //  composite widgets that appear all over the reference
        // -----------------------------------------------------------------------------------------

        /// <summary>Soft glass panel: tinted fill + 1px light top edge + 1px dark bottom edge.</summary>
        public static void Glass(ImDrawListPtr dl, Vector2 a, Vector2 b, float rounding, Vector4 tint, float alpha = 1f, bool border = true)
        {
            Rect(dl, a, b, Palette.U32(tint, alpha), rounding);
            if (!border) return;
            dl.AddLine(new Vector2(a.X + rounding, a.Y + 0.5f), new Vector2(b.X - rounding, a.Y + 0.5f), Palette.U32(new Vector4(1, 1, 1, 0.10f * alpha)));
            dl.AddLine(new Vector2(a.X + rounding, b.Y - 0.5f), new Vector2(b.X - rounding, b.Y - 0.5f), Palette.U32(new Vector4(0, 0, 0, 0.18f * alpha)));
        }

        /// <summary>Text field pill with placeholder + caret (used by the key input and search).</summary>
        public static void Field(ImDrawListPtr dl, Font f, Vector2 a, Vector2 b, string text, string placeholder, bool focused, float caretT, Vector4 bg)
        {
            Rect(dl, a, b, bg, (b.Y - a.Y) * 0.5f);
            var pad = new Vector2(14, 0);
            var size = f.Measure(placeholder);
            float ty = a.Y + (b.Y - a.Y - size.Y) * 0.5f - 1f;
            if (string.IsNullOrEmpty(text) && !focused)
                Text(dl, f, new Vector2(a.X + pad.X, ty), placeholder, Palette.TextMute, 0.9f);
            else
            {
                Text(dl, f, new Vector2(a.X + pad.X, ty), text, Palette.Text);
                float w = f.Measure(text).X;
                if (focused && (caretT % 1.06f) < 0.53f)
                    dl.AddLine(new Vector2(a.X + pad.X + w + 1, a.Y + 8), new Vector2(a.X + pad.X + w + 1, b.Y - 8), Palette.U32(Palette.Text), 1.2f);
            }
        }

        /// <summary>Small pill button ("Internal" / "External", scope toggles).</summary>
        public static bool Chip(ImDrawListPtr dl, Font f, string id, Vector2 a, Vector2 b, string label, bool on, out float hov)
        {
            bool over = Over(a, b);
            hov = Hover(id, over);
            float sw = Toggle(id + "#s", on, 5.4f);
            Vector4 bg = Palette.Mix(Palette.Pill, Palette.Mix(Palette.PillHover, Palette.PurpleDim, sw), MathF.Max(sw, hov * 0.6f));
            Rect(dl, a, b, Palette.U32(bg), (b.Y - a.Y) * 0.5f);
            if (sw > 0.01f) Outline(dl, a, b, Palette.U32(Palette.Purple, 0.55f * sw), (b.Y - a.Y) * 0.5f, 1f);
            TextC(dl, f, (a + b) * 0.5f, label, Palette.Mix(Palette.TextDim, Palette.Text, MathF.Max(sw, hov)));
            bool clicked = Button(id + "#b", a, b);
            if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            return clicked;
        }

        /// <summary>Yellow primary CTA with bolt icon and pressed-scale.</summary>
        public static bool PrimaryButton(ImDrawListPtr dl, Font f, string id, Vector2 a, Vector2 b, string label, Icon icon = Icon.Bolt, bool small = false)
        {
            bool over = Rec == null && Over(a, b);
            float hov = Hover(id, over);
            float press = Press(id + "#p", over && ImGui.IsMouseDown(ImGuiMouseButton.Left));
            float scale = 1f - 0.02f * press;
            var c = (a + b) * 0.5f;
            var ha = c + (a - c) * scale; var hb = c + (b - c) * scale;
            Vector4 top = Palette.Mix(Palette.Yellow, new Vector4(1f, 0.90f, 0.45f, 1f), hov * 0.55f);
            Vector4 bot = Palette.Mix(Palette.YellowDeep, new Vector4(0.94f, 0.78f, 0.30f, 1f), hov * 0.55f);
            float round = (hb.Y - ha.Y) * 0.5f;
            VGradRounded(dl, ha, hb, top, bot, round);
            // inner top highlight
            dl.AddLine(new Vector2(ha.X + round, ha.Y + 1f), new Vector2(hb.X - round, ha.Y + 1f), Palette.U32(new Vector4(1, 1, 1, 0.35f)), 1f);
            float isz = small ? 11f : 13f;
            var txt = f;
            var tsz = txt.Measure(label);
            float gap = icon == Icon.None ? 0f : isz * 0.55f;
            float total = tsz.X + (icon == Icon.None ? 0 : isz * 1.2f + gap);
            float x = c.X - total * 0.5f;
            if (icon != Icon.None)
            {
                DrawIcon(dl, icon, new Vector2(x + isz * 0.6f, c.Y), isz * 1.2f, Palette.YellowInk, 1.8f);
                x += isz * 1.2f + gap;
            }
            Text(dl, txt, new Vector2(x, c.Y - tsz.Y * 0.5f - 1f), label, Palette.YellowInk);
            bool clicked = Button(id + "#b", a, b);
            return clicked;
        }

        /// <summary>Ghost pill button on dark glass (secondary action).</summary>
        public static bool GhostButton(ImDrawListPtr dl, Font f, string id, Vector2 a, Vector2 b, string label, bool on = false, Icon icon = Icon.None)
        {
            bool over = Over(a, b);
            float hov = Hover(id, over);
            float press = Press(id + "#p", over && ImGui.IsMouseDown(ImGuiMouseButton.Left));
            Vector4 bg = on
                ? Palette.Mix(Palette.Pill, Palette.PillHover, 0.6f + hov * 0.4f)
                : Palette.Mix(Palette.A(Palette.Pill, 0.85f), Palette.PillHover, hov);
            Rect(dl, a, b, Palette.U32(bg, 1f - 0.06f * press), (b.Y - a.Y) * 0.5f);
            if (on) Outline(dl, a, b, Palette.U32(new Vector4(1, 1, 1, 0.10f)), (b.Y - a.Y) * 0.5f, 1f);
            var sz = f.Measure(label);
            var c = (a + b) * 0.5f;
            if (icon != Icon.None)
            {
                float isz = sz.Y * 0.95f;
                DrawIcon(dl, icon, new Vector2(a.X + 14f + isz * 0.5f, c.Y), isz, Palette.Mix(Palette.TextMute, Palette.Text, hov));
                Text(dl, f, new Vector2(a.X + 14f + isz + 10f, c.Y - sz.Y * 0.5f - 1f), label, Palette.Mix(Palette.TextDim, Palette.Text, hov));
            }
            else TextC(dl, f, c + new Vector2(0, -1), label, Palette.Mix(Palette.TextDim, Palette.Text, hov));
            bool clicked = Button(id + "#b", a, b);
            return clicked;
        }

        /// <summary>iOS-style switch, 0.22s colour + 0.16s knob travel (measured off reference).</summary>
        public static bool Switch(ImDrawListPtr dl, string id, Vector2 a, bool on, float w = 34f, float h = 18f)
        {
            bool over = Over(a, a + new Vector2(w, h));
            float hov = Hover(id, over);
            float t = Toggle(id, on, 9f);
            float round = h * 0.5f;
            Vector4 off = new Vector4(0.20f, 0.20f, 0.22f, 1f);
            Vector4 offH = new Vector4(0.26f, 0.26f, 0.29f, 1f);
            Vector4 bg = Palette.Mix(Palette.Mix(off, offH, hov), Palette.Purple, t);
            Rect(dl, a, a + new Vector2(w, h), Palette.U32(bg), round);
            float knob = round - 2f;
            float x = a.X + 2f + (w - 4f - knob * 2f) * t;
            Vector4 kc = Palette.Mix(new Vector4(0.72f, 0.72f, 0.75f, 1f), new Vector4(0.98f, 0.98f, 1f, 1f), t);
            dl.AddCircleFilled(new Vector2(x + knob, a.Y + h * 0.5f), knob, Palette.U32(kc), 24);
            if (on) dl.AddCircleFilled(new Vector2(x + knob, a.Y + h * 0.5f), knob * 0.42f, Palette.U32(new Vector4(0.55f, 0.42f, 0.92f, 1f)), 16);
            bool clicked = Button(id + "#b", a, a + new Vector2(w, h));
            if (clicked) { _toggle[id] = t; }
            return clicked;
        }

        /// <summary>Slider with filled track and glowing knob (settings screens).</summary>
        public static bool Slider(ImDrawListPtr dl, string id, Vector2 a, float width, ref float value)
        {
            var b = a + new Vector2(width, 14f);
            bool over = Over(a - new Vector2(0, 4), b + new Vector2(0, 4));
            float t = Math.Clamp(value, 0f, 1f);
            float cy = a.Y + 7f;
            Rect(dl, new Vector2(a.X, cy - 1.5f), new Vector2(a.X + width, cy + 1.5f), Palette.U32(new Vector4(1, 1, 1, 0.10f)), 2f);
            Rect(dl, new Vector2(a.X, cy - 1.5f), new Vector2(a.X + width * t, cy + 1.5f), Palette.U32(Palette.Purple), 2f);
            float kx = a.X + width * t;
            dl.AddCircleFilled(new Vector2(kx, cy), 7.5f, Palette.U32(new Vector4(0.66f, 0.52f, 0.98f, 1f)), 24);
            dl.AddCircleFilled(new Vector2(kx, cy), 4f, Palette.U32(new Vector4(0.92f, 0.90f, 1f, 1f)), 20);
            if (over && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                float m = ImGui.GetMousePos().X - a.X;
                value = Math.Clamp(m / width, 0f, 1f);
            }
            ImGui.SetCursorScreenPos(a);
            ImGui.InvisibleButton(id, new Vector2(width, 14f));
            if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            return false;
        }

        /// <summary>Monogram "F" logo mark of the app (two stacked slanted bars).</summary>
        public static void LogoF(ImDrawListPtr dl, Vector2 c, float size, Vector4 col)
        {
            uint u = Palette.U32(col);
            float h = size, w = size * 0.52f;
            var p0 = new Vector2(c.X - w * 0.5f, c.Y - h * 0.5f);
            // top bar (leaning right)
            var a0 = p0;
            var a1 = p0 + new Vector2(w * 0.86f, -h * 0.05f);
            var a2 = p0 + new Vector2(w * 0.62f, h * 0.26f);
            var a3 = p0 + new Vector2(-w * 0.10f, h * 0.31f);
            dl.AddQuadFilled(a0, a1, a2, a3, u);
            var b0 = p0 + new Vector2(-w * 0.10f, h * 0.40f);
            var b1 = p0 + new Vector2(w * 0.50f, h * 0.36f);
            var b2 = p0 + new Vector2(w * 0.34f, h * 0.62f);
            var b3 = p0 + new Vector2(-w * 0.16f, h * 0.66f);
            dl.AddQuadFilled(b0, b1, b2, b3, u);
            var c0 = p0 + new Vector2(-w * 0.18f, h * 0.74f);
            var c1 = p0 + new Vector2(w * 0.30f, h * 0.70f);
            var c2 = p0 + new Vector2(w * 0.22f, h * 0.98f);
            var c3 = p0 + new Vector2(-w * 0.22f, h * 1.02f);
            dl.AddQuadFilled(c0, c1, c2, c3, u);
        }
    }
}
