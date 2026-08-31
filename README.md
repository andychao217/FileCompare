# MacCompare

<p align="center">
  <b>A modern, high-performance native file & directory comparison and merge tool for macOS.</b><br>
  <b>专为 macOS 生态打造的高性能、现代原生文件与目录对比合并工具。</b>
</p>

<p align="center">
  <a href="#english">English</a> • <a href="#简体中文">简体中文</a>
</p>

---

<a name="english"></a>
## English

> **MacCompare** is a high-performance, modern native file & directory comparison and merge tool tailored for macOS. Built with a high-performance Rust differential engine and native SwiftUI / AppKit, supporting Universal Binary 2 (Apple Silicon M-Series + Intel x86_64).

### 🌟 Key Features

- **📄 Structured Word Document Comparison (.docx / .doc)**:
  - **Body Text & Styling Diff**: High-fidelity paragraph text flow reconstruction, with granular diffing for font weight, italics, underline, font size, and color changes.
  - **Native Embedded Table Grid Diff**: Cell-level precision difference tracking (Green for additions, Red for deletions, Orange for modifications) without misalignments.
  - **Multimedia & Vector Graphics**: Deep SHA-256 fingerprint comparison for embedded images, audio/video, and attachments; native rendering of Word vector shapes (DrawingML Shape).
  - **Outline & Metadata**: Automatic extraction of H1~H6 heading outlines with one-click anchor navigation; comparison of author, creation/modification timestamps, and word count statistics.
- **⚡ Modern Native UI & Two-Stage Diff**: Native SwiftUI + AppKit architecture with macOS Tahoe material styling, Retina smooth scrolling, and seamless Dark/Light mode adaptation. Features line-level change positioning and word/character-level granular highlighting, complete with Phantom Line Alignment.
- **🔀 Git 3-Way Conflict Merge**: Visual 3-way conflict resolution across Local vs. Base vs. Remote, intelligent auto-merging of non-conflicting sections, and one-click conflict resolution.
- **📁 Blazing-Fast Folder Diff & Sync**: Quick timestamp/size checks and deep CRC32 hash comparison, featuring rule-based bidirectional synchronization with safe Dry-Run preview.
- **🛠️ Developer Ecosystem & CLI Integration**: Built-in `mcdiff` terminal CLI tool, with out-of-the-box integration for `git difftool` and `git mergetool`.

### 📸 Screenshots & UI Preview

#### 1. Word Document Structured Comparison (Word Diff)

| Dark UI | Light UI |
| :---: | :---: |
| ![Word Diff Dark](docs/assets/word_diff_dark_ui.png) | ![Word Diff Light](docs/assets/word_diff_light_ui.png) |

#### 2. Fine-Grained Text & Code Comparison (Text Diff)

| 2-Way Text Diff | 3-Way Merge |
| :---: | :---: |
| ![Text Diff](docs/assets/text_diff_ui.jpg) | ![3-Way Merge](docs/assets/three_way_merge_ui.jpg) |

#### 3. Folder Fast Diff & Sync (Folder Diff)

![Folder Diff](docs/assets/folder_diff_ui.jpg)

### 📁 Project Structure

```
FileCompare/
├── docs/                             # Official website & documentation (GitHub Pages)
│   ├── assets/                       # Screenshots and web visual assets
│   ├── index.html                    # Homepage
│   ├── styles.css                    # Glassmorphism & adaptive responsive styles
│   └── script.js                     # Interactions & multi-language switcher (EN/ZH/JA)
│
├── core/                             # Rust high-performance core engine (Cargo Workspace)
│   ├── crates/diff-core/             # Myers/Patience Diff algorithm & 3-Way Merge
│   ├── crates/fs-scanner/            # Fast concurrent directory scanner & CRC32 hashing
│   ├── crates/syntax-highlighter/    # Tree-sitter incremental syntax highlighting interface
│   └── crates/maccompare-ffi/        # UniFFI / C-ABI cross-language bindings
│
├── macos/                            # macOS native application (Swift 6 / SPM)
│   ├── Package.swift                 # SPM modular configuration
│   ├── Sources/
│   │   ├── MacCompareKit/            # Core models, ViewModels, Word parser & UI components
│   │   ├── MacCompare/               # macOS main application
│   │   └── mcdiff/                   # Terminal CLI comparison tool
│   └── Tests/MacCompareTests/        # Unit test suite
│
└── scripts/                          # Build and release automation scripts
    ├── build_universal_lib.sh        # Universal Binary 2 (arm64 + x86_64) compilation
    ├── package_dmg.sh                # Automated code signing & DMG release packaging
    └── run_app.sh                    # Build and run locally for testing
```

### 🚀 Quick Start

#### Build & Run Tests

```bash
cd macos
swift build
swift test
```

#### Using Terminal CLI (`mcdiff`)

```bash
# 1. Compare two text / code files
mcdiff file_a.txt file_b.txt

# 2. Compare two Word documents (.docx / .doc)
mcdiff document_v1.docx document_v2.docx

# 3. Compare two directories
mcdiff dir_a/ dir_b/

# 4. Git 3-Way conflict merge
mcdiff --merge local.py base.py remote.py -o merged.py
```

#### Configure as Default Git Diff & Merge Tool

```bash
git config --global merge.tool maccompare
git config --global mergetool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE" -b "$BASE" -m "$MERGED"'
git config --global mergetool.maccompare.trustExitCode true

git config --global diff.tool maccompare
git config --global difftool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE"'
```

#### Package Universal DMG Release

```bash
./scripts/package_dmg.sh 0.2.0
```

### 📄 License

This project is licensed under the [MIT License](LICENSE).

---

<a name="简体中文"></a>
## 简体中文

> **MacCompare** 是专为 macOS 生态打造的高性能、现代原生文件与目录对比合并工具。基于 Rust 高性能差分引擎与原生 SwiftUI/AppKit 深度构建，支持 Universal Binary 2 (Apple Silicon M系列 + Intel x86_64)。

### 🌟 核心特性

- **📄 Word 文档结构化全要素比对 (.docx / .doc)**：
  - **正文与样式对比**：高保真还原段落文字流，精细比对字体加粗、倾斜、下划线、字号及颜色变动。
  - **表格原生嵌入网格对比**：单元格级差异精准定位（新增绿、删除红、修改橙），杜绝错位。
  - **多媒体与矢量图形**：支持嵌入图片/音视频/附件 SHA-256 深度指纹比对，原生绘制 Word 矢量形状（DrawingML Shape）。
  - **章节大纲与元数据**：自动提取 H1~H6 标题大纲支持一键锚点跳转，比对作者、创建/修改时间与字数统计。
- **⚡ 现代原生视觉与双阶段 Diff**：采用 SwiftUI + AppKit 原生技术栈，支持 macOS Tahoe 材质、视网膜屏平滑滚动与深色/浅色模式无缝自适应；支持行级变更定位与词/字符级精细高亮，配备斜纹对齐幻影行（Phantom Line Alignment）。
- **🔀 Git 3-Way 冲突合并**：本地 (Local) vs 基础基准 (Base) vs 远端 (Remote) 三向冲突可视化解决，智能自动合并非冲突项，支持一键采纳。
- **📁 极速文件夹对比与同步**：支持时间戳/大小极速对比与 CRC32 深度哈希比对，提供带 Dry Run 安全预览的规则化双向同步。
- **🛠️ 开发者生态与 CLI 整合**：提供 `mcdiff` 终端命令行工具，原生支持 `git difftool` 与 `git mergetool` 随时唤起。

### 📸 界面预览

#### 1. Word 文档结构化比对 (Word Diff)

| 深色模式 (Dark UI) | 浅色模式 (Light UI) |
| :---: | :---: |
| ![Word Diff Dark](docs/assets/word_diff_dark_ui.png) | ![Word Diff Light](docs/assets/word_diff_light_ui.png) |

#### 2. 文本与代码精细对比 (Text Diff)

| 文本双向对比 | 三向冲突合并 (3-Way Merge) |
| :---: | :---: |
| ![Text Diff](docs/assets/text_diff_ui.jpg) | ![3-Way Merge](docs/assets/three_way_merge_ui.jpg) |

#### 3. 文件夹极速对比与同步 (Folder Diff)

![Folder Diff](docs/assets/folder_diff_ui.jpg)

### 📁 项目目录结构

```
FileCompare/
├── docs/                             # 产品官网与在线文档 (GitHub Pages)
│   ├── assets/                       # 网页与文档视觉截图资源
│   ├── index.html                    # 官网首页
│   ├── styles.css                    # 现代毛玻璃与自适应样式
│   └── script.js                     # 交互与中/英/日多语言切换
│
├── core/                             # Rust 高性能核心引擎 (Cargo Workspace)
│   ├── crates/diff-core/             # Myers/Patience Diff 算法与 3-Way Merge
│   ├── crates/fs-scanner/            # 目录极速并发扫描与 CRC32 哈希
│   ├── crates/syntax-highlighter/    # Tree-sitter 增量语法高亮接口
│   └── crates/maccompare-ffi/        # UniFFI / C-ABI 跨语言导出
│
├── macos/                            # macOS 原生工程 (Swift 6 / SPM)
│   ├── Package.swift                 # SPM 模块化配置
│   ├── Sources/
│   │   ├── MacCompareKit/            # 核心数据模型、ViewModel、Word解析器与UI组件
│   │   ├── MacCompare/               # macOS 主应用程序
│   │   └── mcdiff/                   # 终端命令行比对工具
│   └── Tests/MacCompareTests/        # 单元测试套件
│
└── scripts/                          # 构建与发布自动化脚本
    ├── build_universal_lib.sh        # Universal Binary 2 (arm64 + x86_64) 编译
    ├── package_dmg.sh                # 自动签名打包 DMG 发布镜像
    └── run_app.sh                    # 构建并启动测试
```

### 🚀 快速开始

#### 编译与运行测试

```bash
cd macos
swift build
swift test
```

#### 使用终端命令行工具 (`mcdiff`)

```bash
# 1. 对比两个文本/代码文件
mcdiff file_a.txt file_b.txt

# 2. 对比两个 Word 文档 (.docx / .doc)
mcdiff document_v1.docx document_v2.docx

# 3. 对比两个文件夹目录
mcdiff dir_a/ dir_b/

# 4. Git 3-Way 冲突合并
mcdiff --merge local.py base.py remote.py -o merged.py
```

#### 配置为 Git 默认合并与对比工具

```bash
git config --global merge.tool maccompare
git config --global mergetool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE" -b "$BASE" -m "$MERGED"'
git config --global mergetool.maccompare.trustExitCode true

git config --global diff.tool maccompare
git config --global difftool.maccompare.cmd 'mcdiff "$LOCAL" "$REMOTE"'
```

#### 打包通用安装镜像 (DMG)

```bash
./scripts/package_dmg.sh 0.2.0
```

### 📄 开源许可证

本项目基于 [MIT License](LICENSE) 开源。

