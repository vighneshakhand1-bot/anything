#!/usr/bin/env python3
"""Turn `dotnet build` output into GitHub workflow-command annotations.

The raw Actions log blob is not always reachable (and is awkward to grep), while job
annotations are plain JSON through the API, so CI re-emits every compiler error as one.
"""
import re
import sys

# src/Fluensia/Core/App.cs(49,28): error CS0246: The type or namespace name 'GamesView' ...
PATTERN = re.compile(r"^(?P<file>[^(]+)\((?P<line>\d+),\d+\): error (?P<code>[A-Z]+\d+): (?P<msg>.*)$")


def main(path):
    shown = 0
    for line in open(path, encoding="utf-8", errors="replace"):
        m = PATTERN.match(line.strip())
        if not m:
            continue
        shown += 1
        if shown > 50:
            break
        msg = m.group("msg").replace("%", "%25").replace("\r", "").replace("\n", " ")
        print(f"::error file={m.group('file')},line={m.group('line')}::{m.group('code')}: {msg}")
    print(f"::error::dotnet build failed with {shown} diagnostic(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
