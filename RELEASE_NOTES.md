<!-- zh-Hans -->
### ✨ 核心新增特性
- **🎉 WinCompare (Windows 11 原生版) 重磅发布**：
  - 基于微软官方 **WinUI 3 (Windows App SDK)** 深度构建，采用原生 **Fluent Design System**（Mica 云母材质、圆角几何、Snap Layouts 贴靠布局与 Cascadia Code 字体）。
  - 底层复用高性能 Rust 核心（`diff-core`、`excel-diff-core`、`fs-scanner`），与 macOS 版实现 100% 算法一致性与处理性能。
- **📊 全端五大核心比对与合并模式**：
  - **文本与代码精细对比**：行级与内联字符级精准追踪，支持差异块左右一键采纳。
  - **Excel 表格全要素比对**：多工作表切换、像素级双向同步网格、主键 Hash Join 对齐与浮点容差过滤。
  - **Word 文档高保真比对**：段落富文本样式流、内嵌原生表格网格差异、多媒体指纹 SHA-256 比对与标题大纲。
  - **文件夹秒级对比与双向同步**：时间戳/大小极速扫描与 CRC32 深度校验，提供 Dry-Run 演练预览。
  - **Git 三向冲突合并 (3-Way Merge)**：直观对比 Local vs Base vs Remote，智能自动合并不冲突代码，一键解决冲突。
- **🤖 端侧与云端双模 AI 智能助手**：
  - 支持 Google Gemma 3 本地端侧离线大模型推理，数据 100% 留存本地；同时兼容 DeepSeek、OpenAI 与局域网 Ollama。
  - 一键提炼「修改核心意图」、生成结构化变更清单并输出规范化 Conventional Commit 提交信息。
- **📦 WiX 原生 Windows 安装向导与终端集成**：
  - 提供全新 WiX v4/v5 安装向导（`.msi`），自动创建桌面快捷方式、开始菜单项并向系统注册 `PATH` 环境变量。
  - 终端输入 `wincmp` 即可全局调用，开箱即用支持 `git difftool` 与 `git mergetool`。
- **🔄 双平台统一持续发布流水线 (CI/CD)**：
  - GitHub Actions 支持 macOS（Universal Binary 2）与 Windows（x64）并行构建。
  - 同一个 Release 中同时提供：`MacCompare-*.dmg`、`MacCompare-*-macos.zip`、`mcdiff-*.tar.gz`、`WinCompare-*-Setup.msi`、`WinCompare-*-windows-x64.zip` 与 `wincmp-*.zip`。
  - 双端在线检查更新模块精准对齐，智能匹配各自平台的更新包与多语言 Release Notes。

### 🐛 性能修复与优化
- 解决 Parallels Desktop / VMware 虚拟机网络共享盘（`Z:\`）下运行 WinUI 3 报 `0xC0000006` 闪退问题，提供本地增量同步运行机制。
- 优化 WiX 安装包构建流水线，加入本地临时磁盘高速缓存机制，解决虚拟机网络盘压缩 CAB 缓慢停滞问题。
- 剔除生产发布包中的 PDB 调试符号，大幅缩减安装包体积并加快启动速度。

---

<!-- en -->
### ✨ Key Features
- **🎉 WinCompare (Windows 11 Native Edition) Official Launch**:
  - Deeply crafted using Microsoft's official **WinUI 3 (Windows App SDK)** with full **Fluent Design System** adoption (Mica material, rounded geometry, Snap Layouts, and Cascadia Code typography).
  - Powered by the shared high-performance Rust core (`diff-core`, `excel-diff-core`, `fs-scanner`), achieving 100% feature and algorithm parity with macOS.
- **📊 5 Core Comparison & Merge Engines Across All Platforms**:
  - **Text & Code Comparison**: Fine-grained line & character inline diffs with 1-click side-by-side chunk adoption.
  - **Excel Spreadsheet Diff**: Multi-sheet navigation, pixel-perfect synchronized grid, primary key Hash Join alignment, and numeric tolerance filtering.
  - **Word Document Diff**: High-fidelity paragraph styling, native table cell diffs, multimedia SHA-256 fingerprinting, and heading outlines.
  - **Folder Fast Diff & Sync**: Timestamp/size quick checks, deep CRC32 verification, and rule-based bidirectional sync with safe Dry-Run previews.
  - **Git 3-Way Merge**: Visual conflict resolution across Local vs. Base vs. Remote with smart auto-merge of non-conflicting sections.
- **🤖 Dual-Mode AI Assistant (Local Offline & Cloud API)**:
  - Supports 100% on-device offline Google Gemma 3 inference, with seamless compatibility for DeepSeek, OpenAI, and local Ollama endpoints.
  - 1-click synthesis of core change intent summaries, bulleted modification lists, and standardized Conventional Commit messages.
- **📦 WiX Native MSI Installer & Global CLI Integration**:
  - Brand-new WiX v4/v5 installer wizard (`.msi`) creating Desktop shortcuts, Start Menu entries, and automatic system `PATH` registration.
  - Built-in `wincmp` terminal utility ready for `git difftool` and `git mergetool` anywhere in your console.
- **🔄 Unified Multi-Platform Release Pipeline (CI/CD)**:
  - GitHub Actions runs macOS (Universal Binary 2) and Windows (x64) builds in parallel.
  - Unified multi-asset releases providing: DMG, macOS Zip, mcdiff tarball, WiX MSI installer, Windows portable Zip, and wincmp Zip.
  - Cross-platform in-app update checkers with intelligent asset resolution and locale-aware release notes display.

### 🐛 Improvements & Fixes
- Add local VM staging synchronization to prevent `0xC0000006 (STATUS_IN_PAGE_ERROR)` crashes when running WinUI 3 over VMware / Parallels shared folders (`Z:\`).
- Optimize WiX packaging with a local disk caching strategy, eliminating CAB compression stalls over network drives.
- Strip PDB debug symbols from production releases, significantly shrinking bundle size and improving startup performance.

---

<!-- ja -->
### ✨ 主な新機能
- **🎉 WinCompare (Windows 11 ネイティブ版) 正式リリース**：
  - マイクロソフト公式 **WinUI 3 (Windows App SDK)** をベースに、**Fluent Design System**（Mica マテリアル、角丸、Snap Layouts、Cascadia Code フォント）を完全採用。
  - 共通の高性能 Rust コア（`diff-core`、`excel-diff-core`、`fs-scanner`）を統合し、macOS 版と 100% 同等の精度と処理速度を実現。
- **📊 全プラットフォーム共通の 5 大比較・マージモード**：
  - **テキスト・コード差分**：行単位およびインライン文字単位の高精度差分検出とワンクリック差分反映。
  - **Excel 表計算ファイル比較**：マルチシート切替、ピクセル完全同期グリッド、主キー Hash Join 整列、数値許容誤差設定。
  - **Word ドキュメント比較**：リッチテキストスタイル流、ネイティブ表差分、マルチメディア SHA-256 フィンガープリント、見出しアウトライン。
  - **フォルダ高速比較・同期**：タイムスタンプ/サイズ判定、CRC32 ハッシュ検証、Dry-Run プレビュー付き双方向同期。
  - **Git 3方向マージ (3-Way Merge)**：Local vs Base vs Remote の直感的な衝突解決と非競合箇所の自動マージ。
- **🤖 デュアルモード AI アシスタント（オンデバイス＆クラウド）**：
  - Google Gemma 3 オフライン推論に対応し、DeepSeek、OpenAI、Ollama とも柔軟に連携。
  - 変更意図のワンクリック要約、重要ポイント箇条書き、および Conventional Commit メッセージ自動生成。
- **📦 WiX ネイティブ MSI インストーラーと CLI 統合**：
  - デスクトップ／スタートメニューのショートカット作成とシステム `PATH` 自動登録を備えた WiX v4/v5 インストーラー（`.msi`）。
  - `git difftool` や `git mergetool` で即座に利用できる `wincmp` ターミナルコマンド。
- **🔄 統一 CI/CD パイプラインとマルチアセット配布**：
  - macOS（Universal Binary 2）と Windows（x64）を GitHub Actions で並列ビルド。
  - DMG、便携 Zip、WiX MSI、wincmp CLI などの全プラットフォーム資産を単一 Release に一括公開。
  - システム言語に応じたリリースノート表示と各 OS 向けパッケージのシームレスな自動更新。

### 🐛 改善と不具合修正
- 仮想マシン（Parallels / VMware）の共有フォルダ（`Z:\`）における WinUI 3 の `0xC0000006` ページエラークラッシュを解消するローカル同期機構を導入。
- WiX パッケージングにローカルディスクバッファを導入し、仮想共有ドライブでの CAB 圧縮遅延を完全解消。
- プロダクションパッケージから PDB デバッグシンボルを除去し、配布サイズの大幅削減と起動速度の向上を実現。
