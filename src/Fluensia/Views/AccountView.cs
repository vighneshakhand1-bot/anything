using System;
using System.Numerics;
using Fluensia.Core;
using ImGuiNET;

namespace Fluensia.Views
{
    /// <summary>"Аккаунт" — licence state, remaining days bar, three plans, key/device info rail.</summary>
    public sealed class AccountView
    {
        private readonly App _app;
        public int DaysLeft = 23;
        public float Progress = 0.76f;         // 23 of 30
        public int SelectedPlan = 1;
        public float ToastT = 0f;
        public string ToastText = "Оплата скоро появится";

        public AccountView(App app) { _app = app; }

        private static readonly (string Name, string Price, string Period, string[] Perks)[] Plans =
        {
            ("Неделя", "199 ₽", "7 дней", new[] { "Internal и External", "Все функции меню", "Обновления" }),
            ("Месяц", "499 ₽", "30 дней", new[] { "Всё из «Недели»", "Сброс HWID раз в месяц", "Поддержка без очереди" }),
            ("Навсегда", "1990 ₽", "один раз", new[] { "Всё из «Месяца»", "Доступ к beta-ветке", "Приятная цена" }),
        };

        public void Draw(ImDrawListPtr dl, RectF content)
        {
            _app.DrawTitleRow(dl, content, "Аккаунт");
            dl.PushClipRect(_app.P(content.Left, content.Top), _app.P(content.Right, content.Bottom), true);

            float x0 = content.Left + 8f;
            float railW = 62f;
            float right = content.Right - 8f;
            float mainW = right - railW - x0 - 5f;

            // ---------------- subscription card
            var card = new RectF(x0, content.Top + 26f, x0 + mainW, content.Top + 118f);
            DrawLicenseCard(dl, card);

            // ---------------- plans
            float py = 166f;
            float pw = (mainW - 2 * 4f) / 3f;
            float ph = 292f - py;
            for (int i = 0; i < Plans.Length; i++)
            {
                var r = new RectF(x0 + i * (pw + 4f), py, x0 + i * (pw + 4f) + pw, py + ph);
                DrawPlan(dl, i, r);
            }

            // ---------------- right rail
            var rail = new RectF(right - railW, content.Top + 26f, right, content.Bottom - 6f);
            rail = new RectF(rail.Left, rail.Top, rail.Right, 292f);
            DrawRail(dl, rail);

            // toast (clicking "Купить" shows the reference's "Оплата скоро появится" pill)
            if (ToastT > 0f)
            {
                ToastT = Math.Max(0f, ToastT - Ui.Dt);
                float t = Ease.OutCubic(1f - ToastT / 0.9f);
                var sz = _app.F.Small.Measure(ToastText);
                float w = sz.X + 22f, h = 13f;
                var c = new Vector2(content.Center.X, content.Bottom - 14f + (1f - t) * 4f);
                var a = _app.P(c.X - w * 0.5f, c.Y - h * 0.5f);
                var b = _app.P(c.X + w * 0.5f, c.Y + h * 0.5f);
                Ui.Shadow(dl, a, b, _app.Sc(6.5f), _app.Sc(5f), 0.35f * t);
                Ui.Rect(dl, a, b, Palette.U32(new Vector4(0.06f, 0.06f, 0.07f, 0.92f * t)), _app.Sc(6.5f));
                Ui.Rect(dl, a, b, Palette.U32(Palette.Yellow, t), _app.Sc(6.5f));
                Ui.TextC(dl, _app.F.Small, (a + b) * 0.5f, ToastText, Palette.YellowInk);
            }
            dl.PopClipRect();
        }

        private void DrawLicenseCard(ImDrawListPtr dl, RectF r)
        {
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            float rad = _app.Sc(8f);
            Ui.Shadow(dl, a, b, rad, _app.Sc(6f), 0.22f);
            Ui.VGradRounded(dl, a, b, new Vector4(0.24f, 0.30f, 0.31f, 0.62f), new Vector4(0.30f, 0.34f, 0.34f, 0.52f), rad);
            dl.AddRect(a, b, Palette.U32(new Vector4(1, 1, 1, 0.07f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            Ui.Text(dl, _app.F.H2, _app.P(r.Left + 8f, r.Top + 8f), "Месяц", Palette.A(Palette.Text, 0.92f));
            // status: shield + "Активна до 3 декабря"
            var stTxt = "Активна до 3 декабря";
            var stw = _app.F.Tiny.Measure(stTxt).X;
            Ui.DrawIcon(dl, Ui.Icon.Shield, _app.P(r.Right - 10f - stw - 7f, r.Top + 12f), _app.Sc(7f), Palette.A(Palette.Text, 0.65f), _app.Sc(0.9f));
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Right - 9f - stw, r.Top + 9f), stTxt, Palette.A(Palette.Text, 0.62f));

            // big counter
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 8f, r.Top + 30f), "Осталось", Palette.A(Palette.Text, 0.45f));
            Ui.Text(dl, _app.F.Big, _app.P(r.Left + 8f, r.Top + 36f), DaysLeft.ToString(), Palette.A(Palette.Text, 0.97f));
            float nw = _app.F.Big.Measure(DaysLeft.ToString()).X;
            Ui.Text(dl, _app.F.Body, _app.P(r.Left + 12f + nw, r.Top + 54f), "дня", Palette.A(Palette.Text, 0.55f));

            // "Продлить" button
            var ba = _app.P(r.Right - 56f, r.Top + 44f);
            var bb = _app.P(r.Right - 10f, r.Top + 60f);
            if (Ui.PrimaryButton(dl, _app.F.Small, "renew", ba, bb, "Продлить", Ui.Icon.None)) ToastT = 0.9f;

            // progress bar with ticks
            var pa = _app.P(r.Left + 8f, r.Bottom - 22f);
            var pb = _app.P(r.Right - 8f, r.Bottom - 14f);
            Ui.Rect(dl, pa, pb, Palette.U32(new Vector4(0.10f, 0.12f, 0.12f, 0.55f)), _app.Sc(4f));
            var fill = new Vector2(pa.X + (pb.X - pa.X) * Progress, pb.Y);
            Ui.Rect(dl, pa, fill, Palette.U32(Palette.Yellow), _app.Sc(4f));
            int ticks = 30;
            for (int i = 1; i < ticks; i++)
            {
                float x = pa.X + (pb.X - pa.X) * i / (float)ticks;
                dl.AddLine(new Vector2(x, pa.Y + _app.Sc(1f)), new Vector2(x, pb.Y - _app.Sc(1f)), Palette.U32(new Vector4(0, 0, 0, 0.25f)), 1f);
            }
            Ui.TextR(dl, _app.F.Tiny, new Vector2(pb.X, pa.Y - _app.Sc(8f)), "23 из 30 дней", Palette.A(Palette.Text, 0.55f));
        }

        private void DrawPlan(ImDrawListPtr dl, int idx, RectF r)
        {
            var p = Plans[idx];
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            bool over = Ui.Over(a, b);
            float hov = Ui.Hover("plan" + idx, over);
            bool sel = idx == SelectedPlan;
            float rad = _app.Sc(8f);

            // selected plan is the light olive card in the reference
            var top = sel ? new Vector4(0.42f, 0.40f, 0.28f, 0.85f) : new Vector4(0.20f, 0.24f, 0.24f, 0.72f);
            var bot = sel ? new Vector4(0.36f, 0.34f, 0.24f, 0.80f) : new Vector4(0.26f, 0.30f, 0.29f, 0.66f);
            top = Palette.Mix(top, new Vector4(top.X + 0.05f, top.Y + 0.05f, top.Z + 0.04f, top.W), hov);
            Ui.VGradRounded(dl, a, b, top, bot, rad);
            dl.AddRect(a, b, Palette.U32(new Vector4(1, 1, 1, sel ? 0.12f : 0.06f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            Ui.TextC(dl, _app.F.Tiny, new Vector2((a.X + b.X) * 0.5f, a.Y + _app.Sc(9f)), p.Name, Palette.A(Palette.Text, 0.62f));
            Ui.TextC(dl, _app.F.Price, new Vector2((a.X + b.X) * 0.5f, a.Y + _app.Sc(24f)), p.Price, Palette.A(Palette.Text, 0.97f));
            var pwid = _app.F.Price.Measure(p.Price).X;
            Ui.Text(dl, _app.F.Tiny, new Vector2((a.X + b.X) * 0.5f + pwid * 0.5f + _app.Sc(3f), a.Y + _app.Sc(27f)),
                "/ " + p.Period, Palette.A(Palette.Text, 0.42f));

            float fy = a.Y + _app.Sc(38f);
            foreach (var perk in p.Perks)
            {
                Ui.TextC(dl, _app.F.Tiny, new Vector2((a.X + b.X) * 0.5f, fy), perk, Palette.A(Palette.Text, 0.58f));
                fy += _app.Sc(8f);
            }

            var ba = new Vector2(a.X + _app.Sc(5f), b.Y - _app.Sc(17f));
            var bb = new Vector2(b.X - _app.Sc(5f), b.Y - _app.Sc(5f));
            if (Ui.PrimaryButton(dl, _app.F.Small, "buy" + idx, ba, bb, "Купить", Ui.Icon.None))
            {
                SelectedPlan = idx;
                ToastT = 0.9f;
            }
            if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        private void DrawRail(ImDrawListPtr dl, RectF r)
        {
            var a = _app.P(r.Left, r.Top); var b = _app.P(r.Right, r.Bottom);
            float rad = _app.Sc(8f);
            Ui.VGradRounded(dl, a, b, new Vector4(0.22f, 0.27f, 0.28f, 0.60f), new Vector4(0.28f, 0.32f, 0.32f, 0.50f), rad);
            dl.AddRect(a, b, Palette.U32(new Vector4(1, 1, 1, 0.06f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));

            float y = r.Top + 8f;
            Ui.Text(dl, _app.F.Small, _app.P(r.Left + 5f, y), "Ключ", Palette.A(Palette.Text, 0.88f));
            y += 10f;
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 5f, y), "Никому его не показывай", Palette.A(Palette.Text, 0.40f));
            y += 10f;
            var ka = _app.P(r.Left + 5f, y); var kb = _app.P(r.Right - 5f, y + 11f);
            Ui.Rect(dl, ka, kb, Palette.U32(new Vector4(0.10f, 0.12f, 0.12f, 0.45f)), _app.Sc(4f));
            Ui.Text(dl, _app.F.Tiny, ka + _app.S(4f, 3.5f), "FLU0-••••-••••-MSIA", Palette.A(Palette.Text, 0.62f));
            Ui.DrawIcon(dl, Ui.Icon.Lock, new Vector2(kb.X - _app.Sc(7f), (ka.Y + kb.Y) * 0.5f), _app.Sc(7f), Palette.A(Palette.Text, 0.35f), _app.Sc(0.9f));

            y += 17f;
            Ui.Text(dl, _app.F.Small, _app.P(r.Left + 5f, y), "Устройство", Palette.A(Palette.Text, 0.88f));
            y += 10f;
            Ui.DrawIcon(dl, Ui.Icon.Monitor, _app.P(r.Left + 8f, y + 3f), _app.Sc(7f), Palette.A(Palette.Text, 0.4f), _app.Sc(0.9f));
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 13f, y), "Компьютер", Palette.A(Palette.Text, 0.40f));
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 13f, y + 7f), "MacBook Air — Razer", Palette.A(Palette.Text, 0.82f));
            y += 17f;
            Ui.DrawIcon(dl, Ui.Icon.Diamond, _app.P(r.Left + 8f, y + 3f), _app.Sc(7f), Palette.A(Palette.Text, 0.4f), _app.Sc(0.9f));
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 13f, y), "HWID", Palette.A(Palette.Text, 0.40f));
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 13f, y + 7f), "A7F3-••••-••••-9C0B", Palette.A(Palette.Text, 0.82f));

            y = r.Top + 74f;
            Ui.Text(dl, _app.F.Small, _app.P(r.Left + 5f, y), "Привязка", Palette.A(Palette.Text, 0.88f));
            y += 10f;
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 5f, y), "Ключ работает только на этом", Palette.A(Palette.Text, 0.40f));
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 5f, y + 7f), "компьютере. Сбросить привязку", Palette.A(Palette.Text, 0.40f));
            Ui.Text(dl, _app.F.Tiny, _app.P(r.Left + 5f, y + 14f), "можно раз в месяц.", Palette.A(Palette.Text, 0.40f));

            var ha = _app.P(r.Left + 5f, y + 26f); var hb = _app.P(r.Right - 5f, y + 37f);
            if (Ui.GhostButton(dl, _app.F.Tiny, "resetHwid", ha, hb, "Сбросить привязку"))
            {
                ToastText = "Сброс раз в месяц";
                ToastT = 0.9f;
            }

            // sign out at the very bottom
            var outA = _app.P(r.Left + 5f, r.Bottom - 16f);
            var outB = _app.P(r.Right - 5f, r.Bottom - 5f);
            bool over2 = Ui.Over(outA, outB);
            float hov2 = Ui.Hover("signout", over2);
            if (hov2 > 0.01f) Ui.Rect(dl, outA, outB, Palette.U32(new Vector4(1, 1, 1, 0.06f * hov2)), _app.Sc(4f));
            Ui.DrawIcon(dl, Ui.Icon.ArrowLeft, new Vector2(outA.X + _app.Sc(6f), (outA.Y + outB.Y) * 0.5f), _app.Sc(6f), Palette.A(Palette.Text, 0.55f), _app.Sc(0.9f));
            Ui.Text(dl, _app.F.Tiny, new Vector2(outA.X + _app.Sc(11f), outA.Y + _app.Sc(3.5f)), "Выйти", Palette.A(Palette.Text, 0.55f));
            Ui.TextR(dl, _app.F.Tiny, new Vector2(outB.X - _app.Sc(3f), outA.Y + _app.Sc(3.5f)), "⌘Q", Palette.A(Palette.Text, 0.30f));
            if (Ui.Button("signout#b", outA, outB)) Environment.Exit(0);
        }
    }
}
