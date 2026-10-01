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

A listed name counts as a file when it contains a directory separator or ends
in an extension. Names are resolved against the tracked files of the
repository: a name that identifies exactly one tracked file is replaced by that
path, so `README.md` and `docs/architecture/README.md` stay distinct. Names
that cannot be resolved (new or ambiguous files) are the same file when one
path is a suffix of the other.

Work packages marked done `[x]` or deferred `[-]` in the status table are
inactive and ignored.

Exit code 0 when every pair is ordered, there is no cycle and every active
work package with files appears in the table; 1 otherwise.
"""
import collections
import itertools
import os
import re
import subprocess
import sys

EXCLUDE = re.compile(r"(^|/)Models/|ModelCloner|ParseDiagnosticCodes|(^|/)tests/|Tests\.cs$|[*\s()<>]")
FILE = re.compile(r"/|\.[A-Za-z0-9]{1,12}$|^\.[A-Za-z0-9]+$")


def tracked_files(plan_path):
    """Tracked repository paths, or an empty list outside a git checkout."""
    try:
        root = subprocess.run(
            ["git", "rev-parse", "--show-toplevel"],
            cwd=os.path.dirname(os.path.abspath(plan_path)) or ".",
            capture_output=True, text=True, check=True).stdout.strip()
        listing = subprocess.run(
            ["git", "ls-files"], cwd=root, capture_output=True, text=True, check=True)
        return listing.stdout.splitlines()
    except (OSError, subprocess.CalledProcessError):
        return []


def resolve(name, tracked):
    """Return the tracked path a listed name identifies, else the name itself."""
    name = name.strip("/")
    if name in tracked:
        return name
    matches = [path for path in tracked if path.endswith("/" + name)]
    return matches[0] if len(matches) == 1 else name


def same_file(a, b, tracked):
    """Two tracked paths must be equal; otherwise one may be a suffix of the other."""
    if a in tracked and b in tracked:
        return a == b
    pa, pb = a.split("/"), b.split("/")
    n = min(len(pa), len(pb))
    return pa[-n:] == pb[-n:]
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
    tracked = tracked_files(path)
    tracked_set = set(tracked)

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
                resolved = resolve(name, tracked)
                if EXCLUDE.search(resolved):
                    continue
                files[wp].add(resolved if resolved in tracked_set else name.strip("/"))
                found = True
        if not found:
            without_files.add(wp)

    done = set(re.findall(r"^\| (WP-\d+) \|[^\n]*\| \[[x-]\] \|", text, re.M))

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
    active = sorted(wp for wp in files if wp not in done)
    for a, b in itertools.combinations(active, 2):
        if ordered(a, b):
            continue
        shared = sorted({x for x in files[a] for y in files[b] if same_file(x, y, tracked_set)})
        if shared:
            unordered[(a, b)] = shared

    missing = sorted(set(packages) - in_table - done - without_files, key=lambda w: int(w[3:]))

    print("work packages: %d | in table: %d | done or deferred: %d" % (len(packages), len(in_table), len(done)))
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
