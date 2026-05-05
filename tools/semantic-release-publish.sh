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
# Use && chain so each step's failure is properly detected under set -euo pipefail,
# while || ensures a failed SBOM never aborts the release.
# Clear any stale SBOM file first so a failed generation cannot upload a previous run's artifact.
echo "Generating CycloneDX SBOM..."
rm -f packages/bom.cdx.json packages/bom.json || true
dotnet tool restore \
  && dotnet tool run dotnet-CycloneDX src/EdsDcfNet/EdsDcfNet.csproj \
  --output packages \
  --json \
  && mv packages/bom.json packages/bom.cdx.json \
  && echo "CycloneDX SBOM written to packages/bom.cdx.json" \
  || echo "Warning: CycloneDX SBOM generation failed; skipping."

# --- SBOM: SPDX via GitHub Dependency Graph API ---
# curl -f returns a non-zero exit code on HTTP 4xx/5xx, so the && chain below
# handles both transport errors and API errors without aborting the release.
echo "Generating SPDX SBOM..."
if [[ -n "${GH_TOKEN:-}" && -n "${GITHUB_REPOSITORY:-}" ]]; then
  rm -f packages/sbom.spdx.json /tmp/sbom.spdx.json /tmp/spdx_raw.json || true
  curl -sLf \
    -H "Authorization: Bearer ${GH_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    --output /tmp/spdx_raw.json \
    "https://api.github.com/repos/${GITHUB_REPOSITORY}/dependency-graph/sbom" \
    && jq -e '.sbom' /tmp/spdx_raw.json > /tmp/sbom.spdx.json \
    && mv /tmp/sbom.spdx.json packages/sbom.spdx.json \
    && echo "SPDX SBOM written to packages/sbom.spdx.json" \
    || echo "Warning: SPDX SBOM generation failed; skipping."
else
  echo "Warning: GH_TOKEN or GITHUB_REPOSITORY not set; SPDX SBOM skipped."
fi
