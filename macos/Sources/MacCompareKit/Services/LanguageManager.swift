import Foundation
import SwiftUI
import AppKit

public enum AppLanguage: String, CaseIterable, Identifiable, Sendable {
    case system = "system"
    case zhHans = "zh-Hans"
    case en = "en"
    case ja = "ja"

    public var id: String { rawValue }

    public func localizedName(for language: AppLanguage) -> String {
        switch self {
        case .system:
            switch language {
            case .zhHans: return "跟随系统 (Auto)"
            case .ja: return "システム設定 (Auto)"
            default: return "Auto (System)"
            }
        case .zhHans: return "简体中文"
        case .en: return "English"
        case .ja: return "日本語"
        }
    }

    @MainActor
    public var displayName: String {
        localizedName(for: LanguageManager.shared.effectiveLanguage)
    }
}

public enum L10nKey: String, Sendable, CaseIterable {
    // Dialog Buttons & Actions
    case done
    case cancel
    case clear
    case clearAll
    case confirmClearTitle
    case confirmClearMessage

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

    // Help Specific
    case helpSubtitle
    case tabDragMergeTitle
    case textDiffDesc
    case folderDiffDesc
    case threeWayMergeDesc
    case tabDragMergeDesc
    case cancelTabDragDesc
    case textDiffGuideDetail
    case excelDiffGuideDetail
    case wordDiffGuideDetail
    case folderDiffGuideDetail
    case threeWayMergeGuideDetail
    case gitMergetoolConfigGuide
    case nextTab
    case prevTab
    case aboutFooter

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
    case newWordCompare
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
    case createBakBackupTitle
    case createBakBackupDesc

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

    // Update Checker
    case checkForUpdates
    case checkingForUpdates
    case upToDateTitle
    case upToDateMessage
    case newVersionAvailableTitle
    case newVersionAvailableMessage
    case downloadUpdate
    case releaseNotes
    case noReleaseNotes
    case currentVersion
    case later
    case autoCheckUpdatesOnLaunch
    case lastChecked
    case checkFailed

    // Word Diff Specific
    case wordDiff
    case structuredContent
    case formattingDiff
    case tableDiff
    case metadataDiff
    case documentOutline
    case noHeadingsDetected
    case headingsHint
    case formatChanged
    case diffOptions
    case ignoreFormatting
    case exportReport
    case searchOrFilter
    case parsingWordDocument
    case toggleOutline
    case dropWordPrompt
    case noTablesDetected
    case property
    case wordCount
    case paragraphs
    case fileSize
    case added
    case deleted
    case modified

    // Excel Diff Specific
    case newExcelCompare
    case excelDiff
    case filterAll
    case filterDiffs
    case filterSame
    case excelRules
    case excelRulesTitle
    case excelFirstRowHeader
    case excelNumericTolerance
    case excelToleranceValue
    case excelKeyColumnsHeader
    case excelKeyColumnsDesc
    case excelRowInspectorTitle
    case excelDifferenceRows
    case excelSameRows
    case excelLoadTime
    case excelLoadingPrompt
    case selectExcelFilesPrompt
    case statusModified
    case statusAdded
    case statusDeleted
    case copyLeft
    case copyRight
    case swapSides
    case selectFilePrompt
    case retry

    // Welcome Home Hub Specific
    case welcomeTitle
    case welcomeSubtitle
    case welcomeTextDiffTitle
    case welcomeTextDiffDesc
    case welcomeExcelDiffTitle
    case welcomeExcelDiffDesc
    case welcomeWordDiffTitle
    case welcomeWordDiffDesc
    case welcomeFolderDiffTitle
    case welcomeFolderDiffDesc
    case recentComparisons
    case openExisting
    case noRecentComparisons
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
        .confirmClearTitle: "Clear All Content?",
        .confirmClearMessage: "This will unload all loaded files/folders and reset comparison state in this tab. Unsaved changes will be lost.",
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
        .helpSubtitle: "macOS Native Diff & Merge Suite",
        .tabDragMergeTitle: "Chrome-Style Tab Drag & Merge",
        .textDiffDesc: "High-performance two-way text diffing with line highlighting and hunk merging.",
        .textDiffGuideDetail: "• Adding Files: Drag & drop two files from Finder into the left/right panes, or click 'Choose...'. Single file loads neutrally; both files trigger comparison.\n• Merge & Actions: Click 'Take Left/Right' to merge hunks and ⌘ S to save.\n• Options: Toggle 'Ignore Whitespace' / 'Ignore Case'; clear with safety confirmation; optionally enable '.bak' backups in Settings.",
        .excelDiffGuideDetail: "• Multi-Sheet Support: Auto-detects and aligns all worksheets across workbooks (.xlsx / .xls / .csv / .tsv).\n• Comparison Engine: Key-based column mapping & Myers LCS row alignment with character-level intra-cell diffing.\n• Rules & Inspector: Configurable floating-point numeric tolerance, case/whitespace sensitivity, and bottom row detail inspector.",
        .wordDiffGuideDetail: "• Rich Text Comparison: Compares text paragraphs, styles (bold/italic/underline/color), and structural changes in .docx files.\n• Visual Diff: Inspect differences across document outline, embedded tables, and media/vector images with image hash matching.",
        .folderDiffDesc: "Deep directory comparison and two-way file tree synchronization.",
        .folderDiffGuideDetail: "• Adding Folders: Drag & drop directories from Finder, or choose from sidebar quick places and recent history.\n• Comparison Modes: Choose 'Quick Mode' (file size & timestamp) or 'Deep Hash Mode' (CRC32 checksums).\n• Safe Sync: Click 'Dry-Run Preview' to inspect all planned file actions before applying physical synchronization.",
        .threeWayMergeDesc: "Interactive 3-way conflict resolution based on a common Base ancestor.",
        .threeWayMergeGuideDetail: "• Panes Overview: Top panels display Local Branch (Mine / Orange), Base Ancestor (Gray), and Remote Branch (Theirs / Green). Bottom panel displays Merged Output.\n• Standalone Manual Use: Drag 3 files or click 'Choose...' to merge offline configs or documents. Click 'Auto-Resolve Non-Conflicts' to merge clean changes automatically, only resolving true conflicts manually.\n• Git Mergetool Integration: Seamlessly resolves merge/rebase conflicts without manual file picking. Run 'git mergetool' to auto-populate files.",
        .gitMergetoolConfigGuide: "Git mergetool Setup Commands (Run in Terminal):",
        .tabDragMergeDesc: "Drag tabs to reorder within window, drag outside to tear off into standalone window, and drag into another window's tab bar to merge seamlessly.",
        .cancelTabDragDesc: "Cancel ongoing tab dragging operation",
        .nextTab: "Next Tab",
        .prevTab: "Previous Tab",
        .aboutFooter: "Built for macOS 14.0+ with Swift & SwiftUI.",
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
        .createBakBackupTitle: "Create .bak backup files when saving",
        .createBakBackupDesc: "Automatically duplicate original files as .bak before overwriting to prevent accidental data loss.",
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
        .noFilesSelected: "No Files Selected",
        .checkForUpdates: "Check for Updates...",
        .checkingForUpdates: "Checking for Updates...",
        .upToDateTitle: "You're Up to Date",
        .upToDateMessage: "MacCompare is currently at the latest version.",
        .newVersionAvailableTitle: "New Version Available",
        .newVersionAvailableMessage: "A new version of MacCompare is available for download.",
        .downloadUpdate: "Download Update",
        .releaseNotes: "Release Notes",
        .noReleaseNotes: "No release notes provided for this release.",
        .currentVersion: "Current Version",
        .later: "Later",
        .autoCheckUpdatesOnLaunch: "Automatically check for updates on launch",
        .lastChecked: "Last checked",
        .checkFailed: "Failed to check for updates. Please check your network connection.",
        .newWordCompare: "New Word Compare",
        .wordDiff: "Word Document Diff",
        .structuredContent: "Structured Content",
        .formattingDiff: "Formatting Diff",
        .tableDiff: "Table Diff",
        .metadataDiff: "Metadata Diff",
        .documentOutline: "Document Outline",
        .noHeadingsDetected: "No Headings Detected",
        .headingsHint: "Word heading styles (H1~H6) will be automatically extracted here as an outline",
        .formatChanged: "Format Changed",
        .diffOptions: "Diff Options",
        .ignoreFormatting: "Ignore Formatting",
        .exportReport: "Export Report",
        .searchOrFilter: "Search / Filter...",
        .parsingWordDocument: "Parsing Word Document...",
        .toggleOutline: "Toggle Outline Sidebar",
        .dropWordPrompt: "Drag & drop .docx / .doc files here to compare",
        .noTablesDetected: "No Tables Detected in Documents",
        .property: "Property",
        .wordCount: "Word Count",
        .paragraphs: "Paragraphs",
        .fileSize: "File Size",
        .added: "Added",
        .deleted: "Deleted",
        .modified: "Modified",
        .newExcelCompare: "New Excel Compare",
        .excelDiff: "Excel Spreadsheet Diff",
        .filterAll: "All",
        .filterDiffs: "Diffs",
        .filterSame: "Same",
        .excelRules: "Rules",
        .excelRulesTitle: "Comparison Rules & Settings",
        .excelFirstRowHeader: "First row as header",
        .excelNumericTolerance: "Numeric Tolerance",
        .excelToleranceValue: "Tolerance (e.g. 0.001)",
        .excelKeyColumnsHeader: "Key Columns (Alignment)",
        .excelKeyColumnsDesc: "Select key columns to align rows across sheets:",
        .excelRowInspectorTitle: "Row Detail Inspector",
        .excelDifferenceRows: "difference row(s)",
        .excelSameRows: "identical row(s)",
        .excelLoadTime: "Load time",
        .excelLoadingPrompt: "Parsing and comparing Excel spreadsheets...",
        .selectExcelFilesPrompt: "Select or drop two Excel (.xlsx / .xls) files to compare",
        .statusModified: "MODIFIED",
        .statusAdded: "ADDED",
        .statusDeleted: "DELETED",
        .copyLeft: "Copy Left Value",
        .copyRight: "Copy Right Value",
        .swapSides: "Swap Sides",
        .selectFilePrompt: "Choose file...",
        .retry: "Retry",
        .welcomeTitle: "Welcome to MacCompare",
        .welcomeSubtitle: "Select a comparison mode to get started",
        .welcomeTextDiffTitle: "Text & Code Compare",
        .welcomeTextDiffDesc: "Fine-grained line & token diff across source code",
        .welcomeExcelDiffTitle: "Excel Table Compare",
        .welcomeExcelDiffDesc: "Multi-sheet sync, cell-level highlighting & alignment",
        .welcomeWordDiffTitle: "Word Document Compare",
        .welcomeWordDiffDesc: "Styles, embedded tables, vector shapes & outline",
        .welcomeFolderDiffTitle: "Folder Diff & Sync",
        .welcomeFolderDiffDesc: "Fast CRC32 scan & bidirectional safe sync",
        .recentComparisons: "Recent Comparisons",
        .openExisting: "Open Existing...",
        .noRecentComparisons: "No recent comparisons yet"
    ]

    private let zhHansDictionary: [L10nKey: String] = [
        .done: "完成",
        .cancel: "取消",
        .clear: "清空",
        .clearAll: "一键清空",
        .confirmClearTitle: "确认清空全部内容？",
        .confirmClearMessage: "此操作将卸载当前标签页中已加载的所有文件/文件夹并重置对比状态，未保存的修改将会丢失。",
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
        .helpSubtitle: "macOS 原生高效比对与合并套件",
        .tabDragMergeTitle: "Chrome 风格标签页拖拽与合并",
        .textDiffDesc: "双向文本差异比对与逐行同步工具。",
        .textDiffGuideDetail: "• 如何添加文件：直接从访达（Finder）将两份文件拖入左右面板，或点击右上角「选择...」按钮。单侧加载保持中性无色，双侧加载后自动高亮比对。\n• 差异采纳与保存：点击「← / → 采纳」可快速将当前差异块复制到对侧，按 ⌘ S 保存修改。\n• 过滤与安全防护：支持忽略空白符/大小写切换；支持一键清空重选（带二次确认）；可在设置中开启「保存时自动创建 .bak 备份」。",
        .excelDiffGuideDetail: "• 多工作表智能对齐：自动识别并对齐工作簿（.xlsx / .xls / .csv / .tsv）中的所有 Sheet 工作表。\n• 高精度对齐引擎：支持按指定主键列 Map Join 关联对齐，或基于 Myers LCS 算法实现整行动态对齐，提供字符级单元格内联高亮。\n• 规则与明细检查器：支持配置浮点数容差（Tolerance）、大小写与空白符忽略，底部提供当前行各列明细检查器（Row Detail Inspector）。",
        .wordDiffGuideDetail: "• 富文本段落流对比：精准对比 .docx 文档的段落文本、文字样式（加粗/倾斜/下划线/前景色）与结构变动。\n• 多维可视化差异：支持大纲目录变动检查、嵌入表格单元格网格级比对、以及基于图片感知哈希的图片与矢量图形比对。",
        .folderDiffDesc: "目录树结构深度对比与双向文件同步工具。",
        .folderDiffGuideDetail: "• 如何添加文件夹：从访达拖入文件夹至左右区域，或在左侧边栏快速选取常用目录（文稿、下载、桌面等）及最近历史记录。\n• 比对模式切换：提供「快速比对」（基于文件大小与修改时间戳）与「深度哈希比对」（基于 CRC32 内容计算）。\n• 安全同步预演：在执行从左到右或从右到左物理同步前，可点击「演练预览 (Dry-Run)」查看所有文件变更计划，确认无误后再执行。",
        .threeWayMergeDesc: "基于共同基准祖先（Base）的三向冲突合并工具。",
        .threeWayMergeGuideDetail: "• 区域说明：顶部自左向右为「本地分支 (Local / 橙色)」、「基准祖先 (Base / 灰色)」、「远程分支 (Remote / 绿色)」，底部为「合并产物 (Merged)」。\n• 手动添加场景：从访达拖入三个文件或点击「选择...」，用于离线配置文件升级或多人版本整合。点击「自动解决无冲突项」可自动融合单侧修改，仅需手动点选冲突项。\n• Git 冲突自动联动 (git mergetool)：作为 Git 冲突解决工具使用时，无需手动选文件。当 git merge / rebase 发生冲突时在终端输入 git mergetool，MacCompare 会自动填充 Local、Base、Remote 并引导完成合并。",
        .gitMergetoolConfigGuide: "Git mergetool 配置命令（在终端中运行）：",
        .tabDragMergeDesc: "支持在窗口内拖拽排序、按住标签拖出窗口拆分为独立新窗口、将标签拖入其他窗口顶部标签栏实现多窗口自由合并。",
        .cancelTabDragDesc: "取消当前正在进行的标签页拖拽",
        .nextTab: "下一个标签页",
        .prevTab: "上一个标签页",
        .aboutFooter: "专为 macOS 14.0+ 打造，采用 Swift 与 SwiftUI 纯原生实现。",
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
        .createBakBackupTitle: "保存时自动创建 .bak 备份文件",
        .createBakBackupDesc: "在覆盖保存原文件前自动生成 .bak 副本，防止误操作丢失旧版本内容。",
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
        .noFilesSelected: "未选择合并文件",
        .checkForUpdates: "检查更新...",
        .checkingForUpdates: "正在检查更新...",
        .upToDateTitle: "已是最新版本",
        .upToDateMessage: "MacCompare 当前已经是最新版本，无需更新。",
        .newVersionAvailableTitle: "发现新版本",
        .newVersionAvailableMessage: "MacCompare 有新版本可供下载更新。",
        .downloadUpdate: "立即下载更新",
        .releaseNotes: "版本更新说明",
        .noReleaseNotes: "此版本暂无更新说明。",
        .currentVersion: "当前版本",
        .later: "稍后提醒",
        .autoCheckUpdatesOnLaunch: "启动时自动检查更新",
        .lastChecked: "上次检查时间",
        .checkFailed: "检查更新失败，请检查您的网络连接。",
        .newWordCompare: "新建 Word 对比",
        .wordDiff: "Word 文档比对",
        .structuredContent: "结构化内容比对",
        .formattingDiff: "样式格式比对",
        .tableDiff: "表格网格比对",
        .metadataDiff: "文档元数据比对",
        .documentOutline: "文档章节大纲",
        .noHeadingsDetected: "未检测到标题大纲",
        .headingsHint: "Word 标题样式（H1~H6）将自动在此提取为章节大纲",
        .formatChanged: "格式变动",
        .diffOptions: "比对选项",
        .ignoreFormatting: "忽略格式微调",
        .exportReport: "导出差异报告",
        .searchOrFilter: "搜索与过滤段落...",
        .parsingWordDocument: "正在解析 Word 文档...",
        .toggleOutline: "切换大纲侧栏",
        .dropWordPrompt: "拖拽 .docx 或 .doc 文档至此处开始比对",
        .noTablesDetected: "文档中未包含表格",
        .property: "文档属性",
        .wordCount: "字数统计",
        .paragraphs: "段落数",
        .fileSize: "文件大小",
        .added: "新增",
        .deleted: "删除",
        .modified: "修改",
        .newExcelCompare: "新建 Excel 对比",
        .excelDiff: "Excel 表格比对",
        .filterAll: "全部",
        .filterDiffs: "仅差异",
        .filterSame: "仅相同",
        .excelRules: "比对规则",
        .excelRulesTitle: "比对规则与设置",
        .excelFirstRowHeader: "首行作为表头",
        .excelNumericTolerance: "数值容差",
        .excelToleranceValue: "容差阈值（如 0.001）",
        .excelKeyColumnsHeader: "主键列（用于行对齐）",
        .excelKeyColumnsDesc: "选择用于对齐各工作表行的关键列：",
        .excelRowInspectorTitle: "当前行明细检查器",
        .excelDifferenceRows: "处差异行",
        .excelSameRows: "处相同行",
        .excelLoadTime: "耗时",
        .excelLoadingPrompt: "正在解析并比对 Excel 表格数据...",
        .selectExcelFilesPrompt: "选择或拖拽两个 Excel (.xlsx / .xls) 文件以开始比对",
        .statusModified: "已修改",
        .statusAdded: "新增行",
        .statusDeleted: "已删除",
        .copyLeft: "复制左侧单元格内容",
        .copyRight: "复制右侧单元格内容",
        .swapSides: "左右对调",
        .selectFilePrompt: "选择文件...",
        .retry: "重试",
        .welcomeTitle: "欢迎使用 MacCompare",
        .welcomeSubtitle: "选择对比模式开始高效比对",
        .welcomeTextDiffTitle: "文本与代码对比",
        .welcomeTextDiffDesc: "行级与分词级精细对比，支持多种语言语法",
        .welcomeExcelDiffTitle: "Excel 表格对比",
        .welcomeExcelDiffDesc: "多工作表自动识别、智能行对齐与单元格高亮",
        .welcomeWordDiffTitle: "Word 文档对比",
        .welcomeWordDiffDesc: "正文段落流、富文本样式、表格与图形比对",
        .welcomeFolderDiffTitle: "文件夹极速对比与同步",
        .welcomeFolderDiffDesc: "目录结构毫秒级扫描、CRC32 哈希与双向同步",
        .recentComparisons: "最近对比记录",
        .openExisting: "打开本地文件...",
        .noRecentComparisons: "暂无历史比对记录"
    ]

    private let jaDictionary: [L10nKey: String] = [
        .done: "完了",
        .cancel: "キャンセル",
        .clear: "クリア",
        .clearAll: "すべてクリア",
        .confirmClearTitle: "すべての内容をクリアしますか？",
        .confirmClearMessage: "このタブで読み込まれたすべてのファイル/フォルダをクリアし、比較状態をリセットします。未保存の変更は失われます。",
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
        .helpSubtitle: "macOS ネイティブの差分比較・マージツール",
        .tabDragMergeTitle: "Chrome風タブドラッグ＆結合",
        .textDiffDesc: "双方向テキスト差分比較および行単位同期ツール。",
        .textDiffGuideDetail: "• ファイルの追加方法：Finder から左右のパネルにファイルをドラッグ＆ドロップするか、「選択...」ボタンをクリックします。単側読み込みは中立表示され、両側読み込みで自動差分比較されます。\n• 差分マージと保存：「← / → 採用」をクリックしてハンクを反対側に素早くコピーし、⌘ S で保存します。\n• フィルタと安全性：空白/大文字小文字の無視切り替え、確認ダイアログ付きの一括クリア、設定での「.bak バックアップ自動作成」に対応しています。",
        .excelDiffGuideDetail: "• 複数シート自動アライメント：ワークブック（.xlsx / .xls / .csv / .tsv）内の全シートを自動検出して照合。\n• 高精度差分エンジン：主キー列による Map Join または Myers LCS アルゴリズムによる行揃えと、セル内インライン差分表示。\n• ルールと詳細インスペクター：数値の許容誤差（Tolerance）、大文字/小文字や空白の無視、行詳細インスペクター（Row Detail Inspector）を搭載。",
        .wordDiffGuideDetail: "• リッチテキスト段落比較：.docx の段落テキスト、書式スタイル（太字/斜体/下線/色）および構造の変更を高精度に抽出。\n• 多次元ビジュアル差分：見出しアウトライン、埋め込みテーブル、画像ハッシュ照合によるベクター・画像差分に対応。",
        .folderDiffDesc: "ディレクトリ構造の差分比較および双方向ファイル同期ツール。",
        .folderDiffGuideDetail: "• フォルダの追加方法：Finder から左右の領域にフォルダをドラッグ＆ドロップするか、サイドバーのショートカットや最近の履歴から選択します。\n• 比較モード：「簡易比較（サイズとタイムスタンプ）」と「ハッシュ比較（CRC32チェックサム）」を選択可能です。\n• 安全な同期プレビュー：同期を実行する前に、「ドライランプレビュー」でファイル変更予定一覧を確認できます。",
        .threeWayMergeDesc: "共通祖先（Base）に基づく3方向コンフリクト解決ツール。",
        .threeWayMergeGuideDetail: "• パネル構成：上部は左から「ローカル（Local / 橙）」、「共通祖先（Base / 灰）」、「リモート（Remote / 緑）」、下部は「マージ結果（Merged）」です。\n• 手動追加とオフライン統合：Finder から3つのファイルをドラッグまたは選択して設定ファイル等を統合。「競合なしを自動解決」で片側の変更を自動適用し、競合行のみを手動選択します。\n• Git 連携（git mergetool）：Git のマージツールとして設定すると、手動でファイルを選ぶ必要がなくなります。コンフリクト時に 'git mergetool' を実行するだけで自動入力され、スムーズに解決できます。",
        .gitMergetoolConfigGuide: "Git mergetool 設定コマンド（ターミナルで実行）：",
        .tabDragMergeDesc: "ウインドウ内でのタブ並べ替え、ドラッグして独立ウインドウへ分離、他ウインドウのタブバーへドラッグしてシームレスに結合。",
        .cancelTabDragDesc: "進行中のタブドラッグ操作をキャンセル",
        .nextTab: "次のタブ",
        .prevTab: "前のタブ",
        .aboutFooter: "macOS 14.0+ 向けに Swift と SwiftUI で開発。",
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
        .createBakBackupTitle: "保存時に .bak バックアップファイルを作成する",
        .createBakBackupDesc: "上書き保存する前に元のファイルの .bak コピーを自動作成し、誤操作によるデータ損失を防ぎます。",
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
        .noFilesSelected: "マージファイルが選択されていません",
        .checkForUpdates: "アップデートを確認...",
        .checkingForUpdates: "アップデートを確認中...",
        .upToDateTitle: "最新バージョンです",
        .upToDateMessage: "MacCompare は現在最新バージョンです。",
        .newVersionAvailableTitle: "新しいバージョンがあります",
        .newVersionAvailableMessage: "MacCompare の新しいバージョンが利用可能です。",
        .downloadUpdate: "今すぐアップデートをダウンロード",
        .releaseNotes: "リリースノート",
        .noReleaseNotes: "このリリースの詳細情報はありません。",
        .currentVersion: "現在のバージョン",
        .later: "後で通知",
        .autoCheckUpdatesOnLaunch: "起動時に自動的にアップデートを確認",
        .lastChecked: "最終確認日時",
        .checkFailed: "アップデートの確認に失敗しました。ネットワーク接続を確認してください。",
        .newWordCompare: "新規 Word 比較",
        .wordDiff: "Word ドキュメント比較",
        .structuredContent: "構造化コンテンツ比較",
        .formattingDiff: "書式・スタイル比較",
        .tableDiff: "テーブル比較",
        .metadataDiff: "メタデータ比較",
        .documentOutline: "ドキュメントアウトライン",
        .noHeadingsDetected: "見出しが検出されませんでした",
        .headingsHint: "Word の見出しスタイル（H1〜H6）がここに自動的に抽出されます",
        .formatChanged: "書式変更",
        .diffOptions: "比較オプション",
        .ignoreFormatting: "書式の差異を無視",
        .exportReport: "比較レポートを出力",
        .searchOrFilter: "検索・フィルター...",
        .parsingWordDocument: "Word ドキュメントを解析中...",
        .toggleOutline: "アウトラインサイドバーの表示切替",
        .dropWordPrompt: ".docx または .doc ファイルをここにドラッグ＆ドロップ",
        .noTablesDetected: "テーブルは検出されませんでした",
        .property: "プロパティ",
        .wordCount: "文字数",
        .paragraphs: "段落数",
        .fileSize: "ファイルサイズ",
        .added: "追加",
        .deleted: "削除",
        .modified: "変更",
        .newExcelCompare: "新規 Excel 比較",
        .excelDiff: "Excel スプレッドシート比較",
        .filterAll: "すべて",
        .filterDiffs: "差異のみ",
        .filterSame: "一致のみ",
        .excelRules: "比較ルール",
        .excelRulesTitle: "比較ルールと設定",
        .excelFirstRowHeader: "1行目をヘッダーとして扱う",
        .excelNumericTolerance: "数値許容誤差",
        .excelToleranceValue: "許容誤差（例: 0.001）",
        .excelKeyColumnsHeader: "キー列（行アライメント）",
        .excelKeyColumnsDesc: "シート間の行アライメントに使用するキー列を選択:",
        .excelRowInspectorTitle: "選択行の詳細インスペクター",
        .excelDifferenceRows: "件の差異行",
        .excelSameRows: "件の一致行",
        .excelLoadTime: "所要時間",
        .excelLoadingPrompt: "Excel データを解析・比較中...",
        .selectExcelFilesPrompt: "2つの Excel (.xlsx / .xls) ファイルを選択またはドロップして比較を開始",
        .statusModified: "変更あり",
        .statusAdded: "追加行",
        .statusDeleted: "削除行",
        .copyLeft: "左側のセル値をコピー",
        .copyRight: "右側のセル値をコピー",
        .swapSides: "左右の入れ替え",
        .selectFilePrompt: "ファイルを選択...",
        .retry: "再試行",
        .welcomeTitle: "MacCompare へようこそ",
        .welcomeSubtitle: "比較モードを選択して開始",
        .welcomeTextDiffTitle: "テキスト・コード比較",
        .welcomeTextDiffDesc: "行レベル・単語レベルの詳細な差分抽出",
        .welcomeExcelDiffTitle: "Excel テーブル比較",
        .welcomeExcelDiffDesc: "複数シート自動認識、セル単位の差分ハイライト",
        .welcomeWordDiffTitle: "Word ドキュメント比較",
        .welcomeWordDiffDesc: "書式スタイル、埋め込みテーブル、図形の高精度比較",
        .welcomeFolderDiffTitle: "フォルダ比較・同期",
        .welcomeFolderDiffDesc: "高速 CRC32 スキャンと安全な双方向同期",
        .recentComparisons: "最近の比較履歴",
        .openExisting: "既存ファイルを開く...",
        .noRecentComparisons: "比較履歴はありません"
    ]
}
