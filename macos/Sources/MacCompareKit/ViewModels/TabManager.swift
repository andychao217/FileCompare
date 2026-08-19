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
    public var customTitle: String?
    public var type: TabContentType
    public var isModified: Bool

    public init(id: UUID = UUID(), title: String? = nil, type: TabContentType, isModified: Bool = false) {
        self.id = id
        self.customTitle = title
        self.type = type
        self.isModified = isModified
    }

    @MainActor
    public var displayTitle: String {
        if let custom = customTitle, !custom.isEmpty {
            return custom
        }
        switch type {
        case .textDiff: return LanguageManager.shared.text(.newTextCompare)
        case .folderDiff: return LanguageManager.shared.text(.newFolderCompare)
        case .threeWayMerge: return LanguageManager.shared.text(.newThreeWayMerge)
        }
    }
}

@MainActor
@Observable
public final class TabManager {
    public var tabs: [TabItem] = []
    public var selectedTabId: UUID?

    public var textDiffViewModels: [UUID: TextDiffViewModel] = [:]
    public var folderDiffViewModels: [UUID: FolderDiffViewModel] = [:]
    public var threeWayMergeViewModels: [UUID: ThreeWayMergeViewModel] = [:]

    public init() {
        let initialTab = TabItem(type: .textDiff)
        tabs.append(initialTab)
        selectedTabId = initialTab.id
        textDiffViewModels[initialTab.id] = TextDiffViewModel()
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
        textDiffViewModels.removeValue(forKey: id)
        folderDiffViewModels.removeValue(forKey: id)
        threeWayMergeViewModels.removeValue(forKey: id)

        if selectedTabId == id {
            selectedTabId = tabs.indices.contains(index) ? tabs[index].id : tabs.last?.id
        }
    }

    public func addTab(type: TabContentType, title: String? = nil) {
        let newTab = TabItem(title: title, type: type)
        tabs.append(newTab)
        selectedTabId = newTab.id

        switch type {
        case .textDiff:
            textDiffViewModels[newTab.id] = TextDiffViewModel()
        case .folderDiff:
            let vm = FolderDiffViewModel()
            vm.onOpenFileDiff = { [weak self] left, right in
                self?.openTextDiff(left: left, right: right)
            }
            folderDiffViewModels[newTab.id] = vm
        case .threeWayMerge:
            threeWayMergeViewModels[newTab.id] = ThreeWayMergeViewModel()
        }
    }

    public func openTextDiff(left: URL, right: URL) {
        let title = "\(left.lastPathComponent) ↔ \(right.lastPathComponent)"
        let newTab = TabItem(title: title, type: .textDiff)
        tabs.append(newTab)
        selectedTabId = newTab.id

        let vm = TextDiffViewModel(leftURL: left, rightURL: right)
        textDiffViewModels[newTab.id] = vm
    }

    public func openFolderDiff(left: URL, right: URL) {
        let title = "\(left.lastPathComponent) ↔ \(right.lastPathComponent)"
        let newTab = TabItem(title: title, type: .folderDiff)
        tabs.append(newTab)
        selectedTabId = newTab.id

        let vm = FolderDiffViewModel(leftURL: left, rightURL: right)
        vm.onOpenFileDiff = { [weak self] l, r in
            self?.openTextDiff(left: l, right: r)
        }
        folderDiffViewModels[newTab.id] = vm
    }

    public func openThreeWayMerge(local: URL, base: URL, remote: URL, output: URL? = nil) {
        let title = "Merge: \(local.lastPathComponent)"
        let newTab = TabItem(title: title, type: .threeWayMerge)
        tabs.append(newTab)
        selectedTabId = newTab.id

        let vm = ThreeWayMergeViewModel(localURL: local, baseURL: base, remoteURL: remote, outputURL: output)
        threeWayMergeViewModels[newTab.id] = vm
    }

    public func textDiffViewModel(for tabId: UUID) -> TextDiffViewModel {
        if let vm = textDiffViewModels[tabId] {
            return vm
        }
        let vm = TextDiffViewModel()
        textDiffViewModels[tabId] = vm
        return vm
    }

    public func folderDiffViewModel(for tabId: UUID) -> FolderDiffViewModel {
        if let vm = folderDiffViewModels[tabId] {
            return vm
        }
        let vm = FolderDiffViewModel()
        vm.onOpenFileDiff = { [weak self] l, r in
            self?.openTextDiff(left: l, right: r)
        }
        folderDiffViewModels[tabId] = vm
        return vm
    }

    public func threeWayMergeViewModel(for tabId: UUID) -> ThreeWayMergeViewModel {
        if let vm = threeWayMergeViewModels[tabId] {
            return vm
        }
        let vm = ThreeWayMergeViewModel()
        threeWayMergeViewModels[tabId] = vm
        return vm
    }
}
