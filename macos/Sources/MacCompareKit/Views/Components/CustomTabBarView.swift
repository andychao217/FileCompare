import SwiftUI
import UniformTypeIdentifiers
import AppKit

public struct CustomTabBarView: View {
    @Bindable public var tabManager: TabManager
    @State private var isSettingsPresented: Bool = false
    @State private var languageManager = LanguageManager.shared
    @State private var dragManager = TabDragManager.shared

    public init(tabManager: TabManager) {
        self.tabManager = tabManager
    }

    public var body: some View {
        HStack(spacing: 2) {
            ForEach(Array(tabManager.tabs.enumerated()), id: \.element.id) { index, tab in
                let isSelected = tab.id == tabManager.selectedTabId
                let isBeingDragged = dragManager.draggingTabId == tab.id
                let showInsertMarkerBefore = dragManager.activeDropZoneManagerId == tabManager.id && dragManager.activeDropZoneIndex == index

                // Insertion Placeholder Line
                if showInsertMarkerBefore {
                    RoundedRectangle(cornerRadius: 1.5)
                        .fill(Color.accentColor)
                        .frame(width: 3, height: 22)
                        .padding(.horizontal, 1)
                        .transition(.scale)
                }

                Button {
                    tabManager.selectTab(id: tab.id)
                } label: {
                    HStack(spacing: 6) {
                        Image(systemName: tab.type.iconName)
                            .font(.system(size: 11, weight: .medium))
                            .foregroundColor(isSelected ? .accentColor : .secondary)

                        Text(tab.displayTitle)
                            .font(.system(size: 12, weight: isSelected ? .semibold : .regular))
                            .foregroundColor(isSelected ? .primary : .secondary)
                            .lineLimit(1)

                        if isSelected && tabManager.tabs.count > 1 {
                            Button {
                                tabManager.closeTab(id: tab.id)
                            } label: {
                                Image(systemName: "xmark")
                                    .font(.system(size: 9, weight: .bold))
                                    .foregroundColor(.secondary)
                            }
                            .buttonStyle(.plain)
                            .focusEffectDisabled()
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
                    .opacity(isBeingDragged ? 0.35 : 1.0)
                }
                .buttonStyle(.plain)
                .focusEffectDisabled()
                // Chrome-style Tab Drag Start
                .simultaneousGesture(
                    DragGesture(minimumDistance: 4, coordinateSpace: .global)
                        .onChanged { _ in
                            dragManager.startDragging(tab: tab, from: tabManager)
                        }
                )
                // Context Menu on Tab
                .contextMenu {
                    if tabManager.tabs.count > 1 {
                        Button {
                            guard let detachedManager = tabManager.detachTabToNewManager(id: tab.id) else { return }
                            WindowManager.shared.openDetachedWindow(with: detachedManager)
                        } label: {
                            Label(languageManager.text(.moveTabToNewWindow), systemImage: "macwindow.badge.plus")
                        }

                        Divider()
                    }

                    Button {
                        tabManager.closeTab(id: tab.id)
                    } label: {
                        Label(languageManager.text(.closeTab), systemImage: "xmark")
                    }
                }
            }

            // Insertion Marker at the tail end
            if dragManager.activeDropZoneManagerId == tabManager.id && dragManager.activeDropZoneIndex == tabManager.tabs.count {
                RoundedRectangle(cornerRadius: 1.5)
                    .fill(Color.accentColor)
                    .frame(width: 3, height: 22)
                    .padding(.horizontal, 1)
                    .transition(.scale)
            }

            Spacer()

            // New Tab Menu
            Menu {
                Button(languageManager.text(.newTextCompare)) {
                    tabManager.addTab(type: .textDiff)
                }
                Button(languageManager.text(.newFolderCompare)) {
                    tabManager.addTab(type: .folderDiff)
                }
                Button(languageManager.text(.newThreeWayMerge)) {
                    tabManager.addTab(type: .threeWayMerge)
                }
            } label: {
                Image(systemName: "plus")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.secondary)
                    .padding(6)
            }
            .menuStyle(.borderlessButton)
            .focusEffectDisabled()

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
            .focusEffectDisabled()
            .help(languageManager.text(.settings))
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .background(
            ZStack {
                Color(nsColor: .windowBackgroundColor).opacity(0.6)
                TabBarFrameTracker(tabManager: tabManager)
            }
        )
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
