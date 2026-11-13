#!/usr/bin/env bash
set -euo pipefail

# RoweMod is a BMX Streets (Unity/IL2CPP + MelonLoader) mod. The main assembly
# (rowemod.csproj) only builds on Windows against a local game install, so it is
# not built here. What is runnable on Linux is the self-contained test suite under
# Tests/ (real mod source compiled against stubs) and the static menu previews in
# docs/. Both target the .NET 6 SDK.

DOTNET_CHANNEL="6.0"
DOTNET_DIR="/usr/share/dotnet"

# net6.0 is EOL and not in the Ubuntu 24.04 package feeds, so install the SDK
# directly from Microsoft. Symlinking into /usr/local/bin keeps `dotnet` on PATH
# for every shell; the host resolves the SDK from the symlink target, so no
# DOTNET_ROOT export or shell-profile edit is needed.
if ! /usr/local/bin/dotnet --list-sdks 2>/dev/null | grep -q '^6\.'; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  sudo mkdir -p "$DOTNET_DIR"
  sudo bash /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_DIR"
  sudo ln -sf "$DOTNET_DIR/dotnet" /usr/local/bin/dotnet
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Warm the NuGet cache and build outputs for every self-contained test project.
for proj in Tests/*/*.Tests.csproj; do
  echo "Restoring $proj"
  dotnet restore "$proj"
done

echo "RoweMod environment ready. Run the checks with: bash Scripts/run-tests.sh"
