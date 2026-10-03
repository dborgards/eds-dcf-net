#!/usr/bin/env bash
# Checks the channel note that tools/semantic-release-run.sh writes when it
# repairs a release (ensure_git_notes).
#
# Everything runs in a throwaway repository under a temp directory, with a
# throwaway bare repository as its origin: no tag or note of this repository
# is read or written. The real .releaserc.json is copied in, so the test
# follows the branch configuration actually in effect.
#
# When node_modules/semantic-release is installed (npm ci), the expected
# channels are additionally computed by semantic-release's own branch
# normalization, so the test fails if a locked upgrade changes the rule.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)

command -v node >/dev/null || { echo "node is required" >&2; exit 1; }

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

failures=0

check() {
  local label="$1"
  local want="$2"
  local got="$3"
  if [[ "$got" != "$want" ]]; then
    echo "FAIL: $label (expected $want, got $got)" >&2
    failures=$((failures + 1))
  else
    echo "ok: $label"
  fi
}

# --- semantic-release's own answer (optional cross-check) -------------------

sr_lib="$root/node_modules/semantic-release/lib/branches/normalize.js"
if [[ -f "$sr_lib" ]]; then
  # index.js records `context.branch.channel || null`; normalize.js decides
  # branch.channel. Feed it the real branches with no tags.
  sr_channels=$(cd "$root" && node --input-type=module -e '
    import fs from "node:fs";
    import { pathToFileURL } from "node:url";
    const normalize = await import(pathToFileURL(process.argv[1]).href);
    const entries = JSON.parse(fs.readFileSync(".releaserc.json", "utf8")).branches
      .map((b) => ({ ...(typeof b === "string" ? { name: b } : b), tags: [] }));
    const release = normalize.release({ release: entries.filter((b) => !b.prerelease) });
    const prerelease = normalize.prerelease({ prerelease: entries.filter((b) => b.prerelease) });
    for (const b of [...release, ...prerelease]) {
      console.log(`${b.name}=${JSON.stringify({ channels: [b.channel || null] })}`);
    }
  ' "$sr_lib")
  sr_main=$(sed -n 's/^main=//p' <<<"$sr_channels")
  sr_develop=$(sed -n 's/^develop=//p' <<<"$sr_channels")
  check "semantic-release normalize: main note" '{"channels":[null]}' "$sr_main"
  check "semantic-release normalize: develop note" '{"channels":["develop"]}' "$sr_develop"
else
  echo "skip: node_modules/semantic-release not installed; no cross-check against normalize.js"
fi

# --- release_channel_note against synthetic configurations ------------------

# shellcheck source=tools/semantic-release-run.sh
source "$root/tools/semantic-release-run.sh"
set +e

cat >"$work/override.json" <<'JSON'
{ "branches": [
  { "name": "main", "channel": "stable" },
  { "name": "develop", "prerelease": "beta", "channel": "next" },
  { "name": "alpha", "prerelease": true },
  { "name": "rc", "prerelease": "rc", "channel": false }
] }
JSON
check "explicit release channel is kept" '{"channels":["stable"]}' \
  "$(release_channel_note 2.0.0 "$work/override.json")"
check "explicit prerelease channel is kept" '{"channels":["next"]}' \
  "$(release_channel_note 2.0.0-beta.1 "$work/override.json")"
check "prerelease: true uses the branch name" '{"channels":["alpha"]}' \
  "$(release_channel_note 2.0.0-alpha.1 "$work/override.json")"
check "channel false means the default channel" '{"channels":[null]}' \
  "$(release_channel_note 2.0.0-rc.1 "$work/override.json")"

release_channel_note 2.0.0-gamma.1 "$work/override.json" >/dev/null 2>&1
check "unknown prerelease id is refused" 1 "$?"

cat >"$work/two-releases.json" <<'JSON'
{ "branches": ["main", "next"] }
JSON
release_channel_note 2.0.0 "$work/two-releases.json" >/dev/null 2>&1
check "stable version with two release branches is refused" 1 "$?"

release_channel_note 2.0.0 "$work/missing.json" >/dev/null 2>&1
check "missing configuration is refused" 1 "$?"

# --- ensure_git_notes in a throwaway repository -----------------------------

git init -q --bare "$work/origin.git"
git init -q "$work/repo"
cp "$root/.releaserc.json" "$work/repo/.releaserc.json"
cd "$work/repo"
git config user.name "test"
git config user.email "test@example.invalid"
git config commit.gpgsign false
git config tag.gpgsign false
git remote add origin "$work/origin.git"

git commit -q --allow-empty -m "feat: one"
git tag v1.2.0-beta.3
beta_commit=$(git rev-parse HEAD)
git commit -q --allow-empty -m "fix: two"
# Annotated on purpose: the note must sit on the peeled commit.
git tag -a -m "v1.2.0" v1.2.0
stable_commit=$(git rev-parse HEAD)
git commit -q --allow-empty -m "fix: three"
git tag v1.2.1-beta.1
kept_commit=$(git rev-parse HEAD)
git notes --ref semantic-release-v1.2.1-beta.1 add -m '{"channels":["develop"]}' "$kept_commit"

for ref_name in develop main; do
  rm -f "$work"/note-*
  for ref in $(git for-each-ref --format='%(refname)' refs/notes/semantic-release-v1.2.0-beta.3 refs/notes/semantic-release-v1.2.0); do
    git update-ref -d "$ref"
  done

  GITHUB_REF_NAME="$ref_name" ensure_git_notes 1.2.0-beta.3 >/dev/null 2>&1
  check "[$ref_name] beta repair succeeds" 0 "$?"
  check "[$ref_name] beta note carries the develop channel" '{"channels":["develop"]}' \
    "$(git notes --ref semantic-release-v1.2.0-beta.3 show "$beta_commit" 2>/dev/null)"

  GITHUB_REF_NAME="$ref_name" ensure_git_notes 1.2.0 >/dev/null 2>&1
  check "[$ref_name] stable repair succeeds" 0 "$?"
  check "[$ref_name] stable note carries the default channel" '{"channels":[null]}' \
    "$(git notes --ref semantic-release-v1.2.0 show "$stable_commit" 2>/dev/null)"
done

GITHUB_REF_NAME=develop ensure_git_notes 1.2.1-beta.1 >/dev/null 2>&1
check "existing note: repair succeeds" 0 "$?"
check "existing note is left untouched" '{"channels":["develop"]}' \
  "$(git notes --ref semantic-release-v1.2.1-beta.1 show "$kept_commit")"

# semantic-release reads notes with this exact command (lib/git.js,
# getTagsNotes). The repaired tags must be visible to it with their channels.
seen=$(git log --tags="*" --decorate-refs="refs/tags/*" --no-walk \
  --format="%d%x09%N" --notes="refs/notes/semantic-release*" | tr -d '\r')
check "semantic-release sees the beta note" 1 \
  "$(grep -cF $'(tag: v1.2.0-beta.3)\t{"channels":["develop"]}' <<<"$seen")"
check "semantic-release sees the stable note" 1 \
  "$(grep -cF $'(tag: v1.2.0)\t{"channels":[null]}' <<<"$seen")"

# --- repaired GitHub notes when the release commit body is empty ------------
#
# complete_release_publish calls release_notes_for_version. A subject-only
# release commit has no body; the notes live in CHANGELOG.md at that tag.
# A non-empty body (older release commits) still wins, so a repair of those
# tags does not switch sources.

cat >CHANGELOG.md <<'EOF'
# Changelog

## [9.9.0](https://example/compare/v9.8.0...v9.9.0) (2026-10-02)

### Bug Fixes

* fix: repaired from changelog

## [9.8.0](https://example/compare/v9.7.0...v9.8.0) (2026-10-01)

### Features

* feat: older section must not leak
EOF
git add CHANGELOG.md
git commit -q -m "chore(release): 9.9.0"
git tag v9.9.0

empty_body_notes="$(release_notes_for_version 9.9.0)"
check "empty body: notes recovered" 0 "$?"
check "empty body: changelog heading kept" 1 \
  "$(grep -cF '## [9.9.0](https://example/compare/v9.8.0...v9.9.0) (2026-10-02)' <<<"$empty_body_notes")"
check "empty body: changelog bullet kept" 1 \
  "$(grep -cF '* fix: repaired from changelog' <<<"$empty_body_notes")"
check "empty body: next section excluded" 0 \
  "$(grep -cF 'feat: older section must not leak' <<<"$empty_body_notes")"
check "empty body: notes are non-empty" 0 \
  "$([[ -n "${empty_body_notes//[[:space:]]/}" ]]; echo $?)"

git commit -q --allow-empty -m "$(printf '%s\n' 'chore(release): 9.8.0' '' 'body notes for 9.8.0')"
git tag v9.8.0
body_notes="$(release_notes_for_version 9.8.0)"
check "non-empty body: repair succeeds" 0 "$?"
check "non-empty body: commit body is used" 1 \
  "$(grep -cF 'body notes for 9.8.0' <<<"$body_notes")"
check "non-empty body: changelog is not substituted" 0 \
  "$(grep -cF 'feat: older section must not leak' <<<"$body_notes")"

git commit -q --allow-empty -m "chore(release): 9.7.0"
git tag v9.7.0
release_notes_for_version 9.7.0 >/dev/null 2>&1
check "empty body without a changelog section is refused" 1 "$?"

if [[ "$failures" -ne 0 ]]; then
  echo "$failures release channel note check(s) failed" >&2
  exit 1
fi

echo "all release channel note checks passed"
