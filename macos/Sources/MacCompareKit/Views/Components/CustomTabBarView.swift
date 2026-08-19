import SwiftUI

public struct CustomTabBarView: View {
    @Bindable public var tabManager: TabManager

    public init(tabManager: TabManager) {
        self.tabManager = tabManager
    }

    public var body: some View {
        HStack(spacing: 4) {
            ForEach(tabManager.tabs) { tab in
                let isSelected = tab.id == tabManager.selectedTabId
                Button {
                    tabManager.selectTab(id: tab.id)
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: tab.type.iconName)
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(isSelected ? .accentColor : .secondary)

                        Text(tab.title)
                            .font(.system(size: 12, weight: isSelected ? .semibold : .regular))
                            .foregroundColor(isSelected ? .primary : .secondary)
                            .lineLimit(1)

                        if isSelected {
                            Button {
                                tabManager.closeTab(id: tab.id)
                            } label: {
                                Image(systemName: "xmark")
                                    .font(.system(size: 9, weight: .bold))
                                    .foregroundColor(.secondary)
                            }
                            .buttonStyle(.plain)
                            .padding(.leading, 2)
                        }
                    }
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(
                        RoundedRectangle(cornerRadius: 6)
                            .fill(isSelected ? Color(nsColor: .controlBackgroundColor) : Color.clear)
                            .shadow(color: isSelected ? Color.black.opacity(0.12) : Color.clear, radius: 2, y: 1)
                    )
                }
                .buttonStyle(.plain)
            }

            Spacer()

            Menu {
                Button("New Text Compare") {
                    tabManager.addTab(type: .textDiff)
                }
                Button("New Folder Compare") {
                    tabManager.addTab(type: .folderDiff)
                }
                Button("New 3-Way Merge") {
                    tabManager.addTab(type: .threeWayMerge)
                }
            } label: {
                Image(systemName: "plus")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.secondary)
                    .padding(6)
            }
            .menuStyle(.borderlessButton)
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.6))
    }
}
