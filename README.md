# FileCompare (MacCompare & WinCompare)

<p align="center">
  <b>A modern, high-performance native file & directory comparison and merge suite for macOS & Windows 11.</b><br>
  <b>专为 macOS 与 Windows 11 双生态深度打造的高性能、现代原生文件与目录对比合并工具。</b>
</p>

<p align="center">
  <a href="#english">English</a> • <a href="#简体中文">简体中文</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-macOS%20%7C%20Windows%2011-blue.svg" alt="Platform" />
  <img src="https://img.shields.io/badge/Core-Rust%202024-orange.svg" alt="Rust Core" />
  <img src="https://img.shields.io/badge/macOS%20UI-SwiftUI%20%2F%20AppKit-red.svg" alt="SwiftUI" />
  <img src="https://img.shields.io/badge/Windows%20UI-WinUI%203%20%2F%20Fluent-0078D4.svg" alt="WinUI 3" />
  <img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License" />
</p>

---

<a name="english"></a>
## English

> **FileCompare** is a modern, cross-platform native comparison suite consisting of **MacCompare** (macOS Native, SwiftUI / AppKit) and **WinCompare** (Windows 11 Native, WinUI 3 / Windows App SDK). Both clients share a high-throughput **Rust differential calculation core**, delivering sub-millisecond diff speeds and 100% algorithm parity.

### 🌟 Key Highlights & Ecosystem

| Platform | Native UI Stack | Architecture | Packaging & Distribution | CLI Utility |
| :--- | :--- | :--- | :--- | :--- |
| 🍏 **MacCompare** | SwiftUI + AppKit | Universal Binary 2 (`arm64` + `x86_64`) | `.dmg`, `.zip`, Homebrew Tap | `mcdiff` |
| 🪟 **WinCompare** | WinUI 3 + Fluent Design | Windows 11 / 10 (`x64` + `ARM64`) | WiX `.msi` Installer, Portable `.zip` | `wincmp` |
| ⚡ **Shared Core** | Rust 2024 Cargo Workspace | Native Static Library / DLL | Embedded FFI (`diff-core`, `excel-diff-core`, `fs-scanner`) | C-ABI / P-Invoke |

---

### 🌟 Core Features

- **🤖 Dual-Mode AI Assistant (Local Offline Gemma 3 & Cloud/Ollama API)**:
  - **100% On-Device Local AI**: Integrated Google Gemma 3 (1B-IT blazing fast & 4B-IT advanced) inference. Completely offline with zero code leaks.
  - **Cloud & LAN LLMs**: Preset adapters for DeepSeek, OpenAI, local/LAN Ollama (`qwen2.5-coder`, `llama3.2`), with 1-click connectivity and latency testing.
  - **Smart Change Intent Summary**: Automatically distills the overarching purpose of paired diffs into a concise one-line summary and structured key modification bullet points.
  - **Automated Conventional Commits**: Generates standardized Git commit messages (`feat(core): ...`, `fix(ui): ...`) with 1-click clipboard copy.
- **⚡ High-Throughput Rust Core & Dual-Engine Architecture**:
  - **Myers & Token LCS Diff (`diff-core`)**: Sub-millisecond text diffing and 3-way merge conflict resolution.
  - **Big-Data Spreadsheet Engine (`excel-diff-core`)**: Memory-mapped zero-copy parsing via `calamine` and `memmap2`, 64-bit row hash fingerprinting with `ahash`, and Viewport Paging maintaining a tiny 30~60MB memory footprint on million-row tables.
  - **Concurrent Directory Scanner (`fs-scanner`)**: Multi-threaded parallel file traversal powered by `rayon` and hardware-accelerated `crc32fast`.
- **📊 Excel & Spreadsheet Structured Comparison (.xlsx / .xlsb / .xls / .csv / .tsv)**:
  - Multi-sheet switching with diff-status badges, pixel-perfect synchronized grid scrolling, primary key Hash Join alignment, cell-level & inline character diff highlights, and configurable numeric floating-point tolerance (`|v1 - v2| <= tolerance`).
- **📄 Structured Word Document Comparison (.docx / .doc)**:
  - High-fidelity paragraph styling diff (font weight, italics, underline, size, color), native embedded table cell diffs, multimedia SHA-256 fingerprinting, and H1~H6 heading outline navigation.
- **📁 Blazing-Fast Folder Diff & Sync**:
  - Timestamp/size quick checks, deep CRC32 hash verification, and rule-based bidirectional synchronization with safe Dry-Run previews.
- **🔀 Git 3-Way Conflict Merge**:
  - Visual 3-way conflict resolution across Local vs. Base vs. Remote, intelligent auto-merging of non-conflicting sections, and 1-click conflict resolution.
- **🏠 Welcome Launcher Dashboard & Persistent History**:
  - 2x2 Launch Grid (Text, Excel, Word, Folder), persistent session history, independent drag-and-drop zones, and rapid tab switching.
- **⌨️ Terminal CLI & Git Integration**:
  - Built-in `mcdiff` (macOS) and `wincmp` (Windows), out of the box for `git difftool` and `git mergetool`.
- **📦 Native Packaging & In-App Auto-Update**:
  - **macOS**: Drag-and-drop `.dmg`, portable `.zip`, and official Homebrew Tap (`brew install andychao217/tap/maccompare`).
  - **Windows**: WiX v4/v5 `.msi` wizard (Desktop & Start Menu shortcuts, system `PATH` registration) and portable `.zip`.
  - **In-App Updater**: Cross-platform auto-update flow with multi-language release notes auto-sensing.

---

### 📸 Screenshots & UI Preview

#### 1. Windows 11 Native Edition (WinUI 3 & Fluent Design)

| Welcome Hub | Fine-Grained Text Diff & AI Summary | Excel Spreadsheet Diff |
| :---: | :---: | :---: |
| ![Win11 Welcome](docs/assets/win/welcome_home_dark_ui.png) | ![Win11 Text Diff](docs/assets/win/text_diff_ai_dark_ui.png) | ![Win11 Excel Diff](docs/assets/win/excel_diff_dark_ui.png) |

| Word Document Diff | Git 3-Way Conflict Merge | Folder Fast Diff & Sync |
| :---: | :---: | :---: |
| ![Win11 Word Diff](docs/assets/win/word_diff_dark_ui.png) | ![Win11 3-Way Merge](docs/assets/win/three_way_merge_dark_ui.png) | ![Win11 Folder Diff](docs/assets/win/folder_diff_dark_ui.png) |

#### 2. macOS Native Edition (SwiftUI & AppKit)

| Welcome Hub | Text Diff with AI Summary | Excel Structured Diff |
| :---: | :---: | :---: |
| ![Mac Welcome](docs/assets/macos/welcome_home_dark_ui.png) | ![Mac Text Diff](docs/assets/macos/text_diff_ai_dark_ui.png) | ![Mac Excel Diff](docs/assets/macos/excel_diff_dark_ui.png) |

| Word Document Diff | Git 3-Way Conflict Merge | Folder Fast Diff & Sync |
| :---: | :---: | :---: |
| ![Mac Word Diff](docs/assets/macos/word_diff_dark_ui.png) | ![Mac 3-Way Merge](docs/assets/macos/three_way_merge_ui.jpg) | ![Mac Folder Diff](docs/assets/macos/folder_diff_dark_ui.png) |

---

### 📁 Project Structure

```text
FileCompare/
├── core/                             # High-performance Rust core engine (Cargo Workspace)
│   ├── crates/diff-core/             # Myers Diff algorithm, token LCS & 3-Way Merge
│   ├── crates/fs-scanner/            # Parallel directory scanner (rayon) & CRC32 hash
│   ├── crates/excel-diff-core/       # Big-data spreadsheet engine (calamine, memmap2, ahash)
│   ├── crates/syntax-highlighter/    # Tree-sitter incremental syntax highlighting interface
│   └── crates/maccompare-ffi/        # Cross-language C-ABI export layer (dynamic & static library)
│
├── macos/                            # macOS native application (Swift 6 / SPM)
│   ├── Package.swift                 # SPM modular configuration
│   ├── Sources/
│   │   ├── CMacCompareCore/          # C-bridge modulemap & headers linking Rust static library
│   │   ├── MacCompareKit/            # Core models, ViewModels, Dual-Engine dispatcher & UI
│   │   ├── MacCompare/               # macOS main application & AppCommands
│   │   └── mcdiff/                   # Terminal CLI comparison tool
│   └── Tests/MacCompareTests/        # Unit test suite (Swift + Rust FFI integration)
│
├── win/                              # Windows 11 native application (.NET 10 / WinUI 3)
│   ├── WinCompare.sln                # Visual Studio 2022/2026 Solution
│   ├── scripts/                      # Windows PowerShell & Batch build scripts
│   │   ├── package_win.ps1           # 1-Click packaging (Portable ZIP + WiX MSI Installer)
│   │   ├── build_rust_core.ps1       # Compile Rust maccompare_ffi.dll
│   │   └── run_local.ps1             # Local incremental runner (prevents VM shared disk crash)
│   └── src/
│       ├── WinCompare.Core/          # C# Core libraries (P/Invoke, DiffService, Excel, Word, AI)
│       ├── WinCompare.UI/            # WinUI 3 Native Fluent Design GUI
│       ├── WinCompare.Cli/           # Terminal CLI comparison tool (wincmp.exe)
│       └── WinCompare.Installer/     # WiX v4/v5 MSI installer (Shortcuts & PATH environment)
│
├── docs/                             # Official website & documentation (GitHub Pages)
└── scripts/                          # macOS build and packaging automation scripts
```

---

### 🚀 Quick Start

#### macOS Installation
- **Homebrew (Recommended)**:
  ```bash
  brew install andychao217/tap/maccompare
  ```
- **Direct Download**: Download `MacCompare-<version>.dmg` or `MacCompare-<version>-macos.zip` from [GitHub Releases](https://github.com/andychao217/FileCompare/releases/latest).

#### Windows Installation
- **MSI Installer (Recommended)**: Download `WinCompare-<version>-windows-x64-Setup.msi` from [GitHub Releases](https://github.com/andychao217/FileCompare/releases/latest) for complete desktop/start menu shortcuts and terminal integration.
- **Portable ZIP**: Download `WinCompare-<version>-windows-x64.zip` (self-contained, unpack & run).

#### Git Diff & Merge Configuration

- **macOS (`mcdiff`)**:
  ```bash
  git config --global diff.tool maccompare
  git config --global difftool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE"'
  git config --global merge.tool maccompare
  git config --global mergetool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE" -b "$BASE" -m "$MERGED"'
  ```
- **Windows (`wincmp`)**:
  ```cmd
  git config --global diff.tool wincmp
  git config --global difftool.wincmp.cmd "wincmp diff \"$LOCAL\" \"$REMOTE\""
  git config --global merge.tool wincmp
  git config --global mergetool.wincmp.cmd "wincmp merge \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\""
  ```

#### Packaging Builds Locally

- **macOS Universal DMG**:
  ```bash
  bash scripts/package_dmg.sh 0.6.0
  ```
- **Windows WiX MSI & ZIP**:
  ```powershell
  .\win\scripts\package_win.ps1 -Version 0.6.0
  ```

---

<a name="简体中文"></a>
## 简体中文

> **FileCompare** 是专为 **macOS** 与 **Windows 11** 双生态深度打造的高性能、现代原生文件与目录对比合并套件。由 macOS 原生版 **MacCompare**（SwiftUI / AppKit）与 Windows 11 原生版 **WinCompare**（WinUI 3 / Windows App SDK）组成。双端均搭载同一套由 **Rust 2024** 编写的高吞吐差分计算核心，实现毫秒级比对性能与算法的 100% 对齐。

### 🌟 双端原生技术栈全景

| 平台客户端 | 原生界面框架 | 运行架构支持 | 打包分发形态 | 命令行工具 |
| :--- | :--- | :--- | :--- | :--- |
| 🍏 **MacCompare** | SwiftUI + AppKit 原生深度适配 | Universal Binary 2（Apple Silicon + Intel） | DMG 镜像、免安装 Zip、Homebrew Tap | `mcdiff` |
| 🪟 **WinCompare** | WinUI 3 + Fluent Design 云母设计 | Windows 11 / 10（`x64` + `ARM64`） | WiX 原生 MSI 安装包、绿色免安装 Zip | `wincmp` |
| ⚡ **底座共享核心** | Rust 2024 高吞吐 Cargo Workspace | 原生静态库 / 动态库 DLL | 跨语言 FFI 零拷贝调度 | C-ABI / P-Invoke |

---

### 🌟 核心功能全览

- **🤖 端侧离线与云端双模 AI 智能助手**：
  - **100% 端侧离线大模型**：集成 Google Gemma 3（1B 极速版与 4B 进阶版）端侧离线推理，断网完全可用，数据 100% 留存本地；
  - **云端与局域网大模型兼容**：预设适配 DeepSeek、OpenAI、本地/局域网 Ollama（如 `qwen2.5-coder`、`llama3.2`）与自定义端点，提供一键延迟与连通性测试；
  - **变更意图智能总结**：自动分析成对差异，秒级输出「一句话修改目的」与精准匹配变量名的「核心变更要点清单」；
  - **规范化 Commit 提交信息生成**：一键生成标准 Git Conventional Commit 提交描述并自动复制到剪贴板。
- **⚡ Rust 高性能核心与双引擎架构**：
  - **Myers 与 Token LCS 差分 (`diff-core`)**：毫秒级完成行级差分、内联字符差异与 Git 三向冲突合并；
  - **海量表格大数据引擎 (`excel-diff-core`)**：集成 `calamine` 与 `memmap2` 零拷贝内存映射，利用 `ahash` 64位行哈希秒级跳过无差异行，FFI 视口分页让百万行表格前端内存稳定在 30~60MB；
  - **多线程并发目录扫描 (`fs-scanner`)**：基于 `rayon` 与硬件加速 `crc32fast` 实现超高速文件夹对比与深度指纹校验。
- **📊 Excel 与表格文件结构化全要素比对 (.xlsx / .xlsb / .xls / .csv / .tsv)**：
  - 多工作表自动提取与切换、像素级双向同步表格网格、主键列 Hash Join 极速对齐与浮点容差过滤 (`|v1 - v2| <= tolerance`)。
- **📄 Word 文档结构化全要素比对 (.docx / .doc)**：
  - 正文段落富文本样式流（字号、粗体、斜体、下划线、颜色）、内嵌表格网格单元格级追踪、多媒体附件 SHA-256 指纹比对与标题大纲快速导航。
- **📁 毫秒级文件夹对比与同步**：
  - 时间戳/大小快速检查与深度 CRC32 哈希对比，规则化双向同步并提供演练预览 (Dry-Run) 安全机制。
- **🔀 Git 三向冲突合并 (3-Way Merge)**：
  - 直观对比 Local vs Base vs Remote 冲突，智能自动解决非冲突部分，一键解决冲突。
- **🏠 启动欢迎主页与会话历史**：
  - 2x2 黄金对称模式导航、全局最近比对记录 (Recent Comparisons)、左右独立分栏 Drop Zone 预览与多标签快速循环切换。
- **⌨️ 终端开发者生态与 Git 深度整合**：
  - 内置 `mcdiff` (macOS) 与 `wincmp` (Windows)，开箱即用无缝对接 `git difftool` 与 `git mergetool`。
- **📦 双端标准化打包与在线更新**：
  - **macOS**：DMG 镜像、免安装 Zip 与 Homebrew 专属源 (`brew install andychao217/tap/maccompare`)；
  - **Windows**：WiX v4/v5 MSI 原生安装向导（自动生成桌面/开始菜单快捷方式并注册系统 `PATH`）与绿色便携版 Zip；
  - **在线更新**：智能多语言 Release Notes 感知与各平台独立发布包的一键精准下载。

---

### 📸 软件实机界面预览 (UI Preview)

#### 1. Windows 11 原生版 (WinUI 3 & Fluent 设计)

| 启动主页 | 文本比对与 AI 总结 | Excel 表格结构化对比 |
| :---: | :---: | :---: |
| ![Win11 启动主页](docs/assets/win/welcome_home_dark_ui.png) | ![Win11 文本比对](docs/assets/win/text_diff_ai_dark_ui.png) | ![Win11 表格对比](docs/assets/win/excel_diff_dark_ui.png) |

| Word 文档对比 | Git 三向冲突合并 | 文件夹秒级比对与同步 |
| :---: | :---: | :---: |
| ![Win11 Word 对比](docs/assets/win/word_diff_dark_ui.png) | ![Win11 三向合并](docs/assets/win/three_way_merge_dark_ui.png) | ![Win11 文件夹对比](docs/assets/win/folder_diff_dark_ui.png) |

#### 2. macOS 原生版 (SwiftUI & AppKit 原生质感)

| 启动主页 | 文本比对与 AI 总结 | Excel 表格结构化对比 |
| :---: | :---: | :---: |
| ![Mac 启动主页](docs/assets/macos/welcome_home_dark_ui.png) | ![Mac 文本比对](docs/assets/macos/text_diff_ai_dark_ui.png) | ![Mac 表格对比](docs/assets/macos/excel_diff_dark_ui.png) |

| Word 文档对比 | Git 三向冲突合并 | 文件夹秒级比对与同步 |
| :---: | :---: | :---: |
| ![Mac Word 对比](docs/assets/macos/word_diff_dark_ui.png) | ![Mac 三向合并](docs/assets/macos/three_way_merge_ui.jpg) | ![Mac 文件夹对比](docs/assets/macos/folder_diff_dark_ui.png) |

---

### 🚀 快速上手

#### macOS 安装方式
- **通过 Homebrew 一键安装（推荐）**：
  ```bash
  brew install andychao217/tap/maccompare
  ```
- **下载 DMG 安装包**：从 [GitHub Releases](https://github.com/andychao217/FileCompare/releases/latest) 下载 `MacCompare-<version>.dmg`。

#### Windows 安装方式
- **MSI 原生安装包（推荐）**：从 [GitHub Releases](https://github.com/andychao217/FileCompare/releases/latest) 下载 `WinCompare-<version>-windows-x64-Setup.msi`，双击向导安装，桌面与开始菜单自动生成快捷方式，终端可直接使用 `wincmp` 命令。
- **绿色免安装包**：下载 `WinCompare-<version>-windows-x64.zip`，解压即用。

#### 配置为 Git 默认比对与合并工具

- **macOS 配置**：
  ```bash
  git config --global diff.tool maccompare
  git config --global difftool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE"'
  git config --global merge.tool maccompare
  git config --global mergetool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE" -b "$BASE" -m "$MERGED"'
  ```

- **Windows 配置**：
  ```cmd
  git config --global diff.tool wincmp
  git config --global difftool.wincmp.cmd "wincmp diff \"$LOCAL\" \"$REMOTE\""
  git config --global merge.tool wincmp
  git config --global mergetool.wincmp.cmd "wincmp merge \"$LOCAL\" \"$BASE\" \"$REMOTE\" \"$MERGED\""
  ```

#### 本地一键打包

- **macOS (生成 Universal DMG)**：
  ```bash
  bash scripts/package_dmg.sh 0.6.0
  ```
- **Windows (生成 WiX MSI 与便携 ZIP)**：
  ```powershell
  .\win\scripts\package_win.ps1 -Version 0.6.0
  ```

---

### 📄 开源许可证

本项目基于 [MIT License](LICENSE) 许可证开源。
