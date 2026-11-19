using System;
using System.Numerics;
using Fluensia.Core;
using ImGuiNET;

namespace Fluensia.Views
{
    /// <summary>
    /// Key-gate screen: left glass panel (logo, greeting, key field, "Войти", secondary links,
    /// footer) + right side where the game cards peek out of a bright art slide with the same
    /// wallpaper clouds (reference 0–10 s).
    /// </summary>
    public sealed class LoginView
    {
        private readonly App _app;
        public string Key = "";
        public bool FocusKey = true;
        public float CaretT = 0f;
        public float EnterT = 0f;      // 0..1 transition to the main window
        public bool Entered = false;

        private static readonly (string Label, Ui.Icon Icon)[] Links =
        {
            ("Купить ключ", Ui.Icon.Cart),
            ("Поддержка", Ui.Icon.Shield),
            ("Discord", Ui.Icon.Chat),
        };

        public LoginView(App app) { _app = app; }

        public void Draw(ImDrawListPtr dl, RectF content)
        {
            CaretT += Ui.Dt;

            // measured: panel x 123..215, y 66..293 (radius 10); art slide x 218..453
            var panel = new RectF(content.Left - 14f, content.Top + 6f, content.Left + 78f, content.Bottom - 6f);

            // ---------------- right art backdrop: CS2 cover centred, CoD / Valorant peeking at the edges
            var artA = _app.P(panel.Right + 3f, content.Top + 6f);
            var artB = _app.P(content.Right - 4f, content.Bottom - 6f);
            dl.PushClipRect(artA, artB, true);
            Ui.VGrad(dl, artA, artB, new Vector4(0.46f, 0.55f, 0.57f, 0.95f), new Vector4(0.80f, 0.80f, 0.71f, 0.95f));
            for (int i = 0; i < 12; i++)
            {
                float t = i / 12f;
                dl.AddRectFilled(new Vector2(artA.X, artB.Y - (artB.Y - artA.Y) * (0.10f + t * 0.55f)), artB,
                    Palette.U32(new Vector4(0.95f, 0.92f, 0.78f, 0.045f)));
            }
            float cardW = _app.Sc(78f), cardH = _app.Sc(148f);
            float ccx = (artA.X + artB.X) * 0.5f;
            float ccy = artA.Y + (artB.Y - artA.Y) * 0.50f;
            for (int i = -1; i <= 1; i++)
            {
                var tex = i == 0 ? _app.Art.ArtCs2 : (i < 0 ? _app.Art.ArtCod : _app.Art.ArtValorant);
                var ca = new Vector2(ccx + i * (cardW * 0.96f) - cardW * 0.5f, ccy - cardH * 0.5f);
                var cb = ca + new Vector2(cardW, cardH);
                Ui.Rect(dl, ca, cb, Palette.U32(new Vector4(0.06f, 0.06f, 0.07f, 0.92f)), _app.Sc(11f));
                if (tex.Valid)
                {
                    dl.PushClipRect(ca, cb, true);
                    dl.AddImageRounded(tex.Id, ca, cb, new Vector2(0, 0), new Vector2(1, 1),
                        Palette.U32(new Vector4(1, 1, 1, i == 0 ? 1f : 0.95f)), _app.Sc(11f));
                    dl.PopClipRect();
                }
                if (i == 0)
                {
                    var la = new Vector2(ca.X, cb.Y - _app.Sc(20f));
                    Ui.RectC(dl, la, cb, new Vector4(0.05f, 0.05f, 0.06f, 0.93f), 0, 0, _app.Sc(11f), _app.Sc(11f));
                    Ui.Text(dl, _app.F.Small, la + _app.S(6f, 3.5f), "Counter-Strike 2", Palette.A(Palette.Text, 0.95f));
                    Ui.Text(dl, _app.F.Micro, la + _app.S(6f, 10.5f), "Шутер · Работает", Palette.A(Palette.TextMute, 0.72f));
                    Ui.TextR(dl, _app.F.Micro, new Vector2(cb.X - _app.Sc(6f), la.Y + _app.Sc(4f)), "v1.0.0", Palette.A(Palette.Text, 0.5f));
                }
            }
            dl.PopClipRect();

            // ---------------- left panel
            var pa = _app.P(panel.Left, panel.Top);
            var pb = _app.P(panel.Right, panel.Bottom);
            float rad = _app.Sc(10f);
            Ui.Shadow(dl, pa, pb, rad, _app.Sc(10f), 0.30f);
            Ui.VGradRounded(dl, pa, pb, new Vector4(0.13f, 0.18f, 0.18f, 0.97f), new Vector4(0.16f, 0.21f, 0.20f, 0.97f), rad);
            dl.AddRect(pa, pb, Palette.U32(new Vector4(1, 1, 1, 0.06f)), rad, ImDrawFlags.RoundCornersAll, _app.Sc(1f));
            dl.PushClipRect(pa, pb, true);

            // logo: F mark + wordmark (centred, y 74)
            var wordSize = _app.F.H2.Measure("Fluensia");
            float lx = panel.Center.X - (7f + 3f + wordSize.X / _app.Sc(1f)) * 0.5f;
            Ui.LogoF(dl, _app.P(lx + 3.5f, panel.Top + 9f), _app.Sc(7.5f), new Vector4(0.96f, 0.94f, 0.88f, 1f));
            Ui.Text(dl, _app.F.H2, _app.P(lx + 10f, panel.Top + 5.5f), "Fluensia", new Vector4(0.97f, 0.95f, 0.89f, 1f));

            // greeting
            Ui.TextC(dl, _app.F.H1, _app.P(panel.Center.X, panel.Top + 44f), "Привет!", Palette.A(Palette.Text, 0.98f));
            var line1 = "Введи ключ Fluensia — и выбирай,";
            var line2 = "в какую игру загрузить меню.";
            Ui.TextC(dl, _app.F.Body, _app.P(panel.Center.X, panel.Top + 60f), line1, Palette.A(Palette.TextMute, 0.75f));
            Ui.TextC(dl, _app.F.Body, _app.P(panel.Center.X, panel.Top + 70f), line2, Palette.A(Palette.TextMute, 0.75f));

            // key field + "Вставить"
            var fa = _app.P(panel.Left + 10f, panel.Top + 82f);
            var fb = _app.P(panel.Right - 10f, panel.Top + 96f);
            bool overF = Ui.Over(fa, fb);
            if (overF && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) FocusKey = true;
            if (FocusKey)
            {
                // type into the field like the reference does (autoplay fills it with "XXXX-…")
                if (ImGui.GetIO().InputQueueCharacters.Size > 0)
                {
                    var io = ImGui.GetIO();
                    for (int i = 0; i < io.InputQueueCharacters.Size; i++)
                    {
                        ushort ch = io.InputQueueCharacters[i];
                        if (ch >= 32 && ch < 127 && Key.Length < 19) Key += (char)ch;
                    }
                }
                if (ImGui.IsKeyPressed(ImGuiKey.Backspace, true) && Key.Length > 0) Key = Key.Substring(0, Key.Length - 1);
                if (ImGui.IsKeyPressed(ImGuiKey.Enter, false)) { EnterT = 1f; }
            }
            Ui.Rect(dl, fa, fb, Palette.U32(new Vector4(0.05f, 0.07f, 0.07f, 0.75f)), _app.Sc(6f));
            dl.AddRect(fa, fb, Palette.U32(new Vector4(1, 1, 1, FocusKey ? 0.14f : 0.05f)), _app.Sc(6f), ImDrawFlags.RoundCornersAll, _app.Sc(1f));
            string shown = Key.Length == 0 ? "XXXX-XXXX-XXXX-XXXX" : Key;
            Ui.Text(dl, _app.F.Body, fa + _app.S(6f, 4f), shown,
                Palette.A(Palette.Text, Key.Length == 0 ? 0.38f : 0.9f));
            if (FocusKey && (CaretT % 1.06f) < 0.55f)
            {
                float cw = Key.Length == 0 ? 0f : _app.F.Body.Measure(Key).X;
                dl.AddLine(fa + _app.S(7f, 4f) + new Vector2(cw, 0), fa + _app.S(7f, 10f) + new Vector2(cw, 0),
                    Palette.U32(Palette.Text), _app.Sc(1f));
            }
            // paste chip inside the field
            var pst = new Vector2(fb.X - _app.Sc(34f), (fa.Y + fb.Y) * 0.5f);
            bool overPaste = Ui.Over(pst - _app.S(15f, 5f), pst + _app.S(15f, 5f));
            float hovPaste = Ui.Hover("paste", overPaste);
            Ui.TextC(dl, _app.F.Small, pst, "Вставить", Palette.A(Palette.Text, 0.7f + 0.25f * hovPaste));
            if (Ui.Button("pasteb", pst - _app.S(15f, 6f), pst + _app.S(15f, 6f))) Key = "FLU0-7C2A-91DE-B3F4";

            // device binding note
            var note = "Ключ привяжется к этому Mac";
            Ui.DrawIcon(dl, Ui.Icon.Monitor, _app.P(panel.Left + 13f, panel.Top + 104f), _app.Sc(6.5f), Palette.A(Palette.Text, 0.35f), _app.Sc(0.85f));
            Ui.Text(dl, _app.F.Tiny, _app.P(panel.Left + 19f, panel.Top + 101f), note, Palette.A(Palette.Text, 0.38f));

            // "Войти" — disabled (olive) until the key looks valid, as in the reference
            bool valid = Key.Replace("-", "").Length >= 12;
            var ba = _app.P(panel.Left + 10f, panel.Top + 114f);
            var bb = _app.P(panel.Right - 10f, panel.Top + 130f);
            var top = valid ? Palette.Mix(Palette.Yellow, new Vector4(1f, 0.94f, 0.6f, 1f), 0f) : new Vector4(0.55f, 0.47f, 0.24f, 1f);
            var bot = valid ? Palette.YellowDeep : new Vector4(0.47f, 0.40f, 0.20f, 1f);
            Ui.VGradRounded(dl, ba, bb, top, bot, (bb.Y - ba.Y) * 0.5f);
            Ui.TextC(dl, _app.F.Small, (ba + bb) * 0.5f, "Войти", valid ? Palette.YellowInk : new Vector4(0.25f, 0.21f, 0.12f, 0.7f));
            bool overIn = Ui.Over(ba, bb);
            if (overIn) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (valid && Ui.Button("enterb", ba, bb)) EnterT = 1f;

            // divider "или"
            float dy = panel.Top + 141f;
            var l0 = _app.P(panel.Left + 10f, dy);
            var l1 = _app.P(panel.Center.X - 8f, dy);
            var l2 = _app.P(panel.Center.X + 8f, dy);
            var l3 = _app.P(panel.Right - 10f, dy);
            dl.AddLine(l0, l1, Palette.U32(new Vector4(1, 1, 1, 0.10f)), 1f);
            dl.AddLine(l2, l3, Palette.U32(new Vector4(1, 1, 1, 0.10f)), 1f);
            Ui.TextC(dl, _app.F.Tiny, _app.P(panel.Center.X, dy), "или", Palette.A(Palette.Text, 0.35f));

            // secondary links
            for (int i = 0; i < Links.Length; i++)
            {
                var la = _app.P(panel.Left + 10f, dy + 8f + i * 17f);
                var lb = _app.P(panel.Right - 10f, dy + 23f + i * 17f);
                bool over = Ui.Over(la, lb);
                float hov = Ui.Hover("link" + i, over);
                Ui.Rect(dl, la, lb, Palette.U32(new Vector4(1, 1, 1, 0.045f + 0.045f * hov)), _app.Sc(6f));
                Ui.DrawIcon(dl, Links[i].Icon, new Vector2(la.X + _app.Sc(9f), (la.Y + lb.Y) * 0.5f), _app.Sc(7f),
                    Palette.A(Palette.Text, 0.6f + 0.3f * hov), _app.Sc(0.9f));
                Ui.Text(dl, _app.F.Small, new Vector2(la.X + _app.Sc(16f), la.Y + _app.Sc(4f)), Links[i].Label,
                    Palette.A(Palette.Text, 0.80f + 0.15f * hov));
                Ui.DrawIcon(dl, Ui.Icon.External, new Vector2(lb.X - _app.Sc(9f), (la.Y + lb.Y) * 0.5f), _app.Sc(6f),
                    Palette.A(Palette.Text, 0.28f + 0.2f * hov), _app.Sc(0.9f));
                if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            // footer
            Ui.Text(dl, _app.F.Tiny, _app.P(panel.Left + 10f, panel.Bottom - 10f), "Условия", Palette.A(Palette.Text, 0.30f));
            Ui.TextC(dl, _app.F.Tiny, _app.P(panel.Center.X, panel.Bottom - 7f), "v1.0.0", Palette.A(Palette.Text, 0.30f));
            Ui.TextR(dl, _app.F.Tiny, _app.P(panel.Right - 10f, panel.Bottom - 10f), "Приватность", Palette.A(Palette.Text, 0.30f));

            // Enter -> main window (cross-fade handled by the app)
            if (EnterT > 0f)
            {
                EnterT = Math.Min(1f, EnterT + Ui.Dt / 0.35f);
                if (EnterT >= 1f && !Entered)
                {
                    Entered = true;
                    _app.Switch(Screen.Games);
                    EnterT = 0f;
                }
            }
        }
    }
}
