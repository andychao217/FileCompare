#!/usr/bin/env bash
set -e

# ==============================================================================
# MacCompare Universal Binary 2 Build Script (Apple Silicon arm64 + Intel x86_64)
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "=== Building MacCompare Core Universal Binary ==="

if ! command -v cargo &> /dev/null; then
    echo "Warning: cargo not found in standard PATH. Please install Rust or configure rustup."
    echo "Skipping Rust static lib compilation."
    exit 0
fi

# Ensure compilation targets exist
rustup target add aarch64-apple-darwin 2>/dev/null || true
rustup target add x86_64-apple-darwin 2>/dev/null || true

echo "[1/3] Compiling for Apple Silicon (arm64)..."
cargo build --manifest-path "${ROOT_DIR}/core/Cargo.toml" --release --target aarch64-apple-darwin --package maccompare-ffi

echo "[2/3] Compiling for Intel Mac (x86_64)..."
cargo build --manifest-path "${ROOT_DIR}/core/Cargo.toml" --release --target x86_64-apple-darwin --package maccompare-ffi

echo "[3/3] Creating Universal Binary 2 with lipo..."
OUTPUT_DIR="${ROOT_DIR}/macos/Sources/MacCompareKit/Generated/lib"
mkdir -p "${OUTPUT_DIR}"

lipo -create \
    "${ROOT_DIR}/core/target/aarch64-apple-darwin/release/libmaccompare_ffi.a" \
    "${ROOT_DIR}/core/target/x86_64-apple-darwin/release/libmaccompare_ffi.a" \
    -output "${OUTPUT_DIR}/libmaccompare_ffi.a"

echo "=== Universal static library created successfully! ==="
lipo -info "${OUTPUT_DIR}/libmaccompare_ffi.a"
