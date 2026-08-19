import SwiftUI

public struct MainWindowView: View {
    @State private var tabManager = TabManager()

    public init() {}

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
                    Text("No Active Comparison")
                        .font(.headline)
                        .foregroundColor(.secondary)
                    Button("Open New Comparison") {
                        tabManager.addTab(type: .textDiff)
                    }
                    .buttonStyle(.borderedProminent)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(Color(nsColor: .windowBackgroundColor))
            }
        }
        .frame(minWidth: 960, minHeight: 600)
    }
}
