#!/usr/bin/env bash
# Fixture check for parserOpts.noteKeywords on @semantic-release/release-notes-generator.
#
# conventional-changelog-conventionalcommits (and conventional-commits-parser's
# own default) uses noteKeywords ["BREAKING CHANGE", "BREAKING-CHANGE"].
# CommitParser applies that as a case-insensitive per-line pattern
# ^[\s|*]*(<keywords>)[:\s]+(.*) to every line after the subject
# (conventional-commits-parser CommitParser.parse / parseNotes).
#
# The commit analyzer in .releaserc.json overrides that list and keeps only
# "BREAKING CHANGE" and "BREAKING CHANGES". The notes generator did not, so
# the preset default still matched a wrapped prose line in fffcb2c
# ("breaking-change intent" ...) and wrote a false "⚠ BREAKING CHANGES"
# section into CHANGELOG 1.13.0, while the analyzer correctly planned no
# major bump. The notes generator must use the same parserOpts.
#
# load-changelog-config.js replaces parser noteKeywords by object spread
# ({ ...preset.parser, ...parserOpts }), so the array has to sit on the
# plugin's parserOpts, not inside presetConfig (the preset hardcodes
# noteKeywords and ignores a presetConfig copy).
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
config="$root/.releaserc.json"

command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }

# Same two phrases the analyzer already publishes. Order matches .releaserc.json
# and the alternation the notes regex builds from the array.
expected='["BREAKING CHANGE","BREAKING CHANGES"]'

# Preset default that produced the 1.13.0 section. Not read from node_modules:
# this job must run from a checkout alone, the way breaking-intent does.
preset_default='["BREAKING CHANGE","BREAKING-CHANGE"]'

note_keywords() {
  local plugin="$1"
  jq -c --arg plugin "$plugin" '
    .plugins[]
    | select(type == "array" and .[0] == $plugin)
    | .[1].parserOpts.noteKeywords
  ' "$config"
}

analyzer_kw=$(note_keywords "@semantic-release/commit-analyzer")
notes_kw=$(note_keywords "@semantic-release/release-notes-generator")

if [[ -z "$analyzer_kw" || "$analyzer_kw" == "null" ]]; then
  echo "commit-analyzer parserOpts.noteKeywords is missing" >&2
  exit 1
fi
if [[ -z "$notes_kw" || "$notes_kw" == "null" ]]; then
  echo "FAIL: release-notes-generator has no parserOpts.noteKeywords; the conventionalcommits preset default includes BREAKING-CHANGE and that hyphenated keyword created the false 1.13.0 section" >&2
  exit 1
fi
if [[ "$analyzer_kw" != "$expected" ]]; then
  echo "FAIL: commit-analyzer noteKeywords changed (expected $expected, got $analyzer_kw)" >&2
  exit 1
fi
if [[ "$notes_kw" != "$analyzer_kw" ]]; then
  echo "FAIL: release-notes-generator noteKeywords differ from the analyzer (analyzer $analyzer_kw, notes $notes_kw)" >&2
  exit 1
fi
if [[ "$notes_kw" == "$preset_default" ]]; then
  echo "FAIL: release-notes-generator still uses the preset default, which includes BREAKING-CHANGE" >&2
  exit 1
fi

# True when any line after the subject matches the parser notes pattern for
# the given keyword array. The subject is the header and is not scanned.
body_has_note() {
  local keywords="$1"
  local message="$2"
  jq -nr --arg msg "$message" --argjson kws "$keywords" '
    def esc: gsub("[.*+?^${}()|\\[\\]\\\\]"; "\\\\&");
    ($kws | map(esc) | join("|")) as $alts
    | ($msg | split("\n")[1:] | map(select(length > 0))) as $lines
    | any($lines[]; test("(?i)^[\\s|*]*(" + $alts + ")[:\\s]+"))
  '
}

failures=0

expect_note() {
  local want="$1"
  local label="$2"
  local message="$3"
  local got
  got=$(body_has_note "$notes_kw" "$message")
  if [[ "$got" != "$want" ]]; then
    echo "FAIL: $label (expected $want, got $got)" >&2
    failures=$((failures + 1))
  else
    echo "ok: $label"
  fi
}

# The fixture is the line that actually matched in 1.13.0. If this stops
# matching the preset default, the regression sample has drifted.
incident=$'fix(release): drop bare BREAKING keyword to stop false-positive major bumps\n\nMirrored the same change in build.yml\'s "Detect explicit\nbreaking-change intent" step - the apicompat gate\'s hand-rolled\nreimplementation of the same detection logic - and updated\nCONTRIBUTING.md\'s description of both mechanisms.'

preset_hit=$(body_has_note "$preset_default" "$incident")
if [[ "$preset_hit" != "true" ]]; then
  echo "FAIL: 1.13.0 prose no longer matches the preset default BREAKING-CHANGE keyword; the fixture does not reproduce the changelog section" >&2
  exit 1
fi
echo "ok: preset default still matches the 1.13.0 prose line"

expect_note false "1.13.0 prose is not a breaking note (fffcb2c)" "$incident"

expect_note false "hyphenated BREAKING-CHANGE footer" \
  $'fix: tidy callers\n\nBREAKING-CHANGE: this removes Foo'

expect_note false "hyphenated keyword only in the subject" \
  $'fix: BREAKING-CHANGE: not a footer\n\nordinary body'

expect_note true "BREAKING CHANGE: footer" \
  $'fix: tidy callers\n\nBREAKING CHANGE: this removes Foo'

expect_note true "BREAKING CHANGES: footer" \
  $'fix: tidy callers\n\nBREAKING CHANGES: removed Foo and Bar'

# The notes parser's separator is [:\\s]+, looser than the apicompat gate,
# which requires a colon. Keep this fixture on the parser, not that gate.
expect_note true "BREAKING CHANGE separated by whitespace" \
  $'fix: tidy callers\n\nBREAKING CHANGE this removes Foo'

expect_note true "BREAKING CHANGES separated by whitespace" \
  $'fix: tidy callers\n\nBREAKING CHANGES this removes Foo'

expect_note true "lowercase footer" \
  $'fix: tidy callers\n\nbreaking change: this removes Foo'

expect_note true "indented footer" \
  $'fix: tidy callers\n\n  BREAKING CHANGE: this removes Foo'

expect_note true "bulleted BREAKING CHANGES footer" \
  $'fix: tidy callers\n\n* BREAKING CHANGES: removed Foo'

expect_note true "piped footer" \
  $'fix: tidy callers\n\n| BREAKING CHANGE: this removes Foo'

expect_note false "bare BREAKING: footer" \
  $'fix: tidy callers\n\nBREAKING: this removes Foo'

expect_note false "keyword mid-line in prose" \
  $'fix: tidy callers\n\nthe two-word BREAKING CHANGE phrase is not a footer'

expect_note false "ordinary fix" \
  $'fix: correct a typo\n\nNo footer here.'

if [[ "$failures" -ne 0 ]]; then
  echo "$failures release-notes parserOpts check(s) failed" >&2
  exit 1
fi

echo "all release-notes parserOpts checks passed"
