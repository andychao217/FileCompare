<!-- zh-Hans -->
### ✨ 新增特性
- **AI 智能双模式引擎**：
  - 集成 Google Gemma 3 (1B/4B) 本地离线大模型推理，数据 100% 留存本地，断网可用。
  - 支持自定义 Ollama 与兼容 OpenAI 规范的云端大模型 API。
- **AI 改动意图总结与 Commit 生成**：
  - 一键智能生成单句核心意图、结构化差异点分析以及标准化 Conventional Commit 提交建议。
- **就地热更新（微信/Sparkle 同款体验）**：
  - 支持应用内流式下载、macOS 原生 `ditto` 自动解压与一键重启覆盖，无需浏览器往返。
  - 智能多语言 Release Notes 感知（自动识别并呈现中、英、日更新日志）。
- **Homebrew 官方专属源上线**：
  - 现已支持终端一行命令直接安装与自动软链接 CLI：`brew install andychao217/tap/maccompare`。
- **多形态发布资产**：
  - GitHub Releases 同时提供 DMG 安装包、便携 `.zip` 免安装包以及独立 `mcdiff` 终端 CLI 归档。
- **Excel & Word 比对性能飞跃**：
  - 深度优化超大数据表多线程比对引擎与 DOCX 复杂嵌套表格/媒体感知。

### 🐛 问题修复与优化
- 优化设置面板 API 配置字段的自适应排版。
- 升级全流程自动化 CI/CD 构建、Rust 缓存加速与双架构通用二进制（Universal Binary 2）支持。

<!-- en -->
### ✨ Key Features
- **Dual-Mode AI Engine**:
  - Integrate local offline Google Gemma 3 (1B/4B) on-device inference for 100% privacy and zero code leaks.
  - Support custom Ollama and OpenAI-compatible cloud LLM endpoints.
- **AI Change Intent Summary & Conventional Commits**:
  - One-click synthesis of primary change intent, bulleted difference points, and Conventional Commit messages.
- **In-App Seamless Auto-Update**:
  - WeChat/Sparkle-inspired 4-stage update: streamed progress download, auto-extraction via `ditto`, and one-click relaunch.
  - Multi-language release notes auto-sensing based on system locale.
- **Official Homebrew Tap Released**:
  - Install via a single terminal command: `brew install andychao217/tap/maccompare`.
- **Multi-Asset Release Packages**:
  - Releases now include DMG installer, portable `.zip` bundle, and standalone `mcdiff` CLI archive.
- **High-Performance Excel & Word Diff**:
  - Multithreaded Rust-accelerated diffing for massive Excel spreadsheets and structured DOCX files.

### 🐛 Bug Fixes & Improvements
- Improve Settings layout and responsive UI styling.
- Upgrade CI/CD workflows with Rust caching and Universal Binary 2 dual-arch packaging.

<!-- ja -->
### ✨ 主な新機能
- **デュアルモード AI エンジン**：
  - Google Gemma 3 (1B/4B) によるローカルオフライン推论に対応し、プライバシーを完全保護。
  - カスタム Ollama および OpenAI 互換のクラウド API をサポート。
- **AI 変更意図の要約と Commit 生成**：
  - ワンクリックでコアな変更意図、重要ポイント、および Conventional Commit メッセージを自動生成。
- **シームレスなアプリ内自動更新**：
  - ストリーミングダウンロード、自動展開、ワンクリック再起動による原地アップデート。
  - システム言語に応じた多言語リリースノートの自動表示。
- **公式 Homebrew Tap を公開**：
  - ターミナルから1行でインストール可能：`brew install andychao217/tap/maccompare`。
- **マルチアセット配布**：
  - DMG インストーラー、ポータブル `.zip`、および単体 `mcdiff` CLI アーカイブを同時提供。
- **Excel & Word 比較の高速化**：
  - 大規模 Excel シートおよび Word ドキュメントの高速・高精度な構造化差分検出。

### 🐛 不具合修正・改善
- 設定画面のレイアウトとレスポンシブ表示を最適化。
- CI/CD パイプラインを Rust キャッシュおよび Universal Binary 2 対応にアップグレード。
