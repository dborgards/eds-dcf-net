#!/usr/bin/env bash
set -euo pipefail

next_version="${1:-}"
if [[ -z "$next_version" ]]; then
  echo "Usage: $0 <next-version>" >&2
  exit 1
fi

branch_name="${GITHUB_REF_NAME:-}"
if [[ -z "$branch_name" ]]; then
  branch_name="$(git rev-parse --abbrev-ref HEAD)"
fi

if [[ "$branch_name" == "main" ]]; then
  tmp_file="$(mktemp)"
  awk '
    /^## / {
      skip = ($0 ~ /^## \[?[0-9]+\.[0-9]+\.[0-9]+-[0-9A-Za-z]/)
    }
    !skip { print }
  ' CHANGELOG.md > "$tmp_file"
  mv "$tmp_file" CHANGELOG.md
fi

csproj_file="src/EdsDcfNet/EdsDcfNet.csproj"
csproj_tmp_file="$(mktemp)"
awk -v version="$next_version" '
  {
    gsub(/<Version>[^<]*<\/Version>/, "<Version>" version "</Version>")
    print
  }
' "$csproj_file" > "$csproj_tmp_file"
mv "$csproj_tmp_file" "$csproj_file"
