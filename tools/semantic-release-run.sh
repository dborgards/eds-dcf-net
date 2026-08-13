#!/usr/bin/env bash
# Runs semantic-release and recovers when GitHub rejects the git-notes push.
#
# semantic-release 25 lifecycle (index.js): prepare (changelog + git commit) →
# tag → addNote → push tags → pushNotes → publish plugins. NuGet
# (tools/semantic-release-publish.sh) and the GitHub release therefore have not
# run when pushNotes fails. Recovery must finish that publish, not treat the
# tagged commit as a completed release.
set -euo pipefail

warn() {
  echo "Warning: $*" >&2
}

notes_ref_for() {
  local version="$1"
  echo "semantic-release-v${version}"
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

tag_commit() {
  git rev-parse "$1^{}"
}

ensure_git_notes() {
  local version="$1"
  local tag="v${version}"
  local notes_ref
  notes_ref="$(notes_ref_for "$version")"
  local commit
  local channel_json

  commit="$(tag_commit "$tag")"
  git fetch origin "+refs/notes/*:refs/notes/*" || true

  if git notes --ref "$notes_ref" show "$commit" >/dev/null 2>&1; then
    echo "Git notes ${notes_ref} already exist for ${tag} (${commit})."
    return 0
  fi

  if [[ "$version" == *-* ]]; then
    channel_json='{"channels":["beta"]}'
  else
    channel_json='{"channels":[null]}'
  fi

  # Attach notes to the peeled commit. semantic-release reads them via
  # `git log`, which does not see notes stored on an annotated tag object.
  git notes --ref "$notes_ref" add -f -m "$channel_json" "$commit"
}

copy_release_packages() {
  local source_dir="$1"
  local dest_dir="$2"
  local version="$3"
  local file

  mkdir -p "$dest_dir"
  for file in \
    "${source_dir}/EdsDcfNet.${version}.nupkg" \
    "${source_dir}/EdsDcfNet.${version}.snupkg" \
    "${source_dir}/bom.cdx.json" \
    "${source_dir}/sbom.spdx.json"
  do
    if [[ -f "$file" ]]; then
      cp -f "$file" "$dest_dir/"
    fi
  done
}

remove_worktree_best_effort() {
  local worktree="$1"

  if [[ -z "$worktree" || ! -e "$worktree" ]]; then
    return 0
  fi

  # MSBuild node reuse on windows-latest often keeps files locked after pack.
  # Disable the server and shut it down before remove; never fail the release
  # if cleanup still cannot delete the tree (the runner is ephemeral).
  MSBUILDDISABLENODEREUSE=1 dotnet build-server shutdown >/dev/null 2>&1 || true
  if git worktree remove --force "$worktree"; then
    return 0
  fi

  warn "Could not remove worktree ${worktree}; continuing because publish already finished."
  git worktree prune || true
}

complete_release_publish() {
  local version="$1"
  local tag="v${version}"
  local repo_root
  repo_root="$(pwd)"
  local packages_dir="${repo_root}/packages"
  local worktree=""
  local notes_file
  local -a assets=()
  local -a cmd
  local file

  echo "Completing publish for ${version} (NuGet + GitHub release)..."

  # Clear on fire: RETURN traps are global and would otherwise leak to the
  # caller; with set -u that re-expands the now-out-of-scope local worktree.
  trap 'trap - RETURN; remove_worktree_best_effort "$worktree"' RETURN

  if [[ "$(tag_commit "$tag")" != "$(git rev-parse HEAD^{})" ]]; then
    worktree="$(mktemp -d)"
    git worktree add --detach "$worktree" "$tag"
    (
      cd "$worktree"
      export MSBUILDDISABLENODEREUSE=1
      dotnet restore
      bash "${repo_root}/tools/semantic-release-publish.sh" "$version"
    )
    copy_release_packages "${worktree}/packages" "$packages_dir" "$version"
    remove_worktree_best_effort "$worktree"
    worktree=""
  else
    bash ./tools/semantic-release-publish.sh "$version"
  fi

  if ! gh release view "$tag" >/dev/null 2>&1; then
    notes_file="$(mktemp)"
    git log -1 --format=%b "$tag" >"$notes_file"

    for file in \
      "${packages_dir}/EdsDcfNet.${version}.nupkg" \
      "${packages_dir}/EdsDcfNet.${version}.snupkg" \
      "${packages_dir}/bom.cdx.json" \
      "${packages_dir}/sbom.spdx.json"
    do
      if [[ -f "$file" ]]; then
        assets+=("$file")
      fi
    done

    cmd=(gh release create "$tag" --title "$tag" --notes-file "$notes_file")
    if [[ "$version" == *-* ]]; then
      cmd+=(--prerelease)
    fi
    if ((${#assets[@]} > 0)); then
      cmd+=("${assets[@]}")
    fi

    "${cmd[@]}"
  else
    echo "GitHub release ${tag} already exists."
  fi
}

repair_notes_and_publish() {
  local version="$1"
  local notes_ref
  notes_ref="$(notes_ref_for "$version")"

  echo "Finishing release ${version}: git notes are not a completed publish."
  complete_release_publish "$version"
  # Channel notes are how later runs treat the tag as lastRelease. Push them
  # only after NuGet and the GitHub release exist, otherwise a notes-only
  # success would skip this repair path and leave artifacts unpublished.
  configure_git_auth || warn "Could not configure git auth for notes push."
  ensure_git_notes "$version" || warn "Could not add local git notes for v${version}."
  retry_git_notes_push "$notes_ref" || warn "Git notes push failed after retries; publish already completed."
}

dry_run_log="$(mktemp)"
FORCE_COLOR=0 npx semantic-release --dry-run >"$dry_run_log" 2>&1 || {
  cat "$dry_run_log"
  exit 1
}

next_version="$(sed -nE 's/.*The next release version is ([^[:space:]]+).*/\1/p' "$dry_run_log" | tail -n 1)"

if [[ -n "$next_version" ]]; then
  next_tag="v${next_version}"
  if git rev-parse -q --verify "refs/tags/$next_tag" >/dev/null; then
    tag_sha="$(git rev-list -n 1 "$next_tag")"
    if ! git merge-base --is-ancestor "$tag_sha" HEAD; then
      echo "Skipping semantic-release: tag $next_tag exists outside current branch history at $tag_sha."
      echo "Likely stale protected prerelease tag after history rewrite."
      exit 0
    fi

    echo "Tag ${next_tag} already exists at ${tag_sha} on current branch history."
    echo "Skipping a second semantic-release run and completing any unfinished publish."
    repair_notes_and_publish "$next_version"
    exit 0
  fi
fi

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
notes_ref="$(notes_ref_for "$next_version")"
if grep -Fq "refs/notes/${notes_ref}" "$sr_log"; then
  echo "semantic-release failed while pushing git notes; completing publish plugins."
  repair_notes_and_publish "$next_version"
  exit 0
fi

if grep -Fq "fatal: tag '${next_tag}' already exists" "$sr_log" || grep -Fq "fatal: tag \"${next_tag}\" already exists" "$sr_log"; then
  echo "semantic-release failed because ${next_tag} already exists; completing any unfinished publish."
  repair_notes_and_publish "$next_version"
  exit 0
fi

echo "semantic-release failed for a reason other than git notes push or an existing tag."
exit "$sr_exit"
