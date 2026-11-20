# brief.md — Fluensia launcher UI (rebuilt in C# + ImGui.NET)

Source clip: `videoplayback.mp4` (committed in the repo root), 576×360, **30 fps native**, 263.9 s.
Everything below is either **[OBS]** (read or measured off frames with `measure.py` /
edge scans) or **[EST]** (inferred, needs a visual A/B against the source).

Design space used by the code: **576×360 units = the clip's own pixel space** (the clip is a 2×
capture of a 288×180 UI; all numbers are in clip pixels so they can be compared 1:1 with frames).
`App.P(x, y)` maps design units → window pixels with integer snapping.

---

## 1. Overview

Fluensia is a game-cheat launcher (CS2 / Valorant / Dota 2 / CoD) with a teal-glass desktop window
over an illustrated wallpaper (dotted world map + pixel clouds). Screens: **Login** (key gate) →
**Игры** (game cards + detail panel) → **Мастерская** (skin shop) → **Аккаунт** (licence /
plans) → **Настройки** (in-game menu settings — the only dark, flat screen).

| # | t (s) | screen / state | notes |
|---|-------|----------------|-------|
| 0 | 0.0 | OBS/editor capture of the raw recording | not part of the UI |
| 1 | 1.0–3.4 | **Login**, empty key field, "Войти" disabled | panel left, CS2 cover art on the right |
| 2 | 3.4–5.0 | Login, key **XXXX-XXXX-XXXX-XXXX** typed | → button turns yellow |
| 3 | 5.0–6.4 | **Игры**, 4 cards, card 1 selected | detail panel = CS2 |
| 4 | 6.4–10.6 | card selection moves CS2 → VAL → Dota | detail panel content swaps |
| 5 | ~11.0–12.6 | Игры → **Мастерская** cross-fade | 8–10 frames of blurry dissolve |
| 6 | 12.6–20 | Мастерская: carousel, chips, 3×2 offers | |
| 7 | 20–28 | **Аккаунт**: "23 дня", 199/499/1990 plans | toast "Оплата скоро появится" |
| 8 | 34–86 | in-game capture, dark settings surface | not a UI rebuild target (game footage) |
| 9 | 86–88 | Настройки (dark) ↔ teal glass theme flip | M key toggles this in our build |
| 10 | 88–263 | in-game capture continues | |

---

## 2. Clip entry

The clip **opens mid-flow** (login screen already fully composed). There is no window
open/scale animation in the recording, so our build opens with the window already at rest; the
only entry motion is the 260 ms content cross-fade between screens. **[OBS]**

---

## 3. Layout & structure (design units, 576×360)

```
window        x 118 → 457      y 60 → 299      radius 12      [OBS] edge scan on frame @11.5 s
  sidebar     x 118 → 137      (19 wide)                      [OBS]
    logo F    centre x 127.5   y 65 → 73                       [OBS]
    nav icons centre x 127.5   y 84, 98, 112, 126, 140, 154    [OBS]
    chat icon y 272  · avatar (yellow dot) y 288               [OBS]
  title row   y 60 → 92  (title baseline 78, caps top 66)      [OBS]
    search    x 375 → 411      y 63 → 79   (pill, ⌘K badge)    [OBS]
    buttons   centres x 420, 432, 443  y 71   (11 px circles)  [OBS]
  content     x 137 → 457
  card row    x 145 → 449      y 86 → 218                       [OBS]
    4 cards   68.5 wide, 9.5 gutter, 132 tall
      art     145 → 218 of card (146 tall)                     [OBS]
      tag pill 9 tall, centered on the top edge                [OBS]
      meta row y ≈ 152 (version · SHORT-code pill · genre)     [OBS]
      title    y ≈ 172  · status y ≈ 181                       [OBS]
      chips    y ≈ 200 → 209.5 (Internal / External)           [OBS]
    footer band (pale checkerboard)  y 201 → 218               [OBS]
  detail     x 145 → 449      y 215 → 292  radius 7            [OBS]
    header   y 218 → 231 (title + v1.0.0 | Сегодня · Работает) [OBS]
    inner    inset 3.5 / 13.5 / 3.5 / 2.5, radius 5            [OBS]
    col 1    x 150 : lede y 231, 3 bullet rows y 242/249/257   [OBS]
    col 2    x 282 : "Подключение" + 2 lines + chips y 267     [OBS]
    col 3    x 372 : "Готово к запуску" + CTA x 375→443 y 266  [OBS]
```

Z-order (back → front): wallpaper → window shadow → window glass → content screen → sidebar →
title row → hover/scroll layers. **[OBS]**

Login screen (frames 1–3): left panel x 122→214, y 66→293 (92 wide, radius 10); key field
x 132→206 y 148→162; "Войти" y 180→196; three link rows y 205/222/239; footer row y 283. **[OBS]**

---

## 4. Visual design — sampled colours

| token | hex | where sampled | tag |
|---|---|---|---|
| wall top | `#6E858C` | (0.05, 0.05) | [OBS] |
| wall bottom haze | `#A1B3B2` → cream `#C6B78F` | (0.05, 0.95) / login bg (0.30, 0.62) | [OBS] |
| window glass | `#7E939A` (≈0.90 α over wall) | (0.50, 0.145) | [OBS] |
| sidebar plate | `#46585B` | (0.215, 0.45) | [OBS] |
| window hairline | `#434F52` | (0.50, 0.128) | [OBS] |
| text primary | `#F3F5F4` | title glyph bodies | [OBS] |
| text dim / mute | `#C3CDCB` / `#9FAEB0` | status lines | [OBS] |
| accent yellow | `#F5D451` (deep `#E9C244`) | launch CTA (0.716, 0.769) | [OBS] |
| accent yellow ink | `#2A2415` | CTA label | [OBS] |
| purple (settings) | `#8A6DE0` | active tab / toggles | [OBS] |
| card footer black | `#131316` → `#0C0C0E` | (0.45, 0.55) | [OBS] |
| chip plate | `#232326` / hover `#33333A` | card scope pills | [EST] |
| detail olive (outer) | `#8E845D` → `#2F221A` | (0.30, 0.66) / (0.27, 0.60) | [OBS] |
| detail inner plate | `rgba(20,18,14,0.55)` | inner card | [EST] |
| settings dark bg | `#0B0B0C` → `#050506` | 88 s frame | [OBS] |
| settings card | `#141416` | switch cards | [OBS] |

Typography: Inter (bundled, 400/500/600/700 as static instances of `Inter[opsz,wght].ttf`, OFL).
The reference has **no display face** — everything is Inter-like neo-grotesque. Sizes in design
units: title 12.5, section 9.5, card name 7.6, body 6.4, meta 5.7, micro 4.7. The whole ladder is
multiplied by one **em scale of 1.38** [EST] because literal 5 px type is sub-pixel at this scale;
all line boxes were positioned from the measured rows above, so the *layout* stays 1:1.

---

## 5. Interactions

* **Card hover** — card lifts ~2 design px and the drop shadow widens; 220 ms ease-out. **[EST]**
* **Card click** — selection moves to the clicked card, the yellow short-code pill and the
  detail panel follow; chained `Ui.Toggle` animation (≈180 ms). **[EST]**
* **Scope chips** (Internal/External) — dark→lighter pill cross-fade, 160 ms. **[EST]**
* **Primary CTA** — 2 % scale-down while pressed, gradient brightens on hover, breathing glow. **[EST]**
* **Switches** — knob travel + colour cross-fade, knob overshoots slightly (spring). **[OBS]** shape,
  **[EST]** timing.
* **Screen switch** — see timeline: ~0.26 s blur/dissolve, content underneath is fully replaced at
  the midpoint (no slide). **[OBS]**
* **Nav rail** — active item gets a 16 %-white rounded plate; hover 7 %. **[EST]**

---

## 6. Animation timeline (measured)

| t (s) | element | property | from → to | duration | easing | trigger | basis |
|---|---|---|---|---|---|---|---|
| 12.03 → 12.30 | content area (Игры → Мастерская) | opacity / glass dissolve | 1 → 0 → 1 | **270 ms** at 30 fps (15–85 % core **167 ms**) | ease-in-out (sigmoid, slow-in + slow-out visible in the value table) | click "Мастерская" | [OBS] `measure.py --metric bright --box 0.28,0.45,0.45,0.85`: 0.339 → 0.528, floor at t=12.03, plateau at 12.30 |
| 86.27 → 86.60 | whole window | theme flip (teal glass → dark settings) | mean luma 11.9 → 19.7 | **330 ms** | ease-out (fast first 200 ms, then settle) | M / settings tab | [OBS] frame-mean table, 30 fps |
| hover (card) | card | translateY | 0 → −2 du | ~220 ms | ease-out | pointer enter | [EST] not measurable from the clip (cursor never rests long enough) |
| press (CTA) | CTA | scale | 1 → 0.98 | 90 ms down / 160 ms up | ease-out | mouse down | [EST] |
| selection | short-code pill | colour | `#0A0A0B` → `#F5D451` | ~180 ms | ease-in-out | card click | [EST] |
| carousel | centre slide | scale + x | 1.0 ↔ 0.72, ±86 du | ~320 ms | spring (stiffness 10) | arrow / drag | [EST] |

`timeline.json` in this folder carries the same data machine-readably.

---

## 7. Implementation notes (this repo)

* **Stack**: C# 12 / .NET 8, `ImGui.NET 1.91.6.1`, `Veldrid 4.9` (OpenGL on Windows/Linux),
  borderless SDL2 window. No image or font toolkits at runtime.
* **Anti-aliasing / sharpness (the "maxed out" part)**
  * `AntiAliasedLines = true`, `AntiAliasedLinesUseTex = true`, `AntiAliasedFill = true`
  * `CurveTessellationTol = 0.12`, `CircleTessellationMaxError = 0.12` (default 1.25 / 0.30)
  * anisotropic (16×) clamp sampler for the whole ImGui pipeline instead of the point sampler
  * fonts rasterised at `size × emScale × DPI × UI scale`, oversample 2/2, glyph origins snapped
    to whole pixels, integer-snapped layout offsets
  * every rounded/hairline primitive drawn in device pixels with per-corner radii, so 1 px
    outlines stay 1 px at any scale.
* **Assets**: 10 bitmaps in the tiny `.png` container (palette + deflate indices, ~2.1 MB total)
  decoded by `Assets.Flut`, plus 4 static Inter weights. All art is generated for this rebuild
  (the original covers/skins are copyrighted); layout, colours and motion are measured.
* **Theme**: colours in `Core/Theme.cs` (`Palette`), curves in `Core/Ease.cs`, all widgets in
  `Core/Ui.cs` (glass panels, chips, switches, icon set drawn as vector paths).
* **Timeline**: `App.Switch()` starts the 260 ms dissolve; `Ui.Dt` drives every spring.

---

## 8. Known gaps / honest notes

* The recording is a 2× downscale of a 1152×720 capture, so sub-pixel type metrics cannot be
  recovered exactly — the type ladder is em-scaled (**[EST]**) while every *box* is measured (**[OBS]**).
* Hover/press timings are **not observable** in the clip (no cursor dwell); they are tasteful
  defaults, tagged **[EST]** everywhere.
* The in-game footage (t≈34–86 s, 200–263 s) is a game capture with the menu overlaid — the dark
  settings surface is rebuilt as a static screen, not as an in-game overlay.
* Skins/avatars in the shop are AI-generated stand-ins with the same crop, radius and label
  geometry as the reference.


## Measurement pass 2 (11.50 s frame, 1152x720 px = 2x design) [OBS]

The first table was read off a 814x576 crop and drifted by 3–8 design px in places. This pass re-measured
the full 30 fps frame with column/row transition scans, so it supersedes the numbers above where they differ.

| element | x (design) | y (design) | notes |
|---|---|---|---|
| window | 116 .. 460 | 60 .. 300 | 344 x 240, radius 12 |
| sidebar rail | 116 .. 136 | 60 .. 300 | icons at x 124 |
| title row | 136 .. 460 | 60 .. 92 | "Игры" baseline ~ y 80, x 143 |
| search pill | 373.5 .. 411.5 | 64 .. 78 | 38 x 14, radius 7 |
| window buttons | centres 420 / 431 / 442 | y 71 | 11 px pitch |
| game cards | 146 .. 446.5 | 88 .. 221.5 | 4 x 66.75 wide, 6.5 gutter |
| card footer | — | 156.5 .. 221.5 | 65 tall (art is 68.5 tall) |
| card chip row | left 148 / 172 | 179 .. 188.5 | "Internal" / "External", 23 x 8.5 |
| detail panel | 147 .. 449 | 212.5 .. 296 | radius 7, olive → cream glass |
| detail inner plate | 149 .. 447 | 224 .. 289.5 | radius 5, dark 0.55 alpha |
| detail CTA | 386 .. 459.5 | 270 .. 284.5 | abuts the panel's right edge, 14.5 tall |
| detail columns | 155 / 280 / 371 | 228 (titles) | lede, Подключение, Готово к запуску |
| nav icons | 124 (centre) | 90, 107, 122, 138, 155 | grid, chevrons, folder, account, gear |
| rail bottom | 124 | 259 (chat), 274 (avatar) | avatar 10 px yellow disc |

Colour corrections from the same frame: window glass is cool grey-blue (`#42565A` @0.55 over the wallpaper),
the rail is darker (`#38494B` @0.6), the detail panel header samples `#92855B` and its body melts into the
wallpaper's cream band `#E9E0B4` — the detail panel is translucent glass, not a solid olive plate.
Card footers sample `#242020`..`#2A1E17` (warm charcoal, 0.96 alpha).

Two earlier guesses were removed: the cream checkerboard band behind the cards does not exist in the
reference (what looked like a strip is the wallpaper's own band behind the translucent detail panel), and
the nav rail has five icons, not six.
