# MacCompare 打包与发布说明指南 / Packaging & Distribution Guide

<p align="center">
  <a href="#简体中文">简体中文</a> • <a href="#english">English</a>
</p>

---

<a name="简体中文"></a>
## 简体中文

本指南详细介绍了如何为 **MacCompare** 构建、签名以及打包生成 macOS 原生应用包（`.app`）与磁盘映像安装包（`.dmg`）。

### 🌟 打包特性概览

* **Universal Binary 2 通用二进制**：一次打包同时原生支持 Apple Silicon（M1/M2/M3/M4 系列，`arm64`）与 Intel Mac（`x86_64`）。
* **轻量分发体积**：得益于离线 AI 模型（Gemma 3）的**按需下载**设计，安装包 DMG 仅占用约 **30~40 MB**，绝不把 800MB+ 的大模型塞入安装包。
* **零外部硬依赖打包**：优先调用 `create-dmg`；若系统未安装，脚本会自动无缝回退到 macOS 自带的 `hdiutil` 原生生成安装界面。
* **命令行工具集成**：打包时自动将终端对比工具 `mcdiff` 嵌入 App 内部（`Contents/MacOS/mcdiff`）。

---

### 🛠️ 环境依赖要求

在开始打包之前，请确保本地已安装并配置好以下开发环境：

| 工具 / 依赖 | 最低版本要求 | 检查命令 | 备注 |
| :--- | :--- | :--- | :--- |
| **macOS** | macOS 14.0 (Sonoma) 或更高 | `sw_vers` | 构建目标支持系统 |
| **Xcode & Command Line Tools** | Xcode 15.0+ / Swift 6.0+ | `xcrun --version && swift --version` | 推荐使用系统默认 Xcode 工具链 |
| **Rust 工具链** | Rust 1.80+ (`stable`) | `cargo --version` | 用于构建高性能 Rust diff-core 核心 |
| **Rust 交叉编译 Target** | `arm64` + `x86_64` | `rustup target list \| grep installed` | 见下方配置命令 |
| **create-dmg (可选)** | 任意版本 | `create-dmg --version` | 若未安装将自动使用原生 `hdiutil` |

#### 配置 Rust 双架构目标（仅需执行一次）：
```bash
rustup target add aarch64-apple-darwin
rustup target add x86_64-apple-darwin
```

---

### 🚀 快速打包命令

在项目根目录下，直接执行 `scripts/package_dmg.sh` 脚本：

```bash
# 1. 赋予执行权限（首次）
chmod +x scripts/*.sh

# 2. 一键构建并生成指定版本的 DMG 包（默认版本为 0.5.0）
bash scripts/package_dmg.sh 0.5.0
```

#### 打包输出位置：
* **应用程序包**：`dist/staging/MacCompare.app`
* **DMG 磁盘镜像**：`dist/MacCompare-0.5.0.dmg`

---

### 📋 脚本工作流程详解

`package_dmg.sh` 按照以下 4 个阶段自动执行：

1. **[0/4] 构建 Rust 双架构静态核心 (`build_universal_lib.sh`)**：
   * 分别为 `aarch64-apple-darwin` 与 `x86_64-apple-darwin` 构建 release 版本的 `libmaccompare_ffi.a`。
   * 使用 `lipo -create` 合并并输出至 `macos/Sources/CMacCompareCore/lib/libmaccompare_ffi.a`。
2. **[1/4] 编译 Swift 通用二进制**：
   * 调用 `xcrun --toolchain default swift build -c release --arch arm64 --arch x86_64`。
   * 同时产出 `MacCompare`（主程序）和 `mcdiff`（CLI 工具）。
3. **[2/4] 组装 App Bundle**：
   * 创建标准 macOS 应用目录结构：`Contents/MacOS`、`Contents/Resources`。
   * 写入 `Info.plist`（包含权限描述、版本号、暗黑模式支持与高分屏声明）。
   * 复制应用图标 `AppIcon.icns` 与嵌入 `mcdiff`。
4. **[3/4] 代码签名**：
   * 默认使用本地 Ad-hoc 签名（`codesign --force --deep --sign -`）。
   * 如需发布到生产环境，可配置环境变量 `DEVELOPER_ID` 指定 Apple 开发者证书。
5. **[4/4] 磁盘镜像生成**：
   * 自动生成带背景、网格对齐、`/Applications` 替身软链接的只读压缩格式（`UDZO`）DMG 镜像。

---

### 🔍 产物验证与测试

打包完成后，可执行以下命令对构建产物进行完整性检查：

```bash
# 1. 验证可执行文件是否为真正的 Universal Binary 2 (arm64 + x86_64)
lipo -info dist/staging/MacCompare.app/Contents/MacOS/MacCompare
# 预期输出：Architectures in the binary: x86_64 arm64

lipo -info dist/staging/MacCompare.app/Contents/MacOS/mcdiff
# 预期输出：Architectures in the binary: x86_64 arm64

# 2. 检查代码签名状态
codesign -dv --verbose=4 dist/staging/MacCompare.app

# 3. 本地挂载测试
hdiutil attach dist/MacCompare-0.5.0.dmg
```

> **提示（首次打开提示“无法打开”）：**  
> 如果在没有 Apple 公证的机器上首次运行解压后的 App，请在终端中移除 Quarantine 隔离属性：
> ```bash
> xattr -dr com.apple.quarantine /Applications/MacCompare.app
> ```

---

### ❓ 常见问题排查 (Troubleshooting)

* **Q: 提示 `ld: unsupported tapi file type '!tapi-tbd'`**
  * **原因**：系统中存在其他第三方 toolchain（如 swiftly 安装的工具链）与系统 SDK 不匹配。
  * **解决**：脚本已经内置指定 `xcrun --toolchain default swift`。请确保运行前终端环境变量使用的是系统的 Xcode：`sudo xcode-select -switch /Applications/Xcode.app`。
* **Q: 提示 `cargo not found`**
  * **解决**：请先安装 Rust 工具链：`curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh`，并确保 `~/.cargo/bin` 在 `PATH` 中。
* **Q: 离线 AI 模型是否需要打入包中？**
  * **说明**：**不需要**。MacCompare 的 AI 模型（Gemma 3）支持按需从 ModelScope 国内高速镜像免登录下载，运行时存放在 `~/Library/Application Support/MacCompare/models/`，避免安装包体积膨胀。

---

<a name="english"></a>
## English

This guide provides step-by-step instructions for building, signing, and packaging **MacCompare** into a native macOS Application bundle (`.app`) and a distributable disk image (`.dmg`).

### 🌟 Packaging Highlights

* **Universal Binary 2**: Natively supports both Apple Silicon (M-Series, `arm64`) and Intel Mac (`x86_64`) in a single binary.
* **Compact Bundle Size**: With on-demand downloading for offline AI models (Gemma 3), the packaged DMG is only **30~40 MB**, keeping distributions fast and lightweight.
* **Zero Hard External Dependencies**: Prefers `create-dmg` when present, but automatically falls back to native macOS `hdiutil` if not installed.
* **Integrated CLI Tools**: Bundles the terminal comparison tool `mcdiff` inside `Contents/MacOS/mcdiff`.

---

### 🛠️ Prerequisites & Dependencies

Ensure your environment meets the following requirements before packaging:

| Dependency | Minimum Version | Verification Command | Notes |
| :--- | :--- | :--- | :--- |
| **macOS** | macOS 14.0 (Sonoma) or newer | `sw_vers` | Target operating system |
| **Xcode & Tools** | Xcode 15.0+ / Swift 6.0+ | `xcrun --version && swift --version` | Default Xcode toolchain recommended |
| **Rust Toolchain** | Rust 1.80+ (`stable`) | `cargo --version` | Compiles the high-throughput Rust diff-core |
| **Cross-Compile Targets** | `arm64` + `x86_64` | `rustup target list \| grep installed` | See configuration below |
| **create-dmg (Optional)** | Any | `create-dmg --version` | Falls back to `hdiutil` if absent |

#### Add Rust Dual-Architecture Targets (One-Time Setup):
```bash
rustup target add aarch64-apple-darwin
rustup target add x86_64-apple-darwin
```

---

### 🚀 Quick Start Packaging Command

Run `scripts/package_dmg.sh` from the repository root:

```bash
# 1. Grant execute permissions (first time only)
chmod +x scripts/*.sh

# 2. Build & package specified version (defaults to 0.5.0)
bash scripts/package_dmg.sh 0.5.0
```

#### Output Locations:
* **Application Bundle**: `dist/staging/MacCompare.app`
* **DMG Disk Image**: `dist/MacCompare-0.5.0.dmg`

---

### 📋 Packaging Pipeline Breakdown

The `package_dmg.sh` script executes through 4 automated stages:

1. **[0/4] Build Rust Universal Static Core (`build_universal_lib.sh`)**:
   * Compiles release static libraries for `aarch64-apple-darwin` and `x86_64-apple-darwin`.
   * Merges them via `lipo -create` into `macos/Sources/CMacCompareCore/lib/libmaccompare_ffi.a`.
2. **[1/4] Compile Swift Universal Binary**:
   * Invokes `xcrun --toolchain default swift build -c release --arch arm64 --arch x86_64`.
   * Generates both `MacCompare` (main GUI app) and `mcdiff` (terminal tool).
3. **[2/4] Assemble App Bundle Structure**:
   * Prepares standard macOS directories: `Contents/MacOS` and `Contents/Resources`.
   * Writes `Info.plist` with required metadata, high-resolution rendering, and Dark Mode support.
   * Embeds `AppIcon.icns` and `mcdiff`.
4. **[3/4] Code Signing**:
   * Performs Ad-hoc code signing (`codesign --force --deep --sign -`).
   * For production, pass the `DEVELOPER_ID` environment variable to sign with an Apple Developer certificate.
5. **[4/4] Generate DMG Disk Image**:
   * Packages a compressed, read-only (`UDZO`) DMG disk image complete with a drag-to-install `/Applications` symlink.

---

### 🔍 Verification & Inspection

Inspect and verify your packaged artifacts with these commands:

```bash
# 1. Confirm Universal Binary 2 architecture (arm64 + x86_64)
lipo -info dist/staging/MacCompare.app/Contents/MacOS/MacCompare
# Expected: Architectures in the binary: x86_64 arm64

lipo -info dist/staging/MacCompare.app/Contents/MacOS/mcdiff
# Expected: Architectures in the binary: x86_64 arm64

# 2. Inspect code signature
codesign -dv --verbose=4 dist/staging/MacCompare.app

# 3. Mount DMG image
hdiutil attach dist/MacCompare-0.5.0.dmg
```

> **Note on Gatekeeper:**  
> If macOS displays a warning stating that the application cannot be opened from an unidentified developer, clear the quarantine attribute:
> ```bash
> xattr -dr com.apple.quarantine /Applications/MacCompare.app
> ```

---

### ❓ Troubleshooting

* **Q: `ld: unsupported tapi file type '!tapi-tbd'`**
  * **Cause**: Custom toolchains (such as `swiftly`) conflicting with the active macOS SDK.
  * **Fix**: Ensure Xcode is set as the active developer directory: `sudo xcode-select -switch /Applications/Xcode.app`.
* **Q: `cargo not found`**
  * **Fix**: Install Rust via `curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh` and ensure `~/.cargo/bin` is in your `PATH`.
* **Q: Should the offline Gemma 3 model be packaged into the DMG?**
  * **Explanation**: **No**. MacCompare downloads the lightweight Gemma 3 model on-demand directly from fast ModelScope CDN mirrors into `~/Library/Application Support/MacCompare/models/`. This keeps the initial download footprint tiny and fast.
