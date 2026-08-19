import SwiftUI
import UniformTypeIdentifiers
import AppKit

public struct CustomTabBarView: View {
    @Bindable public var tabManager: TabManager
    @State private var isSettingsPresented: Bool = false
    @State private var languageManager = LanguageManager.shared
    @State private var draggingTabId: UUID?
    @State private var dragOffset: CGSize = .zero
    @State private var isDropTargeted: Bool = false

    public init(tabManager: TabManager) {
        self.tabManager = tabManager
    }

    public var body: some View {
        HStack(spacing: 4) {
            ForEach(Array(tabManager.tabs.enumerated()), id: \.element.id) { index, tab in
                let isSelected = tab.id == tabManager.selectedTabId
                let isCurrentDragging = draggingTabId == tab.id

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
                }
                .buttonStyle(.plain)
                .focusEffectDisabled()
                .offset(x: isCurrentDragging ? dragOffset.width : 0, y: isCurrentDragging ? dragOffset.height : 0)
                .opacity(isCurrentDragging && abs(dragOffset.height) > 30 ? 0.6 : 1.0)
                // 1. Interactive Drag Gesture (Smooth Chrome-style Tear-Off & Reorder)
                .simultaneousGesture(
                    DragGesture(minimumDistance: 8, coordinateSpace: .global)
                        .onChanged { value in
                            self.draggingTabId = tab.id
                            self.dragOffset = value.translation
                        }
                        .onEnded { value in
                            let offset = value.translation
                            let mousePos = NSEvent.mouseLocation

                            // Check if dragged outside the tab bar (Tear-off into new window)
                            if abs(offset.height) > 35 && tabManager.tabs.count > 1 {
                                if let detachedManager = tabManager.detachTabToNewManager(id: tab.id) {
                                    WindowManager.shared.openDetachedWindow(with: detachedManager, at: mousePos)
                                }
                            } else if abs(offset.width) > 60 {
                                // Horizontal reorder
                                let steps = Int(round(offset.width / 90.0))
                                let targetIdx = min(max(index + steps, 0), tabManager.tabs.count - 1)
                                if targetIdx != index {
                                    withAnimation(.easeInOut(duration: 0.15)) {
                                        tabManager.moveTab(from: index, to: targetIdx)
                                    }
                                }
                            }

                            self.draggingTabId = nil
                            self.dragOffset = .zero
                        }
                )
                // 2. Native Pasteboard Drag for Inter-Window Merging
                .onDrag {
                    let payload = "\(tabManager.id.uuidString):\(tab.id.uuidString)"
                    return NSItemProvider(object: payload as NSString)
                }
                // 3. Drop Destination for Cross-Window Merge
                .onDrop(of: [.text, .plainText], isTargeted: nil) { providers in
                    handleTabDrop(providers: providers, targetIndex: index)
                }
                // 4. Right-Click Context Menu
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
        .background(Color(nsColor: .windowBackgroundColor).opacity(isDropTargeted ? 0.9 : 0.6))
        // Accept Drop on entire Tab Bar area for cross-window merge
        .onDrop(of: [.text, .plainText], isTargeted: $isDropTargeted) { providers in
            handleTabDrop(providers: providers, targetIndex: tabManager.tabs.count)
        }
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

    private func handleTabDrop(providers: [NSItemProvider], targetIndex: Int) -> Bool {
        guard let provider = providers.first else { return false }
        _ = provider.loadObject(ofClass: NSString.self) { item, _ in
            guard let payload = item as? String else { return }
            let parts = payload.components(separatedBy: ":")
            guard parts.count == 2,
                  let sourceManagerId = UUID(uuidString: parts[0]),
                  let tabId = UUID(uuidString: parts[1]) else { return }

            DispatchQueue.main.async {
                if let sourceManager = TabTransferRegistry.shared.getTabManager(for: sourceManagerId) {
                    withAnimation(.easeInOut(duration: 0.2)) {
                        self.tabManager.transferTab(tabId: tabId, from: sourceManager, toIndex: targetIndex)
                    }
                }
            }
        }
        return true
    }
}
