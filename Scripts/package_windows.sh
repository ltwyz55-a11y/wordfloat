#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
mkdir -p work outputs/Windows
sh Scripts/dotnet.sh run --project Windows/WordFloat.Checks -c Release -p:UseSharedCompilation=false -- "$PWD"
sh Scripts/dotnet.sh publish Windows/WordFloat.Desktop -c Release -r win-x64 --self-contained true -p:PublishProfile=win-x64 -p:DebugType=None -p:DebugSymbols=false -o "$PWD/work/windows-publish"
python3 Scripts/package_windows.py --publish-dir "$PWD/work/windows-publish" --destination "$PWD/outputs/Windows"
