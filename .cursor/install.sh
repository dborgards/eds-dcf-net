#!/usr/bin/env bash
# Cloud Agent install script for EdsDcfNet.
# Idempotent: installs the .NET SDK pinned in global.json if missing, then restores and builds.
set -euo pipefail

DOTNET_INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
GLOBAL_JSON="${GLOBAL_JSON:-global.json}"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Derive the SDK version straight from global.json so routine SDK bumps stay in sync
# (a hardcoded literal would drift and, because rollForward=latestPatch cannot roll
# backward, break `dotnet restore` once global.json requests a newer patch/feature band).
DOTNET_VERSION="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$GLOBAL_JSON" | head -n1)"
if [ -z "$DOTNET_VERSION" ]; then
  echo "Could not read sdk.version from ${GLOBAL_JSON}." >&2
  exit 1
fi

if [ ! -x "$DOTNET_INSTALL_DIR/dotnet" ] || ! "$DOTNET_INSTALL_DIR/dotnet" --list-sdks 2>/dev/null | grep -q "^${DOTNET_VERSION} "; then
  echo "Installing .NET SDK ${DOTNET_VERSION} (from ${GLOBAL_JSON}) into ${DOTNET_INSTALL_DIR} ..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --version "$DOTNET_VERSION" --install-dir "$DOTNET_INSTALL_DIR"
  rm -f /tmp/dotnet-install.sh
else
  echo ".NET SDK ${DOTNET_VERSION} already present in ${DOTNET_INSTALL_DIR}."
fi

export DOTNET_ROOT="$DOTNET_INSTALL_DIR"
export PATH="$DOTNET_INSTALL_DIR:$PATH"

# Make `dotnet` discoverable in every agent shell without mutating shell profiles.
if command -v sudo >/dev/null 2>&1; then
  sudo ln -sf "$DOTNET_INSTALL_DIR/dotnet" /usr/local/bin/dotnet 2>/dev/null || true
fi

echo "Using $(dotnet --version) SDK."

dotnet restore EdsDcfNet.sln
dotnet build EdsDcfNet.sln -c Release --no-restore
