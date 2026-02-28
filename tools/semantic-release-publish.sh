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
