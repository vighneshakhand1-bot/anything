using System;
using System.Numerics;
using Fluensia.Core;
using ImGuiNET;

namespace Fluensia.Views
{
    /// <summary>
    /// "Настройки" — the dark, flat settings surface (the reference flips from teal glass to a
    /// near-black workspace here, t≈86 s; pressing M plays the same cross-fade).
    /// Layout: left label rail, weapon-class tabs, two columns of switch cards, sticky footer CTA.
    /// </summary>
    public sealed class SettingsView
    {
        private readonly App _app;
        public bool Dark = true;
        public int Tab = 0;
        public int Section = 0;

        public static readonly string[] Tabs = { "Общие", "Пистолеты", "Винтовки", "Снайперы", "ПП", "Дробовики" };
        public static readonly string[] Sections =
        {
            "Общие", "Прицел", "Визуалы", "Игра", "Звук", "Мышь", "Клавиши", "Оверлей", "Конфиги",
        };

        private readonly (string Label, string Hint, bool Value)[] _aim =
        {
            ("Аимбот", "FOV 3.5°, плавность 4", true),
            ("Контроль отдачи", "только вертикаль", true),
            ("Триггер", "задержка 12 мс", false),
        };
        private readonly (string Label, string Hint, bool Value)[] _visuals =
        {
            ("Только видимые", "raycast каждый кадр", true),
            ("Не через дым", "учитывать смок-гранаты", true),
            ("Не когда ослеплён", "флешка = пауза", true),
            ("Не в прижке", "", false),
            ("Только в зуме", "", false),
        };
        private readonly (string Label, string Hint, bool Value)[] _cross =
        {
            ("Бактрек", "показывать трейс", false),
            ("Автопистолет", "по пулу", false),
            ("Быстрый перевод", "на цель", false),
            ("Автовыстрел снайпера", "", false),
        };

        private bool[] _aimV, _visV, _crossV;
        private float _slider1 = 0.62f, _slider2 = 0.42f;

        public SettingsView(App app)
        {
            _app = app;
            _aimV = Array.ConvertAll(_aim, x => x.Value);
            _visV = Array.ConvertAll(_visuals, x => x.Value);
            _crossV = Array.ConvertAll(_cross, x => x.Value);
        }

        public void ToggleTheme() => Dark = !Dark;

        public void Draw(ImDrawListPtr dl, RectF content)
        {
            var a = _app.P(content.Left, content.Top);
            var b = _app.P(content.Right, content.Bottom);

            // everything on this screen stays inside the window body
            dl.PushClipRect(_app.P(_app.Win.Left + App.SidebarW, _app.Win.Top), _app.P(_app.Win.Right, _app.Win.Bottom), true);

            // theme surface: dark workspace (or the teal glass when Dark == false)
            var bgTop = Dark ? new Vector4(0.045f, 0.045f, 0.05f, 0.97f) : new Vector4(0.30f, 0.38f, 0.39f, 0.90f);
            var bgBot = Dark ? new Vector4(0.02f, 0.02f, 0.025f, 0.98f) : new Vector4(0.36f, 0.43f, 0.42f, 0.88f);
            Ui.VGradRounded(dl, a, b, bgTop, bgBot, _app.Sc(12f));

            var fg = Dark ? Palette.Text : new Vector4(0.06f, 0.08f, 0.08f, 1f);
            var fgDim = Palette.A(fg, Dark ? 0.62f : 0.55f);
            var cardBg = Dark ? new Vector4(0.10f, 0.10f, 0.11f, 0.92f) : new Vector4(1f, 1f, 1f, 0.55f);

            // title
            Ui.Text(dl, _app.F.H1, _app.P(content.Left + 9f, content.Top + 11f), "Настройки", Palette.A(fg, 0.96f));
            _app.DrawTitleRow(dl, content, "");

            // ---------------- left label rail
            float rx = content.Left + 9f;
            float ry = content.Top + 34f;
            for (int i = 0; i < Sections.Length; i++)
            {
                var pos = _app.P(rx, ry + i * 13f);
                bool over = Ui.Over(pos - _app.S(0f, 2f), pos + _app.S(56f, 10f));
                float hov = Ui.Hover("sec" + i, over);
                bool act = i == Section;
                if (act) Ui.Rect(dl, pos - _app.S(3f, 2f), pos + _app.S(57f, 10f), Palette.U32(Palette.A(fg, 0.07f)), _app.Sc(4f));
                if (hov > 0.01f && !act) Ui.Rect(dl, pos - _app.S(3f, 2f), pos + _app.S(57f, 10f), Palette.U32(Palette.A(fg, 0.04f * hov)), _app.Sc(4f));
                Ui.Text(dl, _app.F.Body, pos, Sections[i], Palette.A(fg, act ? 0.95f : 0.55f + 0.25f * hov));
                if (Ui.Button("secb" + i, pos - _app.S(3f, 2f), pos + _app.S(57f, 10f))) Section = i;
            }

            // theme switch at the bottom of the rail
            Ui.Text(dl, _app.F.Tiny, _app.P(rx, content.Bottom - 22f), "Тёмная тема", fgDim);
            if (Ui.Switch(dl, "theme", _app.P(rx + 44f, content.Bottom - 23f), Dark, _app.Sc(18f), _app.Sc(10f))) Dark = !Dark;

            // ---------------- main
            float mx = rx + 64f;
            float mright = content.Right - 9f;

            // tab strip
            float tx = mx;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var sz = _app.F.Small.Measure(Tabs[i]);
                float w = sz.X + 20f;
                var ta = _app.P(tx, content.Top + 32f);
                var tb = _app.P(tx + w, content.Top + 45f);
                bool over = Ui.Over(ta, tb);
                float hov = Ui.Hover("tab" + i, over);
                bool act = i == Tab;
                float t = Ui.Toggle("tabact" + i, act, 7f);
                var tbg = Palette.Mix(Palette.A(fg, 0.06f), Palette.Purple, t);
                tbg = Palette.Mix(tbg, Palette.A(fg, 0.10f), hov * (1 - t));
                if (t > 0.01f) Ui.Rect(dl, ta, tb, Palette.U32(Palette.A(tbg, Dark ? 0.95f : 0.85f)), _app.Sc(5f));
                Ui.TextC(dl, _app.F.Small, (ta + tb) * 0.5f, Tabs[i], Palette.A(fg, act ? 0.98f : 0.58f + 0.3f * hov));
                if (Ui.Button("tabb" + i, ta, tb)) Tab = i;
                tx += w + 3f;
            }

            // ---------------- three cards
            float cy0 = content.Top + 52f;
            float colW = (mright - mx - 5f) * 0.5f;

            var aimCard = new RectF(mx, cy0, mx + colW, cy0 + 44f);
            var visCard = new RectF(mx, cy0 + 49f, mx + colW, cy0 + 100f);
            var crossCard = new RectF(mx + colW + 5f, cy0, mright, cy0 + 38f);
            var drawCard = new RectF(mx + colW + 5f, cy0 + 43f, mright, cy0 + 92f);

            DrawCard(dl, aimCard, "Наведение", _aim, _aimV, fg, cardBg, fgDim, 0);
            DrawCard(dl, visCard, "Проверки", _visuals, _visV, fg, cardBg, fgDim, 1);
            DrawCard(dl, crossCard, "Помощь", _cross, _crossV, fg, cardBg, fgDim, 2);

            // "Отрисовка" card with two sliders + a target swatch
            DrawDrawCard(dl, drawCard, fg, cardBg, fgDim);

            // ---------------- footer actions
            var ap = _app.P(mright - 52f, content.Bottom - 17f);
            var aq = _app.P(mright, content.Bottom - 5f);
            if (Ui.PrimaryButton(dl, _app.F.Small, "apply", ap, aq, "Применить", Ui.Icon.CheckBadge)) { }
            var sp = _app.P(mx, content.Bottom - 14f);
            bool overS = Ui.Over(sp, sp + _app.S(52f, 10f));
            float hovS = Ui.Hover("reset", overS);
            Ui.Text(dl, _app.F.Small, sp, "Сбросить настройки", Palette.A(fg, 0.42f + 0.35f * hovS));
            if (Ui.Button("resetb", sp - _app.S(0f, 2f), sp + _app.S(54f, 10f)))
            {
                _aimV = Array.ConvertAll(_aim, x => x.Value);
                _visV = Array.ConvertAll(_visuals, x => x.Value);
                _crossV = Array.ConvertAll(_cross, x => x.Value);
            }
            dl.PopClipRect();
        }

        private void DrawCard(ImDrawListPtr dl, RectF r, string title, (string Label, string Hint, bool Value)[] rows,
            bool[] values, Vector4 fg, Vector4 cardBg, Vector4 fgDim, int group)
        {
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            float rad = _app.Sc(7f);
            Ui.Rect(dl, a, b, Palette.U32(cardBg), rad);
            dl.AddRect(a, b, Palette.U32(Palette.A(fg, 0.05f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            Ui.Text(dl, _app.F.Small, _app.P(r.Left + 6f, r.Top + 5f), title, Palette.A(fg, 0.78f));
            float y = r.Top + 15f;
            for (int i = 0; i < rows.Length; i++)
            {
                float ry = y + i * 10.5f;
                if (ry + 8f > r.Bottom) break;
                // icon bullet
                var ic = _app.P(r.Left + 8f, ry + 4f);
                Ui.DrawIcon(dl, i % 2 == 0 ? Ui.Icon.Target : Ui.Icon.Crosshair, ic, _app.Sc(6.5f), Palette.A(fg, 0.42f), _app.Sc(0.85f));
                Ui.Text(dl, _app.F.Small, _app.P(r.Left + 14f, ry), rows[i].Label, Palette.A(fg, 0.86f));
                if (rows[i].Hint.Length > 0)
                    Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 14f + _app.F.Small.Measure(rows[i].Label).X + 3f, ry + 1.5f),
                        rows[i].Hint, Palette.A(fg, 0.28f));

                var sa = _app.P(r.Right - 22f, ry - 0.5f);
                if (Ui.Switch(dl, $"sw{group}_{i}", sa, values[i], _app.Sc(17f), _app.Sc(9.5f)))
                    values[i] = !values[i];
            }

            // separator lines between rows
            for (int i = 1; i < rows.Length; i++)
            {
                float ry = y + i * 10.5f;
                if (ry + 8f > r.Bottom) break;
                dl.AddLine(_app.P(r.Left + 6f, ry - 2f), _app.P(r.Right - 6f, ry - 2f), Palette.U32(Palette.A(fg, 0.04f)), 1f);
            }
        }

        private void DrawDrawCard(ImDrawListPtr dl, RectF r, Vector4 fg, Vector4 cardBg, Vector4 fgDim)
        {
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            float rad = _app.Sc(7f);
            Ui.Rect(dl, a, b, Palette.U32(cardBg), rad);
            dl.AddRect(a, b, Palette.U32(Palette.A(fg, 0.05f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            Ui.Text(dl, _app.F.Small, _app.P(r.Left + 6f, r.Top + 5f), "Отрисовка", Palette.A(fg, 0.78f));

            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 6f, r.Top + 16f), "Кого сейчас видит аим", Palette.A(fg, 0.80f));
            var swA = _app.P(r.Right - 18f, r.Top + 16f);
            if (Ui.Switch(dl, "who", swA, true, _app.Sc(16f), _app.Sc(9f))) { }

            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 6f, r.Top + 26f), "Подсветка цели", Palette.A(fg, 0.80f));
            var ic = _app.P(r.Right - 10f, r.Top + 30f);
            Ui.DrawIcon(dl, Ui.Icon.Target, ic, _app.Sc(7f), Palette.A(fg, 0.5f), _app.Sc(0.9f));

            // pause-after-kill slider with live value
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 6f, r.Top + 39f), "Пауза после убийства", Palette.A(fg, 0.80f));
            Ui.TextR(dl, _app.F.Tiny, _app.P(r.Right - 6f, r.Top + 39f), $"{(int)(_slider1 * 400)}мс", Palette.A(fg, 0.55f));
            var s1 = _app.P(r.Left + 6f, r.Top + 46f);
            Ui.Slider(dl, "sl1", s1, _app.Sc(r.Width - 12f), ref _slider1);

            // second slider clipped by the card bottom (as in the reference)
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 6f, r.Top + 53f), "Пауза при смене цели", Palette.A(fg, 0.80f));
            Ui.TextR(dl, _app.F.Tiny, _app.P(r.Right - 6f, r.Top + 53f), $"{(int)(_slider2 * 200)}мс", Palette.A(fg, 0.55f));
            dl.PushClipRect(a, b, true);
            var s2 = _app.P(r.Left + 6f, r.Top + 60f);
            Ui.Slider(dl, "sl2", s2, _app.Sc(r.Width - 12f), ref _slider2);
            dl.PopClipRect();

            // group-boxes behind the rows: 1px outlines like the reference's stacked cards
            for (int i = 0; i < 2; i++)
            {
                float y0 = r.Top + (i == 0 ? 13f : 23f);
                dl.AddRect(_app.P(r.Left + 4f, y0), _app.P(r.Right - 4f, y0 + 9f), Palette.U32(Palette.A(fg, 0.035f)), _app.Sc(4f),
                    ImDrawFlags.RoundCornersAll, _app.Sc(1f));
            }
        }
    }
}
