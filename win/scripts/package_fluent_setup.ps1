# ==============================================================================
# WinCompare Fluent Design Single-File Installer Packaging Script (PowerShell)
# Produces:
#   WinCompare-<Version>-Setup-<Platform>.exe (Modern Fluent Design Single-File Installer)
# ==============================================================================

param(
    [string]$Version = "0.7.0",
    [string]$Platform = "x64",
    [switch]$SkipRustBuild
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Get-Item "$scriptDir\..\..").FullName
$winDir = Join-Path $rootDir "win"
$distDir = Join-Path $rootDir "dist"
$setupDir = Join-Path $winDir "src\WinCompare.Setup"
$stageAppDir = Join-Path $distDir "staging_fluent_app_$Platform"
$setupOutDir = Join-Path $distDir "staging_setup_$Platform"
$payloadZip = Join-Path $setupDir "Resources\payload.zip"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "🎨 Building WinCompare Fluent Setup v$Version ($Platform)" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

# 0. Clean & Prepare Directories
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}
if (Test-Path $stageAppDir) {
    Remove-Item -Recurse -Force $stageAppDir
}
if (Test-Path $setupOutDir) {
    Remove-Item -Recurse -Force $setupOutDir
}
New-Item -ItemType Directory -Path $stageAppDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $setupDir "Resources") -Force | Out-Null

# 1. Build Rust Core (maccompare_ffi.dll)
$coreDir = Join-Path $rootDir "core"
$rustDll = Join-Path $coreDir "target\release\maccompare_ffi.dll"

if (-not $SkipRustBuild) {
    Write-Host "`n[1/4] Building Rust Core DLL (maccompare_ffi.dll)..." -ForegroundColor Yellow
    Push-Location $coreDir
    try {
        if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
            Write-Error "Cargo not found in PATH! Please install Rust."
            exit 1
        }
        cargo build --package maccompare-ffi --release
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Cargo build failed!"
            exit $LASTEXITCODE
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "`n[1/4] Skipping Rust Core build by user request..." -ForegroundColor Yellow
}

if (-not (Test-Path $rustDll)) {
    $altDll = Join-Path $coreDir "target\x86_64-pc-windows-msvc\release\maccompare_ffi.dll"
    if (Test-Path $altDll) {
        $rustDll = $altDll
    } else {
        $found = Get-ChildItem -Path (Join-Path $coreDir "target") -Filter "maccompare_ffi.dll" -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "release" } | Select-Object -First 1
        if ($found) {
            $rustDll = $found.FullName
        } else {
            Write-Error "Could not find compiled DLL at $rustDll"
            exit 1
        }
    }
}
Write-Host "Found Rust Core DLL: $rustDll" -ForegroundColor Green

# 2. Publish Main WinUI 3 App & CLI to Staging Folder
Write-Host "`n[2/4] Publishing WinCompare.UI and wincmp CLI (win-$Platform)..." -ForegroundColor Yellow
$uiProj = Join-Path $winDir "src\WinCompare.UI\WinCompare.UI.csproj"
dotnet publish $uiProj -c Release -r "win-$Platform" --self-contained true `
    /p:Platform=$Platform /p:Version=$Version /p:PublishSingleFile=false /p:DebugType=portable -o $stageAppDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "WinCompare.UI publish failed!"
    exit $LASTEXITCODE
}

# Copy Rust DLL
Copy-Item -Path $rustDll -Destination $stageAppDir -Force

# Publish CLI and merge into App folder
$cliProj = Join-Path $winDir "src\WinCompare.Cli\WinCompare.Cli.csproj"
$cliTempDir = Join-Path $distDir "temp_wincmp_setup"
if (Test-Path $cliTempDir) { Remove-Item -Recurse -Force $cliTempDir }
dotnet publish $cliProj -c Release -r "win-$Platform" --self-contained true `
    /p:Platform=$Platform /p:Version=$Version /p:DebugType=portable -o $cliTempDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "WinCompare.Cli publish failed!"
    exit $LASTEXITCODE
}

# Copy CLI binary and symbol to staging
Copy-Item -Path (Join-Path $cliTempDir "wincmp.exe") -Destination $stageAppDir -Force
if (Test-Path (Join-Path $cliTempDir "wincmp.pdb")) {
    Copy-Item -Path (Join-Path $cliTempDir "wincmp.pdb") -Destination $stageAppDir -Force
}
Remove-Item -Recurse -Force $cliTempDir

# 3. Compress Staging App to payload.zip
Write-Host "`n[3/4] Creating payload.zip from staged application..." -ForegroundColor Yellow
if (Test-Path $payloadZip) {
    Remove-Item -Force $payloadZip
}
Compress-Archive -Path "$stageAppDir\*" -DestinationPath $payloadZip -CompressionLevel Optimal
$zipSize = (Get-Item $payloadZip).Length / 1MB
Write-Host ("payload.zip created successfully: {0:N2} MB" -f $zipSize) -ForegroundColor Green

# 4. Publish Single-File Fluent Installer (Setup.exe)
Write-Host "`n[4/4] Publishing Fluent Design Setup.exe (win-$Platform)..." -ForegroundColor Yellow
$setupProj = Join-Path $setupDir "WinCompare.Setup.csproj"
dotnet publish $setupProj -c Release -r "win-$Platform" --self-contained true `
    /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:Version=$Version `
    -o $setupOutDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "WinCompare.Setup publish failed!"
    exit $LASTEXITCODE
}

$rawSetupExe = Join-Path $setupOutDir "WinCompare-Setup.exe"
if (-not (Test-Path $rawSetupExe)) {
    Write-Error "Expected Setup executable not found at $rawSetupExe"
    exit 1
}

$finalSetupExe = Join-Path $distDir "WinCompare-$Version-Setup-$Platform.exe"
Copy-Item -Path $rawSetupExe -Destination $finalSetupExe -Force
$exeSize = (Get-Item $finalSetupExe).Length / 1MB

# Compute SHA256
$hash = (Get-FileHash -Path $finalSetupExe -Algorithm SHA256).Hash

Write-Host "`n================================================================" -ForegroundColor Green
Write-Host "🎉 Fluent Setup Packaging Complete!" -ForegroundColor Green
Write-Host ("   Output File : {0}" -f $finalSetupExe) -ForegroundColor White
Write-Host ("   File Size   : {0:N2} MB" -f $exeSize) -ForegroundColor White
Write-Host ("   SHA256 Hash : {0}" -f $hash) -ForegroundColor White
Write-Host "================================================================" -ForegroundColor Green
