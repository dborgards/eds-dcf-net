#!/usr/bin/env bash
set -euo pipefail

next_version="${1:-}"
if [[ -z "$next_version" ]]; then
  echo "Usage: $0 <next-version>" >&2
  exit 1
fi

warn() {
  echo "Warning: $*" >&2
}

best_effort() {
  local description="$1"
  local exit_code
  shift

  if "$@"; then
    return 0
  else
    exit_code=$?
  fi

  warn "${description} failed with exit code ${exit_code}; skipping."
  return 0
}

generate_cyclonedx_sbom() {
  echo "Generating CycloneDX SBOM..."

  rm -f packages/bom.cdx.json packages/bom.json || return
  dotnet tool restore || return
  dotnet tool run dotnet-CycloneDX -- src/EdsDcfNet/EdsDcfNet.csproj \
    --output packages \
    --json \
    --set-version "${next_version}" \
    --set-type Library \
    --exclude-dev \
    --exclude-test-projects \
    --enable-github-licenses || return
  mv packages/bom.json packages/bom.cdx.json || return

  echo "CycloneDX SBOM written to packages/bom.cdx.json with version ${next_version}"
}

generate_spdx_sbom() {
  local raw_json
  local spdx_json
  local exit_code

  echo "Generating SPDX SBOM..."

  if [[ -z "${GH_TOKEN:-}" || -z "${GITHUB_REPOSITORY:-}" ]]; then
    warn "GH_TOKEN or GITHUB_REPOSITORY not set; SPDX SBOM skipped."
    return 0
  fi

  raw_json="$(mktemp "${TMPDIR:-/tmp}/spdx_raw.XXXXXX")" || return
  spdx_json="$(mktemp "${TMPDIR:-/tmp}/sbom_spdx.XXXXXX")" || {
    exit_code=$?
    rm -f "$raw_json"
    return "$exit_code"
  }

  rm -f packages/sbom.spdx.json || {
    exit_code=$?
    rm -f "$raw_json" "$spdx_json"
    return "$exit_code"
  }
  curl -sLf \
    -H "Authorization: Bearer ${GH_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    --output "$raw_json" \
    "https://api.github.com/repos/${GITHUB_REPOSITORY}/dependency-graph/sbom" || {
    exit_code=$?
    rm -f "$raw_json" "$spdx_json"
    return "$exit_code"
  }
  jq -e '.sbom' "$raw_json" > "$spdx_json" || {
    exit_code=$?
    rm -f "$raw_json" "$spdx_json"
    return "$exit_code"
  }
  mv "$spdx_json" packages/sbom.spdx.json || {
    exit_code=$?
    rm -f "$raw_json" "$spdx_json"
    return "$exit_code"
  }
  rm -f "$raw_json" "$spdx_json"

  echo "SPDX SBOM written to packages/sbom.spdx.json"
}

dotnet pack src/EdsDcfNet/EdsDcfNet.csproj \
  --configuration Release \
  --no-restore \
  --output ./packages \
  /p:PackageVersion="${next_version}"

dotnet nuget push "./packages/*.nupkg" \
  --api-key "${NUGET_API_KEY}" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate

best_effort "CycloneDX SBOM generation" generate_cyclonedx_sbom
best_effort "SPDX SBOM generation" generate_spdx_sbom
