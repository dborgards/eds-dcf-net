#!/usr/bin/env bash
set -euo pipefail

branch_name="${GITHUB_REF_NAME:-}"
if [[ -z "$branch_name" ]]; then
  branch_name="$(git rev-parse --abbrev-ref HEAD)"
fi

if [[ "$branch_name" != "main" ]]; then
  exit 0
fi

develop_ref="origin/develop"
if ! git rev-parse -q --verify "${develop_ref}^{commit}" >/dev/null; then
  develop_ref="develop"
fi

if ! git rev-parse -q --verify "${develop_ref}^{commit}" >/dev/null; then
  echo "Skipping orphaned prerelease fallback: no develop ref is available." >&2
  exit 0
fi

latest_beta="$(git tag --merged "$develop_ref" --list 'v*-beta.*' | sort -V | tail -n 1)"
if [[ -z "$latest_beta" ]]; then
  exit 0
fi

beta_base="${latest_beta%%-beta.*}"
stable_tag="${beta_base}"
stable_version="${beta_base#v}"

if git rev-parse -q --verify "refs/tags/${stable_tag}" >/dev/null 2>&1; then
  exit 0
fi

beta_sha="$(git rev-list -n 1 "$latest_beta")"
if git merge-base --is-ancestor "$beta_sha" HEAD; then
  exit 0
fi

if ! git diff --quiet HEAD "$beta_sha" -- . ':(exclude)CHANGELOG.md' ':(exclude)src/**/*.csproj'; then
  echo "Skipping orphaned prerelease fallback: HEAD does not match ${latest_beta} outside release artifacts." >&2
  exit 0
fi

latest_stable="$(git tag --merged HEAD --list 'v[0-9]*.[0-9]*.[0-9]*' | sed -nE '/-[0-9A-Za-z]/!p' | sort -V | tail -n 1)"
if [[ -z "$latest_stable" ]]; then
  echo "Skipping orphaned prerelease fallback: no stable tag exists in main history." >&2
  exit 0
fi

latest_stable_version="${latest_stable#v}"
IFS=. read -r stable_major stable_minor stable_patch <<< "$stable_version"
IFS=. read -r latest_major latest_minor latest_patch <<< "$latest_stable_version"

release_type="patch"
if (( stable_major > latest_major )); then
  release_type="major"
elif (( stable_major == latest_major && stable_minor > latest_minor )); then
  release_type="minor"
elif (( stable_major != latest_major || stable_minor != latest_minor || stable_patch <= latest_patch )); then
  exit 0
fi

echo "Detected orphaned prerelease ${latest_beta} (commit ${beta_sha} not in main history)." >&2
echo "Forcing a ${release_type} release to promote the prerelease to stable." >&2
echo "$release_type"
