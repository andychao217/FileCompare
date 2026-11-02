# WinCompare (Windows 11 Native Edition)

<p align="center">
  <b>基于 Rust 高性能比对内核与微软官方 WinUI 3 (Windows App SDK) 打造的 Windows 11 原生文件比对与三向合并工具。</b><br>
  <b>A modern, high-performance native file & directory comparison and 3-way merge suite for Windows 11.</b><br>
  <b>Rust 高性能差分エンジンと WinUI 3 で構築された、Windows 11 原生ファイル比較＆3方向マージスイート。</b>
</p>

<p align="center">
  <a href="#简体中文">简体中文</a> • <a href="#english">English</a> • <a href="#日本語">日本語</a>
</p>

---

<a name="简体中文"></a>
## 简体中文

**WinCompare** 深度遵循 **Fluent Design System**（Mica 云母材质、精致圆角、原生 TabView 多标签管理、Snap Layouts 贴靠布局、Cascadia Code 字体），并与 macOS 版（MacCompare）共享同一套高吞吐 Rust 差分计算内核，实现跨平台功能与体验 100% 对齐。

向下兼容 Windows 10（1809 及更高版本），支持在 Parallels Desktop、VMware Fusion 等虚拟机中流畅运行与调试。

### 🌟 核心特性全览

- **⚡ Rust 高性能差分内核**：底层复用 `diff-core`（Myers + Token LCS 算法）、`excel-diff-core`（零拷贝 `memmap2` 与 `ahash` 百万行表格秒级比对）、`fs-scanner`（多线程并发目录扫描与硬件加速 CRC32）。
- **📊 五大核心比对模式**：
  1. **文本与代码比对 (Text Diff)**：行级与内联字符级精准差异追踪、语法高亮、差异块一键双向采纳合并。
  2. **Excel 表格全要素比对 (Excel Diff)**：支持 `.xlsx` / `.xlsb` / `.xls` / `.csv` / `.tsv` 多工作表无缝切换、像素级双向同步网格、主键 Hash Join 对齐与浮点容差过滤。
  3. **Word 文档结构化比对 (Word Diff)**：段落富文本样式流（字号、粗体、斜体、颜色）、内嵌原生表格网格差异、多媒体指纹 SHA-256 比对与 H1~H6 标题大纲。
  4. **文件夹快速对比与同步 (Folder Diff)**：大小/时间戳极速筛选与 CRC32 深度校验、规则化双向同步与 Dry-Run 演练预览。
  5. **Git 三向冲突合并 (3-Way Merge)**：直观对比 Local vs Base vs Remote，智能自动合并不冲突部分，一键解决代码冲突。
- **🤖 端侧与云端双模 AI 智能助手**：支持一键提炼变更核心意图、生成要点摘要并自动输出标准化 Conventional Commit 提交信息。
- **⌨️ 终端命令行集成 (`wincmp.exe`)**：开箱即用，可无缝注册为 Git 全局 `difftool` 与 `mergetool`。
- **📦 Fluent Design 单文件安装器**：自动创建桌面/开始菜单快捷方式，并自动将应用目录注册到系统 `PATH` 环境变量。

### 📁 目录结构

```text
win/
├── WinCompare.sln                  # Visual Studio 2022 / .NET 解决方案
├── README.md                       # Windows 平台开发与构建指引
├── Directory.Build.props           # 全局 MSBuild UTF-8 编码与 C# 语言版本配置
├── packaging/
│   └── winget/
│       └── WinCompare.WinCompare.yaml # 微软官方 WinGet 软件包清单模板
├── scripts/
│   ├── package_win.ps1             # 一键全自动化打包脚本 (ZIP 便携包 + Fluent Setup)
│   ├── package_fluent_setup.ps1    # Fluent Design 单文件安装器生成脚本
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
    ├── WinCompare.Setup/           # Fluent Design 单文件安装引导程序
    └── WinCompare.Installer/       # WiX v4/v5 安装向导工程
```

### 🚀 虚拟机快速调试与运行指南 (Win10 / Win11)

#### 步骤 1：虚拟机挂载共享文件夹
在虚拟机软件（Parallels Desktop、VMware Fusion 或 VirtualBox）中，将 Mac 上的项目目录挂载为虚拟机的网络盘或共享目录（例如 `Z:\FileCompare`）。

#### 步骤 2：环境准备（虚拟机内一次性操作）
1. 安装 **.NET 10.0 SDK** (或直接安装包含 ".NET 桌面开发" 的 **Visual Studio 2022/2026**)；
2. 安装 **Rust 工具链**：访问 [rustup.rs](https://rustup.rs/) 安装 Rust。

#### 步骤 3：编译 Rust 底层动态库
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

#### 步骤 4：运行与调试图形界面
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

### ⌨️ 终端 Git 工具集成 (wincmp)

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

### 📦 自动化打包与分发 (Fluent Setup & ZIP)

WinCompare 支持一键构建**免安装绿色版 ZIP** 以及 **Fluent Design 单文件安装包（WinCompare-Setup.exe，支持静默部署与原位升级）**。

#### 1. 本地一键打包命令
在 Windows / 虚拟机 PowerShell 中执行：
```powershell
# 一键打包（默认版本 0.7.1，平台 x64）
.\win\scripts\package_win.ps1 -Version 0.7.1

# 仅打包绿色免安装便携版 (跳过安装程序制作)
.\win\scripts\package_win.ps1 -Version 0.7.1 -SkipInstaller
```

#### 2. 打包产物输出 (`dist/` 目录)
- **`dist/WinCompare-0.7.1-Setup-x64.exe`**：现代 Fluent Design 单文件安装包（自动识别旧版原位更新、支持 `--silent` 静默安装/卸载、自适应系统 Accent 强调色与深浅主题）。
- **`dist/WinCompare-0.7.1-windows-x64.zip`**：自包含免安装绿色包，解压即用。
- **`dist/wincmp-0.7.1-windows-x64.zip`**：独立 Git 对比命令行工具包。

### 🪟 Windows 软件包管理器 (WinGet) 支持与自动化发布

WinCompare 原生支持通过微软官方 **WinGet (`winget`)** 进行安装、升级与卸载。

#### 1. 用户端安装命令
```powershell
winget install WinCompare
```
> 如需本地或离线安装测试，可直接使用项目提供的清单：
> `winget install --manifest win/packaging/winget/WinCompare.WinCompare.yaml`

#### 2. CI/CD 自动化发版配置 (类似 macOS Homebrew Tap)
流水线在 `.github/workflows/release.yml` 中集成了 `vedantmgoyal9/winget-releaser`，可在 GitHub 发布新 Release 时自动计算 Hash 并向 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) 提交更新 Pull Request。

- **Fork 官方仓库**：将 [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) Fork 到你的个人账号下。
- **配置 Repository Secret**：在仓库设置中添加 `WINGET_TOKEN`（具有 `public_repo` 与 `workflow` 权限的 Classic Personal Access Token）。
- **全自动发版**：初版收录后，每次推送以 `v*` 开头的 Tag，流水线将全自动完成向微软官方源提 PR。

---

<a name="english"></a>
## English

**WinCompare** is a modern, high-performance file comparison and 3-way merge suite crafted natively for Windows 11 with **WinUI 3 (Windows App SDK)** and the **Fluent Design System** (Mica material, refined rounded corners, native TabView, Snap Layouts, and Cascadia Code typography). It shares the same high-throughput Rust differential engine with macOS MacCompare, guaranteeing 100% feature and algorithmic parity across ecosystems.

Backward compatible with Windows 10 (version 1809+), WinCompare runs smoothly both on native hardware and inside virtual machines like Parallels Desktop and VMware Fusion.

### 🌟 Key Highlights & Features

- **⚡ High-Throughput Rust Core**: Powered by `diff-core` (Myers + Token LCS), `excel-diff-core` (zero-copy `memmap2` & `ahash` for million-row spreadsheets in seconds), and `fs-scanner` (multi-threaded directory scanning with hardware-accelerated CRC32).
- **📊 5 Specialized Comparison Modes**:
  1. **Text & Source Code Diff**: Line-by-line and intra-line character-level precision tracking, syntax highlighting, and 1-click dual-direction hunk adoption.
  2. **Excel Full-Spectrum Diff**: Seamless switching between `.xlsx` / `.xlsb` / `.xls` / `.csv` / `.tsv` worksheets, synchronized dual-pane grid, Primary Key Hash Join alignment, and floating-point tolerance filtering.
  3. **Word Structured Diff**: Rich-text formatting stream (font size, bold, italic, color), embedded table comparisons, media attachment SHA-256 fingerprinting, and H1~H6 heading outline navigation.
  4. **Folder Diff & Sync**: Lightning-fast size/timestamp triage with CRC32 deep hashing, customizable bidirectional synchronization rules, and safe Dry-Run previews.
  5. **Git 3-Way Merge**: Visual comparison across Local vs Base vs Remote, automatic collision-free merge resolution, and 1-click conflict solving.
- **🤖 Dual-Mode AI Assistant**: On-device offline inference (Google Gemma 3) and Cloud/Ollama APIs to summarize code changes and generate Conventional Commit messages.
- **⌨️ Terminal CLI Utility (`wincmp.exe`)**: Ready-to-use command-line integration for Git `difftool` and `mergetool`.
- **📦 Fluent Design Setup & Portable Deployment**: Single-file installer with automatic desktop/start menu shortcuts, system `PATH` registration, and clean uninstaller.

### 📁 Directory Structure

```text
win/
├── WinCompare.sln                  # Visual Studio 2022 / .NET Solution
├── README.md                       # Windows Development and Build Guide
├── Directory.Build.props           # Global MSBuild UTF-8 and C# LangVersion settings
├── packaging/
│   └── winget/
│       └── WinCompare.WinCompare.yaml # Microsoft WinGet Package Manifest template
├── scripts/
│   ├── package_win.ps1             # Full packaging automation (ZIP + Fluent Setup)
│   ├── package_fluent_setup.ps1    # Modern Fluent Design Single-File Installer script
│   ├── build_rust_core.bat         # CMD script for Rust DLL (maccompare_ffi.dll)
│   ├── build_rust_core.ps1         # PowerShell script for Rust DLL
│   ├── build_rust_core.sh          # Mac / Linux cross-compilation script
│   └── run_local.ps1               # Local sync runner immune to 0xC0000006 paging crashes
└── src/
    ├── WinCompare.Core/            # Core Class Library (P/Invoke, DiffService, Excel, Word, AI)
    ├── WinCompare.UI/              # WinUI 3 Native Fluent Design Application
    │   ├── Properties/PublishProfiles/ # win-x64 / win-arm64 publish profiles
    │   ├── Assets/                 # Application icons (AppIcon.ico / AppIcon.png)
    │   ├── Views/                  # Mode views and dialog components
    │   └── ViewModels/             # MVVM view models
    ├── WinCompare.Cli/             # Terminal command-line tool (wincmp.exe)
    ├── WinCompare.Setup/           # Fluent Design single-file bootstrapper
    └── WinCompare.Installer/       # WiX v4/v5 installer project
```

### 🚀 Virtual Machine Debugging & Execution Guide (Win10 / Win11)

#### Step 1: Mount Host Directory in VM
In your virtualization software (Parallels Desktop, VMware Fusion, or VirtualBox), mount the host project directory to a network share or mapped drive (e.g., `Z:\FileCompare`).

#### Step 2: Environment Prerequisites (One-time)
1. Install **.NET 10.0 SDK** (or **Visual Studio 2022/2026** with the ".NET desktop development" workload);
2. Install the **Rust toolchain** via [rustup.rs](https://rustup.rs/).

#### Step 3: Compile Rust Native DLL
Open a terminal in `win\scripts` and run:
- With **PowerShell** (Recommended):
  ```powershell
  .\build_rust_core.ps1
  ```
- With **CMD**:
  ```cmd
  build_rust_core.bat
  ```
> Compiles the Rust workspace in `core/`, produces `maccompare_ffi.dll`, and copies it to build outputs.

#### Step 4: Run & Debug UI Application
> ⚠️ **Critical Notice for VM Shared Folder Users**:
> WinUI 3 utilizes OS **Memory-Mapped Files (on-demand paging)** to load XAML binary assets (e.g., `TextDiffPage.xbf`). Running directly on network shares (like `Z:\`) may trigger **`Fatal error. 0xC0000006 (STATUS_IN_PAGE_ERROR)`** due to hypervisor driver page fault limitations.
> **We strongly recommend the official local sync script:**

**Method A: One-Click Local Build & Sync (Recommended, 0xC0000006 immune)**
```powershell
.\scripts\run_local.ps1
```
> Mirrors built binaries incrementally via `robocopy` to `%LOCALAPPDATA%\WinCompareDev` and launches in sub-second time.

**Method B: Visual Studio Debugging**
Open `Z:\FileCompare\win\WinCompare.sln` in Visual Studio, set `WinCompare.UI` as the startup project, and press **F5**.

**Method C: Command Line In-Place Run**
```powershell
cd Z:\FileCompare\win\src\WinCompare.UI
dotnet run -p:Platform=x64
```

### ⌨️ Terminal Git Integration (wincmp)

Configure `wincmp.exe` as your system-wide Git diff and merge tool:

```cmd
git config --global diff.tool wincmp
git config --global difftool.wincmp.cmd "wincmp diff \"$LOCAL\" \"$REMOTE\""

git config --global merge.tool wincmp
git config --global mergetool.wincmp.cmd "wincmp merge \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\""
```

Common terminal commands:
```cmd
# 1. Compare text or code files
wincmp diff file1.txt file2.txt

# 2. Compare Excel / spreadsheet files
wincmp diff table_v1.xlsx table_v2.xlsx

# 3. Compare Word documents
wincmp diff doc1.docx doc2.docx

# 4. Resolve Git 3-way merge conflict
wincmp merge local.py base.py remote.py merged.py
```

### 📦 Automated Packaging & Distribution (Fluent Setup & ZIP)

WinCompare provides one-click packaging for standalone portable ZIPs and modern single-file Fluent Design installers (`WinCompare-Setup.exe` with silent installation support).

#### 1. Packaging Commands
```powershell
# Full release package (Version 0.7.1, x64)
.\win\scripts\package_win.ps1 -Version 0.7.1

# Portable ZIP only (skip installer build)
.\win\scripts\package_win.ps1 -Version 0.7.1 -SkipInstaller
```

#### 2. Generated Artifacts (`dist/`)
- **`dist/WinCompare-0.7.1-Setup-x64.exe`**: Fluent Design single-file installer (in-place upgrades, `--silent` unattended deployment, adaptive system accent).
- **`dist/WinCompare-0.7.1-windows-x64.zip`**: Self-contained portable ZIP.
- **`dist/wincmp-0.7.1-windows-x64.zip`**: Standalone Git CLI utility archive.

### 🪟 Windows Package Manager (WinGet) Support & CI/CD Automation

WinCompare natively supports installation, upgrade, and removal via Microsoft **WinGet (`winget`)**.

#### 1. User Installation
```powershell
winget install WinCompare
```
> For local/offline testing:
> `winget install --manifest win/packaging/winget/WinCompare.WinCompare.yaml`

#### 2. CI/CD Release Pipeline (Similar to macOS Homebrew Tap)
The GitHub Actions workflow in `.github/workflows/release.yml` includes `vedantmgoyal9/winget-releaser` to submit pull requests to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) automatically upon new tag releases.

- **Fork Upstream**: Fork [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) to your account.
- **Repository Secret**: Add `WINGET_TOKEN` (Classic PAT with `public_repo` and `workflow` scopes).
- **Automated Workflow**: Once the initial package is merged, every new release tag triggers an automated update PR with recalculated hashes.

---

<a name="日本語"></a>
## 日本語

**WinCompare** は、マイクロソフト公式の **WinUI 3 (Windows App SDK)** と **Fluent Design System**（Mica 素材、洗練された角丸、ネイティブ TabView マルチタブ管理、Snap Layouts スナップレイアウト、Cascadia Code タイポグラフィ）に準拠して設計された、Windows 11 向け純正ファイル比較＆3方向マージツールです。macOS 版（MacCompare）と共通の高スループット Rust 差分計算エンジンを共有し、クロスプラットフォームで 100% 同一のアルゴリズムと使用感を実現しています。

Windows 10（バージョン 1809 以降）に下位互換性があり、実機はもちろん Parallels Desktop や VMware Fusion などの仮想マシン環境でも軽快に動作します。

### 🌟 主な機能と特徴

- **⚡ Rust 高性能差分エンジン**：`diff-core`（Myers ＆ Token LCS アルゴリズム）、`excel-diff-core`（ゼロコピー `memmap2` と `ahash` による数百万行テーブルの高速比較）、`fs-scanner`（マルチスレッド並行スキャンとハードウェアアクセラレーション CRC32）を搭載。
- **📊 5大コア比較モード**：
  1. **テキスト＆ソースコード比較 (Text Diff)**：行単位およびインライン文字単位の高精度差分追跡、シンタックスハイライト、1クリック双方向差分マージ。
  2. **Excel 表計算全要素比較 (Excel Diff)**：`.xlsx` / `.xlsb` / `.xls` / `.csv` / `.tsv` のマルチシート切り替え、ピクセル同期グリッド、主キー Hash Join 位置合わせ、浮動小数点許容値フィルタ。
  3. **Word 文書構造化比較 (Word Diff)**：段落リッチテキストスタイル（フォントサイズ、太字、斜体、文字色）、埋め込みテーブル差分、メディア添付ファイルの SHA-256 フィンガープリント、見出しアウトラインナビゲーション。
  4. **フォルダー高速比較＆同期 (Folder Diff)**：サイズ/タイムスタンプの高速スクリーニングと CRC32 詳細検証、ルール化された双方向同期、Dry-Run（シミュレーション）プレビュー機能。
  5. **Git 3方向競合マージ (3-Way Merge)**：Local vs Base vs Remote を直感的に並行比較。競合のない変更をスマートに自動統合し、コード競合を素早く解決。
- **🤖 デュアルモード AI アシスタント**：端末側オフライン推論（Google Gemma 3）およびクラウド/ローカル Ollama API に対応。コード変更の要約作成や Conventional Commit メッセージを自動生成。
- **⌨️ ターミナル CLI ツール (`wincmp.exe`)**：Git の `difftool` および `mergetool` として即座に登録可能。
- **📦 Fluent Design 単一ファイルインストーラー**：デスクトップおよびスタートメニューへのショートカット作成、システム `PATH` 環境変数への自動登録、サイレントインストールに対応。

### 📁 ディレクトリ構成

```text
win/
├── WinCompare.sln                  # Visual Studio 2022 / .NET ソリューション
├── README.md                       # Windows 開発・ビルドガイド
├── Directory.Build.props           # MSBuild UTF-8 エンコードおよび C# 言語バージョン設定
├── packaging/
│   └── winget/
│       └── WinCompare.WinCompare.yaml # Microsoft 公式 WinGet パッケージマニフェスト雛形
├── scripts/
│   ├── package_win.ps1             # ワンクリック自動パッケージング (ZIP + Fluent Setup)
│   ├── package_fluent_setup.ps1    # Fluent Design 単一ファイルインストーラー作成スクリプト
│   ├── build_rust_core.bat         # Rust DLL (maccompare_ffi.dll) ビルド用 CMD スクリプト
│   ├── build_rust_core.ps1         # Rust DLL ビルド用 PowerShell スクリプト
│   ├── build_rust_core.sh          # Mac / Linux クロスコンパイルスクリプト
│   └── run_local.ps1               # 0xC0000006 クラッシュ防止用ローカル同期実行スクリプト
└── src/
    ├── WinCompare.Core/            # コア業務ライブラリ (P/Invoke, DiffService, Excel, Word, AI)
    ├── WinCompare.UI/              # WinUI 3 純正 Fluent Design UI アプリケーション
    │   ├── Properties/PublishProfiles/ # win-x64 / win-arm64 発行プロファイル
    │   ├── Assets/                 # アプリアイコン (AppIcon.ico / AppIcon.png)
    │   ├── Views/                  # 各比較モードのページおよびダイアログ
    │   └── ViewModels/             # MVVM ビューモデル
    ├── WinCompare.Cli/             # ターミナルコマンドラインツール (wincmp.exe)
    ├── WinCompare.Setup/           # Fluent Design 単一インストーラープログラム
    └── WinCompare.Installer/       # WiX v4/v5 インストーラープロジェクト
```

### 🚀 仮想マシンでの高速デバッグ＆実行ガイド (Win10 / Win11)

#### ステップ 1：仮想マシンに共有フォルダーをマウント
仮想化ソフト（Parallels Desktop、VMware Fusion、VirtualBox など）で、Mac ホスト側のプロジェクトフォルダーを仮想マシンの共有ドライブ（例: `Z:\FileCompare`）としてマウントします。

#### ステップ 2：環境の準備（仮想マシン内で初回のみ）
1. **.NET 10.0 SDK**（または「.NET デスクトップ開発」を含む **Visual Studio 2022/2026**）をインストール；
2. **Rust ツールチェーン**：[rustup.rs](https://rustup.rs/) から Rust をインストール。

#### ステップ 3：Rust ネイティブ DLL のビルド
仮想マシンのターミナルで `win\scripts` に移動して実行：
- **PowerShell** を使用する場合（推奨）：
  ```powershell
  .\build_rust_core.ps1
  ```
- **CMD** を使用する場合：
  ```cmd
  build_rust_core.bat
  ```
> `core/` 配下の Rust コードがコンパイルされ、`maccompare_ffi.dll` が各プロジェクトの出力先に配置されます。

#### ステップ 4：GUI アプリケーションの実行とデバッグ
> ⚠️ **重要（仮想マシン共有フォルダー使用時の注意点）**：
> WinUI 3 は XAML バイナリリソース（`TextDiffPage.xbf` など）の読み込みに OS の**メモリマップトファイル（オンデマンドページング）**を使用します。ネットワーク共有フォルダー（`Z:\` など）上で直接実行すると、ハイパーバイザードライバーの制限により **`Fatal error. 0xC0000006 (STATUS_IN_PAGE_ERROR)`** が発生してクラッシュすることがあります。
> **公式提供のローカル同期実行スクリプトの使用を強く推奨します：**

**方法 A：ワンクリックコンパイル＆ローカル同期実行（推奨、0xC0000006 防止）**
```powershell
.\scripts\run_local.ps1
```
> `robocopy` により差分ファイルが仮想マシンのローカル領域（`%LOCALAPPDATA%\WinCompareDev`）へ即座に同期され、安全に起動します。

**方法 B：Visual Studio デバッグ**
Visual Studio で `Z:\FileCompare\win\WinCompare.sln` を開き、`WinCompare.UI` をスタートアッププロジェクトに設定して **F5** を押します。

**方法 C：コマンドラインからの実行**
```powershell
cd Z:\FileCompare\win\src\WinCompare.UI
dotnet run -p:Platform=x64
```

### ⌨️ ターミナル Git 連携 (wincmp)

ビルドまたはインストールされた `wincmp.exe` は、Git の標準差分・マージツールとして設定できます：

```cmd
git config --global diff.tool wincmp
git config --global difftool.wincmp.cmd "wincmp diff \"$LOCAL\" \"$REMOTE\""

git config --global merge.tool wincmp
git config --global mergetool.wincmp.cmd "wincmp merge \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\""
```

日常的なコマンドライン呼び出し例：
```cmd
# 1. 2つのテキストまたはソースコードファイルを比較
wincmp diff file1.txt file2.txt

# 2. 2つの Excel / 表計算ファイルを比較
wincmp diff table_v1.xlsx table_v2.xlsx

# 3. 2つの Word 文書を比較
wincmp diff doc1.docx doc2.docx

# 4. Git 3方向競合のマージ
wincmp merge local.py base.py remote.py merged.py
```

### 📦 自動パッケージング＆配布 (Fluent Setup ＆ ZIP)

WinCompare は、ポータブルな**インストール不要 ZIP** および現代的な **Fluent Design 単一ファイルインストーラー（WinCompare-Setup.exe、サイレントインストール対応）** のワンクリック生成に対応しています。

#### 1. パッケージングコマンド
```powershell
# 標準リリースパッケージの作成（バージョン 0.7.1, x64）
.\win\scripts\package_win.ps1 -Version 0.7.1

# ポータブル ZIP のみ作成（インストーラー生成をスキップ）
.\win\scripts\package_win.ps1 -Version 0.7.1 -SkipInstaller
```

#### 2. 生成される配布物 (`dist/`)
- **`dist/WinCompare-0.7.1-Setup-x64.exe`**：Fluent Design 単一ファイルインストーラー（自動インプレースアップデート、`--silent` による無人インストール/アンインストール対応）。
- **`dist/WinCompare-0.7.1-windows-x64.zip`**：解凍してすぐに使えるポータブル ZIP。
- **`dist/wincmp-0.7.1-windows-x64.zip`**：Git 比較用スタンドアロン CLI ツールアーカイブ。

### 🪟 Windows パッケージマネージャー (WinGet) 対応と CI/CD 自動リリース

WinCompare は、マイクロソフト公式の **WinGet (`winget`)** によるインストール、アップデート、アンインストールにネイティブ対応しています。

#### 1. ユーザーインストールコマンド
```powershell
winget install WinCompare
```
> ローカルまたはオフラインでテストする場合：
> `winget install --manifest win/packaging/winget/WinCompare.WinCompare.yaml`

#### 2. CI/CD 自動リリース設定 (macOS Homebrew Tap と同様)
`.github/workflows/release.yml` に `vedantmgoyal9/winget-releaser` が統合されており、GitHub Release が公開されると SHA256 ハッシュが自動計算され、[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) へ Pull Request が自動送信されます。

- **公式リポジトリの Fork**：[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) をご自身のアカウントに Fork します。
- **Repository Secret の設定**：リポジトリ設定で `WINGET_TOKEN`（`public_repo` と `workflow` 権限を持つ Classic Personal Access Token）を登録します。
- **全自動更新**：初回パッケージの承認後、新しいバージョンタグ（`v*`）をプッシュするたびに、更新 PR が自動発行されます。
