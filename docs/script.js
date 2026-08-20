const i18n = {
  zh: {
    badge: "⚡ macOS 原生架构 • Intel & Apple Silicon 通用二进制",
    heroTitle: "下一代 macOS <span class='gradient-text'>文件比对与合并套件</span>",
    heroSubtitle: "基于 Rust 高性能差分引擎与原生 SwiftUI 深度构建。专为 macOS 开发者和专业用户打造的极致比对工具。",
    downloadBtn: "免费下载 DMG (v0.1.1)",
    githubBtn: "GitHub 源代码",
    textDiffTab: "文本对比",
    folderDiffTab: "文件夹对比",
    threeWayMergeTab: "三向合并",
    themeDark: "深色界面",
    themeLight: "浅色界面",
    featuresTitle: "为极速与优雅而生",
    featuresSubtitle: "原生级响应速度，兼顾直观的视觉反馈与强大的生产力工具。",
    feat1Title: "Rust 差分内核",
    feat1Desc: "基于 Myers 算法与 SIMD 加速哈希，瞬间完成百万行文本及大型目录树的精准对比。",
    feat2Title: "对齐幻影行 (Phantom Lines)",
    feat2Desc: "直观的空白对齐与字符级高亮，差异块一目了然，支持左右双向差异合并采纳。",
    feat3Title: "双向目录同步",
    feat3Desc: "支持深度 Hash 与元数据比对模式，带预演 (Dry-Run) 安全机制的一键目录双向同步。",
    feat4Title: "Git 三向合并 (3-Way Merge)",
    feat4Desc: "无缝衔接 Local、Base、Remote 冲突分支，智能自动解决非冲突项，提供完整 Git CLI 支持。",
    feat5Title: "多标签与多窗口拖拽",
    feat5Desc: "支持像 Safari 一样自由拖拽合并与分离标签页，多任务比对井井有条。",
    feat6Title: "原生多语言与深浅外观",
    feat6Desc: "内置中文、英文、日文支持，完美适配 macOS 全局系统主题自适应与快捷切换。",
    cliTitle: "终端命令行与 Git 工具集成",
    cliSubtitle: "一键配置为 Git 默认合并与比对工具，在终端中随时唤起。",
    ctaTitle: "即刻体验极致流畅的比对体验",
    ctaSubtitle: "支持 macOS 14.0 Sonoma 及以上版本，开源免费。",
    footerDesc: "基于 MIT / Apache-2.0 许可证开源。"
  },
  en: {
    badge: "⚡ macOS Native • Universal Binary (Intel & Apple Silicon)",
    heroTitle: "Next-Gen File Comparison <span class='gradient-text'>& 3-Way Merge for macOS</span>",
    heroSubtitle: "Engineered with a high-performance Rust core and native SwiftUI. The ultimate diff & merge suite built for macOS developers.",
    downloadBtn: "Download DMG (v0.1.1)",
    githubBtn: "View on GitHub",
    textDiffTab: "Text Diff",
    folderDiffTab: "Folder Diff",
    threeWayMergeTab: "3-Way Merge",
    themeDark: "Dark UI",
    themeLight: "Light UI",
    featuresTitle: "Built for Speed & Elegance",
    featuresSubtitle: "Native responsiveness with intuitive visual diffing and powerful productivity workflows.",
    feat1Title: "Rust Diff Engine",
    feat1Desc: "Powered by Myers algorithm and SIMD hashing, diffing millions of lines and large directories instantaneously.",
    feat1Desc: "Powered by Myers algorithm and SIMD hashing, diffing millions of lines instantaneously.",
    feat2Title: "Phantom Line Alignment",
    feat2Desc: "Smart empty line synchronization and token-level highlighting with bidirectional hunk navigation.",
    feat3Title: "Directory Synchronization",
    feat3Desc: "Deep Hash and Quick comparison modes with Dry-Run preview safety before applying sync operations.",
    feat4Title: "Git 3-Way Merge",
    feat4Desc: "Seamlessly resolve Local, Base, and Remote conflict branches with auto-resolution and CLI integration.",
    feat5Title: "Tab & Window Dragging",
    feat5Desc: "Tear off tabs to new windows or merge multiple windows together just like Safari.",
    feat6Title: "Theme & Multilingual",
    feat6Desc: "Built-in English, Chinese, and Japanese with automatic macOS system appearance synchronization.",
    cliTitle: "CLI & Git Mergetool Integration",
    cliSubtitle: "Configure MacCompare as your default git diff and mergetool in seconds.",
    ctaTitle: "Experience Seamless File Comparison Today",
    ctaSubtitle: "Compatible with macOS 14.0 Sonoma and later. Free & Open Source.",
    footerDesc: "Open source under MIT / Apache-2.0 license."
  },
  ja: {
    badge: "⚡ macOS ネイティブ • Universal Binary (Intel & Apple Silicon)",
    heroTitle: "次世代 macOS <span class='gradient-text'>ファイル比較＆3方向マージ</span>",
    heroSubtitle: "高性能 Rust コアとネイティブ SwiftUI で構築。macOS 開発者のための究極の差分比較ツール。",
    downloadBtn: "DMG をダウンロード (v0.1.1)",
    githubBtn: "GitHub で見る",
    textDiffTab: "テキスト比較",
    folderDiffTab: "フォルダ比較",
    threeWayMergeTab: "3方向マージ",
    themeDark: "ダーク",
    themeLight: "ライト",
    featuresTitle: "高速性と洗練されたデザイン",
    featuresSubtitle: "ネイティブの応答性と直感的な視覚差分フィードバックを実現。",
    feat1Title: "Rust 差分エンジン",
    feat1Desc: "Myers アルゴリズムと SIMD ハッシュにより、大規模ファイルやディレクトリを瞬時に比較。",
    feat2Title: "ファントム行アライメント",
    feat2Desc: "空白行の自動同期と単語レベルのハイライトで、差分箇所を一目で把握。",
    feat3Title: "フォルダ双方向同期",
    feat3Desc: "詳細ハッシュ比較とプレビュー（Dry-Run）確認による安全な双方向同期。",
    feat4Title: "Git 3方向マージ",
    feat4Desc: "Local、Base、Remote ブランチの競合を快適に解決。非競合の自動解決もサポート。",
    feat5Title: "タブとウインドウの統合",
    feat5Desc: "Safari のようにタブをドラッグして別ウインドウへの分離や結合が可能。",
    feat6Title: "多言語＆外観テーマ",
    feat6Desc: "日本語、英語、中国語を標準搭載。macOS の外観モードに完全連動。",
    cliTitle: "CLI & Git 統合",
    cliSubtitle: "Git のデフォルトのマージツールとして簡単に連携設定が可能。",
    ctaTitle: "今すぐ MacCompare を体験しましょう",
    ctaSubtitle: "macOS 14.0 Sonoma 以降に対応。オープンソース・無料。",
    footerDesc: "MIT / Apache-2.0 ライセンスに基づくオープンソース。"
  }
};

let currentLang = "zh";
let currentMode = "text";
let currentPreviewTheme = "dark";

const images = {
  text: {
    dark: "assets/text_diff_ui.jpg",
    light: "assets/text_diff_light_ui.jpg"
  },
  folder: {
    dark: "assets/folder_diff_ui.jpg",
    light: "assets/folder_diff_light_ui.jpg"
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

function updateShowcase() {
  const img = document.getElementById("showcase-img");
  if (img && images[currentMode] && images[currentMode][currentPreviewTheme]) {
    img.style.opacity = "0.4";
    setTimeout(() => {
      img.src = images[currentMode][currentPreviewTheme];
      img.style.opacity = "1";
    }, 150);
  }
}

// Mode tab clicks
document.querySelectorAll(".showcase-tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".showcase-tab").forEach(t => t.classList.remove("active"));
    tab.classList.add("active");
    currentMode = tab.getAttribute("data-mode");
    updateShowcase();
  });
});

// Preview theme clicks
document.querySelectorAll(".theme-opt-btn").forEach(btn => {
  btn.addEventListener("click", () => {
    document.querySelectorAll(".theme-opt-btn").forEach(b => b.classList.remove("active"));
    btn.classList.add("active");
    currentPreviewTheme = btn.getAttribute("data-theme-opt");
    updateShowcase();
  });
});

// Web page dark/light mode toggle
const toggleThemeBtn = document.getElementById("theme-toggle-btn");
if (toggleThemeBtn) {
  toggleThemeBtn.addEventListener("click", () => {
    const isLight = document.body.getAttribute("data-theme") === "light";
    if (isLight) {
      document.body.removeAttribute("data-theme");
      toggleThemeBtn.innerHTML = "🌙";
    } else {
      document.body.setAttribute("data-theme", "light");
      toggleThemeBtn.innerHTML = "☀️";
    }
  });
}

// Language selector
const langSelect = document.getElementById("lang-select");
if (langSelect) {
  langSelect.addEventListener("change", (e) => {
    updateLanguage(e.target.value);
  });
}

// Copy CLI commands
function copyCli() {
  const code = document.getElementById("cli-code").innerText;
  navigator.clipboard.writeText(code).then(() => {
    const btn = document.getElementById("copy-btn");
    btn.innerText = "✓ Copied!";
    setTimeout(() => {
      btn.innerText = "Copy";
    }, 2000);
  });
}

// Initialize
document.addEventListener("DOMContentLoaded", () => {
  updateLanguage("zh");
  updateShowcase();
});
