#!/usr/bin/env bash
set -e

# ==============================================================================
# MacCompare Run & Build Script
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

SWIFT_BIN="swift"
if command -v xcrun &> /dev/null; then
    SWIFT_BIN="xcrun swift"
fi

echo "=== Building MacCompare Application & CLI ==="
cd "${ROOT_DIR}/macos"
${SWIFT_BIN} build

echo "=== Running MacCompare Tests ==="
${SWIFT_BIN} test

echo "=== Build and Tests Completed Successfully! ==="
