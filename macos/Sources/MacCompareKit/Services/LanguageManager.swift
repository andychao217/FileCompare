import Foundation
import SwiftUI
import AppKit

public enum AppLanguage: String, CaseIterable, Identifiable, Sendable {
    case system = "system"
    case zhHans = "zh-Hans"
    case en = "en"
    case ja = "ja"

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .system: return "Auto (System) / 跟随系统 / システム設定"
        case .zhHans: return "简体中文 (Chinese)"
        case .en: return "English"
        case .ja: return "日本語 (Japanese)"
        }
    }
}

public enum L10nKey: String, Sendable, CaseIterable {
    // Dialog Buttons & Actions
    case done
    case cancel
    case clear
    case clearAll

    // Menu Headers
    case fileMenu
    case editMenu
    case viewMenu
    case windowMenu
    case helpMenu

    // Menu Items - App Menu
    case aboutApp
    case settings
    case services
    case hideApp
    case hideOthers
    case showAll
    case quitApp

    // Menu Items - Standard Edit & View & Window & Help
    case undo
    case redo
    case cut
    case copy
    case paste
    case delete
    case selectAll
    case showTabBar
    case showAllTabs
    case toggleFullScreen
    case minimize
    case zoom
    case bringAllToFront
    case moveTabToNewWindow
    case mergeAllWindows
    case newWindow
    case help
    case userGuide
    case shortcuts
    case coreFeatures

    // Menu Items - Custom Commands
    case general
    case appearance
    case language
    case selectLanguage
    case defaultEncoding
    case about
    case version
    case architecture
    case universalBinary
    case newTextCompare
    case newFolderCompare
    case newThreeWayMerge
    case openFile
    case save
    case closeTab
    case close
    case closeAll
    case compare
    case nextDiff
    case prevDiff
    case takeLeft
    case takeRight
    case ignoreWhitespace
    case ignoreCase
    case gitHubRepo

    // Settings Specific
    case folderDiff
    case textDiff
    case threeWayMerge
    case defaultCompareMode
    case defaultExcludedPatterns
    case defaultDiffSettings

    // Text Diff
    case sourceFile
    case targetFile
    case noFileSelected
    case dropFilePrompt
    case chooseSourceFile
    case chooseTargetFile
    case chooseButton
    case saveButton
    case unsavedChanges
    case totalChanges
    case additions
    case deletions

    // Folder Diff
    case quickCompareMode
    case deepHashCompareMode
    case syncLeftToRight
    case syncRightToLeft
    case refresh
    case dryRunPreview
    case sourceFolder
    case targetFolder
    case noSourceFolder
    case noTargetFolder
    case dropFolderPrompt
    case chooseSourceFolder
    case chooseTargetFolder
    case quickPlaces
    case documents
    case downloads
    case desktop
    case browseFolder
    case tools
    case swapFolders
    case rescanFolders
    case recentCompares
    case scanningTree
    case selectTwoFoldersPrompt
    case itemsCount
    case modifiedCount
    case addedCount
    case deletedCount

    // Sync Sheet
    case dryRunTitle
    case dryRunSubtitle
    case completelyInSync
    case pendingOperations
    case executeSync
    case executing

    // 3-Way Merge
    case localBranch
    case baseBranch
    case remoteBranch
    case conflictCountFormat
    case autoResolveNonConflicts
    case saveAndCompleteMerge
    case acceptLocal
    case takeBoth
    case acceptRemote
    case mergedOutputResult
    case conflictsRemaining
    case allConflictsResolved
    case noFilesSelected
}

@MainActor
public final class LocalizedMenuDelegate: NSObject, NSMenuDelegate {
    public static let shared = LocalizedMenuDelegate()

    public func menuWillOpen(_ menu: NSMenu) {
        LanguageManager.shared.localizeMenu(menu)
    }

    public func menuNeedsUpdate(_ menu: NSMenu) {
        LanguageManager.shared.localizeMenu(menu)
    }
}

@MainActor
@Observable
public final class LanguageManager {
    public static let shared = LanguageManager()

    public var currentLanguage: AppLanguage {
        didSet {
            UserDefaults.standard.set(currentLanguage.rawValue, forKey: "mc_app_language")
            updateEffectiveLanguage()
        }
    }

    public private(set) var effectiveLanguage: AppLanguage = .en
    private var isUpdatingMenu = false
    private var reverseLookupMap: [String: L10nKey] = [:]

    private init() {
        let saved = UserDefaults.standard.string(forKey: "mc_app_language") ?? AppLanguage.system.rawValue
        self.currentLanguage = AppLanguage(rawValue: saved) ?? .system
        buildReverseLookup()
        updateEffectiveLanguage()

        NotificationCenter.default.addObserver(
            forName: NSApplication.didFinishLaunchingNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.localizeSystemMenu()
            }
        }
    }

    private func buildReverseLookup() {
        reverseLookupMap.removeAll()
        for (key, val) in enDictionary { reverseLookupMap[val] = key }
        for (key, val) in zhHansDictionary { reverseLookupMap[val] = key }
        for (key, val) in jaDictionary { reverseLookupMap[val] = key }
    }

    public func updateEffectiveLanguage() {
        if currentLanguage == .system {
            UserDefaults.standard.removeObject(forKey: "AppleLanguages")
            let preferred = Locale.preferredLanguages.first ?? "en"
            if preferred.starts(with: "zh") {
                effectiveLanguage = .zhHans
            } else if preferred.starts(with: "ja") {
                effectiveLanguage = .ja
            } else {
                effectiveLanguage = .en
            }
        } else {
            effectiveLanguage = currentLanguage
            UserDefaults.standard.set([currentLanguage.rawValue, "en"], forKey: "AppleLanguages")
        }

        applyMenuLocalizationCycle()
    }

    public func applyMenuLocalizationCycle() {
        localizeSystemMenu()

        DispatchQueue.main.async { [weak self] in
            self?.localizeSystemMenu()
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) { [weak self] in
            self?.localizeSystemMenu()
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) { [weak self] in
            self?.localizeSystemMenu()
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [weak self] in
            self?.localizeSystemMenu()
        }
    }

    public func text(_ key: L10nKey) -> String {
        switch effectiveLanguage {
        case .zhHans:
            return zhHansDictionary[key] ?? enDictionary[key] ?? key.rawValue
        case .ja:
            return jaDictionary[key] ?? enDictionary[key] ?? key.rawValue
        case .en, .system:
            return enDictionary[key] ?? key.rawValue
        }
    }

    // MARK: - Dynamic AppKit Main Menu Localization

    public func localizeSystemMenu() {
        guard let mainMenu = NSApp.mainMenu, !isUpdatingMenu else { return }
        isUpdatingMenu = true
        defer { isUpdatingMenu = false }

        for menuIndex in 0..<mainMenu.items.count {
            let item = mainMenu.items[menuIndex]
            guard let submenu = item.submenu else { continue }

            submenu.delegate = LocalizedMenuDelegate.shared

            // 1. Localize Top-Level Menu Titles
            switch menuIndex {
            case 0:
                item.title = "MacCompare"
                submenu.title = "MacCompare"
            case 1:
                item.title = text(.fileMenu)
                submenu.title = text(.fileMenu)
            case 2:
                item.title = text(.editMenu)
                submenu.title = text(.editMenu)
            case 3:
                item.title = text(.viewMenu)
                submenu.title = text(.viewMenu)
            case 4:
                item.title = text(.compare)
                submenu.title = text(.compare)
            case 5:
                item.title = text(.windowMenu)
                submenu.title = text(.windowMenu)
            case 6:
                item.title = text(.helpMenu)
                submenu.title = text(.helpMenu)
            default:
                break
            }

            // 2. Localize Submenu Items
            localizeMenu(submenu)
        }
    }

    public func localizeMenu(_ menu: NSMenu) {
        for item in menu.items {
            localizeMenuItem(item)
            if let sub = item.submenu {
                sub.delegate = LocalizedMenuDelegate.shared
                localizeMenu(sub)
            }
        }
    }

    private func localizeMenuItem(_ item: NSMenuItem) {
        // 1. Match from Reverse Dictionary Map
        if let key = reverseLookupMap[item.title] {
            item.title = text(key)
            return
        }

        // 2. Match standard selectors
        if let action = item.action {
            let actionName = NSStringFromSelector(action)
            switch actionName {
            case "orderFrontStandardAboutPanel:":
                item.title = text(.aboutApp)
                return
            case "hide:":
                item.title = text(.hideApp)
                return
            case "hideOtherApplications:":
                item.title = text(.hideOthers)
                return
            case "unhideAllApplications:":
                item.title = text(.showAll)
                return
            case "terminate:":
                item.title = text(.quitApp)
                return
            case "undo:":
                item.title = text(.undo)
                return
            case "redo:":
                item.title = text(.redo)
                return
            case "cut:":
                item.title = text(.cut)
                return
            case "copy:":
                item.title = text(.copy)
                return
            case "paste:":
                item.title = text(.paste)
                return
            case "delete:":
                item.title = text(.delete)
                return
            case "selectAll:":
                item.title = text(.selectAll)
                return
            case "toggleFullScreen:":
                item.title = text(.toggleFullScreen)
                return
            case "performMiniaturize:":
                item.title = text(.minimize)
                return
            case "performZoom:":
                item.title = text(.zoom)
                return
            case "arrangeInFront:":
                item.title = text(.bringAllToFront)
                return
            default:
                break
            }
        }

        // 3. Fallback substrings
        let lower = item.title.lowercased()
        if lower.contains("settings") || lower.contains("preferences") || item.title.contains("设置") || item.title.contains("設定") {
            item.title = text(.settings)
        } else if lower.contains("services") || item.title.contains("服务") || item.title.contains("サービス") {
            item.title = text(.services)
        } else if lower.contains("tab bar") || item.title.contains("标签页栏") || item.title.contains("タブバー") {
            item.title = text(.showTabBar)
        } else if lower.contains("all tabs") || item.title.contains("所有标签页") || item.title.contains("すべてのタブ") {
            item.title = text(.showAllTabs)
        } else if lower.contains("help") || item.title.contains("帮助") || item.title.contains("ヘルプ") {
            item.title = text(.help)
        }
    }

    // MARK: - Dictionaries

    private let enDictionary: [L10nKey: String] = [
        .done: "Done",
        .cancel: "Cancel",
        .clear: "Clear",
        .clearAll: "Clear All",
        .fileMenu: "File",
        .editMenu: "Edit",
        .viewMenu: "View",
        .windowMenu: "Window",
        .helpMenu: "Help",
        .aboutApp: "About MacCompare",
        .settings: "Settings...",
        .services: "Services",
        .hideApp: "Hide MacCompare",
        .hideOthers: "Hide Others",
        .showAll: "Show All",
        .quitApp: "Quit MacCompare",
        .undo: "Undo",
        .redo: "Redo",
        .cut: "Cut",
        .copy: "Copy",
        .paste: "Paste",
        .delete: "Delete",
        .selectAll: "Select All",
        .showTabBar: "Show Tab Bar",
        .showAllTabs: "Show All Tabs",
        .toggleFullScreen: "Enter Full Screen",
        .minimize: "Minimize",
        .zoom: "Zoom",
        .bringAllToFront: "Bring All to Front",
        .moveTabToNewWindow: "Move Tab to New Window",
        .mergeAllWindows: "Merge All Windows",
        .newWindow: "New Window",
        .help: "MacCompare Help",
        .userGuide: "User Guide",
        .shortcuts: "Keyboard Shortcuts",
        .coreFeatures: "Core Features",
        .general: "General",
        .appearance: "Appearance",
        .language: "Language",
        .selectLanguage: "Interface Language",
        .defaultEncoding: "Default File Encoding",
        .about: "About",
        .version: "Version",
        .architecture: "Architecture",
        .universalBinary: "Universal Binary (Intel & Apple Silicon)",
        .newTextCompare: "New Text Compare",
        .newFolderCompare: "New Folder Compare",
        .newThreeWayMerge: "New 3-Way Merge",
        .openFile: "Open File...",
        .save: "Save",
        .closeTab: "Close Tab",
        .close: "Close",
        .closeAll: "Close All",
        .compare: "Compare",
        .nextDiff: "Next Difference",
        .prevDiff: "Previous Difference",
        .takeLeft: "Take Left",
        .takeRight: "Take Right",
        .ignoreWhitespace: "Ignore Whitespace",
        .ignoreCase: "Ignore Case",
        .gitHubRepo: "GitHub Repository (andychao217/FileCompare)",
        .folderDiff: "Folder Diff",
        .textDiff: "Text Diff",
        .threeWayMerge: "3-Way Merge",
        .defaultCompareMode: "Default Compare Mode",
        .defaultExcludedPatterns: "Default Excluded Patterns",
        .defaultDiffSettings: "Default Diff Settings",
        .sourceFile: "Source File (Left)",
        .targetFile: "Target File (Right)",
        .noFileSelected: "No File Selected",
        .dropFilePrompt: "Drag & drop a file here or choose from disk",
        .chooseSourceFile: "Choose Source File...",
        .chooseTargetFile: "Choose Target File...",
        .chooseButton: "Choose...",
        .saveButton: "Save",
        .unsavedChanges: "Unsaved changes",
        .totalChanges: "changes",
        .additions: "additions",
        .deletions: "deletions",
        .quickCompareMode: "Quick Compare (Timestamp/Size)",
        .deepHashCompareMode: "Deep Hash Compare (CRC32)",
        .syncLeftToRight: "Sync Left to Right",
        .syncRightToLeft: "Sync Right to Left",
        .refresh: "Refresh",
        .dryRunPreview: "Dry Run Preview",
        .sourceFolder: "Source Folder",
        .targetFolder: "Target Folder",
        .noSourceFolder: "No Source Folder Selected",
        .noTargetFolder: "No Target Folder Selected",
        .dropFolderPrompt: "Drag & drop a directory here or click below to choose",
        .chooseSourceFolder: "Choose Source Directory...",
        .chooseTargetFolder: "Choose Target Directory...",
        .quickPlaces: "Quick Places",
        .documents: "Documents",
        .downloads: "Downloads",
        .desktop: "Desktop",
        .browseFolder: "Browse Folder...",
        .tools: "Tools",
        .swapFolders: "Swap Left & Right",
        .rescanFolders: "Rescan Folders",
        .recentCompares: "Recent Compares",
        .scanningTree: "Scanning directory tree...",
        .selectTwoFoldersPrompt: "Select two folders to begin comparison.",
        .itemsCount: "items",
        .modifiedCount: "modified",
        .addedCount: "added",
        .deletedCount: "deleted",
        .dryRunTitle: "Dry Run: Sync Actions Preview",
        .dryRunSubtitle: "Review pending disk operations before executing.",
        .completelyInSync: "Directories are completely in sync.",
        .pendingOperations: "Pending Sync Operations",
        .executeSync: "Execute Sync",
        .executing: "Executing...",
        .localBranch: "Local (Current Branch)",
        .baseBranch: "Base (Common Ancestor)",
        .remoteBranch: "Remote (Incoming Branch)",
        .conflictCountFormat: "Conflict",
        .autoResolveNonConflicts: "Auto-Resolve Non-Conflicts",
        .saveAndCompleteMerge: "Save & Complete Merge",
        .acceptLocal: "Accept Local",
        .takeBoth: "Take Both",
        .acceptRemote: "Accept Remote",
        .mergedOutputResult: "Merged Output Result",
        .conflictsRemaining: "Conflicts Remaining",
        .allConflictsResolved: "All Conflicts Resolved",
        .noFilesSelected: "No Files Selected"
    ]

    private let zhHansDictionary: [L10nKey: String] = [
        .done: "完成",
        .cancel: "取消",
        .clear: "清空",
        .clearAll: "一键清空",
        .fileMenu: "文件",
        .editMenu: "编辑",
        .viewMenu: "显示",
        .windowMenu: "窗口",
        .helpMenu: "帮助",
        .aboutApp: "关于 MacCompare",
        .settings: "设置...",
        .services: "服务",
        .hideApp: "隐藏 MacCompare",
        .hideOthers: "隐藏其他",
        .showAll: "全部显示",
        .quitApp: "退出 MacCompare",
        .undo: "撤销",
        .redo: "重做",
        .cut: "剪切",
        .copy: "拷贝",
        .paste: "粘贴",
        .delete: "删除",
        .selectAll: "全选",
        .showTabBar: "显示标签页栏",
        .showAllTabs: "显示所有标签页",
        .toggleFullScreen: "进入全屏幕",
        .minimize: "最小化",
        .zoom: "缩放",
        .bringAllToFront: "前置全部窗口",
        .moveTabToNewWindow: "将标签页移到新窗口",
        .mergeAllWindows: "合并所有窗口",
        .newWindow: "新建窗口",
        .help: "MacCompare 帮助",
        .userGuide: "用户使用指南",
        .shortcuts: "快捷键大全",
        .coreFeatures: "核心功能说明",
        .general: "常规",
        .appearance: "外观",
        .language: "语言",
        .selectLanguage: "软件语言",
        .defaultEncoding: "默认文件编码",
        .about: "关于",
        .version: "版本",
        .architecture: "架构支持",
        .universalBinary: "通用二进制 (Intel & Apple Silicon)",
        .newTextCompare: "新建文本比对",
        .newFolderCompare: "新建文件夹比对",
        .newThreeWayMerge: "新建三向合并",
        .openFile: "打开文件...",
        .save: "保存",
        .closeTab: "关闭标签页",
        .close: "关闭窗口",
        .closeAll: "关闭全部窗口",
        .compare: "比对",
        .nextDiff: "下一个差异",
        .prevDiff: "上一个差异",
        .takeLeft: "采纳左侧",
        .takeRight: "采纳右侧",
        .ignoreWhitespace: "忽略空白符",
        .ignoreCase: "忽略大小写",
        .gitHubRepo: "GitHub 开源仓库 (andychao217/FileCompare)",
        .folderDiff: "文件夹比对",
        .textDiff: "文本比对",
        .threeWayMerge: "三向合并",
        .defaultCompareMode: "默认比对模式",
        .defaultExcludedPatterns: "默认排除过滤规则",
        .defaultDiffSettings: "默认比对配置",
        .sourceFile: "源文件 (左侧)",
        .targetFile: "目标文件 (右侧)",
        .noFileSelected: "未选择文件",
        .dropFilePrompt: "拖拽文件至此处或点击下方按钮选择",
        .chooseSourceFile: "选择源文件...",
        .chooseTargetFile: "选择目标文件...",
        .chooseButton: "选择...",
        .saveButton: "保存",
        .unsavedChanges: "有未保存更改",
        .totalChanges: "处修改",
        .additions: "处新增",
        .deletions: "处删除",
        .quickCompareMode: "快速比对 (时间戳/大小)",
        .deepHashCompareMode: "深度哈希比对 (CRC32)",
        .syncLeftToRight: "从左向右同步",
        .syncRightToLeft: "从右向左同步",
        .refresh: "刷新",
        .dryRunPreview: "预演预览 (Dry Run)",
        .sourceFolder: "源文件夹",
        .targetFolder: "目标文件夹",
        .noSourceFolder: "未选择源文件夹",
        .noTargetFolder: "未选择目标文件夹",
        .dropFolderPrompt: "拖拽文件夹至此处或点击下方按钮选择",
        .chooseSourceFolder: "选择源文件夹...",
        .chooseTargetFolder: "选择目标文件夹...",
        .quickPlaces: "常用位置",
        .documents: "文稿目录 (Documents)",
        .downloads: "下载目录 (Downloads)",
        .desktop: "桌面 (Desktop)",
        .browseFolder: "浏览文件夹...",
        .tools: "工具",
        .swapFolders: "左右目录对调",
        .rescanFolders: "重新扫描目录",
        .recentCompares: "最近比对记录",
        .scanningTree: "正在递归扫描目录树...",
        .selectTwoFoldersPrompt: "请选择左右两侧文件夹以开始比对。",
        .itemsCount: "项",
        .modifiedCount: "处修改",
        .addedCount: "处新增",
        .deletedCount: "处删除",
        .dryRunTitle: "预演: 文件同步操作清单",
        .dryRunSubtitle: "在执行实际磁盘写操作前仔细检查以下变更项目。",
        .completelyInSync: "两侧文件夹内容完全一致，无需同步。",
        .pendingOperations: "待执行同步项",
        .executeSync: "执行同步",
        .executing: "正在执行...",
        .localBranch: "本地分支 (Local)",
        .baseBranch: "共同祖先 (Base)",
        .remoteBranch: "远端分支 (Remote)",
        .conflictCountFormat: "冲突项",
        .autoResolveNonConflicts: "自动解决无冲突项",
        .saveAndCompleteMerge: "保存并完成合并",
        .acceptLocal: "采纳本地",
        .takeBoth: "保留两者",
        .acceptRemote: "采纳远端",
        .mergedOutputResult: "合并结果预览",
        .conflictsRemaining: "处冲突待解决",
        .allConflictsResolved: "所有冲突已解决",
        .noFilesSelected: "未选择合并文件"
    ]

    private let jaDictionary: [L10nKey: String] = [
        .done: "完了",
        .cancel: "キャンセル",
        .clear: "クリア",
        .clearAll: "すべてクリア",
        .fileMenu: "ファイル",
        .editMenu: "編集",
        .viewMenu: "表示",
        .windowMenu: "ウインドウ",
        .helpMenu: "ヘルプ",
        .aboutApp: "MacCompare について",
        .settings: "設定...",
        .services: "サービス",
        .hideApp: "MacCompare を隠す",
        .hideOthers: "他を隠す",
        .showAll: "すべてを表示",
        .quitApp: "MacCompare を終了",
        .undo: "取り消す",
        .redo: "やり直す",
        .cut: "カット",
        .copy: "コピー",
        .paste: "ペースト",
        .delete: "削除",
        .selectAll: "すべてを選択",
        .showTabBar: "タブバーを表示",
        .showAllTabs: "すべてのタブを表示",
        .toggleFullScreen: "フルスクリーンにする",
        .minimize: "最小化",
        .zoom: "拡大/縮小",
        .bringAllToFront: "すべてを手前に移動",
        .moveTabToNewWindow: "タブを新しいウインドウに移動",
        .mergeAllWindows: "すべてのウインドウを結合",
        .newWindow: "新規ウインドウ",
        .help: "MacCompare ヘルプ",
        .userGuide: "ユーザーガイド",
        .shortcuts: "ショートカット一覧",
        .coreFeatures: "主な機能",
        .general: "一般",
        .appearance: "外観",
        .language: "言語",
        .selectLanguage: "表示言語",
        .defaultEncoding: "デフォルトの文字コード",
        .about: "情報",
        .version: "バージョン",
        .architecture: "アーキテクチャ",
        .universalBinary: "ユニバーサルバイナリ (Intel & Apple Silicon)",
        .newTextCompare: "新規テキスト比較",
        .newFolderCompare: "新規フォルダ比較",
        .newThreeWayMerge: "新規3方向マージ",
        .openFile: "ファイルを開く...",
        .save: "保存",
        .closeTab: "タブを閉じる",
        .close: "ウインドウを閉じる",
        .closeAll: "すべてのウインドウを閉じる",
        .compare: "比較",
        .nextDiff: "次の差分",
        .prevDiff: "前の差分",
        .takeLeft: "左側を採用",
        .takeRight: "右側を採用",
        .ignoreWhitespace: "空白を無視",
        .ignoreCase: "大文字/小文字を無視",
        .gitHubRepo: "GitHub リポジトリ (andychao217/FileCompare)",
        .folderDiff: "フォルダ比較",
        .textDiff: "テキスト比較",
        .threeWayMerge: "3方向マージ",
        .defaultCompareMode: "デフォルト比較モード",
        .defaultExcludedPatterns: "デフォルト除外パターン",
        .defaultDiffSettings: "デフォルト比較設定",
        .sourceFile: "ソースファイル (左)",
        .targetFile: "ターゲットファイル (右)",
        .noFileSelected: "ファイルが選択されていません",
        .dropFilePrompt: "ファイルをここにドラッグ＆ドロップまたは選択",
        .chooseSourceFile: "ソースファイルを選択...",
        .chooseTargetFile: "ターゲットファイルを選択...",
        .chooseButton: "選択...",
        .saveButton: "保存",
        .unsavedChanges: "未保存の変更あり",
        .totalChanges: "箇所の変更",
        .additions: "箇所の追加",
        .deletions: "箇所の削除",
        .quickCompareMode: "高速比較 (日時/サイズ)",
        .deepHashCompareMode: "詳細ハッシュ比較 (CRC32)",
        .syncLeftToRight: "左から右へ同期",
        .syncRightToLeft: "右から左へ同期",
        .refresh: "更新",
        .dryRunPreview: "同期プレビュー (Dry Run)",
        .sourceFolder: "ソースフォルダ",
        .targetFolder: "ターゲットフォルダ",
        .noSourceFolder: "ソースフォルダが未選択",
        .noTargetFolder: "ターゲットフォルダが未選択",
        .dropFolderPrompt: "フォルダをここにドラッグ＆ドロップまたは選択",
        .chooseSourceFolder: "ソースフォルダを選択...",
        .chooseTargetFolder: "ターゲットフォルダを選択...",
        .quickPlaces: "よく使う場所",
        .documents: "書類 (Documents)",
        .downloads: "ダウンロード (Downloads)",
        .desktop: "デスクトップ (Desktop)",
        .browseFolder: "フォルダを参照...",
        .tools: "ツール",
        .swapFolders: "左右のフォルダを入れ替え",
        .rescanFolders: "フォルダを再スキャン",
        .recentCompares: "最近の比較履歴",
        .scanningTree: "ディレクトリツリーをスキャン中...",
        .selectTwoFoldersPrompt: "比較を開始するには2つのフォルダを選択してください。",
        .itemsCount: "項目",
        .modifiedCount: "件の変更",
        .addedCount: "件の追加",
        .deletedCount: "件の削除",
        .dryRunTitle: "プレビュー: 同期アクション一覧",
        .dryRunSubtitle: "実際の同期を実行する前に変更内容を確認してください。",
        .completelyInSync: "両側のフォルダは完全に一致しています。",
        .pendingOperations: "保留中の同期項目",
        .executeSync: "同期を実行",
        .executing: "実行中...",
        .localBranch: "ローカルブランチ (Local)",
        .baseBranch: "共通の先祖 (Base)",
        .remoteBranch: "リモートブランチ (Remote)",
        .conflictCountFormat: "競合",
        .autoResolveNonConflicts: "非競合の変更を自動解決",
        .saveAndCompleteMerge: "保存してマージを完了",
        .acceptLocal: "ローカルを採用",
        .takeBoth: "両方を保持",
        .acceptRemote: "リモートを採用",
        .mergedOutputResult: "マージ結果プレビュー",
        .conflictsRemaining: "箇所の未解決の競合",
        .allConflictsResolved: "すべての競合が解決されました",
        .noFilesSelected: "マージファイルが選択されていません"
    ]
}
