const i18n = {
  zh: {
    matrixNav: "平台技术栈",
    showcaseNav: "界面演示",
    featuresNav: "核心特性",
    cliNav: "Git 集成",
    downloadNav: "下载体验",
    navDownloadBtn: "获取软件",
    badge: "⚡ macOS & Windows 11 双生态深度打造 • Rust 2024 高性能核心 • 端侧离线与云端双模 AI • v0.7.0",
    heroTitle: "跨平台双生态原生 <span class='gradient-text'>文件比对与合并套件</span>",
    heroSubtitle: "MacCompare (macOS SwiftUI / AppKit) 与 WinCompare (Windows 11 WinUI 3) 搭载同一套高吞吐 Rust 差分底座。毫秒级比对速度、双模 AI 代码意图总结、海量表格流式视口分页与纯粹原生桌面质感。",
    downloadMacBtn: "下载 macOS 版 (DMG)",
    downloadWinBtn: "下载 Windows 11 版 (MSI)",
    githubBtn: "GitHub 源代码",
    matrixTitle: "双端原生技术栈与底座共享核心",
    matrixSubtitle: "两款各自平台纯原生打造的界面客户端，搭载同一个 Rust 2024 高性能算法底座，毫秒级比对速度与 100% 算法对齐。",
    specUi: "原生界面：",
    specArch: "架构形态：",
    specPackage: "打包分发：",
    specCli: "命令行工具：",
    specDiff: "核心引擎：",
    specExcel: "表格核心：",
    specScanner: "并发扫描：",
    specInterop: "跨语言调度：",
    showcaseTitle: "沉浸式双端界面与深浅外观实时演示",
    showcaseSubtitle: "自由切换 macOS 与 Windows 11 原生实测截图，左右拖动滑块实时感受深浅色模式。",
    welcomeTab: "启动主页",
    textDiffTab: "文本与 AI 对比",
    excelDiffTab: "Excel 表格",
    wordDiffTab: "Word 文档",
    folderDiffTab: "文件夹对比",
    threeWayMergeTab: "三向合并",
    themeDark: "深色界面",
    themeLight: "浅色界面",
    themeDarkLabel: "深色界面",
    themeLightLabel: "浅色界面",
    sliderHint: "左右拖拽竖线实时对比深浅外观",
    presetSplit: "⚖️ 对半对比",
    presetDark: "🌙 深色",
    presetLight: "☀️ 浅色",
    featuresTitle: "专为极速、全要素对比与极致优雅打造",
    featuresSubtitle: "双生态原生级响应速度与端侧大模型赋能，兼顾直观的视觉反馈与企业级生产力。",
    featAITitle: "端侧离线与云端双模 AI 智能助手",
    featAIDesc: "集成 Google Gemma 3（1B 与 4B）端侧模型，100% 离线隐私推理，零代码外泄；同时兼容 DeepSeek、OpenAI 与本地/局域网 Ollama。一键提炼代码改动意图，自动生成标准 Conventional Commit 提交信息。",
    feat1Title: "Rust 2024 高通量核心与双引擎架构",
    feat1Desc: "基于 Rust 差分计算底座，Myers 算法与 Token LCS 实现毫秒级行级与内联字符高亮；双端均支持偏好设置动态切换 Rust 极速底座与系统原生引擎，算法 100% 对齐。",
    featExcelTitle: "海量表格与多工作表结构化比对",
    featExcelDesc: "支持 .xlsx/.xlsb/.xls/.csv/.tsv 全系列表格。Calamine 与 Memmap2 零拷贝解析，64位行哈希秒级跳过无差异行，视口分页保持 30MB 极低内存，主键 Hash Join 极速对齐与浮点容差过滤。",
    featWordTitle: "Word 文档结构化全要素比对",
    featWordDesc: "支持 .docx/.doc 段落富文本样式流（字号、粗体、斜体、下划线、颜色）、内嵌表格网格单元格追踪、多媒体附件 SHA-256 指纹比对与标题大纲快速导航。",
    feat3Title: "毫秒级多线程文件夹对比与同步",
    feat3Desc: "基于 Rayon 与硬件加速 CRC32fast 并发扫描，时间戳/大小快速对比与深度哈希指纹校验，规则化双向同步并提供演练预览 (Dry-Run) 安全机制。",
    feat4Title: "Git 三向冲突合并 (3-Way Merge)",
    feat4Desc: "可视化三栏对齐 Local vs. Base vs. Remote，智能自动融合非冲突代码变更，提供采纳本地、保留双方、采纳远端一键解决与上下冲突快捷跳转。",
    featHomeTitle: "启动欢迎主页与持久化历史记录",
    featHomeDesc: "2x2 黄金对称四大模式直达、全局持久化历史会话一键恢复、左右独立拖放区 (Drop Zone) 与单侧文件即时预览体验。",
    feat2Title: "智能对齐幻影行 (Phantom Lines)",
    feat2Desc: "像素级直观的空白占位对齐与字符级差异高亮，差异块一目了然，支持左右双向差异合并采纳与快速定位。",
    feat5Title: "原生多标签与自由分拆组合",
    feat5Desc: "原生窗口标签体系，支持自由拖拽拆分与合并窗口，支持键盘快捷键无缝循环切换多任务对比标签。",
    featCliTitle: "终端开发者生态与 Git 深度整合",
    featCliDesc: "内置 macOS mcdiff 与 Windows wincmp 命令行工具，开箱即用无缝对接 git difftool 与 git mergetool。",
    featUpdateTitle: "原生标准化打包与一键更新",
    featUpdateDesc: "macOS DMG 镜像与 Homebrew Tap 分发；Windows WiX v4/v5 原生 MSI 安装向导与便携版 Zip，内置多语言更新日志感知的在线自动更新。",
    feat6Title: "系统级原生质感与自适应深浅外观",
    feat6Desc: "macOS SwiftUI / AppKit 与 Windows 11 Fluent 云母设计，内置中、英、日三语支持，完美适配系统主题自适应与快捷切换。",
    cliTitle: "终端命令行与 Git 工具生态深度整合",
    cliSubtitle: "内置 mcdiff (macOS) 与 wincmp (Windows)，在终端中随时唤起，无缝对接 Git 工作流。",
    downloadSectionTitle: "即刻开启极致流畅的双平台比对体验",
    downloadSectionSubtitle: "完全开源免费，提供 macOS 原生镜像与 Windows 11 安装包。",
    footerDesc: "基于 MIT 许可证完全开源。"
  },
  en: {
    matrixNav: "Tech Stack",
    showcaseNav: "Showcase",
    featuresNav: "Features",
    cliNav: "Git Integration",
    downloadNav: "Download",
    navDownloadBtn: "Get App",
    badge: "⚡ Dual Ecosystem: macOS & Windows 11 Native • Rust 2024 Core • Dual-Mode AI • v0.7.0",
    heroTitle: "Native Cross-Platform <span class='gradient-text'>Diff & Merge Suite</span>",
    heroSubtitle: "MacCompare (macOS SwiftUI / AppKit) & WinCompare (Windows 11 WinUI 3) powered by a unified high-throughput Rust engine. Sub-millisecond performance, dual-mode AI intent summaries, and big-data Viewport Paging.",
    downloadMacBtn: "Download for macOS (DMG)",
    downloadWinBtn: "Download for Windows 11 (MSI)",
    githubBtn: "View on GitHub",
    matrixTitle: "Dual Native Clients, Unified Rust Core",
    matrixSubtitle: "Platform-authentic native interfaces engineered for macOS and Windows 11, sharing a single Rust 2024 workspace for 100% diff algorithm parity.",
    specUi: "Native UI:",
    specArch: "Architecture:",
    specPackage: "Distribution:",
    specCli: "CLI Tooling:",
    specDiff: "Diff Core:",
    specExcel: "Spreadsheet:",
    specScanner: "Concurrency:",
    specInterop: "Interoperability:",
    showcaseTitle: "Interactive Dual-Platform Showcase & Split Preview",
    showcaseSubtitle: "Switch between macOS and Windows 11 screenshots. Drag the center slider to inspect Dark and Light appearances.",
    welcomeTab: "Welcome Hub",
    textDiffTab: "Text & AI Diff",
    excelDiffTab: "Excel Diff",
    wordDiffTab: "Word Diff",
    folderDiffTab: "Folder Diff",
    threeWayMergeTab: "3-Way Merge",
    themeDark: "Dark UI",
    themeLight: "Light UI",
    themeDarkLabel: "Dark UI",
    themeLightLabel: "Light UI",
    sliderHint: "Drag vertical divider to compare Dark & Light",
    presetSplit: "⚖️ 50/50 Split",
    presetDark: "🌙 Dark",
    presetLight: "☀️ Light",
    featuresTitle: "Engineered for Raw Speed, Precision & Elegance",
    featuresSubtitle: "Dual-ecosystem native performance accelerated by on-device LLMs, combining intuitive visual clarity with enterprise productivity.",
    featAITitle: "Dual-Mode AI: Local Offline & Cloud / Ollama",
    featAIDesc: "Powered by on-device Google Gemma 3 (1B & 4B) for 100% offline, privacy-first inference without code leakage. Also supports DeepSeek, OpenAI, and local Ollama to summarize intents and generate Conventional Commits.",
    feat1Title: "Rust 2024 High-Throughput Core & Dual Engine",
    feat1Desc: "Myers algorithm and Token LCS deliver sub-millisecond line and inline character diffing. Seamlessly switch between the Rust engine and native system algorithms with identical results.",
    featExcelTitle: "Big-Data Multi-Sheet Spreadsheet Diff",
    featExcelDesc: "Supports .xlsx/.xlsb/.xls/.csv/.tsv. Stream-parsed with Calamine & Memmap2, 64-bit row hashing, and Viewport Paging keeping memory under 30MB with primary-key Hash Join alignment.",
    featWordTitle: "Word Structured Rich-Text Diff",
    featWordDesc: "Diffs .docx/.doc rich styles (font size, bold, italics, color), nested table cell grids, SHA-256 media attachments, and structured document heading outlines.",
    feat3Title: "Multi-Threaded Folder Diff & Synchronization",
    feat3Desc: "Rayon parallel scanning with hardware-accelerated CRC32fast. Quick metadata checks, deep hash verification, and bidirectional rule sync with Dry-Run safety.",
    feat4Title: "Git 3-Way Conflict Merge",
    feat4Desc: "Visual 3-column alignment for Local vs. Base vs. Remote. Automatically merges non-conflicting edits with single-click resolution actions.",
    featHomeTitle: "Welcome Hub & Session History",
    featHomeDesc: "Sleek 2x2 launcher dashboard, persistent session history recovery, and independent drop zones with instant single-file previews.",
    feat2Title: "Phantom Line Alignment",
    feat2Desc: "Pixel-perfect empty line placeholders and token-level character highlights with bidirectional diff hunk adoption.",
    feat5Title: "Native Tabs & Window Splitting",
    feat5Desc: "Native window tab system with drag-to-split and merge capabilities, accompanied by rapid keyboard tab cycling.",
    featCliTitle: "Terminal Developer Ecosystem & Git Tooling",
    featCliDesc: "Built-in macOS mcdiff and Windows wincmp utilities for instant invocation and effortless git difftool / mergetool configuration.",
    featUpdateTitle: "Native Packaging & Frictionless Updates",
    featUpdateDesc: "macOS DMG images and Homebrew Tap; Windows WiX v4/v5 MSI installer and portable ZIPs, featuring in-app auto-updates with multilingual release notes.",
    feat6Title: "Platform-Authentic Design & Adaptive Themes",
    feat6Desc: "macOS SwiftUI / AppKit and Windows 11 Fluent Mica design with built-in English, Chinese, and Japanese support.",
    cliTitle: "Terminal CLI & Git Ecosystem Integration",
    cliSubtitle: "Built-in mcdiff (macOS) and wincmp (Windows) for instant terminal invocation and seamless Git workflow integration.",
    downloadSectionTitle: "Experience Seamless Dual-Platform Diffing Today",
    downloadSectionSubtitle: "Completely free and open source. Available for macOS and Windows 11.",
    footerDesc: "Open source under the MIT License."
  },
  ja: {
    matrixNav: "技術スタック",
    showcaseNav: "UI デモ",
    featuresNav: "主な機能",
    cliNav: "Git 連携",
    downloadNav: "ダウンロード",
    navDownloadBtn: "入手する",
    badge: "⚡ macOS & Windows 11 両エコシステム対応 • Rust 2024 高速コア • 双模 AI • v0.7.0",
    heroTitle: "クロスプラットフォーム両ネイティブ <span class='gradient-text'>差分比較＆マージツール</span>",
    heroSubtitle: "MacCompare (macOS SwiftUI / AppKit) と WinCompare (Windows 11 WinUI 3) は、共通の高スループット Rust エンジンを搭載。ミリ秒級の比較速度、双模 AI 要約、大容量テーブル視口ページング、純正ネイティブ品質を提供します。",
    downloadMacBtn: "macOS 版をダウンロード (DMG)",
    downloadWinBtn: "Windows 11 版をダウンロード (MSI)",
    githubBtn: "GitHub で見る",
    matrixTitle: "両端ネイティブ UI と共通 Rust 2024 コア",
    matrixSubtitle: "それぞれのプラットフォーム向けに純正ネイティブ設計されたクライアントが、同一の Rust 2024 ワークスペースを共有し、100% 完全なアルゴリズム整合性を保証します。",
    specUi: "ネイティブ UI：",
    specArch: "アーキテクチャ：",
    specPackage: "配布パッケージ：",
    specCli: "コマンドライン：",
    specDiff: "差分エンジン：",
    specExcel: "テーブルコア：",
    specScanner: "並列スキャン：",
    specInterop: "言語間連携：",
    showcaseTitle: "両プラットフォーム UI と深浅テーマのリアルタイム実演",
    showcaseSubtitle: "macOS と Windows 11 の実機スクリーンショットを切り替え、スライダーを左右にドラッグしてダーク/ライトテーマを比較できます。",
    welcomeTab: "ホーム",
    textDiffTab: "テキスト＆AI 比較",
    excelDiffTab: "Excel テーブル",
    wordDiffTab: "Word 文書",
    folderDiffTab: "フォルダ比較",
    threeWayMergeTab: "3方向マージ",
    themeDark: "ダーク",
    themeLight: "ライト",
    themeDarkLabel: "ダーク",
    themeLightLabel: "ライト",
    sliderHint: "左右にドラッグして深浅テーマを比較",
    presetSplit: "⚖️ 50/50 比較",
    presetDark: "🌙 ダーク",
    presetLight: "☀️ ライト",
    featuresTitle: "高速性、全要素比較、そして極上の洗練を追求",
    featuresSubtitle: "両エコシステムでの純正ネイティブ応答性とオンデバイス LLM 連携により、直感的な視覚差分と高い業務効率を両立。",
    featAITitle: "ローカルオフライン＆クラウド双模 AI アシスタント",
    featAIDesc: "Google Gemma 3（1B＆4B）オンデバイスモデルを内蔵し、コードの外部流出ゼロで100% オフライン推論を実現。DeepSeek、OpenAI、ローカル Ollama にも完全対応し、変更意図の要約と Conventional Commits を自動生成。",
    feat1Title: "Rust 2024 高速コア＆デュアルエンジンアーキテクチャ",
    feat1Desc: "Rust 差分計算エンジンに基づき、Myers アルゴリズムと Token LCS でミリ秒級の行・インラインハイライトを実現。設定からネイティブエンジンとの即時切り替えが可能。",
    featExcelTitle: "大規模テーブル＆複数シートの構造化比較",
    featExcelDesc: ".xlsx/.xlsb/.xls/.csv/.tsv を網羅。Calamine と Memmap2 によるゼロコピー解析、64bit 行ハッシュ、メモリ30MB以下の視口ページング、主キー Hash Join 照合に対応。",
    featWordTitle: "Word 文書構造化全要素比較",
    featWordDesc: ".docx/.doc の段落リッチテキストスタイル（フォントサイズ、太字、斜体、下線、色）、埋め込み表グリッド、メディアの SHA-256 指紋比較、見出しアウトラインナビゲーションに対応。",
    feat3Title: "マルチスレッド高速フォルダ比較＆同期",
    feat3Desc: "Rayon とハードウェア加速 CRC32fast 並列スキャン。タイムスタンプとハッシュによる二段階比較、Dry-Run プレビュー付きの安全な双方向同期。",
    feat4Title: "Git 3方向競合マージ (3-Way Merge)",
    feat4Desc: "Local、Base、Remote を視覚的3ペインで対照表示。非競合箇所を自動統合し、ワンクリック競合採納とクイック移動をサポート。",
    featHomeTitle: "ウェルカムホーム＆履歴管理",
    featHomeDesc: "2x2 黄金比ランチャー、永続セッション履歴の一括復元、左右独立ドロップゾーンと単側ファイル即時プレビューを搭載。",
    feat2Title: "ファントム行自動アライメント",
    feat2Desc: "空白プレースホルダーと文字単位の高精度ハイライトにより、差分ブロックを一目で把握可能。",
    feat5Title: "ネイティブタブ＆ウィンドウ分割",
    feat5Desc: "純正ウィンドウタブ体系、ドラッグによる自由なウィンドウ分離/結合、ショートカットによる高速タブ循環切り替えに対応。",
    featCliTitle: "ターミナル開発者エコシステム＆Git 統合",
    featCliDesc: "macOS mcdiff および Windows wincmp ツールを標準搭載。git difftool / mergetool として即座に設定可能。",
    featUpdateTitle: "標準ネイティブパッケージ＆自動更新",
    featUpdateDesc: "macOS DMG および Homebrew Tap 配布。Windows WiX v4/v5 原生 MSI インストーラーと ZIP 便携版、多言語更新履歴対応の自動更新を完備。",
    feat6Title: "システム純正の質感＆自動深浅モード連動",
    feat6Desc: "macOS SwiftUI / AppKit と Windows 11 Fluent Mica デザイン。日本語、英語、中国語を標準搭載し、OS 外観モードに完全連動。",
    cliTitle: "ターミナル CLI＆Git エコシステム統合",
    cliSubtitle: "mcdiff (macOS) と wincmp (Windows) を内蔵し、ターミナルから即座に呼び出し Git ワークフローと連携。",
    downloadSectionTitle: "今すぐ最高の両プラットフォーム比較体験を",
    downloadSectionSubtitle: "完全無料・オープンソース。macOS および Windows 11 向けに提供中。",
    footerDesc: "MIT ライセンスに基づく完全オープンソース。"
  }
};

let currentLang = "zh";
let currentPlatform = "macos"; // "macos" | "win"
let currentMode = "welcome";
let currentSplitPercent = 50; // 0 to 100
let currentTermOs = "mac"; // "mac" | "win"

const images = {
  macos: {
    welcome: {
      dark: "assets/macos/welcome_home_dark_ui.png",
      light: "assets/macos/welcome_home_light_ui.png",
      title: "MacCompare - Welcome Hub"
    },
    text: {
      dark: "assets/macos/text_diff_ai_dark_ui.png",
      light: "assets/macos/text_diff_ai_light_ui.png",
      title: "MacCompare - Text & AI Diff"
    },
    excel: {
      dark: "assets/macos/excel_diff_dark_ui.png",
      light: "assets/macos/excel_diff_light_ui.png",
      title: "MacCompare - Excel Spreadsheet Diff"
    },
    word: {
      dark: "assets/macos/word_diff_dark_ui.png",
      light: "assets/macos/word_diff_light_ui.png",
      title: "MacCompare - Word Document Diff"
    },
    folder: {
      dark: "assets/macos/folder_diff_dark_ui.png",
      light: "assets/macos/folder_diff_light_ui.png",
      title: "MacCompare - Folder Sync & Diff"
    },
    merge: {
      dark: "assets/macos/three_way_merge_ui.jpg",
      light: "assets/macos/three_way_merge_light_ui.jpg",
      title: "MacCompare - Git 3-Way Merge"
    }
  },
  win: {
    welcome: {
      dark: "assets/win/welcome_home_dark_ui.png",
      light: "assets/win/welcome_home_light_ui.png",
      title: "WinCompare - Welcome Hub"
    },
    text: {
      dark: "assets/win/text_diff_ai_dark_ui.png",
      light: "assets/win/text_diff_ai_light_ui.png",
      title: "WinCompare - Text & AI Diff"
    },
    excel: {
      dark: "assets/win/excel_diff_dark_ui.png",
      light: "assets/win/excel_diff_light_ui.png",
      title: "WinCompare - Excel Spreadsheet Diff"
    },
    word: {
      dark: "assets/win/word_diff_dark_ui.png",
      light: "assets/win/word_diff_light_ui.png",
      title: "WinCompare - Word Document Diff"
    },
    folder: {
      dark: "assets/win/folder_diff_dark_ui.png",
      light: "assets/win/folder_diff_light_ui.png",
      title: "WinCompare - Folder Diff"
    },
    merge: {
      dark: "assets/win/three_way_merge_dark_ui.png",
      light: "assets/win/three_way_merge_light_ui.png",
      title: "WinCompare - Git 3-Way Merge"
    }
  }
};

function updateLanguage(lang) {
  currentLang = lang;
  document.querySelectorAll("[data-i18n]").forEach(el => {
    const key = el.getAttribute("data-i18n");
    if (i18n[lang] && i18n[lang][key]) {
      el.innerHTML = i18n[lang][key];
    }
  });
}

// Update showcase images and window chrome
function updateShowcaseImages() {
  const imgLight = document.getElementById("img-light");
  const imgDark = document.getElementById("img-dark");
  const windowTitle = document.getElementById("window-title");
  const macControls = document.getElementById("mac-controls");
  const winControlsLeft = document.getElementById("win-controls-left");
  const winCaptionButtons = document.getElementById("win-caption-buttons");

  // Update window chrome depending on current platform
  if (currentPlatform === "win") {
    if (macControls) macControls.style.display = "none";
    if (winControlsLeft) winControlsLeft.style.display = "flex";
    if (winCaptionButtons) winCaptionButtons.style.display = "flex";
  } else {
    if (macControls) macControls.style.display = "flex";
    if (winControlsLeft) winControlsLeft.style.display = "none";
    if (winCaptionButtons) winCaptionButtons.style.display = "none";
  }

  const platformData = images[currentPlatform] && images[currentPlatform][currentMode];
  if (platformData) {
    if (windowTitle) {
      windowTitle.textContent = platformData.title;
    }

    if (imgLight && imgDark) {
      imgLight.style.opacity = "0.5";
      imgDark.style.opacity = "0.5";
      setTimeout(() => {
        imgLight.src = platformData.light;
        imgDark.src = platformData.dark;
        imgLight.style.opacity = "1";
        imgDark.style.opacity = "1";
      }, 100);
    }
  }
}

// Split-Slider Logic
const compareContainer = document.getElementById("image-compare");
const compareOverlay = document.getElementById("compare-overlay");
const compareHandle = document.getElementById("compare-handle");

function setSplitPosition(percent, smooth = false) {
  currentSplitPercent = Math.max(0, Math.min(100, percent));

  if (compareOverlay && compareHandle) {
    if (smooth) {
      compareOverlay.style.transition = "clip-path 0.3s cubic-bezier(0.25, 1, 0.5, 1)";
      compareHandle.style.transition = "left 0.3s cubic-bezier(0.25, 1, 0.5, 1)";
      setTimeout(() => {
        compareOverlay.style.transition = "";
        compareHandle.style.transition = "";
      }, 300);
    }
    compareOverlay.style.clipPath = `polygon(0 0, ${currentSplitPercent}% 0, ${currentSplitPercent}% 100%, 0 100%)`;
    compareHandle.style.left = `${currentSplitPercent}%`;
  }
}

let isDragging = false;

function onDragStart(e) {
  isDragging = true;
  if (compareHandle) compareHandle.classList.add("dragging");
  handleDrag(e);
}

function onDragEnd() {
  if (isDragging) {
    isDragging = false;
    if (compareHandle) compareHandle.classList.remove("dragging");
  }
}

function handleDrag(e) {
  if (!isDragging || !compareContainer) return;
  const rect = compareContainer.getBoundingClientRect();
  const clientX = e.touches ? e.touches[0].clientX : e.clientX;
  const x = clientX - rect.left;
  const percent = (x / rect.width) * 100;
  setSplitPosition(percent);
}

if (compareContainer) {
  // Mouse drag
  compareContainer.addEventListener("mousedown", (e) => {
    onDragStart(e);
  });
  window.addEventListener("mousemove", (e) => {
    if (isDragging) handleDrag(e);
  });
  window.addEventListener("mouseup", onDragEnd);

  // Touch drag
  compareContainer.addEventListener("touchstart", (e) => {
    onDragStart(e);
  }, { passive: true });
  window.addEventListener("touchmove", (e) => {
    if (isDragging) handleDrag(e);
  }, { passive: true });
  window.addEventListener("touchend", onDragEnd);
}

// Platform Switcher
document.querySelectorAll(".platform-tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".platform-tab").forEach(t => t.classList.remove("active"));
    tab.classList.add("active");
    currentPlatform = tab.getAttribute("data-platform") || "macos";
    updateShowcaseImages();
  });
});

// Mode Tabs
document.querySelectorAll(".showcase-tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".showcase-tab").forEach(t => t.classList.remove("active"));
    tab.classList.add("active");
    currentMode = tab.getAttribute("data-mode") || "welcome";
    updateShowcaseImages();
  });
});

// Preset Buttons (Dark, Split, Light)
document.querySelectorAll(".theme-opt-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    document.querySelectorAll(".theme-opt-btn").forEach(b => b.classList.remove("active"));
    btn.classList.add("active");
    const presetVal = parseFloat(btn.getAttribute("data-preset"));
    setSplitPosition(presetVal, true);
  });
});

// Terminal OS Switcher
document.querySelectorAll(".term-tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".term-tab").forEach(t => t.classList.remove("active"));
    tab.classList.add("active");
    currentTermOs = tab.getAttribute("data-term") || "mac";

    const macCode = document.getElementById("cli-code-mac");
    const winCode = document.getElementById("cli-code-win");

    if (currentTermOs === "win") {
      if (macCode) macCode.style.display = "none";
      if (winCode) winCode.style.display = "block";
    } else {
      if (macCode) macCode.style.display = "block";
      if (winCode) winCode.style.display = "none";
    }
  });
});

// Global Theme Toggle
const themeToggleBtn = document.getElementById("theme-toggle-btn");
if (themeToggleBtn) {
  themeToggleBtn.addEventListener("click", () => {
    const currentTheme = document.documentElement.getAttribute("data-theme") || "dark";
    const newTheme = currentTheme === "dark" ? "light" : "dark";
    document.documentElement.setAttribute("data-theme", newTheme);
    themeToggleBtn.textContent = newTheme === "dark" ? "🌙" : "☀️";
    localStorage.setItem("mc_theme", newTheme);
  });
}

// Language Select
const langSelect = document.getElementById("lang-select");
if (langSelect) {
  langSelect.addEventListener("change", (e) => {
    const selectedLang = e.target.value;
    updateLanguage(selectedLang);
    localStorage.setItem("mc_lang", selectedLang);
  });
}

// Clipboard copy helpers
function copyCli() {
  const codeEl = currentTermOs === "win"
    ? document.getElementById("cli-code-win")
    : document.getElementById("cli-code-mac");
  if (!codeEl) return;
  const code = codeEl.innerText;
  navigator.clipboard.writeText(code).then(() => {
    const btn = document.getElementById("copy-btn");
    if (!btn) return;
    btn.textContent = "Copied!";
    btn.classList.add("copied");
    setTimeout(() => {
      btn.textContent = "Copy";
      btn.classList.remove("copied");
    }, 2000);
  });
}

function copyBrewCmd() {
  const cmd = "brew install andychao217/tap/maccompare";
  navigator.clipboard.writeText(cmd).then(() => {
    const btn = document.querySelector(".brew-copy-btn");
    if (btn) {
      btn.innerText = "✅";
      setTimeout(() => {
        btn.innerText = "📋";
      }, 2000);
    }
  });
}

function copyWinCliCmd() {
  const cmd = "wincmp diff file1.xlsx file2.xlsx";
  navigator.clipboard.writeText(cmd).then(() => {
    const banners = document.querySelectorAll(".brew-copy-btn");
    if (banners.length > 1) {
      const btn = banners[1];
      btn.innerText = "✅";
      setTimeout(() => {
        btn.innerText = "📋";
      }, 2000);
    }
  });
}

// Init
window.addEventListener("DOMContentLoaded", () => {
  // Load saved theme
  const savedTheme = localStorage.getItem("mc_theme") || "dark";
  document.documentElement.setAttribute("data-theme", savedTheme);
  if (themeToggleBtn) {
    themeToggleBtn.textContent = savedTheme === "dark" ? "🌙" : "☀️";
  }

  // Load saved language
  const savedLang = localStorage.getItem("mc_lang") || "zh";
  if (langSelect) {
    langSelect.value = savedLang;
  }
  updateLanguage(savedLang);

  // Initialize Split position at 50%
  setSplitPosition(50);

  // Initialize Showcase view
  updateShowcaseImages();
});
