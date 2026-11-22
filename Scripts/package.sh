#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
export CLANG_MODULE_CACHE_PATH="$PWD/.build/ModuleCache"
export SWIFTPM_MODULECACHE_OVERRIDE="$PWD/.build/ModuleCache"
if [ -z "${DEVELOPER_DIR:-}" ] && [ -d /Applications/Xcode.app/Contents/Developer ]; then
    export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi
python3 Scripts/build_data.py
sh Scripts/swift.sh build -c release
binary_dir=$(sh Scripts/swift.sh build -c release --show-bin-path)
mkdir -p outputs work
stage_dir=$(mktemp -d "$PWD/work/package.XXXXXX")
app_dir="$stage_dir/浮词.app"
mkdir -p "$app_dir/Contents/MacOS" "$app_dir/Contents/Resources"
cp "$binary_dir/WordFloat" "$app_dir/Contents/MacOS/WordFloat"
cp App/Core/Resources/vocabulary.sqlite Data/ECDICT-LICENSE.txt Data/counts.json "$app_dir/Contents/Resources/"
cp DATA-NOTICE.md "$app_dir/Contents/Resources/"
xcrun swift -module-cache-path "$CLANG_MODULE_CACHE_PATH" Scripts/make_icon.swift "$stage_dir/AppIcon.iconset"
iconutil -c icns "$stage_dir/AppIcon.iconset" -o "$app_dir/Contents/Resources/AppIcon.icns"
python3 - "$app_dir/Contents/Info.plist" <<'PY'
import plistlib,sys
info=dict(CFBundleExecutable='WordFloat',CFBundleIdentifier='local.wordfloat.mac',CFBundleName='浮词',
    CFBundleDisplayName='浮词',CFBundlePackageType='APPL',CFBundleShortVersionString='1.4.1',CFBundleVersion='9',
    CFBundleIconFile='AppIcon',LSMinimumSystemVersion='13.0',LSUIElement=True,NSHighResolutionCapable=True,
    CFBundleDevelopmentRegion='zh_CN',CFBundleLocalizations=['zh-Hans'],LSApplicationCategoryType='public.app-category.education')
with open(sys.argv[1],'wb') as f: plistlib.dump(info,f)
PY
plutil -lint "$app_dir/Contents/Info.plist"
codesign --force --sign - --timestamp=none "$app_dir"
codesign --verify --deep --strict "$app_dir"
# Preserve other output files; replace only this build's application bundle.
if [ -e outputs/浮词.app ]; then mv outputs/浮词.app "$stage_dir/previous.app"; fi
mv "$app_dir" outputs/浮词.app
cp README.md outputs/使用说明.md
cp VALIDATION.md outputs/验收记录.md
cp DATA-NOTICE.md outputs/词库来源.md
/usr/bin/ditto -c -k --sequesterRsrc --keepParent outputs/浮词.app outputs/浮词-Mac.zip
printf 'Packaged: %s/outputs/浮词.app\n' "$PWD"
