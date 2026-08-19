import SwiftUI

public struct MainWindowView: View {
    @Bindable public var tabManager: TabManager
    @State private var isSettingsSheetPresented: Bool = false
    @State private var languageManager = LanguageManager.shared

    public init(tabManager: TabManager? = nil) {
        self.tabManager = tabManager ?? TabManager()
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Tab Bar
            CustomTabBarView(tabManager: tabManager)

            Divider()

            // Dynamic Active View
            if let activeTab = tabManager.activeTab {
                switch activeTab.type {
                case .textDiff:
                    TwoWayDiffView(viewModel: tabManager.textDiffViewModel(for: activeTab.id))
                case .folderDiff:
                    FolderDiffView(viewModel: tabManager.folderDiffViewModel(for: activeTab.id))
                case .threeWayMerge:
                    ThreeWayMergeView(viewModel: tabManager.threeWayMergeViewModel(for: activeTab.id))
                }
            } else {
                VStack(spacing: 12) {
                    Image(systemName: "square.stack.3d.up.slash")
                        .font(.system(size: 36))
                        .foregroundColor(.secondary)
                    Text(languageManager.text(.noFileSelected))
                        .font(.headline)
                        .foregroundColor(.secondary)
                    Button(languageManager.text(.newTextCompare)) {
                        tabManager.addTab(type: .textDiff)
                    }
                    .buttonStyle(.borderedProminent)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color(nsColor: .windowBackgroundColor))
            }
        }
        .id(languageManager.effectiveLanguage)
        .frame(minWidth: 960, minHeight: 600)
        // MARK: - Menu Command Notification Listeners
        .onReceive(NotificationCenter.default.publisher(for: .mcNewTextCompare)) { _ in
            tabManager.addTab(type: .textDiff)
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
        .onReceive(NotificationCenter.default.publisher(for: .mcMoveTabToNewWindow)) { _ in
            WindowManager.shared.moveActiveTabToNewWindow(from: tabManager)
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcMergeAllWindows)) { _ in
            WindowManager.shared.mergeAllWindows()
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcOpenFile)) { _ in
            guard let activeTab = tabManager.activeTab else { return }
            switch activeTab.type {
            case .textDiff:
                tabManager.textDiffViewModel(for: activeTab.id).openLeftFile()
            case .folderDiff:
                tabManager.folderDiffViewModel(for: activeTab.id).chooseLeftFolder()
            case .threeWayMerge:
                tabManager.threeWayMergeViewModel(for: activeTab.id).openLocalFile()
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcSaveActive)) { _ in
            guard let activeTab = tabManager.activeTab else { return }
            switch activeTab.type {
            case .textDiff:
                let vm = tabManager.textDiffViewModel(for: activeTab.id)
                if vm.isLeftDirty { vm.saveLeftFile() }
                if vm.isRightDirty { vm.saveRightFile() }
            case .folderDiff:
                break
            case .threeWayMerge:
                tabManager.threeWayMergeViewModel(for: activeTab.id).saveAndCompleteMerge()
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcNextDiff)) { _ in
            guard let activeTab = tabManager.activeTab else { return }
            switch activeTab.type {
            case .textDiff:
                tabManager.textDiffViewModel(for: activeTab.id).nextDiff()
            case .threeWayMerge:
                tabManager.threeWayMergeViewModel(for: activeTab.id).nextConflict()
            case .folderDiff:
                break
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcPrevDiff)) { _ in
            guard let activeTab = tabManager.activeTab else { return }
            switch activeTab.type {
            case .textDiff:
                tabManager.textDiffViewModel(for: activeTab.id).previousDiff()
            case .threeWayMerge:
                tabManager.threeWayMergeViewModel(for: activeTab.id).previousConflict()
            case .folderDiff:
                break
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcTakeLeft)) { _ in
            guard let activeTab = tabManager.activeTab else { return }
            if activeTab.type == .textDiff {
                tabManager.textDiffViewModel(for: activeTab.id).takeLeft()
            } else if activeTab.type == .threeWayMerge {
                tabManager.threeWayMergeViewModel(for: activeTab.id).acceptLocal()
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcTakeRight)) { _ in
            guard let activeTab = tabManager.activeTab else { return }
            if activeTab.type == .textDiff {
                tabManager.textDiffViewModel(for: activeTab.id).takeRight()
            } else if activeTab.type == .threeWayMerge {
                tabManager.threeWayMergeViewModel(for: activeTab.id).acceptRemote()
            }
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcToggleIgnoreWhitespace)) { _ in
            guard let activeTab = tabManager.activeTab, activeTab.type == .textDiff else { return }
            let vm = tabManager.textDiffViewModel(for: activeTab.id)
            vm.ignoreWhitespace.toggle()
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcToggleIgnoreCase)) { _ in
            guard let activeTab = tabManager.activeTab, activeTab.type == .textDiff else { return }
            let vm = tabManager.textDiffViewModel(for: activeTab.id)
            vm.ignoreCase.toggle()
        }
        .onReceive(NotificationCenter.default.publisher(for: .mcOpenSettings)) { _ in
            isSettingsSheetPresented = true
        }
        .sheet(isPresented: $isSettingsSheetPresented) {
            VStack(spacing: 0) {
                HStack {
                    Spacer()
                    Button(languageManager.text(.done)) {
                        isSettingsSheetPresented = false
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.small)
                    .padding(12)
                }
                SettingsView()
            }
            .frame(width: 520, height: 420)
        }
    }
}
