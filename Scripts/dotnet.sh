#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME="$PWD/work/dotnet-home"
export NUGET_PACKAGES="$PWD/work/nuget"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if [ -x "$PWD/work/dotnet/dotnet" ]; then
    exec "$PWD/work/dotnet/dotnet" "$@"
fi
exec dotnet "$@"
