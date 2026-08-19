#!/usr/bin/env bash
set -e

# ==============================================================================
# MacCompare Release Packager & DMG Generator
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
echo "📦 Packaging ${APP_NAME} v${VERSION} for Release"
echo "========================================================"

SWIFT_BIN="swift"
if command -v xcrun &> /dev/null; then
    SWIFT_BIN="xcrun swift"
fi

# 1. Build Release Executables
echo "[1/4] Building Swift Release Binaries..."
cd "${ROOT_DIR}/macos"
${SWIFT_BIN} build -c release

# Locate binary files
MACCOMPARE_BIN="$(find "${ROOT_DIR}/macos/.build" -type f -name "MacCompare" -path "*/release/*" 2>/dev/null | head -n 1)"
MCDIFF_BIN="$(find "${ROOT_DIR}/macos/.build" -type f -name "mcdiff" -path "*/release/*" 2>/dev/null | head -n 1)"

if [ -z "${MACCOMPARE_BIN}" ] || [ ! -f "${MACCOMPARE_BIN}" ]; then
    echo "Error: MacCompare executable not found in .build output!"
    exit 1
fi

echo "Found MacCompare at: ${MACCOMPARE_BIN}"
if [ -n "${MCDIFF_BIN}" ]; then
    echo "Found mcdiff at: ${MCDIFF_BIN}"
fi

# 2. Prepare .app Bundle
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
echo "✅ DMG Packaging Complete!"
echo "📍 DMG File: ${DMG_PATH}"
echo "📏 Size: $(du -sh "${DMG_PATH}" | awk '{print $1}')"
echo "========================================================"
