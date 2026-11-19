using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace Fluensia.Core
{
    /// <summary>High level screens of the app (mirrors the video's nav order).</summary>
    public enum Screen
    {
        Login = 0,
        Games = 1,
        Workshop = 2,
        Account = 3,
        Settings = 4,
    }

    /// <summary>
    /// Root of the immediate-mode tree. Owns global layout (design space -> pixels), the shared
    /// animation clock, the wallpaper/window chrome and screen switching.
    ///
    /// All coordinates below are DESIGN units (576 x 360, the reference recording's space).
    /// Measured geometry — see analysis/brief.md §Layout:
    ///   window      x 118..457, y 60..299   (width 339, height 239), radius 12
    ///   sidebar     x 118..137              (width 19)
    ///   content     x 137..457
    ///   title row   y 66..92                (28 px tall)
    ///   card row    x 145..449, y 86..218   (4 cards of 68.5 x 132, 8.5 gutter)
    ///   detail      x 145..449, y 215..292
    /// </summary>
    public sealed class App
    {
        public Fonts F;
        public readonly Assets Art = new();

        // ---- window box (design space)
        public RectF Win = new RectF(118, 60, 457, 299);
        public const float SidebarW = 19f;
        public const float ContentX = 137f;
        public const float PadX = 8f;

        public RectF Content => new RectF(ContentX, Win.Top, Win.Right, Win.Bottom);

        public Screen Current = Screen.Games;
        public Screen Last = Screen.Games;
        public float SwitchT = 1f;              // 0..1 cross-fade + slide of the content area
        public float Time;                      // global seconds

        public GamesView Games;
        public WorkshopView Workshop;
        public AccountView Account;
        public SettingsView Settings;
        public LoginView Login;

        public bool WindowFocused = true;
        public float ChromeT = 1f;
        public int HoveredNav = -1;

        public void Init(Fonts fonts, bool loadArt = true)
        {
            F = fonts;
            Games = new GamesView(this);
            Workshop = new WorkshopView(this);
            Account = new AccountView(this);
            Settings = new SettingsView(this);
            Login = new LoginView(this);
            if (loadArt) Art.Load(AppContext.BaseDirectory, ControllerAccess);
        }

        /// <summary>Textures are registered on the renderer lazily (the controller exists by then).</summary>
        public Func<Texture, IntPtr> ControllerAccess => tex =>
            Program.Controller.GetOrCreateImGuiBinding(Program.Gd.ResourceFactory, tex);

        public static readonly string[] NavLabels = { "Игры", "Мастерская", "Аккаунт", "Настройки" };

        /// <summary>Headless verification helper: jump straight to a screen (tools/snapshot.sh).</summary>
        public void PlayState(string state, float time)
        {
            switch ((state ?? "").ToLowerInvariant())
            {
                case "login": Current = Screen.Login; Login.Key = ""; break;
                case "login-filled": Current = Screen.Login; Login.Key = "FLU0-7C2A-91DE-B3F4"; break;
                case "workshop": Current = Screen.Workshop; break;
                case "account": Current = Screen.Account; break;
                case "settings": Current = Screen.Settings; break;
                case "settings-light": Current = Screen.Settings; Settings.Dark = false; break;
                default: Current = Screen.Games; break;
            }
            Last = Current;
            SwitchT = 1f;
            Time = time;
        }

        // -----------------------------------------------------------------------------------------
        //  public helpers used by the views
        // -----------------------------------------------------------------------------------------
        public ImDrawListPtr DL => ImGui.GetWindowDrawList();

        /// <summary>design -> screen pixels</summary>
        public Vector2 P(Vector2 design) => new Vector2(design.X * Program.Scale + Program.Offset.X, design.Y * Program.Scale + Program.Offset.Y);
        public Vector2 P(float x, float y) => P(new Vector2(x, y));
        public Vector2 S(float v) => new Vector2(v * Program.Scale, v * Program.Scale);
        public Vector2 S(float x, float y) => new Vector2(x * Program.Scale, y * Program.Scale);
        public float Sc(float v) => v * Program.Scale;

        public void PushDesignClip(RectF r)
        {
            DL.PushClipRect(P(r.Left, r.Top), P(r.Right, r.Bottom), true);
        }
        public void PopClip() => DL.PopClipRect();

        public void Switch(Screen s)
        {
            if (s == Current) { SwitchT = 1f; return; }
            Last = Current;
            Current = s;
            SwitchT = 0f;
            Ui.Reset();          // drop per-widget animation caches so the new screen starts clean
        }

        // -----------------------------------------------------------------------------------------
        //  per-frame entry
        // -----------------------------------------------------------------------------------------
        public void Frame(float dt, bool autoplay)
        {
            Time += dt;
            SwitchT = Math.Min(1f, SwitchT + dt / 0.28f);   // measured: 8 frames @30fps ≈ 0.26 s

            var dl = DL;
            dl.PushClipRect(Vector2.Zero, new Vector2(Program.Window.Width, Program.Window.Height), false);

            // 1. wallpaper — flat wall + illustrated map/clouds, warm haze bottom-left like the ref
            DrawWallpaper(dl);

            // 2. window: glass body, 1px dark hairline, soft drop shadow
            DrawWindowChrome(dl);

            // 3. screens (cross-fade: outgoing screen, glass scrim, incoming screen)
            var content = Content;
            if (SwitchT < 1f && Last != Current && ImGui.GetIO().MouseDelta.LengthSquared() < 0f)
            {
                DrawScreen(dl, Last, content);
                float a = (1f - Ease.InOutCubic(SwitchT)) * 0.92f;
                dl.PushClipRect(P(content.Left, content.Top), P(content.Right, content.Bottom), true);
                dl.AddRectFilled(P(content.Left, content.Top), P(content.Right, content.Bottom),
                    Palette.U32(Palette.A(new Vector4(0.42f, 0.49f, 0.50f, 1f), a)));
                dl.PopClipRect();
            }
            DrawScreen(dl, Current, content);

            // autoplay: replays the recording's own click path when the user is idle
            if (autoplay) UpdateAutoplay(dt);

            // 4. sidebar sits above the content (it owns the app logo + nav + chat + avatar)
            DrawSidebar(dl);

            // 5. global keyboard
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) Switch(Screen.Games);
            if (ImGui.IsKeyPressed(ImGuiKey.M, false)) { Settings.ToggleTheme(); if (Current != Screen.Settings) Switch(Screen.Settings); }
            if (ImGui.IsKeyPressed(ImGuiKey._1, false)) Switch(Screen.Games);
            if (ImGui.IsKeyPressed(ImGuiKey._2, false)) Switch(Screen.Workshop);
            if (ImGui.IsKeyPressed(ImGuiKey._3, false)) Switch(Screen.Account);
            if (ImGui.IsKeyPressed(ImGuiKey._4, false)) Switch(Screen.Settings);
            if (Current == Screen.Login && ImGui.IsKeyPressed(ImGuiKey.Enter, false))
            {
                Login.Key = string.IsNullOrWhiteSpace(Login.Key) ? "FLU0-7C2A-91DE-B3F4" : Login.Key;
                Login.EnterT = 0.001f;
            }

            dl.PopClipRect();
        }

        private void DrawScreen(ImDrawListPtr dl, Screen s, RectF content)
        {
            float a = 1f;
            switch (s)
            {
                case Screen.Login: Login.Draw(dl, content); break;
                case Screen.Games: Games.Draw(dl, content); break;
                case Screen.Workshop: Workshop.Draw(dl, content); break;
                case Screen.Account: Account.Draw(dl, content); break;
                case Screen.Settings: Settings.Draw(dl, content); break;
            }
            _ = a;
        }

        // -----------------------------------------------------------------------------------------
        //  scripted demo (mirrors the reference recording's order of screens)
        // -----------------------------------------------------------------------------------------
        private float _autoT;
        private int _autoStep;
        public bool AutoplayStopped;

        private static readonly (float At, Action<App> Do)[] AutoScript =
        {
            (2.2f, a => { a.Login.Key = "FLU0-7C2A-91DE-B3F4"; }),
            (3.6f, a => a.Switch(Screen.Games)),
            (8.0f, a => a.Games.Selected = 1),
            (10.5f, a => a.Games.Selected = 2),
            (13.0f, a => a.Switch(Screen.Workshop)),
            (20.0f, a => a.Switch(Screen.Account)),
            (28.0f, a => a.Switch(Screen.Settings)),
            (38.0f, a => { a.Settings.Dark = false; }),
            (46.0f, a => { a.Settings.Dark = true; a.Switch(Screen.Games); }),
            (54.0f, a => { a._autoStep = 0; a._autoT = 0f; a.Switch(Screen.Login); }),
        };

        private void UpdateAutoplay(float dt)
        {
            if (AutoplayStopped) return;
            // any real input hands control back to the user
            var io = ImGui.GetIO();
            if (io.MouseDelta.LengthSquared() > 4f || ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                AutoplayStopped = true;
                return;
            }
            _autoT += dt;
            while (_autoStep < AutoScript.Length && _autoT >= AutoScript[_autoStep].At)
            {
                AutoScript[_autoStep].Do(this);
                _autoStep++;
            }
            if (_autoStep >= AutoScript.Length) AutoplayStopped = true;
        }

        // -----------------------------------------------------------------------------------------
        //  wallpaper
        // -----------------------------------------------------------------------------------------
        private void DrawWallpaper(ImDrawListPtr dl)
        {
            var o = Program.Offset; var s = Program.Scale;
            var px0 = o;
            var px1 = o + new Vector2(Program.DesignW * s, Program.DesignH * s);
            // vertical gradient base
            Ui.VGrad(dl, px0, px1, Palette.WallTop, Palette.WallBottom);
            // warm cream haze bottom (sampled #A1B3B2 -> creamy #C6B78F in the recording)
            int bands = 26;
            for (int i = 0; i < bands; i++)
            {
                float t0 = 0.55f + 0.45f * i / bands, t1 = 0.55f + 0.45f * (i + 1) / bands;
                float a = 0.05f + 0.42f * MathF.Pow(i / (float)bands, 1.6f);
                var c = new Vector4(0.86f, 0.82f, 0.66f, a);
                dl.AddRectFilled(new Vector2(px0.X, px0.Y + (px1.Y - px0.Y) * t0),
                                 new Vector2(px1.X, px0.Y + (px1.Y - px0.Y) * t1 + 1), Palette.U32(c));
            }
            if (Art.Wall.Id != IntPtr.Zero)
            {
                // the illustrated map + pixel clouds, screen-blended over the flat gradient
                dl.AddImage(Art.Wall.Id, px0, px1, new Vector2(0, 0), new Vector2(1, 1), Palette.U32(new Vector4(1, 1, 1, 0.90f)));
            }
            // vignette: darker corners top, keeps focus on the window
            for (int i = 0; i < 10; i++)
            {
                float t = i / 10f;
                dl.AddRect(px0, px1, Palette.U32(new Vector4(0.10f, 0.14f, 0.15f, 0.035f * (1 - t))), 0, ImDrawFlags.None, 2f + i * 2f);
            }
        }

        // -----------------------------------------------------------------------------------------
        //  window chrome
        // -----------------------------------------------------------------------------------------
        private void DrawWindowChrome(ImDrawListPtr dl)
        {
            var a = P(Win.Left, Win.Top);
            var b = P(Win.Right, Win.Bottom);
            float r = Sc(12f);

            // drop shadow — the ref has a wide, very soft shadow under the window
            Ui.Shadow(dl, a, b, r, Sc(14f), 0.30f);

            // body: slightly cooler + darker than the wall, translucent so the wallpaper reads through
            var top = Palette.Mix(Palette.Win, new Vector4(0.36f, 0.45f, 0.47f, 1f), 0.34f);
            var bot = Palette.Mix(Palette.Win, new Vector4(0.55f, 0.62f, 0.60f, 1f), 0.22f);
            Ui.VGradRounded(dl, a, b, Palette.A(top, 0.93f), Palette.A(bot, 0.90f), r);

            // hairline: 1px dark outline + 1px inner light rim (glass edge)
            dl.AddRect(a, b, Palette.U32(new Vector4(0.10f, 0.12f, 0.13f, 0.55f)), r, ImDrawFlags.RoundCornersAll, 1f);
            dl.AddRect(a + new Vector2(0.5f), b - new Vector2(0.5f), Palette.U32(new Vector4(1, 1, 1, 0.10f)), r - 0.5f, ImDrawFlags.RoundCornersAll, 1f);

            // top sheen: the reference has a brighter band behind the title row
            var sy0 = P(ContentX, Win.Top);
            var sy1 = P(Win.Right, Win.Top + 32);
            dl.PushClipRect(a, b, true);
            Ui.VGrad(dl, sy0, sy1, new Vector4(1, 1, 1, 0.07f), new Vector4(1, 1, 1, 0f));
            dl.PopClipRect();
        }

        // -----------------------------------------------------------------------------------------
        //  sidebar
        // -----------------------------------------------------------------------------------------
        private static readonly Ui.Icon[] NavIcons = { Ui.Icon.Grid, Ui.Icon.Diamond, Ui.Icon.Cloud, Ui.Icon.Bell, Ui.Icon.Clipboard, Ui.Icon.Gear };

        private void DrawSidebar(ImDrawListPtr dl)
        {
            float x0 = Win.Left, x1 = Win.Left + SidebarW;
            var a = P(x0, Win.Top); var b = P(x1, Win.Bottom);
            dl.PushClipRect(P(x0, Win.Top), P(x1, Win.Bottom), true);
            // sidebar plate: darker than content, with a right-hand divider
            Ui.VGradRounded(dl, a, b, Palette.A(new Vector4(0.30f, 0.37f, 0.39f, 1f), 0.55f),
                                     Palette.A(new Vector4(0.40f, 0.47f, 0.47f, 1f), 0.45f), Sc(12f));
            dl.AddLine(P(x1 - 0.5f, Win.Top + 6), P(x1 - 0.5f, Win.Bottom - 6), Palette.U32(new Vector4(0, 0, 0, 0.16f)), 1f);

            float cx = x0 + SidebarW * 0.5f;
            // logo mark (measured: F mark at y 65..73, x 122..128)
            Ui.LogoF(dl, P(cx, Win.Top + 9f), Sc(9f), new Vector4(1, 1, 1, 0.96f));

            // nav icons (measured: y 84, 98, 112, 126, 140)
            var navY = new[] { 24f, 38f, 52f, 66f, 80f, 94f };
            for (int i = 0; i < navY.Length; i++)
            {
                float y = Win.Top + navY[i];
                var c = P(cx, y);
                var pillA = P(x0 + 2.5f, y - 6f);
                var pillB = P(x1 - 2.5f, y + 6f);
                bool over = Ui.Over(pillA, pillB);
                float hov = Ui.Hover("nav" + i, over);
                bool active = i == 3 /* account */ || (i == 0 && (Current == Screen.Games)) || (i == 1 && Current == Screen.Workshop) || (i == 4 && Current == Screen.Settings);
                float act = Ui.Toggle("navact" + i, active, 6f);
                if (act > 0.01f)
                    Ui.Rect(dl, pillA, pillB, Palette.U32(new Vector4(1, 1, 1, 0.16f * act)), Sc(4f));
                if (hov > 0.01f)
                    Ui.Rect(dl, pillA, pillB, Palette.U32(new Vector4(1, 1, 1, 0.07f * hov * (1 - act))), Sc(4f));
                var col = Palette.Mix(Palette.Mix(new Vector4(0.86f, 0.90f, 0.90f, 1f), new Vector4(1, 1, 1, 1f), act), new Vector4(1, 1, 1, 1f), hov * 0.5f);
                Ui.DrawIcon(dl, NavIcons[i], c, Sc(9.5f), Palette.A(col, 0.72f + 0.28f * MathF.Max(act, hov)), Sc(0.95f));
                if (Ui.Button("navbtn" + i, pillA, pillB))
                {
                    switch (i)
                    {
                        case 0: Switch(Screen.Games); break;
                        case 1: Switch(Screen.Workshop); break;
                        case 3: Switch(Screen.Account); break;
                        case 4: Switch(Screen.Settings); break;
                    }
                }
                if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            // bottom: chat bubble + avatar
            var chatC = P(cx, Win.Bottom - 27f);
            float chatHov = Ui.Hover("chat", Ui.Over(chatC - S(5), chatC + S(5)));
            Ui.DrawIcon(dl, Ui.Icon.Chat, chatC, Sc(9f), Palette.A(Palette.Mix(Palette.TextMute, Palette.Text, chatHov), 0.8f), Sc(0.95f));
            var avC = P(cx, Win.Bottom - 10f);
            dl.AddCircleFilled(avC, Sc(5f), Palette.U32(Palette.Yellow), 40);
            Ui.DrawIcon(dl, Ui.Icon.Dot, avC, Sc(6f), Palette.YellowInk, Sc(1.2f));
            dl.PopClipRect();
        }

        // -----------------------------------------------------------------------------------------
        //  shared: window title row (title + search + window buttons)
        // -----------------------------------------------------------------------------------------
        public string TitleText = "Игры";

        public void DrawTitleRow(ImDrawListPtr dl, RectF content, string title, bool showSearch = true)
        {
            // measured: title baseline ~ y 78, caps top y 66; search pill x 375..411, y 63..79;
            // three window buttons with 14px hit circles centred at x 420 / 432 / 443, y 71
            float cy = content.Top + 11f;
            Ui.Text(dl, F.H2, P(content.Left + PadX + 1f, cy - 6f), title, Palette.A(Palette.Text, 0.97f));

            float bx = content.Right - 3f;
            WindowButton(dl, "wclose", bx - 5f, cy, Ui.Icon.Close, () => Environment.Exit(0));
            WindowButton(dl, "wmin", bx - 13f, cy, Ui.Icon.Minus, () => { try { Program.Window.WindowState = WindowState.Minimized; } catch { } });
            WindowButton(dl, "wexp", bx - 21f, cy, Ui.Icon.Expand, () => { });

            if (!showSearch) return;

            float sw = 36f, sh = 14f;
            float sx = content.Right - 27f - sw;   // measured: pill x 375..411
            var sa = P(sx, cy - sh * 0.5f); var sb = P(sx + sw, cy + sh * 0.5f);
            bool overS = Ui.Over(sa, sb);
            float hovS = Ui.Hover("search", overS);
            Ui.Rect(dl, sa, sb, Palette.U32(Palette.Mix(new Vector4(0.26f, 0.31f, 0.33f, 0.50f), new Vector4(0.30f, 0.35f, 0.37f, 0.70f), hovS)), Sc(sh * 0.5f));
            Ui.DrawIcon(dl, Ui.Icon.Search, P(sx + 5.5f, cy), Sc(6f), Palette.A(Palette.Text, 0.40f + 0.25f * hovS), Sc(0.85f));
            Ui.Text(dl, F.Small, P(sx + 10f, cy - 4.5f), "Поиск", Palette.A(Palette.TextDim, 0.50f + 0.2f * hovS));
            var ka = P(sx + sw - 10.5f, cy - 4f); var kb = P(sx + sw - 3f, cy + 4f);
            Ui.Rect(dl, ka, kb, Palette.U32(new Vector4(0.09f, 0.11f, 0.12f, 0.45f)), Sc(2f));
            Ui.TextC(dl, F.Micro, (ka + kb) * 0.5f + new Vector2(0, -0.5f), "⌘K", Palette.A(Palette.Text, 0.38f));
        }

        /// <summary>14px circular window button with a hover plate (matches the reference chrome).</summary>
        private void WindowButton(ImDrawListPtr dl, string id, float cx, float cy, Ui.Icon icon, Action onClick)
        {
            var a = P(cx - 5.5f, cy - 5.5f); var b = P(cx + 5.5f, cy + 5.5f);
            bool over = Ui.Over(a, b);
            float hov = Ui.Hover(id, over);
            if (hov > 0.01f) Ui.Rect(dl, a, b, Palette.U32(new Vector4(1, 1, 1, 0.10f * hov)), Sc(5.5f));
            Ui.DrawIcon(dl, icon, P(cx, cy), Sc(7.5f), Palette.A(Palette.Text, 0.42f + 0.33f * hov), Sc(0.95f));
            if (Ui.Button(id + "#b", a, b) && onClick != null) onClick();
            if (over) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    /// <summary>Simple design-space float rect.</summary>
    public struct RectF
    {
        public float Left, Top, Right, Bottom;
        public RectF(float l, float t, float r, float b) { Left = l; Top = t; Right = r; Bottom = b; }
        public float Width => Right - Left;
        public float Height => Bottom - Top;
        public Vector2 Center => new Vector2((Left + Right) * 0.5f, (Top + Bottom) * 0.5f);
        public RectF Inset(float dx, float dy) => new RectF(Left + dx, Top + dy, Right - dx, Bottom - dy);
        public RectF Offset(float dx, float dy) => new RectF(Left + dx, Top + dy, Right + dx, Bottom + dy);
    }
}
