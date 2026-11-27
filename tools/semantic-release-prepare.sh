#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

next_version="${1:-}"
if [[ -z "$next_version" ]]; then
  echo "Usage: $0 <next-version>" >&2
  exit 1
fi

branch_name="${GITHUB_REF_NAME:-}"
if [[ -z "$branch_name" ]]; then
  branch_name="$(git rev-parse --abbrev-ref HEAD)"
fi

if [[ "$branch_name" == "main" ]]; then
  tmp_file="$(mktemp)"
  awk '
    /^## / {
      skip = ($0 ~ /^## \[?[0-9]+\.[0-9]+\.[0-9]+-[0-9A-Za-z]/)
    }
    !skip { print }
  ' CHANGELOG.md > "$tmp_file"
  mv "$tmp_file" CHANGELOG.md
fi

# No project file carries a version (Directory.Build.props, VersionPrefix 0.0.0):
# tools/semantic-release-publish.sh passes it to the pack. Rewriting a csproj here
# would change the version the next restore sees, and packages.lock.json records
# the version of every referenced project, so the release commit would leave the
# committed lock files stale.
