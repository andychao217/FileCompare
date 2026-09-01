import SwiftUI

public struct MainWindowView: View {
    @State private var tabManager: TabManager
    @State private var isSettingsSheetPresented: Bool = false
    @State private var isHelpSheetPresented: Bool = false
    @State private var languageManager = LanguageManager.shared
    @State private var themeManager = ThemeManager.shared
    @State private var updateChecker = UpdateCheckerService.shared

    public var currentTabManager: TabManager { tabManager }

    public init(tabManager: TabManager? = nil) {
        _tabManager = State(initialValue: tabManager ?? TabManager())
    }

    public var body: some View {
        mainContent
            .id("main-\(themeManager.themeRevision)-\(languageManager.effectiveLanguage.rawValue)")
            .preferredColorScheme(themeManager.effectiveColorScheme)
            .frame(minWidth: 960, minHeight: 600)
            .background(
                WindowAccessor { window in
                    WindowTabRegistry.shared.register(manager: tabManager, window: window)
                }
            )
            .modifier(MainWindowSheetsModifier(
                isSettingsPresented: $isSettingsSheetPresented,
                isHelpPresented: $isHelpSheetPresented,
                updateChecker: updateChecker,
                languageManager: languageManager
            ))
            .modifier(TabCreationCommands(tabManager: tabManager))
            .modifier(DiffActionCommands(tabManager: tabManager))
            .modifier(WindowAndSheetCommands(
                isSettingsPresented: $isSettingsSheetPresented,
                isHelpPresented: $isHelpSheetPresented,
                updateChecker: updateChecker
            ))
    }

    @ViewBuilder
    private var mainContent: some View {
        VStack(spacing: 0) {
            if !tabManager.tabs.isEmpty {
                CustomTabBarView(tabManager: tabManager)
                Divider()
            }

            if let activeTab = tabManager.activeTab {
                switch activeTab.type {
                case .textDiff:
                    TwoWayDiffView(viewModel: tabManager.textDiffViewModel(for: activeTab.id))
                case .wordDiff:
                    WordDiffView(viewModel: tabManager.wordDiffViewModel(for: activeTab.id))
                case .excelDiff:
                    ExcelDiffView(viewModel: tabManager.excelDiffViewModel(for: activeTab.id))
                case .folderDiff:
                    FolderDiffView(viewModel: tabManager.folderDiffViewModel(for: activeTab.id))
                case .threeWayMerge:
                    ThreeWayMergeView(viewModel: tabManager.threeWayMergeViewModel(for: activeTab.id))
                }
            } else {
                WelcomeHomeView(tabManager: tabManager)
            }
        }
    }
}

// MARK: - Sheets & Alerts Modifier

private struct MainWindowSheetsModifier: ViewModifier {
    @Binding var isSettingsPresented: Bool
    @Binding var isHelpPresented: Bool
    var updateChecker: UpdateCheckerService
    var languageManager: LanguageManager

    func body(content: Content) -> some View {
        content
            .sheet(isPresented: $isSettingsPresented) {
                SettingsView {
                    isSettingsPresented = false
                }
            }
            .sheet(isPresented: $isHelpPresented) {
                HelpView {
                    isHelpPresented = false
                }
            }
            .sheet(isPresented: Binding(
                get: { updateChecker.showUpdateSheet },
                set: { updateChecker.showUpdateSheet = $0 }
            )) {
                UpdateAvailableSheetView {
                    updateChecker.showUpdateSheet = false
                }
            }
            .alert(languageManager.text(.upToDateTitle), isPresented: Binding(
                get: { updateChecker.showUpToDateAlert },
                set: { updateChecker.showUpToDateAlert = $0 }
            )) {
                Button(languageManager.text(.done), role: .cancel) {}
            } message: {
                Text(languageManager.text(.upToDateMessage))
            }
    }
}

// MARK: - Tab Creation Commands

private struct TabCreationCommands: ViewModifier {
    var tabManager: TabManager

    func body(content: Content) -> some View {
        content
            .onReceive(NotificationCenter.default.publisher(for: .mcNewTextCompare)) { _ in
                tabManager.addTab(type: .textDiff)
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcNewExcelCompare)) { _ in
                tabManager.addTab(type: .excelDiff)
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcNewWordCompare)) { _ in
                tabManager.addTab(type: .wordDiff)
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcNewFolderCompare)) { _ in
                tabManager.addTab(type: .folderDiff)
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcNewThreeWayMerge)) { _ in
                tabManager.addTab(type: .threeWayMerge)
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcCloseActiveTab)) { _ in
                if let activeId = tabManager.selectedTabId {
                    tabManager.closeTab(id: activeId)
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcNextTab)) { _ in
                tabManager.selectNextTab()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcPrevTab)) { _ in
                tabManager.selectPreviousTab()
            }
    }
}

// MARK: - Diff & Merge Action Commands

private struct DiffActionCommands: ViewModifier {
    var tabManager: TabManager

    func body(content: Content) -> some View {
        content
            .onReceive(NotificationCenter.default.publisher(for: .mcOpenFile)) { _ in
                handleOpenFile()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcSaveActive)) { _ in
                handleSaveActive()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcNextDiff)) { _ in
                handleNextDiff()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcPrevDiff)) { _ in
                handlePrevDiff()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcTakeLeft)) { _ in
                handleTakeLeft()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcTakeRight)) { _ in
                handleTakeRight()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcToggleIgnoreWhitespace)) { _ in
                guard let activeTab = tabManager.activeTab else { return }
                if activeTab.type == .textDiff {
                    tabManager.textDiffViewModel(for: activeTab.id).ignoreWhitespace.toggle()
                } else if activeTab.type == .wordDiff {
                    tabManager.wordDiffViewModel(for: activeTab.id).ignoreWhitespace.toggle()
                }
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcToggleIgnoreCase)) { _ in
                guard let activeTab = tabManager.activeTab, activeTab.type == .textDiff else { return }
                tabManager.textDiffViewModel(for: activeTab.id).ignoreCase.toggle()
            }
    }

    private func handleOpenFile() {
        guard let activeTab = tabManager.activeTab else { return }
        switch activeTab.type {
        case .textDiff:
            tabManager.textDiffViewModel(for: activeTab.id).openLeftFile()
        case .wordDiff:
            break
        case .excelDiff:
            break
        case .folderDiff:
            tabManager.folderDiffViewModel(for: activeTab.id).chooseLeftFolder()
        case .threeWayMerge:
            tabManager.threeWayMergeViewModel(for: activeTab.id).openLocalFile()
        }
    }

    private func handleSaveActive() {
        guard let activeTab = tabManager.activeTab else { return }
        switch activeTab.type {
        case .textDiff:
            let vm = tabManager.textDiffViewModel(for: activeTab.id)
            if vm.isLeftDirty { vm.saveLeftFile() }
            if vm.isRightDirty { vm.saveRightFile() }
        case .wordDiff:
            break
        case .excelDiff:
            break
        case .folderDiff:
            break
        case .threeWayMerge:
            tabManager.threeWayMergeViewModel(for: activeTab.id).saveAndCompleteMerge()
        }
    }

    private func handleNextDiff() {
        guard let activeTab = tabManager.activeTab else { return }
        switch activeTab.type {
        case .textDiff:
            tabManager.textDiffViewModel(for: activeTab.id).nextDiff()
        case .wordDiff:
            tabManager.wordDiffViewModel(for: activeTab.id).nextDiff()
        case .excelDiff:
            tabManager.excelDiffViewModel(for: activeTab.id).nextDiff()
        case .threeWayMerge:
            tabManager.threeWayMergeViewModel(for: activeTab.id).nextConflict()
        case .folderDiff:
            break
        }
    }

    private func handlePrevDiff() {
        guard let activeTab = tabManager.activeTab else { return }
        switch activeTab.type {
        case .textDiff:
            tabManager.textDiffViewModel(for: activeTab.id).previousDiff()
        case .wordDiff:
            tabManager.wordDiffViewModel(for: activeTab.id).prevDiff()
        case .excelDiff:
            tabManager.excelDiffViewModel(for: activeTab.id).prevDiff()
        case .threeWayMerge:
            tabManager.threeWayMergeViewModel(for: activeTab.id).previousConflict()
        case .folderDiff:
            break
        }
    }

    private func handleTakeLeft() {
        guard let activeTab = tabManager.activeTab else { return }
        if activeTab.type == .textDiff {
            tabManager.textDiffViewModel(for: activeTab.id).takeLeft()
        } else if activeTab.type == .threeWayMerge {
            tabManager.threeWayMergeViewModel(for: activeTab.id).acceptLocal()
        }
    }

    private func handleTakeRight() {
        guard let activeTab = tabManager.activeTab else { return }
        if activeTab.type == .textDiff {
            tabManager.textDiffViewModel(for: activeTab.id).takeRight()
        } else if activeTab.type == .threeWayMerge {
            tabManager.threeWayMergeViewModel(for: activeTab.id).acceptRemote()
        }
    }
}

// MARK: - Window & Help/Update Commands

private struct WindowAndSheetCommands: ViewModifier {
    @Binding var isSettingsPresented: Bool
    @Binding var isHelpPresented: Bool
    var updateChecker: UpdateCheckerService

    func body(content: Content) -> some View {
        content
            .onReceive(NotificationCenter.default.publisher(for: .mcMoveTabToNewWindow)) { _ in
                WindowManager.shared.moveActiveTabToNewWindow()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcMergeAllWindows)) { _ in
                WindowManager.shared.mergeAllWindows()
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcOpenSettings)) { _ in
                isSettingsPresented = true
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcOpenHelp)) { _ in
                isHelpPresented = true
            }
            .onReceive(NotificationCenter.default.publisher(for: .mcCheckForUpdates)) { _ in
                updateChecker.checkForUpdates(isUserInitiated: true)
            }
    }
}
