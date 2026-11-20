# Fluensia — C# / ImGui.NET rebuild

A frame-accurate rebuild of the **Fluensia** launcher UI (game cards, skin workshop, account
page, dark settings surface, key-gate login) rendered entirely with **Dear ImGui** in C#.

* `src/Fluensia/` — the app (ImGui.NET 1.91.6.1 + Veldrid 4.9, borderless SDL2 window)
* `src/Fluensia/Core/Fonts.cs` — the type ladder (line heights in design px, Inter `em` derived
  from its vertical metrics so cap heights match the measured reference)
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

The GitHub Actions workflow (`.github/workflows/build.yml`) builds both variants on every push,
runs the GPU-less layout preview for all five screens, checks the measured geometry, uploads the
zips as artifacts and attaches them to a release for tags `v*`.

Status of the last run is visible on the repo's Actions tab; the compiled zips are under
**Actions → the newest `build` run → Artifacts** (`Fluensia-windows.zip`,
`Fluensia-layout-preview.zip`).

## Requirements

* Windows 10/11 x64, .NET 8 SDK (or just the .NET 8 Desktop Runtime for the `--portable`-less variant)
* a GPU with OpenGL 3.3+ or Vulkan — Veldrid picks the backend automatically
* no extra native DLLs: `cimgui` ships inside the ImGui.NET package and is copied by the SDK

## Layout verification without a GPU

The app can mirror its whole widget tree into a software canvas, so a build machine (or CI) can
check the geometry without a display:

```bash
Fluensia.exe --ui-preview out --state games     # writes preview_games.png + .txt (all drawn strings)
                                                # + .layout.txt (every widget in design units)
Fluensia.exe --ui-preview out --state login
Fluensia.exe --ui-preview out --state settings --time 40
```

This is a checker, not a second renderer: it shares `Ui`, `App` and the views with the real path and
draws solid rects, gradients, outlines and proportional text blocks (real Inter advance widths, so
line lengths are honest). `--snapshot <dir>` does the same with the real GPU renderer
(`--state games --frames 3 --time 12.03`) and also dumps `layout.txt`.

The layout dump is machine-checkable against the measurements in `analysis/brief.md`:

```bash
python3 tools/check_layout.py out/preview_games.layout.txt
```

CI runs that on every push, so geometry drift fails the build instead of being spotted by eye.

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
* Compiles clean on Windows and Linux (`dotnet build`, 0 errors) and the headless preview runs for
  every screen in CI; the geometry assertions in `tools/check_layout.py` pass.
* Measured geometry lives in `analysis/brief.md` (**Measurement pass 2** supersedes the first table)
  and in `analysis/timeline.json` → `layout_revised`.
