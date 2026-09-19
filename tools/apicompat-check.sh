#!/usr/bin/env bash
# Downloads the latest stable EdsDcfNet package from nuget.org and runs
# Microsoft.DotNet.ApiCompat (apicompat) against the locally built assemblies.
#
# Usage: tools/apicompat-check.sh <build-output-dir> [work-dir]
#   <build-output-dir>  Directory that contains one sub-folder per TFM with the
#                       built EdsDcfNet.dll (e.g. src/EdsDcfNet/bin/Release).
#   [work-dir]          Scratch directory for the baseline package and the diff
#                       report (default: .apicompat).
#
# Exit codes:
#   0  no compatibility differences (or only suppressed ones)
#   1  compatibility differences found — report written to <work-dir>/apicompat-diff.md
#   2  infrastructure failure (download, missing tool, missing build output)
#
# When differences are found the report is written to <work-dir>/apicompat-diff.md
# so the caller can post it as a PR comment.

set -euo pipefail

BUILD_OUTPUT="${1:?usage: apicompat-check.sh <build-output-dir> [work-dir]}"
WORK_DIR="${2:-.apicompat}"
PACKAGE_ID="edsdcfnet"
FLAT_CONTAINER="https://api.nuget.org/v3-flatcontainer/${PACKAGE_ID}"
SUPPRESSION_FILE="src/EdsDcfNet/ApiCompatSuppressions.xml"

die() { echo "apicompat-check: $*" >&2; exit 2; }

command -v curl >/dev/null || die "curl not found"
command -v jq >/dev/null || die "jq not found"
command -v unzip >/dev/null || die "unzip not found"
command -v apicompat >/dev/null || die "apicompat not found — install with: dotnet tool install -g Microsoft.DotNet.ApiCompat.Tool"

mkdir -p "${WORK_DIR}"

# --- Resolve the latest stable (non-prerelease) package version -------------
echo "Resolving latest stable ${PACKAGE_ID} package from nuget.org..."
BASELINE_VERSION=$(curl -fsSL "${FLAT_CONTAINER}/index.json" \
  | jq -r '[.versions[] | select(contains("-") | not)] | last // empty')
[ -n "${BASELINE_VERSION}" ] || die "no stable ${PACKAGE_ID} version found on nuget.org"
echo "Baseline: ${PACKAGE_ID} ${BASELINE_VERSION}"

# --- Download and extract the baseline package ------------------------------
BASELINE_DIR="${WORK_DIR}/baseline/${BASELINE_VERSION}"
if [ ! -d "${BASELINE_DIR}/lib" ]; then
  mkdir -p "${BASELINE_DIR}"
  curl -fsSL "${FLAT_CONTAINER}/${BASELINE_VERSION}/${PACKAGE_ID}.${BASELINE_VERSION}.nupkg" \
    -o "${WORK_DIR}/baseline.nupkg" \
    || die "failed to download ${PACKAGE_ID} ${BASELINE_VERSION}"
  unzip -q -o "${WORK_DIR}/baseline.nupkg" -d "${BASELINE_DIR}" \
    || die "failed to extract baseline package"
fi

# --- Compare per TFM ---------------------------------------------------------
# Compare each built TFM against the same TFM in the baseline package. When the
# baseline does not ship that TFM (older releases), fall back to the
# netstandard2.0 asset: the public surface is identical across TFMs, so this is
# still a meaningful guard.
DIFF_FILE="${WORK_DIR}/apicompat-diff.md"
: > "${DIFF_FILE}"
FAILED=0
COMPARED=0

SUPPRESSION_ARGS=()
if [ -f "${SUPPRESSION_FILE}" ]; then
  SUPPRESSION_ARGS=(--suppression-file "${SUPPRESSION_FILE}")
fi

shopt -s nullglob
for tfm_dir in "${BUILD_OUTPUT}"/*/; do
  tfm="$(basename "${tfm_dir}")"
  right="${tfm_dir}EdsDcfNet.dll"
  [ -f "${right}" ] || continue

  if [ -f "${BASELINE_DIR}/lib/${tfm}/EdsDcfNet.dll" ]; then
    left="${BASELINE_DIR}/lib/${tfm}/EdsDcfNet.dll"
    baseline_tfm="${tfm}"
  elif [ -f "${BASELINE_DIR}/lib/netstandard2.0/EdsDcfNet.dll" ]; then
    left="${BASELINE_DIR}/lib/netstandard2.0/EdsDcfNet.dll"
    baseline_tfm="netstandard2.0 (fallback)"
  else
    echo "apicompat-check: no compatible baseline asset for ${tfm} — skipped" >&2
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
      echo "### \`${tfm}\` vs baseline ${BASELINE_VERSION} (\`${baseline_tfm}\`)"
      echo
      echo '```'
      echo "${output}"
      echo '```'
      echo
    } >> "${DIFF_FILE}"
  fi
done

[ "${COMPARED}" -gt 0 ] || die "no built EdsDcfNet.dll found under ${BUILD_OUTPUT}"

if [ "${FAILED}" -ne 0 ]; then
  echo "apicompat-check: compatibility differences found — see ${DIFF_FILE}"
  exit 1
fi

echo "apicompat-check: public API is compatible with ${PACKAGE_ID} ${BASELINE_VERSION}"
exit 0
