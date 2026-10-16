# WinCompare (Windows 11 Native Edition)

<p align="center">
  <b>基于 Rust 高性能比对内核与微软官方 WinUI 3 (Windows App SDK) 打造的 Windows 11 原生文件比对与三向合并工具。</b><br>
  <b>A modern, high-performance native file & directory comparison suite for Windows 11.</b>
</p>

---

**WinCompare** 深度遵循 **Fluent Design System**（Mica 云母材质、精致圆角、原生 TabView 多标签管理、Snap Layouts 贴靠布局、Cascadia Code 字体），并与 macOS 版（MacCompare）共享同一套高吞吐 Rust 差分计算内核，实现跨平台功能与体验 100% 对齐。

向下兼容 Windows 10（1809 及更高版本），支持在 Parallels Desktop、VMware Fusion 等虚拟机中流畅运行与调试。

---

## 🌟 核心特性全览

- **⚡ Rust 高性能差分内核**：底层复用 `diff-core`（Myers + Token LCS 算法）、`excel-diff-core`（零拷贝 `memmap2` 与 `ahash` 百万行表格秒级比对）、`fs-scanner`（多线程并发目录扫描与硬件加速 CRC32）。
- **📊 五大核心比对模式**：
  1. **文本与代码比对 (Text Diff)**：行级与内联字符级精准差异追踪、语法高亮、差异块一键双向采纳合并。
  2. **Excel 表格全要素比对 (Excel Diff)**：支持 `.xlsx` / `.xlsb` / `.xls` / `.csv` / `.tsv` 多工作表无缝切换、像素级双向同步网格、主键 Hash Join 对齐与浮点容差过滤。
  3. **Word 文档结构化比对 (Word Diff)**：段落富文本样式流（字号、粗体、斜体、颜色）、内嵌原生表格网格差异、多媒体指纹 SHA-256 比对与 H1~H6 标题大纲。
  4. **文件夹快速对比与同步 (Folder Diff)**：大小/时间戳极速筛选与 CRC32 深度校验、规则化双向同步与 Dry-Run 演练预览。
  5. **Git 三向冲突合并 (3-Way Merge)**：直观对比 Local vs Base vs Remote，智能自动合并不冲突部分，一键解决代码冲突。
- **🤖 端侧与云端双模 AI 智能助手**：支持一键提炼变更核心意图、生成要点摘要并自动输出标准化 Conventional Commit 提交信息。
- **⌨️ 终端命令行集成 (`wincmp.exe`)**：开箱即用，可无缝注册为 Git 全局 `difftool` 与 `mergetool`。
- **📦 WiX 原生安装向导**：自动创建桌面/开始菜单快捷方式，并自动将应用目录注册到系统 `PATH` 环境变量。

---

## 📁 目录结构

```text
win/
├── WinCompare.sln                  # Visual Studio 2022 / .NET 解决方案
├── README.md                       # Windows 平台开发与构建指引
├── Directory.Build.props           # 全局 MSBuild UTF-8 编码与 C# 语言版本配置
├── scripts/
│   ├── package_win.ps1             # 一键全自动化打包脚本 (ZIP 便携包 + WiX MSI 安装向导)
│   ├── build_rust_core.bat         # Windows CMD 编译 Rust 动态库 (maccompare_ffi.dll)
│   ├── build_rust_core.ps1         # PowerShell 编译 Rust 动态库
│   ├── build_rust_core.sh          # Mac / Linux 跨平台构建脚本
│   └── run_local.ps1               # 防 0xC0000006 闪退的本地同步调试运行脚本
└── src/
    ├── WinCompare.Core/            # 核心业务类库 (P/Invoke, DiffService, Excel, Word, AI)
    ├── WinCompare.UI/              # WinUI 3 原生 Fluent Design 界面程序
    │   ├── Properties/PublishProfiles/ # win-x64 / win-arm64 发布配置文件
    │   ├── Assets/                 # 应用图标 (AppIcon.ico / AppIcon.png)
    │   ├── Views/                  # 各比对模式页面与弹窗控件
    │   └── ViewModels/             # MVVM 视图模型
    ├── WinCompare.Cli/             # 终端命令行工具 (wincmp.exe)
    └── WinCompare.Installer/       # WiX v4/v5 安装向导工程 (桌面/开始菜单快捷方式 + PATH 环境变量)
```

---

## 🚀 虚拟机快速调试与运行指南 (Win10 / Win11)

### 步骤 1：虚拟机挂载共享文件夹
在虚拟机软件（Parallels Desktop、VMware Fusion 或 VirtualBox）中，将 Mac 上的项目目录 `/Users/andychao217/Works/FileCompare` 挂载为虚拟机的网络盘或共享目录（例如 `Z:\FileCompare`）。

### 步骤 2：环境准备（虚拟机内一次性操作）
1. 安装 **.NET 10.0 SDK** (或直接安装包含 ".NET 桌面开发" 的 **Visual Studio 2022/2026**)；
2. 安装 **Rust 工具链**：访问 [rustup.rs](https://rustup.rs/) 安装 Rust。

### 步骤 3：编译 Rust 底层动态库
在虚拟机终端中进入 `win\scripts` 执行：
- 如果使用 **PowerShell**（推荐）：
  ```powershell
  .\build_rust_core.ps1
  ```
- 如果使用 **CMD**：
  ```cmd
  build_rust_core.bat
  ```
> 该脚本会自动编译 `core/` 下的 Rust 代码，生成 `maccompare_ffi.dll` 并自动部署到各项目的输出目录中。

### 步骤 4：运行与调试图形界面
> ⚠️ **重要注意（虚拟机共享盘用户必读）**：
> WinUI 3 采用操作系统内核的**内存映射文件（Memory-Mapped Files / 按需分页）**加载 XAML 二进制资源（如 `TextDiffPage.xbf`）。在 Parallels Desktop 或 VMware 的网络共享盘（如 `Z:\`）上直接运行程序时，可能因网络共享驱动不支持缺页中断而偶发报错 **`Fatal error. 0xC0000006 (STATUS_IN_PAGE_ERROR)`** 并闪退。
> **强烈推荐使用官方提供的本地同步启动脚本：**

**方法 A：一键编译并本地运行（推荐，防 0xC0000006 闪退）**
在 PowerShell 中执行：
```powershell
.\scripts\run_local.ps1
```
> 该脚本会在编译后通过 `robocopy` 增量镜像至虚拟机本地安全目录（`%LOCALAPPDATA%\WinCompareDev`）并启动，毫秒级响应且 100% 免疫共享盘分页崩溃。

**方法 B：Visual Studio 调试**
直接在 Visual Studio 2022/2026 中打开 `Z:\FileCompare\win\WinCompare.sln`，设置 `WinCompare.UI` 为启动项，按 **F5** 即可启动。

**方法 C：命令行原地运行**
```powershell
cd Z:\FileCompare\win\src\WinCompare.UI
dotnet run -p:Platform=x64
```

---

## ⌨️ 终端 Git 工具集成 (wincmp)

编译或安装后的 `wincmp.exe` 可无缝配置为 Git 的默认比对与合并工具：

```cmd
git config --global diff.tool wincmp
git config --global difftool.wincmp.cmd "wincmp diff \"$LOCAL\" \"$REMOTE\""

git config --global merge.tool wincmp
git config --global mergetool.wincmp.cmd "wincmp merge \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\""
```

终端日常调用示例：
```cmd
# 1. 对比两个代码或文本文件
wincmp diff file1.txt file2.txt

# 2. 对比两个 Excel / 表格文件
wincmp diff table_v1.xlsx table_v2.xlsx

# 3. 对比两个 Word 文档
wincmp diff doc1.docx doc2.docx

# 4. Git 三向合并冲突
wincmp merge local.py base.py remote.py merged.py
```

---

## 📦 自动化打包与分发 (WiX MSI & ZIP)

WinCompare 支持一键构建**免安装绿色版 ZIP** 以及 **标准 MSI 安装包（带桌面、开始菜单快捷方式与终端 PATH 注册）**。脚本内置了**本地磁盘高速缓冲机制**，彻底解决在虚拟机网络共享盘（`Z:\`）上压缩 CAB 导致的缓慢或卡顿问题。

### 1. 本地一键打包命令
在 Windows / 虚拟机 PowerShell 中执行：
```powershell
# 一键打包（默认版本 0.6.0，平台 x64）
.\win\scripts\package_win.ps1 -Version 0.6.0

# 仅打包绿色免安装便携版 (跳过 WiX 安装包)
.\win\scripts\package_win.ps1 -Version 0.6.0 -SkipInstaller
```

### 2. 打包产物输出 (`dist/` 目录)
- **`dist/WinCompare-0.6.0-windows-x64-Setup.msi`**：微软标准 MSI 安装向导（含许可协议签署、自定义安装目录、桌面图标、开始菜单快捷方式以及向系统注册 `wincmp` 命令行工具）。
- **`dist/WinCompare-0.6.0-windows-x64.zip`**：自包含免安装绿色包，解压即用。
- **`dist/wincmp-0.6.0-windows-x64.zip`**：独立 Git 对比命令行工具包。

### 3. GitHub 持续集成与发布
每次推送到以 `v*` 开头的 Tag 时，GitHub Actions（`.github/workflows/release.yml`）会自动启动 Windows 构建环境（`windows-2022`），并行完成打包，并将 Windows 与 macOS 的全部资产统一挂载发布到同一个 GitHub Release 下。
