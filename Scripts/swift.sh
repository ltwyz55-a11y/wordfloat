#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
export CLANG_MODULE_CACHE_PATH="$PWD/.build/ModuleCache"
export SWIFTPM_MODULECACHE_OVERRIDE="$PWD/.build/ModuleCache"
if [ -z "${DEVELOPER_DIR:-}" ] && [ -d /Applications/Xcode.app/Contents/Developer ]; then
    export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi
action="${1:-test}"
if [ "$#" -gt 0 ]; then shift; fi
exec xcrun swift "$action" --disable-sandbox --cache-path "$PWD/.build/cache" "$@"
