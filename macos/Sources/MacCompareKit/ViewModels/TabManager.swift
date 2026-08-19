import Foundation
import SwiftUI

public enum TabContentType: String, CaseIterable, Identifiable, Sendable {
    case textDiff = "Text Diff"
    case folderDiff = "Folder Diff"
    case threeWayMerge = "3-Way Merge"

    public var id: String { rawValue }
    public var iconName: String {
        switch self {
        case .textDiff: return "doc.text"
        case .folderDiff: return "folder"
        case .threeWayMerge: return "arrow.triangle.branch"
        }
    }
}

public struct TabItem: Identifiable, Equatable, Sendable {
    public let id: UUID
    public var title: String
    public var type: TabContentType
    public var isModified: Bool

    public init(id: UUID = UUID(), title: String, type: TabContentType, isModified: Bool = false) {
        self.id = id
        self.title = title
        self.type = type
        self.isModified = isModified
    }
}

@MainActor
@Observable
public final class TabManager {
    public var tabs: [TabItem] = [
        TabItem(title: "Compare: script.py", type: .textDiff),
        TabItem(title: "Compare: src", type: .folderDiff),
        TabItem(title: "Merge: MainView.swift", type: .threeWayMerge)
    ]
    public var selectedTabId: UUID?

    public init() {
        self.selectedTabId = tabs.first?.id
    }

    public var activeTab: TabItem? {
        tabs.first(where: { $0.id == selectedTabId })
    }

    public func selectTab(id: UUID) {
        selectedTabId = id
    }

    public func closeTab(id: UUID) {
        guard let index = tabs.firstIndex(where: { $0.id == id }) else { return }
        tabs.remove(at: index)
        if selectedTabId == id {
            selectedTabId = tabs.indices.contains(index) ? tabs[index].id : tabs.last?.id
        }
    }

    public func addTab(type: TabContentType, title: String? = nil) {
        let newTitle = title ?? "New \(type.rawValue)"
        let newTab = TabItem(title: newTitle, type: type)
        tabs.append(newTab)
        selectedTabId = newTab.id
    }
}
