# 技术架构与开发设计文档 (Technical Architecture & Development Guide)

> **项目名称**：MacCompare  
> **文档版本**：v1.0.0  
> **适用平台**：macOS 26 (Tahoe) 及以上  
> **硬件架构**：Universal Binary 2 (Apple Silicon `arm64` + Intel `x86_64`)  
> **开发语言**：Swift 6 / SwiftUI / AppKit (前端表现层) + Rust 2024 Edition (底层核心计算引擎)

---

## 1. 架构总览与技术选型

### 1.1 架构分层设计
MacCompare 采用 **“轻量高效的原生 UI 前端 + 高性能内存安全的核心计算后端”** 分离式架构：

```
┌────────────────────────────────────────────────────────────────────────┐
│                        macOS 表现层 (Swift / SwiftUI)                  │
│  - AppKit / SwiftUI 混合架构 (NSViewRepresentable 包装高性能编辑器)       │
│  - 双向/三向虚拟化联动滚动渲染器 (Virtual Diff Scroll Engine)              │
│  - 状态管理 (Observation Framework: @Observable)                      │
│  - 系统服务集成 (Finder Extension / CLI Bridge / Git Mergetool)          │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ (UniFFI / C-ABI 零拷贝/高性能通信)
┌───────────────────────────────────▼────────────────────────────────────┐
│                    跨语言通信桥接层 (Bridge Layer)                       │
│  - UniFFI 生成的 Swift Binding 与 Safe C-ABI 接口                      │
│  - 异步事件流 (AsyncStream) 与并发任务调度 (Swift Concurrency Task)     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ (In-Process Direct FFI Calls)
┌───────────────────────────────────▼────────────────────────────────────┐
│                  核心计算引擎 (Rust: maccompare-core)                   │
│ ┌─────────────────────────┬─────────────────────────┬────────────────┐ │
│ │     diff-engine         │       fs-scanner        │  syntax-parser │ │
│ │ - Myers / Patience Diff │ - 并发目录扫描 (Rayon)  │ - Tree-sitter  │ │
│ │ - Intra-line Token Diff │ - SIMD CRC32/BLAKE3     │   AST 增量解析 │ │
│ │ - 3-Way 冲突合并引擎    │ - 规则化过滤 (Glob/正则)│ - Token 高亮   │ │
│ ├─────────────────────────┴─────────────────────────┴────────────────┤ │
│ │                        sync-engine & mmap-io                       │ │
│ │ - 大文件内存映射 (memmap2)                                         │ │
│ │ - 目录同步计划生成器 (Sync Planner) 与原子文件写保护                │ │
│ └────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. 模块划分与职责说明

| 模块名称 | 实现语言 | 核心依赖 | 职责与功能 |
| :--- | :--- | :--- | :--- |
| **MacCompareApp** | Swift 6 | SwiftUI, AppKit | 主程序入口、窗口与多标签页管理、偏好设置、菜单项系统响应。 |
| **DiffEditorView** | Swift / AppKit | NSTextView / Metal | 双栏/三栏虚拟滚动编辑器、行号与占位空行排版、行内差异绘制。 |
| **FolderDiffView** | Swift / SwiftUI | SwiftUI OutlineGroup | 双目录树对齐呈现、状态徽标渲染、同步动作交互面板。 |
| **maccompare-core** | Rust | `similar`, `rayon`, `tree-sitter` | 底层 Diff 算法、大文件扫描、哈希校验、3-Way 冲突解决逻辑。 |
| **maccompare-bridge** | Rust / Swift | `uniffi-rs` | 自动生成类型安全的 Swift-Rust FFI 绑定代码。 |
| **mcdiff (CLI)** | Rust / Swift | `clap` | 终端命令行比对工具，供终端用户及 Git Difftool/Mergetool 调用。 |
| **FinderSyncExt** | Swift | FinderSync Framework | Finder 右键快捷菜单（“与...对比”、“暂存比对项”）。 |

---

## 3. 核心数据模型与 FFI 接口协议

### 3.1 核心数据结构定义 (Rust / UDL)

```rust
// 差异变更类型
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ChangeType {
    Unchanged,
    Added,
    Deleted,
    Modified,
}

// 行内局部 Token/字符级差异
#[derive(Debug, Clone)]
pub struct DiffToken {
    pub start_offset: u32,
    pub length: u32,
    pub change_type: ChangeType,
}

// 单行差异信息
#[derive(Debug, Clone)]
pub struct DiffLine {
    pub left_line_number: Option<u32>,   // 左侧行号（若右侧新增则为 None）
    pub right_line_number: Option<u32>, // 右侧行号（若左侧删除则为 None）
    pub content_left: String,
    pub content_right: String,
    pub change_type: ChangeType,
    pub tokens_left: Vec<DiffToken>,     // 行内精细差异 (左)
    pub tokens_right: Vec<DiffToken>,    // 行内精细差异 (右)
}

// 文本对比总输出
#[derive(Debug, Clone)]
pub struct TextDiffResult {
    pub lines: Vec<DiffLine>,
    pub total_additions: u32,
    pub total_deletions: u32,
    pub total_modifications: u32,
}

// 目录树差异节点
#[derive(Debug, Clone)]
pub struct FolderDiffEntry {
    pub relative_path: String,
    pub is_directory: bool,
    pub status: FolderItemStatus, // Equal, LeftOnly, RightOnly, ContentDifferent, MetadataDifferent
    pub left_size: Option<u64>,
    pub right_size: Option<u64>,
    pub left_modified_timestamp: Option<u64>,
    pub right_modified_timestamp: Option<u64>,
    pub left_hash: Option<String>,
    pub right_hash: Option<String>,
}
```

### 3.2 跨语言 FFI 核心接口签名

```rust
#[uniffi::export]
pub fn compare_text(
    left_content: &str, 
    right_content: &str, 
    options: DiffOptions
) -> TextDiffResult;

#[uniffi::export]
pub fn compare_folders_async(
    left_path: &str, 
    right_path: &str, 
    options: FolderDiffOptions,
    callback: Box<dyn FolderProgressCallback>
) -> Vec<FolderDiffEntry>;

#[uniffi::export]
pub fn merge_three_way(
    local_content: &str,
    base_content: &str,
    remote_content: &str
) -> MergeResult;
```

---

## 4. 关键算法与技术实现细节

### 4.1 两阶段文本 Diff 算法 (Two-Stage Diff Engine)
1. **第一阶段（行级别比对）**：
   - 采用 **Patience Diff / Myers Diff** 算法定位行级别的变更块（Hunks）。
   - Patience 算法优先匹配唯一行（Unique Lines），保证函数块与大括号在重构时不会发生错误错位对齐。
2. **第二阶段（行内字符/词级别精细高亮）**：
   - 对识别为 `Modified` 的对应行，提取 Token 序列（基于分词器或 Tree-sitter Lexer）。
   - 在 Token 序列上运行最长公共子序列 (LCS) 算法，生成精准的字符级加亮坐标 (`DiffToken`)。

### 4.2 双视口联动虚拟滚动与占位对齐 (Phantom Line Alignment)
- **挑战**：当左侧删除了 5 行代码时，右侧没有对应内容。若不进行对齐，左右两边的下一行代码将产生垂直视觉错位。
- **解决方案**：
  1. Rust 引擎在输出 `DiffLine` 时，对缺失的一侧填充占位标识 (`None` 行号)。
  2. Swift 前端自定义渲染容器（基于 `NSScrollView` + 视口虚拟化缓存）：
     - 维护一份左右共享的逻辑总行索引 `TotalVisualLines`。
     - 仅渲染当前屏幕可见区域（Visible Rect ± 15 屏缓冲区），渲染高度恒定为 `lineHeight * TotalVisualLines`。
     - 缺失行渲染为灰色斜纹占位背景（Phantom Lines），严格保持左右两侧有效代码垂直基准线 100% 对齐。

```
Left Buffer               Visual Layout                 Right Buffer
┌──────────────┐         ┌──────────────┐              ┌──────────────┐
│ line 10: foo │ ──────> │ line 10: foo │ <─────────── │ line 10: foo │
├──────────────┤         ├──────────────┤              ├──────────────┤
│ line 11: bar │ ──────> │ line 11: bar │ <── (Hunk) ─ │ // Phantom   │
├──────────────┤         ├──────────────┤              ├──────────────┤
│ line 12: baz │ ──────> │ line 12: baz │ <─────────── │ line 11: baz │
└──────────────┘         └──────────────┘              └──────────────┘
```

### 4.3 文件夹并发比对与硬件加速哈希 (SIMD Hash Engine)
- **多线程扫描**：使用 `rayon` 建立并行工作窃取池，分别对左右两端文件系统进行并发 `lstat` 遍历，构建路径索引树。
- **哈希硬件加速**：
  - **Intel (x86_64)**：利用 `CRC32-PCLMULQDQ` / `AVX2` 指令集实现 10GB/s+ 的内存哈希计算。
  - **Apple Silicon (arm64)**：利用 ARM NEON `PMULL` / `CRC32` 专用指令加速。
- **大文件 mmap 流式加载**：
  - 大于 50MB 的大文件不全部读入堆内存，统一通过 `memmap2` 建立只读映射页，按需读取比对，防止出现 OOM 崩溃。

### 4.4 三向冲突合并算法 (3-Way Merge Engine)
- 传入三份数据：`Local`（本地分支）、`Base`（共同祖先）、`Remote`（远端分支）。
- 计算 `Local` vs `Base` 的补丁 $\Delta_L$，以及 `Remote` vs `Base` 的补丁 $\Delta_R$。
- **冲突检测规则**：
  - 若某一行/块仅 $\Delta_L$ 变动，自动采纳 $\Delta_L$；
  - 若某一行/块仅 $\Delta_R$ 变动，自动采纳 $\Delta_R$；
  - 若两者均变动且变动内容不同，标记为 **Conflict**，生成三态操作锚点提供给用户手动抉择。

---

## 5. 项目目录结构与工程组织

```
FileCompare/
├── assets/                           # UI 设计效果图与设计资产
│   ├── text_diff_ui.jpg
│   ├── folder_diff_ui.jpg
│   └── three_way_merge_ui.jpg
├── PRD.md                            # 产品需求文档
├── DEVELOPMENT.md                    # 本开发架构文档
│
├── core/                             # Rust 核心引擎 (Cargo Workspace)
│   ├── Cargo.toml
│   ├── crates/
│   │   ├── diff-core/                # Myers/Patience Diff 算法实现
│   │   ├── fs-scanner/               # 目录极速扫描、过滤与哈希
│   │   ├── syntax-highlighter/       # Tree-sitter 语法树解析
│   │   └── maccompare-ffi/           # UniFFI 绑定导出层
│   └── tests/                        # 核心算法压力与单元测试
│
├── macos/                            # macOS 原生工程 (Xcode Project / SPM)
│   ├── MacCompare.xcodeproj
│   ├── MacCompare/                   # 主 App 源码
│   │   ├── App/                      # App 入口、AppDelegate
│   │   ├── Views/
│   │   │   ├── DiffEditor/           # 文本比对/三向合并 UI
│   │   │   ├── FolderDiff/           # 文件夹比对与同步 UI
│   │   │   └── Components/           # Minimap、Toolbar、Tabs 组件
│   │   ├── ViewModels/               # 状态机与业务逻辑
│   │   ├── Generated/                # UniFFI 自动生成的 Swift 代码
│   │   └── Resources/                # 资产文件、本地化资源
│   ├── CLI/                          # mcdiff 命令行工具工程
│   └── FinderExtension/              # Finder 右键上下文菜单扩展
│
└── scripts/                          # 构建与自动化脚本
    ├── build_universal_lib.sh        # 编译 Universal Binary 2 静态库
    └── generate_bindings.sh          # 生成 Swift-Rust 胶水代码
```

---

## 6. 构建、交叉编译与 Universal Binary 2 分发

为了确保同时适配 **Apple Silicon (M系列)** 与 **Intel Mac (x86_64)**，采用统一的构建自动化流水线：

### 6.1 Rust 核心库交叉编译脚本 (`scripts/build_universal_lib.sh`)

```bash
#!/usr/bin/env bash
set -e

# 确保安装了双架构目标
rustup target add aarch64-apple-darwin
rustup target add x86_64-apple-darwin

echo "Compiling for Apple Silicon (arm64)..."
cargo build --release --target aarch64-apple-darwin --package maccompare-ffi

echo "Compiling for Intel (x86_64)..."
cargo build --release --target x86_64-apple-darwin --package maccompare-ffi

echo "Merging into Universal 2 Static Library via lipo..."
mkdir -p ./macos/MacCompare/Generated/lib
lipo -create \
    target/aarch64-apple-darwin/release/libmaccompare_ffi.a \
    target/x86_64-apple-darwin/release/libmaccompare_ffi.a \
    -output ./macos/MacCompare/Generated/lib/libmaccompare_ffi.a

echo "Universal binary successfully created!"
lipo -info ./macos/MacCompare/Generated/lib/libmaccompare_ffi.a
```

### 6.2 Xcode 配置规范
- **Build Active Architecture Only**：Release 模式设为 `No`。
- **Architectures**：`Standard Architectures (Apple Silicon, Intel) - $(ARCHS_STANDARD)`。
- **Deployment Target**：`macOS 26.0`。
- **Linking Flags**：链接 `libmaccompare_ffi.a` 及系统框架 `SystemConfiguration`, `Security`。

---

## 7. 质量保证与测试规范

1. **Rust 底层算法测试**：
   - 单元测试：覆盖极端空行、超大文件 (100MB+)、编码错乱、畸形 YAML/JSON。
   - 模糊测试 (Fuzzing)：采用 `cargo-fuzz` 测试 Diff 引擎的健壮性，杜绝越界与死循环。
   - 性能基准测试 (Benchmarks)：使用 `criterion` 压测 10 万行代码比对耗时（基准要求 < 50ms）。
2. **Swift UI 渲染测试**：
   - 验证双向快速滑动时的帧率（保持 60fps / 120fps ProMotion 刷新）。
   - 验证暗黑模式与明亮模式无缝切换时的颜色重绘性能。
3. **真实场景回归测试**：
   - 配置为 `git mergetool` 解决包含多个冲突的真实代码仓库合并场景。
