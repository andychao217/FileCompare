const i18n = {
  zh: {
    badge: "⚡ macOS 原生架构 • Rust 双引擎 • 端侧离线与云端双模 AI • Universal Binary 2 v0.5.0",
    heroTitle: "下一代 macOS <span class='gradient-text'>文件比对与合并套件</span>",
    heroSubtitle: "基于 Rust 高吞吐差分引擎与原生 SwiftUI 深度构建。内置双模 AI 代码意图分析、海量数据流式视口分页与双引擎调度，专为 macOS 打造。",
    downloadBtn: "免费下载 DMG",
    githubBtn: "GitHub 源代码",
    welcomeTab: "启动主页",
    excelDiffTab: "Excel 对比",
    wordDiffTab: "Word 对比",
    textDiffTab: "文本与 AI 对比",
    folderDiffTab: "文件夹对比",
    threeWayMergeTab: "三向合并",
    themeDark: "深色界面",
    themeLight: "浅色界面",
    themeDarkLabel: "深色界面",
    themeLightLabel: "浅色界面",
    sliderHint: "左右拖拽竖线实时对比深浅外观",
    presetSplit: "⚖️ 对半",
    presetDark: "🌙",
    presetLight: "☀️",
    featuresTitle: "为极速、优雅与智能而生",
    featuresSubtitle: "原生级响应速度与端侧大模型赋能，兼顾直观的视觉反馈与强大的生产力工具。",
    featAITitle: "端侧离线与云端双模 AI 智能助手",
    featAIDesc: "内置 Google Gemma 3 端侧模型（100% 本地离线隐私保护）并兼容 DeepSeek、OpenAI 与本地 Ollama。秒级提炼代码改动意图，生成规范 Git 提交信息。",
    feat1Title: "Rust 核心与双引擎架构",
    feat1Desc: "支持在偏好设置中自由切换 Rust 高性能核心或 Swift 原生引擎，支持 Myers 算法与并发 Rayon 扫描。",
    featExcelTitle: "海量表格与多工作表比对",
    featExcelDesc: "集成 Calamine 与 Memmap2 流式解析，64位行哈希秒级筛选，视口分页保持 30MB 极低内存，表头双向像素级锁定同步滚动。",
    featWordTitle: "Word 结构化全要素比对",
    featWordDesc: "支持 .docx/.doc 段落富文本样式、原生内嵌表格网格、多媒体指纹与矢量图形 Shape 深度对比。",
    featHomeTitle: "启动欢迎主页与历史记录",
    featHomeDesc: "2x2 黄金对称四大模式直达、全局持久化历史会话一键恢复与单侧文件即时预览。",
    feat2Title: "对齐幻影行 (Phantom Lines)",
    feat2Desc: "直观的空白对齐与字符级高亮，差异块一目了然，支持左右双向差异合并采纳。",
    feat3Title: "双向目录同步",
    feat3Desc: "支持深度 Hash 与元数据比对模式，带预演 (Dry-Run) 安全机制的一键目录双向同步。",
    feat4Title: "Git 三向合并 (3-Way Merge)",
    feat4Desc: "无缝衔接 Local、Base、Remote 冲突分支，智能自动解决非冲突项，提供完整 Git CLI 支持。",
    feat5Title: "多标签与快捷循环切换",
    feat5Desc: "支持像 Safari 一样自由拖拽拆分与合并窗口，支持 ⌃Tab 快捷键无缝循环切换多任务标签。",
    feat6Title: "原生多语言与深浅外观",
    feat6Desc: "内置中文、英文、日文支持，完美适配 macOS 全局系统主题自适应与快捷切换。",
    featUpdateTitle: "就地热更新与 Homebrew 生态",
    featUpdateDesc: "支持微信同款流式下载与一键重启覆盖，多语言更新日志自动呈现，提供 Homebrew 终端极速分发。",
    cliTitle: "终端命令行与 Git 工具集成",
    cliSubtitle: "一键配置为 Git 默认合并与比对工具，在终端中随时唤起。",
    ctaTitle: "即刻体验极致流畅的比对体验",
    ctaSubtitle: "支持 macOS 14.0 Sonoma 及以上版本，开源免费。",
    footerDesc: "基于 MIT / Apache-2.0 许可证开源。"
  },
  en: {
    badge: "⚡ macOS Native • Rust Dual-Engine • Dual-Mode AI Assistant • Universal Binary 2 v0.5.0",
    heroTitle: "Next-Gen File Comparison <span class='gradient-text'>& 3-Way Merge for macOS</span>",
    heroSubtitle: "Engineered with a high-throughput Rust core, native SwiftUI, and dual-mode AI intent summarization. Featuring Viewport Paging for big-data spreadsheets.",
    downloadBtn: "Download DMG",
    githubBtn: "View on GitHub",
    welcomeTab: "Welcome Hub",
    excelDiffTab: "Excel Diff",
    wordDiffTab: "Word Diff",
    textDiffTab: "Text & AI Diff",
    folderDiffTab: "Folder Diff",
    threeWayMergeTab: "3-Way Merge",
    themeDark: "Dark UI",
    themeLight: "Light UI",
    themeDarkLabel: "Dark UI",
    themeLightLabel: "Light UI",
    sliderHint: "Drag vertical divider to compare Dark & Light",
    presetSplit: "⚖️ 50/50",
    presetDark: "🌙",
    presetLight: "☀️",
    featuresTitle: "Built for Speed, Elegance & Intelligence",
    featuresSubtitle: "Native responsiveness with on-device LLM intelligence and intuitive visual diffing.",
    featAITitle: "Dual-Mode AI: Local Offline & Cloud/Ollama",
    featAIDesc: "Powered by on-device Google Gemma 3 (100% offline & privacy-preserving) with full support for DeepSeek, OpenAI, and local Ollama. Generates instant change intent summaries and Conventional Commits.",
    feat1Title: "Rust Core & Dual-Engine",
    feat1Desc: "Toggle between high-performance Rust core and Swift native engine seamlessly in Settings with live status.",
    featExcelTitle: "Big-Data Excel & Spreadsheet Diff",
    featExcelDesc: "Multi-sheet parsing via Calamine & Memmap2, 64-bit row hash filtering, Viewport Paging with tiny 30MB memory, and pixel-perfect synchronized scrolling.",
    featWordTitle: "Word Structured Diff",
    featWordDesc: "Deep diffing for .docx/.doc with rich styles, native table grids, media fingerprints, and vector shapes.",
    featHomeTitle: "Welcome Hub & History",
    featHomeDesc: "Sleek 2x2 launcher dashboard, persistent session history, and single-file instant previews.",
    feat2Title: "Phantom Line Alignment",
    feat2Desc: "Smart empty line synchronization and token-level highlighting with bidirectional hunk navigation.",
    feat3Title: "Directory Synchronization",
    feat3Desc: "Deep Hash and Quick comparison modes with Dry-Run preview safety before applying sync operations.",
    feat4Title: "Git 3-Way Merge",
    feat4Desc: "Seamlessly resolve Local, Base, and Remote conflict branches with auto-resolution and CLI integration.",
    feat5Title: "Tabs & Fast Cycling",
    feat5Desc: "Safari-style drag-to-split and merge windows, with ⌃Tab hotkeys for rapid tab cycling.",
    feat6Title: "Theme & Multilingual",
    feat6Desc: "Built-in English, Chinese, and Japanese with automatic macOS system appearance synchronization.",
    featUpdateTitle: "In-App Auto-Update & Homebrew",
    featUpdateDesc: "WeChat-inspired 4-stage streamed auto-update with one-click restart, multi-language release notes, and official Homebrew Tap.",
    cliTitle: "CLI & Git Mergetool Integration",
    cliSubtitle: "Configure MacCompare as your default git diff and mergetool in seconds.",
    ctaTitle: "Experience Seamless File Comparison Today",
    ctaSubtitle: "Compatible with macOS 14.0 Sonoma and later. Free & Open Source.",
    footerDesc: "Open source under MIT / Apache-2.0 license."
  },
  ja: {
    badge: "⚡ macOS ネイティブ • Rust デュアルエンジン • 双模 AI アシスタント • Universal Binary 2 v0.5.0",
    heroTitle: "次世代 macOS <span class='gradient-text'>ファイル比較＆3方向マージ</span>",
    heroSubtitle: "高性能 Rust コア、ネイティブ SwiftUI、双模 AI 要約機能を搭載。大容量データに対応した視口ストリーミングと柔軟なエンジン切り替えを実現。",
    downloadBtn: "DMG をダウンロード",
    githubBtn: "GitHub で見る",
    welcomeTab: "ホーム",
    excelDiffTab: "Excel 比較",
    wordDiffTab: "Word 比較",
    textDiffTab: "テキスト＆AI 比較",
    folderDiffTab: "フォルダ比較",
    threeWayMergeTab: "3方向マージ",
    themeDark: "ダーク",
    themeLight: "ライト",
    themeDarkLabel: "ダーク",
    themeLightLabel: "ライト",
    sliderHint: "左右にドラッグして深浅テーマを比較",
    presetSplit: "⚖️ 50/50",
    presetDark: "🌙",
    presetLight: "☀️",
    featuresTitle: "高速性、洗練、そしてインテリジェンス",
    featuresSubtitle: "ネイティブの応答性とオンデバイス LLM により、直感的な視覚差分と高い生産性を両立。",
    featAITitle: "ローカルオフライン＆クラウド双模 AI アシスタント",
    featAIDesc: "Google Gemma 3 オンデバイスモデル（100% ローカル処理でプライバシー保護）を内蔵し、DeepSeek、OpenAI、ローカル Ollama にも完全対応。変更意図の要約と Git コミットメッセージを即座に生成。",
    feat1Title: "Rust コア＆デュアルエンジン",
    feat1Desc: "設定画面から Rust 高速コアと Swift ネイティブエンジンを自由に切り替え可能。Myers アルゴリズムと並列スキャンを搭載。",
    featExcelTitle: "大規模 Excel＆テーブル構造化比較",
    featExcelDesc: "Calamine と Memmap2 による超高速ストリーミング、64bit 行ハッシュ、視口ページング（メモリ消費30MB）、ヘッダー同期スクロールに対応。",
    featWordTitle: "Word 構造化全要素比較",
    featWordDesc: ".docx/.doc のリッチテキスト書式、ネイティブ表グリッド、メディアハッシュ、ベクター図形を高精度に比較。",
    featHomeTitle: "ウェルカムホーム＆履歴",
    featHomeDesc: "2x2 黄金比ランチャー、永続的な比較セッション履歴、単側ファイルの即時プレビューを搭載。",
    feat2Title: "ファントム行アライメント",
    feat2Desc: "空白行の自動同期と単語レベルのハイライトで、差分箇所を一目で把握。",
    feat3Title: "フォルダ双方向同期",
    feat3Desc: "詳細ハッシュ比較とプレビュー（Dry-Run）確認による安全な双方向同期。",
    feat4Title: "Git 3方向マージ",
    feat4Desc: "Local、Base、Remote ブランチの競合を快適に解決。非競合の自動解決もサポート。",
    feat5Title: "タブ循環切替と結合",
    feat5Desc: "Safari のようにタブをドラッグして分離/結合可能。⌃Tab でスムーズにタブ間を切り替え。",
    feat6Title: "多言語＆外观テーマ",
    feat6Desc: "日本語、英語、中国語を標準搭載。macOS の外観モードに完全連動。",
    featUpdateTitle: "アプリ内自動更新＆Homebrew エコシステム",
    featUpdateDesc: "ストリーミングダウンロード、自動展開、ワンクリック再起動による原地更新。多言語リリースノートと公式 Homebrew Tap をサポート。",
    cliTitle: "CLI & Git 統合",
    cliSubtitle: "Git のデフォルトのマージツールとして簡単に連携設定が可能。",
    ctaTitle: "今すぐ MacCompare を体験しましょう",
    ctaSubtitle: "macOS 14.0 Sonoma 以降に対応。オープンソース・無料。",
    footerDesc: "MIT / Apache-2.0 ライセンスに基づくオープンソース。"
  }
};

let currentLang = "zh";
let currentMode = "welcome";
let currentSplitPercent = 50; // 0 to 100

const images = {
  welcome: {
    dark: "assets/welcome_home_dark_ui.png",
    light: "assets/welcome_home_light_ui.png"
  },
  excel: {
    dark: "assets/excel_diff_dark_ui.png",
    light: "assets/excel_diff_light_ui.png"
  },
  word: {
    dark: "assets/word_diff_dark_ui.png",
    light: "assets/word_diff_light_ui.png"
  },
  text: {
    dark: "assets/text_diff_ai_dark_ui.png",
    light: "assets/text_diff_ai_light_ui.png"
  },
  folder: {
    dark: "assets/folder_diff_dark_ui.png",
    light: "assets/folder_diff_light_ui.png"
  },
  merge: {
    dark: "assets/three_way_merge_ui.jpg",
    light: "assets/three_way_merge_light_ui.jpg"
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

// Update images for current mode
function updateShowcaseImages() {
  const imgLight = document.getElementById("img-light");
  const imgDark = document.getElementById("img-dark");
  if (imgLight && imgDark && images[currentMode]) {
    imgLight.style.opacity = "0.5";
    imgDark.style.opacity = "0.5";
    setTimeout(() => {
      imgLight.src = images[currentMode].light;
      imgDark.src = images[currentMode].dark;
      imgLight.style.opacity = "1";
      imgDark.style.opacity = "1";
    }, 120);
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

// Preset Buttons
document.querySelectorAll(".theme-opt-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    document.querySelectorAll(".theme-opt-btn").forEach(b => b.classList.remove("active"));
    btn.classList.add("active");
    const presetVal = parseFloat(btn.getAttribute("data-preset"));
    setSplitPosition(presetVal, true);
  });
});

// Mode Tabs
document.querySelectorAll(".showcase-tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".showcase-tab").forEach(t => t.classList.remove("active"));
    tab.classList.add("active");
    currentMode = tab.getAttribute("data-mode");
    updateShowcaseImages();
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

// Clipboard copy helper
function copyCli() {
  const codeEl = document.getElementById("cli-code");
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
});
