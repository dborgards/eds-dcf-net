#!/usr/bin/env bash
set -euo pipefail

next_version="${1:-}"
if [[ -z "$next_version" ]]; then
  echo "Usage: $0 <next-version>" >&2
  exit 1
fi

dotnet pack src/EdsDcfNet/EdsDcfNet.csproj \
  --configuration Release \
  --no-restore \
  --output ./packages \
  /p:PackageVersion="${next_version}"

dotnet nuget push "./packages/*.nupkg" \
  --api-key "${NUGET_API_KEY}" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate

# --- SBOM: CycloneDX ---
echo "Generating CycloneDX SBOM..."
{
  dotnet CycloneDX src/EdsDcfNet/EdsDcfNet.csproj \
    --output packages \
    --json
  mv packages/bom.json packages/bom.cdx.json
  echo "CycloneDX SBOM written to packages/bom.cdx.json"
} || echo "Warning: CycloneDX SBOM generation failed; skipping."

# --- SBOM: SPDX via GitHub Dependency Graph API ---
echo "Generating SPDX SBOM..."
if [[ -n "${GH_TOKEN:-}" && -n "${GITHUB_REPOSITORY:-}" ]]; then
  http_status="$(curl -sL \
    -H "Authorization: Bearer ${GH_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    --write-out "%{http_code}" \
    --output /tmp/spdx_raw.json \
    "https://api.github.com/repos/${GITHUB_REPOSITORY}/dependency-graph/sbom")"
  if [[ "$http_status" == "200" ]]; then
    jq '.sbom' /tmp/spdx_raw.json > packages/sbom.spdx.json
    echo "SPDX SBOM written to packages/sbom.spdx.json"
  else
    echo "Warning: GitHub SBOM API returned HTTP ${http_status}; SPDX SBOM skipped."
  fi
else
  echo "Warning: GH_TOKEN or GITHUB_REPOSITORY not set; SPDX SBOM skipped."
fi
