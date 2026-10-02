#!/usr/bin/env bash
# Enforce the repository line-coverage gate from Cobertura reports.
#
# Used by both .github/workflows/build.yml and semantic-release.yml so PR and
# release runs apply the same metric.
#
# Metric:
#   Sum lines-covered and lines-valid from every coverage.cobertura.xml under
#   the results directory (sorted paths for stable logs), then:
#     percent = (sum(lines-covered) / sum(lines-valid)) * 100
#   Fail if percent < COVERAGE_MIN_PERCENT (default 95.0).
#
# Why sum counts instead of averaging line-rate attributes:
#   Multiple Cobertura files (e.g. one per test host TFM) must be weighted by
#   lines-valid. Averaging line-rate or taking only the first file can pass PR
#   CI while failing release (or the reverse) for the same commit.
#
# Search root:
#   Pass the directory given to `dotnet test --results-directory` for this
#   run. CI creates that directory with mktemp under $RUNNER_TEMP, outside
#   the checkout. Files in the working tree are not inputs: a force-added
#   coverage.cobertura.xml, a leftover TestResults run, or a report written
#   beside EdsDcfNet.Checker. The checker assembly is also marked
#   ExcludeFromCodeCoverage, so its lines are not part of the library report.
#
# Environment:
#   COVERAGE_MIN_PERCENT  Minimum allowed percent (default: 95.0)
#   GITHUB_OUTPUT         When set, writes coverage_files and coverage_percent
#
# Usage:
#   tools/enforce-coverage-threshold.sh <results-directory>
set -euo pipefail

if [[ $# -ne 1 || -z "${1:-}" ]]; then
  echo "Usage: tools/enforce-coverage-threshold.sh <results-directory>" >&2
  echo "Pass the fresh directory created under RUNNER_TEMP for this test run." >&2
  exit 1
fi

search_root="$1"
# Git Bash on windows-latest exposes RUNNER_TEMP as a Windows path. find needs
# a POSIX path; cygpath is absent on the Linux runners.
if command -v cygpath >/dev/null 2>&1; then
  search_root="$(cygpath -u "$search_root")"
fi
min_percent="${COVERAGE_MIN_PERCENT:-95.0}"

if [[ ! -d "$search_root" ]]; then
  echo "Search root is not a directory: $search_root" >&2
  exit 1
fi

coverage_files=()
while IFS= read -r coverage_file; do
  coverage_files+=("$coverage_file")
done < <(find "$search_root" -type f -name 'coverage.cobertura.xml' -print | sort)

if (( ${#coverage_files[@]} == 0 )); then
  echo "No coverage.cobertura.xml found under $search_root."
  exit 1
fi

# Portable join (Bash 3.2+): comma-separated list for Codecov upload.
coverage_files_csv=""
for coverage_file in "${coverage_files[@]}"; do
  if [[ -z "$coverage_files_csv" ]]; then
    coverage_files_csv="$coverage_file"
  else
    coverage_files_csv="${coverage_files_csv},${coverage_file}"
  fi
done

echo "Found ${#coverage_files[@]} coverage report(s)."
for coverage_file in "${coverage_files[@]}"; do
  echo "  - $coverage_file"
done

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "coverage_files=${coverage_files_csv}" >> "$GITHUB_OUTPUT"
fi

total_lines_covered=0
total_lines_valid=0
for coverage_file in "${coverage_files[@]}"; do
  lines_covered="$(awk '
    /<coverage / {
      if (match($0, /lines-covered="[0-9]+"/)) {
        value = substr($0, RSTART, RLENGTH)
        sub(/lines-covered="/, "", value)
        sub(/"$/, "", value)
        print value
        exit
      }
    }
  ' "$coverage_file")"
  lines_valid="$(awk '
    /<coverage / {
      if (match($0, /lines-valid="[0-9]+"/)) {
        value = substr($0, RSTART, RLENGTH)
        sub(/lines-valid="/, "", value)
        sub(/"$/, "", value)
        print value
        exit
      }
    }
  ' "$coverage_file")"

  if [[ -z "$lines_covered" || -z "$lines_valid" ]]; then
    echo "Could not read lines-covered/lines-valid from $coverage_file"
    exit 1
  fi

  total_lines_covered=$((total_lines_covered + lines_covered))
  total_lines_valid=$((total_lines_valid + lines_valid))
done

if (( total_lines_valid == 0 )); then
  echo "Could not calculate coverage: lines-valid sum is zero."
  exit 1
fi

coverage_percent="$(awk "BEGIN { printf \"%.2f\", ($total_lines_covered / $total_lines_valid) * 100 }")"
echo "Line coverage: ${coverage_percent}% (required: >= ${min_percent}%)"
echo "Aggregated lines-covered=${total_lines_covered} lines-valid=${total_lines_valid}"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "coverage_percent=${coverage_percent}" >> "$GITHUB_OUTPUT"
fi

awk "BEGIN { exit !($coverage_percent >= $min_percent) }" || {
  echo "Coverage gate failed."
  exit 1
}
