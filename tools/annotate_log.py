#!/usr/bin/env python3
"""Emit a captured command's output as GitHub annotations (plus a few structured checks).

Usage:  python3 tools/annotate_log.py <log> [--show-all]
Why: the raw Actions log blob and artifact zips are not always reachable, while job
annotations are plain JSON through the API. Anything interesting in the log becomes one.
"""
import re
import sys

INTERESTING = re.compile(
    r"(Unhandled exception|Assertion|assert|abort|Aborted|Segmentation|Fatal|"
    r"^  frame \d+:|preview written|text dump|layout runs|exit=|Traceback|"
    r"error|Error|ERROR|missing|FAIL|OK  )"
)


def main(path, show_all=False):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    picked = lines if show_all else [l for l in lines if INTERESTING.search(l)]
    picked = picked[-40:]
    for line in picked:
        msg = line.replace("%", "%25").replace("\r", "").strip()
        if not msg:
            continue
        if len(msg) > 900:
            msg = msg[:900] + "…"
        print(f"::warning::{msg}")
    if not picked:
        print("::warning::log had no interesting lines")


if __name__ == "__main__":
    main(sys.argv[1], "--show-all" in sys.argv)
