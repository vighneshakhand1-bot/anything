using System;
using System.Collections.Generic;
using System.Numerics;
using Fluensia.Core;
using ImGuiNET;

namespace Fluensia.Views
{
    /// <summary>
    /// "Игры" — the app's home. 4 game cards + the olive detail panel for the selected card.
    /// Geometry measured off the reference (design units, 576x360):
    ///   card row   x 145..449 (4 x 68.5 w, 8.5 gutter), y 86..218 (top art 160, footer 74)
    ///   -> we use 68.5 x 132 inside 145..449 with 9.5 gutters
    ///   detail     x 145..449, y 215..292, footer strip 201..218
    /// </summary>
    public sealed class GameCard
    {
        public string Tag;       // Сегодня / Вчера / 3 дня назад / В разработке
        public string Version;
        public string Short;     // CS2 / VAL / Dota / CoD
        public string Genre;     // Шутер / MOBA
        public string Title;
        public string Status;    // Можно запускать / Запуск после обновления ...
        public string[] Scopes;  // Internal / External
        public Tex Art;
        public string[] Features;
        public string DetailLede;
        public string Connection;
        public string AccentHex;
    }

    public sealed class GamesView
    {
        private readonly App _app;
        public List<GameCard> Cards = new();
        public int Selected = 0;
        public int Scope = 0;                  // 0 internal, 1 external
        public float HoverT;
        public string HoveredId = "";
        public float LaunchGlow = 0f;
        public float PressT = 0f;

        public GamesView(App app)
        {
            _app = app;
        }

        public void Build()
        {
            Cards = new List<GameCard>
            {
                new GameCard
                {
                    Tag = "Сегодня", Version = "1.0.0", Short = "CS2", Genre = "Шутер",
                    Title = "Counter-Strike 2", Status = "Можно запускать",
                    Scopes = new[] { "Internal", "External" }, Art = _app.Art.ArtCs2, AccentHex = "#F09C3C",
                    Features = new[] { "Визуалы и ESP", "Смена скинов в матче", "Конфиги" },
                    DetailLede = "Меню открывается прямо в матче.",
                    Connection = "Меню рисуется прямо\nвнутри игры",
                },
                new GameCard
                {
                    Tag = "Вчера", Version = "0.9.2", Short = "VAL", Genre = "Шутер",
                    Title = "Valorant", Status = "Запуск после обновления",
                    Scopes = new[] { "External" }, Art = _app.Art.ArtValorant, AccentHex = "#D45AA8",
                    Features = new[] { "Визуалы и ESP", "Смена скинов в матче", "Конфиги" },
                    DetailLede = "Меню открывается прямо в матче.",
                    Connection = "Меню рисуется прямо\nвнутри игры",
                },
                new GameCard
                {
                    Tag = "3 дня назад", Version = "0.8.0", Short = "Dota", Genre = "MOBA",
                    Title = "Dota 2", Status = "Можно запускать",
                    Scopes = new[] { "External" }, Art = _app.Art.ArtDota, AccentHex = "#C25733",
                    Features = new[] { "Визуалы и ESP", "Смена скинов в матче", "Конфиги" },
                    DetailLede = "Меню открывается прямо в матче.",
                    Connection = "Меню рисуется прямо\nвнутри игры",
                },
                new GameCard
                {
                    Tag = "В разработке", Version = "—", Short = "CoD", Genre = "Шутер",
                    Title = "Call of Duty", Status = "Запуск пока закрыт",
                    Scopes = new[] { "External" }, Art = _app.Art.ArtCod, AccentHex = "#8E2A2A",
                    Features = new[] { "Визуалы и ESP", "Смена скинов в матче", "Конфиги" },
                    DetailLede = "Меню открывается прямо в матче.",
                    Connection = "Меню рисуется прямо\nвнутри игры",
                },
            };
        }

        public void Draw(ImDrawListPtr dl, RectF content)
        {
            if (Cards.Count == 0) Build();

            // measured on the 11.50 s frame (÷2): cards x 146..446.5, y 88..221.5, footer 156.5..221.5
            float x0 = 146f;
            float cardW = 66.75f;
            float gap = 6.5f;
            float top = 88f;
            float cardH = 133.5f;
            float footerH = 65f;                       // 156.5..221.5
            _ = footerH;

            _app.DrawTitleRow(dl, content, "Игры");
            dl.PushClipRect(_app.P(content.Left, content.Top), _app.P(content.Right, content.Bottom), true);

            for (int i = 0; i < Cards.Count; i++)
            {
                var c = Cards[i];
                float cx = x0 + i * (cardW + gap);
                DrawCard(dl, c, i, new RectF(cx, top, cx + cardW, top + cardH));
            }

            // footer strip (the pale "grid" band the cards sit on)
            // measured: detail panel 147..449 x (aligned with the card row), 212.5..296 y
            DrawDetail(dl, Cards[Selected], new RectF(x0 + 1f, 212.5f, x0 + 4 * cardW + 3 * gap - 1f, 296f));
            dl.PopClipRect();
        }

        private void DrawCard(ImDrawListPtr dl, GameCard c, int idx, RectF r)
        {
            var a = _app.P(r.Left, r.Top);
            var b = _app.P(r.Right, r.Bottom);
            float rad = _app.Sc(9f);
            bool over = Ui.Over(a, b);
            float hov = Ui.Hover("card" + idx, over);
            bool selected = idx == Selected;

            // hover lift: 1.5 design px up + slight scale, 220 ms ease-out (measured)
            float lift = _app.Sc(2.2f) * hov;
            a -= new Vector2(0, lift); b -= new Vector2(0, lift);

            Ui.Shadow(dl, a + new Vector2(0, _app.Sc(1)), b + new Vector2(0, _app.Sc(2)), rad, _app.Sc(8f), 0.34f + 0.1f * hov);

            dl.PushClipRect(a, b, true);

            // ---- art
            var art = c.Art;
            var artH = (_app.S(r.Height - 65f)).Y;
            var artB = new Vector2(b.X, a.Y + artH);
            if (art.Valid)
            {
                // fit-fill crop (cover) of the portrait art
                float ar = art.Width / (float)art.Height;
                float boxAr = (b.X - a.X) / artH;
                Vector2 uv0 = new Vector2(0, 0), uv1 = new Vector2(1, 1);
                if (ar > boxAr) { float f = boxAr / ar; uv0.X = (1 - f) * 0.5f; uv1.X = 1 - uv0.X; }
                else { float f = ar / boxAr; uv0.Y = (1 - f) * 0.5f; uv1.Y = 1 - uv0.Y; }
                dl.AddImage(art.Id, a, artB, uv0, uv1);
            }
            else
            {
                Ui.VGrad(dl, a, artB, Palette.Hex(c.AccentHex), Palette.Hex("#101013"));
            }
            // art vignette to black so the footer melts in
            for (int i = 0; i < 14; i++)
            {
                float t = i / 14f;
                dl.AddRectFilled(new Vector2(a.X, artB.Y - _app.Sc(34f) * (1 - t)),
                                 new Vector2(b.X, artB.Y), Palette.U32(new Vector4(0, 0, 0, 0.055f)));
            }

            // ---- footer (black, rounded bottom corners only)
            var footA = new Vector2(a.X, a.Y + artH);
            Ui.VGradRounded(dl, new Vector2(footA.X, footA.Y - _app.Sc(1.5f)), b, Palette.CardFoot, Palette.CardFoot2, rad);

            // ---- date tag (rounded pill hanging from the top edge)
            var tag = c.Tag;
            var ts = _app.F.Micro.Measure(tag);
            float tw = ts.X + _app.Sc(9f);
            float th = _app.Sc(9f);
            var tagA = new Vector2((a.X + b.X) * 0.5f - tw * 0.5f, a.Y + _app.Sc(3f));
            var tagB = new Vector2(tagA.X + tw, tagA.Y + th);
            Ui.Rect(dl, tagA, tagB, Palette.U32(new Vector4(0.05f, 0.05f, 0.06f, 0.92f)), _app.Sc(6f));
            Ui.TextC(dl, _app.F.Micro, (tagA + tagB) * 0.5f, tag, Palette.A(Palette.Text, 0.92f));

            // ---- stats row: version | short pill | genre
            float sy = footA.Y + _app.Sc(1.5f);
            float rowMidY = sy + _app.Sc(4f);
            Ui.Text(dl, _app.F.Micro, new Vector2(a.X + _app.Sc(4f), rowMidY - _app.Sc(3.5f)), c.Version,
                Palette.A(Palette.Text, 0.72f));

            var pillSize = _app.F.Micro.Measure(c.Short);
            float pw = pillSize.X + _app.Sc(8f);
            float ph = _app.Sc(8f);
            var pa = new Vector2((a.X + b.X) * 0.5f - pw * 0.5f, rowMidY - ph * 0.5f);
            var pb = new Vector2(pa.X + pw, pa.Y + ph);
            bool pillHot = Ui.Over(pa, pb);
            float pillHov = Ui.Hover("pill" + idx, pillHot);
            // active (selected) card gets the yellow pill, others the dark plate
            bool yellow = selected;
            var pillBg = yellow
                ? Palette.Mix(Palette.Yellow, new Vector4(1f, 0.92f, 0.55f, 1f), pillHov * 0.6f)
                : Palette.Mix(Palette.ChipDark, Palette.PillHover, pillHov);
            Ui.Rect(dl, pa, pb, Palette.U32(pillBg, 0.96f), _app.Sc(6f));
            Ui.TextC(dl, _app.F.Micro, (pa + pb) * 0.5f, c.Short,
                yellow ? Palette.YellowInk : Palette.A(Palette.Text, 0.85f));

            Ui.TextR(dl, _app.F.Micro, new Vector2(b.X - _app.Sc(4f), rowMidY - _app.Sc(3f)), c.Genre,
                Palette.A(Palette.Text, 0.66f));

            // ---- title + status
            Ui.TextCFit(dl, _app.F.H3, new Vector2((a.X + b.X) * 0.5f, footA.Y + _app.Sc(18.5f)), c.Title,
                b.X - a.X - _app.Sc(4f), Palette.A(Palette.Text, 0.96f));
            Ui.TextCFit(dl, _app.F.Micro, new Vector2((a.X + b.X) * 0.5f, footA.Y + _app.Sc(26.5f)), c.Status,
                b.X - a.X - _app.Sc(4f), Palette.A(Palette.TextMute, 0.72f));

            // ---- scope chips row
            float chH = _app.Sc(8.5f);
            float cy = b.Y - _app.Sc(4.5f) - chH * 0.5f;
            if (c.Scopes.Length == 2)
            {
                DrawScopeChip(dl, "Internal", new Vector2(a.X + _app.Sc(2.5f), cy), _app.Sc(23f), chH, idx);
                DrawScopeChip(dl, "External", new Vector2(a.X + _app.Sc(27f), cy), _app.Sc(23f), chH, idx);
            }
            else
            {
                DrawScopeChip(dl, "External", new Vector2((a.X + b.X) * 0.5f - _app.Sc(11.5f), cy), _app.Sc(23f), chH, idx);
            }

            // selected ring: 1px warm outline that fades in (the active card reads slightly warmer)
            if (selected)
            {
                dl.AddRect(a, b, Palette.U32(new Vector4(1f, 0.86f, 0.45f, 0.20f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));
            }

            dl.PopClipRect();

            if (Ui.Button("cardbtn" + idx, a, b))
            {
                Selected = idx;
                PressT = 0f;
            }
        }

        private void DrawScopeChip(ImDrawListPtr dl, string label, Vector2 leftMid, float w, float h, int cardIdx)
        {
            var a = new Vector2(leftMid.X, leftMid.Y - h * 0.5f);
            var b = new Vector2(a.X + w, a.Y + h);
            bool over = Ui.Over(a, b);
            float hov = Ui.Hover(label + cardIdx, over);
            bool active = (label == "Internal" && Scope == 0) || (label == "External" && Scope == 1);

            // On the non-selected reference cards only "External" is shown; active state = lighter pill
            var bg = active
                ? new Vector4(0.30f, 0.30f, 0.33f, 0.95f)
                : new Vector4(0.16f, 0.16f, 0.17f, 0.95f);
            bg = Palette.Mix(bg, new Vector4(0.38f, 0.38f, 0.42f, 0.95f), hov);
            Ui.Rect(dl, a, b, Palette.U32(bg), h * 0.5f);
            Ui.TextCFit(dl, _app.F.Micro, (a + b) * 0.5f, label, b.X - a.X - _app.Sc(3f),
                Palette.A(Palette.Text, active ? 0.92f : 0.55f));
            if (Ui.Button(label + "#" + cardIdx, a, b)) Scope = label == "Internal" ? 0 : 1;
        }

        // ---------------------------------------------------------------------------------------
        //  olive detail panel
        // ---------------------------------------------------------------------------------------
        private void DrawDetail(ImDrawListPtr dl, GameCard c, RectF r)
        {
            var a = _app.P(r.Left, r.Top);
            var b = _app.P(r.Right, r.Bottom);
            float rad = _app.Sc(7f);
            // measured inner plate (design): 149..447 x, 224..289.5 y (panel 147..449 / 212.5..296)
            var inner = new RectF(r.Left + 2.5f, r.Top + 11.5f, r.Right - 2.5f, r.Bottom - 6.5f);

            // outer plate: olive/amber gradient glass (sampled #8E845D top-left -> #2F221A bottom-right)
            // sampled: header band #92855B, body melts into the cream wallpaper band #E9E0B4
            var tl = new Vector4(0.573f, 0.522f, 0.357f, 0.62f);
            var br = new Vector4(0.914f, 0.878f, 0.706f, 0.30f);
            dl.PushClipRect(a, b, true);
            Ui.DiagGrad(dl, a, b, tl, br, rad);
            // warm bloom bottom-left (wallpaper light leaking through the glass)
            for (int i = 0; i < 12; i++)
            {
                float t = i / 12f;
                dl.AddRectFilled(new Vector2(a.X, b.Y - (b.Y - a.Y) * (0.15f + t * 0.85f)), b,
                    Palette.U32(new Vector4(0.85f, 0.80f, 0.58f, 0.012f)));
            }
            dl.PopClipRect();
            dl.AddRect(a, b, Palette.U32(new Vector4(1f, 0.95f, 0.80f, 0.10f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            // header row
            float hty = r.Top + 6f;
            Ui.Text(dl, _app.F.Small, _app.P(r.Left + 5.5f, hty), c.Title, Palette.A(Palette.Text, 0.98f));
            float tw = _app.F.Small.Measure(c.Title).X;
            Ui.Text(dl, _app.F.Micro, _app.P(r.Left + 8f + tw, hty + 2f), "v" + c.Version, Palette.A(Palette.Text, 0.45f));
            Ui.TextR(dl, _app.F.Micro, _app.P(r.Right - 8f, hty + 2f), "Работает", Palette.A(Palette.Text, 0.62f));
            Ui.TextR(dl, _app.F.Micro, _app.P(r.Right - 30f, hty + 2f), c.Tag, Palette.A(Palette.Text, 0.42f));

            // inner dark plate
            var ia = _app.P(inner.Left, inner.Top);
            var ib = _app.P(inner.Right, inner.Bottom);
            float ir = _app.Sc(5f);
            Ui.VGradRounded(dl, ia, ib, new Vector4(0.11f, 0.10f, 0.08f, 0.55f), new Vector4(0.14f, 0.13f, 0.10f, 0.42f), ir);
            dl.AddRect(ia, ib, Palette.U32(new Vector4(1f, 0.95f, 0.75f, 0.06f)), ir, ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            // column 1 — lede + feature list
            float colX = inner.Left + 3.5f;
            Ui.Text(dl, _app.F.Small, _app.P(colX, inner.Top + 3f), c.DetailLede, Palette.A(Palette.Text, 0.88f));
            for (int i = 0; i < c.Features.Length; i++)
            {
                float fy = inner.Top + 9f + i * 8f;
                var bc = _app.P(colX + 2.5f, fy + 1.5f);
                dl.AddCircle(bc, _app.Sc(2.1f), Palette.U32(new Vector4(1f, 1f, 1f, 0.50f)), 24, _app.Sc(0.6f));
                dl.AddLine(bc + new Vector2(-_app.Sc(0.8f), 0), bc + new Vector2(_app.Sc(0.9f), 0), Palette.U32(new Vector4(1, 1, 1, 0.5f)), _app.Sc(0.6f));
                dl.AddLine(bc + new Vector2(0, -_app.Sc(0.8f)), bc + new Vector2(0, _app.Sc(0.9f)), Palette.U32(new Vector4(1, 1, 1, 0.5f)), _app.Sc(0.6f));
                Ui.Text(dl, _app.F.Small, _app.P(colX + 6f, fy), c.Features[i], Palette.A(Palette.Text, 0.80f));
            }

            // column 2 — Подключение (title x 285, body x 285 y 240, chips y 267..276)
            float midX = r.Left + 133f;
            Ui.Text(dl, _app.F.Small, _app.P(midX, inner.Top + 4f), "Подключение", Palette.A(Palette.Text, 0.92f));
            Ui.Text(dl, _app.F.Micro, _app.P(midX, inner.Top + 12f), "Меню рисуется прямо", Palette.A(Palette.Text, 0.58f));
            Ui.Text(dl, _app.F.Micro, _app.P(midX, inner.Top + 18f), "внутри игры", Palette.A(Palette.Text, 0.58f));
            float chipY = r.Bottom - 26f;
            DrawChip(dl, "Internal2", _app.P(midX, chipY), _app.Sc(30f), _app.Sc(10f), "Internal", Scope == 0);
            DrawChip(dl, "External2", _app.P(midX + _app.Sc(32.5f), chipY), _app.Sc(30f), _app.Sc(10f), "External", Scope == 1);

            // column 3 — Готово к запуску + CTA (title x 375, CTA x 375..443, y 266..280)
            float rightX = r.Left + 224f;
            Ui.Text(dl, _app.F.Small, _app.P(rightX, inner.Top + 4f), "Готово к запуску", Palette.A(Palette.Text, 0.92f));
            Ui.Text(dl, _app.F.Micro, _app.P(rightX, inner.Top + 12f), "Fluensia сама найдёт игру и", Palette.A(Palette.Text, 0.58f));
            Ui.Text(dl, _app.F.Micro, _app.P(rightX, inner.Top + 18f), "подключится", Palette.A(Palette.Text, 0.58f));

            // measured: CTA x 447..459.5, y 254..270 design; its right edge is inset 12 from the panel
            // measured CTA: x 386..459.5, y 270..284.5 (inner plate right edge 447, panel 449)
            var ba = _app.P(380f, r.Bottom - 26f);
            var bb = _app.P(r.Right - 2.5f, r.Bottom - 5.5f);
            LaunchGlow += Ui.Dt;
            float pulse = 0.55f + 0.45f * MathF.Sin(LaunchGlow * 2.2f);
            dl.AddRectFilled(ba - new Vector2(0, _app.Sc(1f)), bb + new Vector2(0, _app.Sc(1.5f)),
                Palette.U32(new Vector4(0.96f, 0.82f, 0.32f, 0.09f * pulse)), _app.Sc(5f));
            if (Ui.PrimaryButton(dl, _app.F.Small, "launch", ba, bb, "Запустить")) LaunchGlow = 0f;
        }

        private void DrawChip(ImDrawListPtr dl, string id, Vector2 leftTop, float w, float h, string label, bool active)
        {
            var a = leftTop; var b = new Vector2(a.X + w, a.Y + h);
            bool over = Ui.Over(a, b);
            float hov = Ui.Hover(id, over);
            var bg = active ? new Vector4(0.32f, 0.32f, 0.35f, 0.92f) : new Vector4(0.18f, 0.18f, 0.19f, 0.75f);
            bg = Palette.Mix(bg, new Vector4(0.40f, 0.40f, 0.44f, 0.92f), hov);
            Ui.Rect(dl, a, b, Palette.U32(bg), h * 0.5f);
            Ui.TextC(dl, _app.F.Micro, (a + b) * 0.5f, label, Palette.A(Palette.Text, active ? 0.92f : 0.52f));
            if (Ui.Button(id + "#b", a, b)) Scope = label == "Internal" ? 0 : 1;
        }

        /// <summary>Text with manual line breaks (the reference strings already contain \n).</summary>
        private void DrawWrapped(ImDrawListPtr dl, Font f, Vector2 pos, string text, Vector4 col, float width)
        {
            var lines = text.Split('\n');
            float lh = f.Size * 1.35f * Program.Scale;
            for (int i = 0; i < lines.Length; i++)
            {
                var tmp = "";
                foreach (var word in lines[i].Split(' '))
                {
                    var next = tmp.Length == 0 ? word : tmp + " " + word;
                    if (f.Measure(next).X * _app.Sc(1f) > width && tmp.Length > 0)
                    {
                        Ui.Text(dl, f, pos, tmp, col);
                        pos.Y += lh;
                        tmp = word;
                    }
                    else tmp = next;
                }
                if (tmp.Length > 0) { Ui.Text(dl, f, pos, tmp, col); pos.Y += lh; }
            }
        }
    }
}
