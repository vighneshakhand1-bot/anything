"""Bake Inter advance metrics into src/Fluensia/Core/Metrics.cs (used by the GPU-less preview)."""
from fontTools.ttLib import TTFont

SRC = 'src/Fluensia/Assets/fonts/Inter-Medium.ttf'
OUT = 'src/Fluensia/Core/Metrics.cs'

f = TTFont(SRC)
upm = f['head'].unitsPerEm
hm = f['hmtx']
cmap = f.getBestCmap()
os2 = f['OS/2']
hhea = f['hhea']

chars = [chr(c) for c in range(32, 127)] + [chr(c) for c in range(0x400, 0x460)] + list('—–№«»…×⌘·→•✓')
seen, pairs = set(), []
for ch in chars:
    if ch in seen:
        continue
    seen.add(ch)
    g = cmap.get(ord(ch))
    if g is None:
        continue
    pairs.append((ord(ch), round(hm[g][0] * 1000 / upm)))

codes = ', '.join(str(c) for c, _ in pairs)
advs = ', '.join(str(a) for _, a in pairs)
def line(s):
    parts = [x.strip() for x in s.split(',')]
    out, cur = [], []
    for p in parts:
        cur.append(p)
        if len(cur) == 16:
            out.append(', '.join(cur) + ',')
            cur = []
    if cur:
        out.append(', '.join(cur) + ',')
    return '\n'.join('            ' + o for o in out)

body = f'''using System;
using System.Collections.Generic;

namespace Fluensia.Core
{{
    /// <summary>
    /// Inter metrics (advance widths in per-mille of em, plus vertical metrics) baked from the bundled
    /// Inter-Medium face. Used by the GPU-less layout preview so text runs measure like the real atlas.
    /// Regenerate with:  python3 tools/gen_metrics.py
    /// </summary>
    public static class Metrics
    {{
        public const float XHeight = {os2.sxHeight} / 1000f;
        public const float CapHeight = {os2.sCapHeight} / 1000f;
        public const float Ascender = {hhea.ascent} / 1000f;
        public const float Descender = {hhea.descent} / 1000f;
        public const float LineHeight = {hhea.ascent - hhea.descent + hhea.lineGap} / 1000f;

        private static readonly ushort[] Codepoints =
        {{
{line(codes)}
        }};

        private static readonly ushort[] Advances =
        {{
{line(advs)}
        }};

        private static Dictionary<char, float> _map;

        /// <summary>Advance width of one character in em; unknown characters fall back to 0.6 em.</summary>
        public static float Advance(char c)
        {{
            if (_map == null)
            {{
                _map = new Dictionary<char, float>(Codepoints.Length);
                for (int i = 0; i < Codepoints.Length; i++) _map[(char)Codepoints[i]] = Advances[i] / 1000f;
            }}
            return _map.TryGetValue(c, out var a) ? a : 0.6f;
        }}

        /// <summary>Width of a string in em (no kerning — Inter's UI faces barely need it at this size).</summary>
        public static float Width(string s)
        {{
            float w = 0f;
            if (s != null) foreach (var c in s) w += Advance(c);
            return w;
        }}
    }}
}}
'''
open(OUT, 'w', encoding='utf-8').write(body)
print('wrote', OUT, len(pairs), 'glyphs')
