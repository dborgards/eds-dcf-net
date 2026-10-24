#!/usr/bin/env bash
# Fixture tests for changed-lines-coverage.sh, including Windows Coverlet paths.
set -euo pipefail
export LC_ALL=C

root=$(cd "$(dirname "$0")/.." && pwd)
script="$root/tools/changed-lines-coverage.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

repo="$work/repo"
results="$work/results"
mkdir -p "$repo/src" "$results"
git -C "$work" init -q repo
git -C "$repo" config user.name fixture
git -C "$repo" config user.email fixture@example.invalid
printf 'alpha\nomega\n' > "$repo/src/Demo.cs"
git -C "$repo" add src/Demo.cs
git -C "$repo" commit -qm base
git -C "$repo" branch base

# Lines 2 and 3 are added and instrumentable; line 4 is intentionally absent
# from Cobertura and therefore does not count as a coverable source line.
printf 'alpha\nnew covered\nnew uncovered\nnoninstrumentable\nomega\n' > "$repo/src/Demo.cs"
printf 'deleted\n' > "$repo/src/Deleted.cs"
git -C "$repo" add src/Deleted.cs
git -C "$repo" commit -qm 'add deleted fixture'
git -C "$repo" rm -q src/Deleted.cs

cat > "$results/coverage.cobertura.xml" <<'XML'
<?xml version="1.0"?>
<coverage lines-covered="1" lines-valid="2">
  <packages><package><classes>
    <!-- Absolute Windows checkout path: must match src/Demo.cs on every host. -->
    <class filename="D:\a\demo\src\Demo.cs"><lines>
      <line number="2" hits="1" />
      <line number="3" hits="0" />
    </lines></class>
  </classes></package></packages>
</coverage>
XML

failures=0
check() {
  local label=$1 want=$2 got=$3
  if [[ "$got" == "$want" ]]; then
    echo "ok: $label"
  else
    echo "FAIL: $label (expected $want, got $got)" >&2
    failures=$((failures + 1))
  fi
}

run_gate() {
  local minimum=$1 report_dir=${2:-$results}
  set +e
  output=$(cd "$repo" && COVERAGE_MIN_PERCENT="$minimum" "$script" "$report_dir" base 2>&1)
  status=$?
  set -e
}

run_gate 50
check "Windows paths and 50 percent threshold pass" 0 "$status"
case "$output" in
  *"Changed-lines coverage: 50.00% (1/2"*) echo "ok: only instrumentable added lines count" ;;
  *) echo "FAIL: unexpected gate output: $output" >&2; failures=$((failures + 1)) ;;
esac

run_gate 51
check "uncovered added line fails a 51 percent threshold" 1 "$status"
case "$output" in
  *"src/Demo.cs: uncovered added line(s) 3"*) echo "ok: uncovered line is reported" ;;
  *) echo "FAIL: missing uncovered-line diagnostic: $output" >&2; failures=$((failures + 1)) ;;
esac

# The Git pathspec '.' selects the whole repository; it must not silently skip
# every changed file and turn an uncovered change into a successful no-op.
set +e
dot_output=$(cd "$repo" && COVERAGE_MIN_PERCENT=51 "$script" "$results" base . 2>&1)
dot_status=$?
set -e
check "dot path filter includes changed files" 1 "$dot_status"
case "$dot_output" in
  *"Changed-lines coverage: 50.00% (1/2"*) echo "ok: dot path filter calculates coverage" ;;
  *) echo "FAIL: dot path filter skipped coverage: $dot_output" >&2; failures=$((failures + 1)) ;;
esac

# Local Git prefix configuration must not change the machine-readable paths.
git -C "$repo" config diff.mnemonicPrefix true
run_gate 51
check "mnemonic Git prefixes preserve changed paths" 1 "$status"
case "$output" in
  *"src/Demo.cs: uncovered added line(s) 3"*) echo "ok: forced diff prefixes are parsed" ;;
  *) echo "FAIL: mnemonic diff prefixes skipped coverage: $output" >&2; failures=$((failures + 1)) ;;
esac

# A source-relative Cobertura filename must be resolved through <sources>.
source_results="$work/source-results"
mkdir -p "$source_results"
cat > "$source_results/coverage.cobertura.xml" <<'XML'
<?xml version="1.0"?>
<coverage>
  <sources><source>D:\a\demo\src</source></sources>
  <packages><package><classes>
    <class filename="Demo.cs"><lines>
      <line number="2" hits="1" />
      <line number="3" hits="0" />
    </lines></class>
  </classes></package></packages>
</coverage>
XML
run_gate 51 "$source_results"
check "Cobertura source roots resolve relative filenames" 1 "$status"
case "$output" in
  *"src/Demo.cs: uncovered added line(s) 3"*) echo "ok: source-rooted path is reported" ;;
  *) echo "FAIL: Cobertura source root was ignored: $output" >&2; failures=$((failures + 1)) ;;
esac

# A parent directory named src must not win over the checkout's own src anchor.
anchor_results="$work/anchor-results"
mkdir -p "$anchor_results"
cat > "$anchor_results/coverage.cobertura.xml" <<'XML'
<?xml version="1.0"?>
<coverage><packages><package><classes>
  <class filename="C:\Users\dev\src\eds-dcf-net\src\Demo.cs"><lines>
    <line number="2" hits="1" />
    <line number="3" hits="0" />
  </lines></class>
</classes></package></packages></coverage>
XML
run_gate 51 "$anchor_results"
check "nested checkout anchors select the repository file" 1 "$status"
case "$output" in
  *"src/Demo.cs: uncovered added line(s) 3"*) echo "ok: innermost matching anchor is used" ;;
  *) echo "FAIL: parent src directory bypassed coverage: $output" >&2; failures=$((failures + 1)) ;;
esac

# core.quotepath=true must not turn UTF-8 filenames into unmatched C escapes.
printf 'new covered\nnew uncovered\n' > "$repo/src/Démø.cs"
git -C "$repo" add src/Démø.cs
git -C "$repo" config core.quotepath true
unicode_results="$work/unicode-results"
mkdir -p "$unicode_results"
cat > "$unicode_results/coverage.cobertura.xml" <<'XML'
<?xml version="1.0"?>
<coverage><packages><package><classes>
  <class filename="src/Démø.cs"><lines>
    <line number="1" hits="1" />
    <line number="2" hits="0" />
  </lines></class>
</classes></package></packages></coverage>
XML
run_gate 51 "$unicode_results"
check "quoted UTF-8 Git paths remain measurable" 1 "$status"
case "$output" in
  *"src/Démø.cs: uncovered added line(s) 2"*) echo "ok: UTF-8 path is decoded and reported" ;;
  *) echo "FAIL: UTF-8 Git path bypassed coverage: $output" >&2; failures=$((failures + 1)) ;;
esac

# A second host report contributes coverage by line, not by overwriting the
# first report's same-named class.
second="$work/second"
mkdir -p "$second"
cat > "$second/coverage.cobertura.xml" <<'XML'
<?xml version="1.0"?>
<coverage><packages><package><classes>
  <class filename="src/Demo.cs"><lines>
    <line number="2" hits="0" />
    <line number="3" hits="2" />
  </lines></class>
</classes></package></packages></coverage>
XML
mkdir -p "$second/other"
cat "$results/coverage.cobertura.xml" > "$second/other/coverage.cobertura.xml"
run_gate 100 "$second"
check "multiple reports merge covered lines" 0 "$status"
case "$output" in
  *"Read 2 Cobertura report(s)."*"Changed-lines coverage: 100.00% (2/2"*)
    echo "ok: merged report count and percentage" ;;
  *) echo "FAIL: reports were not merged correctly: $output" >&2; failures=$((failures + 1)) ;;
esac

# Deleted files have no +++ b/... target and must not produce phantom lines.
case "$output" in
  *"Deleted.cs"*) echo "FAIL: deleted file entered coverage: $output" >&2; failures=$((failures + 1)) ;;
  *) echo "ok: deleted files are ignored" ;;
esac

set +e
missing_output=$(cd "$repo" && "$script" "$results" does-not-exist 2>&1)
missing_status=$?
set -e
check "unknown Git base returns an infrastructure error" 2 "$missing_status"

if [[ $failures -ne 0 ]]; then
  echo "$failures changed-lines coverage test(s) failed" >&2
  exit 1
fi

echo "all changed-lines coverage checks passed"
