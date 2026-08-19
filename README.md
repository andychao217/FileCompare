# MacCompare

> **MacCompare** 是专为 macOS 生态打造的高性能、现代原生文件与目录对比合并工具。

---

## 🌟 核心特性

- **现代原生视觉**：采用 SwiftUI + AppKit 原生技术栈，支持 macOS Tahoe 材质、视网膜屏平滑滚动与深色模式。
- **高性能双阶段 Diff**：支持行级变更定位与词/字符级行内精细高亮，配备斜纹占位对齐（Phantom Line Alignment）。
- **3-Way 冲突合并**：本地 (Local) vs 基础基准 (Base) vs 远端 (Remote) 三向冲突可视化解决与一键采纳。
- **极速文件夹对比与同步**：支持时间戳/大小极速对比与 CRC32 深度哈希比对，提供带 Dry Run 安全预览的规则化同步。
- **开发者生态整合**：提供 `mcdiff` 命令行工具，原生支持 `git difftool` 与 `git mergetool`。

---

## 📁 目录结构

```
FileCompare/
├── assets/                           # UI 设计效果图与设计资产
│   ├── text_diff_ui.jpg              # 文本/代码双向比对设计图
│   ├── folder_diff_ui.jpg            # 文件夹对比与同步设计图
│   └── three_way_merge_ui.jpg        # 三向合并设计图
├── PRD.md                            # 产品需求文档
├── DEVELOPMENT.md                    # 技术架构与开发指南
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
│   │   ├── MacCompareKit/            # 核心数据模型、ViewModel 与 UI 组件
│   │   ├── MacCompare/               # macOS 主应用程序
│   │   └── mcdiff/                   # 终端命令行比对工具
│   └── Tests/MacCompareTests/        # 单元测试
│
└── scripts/                          # 构建与自动化脚本
    ├── build_universal_lib.sh        # Universal Binary 2 (arm64 + x86_64) 编译
    ├── generate_bindings.sh          # UniFFI 绑定生成
    └── run_app.sh                    # 构建并运行测试
```

---

## 🚀 快速开始

### 编译与运行测试

```bash
cd macos
swift build
swift test
```

### 使用命令行工具 (`mcdiff`)

```bash
# 2-Way 文本比对
swift run mcdiff file_a.txt file_b.txt

# 3-Way 冲突合并
swift run mcdiff --merge local.py base.py remote.py -o merged.py
```

### 编译 Universal Binary 2 (Apple Silicon + Intel)

```bash
./scripts/build_universal_lib.sh
```
