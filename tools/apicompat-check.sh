#!/usr/bin/env bash
# Runs Microsoft.DotNet.ApiCompat (apicompat) comparing the PR's built
# assemblies against the same assemblies built from the PR's base branch.
#
# Usage: tools/apicompat-check.sh <head-build-output-dir> <base-build-output-dir> [work-dir]
#   <head-build-output-dir>  Directory with one sub-folder per TFM containing
#                            the PR branch's built EdsDcfNet.dll (e.g.
#                            src/EdsDcfNet/bin/Release).
#   <base-build-output-dir>  Same layout, built from the PR's base branch
#                            (develop or main) — the public API this PR is
#                            compared against.
#   [work-dir]               Scratch directory for the diff report (default:
#                            .apicompat).
#
# Exit codes:
#   0  no compatibility differences (or only suppressed ones)
#   1  compatibility differences found — report written to <work-dir>/apicompat-diff.md
#   2  infrastructure failure (missing tool, missing build output)
#
# Comparing against the PR's own base branch rather than the latest stable
# NuGet package avoids blaming a PR for an incompatibility some *other*,
# already-merged PR introduced: `develop` only ever publishes beta
# pre-releases, so an accepted breaking change can sit there unreleased as
# stable for a while, and every subsequent PR into develop would otherwise
# inherit that blame. For a develop -> main release PR, the base branch
# (main) is exactly the last published stable version, so this reduces to
# the original "vs latest stable" comparison there.
#
# When differences are found the report is written to <work-dir>/apicompat-diff.md
# so the caller can post it as a PR comment.

set -euo pipefail

HEAD_BUILD_OUTPUT="${1:?usage: apicompat-check.sh <head-build-output-dir> <base-build-output-dir> [work-dir]}"
BASE_BUILD_OUTPUT="${2:?usage: apicompat-check.sh <head-build-output-dir> <base-build-output-dir> [work-dir]}"
WORK_DIR="${3:-.apicompat}"
SUPPRESSION_FILE="src/EdsDcfNet/ApiCompatSuppressions.xml"

die() { echo "apicompat-check: $*" >&2; exit 2; }

command -v apicompat >/dev/null || die "apicompat not found — install with: dotnet tool install -g Microsoft.DotNet.ApiCompat.Tool"
[ -d "${BASE_BUILD_OUTPUT}" ] || die "base branch build output not found: ${BASE_BUILD_OUTPUT}"

mkdir -p "${WORK_DIR}"

# --- Compare per TFM ---------------------------------------------------------
# Compare each built TFM against the same TFM in the base branch's build
# output. When the base branch does not ship that TFM (e.g. it was just added
# in this PR), fall back to the netstandard2.0 asset: the public surface is
# identical across TFMs, so this is still a meaningful guard.
DIFF_FILE="${WORK_DIR}/apicompat-diff.md"
: > "${DIFF_FILE}"
FAILED=0
COMPARED=0

SUPPRESSION_ARGS=()
if [ -f "${SUPPRESSION_FILE}" ]; then
  SUPPRESSION_ARGS=(--suppression-file "${SUPPRESSION_FILE}")
fi

shopt -s nullglob
for tfm_dir in "${HEAD_BUILD_OUTPUT}"/*/; do
  tfm="$(basename "${tfm_dir}")"
  right="${tfm_dir}EdsDcfNet.dll"
  [ -f "${right}" ] || continue

  if [ -f "${BASE_BUILD_OUTPUT}/${tfm}/EdsDcfNet.dll" ]; then
    left="${BASE_BUILD_OUTPUT}/${tfm}/EdsDcfNet.dll"
    baseline_tfm="${tfm}"
  elif [ -f "${BASE_BUILD_OUTPUT}/netstandard2.0/EdsDcfNet.dll" ]; then
    left="${BASE_BUILD_OUTPUT}/netstandard2.0/EdsDcfNet.dll"
    baseline_tfm="netstandard2.0 (fallback)"
  else
    echo "apicompat-check: no compatible base-branch asset for ${tfm} — skipped" >&2
    continue
  fi

  echo "Comparing ${tfm}: ${left} -> ${right}"
  COMPARED=$((COMPARED + 1))

  set +e
  output=$(apicompat --left "${left}" --right "${right}" "${SUPPRESSION_ARGS[@]}" 2>&1)
  rc=$?
  set -e

  if [ "${rc}" -ne 0 ]; then
    FAILED=1
    {
      echo "### \`${tfm}\` vs base branch (\`${baseline_tfm}\`)"
      echo
      echo '```'
      echo "${output}"
      echo '```'
      echo
    } >> "${DIFF_FILE}"
  fi
done

[ "${COMPARED}" -gt 0 ] || die "no built EdsDcfNet.dll found under ${HEAD_BUILD_OUTPUT}"

if [ "${FAILED}" -ne 0 ]; then
  echo "apicompat-check: compatibility differences found vs base branch — see ${DIFF_FILE}"
  exit 1
fi

echo "apicompat-check: public API is compatible with the base branch"
exit 0
