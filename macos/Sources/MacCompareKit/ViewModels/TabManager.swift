import Foundation
import SwiftUI

public enum TabContentType: String, CaseIterable, Identifiable, Sendable {
    case textDiff = "Text Diff"
    case wordDiff = "Word Diff"
    case folderDiff = "Folder Diff"
    case threeWayMerge = "3-Way Merge"

    public var id: String { rawValue }
    public var iconName: String {
        switch self {
        case .textDiff: return "doc.text"
        case .wordDiff: return "doc.richtext"
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
        case .wordDiff: return LanguageManager.shared.text(.newWordCompare)
        case .folderDiff: return LanguageManager.shared.text(.newFolderCompare)
        case .threeWayMerge: return LanguageManager.shared.text(.newThreeWayMerge)
        }
    }
}

@MainActor
@Observable
public final class TabManager: Identifiable {
    public let id = UUID()
    public var tabs: [TabItem] = []
    public var selectedTabId: UUID?

    public var textDiffViewModels: [UUID: TextDiffViewModel] = [:]
    public var wordDiffViewModels: [UUID: WordDiffViewModel] = [:]
    public var folderDiffViewModels: [UUID: FolderDiffViewModel] = [:]
    public var threeWayMergeViewModels: [UUID: ThreeWayMergeViewModel] = [:]

    public init(createInitialTab: Bool = true) {
        TabTransferRegistry.shared.register(tabManager: self)
        if createInitialTab {
            let initialTab = TabItem(type: .textDiff)
            tabs.append(initialTab)
            selectedTabId = initialTab.id
            textDiffViewModels[initialTab.id] = TextDiffViewModel()
        }
    }

    public init(initialTabType: TabContentType) {
        TabTransferRegistry.shared.register(tabManager: self)
        let initialTab = TabItem(type: initialTabType)
        tabs.append(initialTab)
        selectedTabId = initialTab.id
        switch initialTabType {
        case .textDiff:
            textDiffViewModels[initialTab.id] = TextDiffViewModel()
        case .wordDiff:
            wordDiffViewModels[initialTab.id] = WordDiffViewModel()
        case .folderDiff:
            folderDiffViewModels[initialTab.id] = FolderDiffViewModel()
        case .threeWayMerge:
            threeWayMergeViewModels[initialTab.id] = ThreeWayMergeViewModel()
        }
    }

    deinit {
        let managerId = id
        Task { @MainActor in
            TabTransferRegistry.shared.unregister(id: managerId)
        }
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
        wordDiffViewModels.removeValue(forKey: id)
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
        case .wordDiff:
            wordDiffViewModels[newTab.id] = WordDiffViewModel()
        case .folderDiff:
            let vm = FolderDiffViewModel()
            vm.onOpenFileDiff = { [weak self] left, right in
                self?.openAutoDiff(left: left, right: right)
            }
            folderDiffViewModels[newTab.id] = vm
        case .threeWayMerge:
            threeWayMergeViewModels[newTab.id] = ThreeWayMergeViewModel()
        }
    }

    public func moveTab(from sourceIndex: Int, to destinationIndex: Int) {
        guard tabs.indices.contains(sourceIndex), tabs.indices.contains(destinationIndex), sourceIndex != destinationIndex else { return }
        let tab = tabs.remove(at: sourceIndex)
        tabs.insert(tab, at: destinationIndex)
    }

    public func detachTabToNewManager(id: UUID) -> TabManager? {
        guard let index = tabs.firstIndex(where: { $0.id == id }) else { return nil }
        let tab = tabs.remove(at: index)
        let textVM = textDiffViewModels.removeValue(forKey: id)
        let wordVM = wordDiffViewModels.removeValue(forKey: id)
        let folderVM = folderDiffViewModels.removeValue(forKey: id)
        let mergeVM = threeWayMergeViewModels.removeValue(forKey: id)

        if selectedTabId == id {
            selectedTabId = tabs.indices.contains(index) ? tabs[index].id : tabs.last?.id
        }

        let newManager = TabManager(createInitialTab: false)
        newManager.tabs.append(tab)
        newManager.selectedTabId = tab.id

        if let textVM { newManager.textDiffViewModels[tab.id] = textVM }
        if let wordVM { newManager.wordDiffViewModels[tab.id] = wordVM }
        if let folderVM { newManager.folderDiffViewModels[tab.id] = folderVM }
        if let mergeVM { newManager.threeWayMergeViewModels[tab.id] = mergeVM }

        return newManager
    }

    public func transferTab(tabId: UUID, from sourceManager: TabManager, toIndex: Int? = nil) {
        if sourceManager.id == self.id {
            // Reorder within same manager
            guard let srcIdx = sourceManager.tabs.firstIndex(where: { $0.id == tabId }) else { return }
            let dstIdx = toIndex ?? (tabs.count - 1)
            moveTab(from: srcIdx, to: dstIdx)
            return
        }

        guard let srcIdx = sourceManager.tabs.firstIndex(where: { $0.id == tabId }) else { return }
        let tab = sourceManager.tabs.remove(at: srcIdx)
        let textVM = sourceManager.textDiffViewModels.removeValue(forKey: tabId)
        let wordVM = sourceManager.wordDiffViewModels.removeValue(forKey: tabId)
        let folderVM = sourceManager.folderDiffViewModels.removeValue(forKey: tabId)
        let mergeVM = sourceManager.threeWayMergeViewModels.removeValue(forKey: tabId)

        if sourceManager.selectedTabId == tabId {
            sourceManager.selectedTabId = sourceManager.tabs.indices.contains(srcIdx) ? sourceManager.tabs[srcIdx].id : sourceManager.tabs.last?.id
        }

        let insertIdx = min(toIndex ?? tabs.count, tabs.count)
        tabs.insert(tab, at: insertIdx)
        selectedTabId = tab.id

        if let textVM { textDiffViewModels[tab.id] = textVM }
        if let wordVM { wordDiffViewModels[tab.id] = wordVM }
        if let folderVM { folderDiffViewModels[tab.id] = folderVM }
        if let mergeVM { threeWayMergeViewModels[tab.id] = mergeVM }
    }

    public func openAutoDiff(left: URL, right: URL) {
        let leftExt = left.pathExtension.lowercased()
        let rightExt = right.pathExtension.lowercased()
        if ["docx", "doc"].contains(leftExt) || ["docx", "doc"].contains(rightExt) {
            openWordDiff(left: left, right: right)
        } else {
            openTextDiff(left: left, right: right)
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

    public func openWordDiff(left: URL, right: URL) {
        let title = "\(left.lastPathComponent) ↔ \(right.lastPathComponent)"
        let newTab = TabItem(title: title, type: .wordDiff)
        tabs.append(newTab)
        selectedTabId = newTab.id

        let vm = WordDiffViewModel(leftURL: left, rightURL: right)
        wordDiffViewModels[newTab.id] = vm
    }

    public func openFolderDiff(left: URL, right: URL) {
        let title = "\(left.lastPathComponent) ↔ \(right.lastPathComponent)"
        let newTab = TabItem(title: title, type: .folderDiff)
        tabs.append(newTab)
        selectedTabId = newTab.id

        let vm = FolderDiffViewModel(leftURL: left, rightURL: right)
        vm.onOpenFileDiff = { [weak self] l, r in
            self?.openAutoDiff(left: l, right: r)
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

    public func wordDiffViewModel(for tabId: UUID) -> WordDiffViewModel {
        if let vm = wordDiffViewModels[tabId] {
            return vm
        }
        let vm = WordDiffViewModel()
        wordDiffViewModels[tabId] = vm
        return vm
    }

    public func folderDiffViewModel(for tabId: UUID) -> FolderDiffViewModel {
        if let vm = folderDiffViewModels[tabId] {
            return vm
        }
        let vm = FolderDiffViewModel()
        vm.onOpenFileDiff = { [weak self] l, r in
            self?.openAutoDiff(left: l, right: r)
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
