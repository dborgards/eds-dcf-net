#!/usr/bin/env python3
"""Check the work-package ordering of an action plan.

Usage:
    python3 tools/check-plan-order.py docs/plan-2026-09-30.md

The plan lists the files each work package (WP) changes in its "Dateien"
bullets and defines the execution order in the table of section 5.1
("| Gruppe | Reihenfolge | Dateien |"). Two work packages that name the same
file must be ordered by that table, directly or transitively, so that agents
working in separate worktrees never change the same file at the same time.

The script reads the order from the table itself:
    WP-a -> WP-b                      chain (written with the arrow character)
    WP-b (... nach WP-x und WP-y)     additional predecessors of WP-b
Chains are separated by ";" or "," outside parentheses.

Excluded from the check, as stated in the plan: test files, model classes,
ModelCloner and the diagnostic code constants.

Exit code 0 when every pair is ordered, there is no cycle and every open work
package with files appears in the table; 1 otherwise.
"""
import collections
import itertools
import re
import sys

EXCLUDE = re.compile(r"(^|/)Models/|ModelCloner|ParseDiagnosticCodes|(^|/)tests/|Tests\.cs$|\*")
FILE = re.compile(r"\.(cs|yml|json|sh|md|csproj|html|gitignore)$|^\.gitignore$")
FILES_BULLET = re.compile(r"- \*\*Dateien[^:]*:\*\*(.*?)(?=\n- \*\*|\n\n|\Z)", re.S)


def split_top(text, separators):
    parts, depth, current = [], 0, ""
    for ch in text:
        if ch == "(":
            depth += 1
        if ch == ")":
            depth -= 1
        if ch in separators and depth == 0:
            parts.append(current)
            current = ""
        else:
            current += ch
    parts.append(current)
    return [p.strip() for p in parts if p.strip()]


def main(path):
    text = open(path, encoding="utf-8").read()

    files = collections.defaultdict(set)
    packages, without_files = [], set()
    for block in re.split(r"\n(?=#### WP-\d+ ·)", text):
        match = re.match(r"#### (WP-\d+) ·", block)
        if not match:
            continue
        wp = match.group(1)
        packages.append(wp)
        body = block.split("\n### ")[0].split("\n## ")[0]
        found = False
        for bullet in FILES_BULLET.finditer(body):
            for name in re.findall(r"`([^`]+)`", bullet.group(1)):
                if EXCLUDE.search(name) or not FILE.search(name):
                    continue
                parts = name.split("/")
                key = parts[-1] if parts[-1] != "Program.cs" else "/".join(parts[-2:])
                files[key].add(wp)
                found = True
        if not found:
            without_files.add(wp)

    done = set(re.findall(r"^\| (WP-\d+) \|[^\n]*\| \[x\] \|", text, re.M))

    start = text.index("| Gruppe | Reihenfolge | Dateien |")
    table = text[start:]
    table = table[: table.index("\n\n")]
    before = collections.defaultdict(set)
    in_table = set()
    for row in table.split("\n")[2:]:
        cell = row.split("|")[2]
        for segment in split_top(cell, ";,"):
            previous = None
            for element in segment.split("→"):
                match = re.match(r"\s*(WP-\d+)\s*(\((.*)\))?", element)
                if not match:
                    raise SystemExit("cannot parse table element: " + element)
                wp = match.group(1)
                in_table.add(wp)
                if previous:
                    before[previous].add(wp)
                if match.group(3):
                    for predecessor in re.findall(r"WP-\d+", match.group(3)):
                        before[predecessor].add(wp)
                previous = wp

    changed = True
    while changed:
        changed = False
        for a in list(before):
            for b in list(before[a]):
                new = before.get(b, set()) - before[a]
                if new:
                    before[a] |= new
                    changed = True

    cycles = sorted(a for a in before if a in before[a])

    def ordered(a, b):
        return b in before.get(a, ()) or a in before.get(b, ())

    unordered = collections.defaultdict(list)
    for name, wps in sorted(files.items()):
        for a, b in itertools.combinations(sorted(wps), 2):
            if a in done or b in done:
                continue
            if not ordered(a, b):
                unordered[(a, b)].append(name)

    missing = sorted(set(packages) - in_table - done - without_files, key=lambda w: int(w[3:]))

    print("work packages: %d | in table: %d | done: %d" % (len(packages), len(in_table), len(done)))
    print("not in table:", ", ".join(missing) or "none")
    print("cycles:", ", ".join(cycles) or "none")
    print("unordered pairs sharing files:", "none" if not unordered else "")
    for (a, b), names in sorted(unordered.items()):
        print("  %s / %s: %s" % (a, b, ", ".join(sorted(names))))
    return 1 if (unordered or cycles or missing) else 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1]))
