#!/bin/bash
# Prépare une session Claude Code cloud : SDK .NET 8 + restauration NuGet pour `dotnet test PkmnRaceBattle.Tests`.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# builds.dotnet.microsoft.com est bloqué par le proxy : on passe par le paquet Ubuntu
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^8\.'; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-8.0
fi

echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >> "${CLAUDE_ENV_FILE:-/dev/null}"
echo 'export DOTNET_NOLOGO=1' >> "${CLAUDE_ENV_FILE:-/dev/null}"

cd "$CLAUDE_PROJECT_DIR"
dotnet restore PkmnRaceBattle.API.sln
