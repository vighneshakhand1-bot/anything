using System;
using System.Collections.Generic;
using System.Numerics;
using Fluensia.Core;
using ImGuiNET;

namespace Fluensia.Views
{
    /// <summary>"Мастерская" — curated skins carousel + filter chips + 3x2 offer grid.</summary>
    public sealed class WorkshopView
    {
        private readonly App _app;
        public int GameFilter = 1;            // CS2 preselected in the reference
        public int CatalogFilter = 0;         // Все
        public bool SafeOnly = true;
        public int Carousel = 2;              // centre slide index
        public float CarouselT = 0f;
        public int SelectedOffer = 0;

        private static readonly string[] Games = { "Игры", "CS2", "VAL", "Dota", "CoD" };
        private static readonly string[] Catalog = { "Все", "Коллекция", "Цена", "Изб" };

        private sealed class Offer
        {
            public string Name, Sub, Thumb, Rating, Price;
            public Tex Art;
            public bool Free, Best;
        }

        private readonly List<Offer> _offers = new();

        public WorkshopView(App app) { _app = app; }

        private void Build()
        {
            _offers.Add(new Offer { Name = "Isadora", Sub = "Карта навыка", Thumb = "isadora", Rating = "4.8 тыс.", Price = "149 ₽", Art = _app.Art.AvIsadora });
            _offers.Add(new Offer { Name = "Himto", Sub = "Five-SeveN", Thumb = "himto", Rating = "3.1 тыс.", Price = "Бесплатно", Art = _app.Art.AvHimto, Free = true });
            _offers.Add(new Offer { Name = "Magical Girl", Sub = "Партнёр", Thumb = "magical", Rating = "4.2 тыс.", Price = "199 ₽", Art = _app.Art.AvMagical });
            _offers.Add(new Offer { Name = "Kayn", Sub = "AK-47", Thumb = "kayn", Rating = "4.5 тыс.", Price = "Бесплатно", Art = _app.Art.AvKayn, Free = true });
            _offers.Add(new Offer { Name = "Techno-Cat Girl", Sub = "Карта навыка", Thumb = "catgirl", Rating = "1.8 тыс.", Price = "99 ₽", Art = _app.Art.AvCatgirl, Best = true });
            _offers.Add(new Offer { Name = "Night Dress", Sub = "Коллекция", Thumb = "night", Rating = "2.7 тыс.", Price = "149 ₽", Art = _app.Art.AvMagical });
        }

        public void Draw(ImDrawListPtr dl, RectF content)
        {
            if (_offers.Count == 0) Build();
            _app.DrawTitleRow(dl, content, "Мастерская");
            dl.PushClipRect(_app.P(content.Left, content.Top), _app.P(content.Right, content.Bottom), true);

            float x0 = content.Left + 8f;
            float right = content.Right - 8f;

            // ---------------- carousel block
            var car = new RectF(x0, content.Top + 26f, right, content.Top + 92f);
            DrawCarousel(dl, car);

            // ---------------- filter chips
            float fy = car.Bottom + 7f;
            float fx = x0;
            for (int i = 0; i < Games.Length; i++)
            {
                var sz = _app.F.Small.Measure(Games[i]);
                float w = sz.X + 14f;
                if (DrawFilterChip(dl, "g" + i, new RectF(fx, fy, fx + w, fy + 12f), Games[i], GameFilter == i))
                    GameFilter = i;
                fx += w + 3f;
            }
            fx += 6f;
            for (int i = 0; i < Catalog.Length; i++)
            {
                string label = Catalog[i];
                var sz = _app.F.Small.Measure(label);
                float w = sz.X + (i == 0 ? 22f : 14f);
                if (DrawFilterChip(dl, "c" + i, new RectF(fx, fy, fx + w, fy + 12f), label, CatalogFilter == i, i == 0))
                    CatalogFilter = i;
                fx += w + 3f;
            }

            // safe toggle + count badge on the right
            var safeA = _app.P(right - 84f, fy);
            Ui.Rect(dl, safeA, safeA + _app.S(34f, 12f), Palette.U32(new Vector4(0.30f, 0.33f, 0.33f, 0.35f)), _app.Sc(6f));
            Ui.TextC(dl, _app.F.Tiny, safeA + _app.S(15f, 6f), "Безопасные", Palette.A(Palette.Text, 0.70f));
            var swA = safeA + _app.S(23f, 2f);
            if (Ui.Switch(dl, "safe", swA + new Vector2(0, -_app.Sc(1f)), SafeOnly, _app.Sc(20f), _app.Sc(11f))) SafeOnly = !SafeOnly;

            var cntA = _app.P(right - 44f, fy);
            Ui.Rect(dl, cntA, cntA + _app.S(44f, 12f), Palette.U32(new Vector4(0.28f, 0.30f, 0.31f, 0.55f)), _app.Sc(6f));
            Ui.TextC(dl, _app.F.Tiny, cntA + _app.S(22f, 6f), "12 предложений", Palette.A(Palette.Text, 0.72f));

            // ---------------- offers grid (3 x 2)
            float gy = fy + 18f;
            float cw = (right - x0 - 2 * 5f) / 3f;
            float ch = 34f;
            for (int i = 0; i < _offers.Count && i < 6; i++)
            {
                int col = i % 3, row = i / 3;
                var r = new RectF(x0 + col * (cw + 5f), gy + row * (ch + 5f), x0 + col * (cw + 5f) + cw, gy + row * (ch + 5f) + ch);
                DrawOffer(dl, _offers[i], i, r);
            }
            dl.PopClipRect();
        }

        private void DrawCarousel(ImDrawListPtr dl, RectF r)
        {
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            float rad = _app.Sc(8f);
            Ui.Shadow(dl, a, b, rad, _app.Sc(6f), 0.22f);
            Ui.Rect(dl, a, b, Palette.U32(new Vector4(0.33f, 0.39f, 0.40f, 0.42f)), rad);
            dl.AddRect(a, b, Palette.U32(new Vector4(1, 1, 1, 0.06f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));
            dl.PushClipRect(a, b, true);

            // header: Подборка + hint + arrows
            Ui.Text(dl, _app.F.H2, _app.P(r.Left + 7f, r.Top + 6f), "Подборка", Palette.A(Palette.Text, 0.96f));
            float tw = _app.F.H2.Measure("Подборка").X;
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 8f + tw, r.Top + 9f), "подбор-скинлов", Palette.A(Palette.Text, 0.35f));

            var ra = _app.P(r.Right - 30f, r.Top + 6f);
            var rb = _app.P(r.Right - 18f, r.Top + 18f);
            Ui.Rect(dl, ra, rb, Palette.U32(new Vector4(1, 1, 1, 0.07f)), _app.Sc(6f));
            Ui.DrawIcon(dl, Ui.Icon.ArrowLeft, (ra + rb) * 0.5f, _app.Sc(7f), Palette.A(Palette.Text, 0.55f), _app.Sc(1f));
            if (Ui.Button("carPrev", ra, rb)) Carousel = Math.Max(0, Carousel - 1);
            var na = _app.P(r.Right - 15f, r.Top + 6f);
            var nb = _app.P(r.Right - 3f, r.Top + 18f);
            Ui.Rect(dl, na, nb, Palette.U32(new Vector4(1, 1, 1, 0.07f)), _app.Sc(6f));
            Ui.DrawIcon(dl, Ui.Icon.ArrowRight, (na + nb) * 0.5f, _app.Sc(7f), Palette.A(Palette.Text, 0.55f), _app.Sc(1f));
            if (Ui.Button("carNext", na, nb)) Carousel = Math.Min(4, Carousel + 1);

            // slides: centre 1.0, sides pushed out and dimmed (perspective-ish, measured from ref: centre
            // card is ~1.5x taller and sits 8px above the side cards)
            float slideW = 96f, slideH = 54f;
            float cx = r.Center.X, cy = r.Top + 44f;
            CarouselT = Ease.SpringLerp(CarouselT, Carousel, 10f, 1f, Ui.Dt);
            for (int k = -2; k <= 2; k++)
            {
                float idx = CarouselT + k;
                float d = idx - Carousel;                     // -2..2 relative offset (animated)
                if (MathF.Abs(d) > 2.2f) continue;
                float t = MathF.Abs(d);
                float scale = 1f - 0.28f * MathF.Min(t, 1f);
                float w = slideW * scale * _app.Sc(1f);
                float h = slideH * scale * _app.Sc(1f);
                float offX = d * (slideW * 0.86f) * _app.Sc(1f);
                var sa = new Vector2(cx + offX - w * 0.5f, cy - h * 0.5f);
                var sb = new Vector2(sa.X + w, sa.Y + h);
                float alpha = t < 0.05f ? 1f : Palette.Mix(new Vector4(1, 1, 1, 0.42f), new Vector4(1, 1, 1, 0.92f), 1f - MathF.Min(t, 1f)).W;
                var tex = k == 0 ? _app.Art.ArtCs2 : (k % 2 == 0 ? _app.Art.ArtValorant : _app.Art.ArtDota);
                Ui.Rect(dl, sa, sb, Palette.U32(new Vector4(0.06f, 0.06f, 0.07f, 0.9f * alpha)), _app.Sc(6f));
                if (tex.Valid)
                    dl.AddImage(tex.Id, sa, sb, new Vector2(0, 0), new Vector2(1, 1), Palette.U32(new Vector4(1, 1, 1, alpha)));
                dl.AddRect(sa, sb, Palette.U32(new Vector4(1, 1, 1, 0.08f * alpha)), _app.Sc(6f), ImDrawFlags.RoundCornersAll, _app.Sc(1f));

                if (k == 0)
                {
                    // caption bar on the centre slide
                    var ca = new Vector2(sa.X + _app.Sc(4f), sb.Y - _app.Sc(14f));
                    var cb = new Vector2(sb.X - _app.Sc(4f), sb.Y - _app.Sc(3f));
                    Ui.Rect(dl, ca, cb, Palette.U32(new Vector4(0.04f, 0.04f, 0.05f, 0.78f)), _app.Sc(4f));
                    Ui.Text(dl, _app.F.Small, new Vector2(ca.X + _app.Sc(3f), ca.Y + _app.Sc(2.5f)), "Isadora", Palette.A(Palette.Text, 0.95f));
                    Ui.Text(dl, _app.F.Tiny, new Vector2(ca.X + _app.Sc(3f), ca.Y + _app.Sc(9f)), "Карта навыка · Counter-Strike 2", Palette.A(Palette.TextMute, 0.7f));
                    Ui.TextR(dl, _app.F.Small, new Vector2(cb.X - _app.Sc(3f), ca.Y + _app.Sc(2.5f)), "149 ₽", Palette.A(Palette.Text, 0.9f));
                    Ui.TextR(dl, _app.F.Tiny, new Vector2(cb.X - _app.Sc(3f), ca.Y + _app.Sc(9f)), "Аккаунт", Palette.A(Palette.TextMute, 0.6f));
                }
            }

            dl.PopClipRect();

            // dot indicator row
            int dots = 9;
            float dw = _app.Sc(3.4f);
            float tot = dots * dw + (dots - 1) * _app.Sc(2.6f);
            float dx = cx - tot * 0.5f;
            for (int i = 0; i < dots; i++)
            {
                bool on = Math.Abs(i - Carousel) < 0.5f;
                var p = new Vector2(dx + i * (dw + _app.Sc(2.6f)), r.Bottom - _app.Sc(4f));
                dl.AddRectFilled(p, p + new Vector2(on ? dw * 2.4f : dw, _app.Sc(1.6f)),
                    Palette.U32(new Vector4(1, 1, 1, on ? 0.85f : 0.30f)), _app.Sc(1f));
                if (on) dx += dw * 1.4f;
            }
        }

        private bool DrawFilterChip(ImDrawListPtr dl, string id, RectF r, string label, bool active, bool withIcon = false)
        {
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            bool over = Ui.Over(a, b);
            float hov = Ui.Hover(id, over);
            var bg = active ? new Vector4(0.52f, 0.55f, 0.50f, 0.55f) : new Vector4(0.16f, 0.18f, 0.18f, 0.30f);
            bg = Palette.Mix(bg, new Vector4(0.62f, 0.64f, 0.58f, 0.55f), hov);
            Ui.Rect(dl, a, b, Palette.U32(bg), _app.Sc(6f));
            float tx = a.X + _app.Sc(7f);
            if (withIcon)
            {
                Ui.DrawIcon(dl, Ui.Icon.Grid, new Vector2(a.X + _app.Sc(7f), (a.Y + b.Y) * 0.5f), _app.Sc(7f), Palette.A(Palette.Text, 0.8f), _app.Sc(1f));
                tx = a.X + _app.Sc(15f);
            }
            Ui.Text(dl, _app.F.Small, new Vector2(tx, (a.Y + b.Y) * 0.5f - _app.Sc(5f)), label,
                Palette.A(active ? Palette.Text : Palette.TextDim, active ? 0.95f : 0.72f));
            if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            return Ui.Button(id + "#b", a, b);
        }

        private void DrawOffer(ImDrawListPtr dl, Offer o, int i, RectF r)
        {
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            bool over = Ui.Over(a, b);
            float hov = Ui.Hover("offer" + i, over);
            Ui.Rect(dl, a, b, Palette.U32(Palette.Mix(new Vector4(0.30f, 0.34f, 0.31f, 0.55f), new Vector4(0.36f, 0.40f, 0.36f, 0.62f), hov)), _app.Sc(6f));
            if (i == SelectedOffer)
                dl.AddRect(a, b, Palette.U32(new Vector4(1f, 1f, 1f, 0.16f)), _app.Sc(6f), ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            // thumbnail
            var ta = a + _app.S(4f, 4f);
            var tb = ta + _app.S(26f, 26f);
            if (o.Art.Valid)
            {
                dl.PushClipRect(ta, tb, true);
                dl.AddImage(o.Art.Id, ta, tb, new Vector2(0, 0), new Vector2(1, 1));
                dl.PopClipRect();
            }
            Ui.Outline(dl, ta, tb, Palette.U32(new Vector4(1, 1, 1, 0.10f)), _app.Sc(4f), _app.Sc(1f));

            float tx = ta.X + _app.Sc(31f);
            Ui.Text(dl, _app.F.H3, new Vector2(tx, a.Y + _app.Sc(5f)), o.Name, Palette.A(Palette.Text, 0.94f));
            Ui.Text(dl, _app.F.Tiny, new Vector2(tx, a.Y + _app.Sc(14f)), o.Sub, Palette.A(Palette.TextMute, 0.62f));
            var pa = new Vector2(tx, a.Y + _app.Sc(21f));
            Ui.Rect(dl, pa, pa + _app.S(17f, 7f), Palette.U32(new Vector4(1, 1, 1, 0.10f)), _app.Sc(3f));
            Ui.TextC(dl, _app.F.Tiny, pa + _app.S(8.5f, 3.5f), "Аккаунт", Palette.A(Palette.Text, 0.6f));
            var pb = pa + _app.S(20f, 0f);
            Ui.Rect(dl, pb, pb + _app.S(10f, 7f), Palette.U32(new Vector4(1, 1, 1, 0.10f)), _app.Sc(3f));
            Ui.TextC(dl, _app.F.Tiny, pb + _app.S(5f, 3.5f), "CS2", Palette.A(Palette.Text, 0.6f));

            // bottom row: rating + price / free / best badge
            float by = b.Y - _app.Sc(8f);
            Ui.Text(dl, _app.F.Tiny, new Vector2(a.X + _app.Sc(5f), by - _app.Sc(4f)), o.Rating, Palette.A(Palette.Text, 0.35f));
            if (o.Best)
            {
                var ba = new Vector2(b.X - _app.Sc(42f), by - _app.Sc(5f));
                Ui.Rect(dl, ba, ba + _app.S(20f, 7f), Palette.U32(new Vector4(0.95f, 0.78f, 0.30f, 0.22f)), _app.Sc(3f));
                Ui.TextC(dl, _app.F.Tiny, ba + _app.S(10f, 3.5f), "Выгодно", Palette.A(Palette.Yellow, 0.95f));
            }
            Ui.TextR(dl, _app.F.Small, new Vector2(b.X - _app.Sc(5f), by - _app.Sc(5f)),
                o.Free ? "Бесплатно" : o.Price, Palette.A(Palette.Text, 0.72f));

            if (Ui.Button("offerb" + i, a, b)) SelectedOffer = i;
            if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
