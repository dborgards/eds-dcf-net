#!/usr/bin/env bash
# Runs semantic-release and tolerates git-notes push failures when the release
# itself (tag, release commit, NuGet publish) already succeeded. GitHub has
# intermittently returned 500 Internal Server Error when pushing
# refs/notes/semantic-release-v*; treating that as a hard failure leaves develop
# with failed required checks because relay-release-status mirrors the workflow
# conclusion onto the [skip ci] release commit.
set -euo pipefail

warn() {
  echo "Warning: $*" >&2
}

configure_git_auth() {
  if [[ -z "${GIT_CREDENTIALS:-}" ]]; then
    echo "GIT_CREDENTIALS is required for git push retries." >&2
    return 1
  fi

  local repo_url
  repo_url="$(git config --get remote.origin.url || true)"
  if [[ -z "$repo_url" ]]; then
    echo "No origin remote configured." >&2
    return 1
  fi

  case "$repo_url" in
    https://github.com/*)
      repo_url="https://${GIT_CREDENTIALS}@github.com/${repo_url#https://github.com/}"
      ;;
    https://*@github.com/*)
      repo_url="https://${GIT_CREDENTIALS}@github.com/${repo_url#*@github.com/}"
      ;;
  esac

  git remote set-url origin "$repo_url"
}

retry_git_notes_push() {
  local notes_ref="$1"
  local attempt

  for attempt in 1 2 3; do
    if git push -- origin "refs/notes/${notes_ref}"; then
      echo "Git notes push succeeded on attempt ${attempt}."
      return 0
    fi

    warn "Git notes push attempt ${attempt}/3 failed for refs/notes/${notes_ref}."
    sleep $((attempt * 10))
  done

  return 1
}

verify_published_release() {
  local trigger_sha="$1"
  local branch="$2"
  local next_version="$3"
  local next_tag="v${next_version}"
  local notes_ref="semantic-release-v${next_version}"

  git fetch origin "$branch" --tags

  if ! git rev-parse -q --verify "refs/tags/${next_tag}" >/dev/null; then
    echo "Tag ${next_tag} was not created."
    return 1
  fi

  local current_sha
  current_sha="$(git rev-parse "origin/${branch}")"
  if [[ "$current_sha" == "$trigger_sha" ]]; then
    echo "Branch ${branch} HEAD unchanged after semantic-release failure."
    return 1
  fi

  local message committer parent_sha
  message="$(git log -1 --format=%s "$current_sha")"
  committer="$(git log -1 --format=%cn "$current_sha")"
  parent_sha="$(git rev-parse "${current_sha}^")"

  if [[ "$message" != "chore(release):"* ]]; then
    echo "Branch HEAD ${current_sha} is not a chore(release) commit."
    return 1
  fi

  if [[ "$committer" != "semantic-release-bot" ]]; then
    echo "Branch HEAD ${current_sha} committer is '${committer}', expected semantic-release-bot."
    return 1
  fi

  if [[ "$parent_sha" != "$trigger_sha" ]]; then
    echo "Branch HEAD ${current_sha} parent is ${parent_sha}, expected trigger ${trigger_sha}."
    return 1
  fi

  NOTES_REF="$notes_ref"
  RELEASE_TAG="$next_tag"
  RELEASE_SHA="$current_sha"
  return 0
}

dry_run_log="$(mktemp)"
FORCE_COLOR=0 npx semantic-release --dry-run >"$dry_run_log" 2>&1 || {
  cat "$dry_run_log"
  exit 1
}

next_version="$(sed -nE 's/.*The next release version is ([^[:space:]]+).*/\1/p' "$dry_run_log" | tail -n 1)"

if [[ -n "$next_version" ]]; then
  next_tag="v${next_version}"
  notes_ref="semantic-release-v${next_version}"

  if git rev-parse -q --verify "refs/tags/$next_tag" >/dev/null; then
    tag_sha="$(git rev-list -n 1 "$next_tag")"
    if ! git merge-base --is-ancestor "$tag_sha" HEAD; then
      echo "Skipping semantic-release: tag $next_tag exists outside current branch history at $tag_sha."
      echo "Likely stale protected prerelease tag after history rewrite."
      exit 0
    fi

    echo "Tag ${next_tag} already exists at ${tag_sha} on current branch history."
    echo "Release ${next_version} was already published; skipping re-release and repairing git notes if needed."
    configure_git_auth || true
    retry_git_notes_push "$notes_ref" || warn "Git notes push failed; continuing without blocking CI."
    exit 0
  fi
fi

branch="${GITHUB_REF_NAME:-$(git rev-parse --abbrev-ref HEAD)}"
trigger_sha="$(git rev-parse HEAD)"
sr_log="$(mktemp)"

set +e
npx semantic-release 2>&1 | tee "$sr_log"
sr_exit="${PIPESTATUS[0]}"
set -e

if [[ "$sr_exit" -eq 0 ]]; then
  exit 0
fi

if [[ -z "$next_version" ]]; then
  echo "semantic-release failed and no release version was planned."
  exit "$sr_exit"
fi

next_tag="v${next_version}"
notes_ref="semantic-release-v${next_version}"
if grep -Fq "refs/notes/${notes_ref}" "$sr_log"; then
  :
elif grep -Fq "fatal: tag '${next_tag}' already exists" "$sr_log" || grep -Fq "fatal: tag \"${next_tag}\" already exists" "$sr_log"; then
  echo "semantic-release failed because ${next_tag} already exists; treating as already published."
  configure_git_auth || true
  retry_git_notes_push "$notes_ref" || warn "Git notes push failed; continuing without blocking CI."
  exit 0
else
  echo "semantic-release failed for a reason other than git notes push or an existing tag."
  exit "$sr_exit"
fi

NOTES_REF=""
RELEASE_TAG=""
RELEASE_SHA=""
if ! verify_published_release "$trigger_sha" "$branch" "$next_version"; then
  exit "$sr_exit"
fi

echo "Release ${next_version} (${RELEASE_TAG} @ ${RELEASE_SHA}) was published; recovering from git notes push failure."

configure_git_auth
if retry_git_notes_push "$NOTES_REF"; then
  exit 0
fi

warn "Release ${next_version} was published but git notes push failed after retries."
warn "Continuing with workflow success so develop required checks are not left failing."
exit 0
