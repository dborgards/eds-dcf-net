#!/usr/bin/env bash
# Cloud Agent install script for EdsDcfNet.
# Idempotent: installs the pinned .NET SDK if missing, then restores and builds.
set -euo pipefail

DOTNET_VERSION="10.0.400"
DOTNET_INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

if [ ! -x "$DOTNET_INSTALL_DIR/dotnet" ] || ! "$DOTNET_INSTALL_DIR/dotnet" --list-sdks 2>/dev/null | grep -q "^${DOTNET_VERSION} "; then
  echo "Installing .NET SDK ${DOTNET_VERSION} into ${DOTNET_INSTALL_DIR} ..."
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
