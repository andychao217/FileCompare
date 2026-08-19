import Foundation
import SwiftUI

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

public enum L10nKey: String, Sendable {
    // Menu & App
    case settings
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
    case compare
    case nextDiff
    case prevDiff
    case takeLeft
    case takeRight
    case ignoreWhitespace
    case ignoreCase

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
    case cancel
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

    private init() {
        let saved = UserDefaults.standard.string(forKey: "mc_app_language") ?? AppLanguage.system.rawValue
        self.currentLanguage = AppLanguage(rawValue: saved) ?? .system
        updateEffectiveLanguage()
    }

    private func updateEffectiveLanguage() {
        if currentLanguage == .system {
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

    // MARK: - Dictionaries

    private let enDictionary: [L10nKey: String] = [
        .settings: "Settings...",
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
        .compare: "Compare",
        .nextDiff: "Next Difference",
        .prevDiff: "Previous Difference",
        .takeLeft: "Take Left",
        .takeRight: "Take Right",
        .ignoreWhitespace: "Ignore Whitespace",
        .ignoreCase: "Ignore Case",
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
        .cancel: "Cancel",
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
        .settings: "设置...",
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
        .compare: "比对",
        .nextDiff: "下一个差异",
        .prevDiff: "上一个差异",
        .takeLeft: "采纳左侧",
        .takeRight: "采纳右侧",
        .ignoreWhitespace: "忽略空白符",
        .ignoreCase: "忽略大小写",
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
        .cancel: "取消",
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
        .settings: "設定...",
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
        .compare: "比較",
        .nextDiff: "次の差分",
        .prevDiff: "前の差分",
        .takeLeft: "左側を採用",
        .takeRight: "右側を採用",
        .ignoreWhitespace: "空白を無視",
        .ignoreCase: "大文字/小文字を無視",
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
        .cancel: "キャンセル",
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
