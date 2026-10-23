<!-- zh-Hans -->
### ✨ 核心新增特性 (v0.7.0)
- **🎨 全新 Fluent Design 单文件安装器 (WinCompare-Setup.exe)**：
  - 彻底淘汰旧版 WiX MSI，采用基于 **WPF-UI** 打造的纯原生 Win11 **Fluent Design** 单文件安装向导（圆角、Acrylic 亚克力材质与平滑过渡动画）。
  - **智能识别旧版原位升级**：启动自动探测旧版软件安装路径，支持一键「覆盖更新」，保留用户配置与快捷方式。
  - **动态系统主题与强调色联动**：自动拉取 Windows 本地个性化 Accent 强调色，支持实时切换跟随浅色/深色主题。
  - **安装/卸载双入口部署**：安装目录同时内置 `Setup.exe`（维护修复）与 `Uninstall.exe`（支持无残留延时自清理卸载）。
- **⚡ 命令行静默安装支持 (Silent / Unattended Mode)**：
  - 支持企业批量下发、CI/CD 自动化部署及 **WinGet / Scoop / Chocolatey** 包管理器：
    - `Setup.exe --silent` 或 `/S`：后台全自动静默安装。
    - `Setup.exe --dir="D:\CustomPath"` 或 `/D="D:\CustomPath"`：自定义安装目录。
    - `Setup.exe --uninstall --silent`：后台静默卸载。
  - 返回 Windows 标准进程退出码（0 成功，非 0 失败），并通过 P/Invoke `AttachConsole` 向父终端实时输出进度与错误信息。
- **🚀 流水线整合与构建提速**：
  - 统一打包脚本 `package_win.ps1`，一行命令即可输出 Portable ZIP 与 Fluent Setup 安装包。
  - GitHub Actions 自动化发布流程接入单文件安装器，移除 WiX 工具链，大幅提升 CI/CD 构建效率。

---

<!-- en -->
### ✨ Key Features (v0.7.0)
- **🎨 Brand-New Fluent Design Single-File Installer (WinCompare-Setup.exe)**:
  - Completely replaces legacy WiX MSI with a modern, native Windows 11 **Fluent Design** single-file installer powered by **WPF-UI** (Acrylic material, rounded geometry, and fluid expand/collapse animations).
  - **Intelligent In-Place Upgrade**: Automatically detects previous installation directories from registry across HKCU/HKLM for seamless 1-click in-place upgrades.
  - **Dynamic System Theme & Accent Color Sync**: Automatically synchronizes with Windows personalization accent colors and system light/dark theme switches in real time.
  - **Dual Maintenance & Uninstaller Deployment**: Deploys both `Setup.exe` (maintenance) and `Uninstall.exe` (deferred clean self-deletion) into the installation root.
- **⚡ Silent / Unattended CLI Automation (WinGet / CI/CD Ready)**:
  - Full support for automated deployments, enterprise policies, and package managers (**WinGet**, **Scoop**, **Chocolatey**):
    - `Setup.exe --silent` or `/S`: Zero-UI background installation.
    - `Setup.exe --dir="D:\CustomPath"` or `/D="D:\CustomPath"`: Custom installation destination.
    - `Setup.exe --uninstall --silent`: Completely silent uninstallation.
  - Standard Windows process exit codes (0 for success, non-zero for failure) with real-time console progress output via `AttachConsole`.
- **🚀 Unified Packaging & Streamlined CI/CD**:
  - Unified `package_win.ps1` produces portable ZIP archives and the Fluent Setup package in a single pass.
  - GitHub Actions automated releases now upload `WinCompare-<Version>-Setup-x64.exe` directly, completely removing obsolete WiX dependencies for faster CI/CD turnaround.

---

<!-- ja -->
### ✨ 主な新機能 (v0.7.0)
- **🎨 新世代 Fluent Design 単一ファイルインストーラー (WinCompare-Setup.exe)**:
  - 従来の WiX MSI を完全に刷新し、**WPF-UI** を採用した洗練された Windows 11 **Fluent Design** インストーラー（Acrylic 透過素材、丸角、スムーズなアニメーション）を導入。
  - **旧バージョンの自動検出と上書き更新**: 既存のインストールパスを自動認識し、設定やショートカットを維持したままワンクリックで「上書き更新」が可能。
  - **システムアクセントカラーとテーマの連動**: Windows 本体の個人用設定アクセントカラーおよびライト／ダークテーマに自動追従。
  - **デュアルエントリー展開**: インストール先に `Setup.exe`（メンテナンス用）と `Uninstall.exe`（遅延自己削除クリーンアップ付き）を自動配置。
- **⚡ サイレント／無人コマンドラインインストール対応 (WinGet / CI/CD)**:
  - 企業の自動展開や **WinGet**、**Scoop**、**Chocolatey** パッケージマネージャーに対応:
    - `Setup.exe --silent` または `/S`: バックグラウンドでの完全サイレントインストール。
    - `Setup.exe --dir="D:\CustomPath"`: インストール先のカスタム指定。
    - `Setup.exe --uninstall --silent`: サイレント完全アンインストール。
  - Windows 標準の終了コード（成功時 0、失敗時 非0）を返し、親ターミナルへリアルタイムで進捗状況を出力。
- **🚀 統合ビルドスクリプトと CI/CD の高速化**:
  - `package_win.ps1` を統合し、ポータブル ZIP と Fluent Setup を一括ビルド。
  - GitHub Actions から不要な WiX 依存関係を排除し、ビルドと Release 配布を高速化。
