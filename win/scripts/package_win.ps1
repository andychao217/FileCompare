# ==============================================================================
# WinCompare Universal Windows Packaging Script (PowerShell)
# Produces:
#   1. Standalone Portable Zip (WinUI 3 GUI + CLI + Rust Core)
#   2. Standalone Portable CLI Zip (wincmp + Rust Core)
#   3. WiX Native MSI Installer with Desktop & Start Menu Shortcuts
# ==============================================================================

param(
    [string]$Version = "0.6.0",
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
    Write-Error "Could not find compiled DLL at $rustDll"
    exit 1
}

# ------------------------------------------------------------------------------
# 2. Publish WinUI 3 Desktop App (Self-Contained Unpackaged)
# ------------------------------------------------------------------------------
Write-Host "`n[2/5] Publishing WinCompare.UI (win-$Platform)..." -ForegroundColor Yellow
$uiProj = Join-Path $winDir "src\WinCompare.UI\WinCompare.UI.csproj"
dotnet publish $uiProj -c Release -r "win-$Platform" --self-contained true `
    /p:Platform=$Platform /p:Version=$Version /p:PublishSingleFile=false /p:DebugType=None /p:DebugSymbols=false -o $stageDir

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
    /p:Platform=$Platform /p:Version=$Version /p:DebugType=None /p:DebugSymbols=false -o $cliTempDir

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
# 5. Build Native MSI Installer via WiX Toolset
# ------------------------------------------------------------------------------
if (-not $SkipInstaller) {
    Write-Host "`n[5/5] Building WiX MSI Installer (with Desktop & Start Menu Shortcuts)..." -ForegroundColor Yellow
    $installerProj = Join-Path $winDir "src\WinCompare.Installer\WinCompare.Installer.wixproj"
    $msiTarget = Join-Path $distDir "WinCompare-$Version-windows-$Platform-Setup.msi"
    
    # Use VM local fast disk directory ($env:TEMP) for intermediate & CAB generation
    # to completely eliminate Parallels/VMware network share (Z:\) byte-lock delays and oplock freezes
    $localWixWorkDir = Join-Path $env:TEMP "WinCompareWixBuild"
    if (Test-Path $localWixWorkDir) { Remove-Item -Recurse -Force $localWixWorkDir -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path "$localWixWorkDir\staging" -Force | Out-Null
    New-Item -ItemType Directory -Path "$localWixWorkDir\obj" -Force | Out-Null
    New-Item -ItemType Directory -Path "$localWixWorkDir\bin" -Force | Out-Null

    try {
        Write-Host "  -> Syncing published files to local VM disk to prevent network share lock delays..." -ForegroundColor Cyan
        & robocopy.exe "$stageDir" "$localWixWorkDir\staging" /MIR /NFL /NDL /NJH /NJS /NC /NS /NP /MT:8 | Out-Null

        # Remove PDB debug symbols from installer to shrink size and speed up CAB compression
        Get-ChildItem -Path "$localWixWorkDir\staging" -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

        Write-Host "  -> Running WiX compiler on local drive..." -ForegroundColor Yellow
        dotnet build $installerProj -c Release /p:Platform=$Platform /p:Version=$Version `
            /p:PublishDir="$localWixWorkDir\staging" `
            /p:IntermediateOutputPath="$localWixWorkDir\obj\" `
            /p:OutputPath="$localWixWorkDir\bin\"
        
        $builtMsi = Join-Path $localWixWorkDir "bin\WinCompare-$Version-Setup-$Platform.msi"
        if (-not (Test-Path $builtMsi)) {
            $foundMsi = Get-ChildItem -Path "$localWixWorkDir\bin" -Filter "*.msi" -Recurse | Select-Object -First 1
            if ($foundMsi) {
                $builtMsi = $foundMsi.FullName
            }
        }

        if (Test-Path $builtMsi) {
            Copy-Item -Path $builtMsi -Destination $msiTarget -Force
            Write-Host "  -> Created MSI Installer: $msiTarget" -ForegroundColor Green
        } else {
            Write-Warning "WiX build finished but could not locate output MSI file. Checked: $builtMsi"
        }
    } catch {
        Write-Warning "WiX build failed: $_. Portable ZIP packages remain available."
    } finally {
        if (Test-Path $localWixWorkDir) {
            Remove-Item -Recurse -Force $localWixWorkDir -ErrorAction SilentlyContinue
        }
    }
} else {
    Write-Host "`n[5/5] Skipping MSI Installer build (-SkipInstaller specified)." -ForegroundColor Gray
}

# Cleanup staging directory
if (Test-Path $stageDir) {
    Remove-Item -Recurse -Force $stageDir
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "✅ Windows Packaging Complete!" -ForegroundColor Green
Write-Host "📍 GUI Portable:  $zipApp"
Write-Host "📍 CLI Tool:      $zipCli"
if (Test-Path (Join-Path $distDir "WinCompare-$Version-windows-$Platform-Setup.msi")) {
    Write-Host "📍 MSI Installer: $distDir\WinCompare-$Version-windows-$Platform-Setup.msi"
}
Write-Host "========================================================" -ForegroundColor Cyan
