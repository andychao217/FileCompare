#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
CORE_DIR="$ROOT_DIR/core"

echo "======================================================="
echo "  WinCompare: Building Rust Core"
echo "======================================================="

cd "$CORE_DIR"

if command -v cargo-xwin >/dev/null 2>&1; then
    echo "Found cargo-xwin. Cross-compiling for x86_64-pc-windows-msvc..."
    cargo xwin build --package maccompare-ffi --target x86_64-pc-windows-msvc --release
    DLL_SRC="$CORE_DIR/target/x86_64-pc-windows-msvc/release/maccompare_ffi.dll"
elif [[ "$(uname -s)" == *"MINGW"* ]] || [[ "$(uname -s)" == *"MSYS"* ]] || [[ "$(uname -s)" == *"CYGWIN"* ]]; then
    echo "Native Windows bash detected. Compiling release..."
    cargo build --package maccompare-ffi --release
    DLL_SRC="$CORE_DIR/target/release/maccompare_ffi.dll"
else
    echo "Building for host platform $(uname -s)..."
    cargo build --package maccompare-ffi --release
    DLL_SRC="$CORE_DIR/target/release/libmaccompare_ffi.dylib"
fi

echo "Rust build completed."
