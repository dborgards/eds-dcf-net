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

release_artifact_names() {
  local version="$1"
  printf '%s\n' \
    "EdsDcfNet.${version}.nupkg" \
    "EdsDcfNet.${version}.snupkg" \
    "bom.cdx.json" \
    "sbom.spdx.json"
}

copy_release_packages() {
  local source_dir="$1"
  local dest_dir="$2"
  local version="$3"
  local name
  local nupkg="${dest_dir}/EdsDcfNet.${version}.nupkg"

  mkdir -p "$dest_dir"
  while IFS= read -r name; do
    if [[ -f "${source_dir}/${name}" ]]; then
      if ! cp -f "${source_dir}/${name}" "$dest_dir/"; then
        warn "Could not copy ${name} out of the worktree."
      fi
    fi
  done < <(release_artifact_names "$version")

  if [[ ! -f "$nupkg" ]]; then
    echo "Expected package not found after copy: ${nupkg}" >&2
    return 1
  fi
}

remove_worktree_best_effort() {
  local worktree="$1"

  if [[ -z "$worktree" || ! -e "$worktree" ]]; then
    return 0
  fi

  # VBCSCompiler / MSBuild nodes can keep files locked after pack. Shut them
  # down before remove; never fail the release if cleanup still cannot delete
  # the tree (the runner is ephemeral). shutdown does not read
  # MSBUILDDISABLENODEREUSE; reuse is disabled at pack time instead.
  dotnet build-server shutdown >/dev/null 2>&1 || true
  if git worktree remove --force "$worktree"; then
    return 0
  fi

  # Runner is ephemeral; a locked tree must not fail the job after pack.
  warn "Could not remove worktree ${worktree}; continuing (ephemeral runner)."
  git worktree prune || true
}

local_release_assets() {
  local version="$1"
  local packages_dir="$2"
  local name

  while IFS= read -r name; do
    if [[ -f "${packages_dir}/${name}" ]]; then
      printf '%s\n' "$name"
    fi
  done < <(release_artifact_names "$version")
}

# Asset names GitHub reports as fully uploaded. Anything still in the `starter`
# state is a half-finished upload from the aborted run and must be re-sent.
#
# A failed probe must fail the repair, for the same reason as release_is_draft:
# an unreadable asset list silently becomes "nothing is uploaded", which either
# re-clobbers a healthy release or, once the channel note is pushed, hides a
# release that is genuinely missing artifacts from every later repair run.
uploaded_release_assets() {
  local tag="$1"

  gh release view "$tag" --json assets \
    --jq '.assets[] | select(.state == "uploaded") | .name' || {
    echo "Could not list assets for ${tag}." >&2
    return 1
  }
}

# Prints "true" or "false". A failed or unreadable probe must fail the repair:
# treating a missed read as "not a draft" would skip --draft=false, after which
# channel notes are pushed and later runs leave the GitHub release unpublished.
# Strip CR so Git Bash on windows-latest does not turn `true\r` into a miss.
release_is_draft() {
  local tag="$1"
  local is_draft

  is_draft="$(gh release view "$tag" --json isDraft --jq .isDraft)" || {
    echo "Could not determine draft status for ${tag}." >&2
    return 1
  }
  is_draft="${is_draft//$'\r'/}"
  case "$is_draft" in
    true|false)
      printf '%s\n' "$is_draft"
      ;;
    *)
      echo "Could not determine draft status for ${tag} (got: ${is_draft})." >&2
      return 1
      ;;
  esac
}

# `gh release create` with assets creates a draft, uploads each asset, then
# publishes. A run that dies partway leaves a release that `gh release view`
# finds but that is missing assets, still a draft, or both. Resume whichever
# step did not finish instead of treating mere existence as success.
resume_release_publish() {
  local version="$1"
  local tag="v${version}"
  local packages_dir="$2"
  local -a missing=()
  local -a uploaded=()
  local name
  local resumed=0
  local is_draft
  local uploaded_raw

  # Capture before mapfile: a process substitution would discard the exit status
  # and turn a failed probe back into an empty "nothing uploaded" list.
  uploaded_raw="$(uploaded_release_assets "$tag")" || return 1
  # Strip CR so Git Bash on windows-latest does not break exact name matching.
  # Command substitution of `gh --jq` and a here-string can both attach CR.
  uploaded_raw="${uploaded_raw//$'\r'/}"
  mapfile -t uploaded <<<"$uploaded_raw"
  uploaded=("${uploaded[@]//$'\r'/}")

  while IFS= read -r name; do
    if ! printf '%s\n' "${uploaded[@]}" | grep -Fxq "$name"; then
      missing+=("${packages_dir}/${name}")
    fi
  done < <(local_release_assets "$version" "$packages_dir")

  if ((${#missing[@]} > 0)); then
    echo "Uploading ${#missing[@]} missing asset(s) to ${tag}."
    # --clobber replaces `starter` leftovers from the interrupted upload.
    gh release upload "$tag" "${missing[@]}" --clobber
    resumed=1
  fi

  is_draft="$(release_is_draft "$tag")" || return 1
  if [[ "$is_draft" == "true" ]]; then
    echo "Publishing draft release ${tag}."
    gh release edit "$tag" --draft=false
    resumed=1
  fi

  if ((resumed == 0)); then
    echo "GitHub release ${tag} already complete."
  fi
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
  local name

  echo "Completing publish for ${version} (NuGet + GitHub release)..."

  if [[ "$(tag_commit "$tag")" != "$(git rev-parse HEAD^{})" ]]; then
    worktree="$(mktemp -d)"
    git worktree add --detach "$worktree" "$tag"
    (
      cd "$worktree"
      export MSBUILDDISABLENODEREUSE=1
      # setup-dotnet installed the SDK named by the *current* checkout's
      # global.json. The tag's own global.json can pin an older feature band
      # that is not installed, and SDK selection rolls forward only within a
      # band, so restore would abort and the repair could never run for tags
      # predating an SDK bump. Deleting the pin is not the fix either: with no
      # global.json, .NET selects the latest installed SDK, which on a hosted
      # runner may be newer than — or a preview of — what the workflow chose.
      # Copy the current pin in, so the pack uses exactly the installed SDK.
      if [[ -f "${repo_root}/global.json" ]]; then
        cp -f "${repo_root}/global.json" global.json
      else
        rm -f global.json
      fi
      dotnet restore
      bash "${repo_root}/tools/semantic-release-publish.sh" "$version"
    )
    # Unlock packages before copy. The pack subshell already disabled node reuse;
    # this terminates leftover VBCSCompiler / MSBuild processes.
    dotnet build-server shutdown >/dev/null 2>&1 || true
    copy_release_packages "${worktree}/packages" "$packages_dir" "$version"
    remove_worktree_best_effort "$worktree"
  else
    bash ./tools/semantic-release-publish.sh "$version"
  fi

  if ! gh release view "$tag" >/dev/null 2>&1; then
    notes_file="$(mktemp)"
    git log -1 --format=%b "$tag" >"$notes_file"

    while IFS= read -r name; do
      if [[ -f "${packages_dir}/${name}" ]]; then
        assets+=("${packages_dir}/${name}")
      fi
    done < <(release_artifact_names "$version")

    cmd=(gh release create "$tag" --title "$tag" --notes-file "$notes_file")
    if [[ "$version" == *-* ]]; then
      cmd+=(--prerelease)
    fi
    if ((${#assets[@]} > 0)); then
      cmd+=("${assets[@]}")
    fi

    "${cmd[@]}"
  else
    resume_release_publish "$version" "$packages_dir"
  fi
}

# 0 = version is on nuget.org, 1 = absent, 2 = could not ask.
nuget_has_version() {
  local version="$1"
  local body

  if ! body="$(curl -fsSL "https://api.nuget.org/v3-flatcontainer/edsdcfnet/index.json")"; then
    return 2
  fi

  printf '%s' "$body" | grep -Fq "\"${version}\""
}

# Reads one --jq field off a release. 0 = value on stdout, 1 = the release
# does not exist, 2 = the question could not be answered.
#
# gh exits 1 for every failure, so the message is the only thing separating
# "no such release" from an outage or a missing token. Requiring exit 1 *and*
# a not-found message keeps an unrelated failure (127 for a missing gh, 4 for
# auth) from being read as a confirmed absence.
gh_release_field() {
  local tag="$1"
  local json="$2"
  local filter="$3"
  local err_file
  local out
  local status=0

  err_file="$(mktemp)"
  out="$(gh release view "$tag" --json "$json" --jq "$filter" 2>"$err_file")" || status=$?

  if ((status == 0)); then
    rm -f "$err_file"
    printf '%s\n' "$out"
    return 0
  fi

  if ((status == 1)) &&
    grep -qiE "release not found|could not resolve to a release|HTTP 404" "$err_file"; then
    rm -f "$err_file"
    return 1
  fi

  cat "$err_file" >&2
  rm -f "$err_file"
  return 2
}

# 0 = published with its package asset, 1 = missing/draft/incomplete,
# 2 = could not determine.
#
# Only the nupkg is required. Symbols and the SBOMs are best-effort in
# semantic-release-publish.sh, so demanding them here would report every
# release incomplete and re-run the repair on every build.
github_release_complete() {
  local tag="$1"
  local version="${tag#v}"
  local is_draft
  local uploaded_raw
  local -a uploaded=()
  local status=0

  is_draft="$(gh_release_field "$tag" isDraft .isDraft)" || status=$?
  if ((status != 0)); then
    return "$status"
  fi
  if [[ "${is_draft//$'\r'/}" == "true" ]]; then
    return 1
  fi

  status=0
  uploaded_raw="$(gh_release_field "$tag" assets \
    '.assets[] | select(.state == "uploaded") | .name')" || status=$?
  if ((status != 0)); then
    return "$status"
  fi

  uploaded_raw="${uploaded_raw//$'\r'/}"
  mapfile -t uploaded <<<"$uploaded_raw"
  uploaded=("${uploaded[@]//$'\r'/}")

  printf '%s\n' "${uploaded[@]}" | grep -Fxq "EdsDcfNet.${version}.nupkg"
}

# semantic-release pushes the tag and channel note before the publish plugins
# run, so a NuGet or GitHub failure leaves a tag that later dry runs accept as
# lastRelease. Those runs plan nothing and exit 0, which would strand the
# partial release forever behind a green build. Check the last tag before
# accepting a no-op result.
# The last release tag *on this branch's channel*. .releaserc.json runs two
# channels: stable on main, beta prereleases on develop. An unfiltered
# `git describe` returns whichever tag is nearest, so on develop — where main
# is merged back and stable tags become reachable — it can return the stable
# tag and pronounce the release healthy while the beta this branch actually
# publishes is the broken one. On main the reverse holds: merged develop
# history makes beta tags reachable, so prereleases are excluded there.
last_release_tag() {
  local branch="${GITHUB_REF_NAME:-}"
  local -a keep
  local -a candidates=()

  if [[ -z "$branch" ]]; then
    branch="$(git rev-parse --abbrev-ref HEAD 2>/dev/null || true)"
  fi

  case "$branch" in
    develop)
      # semantic-release's get-last-release accepts, on a prerelease branch,
      # this channel's own prereleases *and* every non-prerelease tag, then
      # takes the SemVer-highest of the combined set:
      #
      #   ((branch.type === "prerelease" && <channel prerelease>) ||
      #     !semver.prerelease(tag.version))
      #
      # A stable tag merged back from main is therefore eligible on develop and
      # can outrank the newest beta, so both forms have to be considered here.
      keep=(grep -E '^v[0-9]+\.[0-9]+\.[0-9]+(-beta\.[0-9]+)?$')
      ;;
    main)
      keep=(grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$')
      ;;
    *)
      # The workflow also accepts workflow_dispatch, which can target any
      # branch, but only main and develop are channels in .releaserc.json. Such
      # a run plans no version, and verifying "the last release" from there
      # would mean rebuilding and repairing a production release with recovery
      # code taken from an unreleased branch.
      echo "Branch '${branch:-unknown}' is not a release channel; skipping verification." >&2
      return 1
      ;;
  esac

  # Highest version among the reachable channel tags, not the nearest one.
  # `git describe` selects by ancestry distance, so on non-linear history — a
  # merge back, or one of the tag rewrites this script already handles — it can
  # return an older tag while a higher, partially published release is the real
  # lastRelease, and the run would go green without repairing it.
  # semantic-release picks its lastRelease with semver.rcompare, so match that.
  #
  # versionsort.suffix is required now that prereleases and stable tags share
  # one list: git's plain version sort ranks v1.12.0-beta.15 *above* v1.12.0,
  # the reverse of SemVer. Naming the suffix restores SemVer's order, which was
  # checked against git rather than assumed.
  mapfile -t candidates < <(
    git -c versionsort.suffix=-beta. tag --merged HEAD --list 'v*' \
      --sort=-v:refname 2>/dev/null | tr -d '\r' | "${keep[@]}"
  )

  if ((${#candidates[@]} == 0)) || [[ -z "${candidates[0]}" ]]; then
    return 1
  fi

  printf '%s\n' "${candidates[0]}"
}

verify_last_release() {
  local tag
  local version
  local github_status
  local nuget_status

  if ! tag="$(last_release_tag)"; then
    echo "No release to verify for this run."
    return 0
  fi

  tag="${tag//$'\r'/}"
  version="${tag#v}"

  # Capture through `|| var=$?`: a bare call would hit errexit on any nonzero
  # status and kill the run before the status could be read, so an incomplete
  # release would fail the build instead of being repaired.
  github_status=0
  github_release_complete "$tag" || github_status=$?

  nuget_status=0
  nuget_has_version "$version" || nuget_status=$?

  # A confirmed incomplete result outranks an indeterminate companion probe.
  # One side saying "this really is missing" is evidence; the other side being
  # unreachable is only absence of evidence, and letting the unknown mask the
  # confirmation would strand the release: the run continues, the next planned
  # version publishes, and from then on only that newer tag is ever inspected.
  # Repairing on the confirmation either fixes it or fails the run — both leave
  # the release recoverable, which returning success here would not.
  if ((github_status == 1)) || ((nuget_status == 1)); then
    echo "Last release ${tag} is incomplete (github=${github_status}, nuget=${nuget_status}); repairing."
    repair_notes_and_publish "$version"

    # The repair leaves its artifacts in packages/, and .releaserc.json attaches
    # that directory's packages and SBOMs to a release by path. When a newly
    # planned release follows in this same run, they would be hung on the new
    # release too, so drop them now that the repair has published them.
    #
    # The SBOMs need clearing as much as the packages do: they have fixed names,
    # but semantic-release-publish.sh only overwrites them on the happy path.
    # generate_spdx_sbom returns early when CycloneDX generation failed, before
    # it deletes the old sbom.spdx.json, which would then ship as the new
    # release's SPDX SBOM. Absent beats wrong — the SBOMs are best-effort.
    rm -f "packages/EdsDcfNet.${version}.nupkg" \
      "packages/EdsDcfNet.${version}.snupkg" \
      packages/bom.cdx.json \
      packages/sbom.spdx.json
    return 0
  fi

  # Nothing confirmed broken, but something could not be read. This runs on
  # every push, so say so loudly rather than failing the build or repairing on
  # a guess.
  if ((github_status == 2)) || ((nuget_status == 2)); then
    warn "Could not verify ${tag} (github=${github_status}, nuget=${nuget_status}); leaving it as-is."
    return 0
  fi

  echo "Last release ${tag} is complete on GitHub and NuGet."
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

# Verify the previous release before acting on a newly planned one, not only on
# a no-op run. A partial release is visible as "the last release" just until the
# next commit warrants a version: from then on every dry run plans that newer
# version, and a check confined to no-op runs would inspect the new, healthy tag
# and never look back at the broken one, which stays unpublished for good.
#
# This runs before the release below on purpose. If the previous release cannot
# be repaired the run stops here, rather than stacking a new release on top of a
# broken one.
verify_last_release

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
