# WinCompare (Windows 11 Native Edition)

**WinCompare** 是基于 Rust 高性能比对内核与微软官方 **WinUI 3 (Windows App SDK)** 深度构建的 Windows 11 原生文件比对与三向合并工具，完美遵循 **Fluent Design System**（Mica 云母材质、圆角、原生 TabView、Snap Layouts 贴靠布局、Cascadia Code 等宽字体）。

向下兼容 Windows 10（1809 及更高版本），支持在 Windows 10 虚拟机中流畅运行与调试。

---

## 📁 目录结构

```text
win/
├── WinCompare.sln                  # Visual Studio 2022 / .NET 解决方案
├── README.md                       # Windows 平台开发与构建指引
├── scripts/
│   ├── build_rust_core.bat         # Windows 本地一键编译 Rust 动态库 (maccompare_ffi.dll)
│   └── build_rust_core.sh          # Mac / Linux 跨平台构建脚本
└── src/
    ├── WinCompare.Core/            # 核心业务类库 (P/Invoke, DiffService, Excel, Word, AI)
    ├── WinCompare.UI/              # WinUI 3 原生 Fluent Design 界面程序
    └── WinCompare.Cli/             # 终端命令行工具 (wincmp.exe)
```

---

## 🚀 虚拟机快速调试与运行指南 (Win10 / Win11)

### 步骤 1：虚拟机挂载共享文件夹
在虚拟机软件（Parallels Desktop、VMware Fusion 或 VirtualBox）中，将 Mac 上的项目目录 `/Users/andychao217/Works/FileCompare` 挂载为虚拟机的网络盘或共享目录（例如 `Z:\FileCompare`）。

### 步骤 2：环境准备（虚拟机内一次性操作）
1. 安装 **.NET 10.0 SDK** (或直接安装包含 ".NET 桌面开发" 的 **Visual Studio 2026**)；
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

编译输出的 `wincmp.exe` 可无缝配置为 Git 的比对与合并工具：

```cmd
git config --global diff.tool wincmp
git config --global difftool.wincmp.cmd "wincmp diff \"$LOCAL\" \"$REMOTE\""

git config --global merge.tool wincmp
git config --global mergetool.wincmp.cmd "wincmp merge \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\""
```
