#!/usr/bin/env bash
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export NUGET_PACKAGES="${HOME}/.nuget_local/packages"
export NUGET_HTTP_CACHE_PATH="${HOME}/.nuget_local/http-cache"
dotnet run --project "${SCRIPT_DIR}/ModFinder.Cli" -- "$@"
