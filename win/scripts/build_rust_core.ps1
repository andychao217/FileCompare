# WinCompare: PowerShell script to build Rust core
$ErrorActionPreference = "Stop"

Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  WinCompare: Building Rust Core (maccompare_ffi.dll)" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$coreDir = Join-Path $scriptDir "..\..\core"

Write-Host "Checking Rust toolchain..."
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Error "Cargo is not found in PATH! Please install Rust from https://rustup.rs/ or restart your terminal."
    exit 1
}

Write-Host "Building maccompare-ffi in release mode in $coreDir ..."
Push-Location $coreDir
try {
    cargo build --package maccompare-ffi --release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Cargo build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    $dllSrc = Join-Path $coreDir "target\release\maccompare_ffi.dll"
    if (-not (Test-Path $dllSrc)) {
        Write-Error "Could not find compiled DLL at $dllSrc"
        exit 1
    }

    Write-Host "`nCopying maccompare_ffi.dll to WinCompare project output paths..." -ForegroundColor Yellow

    $outputDirs = @(
        (Join-Path $scriptDir "..\src\WinCompare.UI\bin\x64\Debug\net10.0-windows10.0.19041.0"),
        (Join-Path $scriptDir "..\src\WinCompare.UI\bin\x64\Release\net10.0-windows10.0.19041.0"),
        (Join-Path $scriptDir "..\src\WinCompare.Cli\bin\x64\Debug\net10.0"),
        (Join-Path $scriptDir "..\src\WinCompare.Cli\bin\x64\Release\net10.0"),
        (Join-Path $scriptDir "..\src\WinCompare.UI")
    )

    foreach ($dir in $outputDirs) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
        Copy-Item -Path $dllSrc -Destination $dir -Force
        Write-Host "  -> Copied to $dir" -ForegroundColor Green
    }

    Write-Host "=======================================================" -ForegroundColor Cyan
    Write-Host "  [SUCCESS] Rust core compiled and deployed successfully!" -ForegroundColor Green
    Write-Host "=======================================================" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
