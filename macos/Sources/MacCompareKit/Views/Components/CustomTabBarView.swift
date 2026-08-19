import SwiftUI

public struct CustomTabBarView: View {
    @Bindable public var tabManager: TabManager
    @State private var isSettingsPresented: Bool = false
    @State private var languageManager = LanguageManager.shared

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

            // New Tab Menu
            Menu {
                Button(languageManager.text(.newTextCompare)) {
                    tabManager.addTab(type: .textDiff, title: languageManager.text(.newTextCompare))
                }
                Button(languageManager.text(.newFolderCompare)) {
                    tabManager.addTab(type: .folderDiff, title: languageManager.text(.newFolderCompare))
                }
                Button(languageManager.text(.newThreeWayMerge)) {
                    tabManager.addTab(type: .threeWayMerge, title: languageManager.text(.newThreeWayMerge))
                }
            } label: {
                Image(systemName: "plus")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.secondary)
                    .padding(6)
            }
            .menuStyle(.borderlessButton)

            // Settings Button
            Button {
                isSettingsPresented = true
            } label: {
                Image(systemName: "gearshape")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.secondary)
                    .padding(6)
            }
            .buttonStyle(.plain)
            .help(languageManager.text(.settings))
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .background(Color(nsColor: .windowBackgroundColor).opacity(0.6))
        .sheet(isPresented: $isSettingsPresented) {
            VStack(spacing: 0) {
                HStack {
                    Spacer()
                    Button(languageManager.text(.done)) {
                        isSettingsPresented = false
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
