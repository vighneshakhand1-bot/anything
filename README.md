# Fluensia — C# / ImGui.NET rebuild

A frame-accurate rebuild of the **Fluensia** launcher UI (game cards, skin workshop, account
page, dark settings surface, key-gate login) rendered entirely with **Dear ImGui** in C#.

* `src/Fluensia/` — the app (ImGui.NET 1.91.6 + Veldrid, borderless SDL2 window)
* `src/Fluensia/analysis/brief.md` — layout / colour / motion analysis of the source clip,
  every value tagged **[OBS]** (measured) or **[EST]** (inferred)
* `src/Fluensia/analysis/timeline.json` — machine-readable timings
* `videoplayback.mp4` — the reference recording (576×360 @ 30 fps)

## Build

Windows (manual, as requested):

```bat
src\Fluensia\build.bat            :: framework-dependent  -> dist\Fluensia\Fluensia.exe
src\Fluensia\build.bat --portable :: single self-contained exe
src\Fluensia\build.bat --debug    :: Debug build
```

or with the .NET 8 SDK directly:

```bash
dotnet publish src/Fluensia/Fluensia.csproj -c Release -r win-x64 --self-contained false -o dist/Fluensia
```

The GitHub Actions workflow (`.github/workflows/build.yml`) builds both variants, uploads the
zips as artifacts on every push and attaches them to a release for tags `v*`.

## Controls

| key / action | effect |
|---|---|
| mouse | hover / click cards, chips, toggles, CTA |
| `1` `2` `3` `4` | jump to Игры / Мастерская / Аккаунт / Настройки (nav rail also works) |
| `M` | flip the settings theme (the teal ↔ dark flip from the recording) |
| `Esc` | back to Игры |
| typing | the login key field accepts text, `Войти` enables at 12 characters |

## Tech notes

* Design space is the clip's own 576×360 px; all geometry is authored there and mapped 1:1 to
  window pixels with integer snapping (`App.P`, `App.Sc`).
* Anti-aliasing is maxed out: antialiased lines/fills/line-textures, curve tessellation tolerance
  0.12, circle max error 0.12, 16× anisotropic clamp sampler, fonts rasterised at
  `design size × 1.38 × DPI × UI scale` with 2×2 oversampling and pixel-snapped glyph origins.
* Art is bundled as `.flut` (palette + deflate index plane, ~2.1 MB for all 10 bitmaps) and
  decoded in-process — no ImageSharp/StbImageSharp dependency.
* All artwork is generated for this rebuild; the original game covers are copyrighted.
