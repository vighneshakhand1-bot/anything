#!/usr/bin/env python3
"""Assert the measured layout of a running Fluensia build.

Usage:
    Fluensia.exe --ui-preview out --state games      # writes preview_games.layout.txt
    python3 tools/check_layout.py out/preview_games.layout.txt

Every line of the layout log is `kind left top right bottom extra` in *design* units
(576 x 360). The expected boxes below are the ones measured on the reference frame
(analysis/brief.md, "Measurement pass 2"); tolerance is in design px.
"""
import sys

# (label, left, top, right, bottom, tolerance)
EXPECTED = [
    ("window body",   116.0,  60.0, 460.0, 300.0, 1.5),
    ("first card",    146.0,  88.0, 212.75, 221.5, 1.5),
    ("last card",     379.75, 88.0, 446.5, 221.5, 1.5),
    ("detail panel",  147.0, 212.5, 449.0, 296.0, 1.5),
]


def load(path):
    rows = []
    for line in open(path, encoding="utf-8"):
        parts = line.split()
        if len(parts) < 5 or parts[0] == "text":   # text rows: pos, pos+size, the string itself
            continue
        try:
            rows.append((parts[0], float(parts[1]), float(parts[2]), float(parts[3]), float(parts[4])))
        except ValueError:
            continue
    return rows


def main(path):
    rows = load(path)
    print(f"{len(rows)} widget rects in {path}")
    bad = 0
    for label, l, t, r, b, tol in EXPECTED:
        best = None
        for kind, a, c, d, e in rows:
            dist = abs(a - l) + abs(c - t) + abs(d - r) + abs(e - b)
            if best is None or dist < best[0]:
                best = (dist, kind, a, c, d, e)
        dist, kind, a, c, d, e = best
        ok = dist <= tol * 4
        if not ok:
            bad += 1
        got = f"({a:.2f}, {c:.2f}) - ({d:.2f}, {e:.2f})"
        want = f"({l:.2f}, {t:.2f}) - ({r:.2f}, {b:.2f})"
        print(f"{'OK  ' if ok else 'FAIL'} {label:12s} want {want}  got {got}  [{kind}]")
    outside = [row for row in rows if row[1] < -1 or row[2] < -1 or row[3] > 577 or row[4] > 361]
    if outside:
        bad += 1
        print(f"FAIL {len(outside)} rects outside the 576x360 design space, e.g. {outside[0]}")
    print("layout ok" if bad == 0 else f"{bad} layout problem(s)")
    return 0 if bad == 0 else 1


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
