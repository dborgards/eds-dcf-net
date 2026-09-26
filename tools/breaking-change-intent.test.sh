#!/usr/bin/env bash
# Fixture check for the ApiCompat "Detect explicit breaking-change intent"
# jq program embedded in .github/workflows/build.yml.
#
# The program is the source of truth (the workflow step passes it to
# `gh api --jq`). This script extracts that program and evaluates the same
# commit-message shapes the gate sees.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
workflow="$root/.github/workflows/build.yml"

command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }

# The program is a single-quoted bash assignment inside the workflow run
# block. Print only the lines between jq_program=' and the closing quote.
jq_program=$(awk '
  capture && $0 ~ /^[[:space:]]*'"'"'[[:space:]]*$/ { exit }
  capture { print }
  $0 ~ /jq_program=/ && $0 ~ /'"'"'$/ { capture = 1 }
' "$workflow")

if [[ -z "$jq_program" ]]; then
  echo "failed to extract jq_program from $workflow" >&2
  exit 1
fi

# Colon plus whitespace, not the parser's looser "colon or whitespace".
if [[ "$jq_program" == *'[:\\s]+'* ]]; then
  echo "footer separator still accepts whitespace without a colon ([:\\s]+)" >&2
  exit 1
fi
if [[ "$jq_program" != *':\\s+'* ]]; then
  echo "footer separator is missing the required colon-plus-whitespace (:\\s+)" >&2
  exit 1
fi

# Do not re-introduce a bare BREAKING alternative next to the two-word keywords.
if [[ "$jq_program" == *'|BREAKING)'* || "$jq_program" == *'|BREAKING|'* ]]; then
  echo "footer pattern re-introduces a bare BREAKING alternative" >&2
  exit 1
fi

expected_error="or a 'BREAKING CHANGE:'/'BREAKING CHANGES:' footer (Major release)."
if ! grep -F "$expected_error" "$workflow" >/dev/null; then
  echo "unannounced-breaking error text does not require the colon footer" >&2
  exit 1
fi
if grep -F "or a BREAKING CHANGE/BREAKING CHANGES footer" "$workflow" >/dev/null; then
  echo "unannounced-breaking error text still omits the colon" >&2
  exit 1
fi

failures=0

expect() {
  local want="$1"
  local label="$2"
  shift 2
  local payload got
  payload=$(jq -n --args '[$ARGS.positional[] | {commit: {message: .}}]' "$@")
  got=$(printf '%s' "$payload" | jq -r "$jq_program")
  if [[ "$got" != "$want" ]]; then
    echo "FAIL: $label (expected $want, got $got)" >&2
    failures=$((failures + 1))
  else
    echo "ok: $label"
  fi
}

expect false "BREAKING CHANGE without a colon" \
  $'fix: tidy callers\n\nBREAKING CHANGE this removes Foo'

expect true "BREAKING CHANGE: footer" \
  $'fix: tidy callers\n\nBREAKING CHANGE: this removes Foo'

expect true "BREAKING CHANGES: footer" \
  $'fix: tidy callers\n\nBREAKING CHANGES: removed Foo and Bar'

expect false "BREAKING CHANGES without a colon" \
  $'fix: tidy callers\n\nBREAKING CHANGES this removes Foo'

expect true "feat!: header" 'feat!: remove Foo'
expect true "scoped feat!: header" 'feat(api)!: remove Foo'
expect true "breaking: header" 'breaking: remove Foo'
expect true "major: header" 'major: remove Foo'
expect true "scoped breaking: header" 'breaking(api): remove Foo'
expect true "scoped major: header" 'major(api): remove Foo'
expect true "fix!: header" 'fix!: remove Foo'

expect false "bang header missing the required space" 'fix!:remove API'
expect false "major header missing the required space" 'major:remove API'

expect false "footer keyword used as the subject" \
  'BREAKING CHANGE: this removes Foo'

expect false "feat!: only in the body" \
  $'fix: tidy callers\n\nfeat!: remove Foo'

expect false "breaking: only in the body" \
  $'fix: tidy callers\n\nbreaking: remove Foo'

expect false "major: only in the body" \
  $'fix: tidy callers\n\nmajor: remove Foo'

expect false "bare BREAKING: footer" \
  $'fix: tidy callers\n\nBREAKING: this removes Foo'

expect false "hyphenated BREAKING-CHANGE footer" \
  $'fix: tidy callers\n\nBREAKING-CHANGE: this removes Foo'

expect false "colon jammed against the description" \
  $'fix: tidy callers\n\nBREAKING CHANGE:this removes Foo'

expect false "space before the colon" \
  $'fix: tidy callers\n\nBREAKING CHANGE : this removes Foo'

expect true "lowercase footer" \
  $'fix: tidy callers\n\nbreaking change: this removes Foo'

expect true "indented footer" \
  $'fix: tidy callers\n\n  BREAKING CHANGE: this removes Foo'

expect true "bulleted footer" \
  $'fix: tidy callers\n\n* BREAKING CHANGES: removed Foo'

expect true "footer after a prose paragraph" \
  $'feat: redesign the reader\n\nCallers must update.\n\nBREAKING CHANGE: ReadFile now returns a Result type'

expect false "ordinary fix" 'fix: correct a typo'

# Issue #540: a commit body that talks about these keywords (the line that
# used to match a bare BREAKING alternative) is still not a footer.
expect false "issue 540 prose class" \
  $'ci: align breaking-change intent detection\n\n- Add the bare `BREAKING:` footer keyword (parserOpts.noteKeywords\n  includes "BREAKING CHANGE", "BREAKING CHANGES", and "BREAKING"),\n  which the old regex did not match.\n  BREAKING CHANGE/BREAKING CHANGES/BREAKING footer check only runs\n  against the body.\nbulleted footer, or "BREAKING CHANGE " (space, no colon) — all of\nwhich would be ordinary prose.'

expect false "no-colon footer among other commits" \
  $'fix: one' \
  $'fix: two\n\nBREAKING CHANGE this removes Foo'

expect true "colon footer among other commits" \
  $'fix: one' \
  $'fix: two\n\nBREAKING CHANGE: this removes Foo'

expect true "feat!: header still matches beside a no-colon body line" \
  $'feat!: remove Foo\n\nBREAKING CHANGE this removes Foo'

if [[ "$failures" -ne 0 ]]; then
  echo "$failures breaking-change intent check(s) failed" >&2
  exit 1
fi

echo "all breaking-change intent checks passed"
