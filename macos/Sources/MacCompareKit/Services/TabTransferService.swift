import Foundation
import SwiftUI
import AppKit
import UniformTypeIdentifiers

public extension UTType {
    static let macCompareTab = UTType(exportedAs: "com.andychao217.maccompare.tab")
}

public struct TabTransferSession: Sendable {
    public let tabId: UUID
    public let sourceManagerId: UUID

    public init(tabId: UUID, sourceManagerId: UUID) {
        self.tabId = tabId
        self.sourceManagerId = sourceManagerId
    }
}

@MainActor
public final class TabTransferRegistry {
    public static let shared = TabTransferRegistry()

    private var activeSessions: [UUID: TabManager] = [:]

    private init() {}

    public func register(tabManager: TabManager) {
        activeSessions[tabManager.id] = tabManager
    }

    public func unregister(id: UUID) {
        activeSessions.removeValue(forKey: id)
    }

    public func getTabManager(for id: UUID) -> TabManager? {
        activeSessions[id]
    }
}
