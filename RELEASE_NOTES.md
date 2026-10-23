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
    - `Setup.exe --dir="D:\\CustomPath"` 或 `/D="D:\\CustomPath"`：自定义安装目录。
    - `Setup.exe --uninstall --silent`：后台静默卸载。
  - 返回 Windows 标准进程退出码（0 成功，非 0 失败），并通过 P/Invoke `AttachConsole` 向父终端实时输出进度与错误信息。
- **📑 跨平台 PDF 结构化与像素级视觉比对 (PixelDiff)**：
  - **原生零依赖 PDF 解析**：流式解析 FlateDecode 解压、ToUnicode CMap 字符映射转换、按 Y 坐标自适应行基线（Baseline Grouping）聚类。
  - **双模比对引擎 (Token Text & PixelDiff)**：精准识别文本 Token 差异，并提供像素级视觉比对与差异遮罩蒙版（Mask Overlay）。
  - **专业比对视图模式**：支持并排对比（Side-by-Side）与透视叠加（Overlay）模式，配有透明度滑块与双向像素同步滚动。
  - **格式智能自检测与离线报告**：文本对比中拖入/打开 PDF 自动提示切换至 PDF 模式；支持一键导出双端一致的自包含 HTML 独立审查报告。
- **✍️ 原位文本即时编辑与 Minimap 防抖稳定化**：
  - **双端原位文本行编辑**：支持在文本差分视图中直接编辑，回车（Enter）智能拆分新行，退格（Backspace）智能合并前行。
  - **防抖差分重算与集合细粒度同步**：Windows 统一 250ms 防抖差分异步重算，避免打字卡顿，通过原位集合更新刷新行高亮。
  - **缩略图视口稳定化**：彻底消除拖拽与缩放时的 Minimap 视口抖动循环（Jitter Loop），平稳随主视口同步。
  - **快捷键完善**：支持标准剪贴板快捷键（Ctrl+C/V/X/A），禁用拼写检查波浪线，提供专业编辑器级输入手感。
- **📁 文件夹层级树形展开/折叠与滚动持久化**：
  - **层级树深度缩进与平滑动画**：双端支持多级目录深度缩进展示，配备平滑旋转动画的 Chevron 折叠/展开切换图标。
  - **全局快捷操作**：工具栏支持一键「全部展开 (Expand All)」与「全部折叠 (Collapse All)」。
  - **滚动视口平稳与状态持久化**：Windows 采用细粒度 `ObservableCollection` 原位插入/移除机制杜绝跳动，重新应用过滤规则时保留 `ScrollViewer` 垂直滚动位移。
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
    - `Setup.exe --dir="D:\\CustomPath"` or `/D="D:\\CustomPath"`: Custom installation destination.
    - `Setup.exe --uninstall --silent`: Completely silent uninstallation.
  - Standard Windows process exit codes (0 for success, non-zero for failure) with real-time console progress output via `AttachConsole`.
- **📑 Cross-Platform PDF Structured & Pixel Visual Diff (PixelDiff)**:
  - **Zero-Dependency Native PDF Parsing**: Stream parsing with FlateDecode, ToUnicode CMap character mapping, and Y-coordinate adaptive baseline grouping.
  - **Dual Diff Engine (Token Text & PixelDiff)**: High-precision token-level text diff alongside pixel-perfect visual difference rendering with mask overlays.
  - **Professional Viewing Modes**: Side-by-side and overlay blend modes with opacity slider and pixel-synchronized dual scrolling.
  - **Format Auto-Detection & Offline Reports**: Dragging/opening PDF files in text diff triggers an automatic switch prompt; export standalone, self-contained HTML inspection reports with a single click.
- **✍️ In-Place Text Editing & Stable Minimap Viewport**:
  - **In-Place Line Editing**: Edit text directly inside diff editors across macOS and Windows; split lines on Enter and merge on Backspace.
  - **Debounced Diff Recalculation**: 250ms asynchronous debounced recalculation with fine-grained collection updates to eliminate typing latency.
  - **Stable Minimap Viewport**: Eliminates drag and resize jitter loops for rock-solid minimap tracking.
  - **Enhanced Shortcuts**: Standard clipboard shortcuts (Ctrl+C, Ctrl+V, Ctrl+X, Ctrl+A) and suppressed spellcheck squiggles for code-editor responsiveness.
- **📁 Folder Hierarchical Tree Expand/Collapse & Persistent Scroll**:
  - **Tree Hierarchy with Smooth Chevron Animations**: Visual depth indentation with rotating chevron toggles across macOS and Windows.
  - **Global Toolbar Controls**: One-click "Expand All" and "Collapse All" actions.
  - **Smooth Viewport & Persistent Scroll Offset**: In-place `ObservableCollection` item updates prevent scroll jumps on Windows, preserving `ScrollViewer` vertical scroll position across filter reapplications.
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
    - `Setup.exe --dir="D:\\CustomPath"`: インストール先のカスタム指定。
    - `Setup.exe --uninstall --silent`: サイレント完全アンインストール。
  - Windows 標準の終了コード（成功時 0、失敗時 非0）を返し、親ターミナルへリアルタイムで進捗状況を出力。
- **📑 クロスプラットフォーム PDF 構造化＆ピクセル視覚差分 (PixelDiff)**:
  - **ゼロ依存ネイティブ PDF 解析**: FlateDecode 解凍、ToUnicode CMap 文字マッピング、Y座標適応型ベースライン行クラスタリングを搭載。
  - **デュアル差分エンジン (Token Text & PixelDiff)**: 高精度なトークン単位テキスト差分と、視覚差分マスクレイヤー（Mask Overlay）によるピクセル比較。
  - **多彩な表示モード**: 並列表示（Side-by-Side）および重畳表示（Overlay、不透明度スライダー・同期スクロール付き）。
  - **フォーマット自動検出＆独立 HTML レポート**: テキスト比較への PDF 投入時に自動切替を促し、ワンクリックで単体 HTML 検査レポートを出力可能。
- **✍️ テキストインプレース直接編集＆防振ミニマップ**:
  - **インプレース行編集**: 差分エディタ上で直接編集が可能。Enter で改行分割、Backspace で前行統合をスマートに処理。
  - **デバウンス差分再計算**: 250ms 非同期デバウンス再計算と細粒度コレクション更新により、入力の引っかかりを排除。
  - **防振ミニマップビューポート**: リサイズやドラッグ時のジッター（小刻みな振動ループ）を完全解消。
  - **ショートカット強化**: Ctrl+C/V/X/A のクリップボード操作を完備し、スペルチェック波線を抑制して快適な編集体験を提供。
- **📁 フォルダ階層ツリー展開・折りたたみ＆スクロール位置保持**:
  - **階層インデントと回転アニメーション**: ディレクトリの深さに応じたインデントと、スムーズに回転する Chevron アイコンを両OSに搭載。
  - **一括ツールバー操作**: 「すべて展開 (Expand All)」および「すべて折りたたみ (Collapse All)」をワンクリックで実行。
  - **スクロールジャンプ防止と位置保持**: Windows で細粒度 `ObservableCollection` 原位更新を採用してちらつきを防止し、フィルタ再適用時も `ScrollViewer` の縦スクロール位置を正確に維持。
- **🚀 統合ビルドスクリプトと CI/CD の高速化**:
  - `package_win.ps1` を統合し、ポータブル ZIP と Fluent Setup を一括ビルド。
  - GitHub Actions から不要な WiX 依存関係を排除し、ビルドと Release 配布を高速化。
