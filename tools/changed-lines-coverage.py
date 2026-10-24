#!/usr/bin/env python3
"""Calculate Cobertura coverage for added Git lines.

This is invoked by changed-lines-coverage.sh. Paths are matched by normalized
repository-relative names, including Coverlet's Windows absolute paths, rather
than by host-specific string prefixes. Multiple reports are merged by line with
covered-when-any-report-covered semantics.
"""

from __future__ import annotations

import os
import posixpath
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path


def fail(message: str, status: int = 2) -> None:
    print(f"changed-lines-coverage: {message}", file=sys.stderr)
    raise SystemExit(status)


def git(root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=root,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        fail(result.stderr.strip() or f"git {' '.join(args)} failed")
    return result.stdout


def normalize(value: str) -> str:
    value = value.replace("\\", "/").strip()
    while value.startswith("./"):
        value = value[2:]
    return posixpath.normpath(value)


def repo_relative(filename: str, root: Path) -> str | None:
    """Map relative, Unix absolute, and Windows absolute paths to repo paths."""
    normalized = normalize(filename)
    lower = normalized.lower()
    root_normalized = normalize(str(root)).rstrip("/").lower()

    if lower == root_normalized:
        return None
    if lower.startswith(root_normalized + "/"):
        return normalized[len(root_normalized) + 1 :]

    # Coverlet can retain a Windows checkout path even when the report is read
    # on another host. Recover at a known repository root directory, not by a
    # basename-only match (which aliases same-named files in different folders).
    for anchor in ("/src/", "/tests/", "/examples/", "/benchmarks/"):
        index = lower.find(anchor)
        if index >= 0:
            return normalized[index + 1 :]

    # A genuinely relative Coverlet filename is already repository-relative.
    is_absolute = normalized.startswith("/") or (
        len(normalized) >= 3 and normalized[1:3] == ":/"
    )
    return None if is_absolute else normalized


def report_relative(filename: str, sources: list[str], root: Path) -> str | None:
    """Resolve a class filename against Cobertura's report source roots."""
    normalized = normalize(filename)
    is_absolute = normalized.startswith("/") or (
        len(normalized) >= 3 and normalized[1:3] == ":/"
    )
    if is_absolute or not sources:
        return repo_relative(filename, root)

    candidates: list[str] = []
    for source in sources:
        candidate = repo_relative(
            posixpath.join(normalize(source), normalized), root
        )
        if candidate is not None and candidate not in candidates:
            candidates.append(candidate)

    # Prefer a source-root interpretation that names a real repository file.
    # Cobertura permits multiple roots; their document order is the fallback.
    for candidate in candidates:
        if (root / candidate).is_file():
            return candidate
    return candidates[0] if candidates else repo_relative(filename, root)


def load_coverage(results: Path, root: Path) -> dict[str, dict[int, bool]]:
    reports = (
        [results]
        if results.is_file()
        else sorted(results.rglob("coverage.cobertura.xml"))
        if results.is_dir()
        else []
    )
    if not reports:
        fail(f"no coverage.cobertura.xml found under {results}")

    merged: dict[str, dict[int, bool]] = defaultdict(dict)
    for report in reports:
        try:
            document = ET.parse(report)
        except (ET.ParseError, OSError) as error:
            fail(f"cannot read {report}: {error}")

        sources = [
            element.text.strip()
            for element in document.findall(".//sources/source")
            if element.text and element.text.strip()
        ]
        for class_element in document.findall(".//class"):
            filename = class_element.get("filename")
            if not filename:
                continue
            relative = report_relative(filename, sources, root)
            if relative is None:
                continue
            lines = merged[relative]
            for line in class_element.findall(".//line"):
                try:
                    number = int(line.get("number", ""))
                    hits = int(line.get("hits", "0"))
                except ValueError:
                    continue
                lines[number] = lines.get(number, False) or hits > 0

    print(f"Read {len(reports)} Cobertura report(s).")
    return merged


def parse_diff(diff: str) -> dict[str, set[int]]:
    changed: dict[str, set[int]] = defaultdict(set)
    current: str | None = None
    new_line: int | None = None

    for line in diff.splitlines():
        if line.startswith("+++ "):
            target = line[4:].strip()
            if target == "/dev/null":
                current = None
            else:
                current = normalize(target[2:] if target.startswith("b/") else target)
                changed.setdefault(current, set())
            new_line = None
            continue
        if line.startswith("@@ ") and current is not None:
            try:
                added = line.split(" +", 1)[1].split(" ", 1)[0]
                start = added.split(",", 1)[0]
                new_line = int(start)
            except (IndexError, ValueError):
                fail(f"unsupported diff hunk header: {line}")
            continue
        if new_line is None or current is None:
            continue
        if line.startswith("+"):
            changed[current].add(new_line)
            new_line += 1
        elif line.startswith(" "):
            new_line += 1
        # Deleted lines and "\\ No newline" do not advance the new file line.

    return changed


def selected(path: str, roots: list[str]) -> bool:
    normalized = normalize(path)
    return any(
        root == "."
        or normalized == root
        or normalized.startswith(root.rstrip("/") + "/")
        for root in roots
    )


def main() -> None:
    if len(sys.argv) < 3:
        fail("usage: changed-lines-coverage.py <results> <base-ref> [paths...]")
    results = Path(sys.argv[1])
    base_ref = sys.argv[2]
    roots = [normalize(arg) for arg in sys.argv[3:]] or ["src"]

    root_text = git(Path.cwd(), "rev-parse", "--show-toplevel").strip()
    if not root_text:
        fail("not inside a Git worktree")
    root = Path(root_text).resolve()
    merge_base = git(root, "merge-base", base_ref, "HEAD").strip()
    if not merge_base:
        fail(f"no merge base between {base_ref} and HEAD")

    diff = git(
        root,
        "diff",
        "--unified=0",
        "--no-color",
        "--src-prefix=a/",
        "--dst-prefix=b/",
        merge_base,
        "--",
        *roots,
    )
    changed = parse_diff(diff)
    coverage = load_coverage(results, root)
    minimum = float(os.environ.get("COVERAGE_MIN_PERCENT", "95.0"))

    total = 0
    covered = 0
    failures: list[str] = []
    for path in sorted(changed):
        if not selected(path, roots):
            continue
        lines = coverage.get(path, {})
        instrumentable = sorted(number for number in changed[path] if number in lines)
        if not instrumentable:
            continue
        file_covered = sum(1 for number in instrumentable if lines[number])
        total += len(instrumentable)
        covered += file_covered
        percent = 100.0 * file_covered / len(instrumentable)
        print(f"{path}: {percent:.2f}% ({file_covered}/{len(instrumentable)})")
        uncovered = [number for number in instrumentable if not lines[number]]
        if uncovered:
            failures.append(
                f"{path}: uncovered added line(s) {', '.join(map(str, uncovered))}"
            )

    if total == 0:
        print("No instrumentable added lines; changed-lines coverage is not applicable.")
        return

    percent = 100.0 * covered / total
    print(
        f"Changed-lines coverage: {percent:.2f}% "
        f"({covered}/{total}; required >= {minimum:.2f}%)"
    )
    if percent + 1e-9 < minimum:
        for message in failures:
            print(message, file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    main()
