#!/usr/bin/env bash
# Report line coverage for lines added relative to a Git base ref.
#
# Usage:
#   tools/changed-lines-coverage.sh <results-directory> <base-ref> [paths...]
#
# The default path is src/. Additional paths limit both the Git diff and the
# report. COVERAGE_MIN_PERCENT defaults to 95.0. The Git diff includes committed,
# staged, and unstaged changes relative to the merge base.
set -euo pipefail
export LC_ALL=C

if [[ $# -lt 2 || -z "${1:-}" || -z "${2:-}" ]]; then
  echo "Usage: tools/changed-lines-coverage.sh <results-directory> <base-ref> [paths...]" >&2
  exit 2
fi

script_dir=$(cd "$(dirname "$0")" && pwd)
results=$1
base_ref=$2
shift 2

# Python understands Windows paths, but this script's results argument may come
# from a .NET command on Git Bash. Normalize it before crossing that boundary.
if command -v cygpath >/dev/null 2>&1; then
  results=$(cygpath -u "$results")
fi

exec python3 "$script_dir/changed-lines-coverage.py" "$results" "$base_ref" "$@"
