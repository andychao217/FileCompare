@echo off
setlocal enabledelayedexpansion

echo =======================================================
echo   WinCompare: Building Rust Core (maccompare_ffi.dll)
echo =======================================================

:: Use pushd instead of cd /d so Windows automatically maps a drive letter for UNC paths (\\vmware-host\...)
pushd "%~dp0..\..\core"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to enter core directory!
    exit /b 1
)

echo Current directory: %CD%
echo Checking Rust toolchain...
where cargo >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Cargo is not found in PATH! Please install Rust from https://rustup.rs/
    popd
    exit /b 1
)

echo Building maccompare-ffi in release mode...
cargo build --package maccompare-ffi --release
if %errorlevel% neq 0 (
    echo [ERROR] Cargo build failed!
    popd
    exit /b %errorlevel%
)

set DLL_SRC=%CD%\target\release\maccompare_ffi.dll
if not exist "%DLL_SRC%" (
    echo [ERROR] Could not find compiled DLL at %DLL_SRC%
    popd
    exit /b 1
)

echo.
echo Copying maccompare_ffi.dll to WinCompare project output paths...
set OUTPUT_DIRS=^
  "%~dp0..\src\WinCompare.UI\bin\x64\Debug\net10.0-windows10.0.19041.0"^
  "%~dp0..\src\WinCompare.UI\bin\x64\Release\net10.0-windows10.0.19041.0"^
  "%~dp0..\src\WinCompare.Cli\bin\x64\Debug\net10.0"^
  "%~dp0..\src\WinCompare.Cli\bin\x64\Release\net10.0"^
  "%~dp0..\src\WinCompare.UI"

for %%D in (%OUTPUT_DIRS%) do (
    if not exist "%%~D" mkdir "%%~D" 2>nul
    copy /Y "%DLL_SRC%" "%%~D\" >nul
    echo   -^> Copied to %%~D
)

popd

echo =======================================================
echo   [SUCCESS] Rust core compiled and deployed successfully!
echo =======================================================
