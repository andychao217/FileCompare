#!/usr/bin/env bash
set -e

# ==============================================================================
# MacCompare Release Packager & Universal DMG Generator
# Supports Universal Binary 2 (Apple Silicon arm64 + Intel x86_64)
# ==============================================================================

VERSION="${1:-0.1.0}"
APP_NAME="MacCompare"
BUNDLE_ID="com.andychao.maccompare"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
DIST_DIR="${ROOT_DIR}/dist"
STAGE_DIR="${DIST_DIR}/stage"
APP_BUNDLE="${STAGE_DIR}/${APP_NAME}.app"
DMG_NAME="${APP_NAME}-${VERSION}.dmg"
DMG_PATH="${DIST_DIR}/${DMG_NAME}"

echo "========================================================"
echo "📦 Packaging ${APP_NAME} v${VERSION} (Universal Binary 2)"
echo "========================================================"

SWIFT_BIN="swift"
if command -v xcrun &> /dev/null; then
    SWIFT_BIN="xcrun swift"
fi

# 1. Build Universal Binary 2 (arm64 + x86_64)
echo "[1/4] Compiling Universal Binary 2 (arm64 + x86_64)..."
cd "${ROOT_DIR}/macos"
if ! ${SWIFT_BIN} build -c release --arch arm64 --arch x86_64; then
    echo "Warning: Universal dual-arch build failed. Falling back to default release build..."
    ${SWIFT_BIN} build -c release
fi

# Check possible locations for built binary
MACCOMPARE_BIN=""
MCDIFF_BIN=""

POSSIBLE_PATHS=(
    "${ROOT_DIR}/macos/.build/apple/Products/Release/MacCompare"
    "${ROOT_DIR}/macos/.build/release/MacCompare"
    "${ROOT_DIR}/macos/.build/arm64-apple-macosx/release/MacCompare"
    "${ROOT_DIR}/macos/.build/x86_64-apple-macosx/release/MacCompare"
)

for p in "${POSSIBLE_PATHS[@]}"; do
    if [ -f "${p}" ]; then
        MACCOMPARE_BIN="${p}"
        break
    fi
done

POSSIBLE_MCDIFF_PATHS=(
    "${ROOT_DIR}/macos/.build/apple/Products/Release/mcdiff"
    "${ROOT_DIR}/macos/.build/release/mcdiff"
    "${ROOT_DIR}/macos/.build/arm64-apple-macosx/release/mcdiff"
    "${ROOT_DIR}/macos/.build/x86_64-apple-macosx/release/mcdiff"
)

for p in "${POSSIBLE_MCDIFF_PATHS[@]}"; do
    if [ -f "${p}" ]; then
        MCDIFF_BIN="${p}"
        break
    fi
done

if [ -z "${MACCOMPARE_BIN}" ] || [ ! -f "${MACCOMPARE_BIN}" ]; then
    echo "Error: MacCompare executable not found in .build output!"
    exit 1
fi

echo "Found MacCompare at: ${MACCOMPARE_BIN}"
if command -v lipo &> /dev/null; then
    echo "Binary Architecture Info: $(lipo -info "${MACCOMPARE_BIN}" 2>&1)"
fi

# 2. Prepare .app Bundle Structure
echo "[2/4] Constructing ${APP_NAME}.app bundle..."
rm -rf "${STAGE_DIR}"
mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"

cp "${MACCOMPARE_BIN}" "${APP_BUNDLE}/Contents/MacOS/MacCompare"
if [ -n "${MCDIFF_BIN}" ] && [ -f "${MCDIFF_BIN}" ]; then
    cp "${MCDIFF_BIN}" "${APP_BUNDLE}/Contents/MacOS/mcdiff"
    chmod +x "${APP_BUNDLE}/Contents/MacOS/mcdiff"
fi
chmod +x "${APP_BUNDLE}/Contents/MacOS/MacCompare"

# Copy App Icon
if [ -f "${ROOT_DIR}/macos/Resources/AppIcon.icns" ]; then
    cp "${ROOT_DIR}/macos/Resources/AppIcon.icns" "${APP_BUNDLE}/Contents/Resources/AppIcon.icns"
    echo "Included AppIcon.icns in bundle."
fi

# Generate Info.plist
cat <<EOF > "${APP_BUNDLE}/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>14.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
</dict>
</plist>
EOF

# 3. Create Applications symlink for drag-and-drop installer
echo "[3/4] Creating DMG staging layout..."
ln -s /Applications "${STAGE_DIR}/Applications"

# 4. Generate Compressed DMG via hdiutil
echo "[4/4] Generating DMG image with hdiutil..."
rm -f "${DMG_PATH}"
mkdir -p "${DIST_DIR}"

hdiutil create \
    -volname "${APP_NAME}" \
    -srcfolder "${STAGE_DIR}" \
    -ov \
    -format UDZO \
    "${DMG_PATH}"

# Clean staging directory
rm -rf "${STAGE_DIR}"

echo "========================================================"
echo "✅ Universal DMG Packaging Complete!"
echo "📍 DMG File: ${DMG_PATH}"
echo "📏 Size: $(du -sh "${DMG_PATH}" | awk '{print $1}')"
echo "========================================================"
