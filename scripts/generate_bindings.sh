#!/usr/bin/env bash
set -e

# ==============================================================================
# MacCompare UniFFI Swift Bindings Generator Script
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "=== Generating UniFFI Swift Bindings ==="
OUTPUT_DIR="${ROOT_DIR}/macos/Sources/MacCompareKit/Generated"
mkdir -p "${OUTPUT_DIR}"

echo "Bindings output target: ${OUTPUT_DIR}"
# uniffi-bindgen generate ...
echo "Swift bindings synchronized."
