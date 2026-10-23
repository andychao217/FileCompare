# ==============================================================================
# WinCompare Windows Packaging Script (PowerShell)
# Produces:
#   1. Standalone Portable Zip (WinUI 3 GUI + CLI + Rust Core)
#   2. Standalone Portable CLI Zip (wincmp + Rust Core)
#   3. Fluent Design Single-File Installer (WinCompare-<Version>-Setup-<Platform>.exe)
# ==============================================================================

param(
    [string]$Version = "0.7.0",
    [string]$Platform = "x64",
    [switch]$SkipInstaller
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Get-Item "$scriptDir\..\..").FullName
$winDir = Join-Path $rootDir "win"
$distDir = Join-Path $rootDir "dist"
$stageDir = Join-Path $distDir "staging_win_$Platform"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "📦 Packaging WinCompare v$Version (Platform: $Platform)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# Ensure dist directory
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}
if (Test-Path $stageDir) {
    Remove-Item -Recurse -Force $stageDir
}
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null

# ------------------------------------------------------------------------------
# 1. Compile Rust Core (maccompare_ffi.dll)
# ------------------------------------------------------------------------------
Write-Host "`n[1/5] Building Rust Core DLL (maccompare_ffi.dll)..." -ForegroundColor Yellow
$coreDir = Join-Path $rootDir "core"
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

$rustDll = Join-Path $coreDir "target\release\maccompare_ffi.dll"
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
Write-Host "Found Rust Core DLL at: $rustDll" -ForegroundColor Green

# ------------------------------------------------------------------------------
# 2. Publish WinUI 3 Desktop App (Self-Contained Unpackaged)
# ------------------------------------------------------------------------------
Write-Host "`n[2/5] Publishing WinCompare.UI (win-$Platform)..." -ForegroundColor Yellow
$uiProj = Join-Path $winDir "src\WinCompare.UI\WinCompare.UI.csproj"
dotnet publish $uiProj -c Release -r "win-$Platform" --self-contained true `
    /p:Platform=$Platform /p:Version=$Version /p:PublishSingleFile=false /p:DebugType=portable /p:DebugSymbols=true -o $stageDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "WinCompare.UI publish failed!"
    exit $LASTEXITCODE
}

# Ensure maccompare_ffi.dll is placed in the published folder
Copy-Item -Path $rustDll -Destination $stageDir -Force

# ------------------------------------------------------------------------------
# 3. Publish wincmp CLI and Bundle with GUI
# ------------------------------------------------------------------------------
Write-Host "`n[3/5] Publishing wincmp CLI tool..." -ForegroundColor Yellow
$cliProj = Join-Path $winDir "src\WinCompare.Cli\WinCompare.Cli.csproj"
$cliTempDir = Join-Path $distDir "temp_wincmp"
dotnet publish $cliProj -c Release -r "win-$Platform" --self-contained true `
    /p:Platform=$Platform /p:Version=$Version /p:DebugType=portable /p:DebugSymbols=true -o $cliTempDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "WinCompare.Cli publish failed!"
    exit $LASTEXITCODE
}

# Bundle wincmp.exe into the GUI staging directory as well
Copy-Item -Path (Join-Path $cliTempDir "wincmp.exe") -Destination $stageDir -Force

# ------------------------------------------------------------------------------
# 4. Generate Portable ZIP Archives
# ------------------------------------------------------------------------------
Write-Host "`n[4/5] Generating Portable ZIP Packages..." -ForegroundColor Yellow
$zipApp = Join-Path $distDir "WinCompare-$Version-windows-$Platform.zip"
$zipCli = Join-Path $distDir "wincmp-$Version-windows-$Platform.zip"

if (Test-Path $zipApp) { Remove-Item -Force $zipApp }
if (Test-Path $zipCli) { Remove-Item -Force $zipCli }

# Compress UI bundle
Compress-Archive -Path "$stageDir\*" -DestinationPath $zipApp -Force
Write-Host "  -> Created: $zipApp" -ForegroundColor Green

# Compress standalone CLI bundle (wincmp.exe + maccompare_ffi.dll)
$cliBundleDir = Join-Path $distDir "staging_cli_$Platform"
if (Test-Path $cliBundleDir) { Remove-Item -Recurse -Force $cliBundleDir }
New-Item -ItemType Directory -Path $cliBundleDir -Force | Out-Null
Copy-Item -Path (Join-Path $cliTempDir "wincmp.exe") -Destination $cliBundleDir -Force
Copy-Item -Path $rustDll -Destination $cliBundleDir -Force
Compress-Archive -Path "$cliBundleDir\*" -DestinationPath $zipCli -Force
Write-Host "  -> Created: $zipCli" -ForegroundColor Green

Remove-Item -Recurse -Force $cliTempDir
Remove-Item -Recurse -Force $cliBundleDir

# ------------------------------------------------------------------------------
# 5. Build Fluent Design Single-File Installer (Setup.exe)
# ------------------------------------------------------------------------------
if (-not $SkipInstaller) {
    Write-Host "`n[5/5] Building Fluent Design Single-File Installer (Setup.exe)..." -ForegroundColor Yellow
    $setupDir = Join-Path $winDir "src\WinCompare.Setup"
    $setupResDir = Join-Path $setupDir "Resources"
    if (-not (Test-Path $setupResDir)) {
        New-Item -ItemType Directory -Path $setupResDir -Force | Out-Null
    }
    $payloadZip = Join-Path $setupResDir "payload.zip"
    if (Test-Path $payloadZip) { Remove-Item -Force $payloadZip }

    Write-Host "  -> Packaging payload.zip for installer..." -ForegroundColor Gray
    Compress-Archive -Path "$stageDir\*" -DestinationPath $payloadZip -Force

    $setupProj = Join-Path $setupDir "WinCompare.Setup.csproj"
    $setupOutDir = Join-Path $distDir "staging_setup_$Platform"
    if (Test-Path $setupOutDir) { Remove-Item -Recurse -Force $setupOutDir }

    Write-Host "  -> Publishing single-file WinCompare-Setup.exe..." -ForegroundColor Gray
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
        $foundExe = Get-ChildItem -Path $setupOutDir -Filter "*.exe" | Select-Object -First 1
        if ($foundExe) { $rawSetupExe = $foundExe.FullName }
    }

    if (Test-Path $rawSetupExe) {
        $finalSetupExe = Join-Path $distDir "WinCompare-$Version-Setup-$Platform.exe"
        Copy-Item -Path $rawSetupExe -Destination $finalSetupExe -Force
        $exeSize = (Get-Item $finalSetupExe).Length / 1MB
        Write-Host ("  -> Created Fluent Setup: {0} ({1:N2} MB)" -f $finalSetupExe, $exeSize) -ForegroundColor Green
    } else {
        Write-Error "Expected Setup executable not found in $setupOutDir"
        exit 1
    }

    if (Test-Path $setupOutDir) { Remove-Item -Recurse -Force $setupOutDir }
} else {
    Write-Host "`n[5/5] Skipping Installer build (-SkipInstaller specified)." -ForegroundColor Gray
}

# Cleanup staging directory
if (Test-Path $stageDir) {
    Remove-Item -Recurse -Force $stageDir
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "✅ Windows Packaging Complete!" -ForegroundColor Green
Write-Host "📍 GUI Portable:  $zipApp"
Write-Host "📍 CLI Tool:      $zipCli"
$fluentSetupTarget = Join-Path $distDir "WinCompare-$Version-Setup-$Platform.exe"
if (Test-Path $fluentSetupTarget) {
    Write-Host "📍 Fluent Setup:  $fluentSetupTarget"
}
Write-Host "========================================================" -ForegroundColor Cyan
