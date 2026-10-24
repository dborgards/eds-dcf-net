#!/usr/bin/env bash
# Ensure the contents-only release token cannot be used for issue interactions.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
config="$root/.releaserc.json"

command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }

github_config=$(jq -c '
  .plugins[]
  | select(type == "array" and .[0] == "@semantic-release/github")
  | .[1]
' "$config")

if [[ -z "$github_config" || "$github_config" == "null" ]]; then
  echo "FAIL: @semantic-release/github config is missing" >&2
  exit 1
fi

if ! jq -e '
  .successCommentCondition == false
  and .failCommentCondition == false
  and .releasedLabels == false
  and (has("successComment") | not)
  and (has("failComment") | not)
' <<< "$github_config" >/dev/null; then
  echo "FAIL: GitHub release issue/PR interactions must use disabled conditions without deprecated failComment/successComment flags" >&2
  exit 1
fi

echo "GitHub release issue and pull-request interactions are disabled."
