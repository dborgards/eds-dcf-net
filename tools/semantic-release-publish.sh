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
  local cdx_json
  local exit_code

  echo "Generating CycloneDX SBOM..."

  rm -f packages/bom.cdx.json packages/bom.json || return
  dotnet tool restore || return
  dotnet tool run dotnet-CycloneDX -- src/EdsDcfNet/EdsDcfNet.csproj \
    --output packages \
    --json \
    --set-name EdsDcfNet \
    --set-version "${next_version}" \
    --set-type Library \
    --set-nuget-purl \
    --import-metadata-path sbom/bom-metadata.template.xml \
    --exclude-dev \
    --exclude-test-projects \
    --enable-github-licenses || return
  cdx_json="$(mktemp packages/bom.cdx.XXXXXX)" || return
  jq -f sbom/prune-cyclonedx.jq packages/bom.json > "$cdx_json" || {
    exit_code=$?
    rm -f "$cdx_json"
    return "$exit_code"
  }
  mv "$cdx_json" packages/bom.cdx.json || {
    exit_code=$?
    rm -f "$cdx_json"
    return "$exit_code"
  }
  rm -f packages/bom.json "$cdx_json"

  echo "CycloneDX SBOM written to packages/bom.cdx.json with version ${next_version}"
}

generate_spdx_sbom() {
  echo "Generating SPDX SBOM..."

  if [[ ! -f packages/bom.cdx.json ]]; then
    warn "packages/bom.cdx.json not found; SPDX SBOM skipped."
    return 0
  fi

  rm -f packages/sbom.spdx.json || return
  jq -f sbom/cyclonedx-to-spdx.jq packages/bom.cdx.json > packages/sbom.spdx.json || return

  echo "SPDX SBOM written to packages/sbom.spdx.json (derived from CycloneDX BOM)"
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
