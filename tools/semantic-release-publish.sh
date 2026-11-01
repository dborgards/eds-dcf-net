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

# Use -p: (not /p:): Git Bash on Windows converts bare /p:... args via MSYS
# path munging, which turns the property into a second "project" and fails with
# MSB1008 after the release workflow moved to windows-latest.
pack() {
  dotnet pack "$1" \
    --configuration Release \
    --no-restore \
    --output ./packages \
    -p:PackageVersion="${next_version}"
}

# Push an explicit package path. A quoted "./packages/*.nupkg" glob is not
# expanded by the Windows NuGet CLI (unlike Linux), so push fails with
# "File does not exist (./packages/*.nupkg)" even after a successful pack.
# Adjacent .snupkg symbols are published with the .nupkg automatically.
push() {
  local nupkg="./packages/$1.${next_version}.nupkg"
  if [[ ! -f "$nupkg" ]]; then
    echo "Expected package not found: ${nupkg}" >&2
    exit 1
  fi

  dotnet nuget push "$nupkg" \
    --api-key "${NUGET_API_KEY}" \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate
}

# The edsdcf-check tool is released in lockstep with the library and bundles the
# core assembly of the same build. Both are packed before anything is pushed, so
# a failing tool pack never leaves a library-only release behind. The library is
# pushed first. Tags from before the tool existed (a repair run packs the tag's
# tree) have no tool project and publish the library only.
tool_project="src/EdsDcfNet.Checker/EdsDcfNet.Checker.csproj"

pack src/EdsDcfNet/EdsDcfNet.csproj
if [[ -f "$tool_project" ]]; then
  pack "$tool_project"
fi

push EdsDcfNet
if [[ -f "$tool_project" ]]; then
  push EdsDcfNet.Checker
fi

best_effort "CycloneDX SBOM generation" generate_cyclonedx_sbom
best_effort "SPDX SBOM generation" generate_spdx_sbom
