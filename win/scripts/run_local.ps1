# WinCompare: 本地快速构建与运行脚本
# 解决 Parallels/VMware 共享目录 (Z:\) 下运行 WinUI 3 报 0xC0000006 STATUS_IN_PAGE_ERROR 闪退问题

param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [string]$LocalDeployDir = "$env:LOCALAPPDATA\WinCompareDev",
    [switch]$NoBuild,
    [switch]$Clean
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$slnPath = Join-Path $scriptDir "..\WinCompare.sln"
$uiProjPath = Join-Path $scriptDir "..\src\WinCompare.UI\WinCompare.UI.csproj"

Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  WinCompare: Local Deploy & Runner" -ForegroundColor Cyan
Write-Host "  Target Local Dir: $LocalDeployDir" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan

# 自动检测并自愈 XamlCompiler 残缺文件（避免因生成中断留下 3 字节残缺 .g.cs 导致页面控件未能与 XAML 绑定）
$objViewsDir = Join-Path $scriptDir "..\src\WinCompare.UI\obj\$Platform\$Configuration\net10.0-windows10.0.19041.0\Views"
$stateFile = Join-Path $scriptDir "..\src\WinCompare.UI\obj\$Platform\$Configuration\net10.0-windows10.0.19041.0\XamlSaveStateFile.xml"
$hasCorruptedGcs = $false
if (Test-Path $objViewsDir) {
    $corrupted = Get-ChildItem -Path $objViewsDir -Filter "*.g.cs" -ErrorAction SilentlyContinue | Where-Object { $_.Length -le 10 }
    if ($corrupted -and $corrupted.Count -gt 0) {
        $hasCorruptedGcs = $true
    }
}

if ($Clean -or $hasCorruptedGcs) {
    Write-Host "`n[XamlCheck] 检测到 XAML 编译器残缺产物或指定了 -Clean，正在清理 XAML 缓存以触发完整重编译..." -ForegroundColor Yellow
    if (Test-Path $stateFile) { Remove-Item $stateFile -Force -ErrorAction SilentlyContinue }
    if (Test-Path $objViewsDir) {
        Get-ChildItem -Path $objViewsDir -Filter "*.g.cs" -ErrorAction SilentlyContinue | Where-Object { $_.Length -le 10 } | Remove-Item -Force -ErrorAction SilentlyContinue
    }
    Get-ChildItem -Path (Join-Path $scriptDir "..\src\WinCompare.UI") -Filter "*.xaml" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $_.LastWriteTime = [DateTime]::Now
    }
}

# 1. 编译项目 (除非指定 -NoBuild)
if (-not $NoBuild) {
    Write-Host "`n[1/3] 正在编译 WinCompare.UI ($Configuration | $Platform)..." -ForegroundColor Yellow
    dotnet build $uiProjPath -c $Configuration /p:Platform=$Platform
    if ($LASTEXITCODE -ne 0) {
        Write-Error "构建失败，退出码: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
} else {
    Write-Host "`n[1/3] 跳过构建步骤 (-NoBuild)" -ForegroundColor Gray
}

# 2. 定位编译产物目录
$sourceBinDir = Join-Path $scriptDir "..\src\WinCompare.UI\bin\$Platform\$Configuration\net10.0-windows10.0.19041.0"
if (-not (Test-Path $sourceBinDir)) {
    Write-Error "找不到构建输出目录: $sourceBinDir"
    exit 1
}

# 3. 增量同步到虚拟机本地磁盘 (使用 robocopy 仅同步变动文件，毫秒级完成)
Write-Host "`n[2/3] 正在将构建产物增量同步到虚拟机本地磁盘..." -ForegroundColor Yellow
if (-not (Test-Path $LocalDeployDir)) {
    New-Item -ItemType Directory -Path $LocalDeployDir -Force | Out-Null
}

# robocopy 返回码 0-7 均代表成功 (0=无变化, 1=有文件复制)
$robocopyArgs = @(
    $sourceBinDir,
    $LocalDeployDir,
    "/MIR",
    "/NJH",
    "/NJS",
    "/NDL",
    "/NC",
    "/NS",
    "/NP"
)
& robocopy.exe @robocopyArgs
if ($LASTEXITCODE -gt 7) {
    Write-Error "同步文件失败，Robocopy 退出码: $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 检查可执行文件是否存在
$targetExe = Join-Path $LocalDeployDir "WinCompare.UI.exe"
if (-not (Test-Path $targetExe)) {
    Write-Error "在本地部署目录中未找到 WinCompare.UI.exe: $targetExe"
    exit 1
}

# 4. 启动程序
Write-Host "`n[3/3] 从本地安全目录启动 WinCompare..." -ForegroundColor Green
Write-Host "运行路径: $targetExe" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan

Start-Process -FilePath $targetExe -WorkingDirectory $LocalDeployDir
