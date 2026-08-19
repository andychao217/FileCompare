import Foundation
import SwiftUI
import AppKit

@MainActor
public final class WindowManager {
    public static let shared = WindowManager()

    public let defaultTabbingIdentifier = "com.andychao217.MacCompare.tabGroup"

    private init() {}

    public func openDetachedWindow(with tabManager: TabManager, at screenPoint: NSPoint? = nil) {
        let hostingView = NSHostingView(rootView: MainWindowView(tabManager: tabManager))
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 1080, height: 720),
            styleMask: [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )

        window.tabbingIdentifier = defaultTabbingIdentifier
        window.tabbingMode = .disallowed
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.contentView = hostingView
        window.isReleasedWhenClosed = false

        if let point = screenPoint {
            window.setFrameTopLeftPoint(point)
        } else {
            window.center()
        }

        WindowTabRegistry.shared.register(manager: tabManager, window: window)
        window.makeKeyAndOrderFront(nil)
    }

    public func mergeAllWindows() {
        let allEntries = WindowTabRegistry.shared.getAllRegistered()
        guard allEntries.count > 1 else { return }

        // Find key window or first window as destination
        guard let primaryEntry = allEntries.first(where: { $0.window.isKeyWindow }) ?? allEntries.first else { return }
        let targetManager = primaryEntry.manager
        let targetWindow = primaryEntry.window

        let otherEntries = allEntries.filter { $0.manager.id != targetManager.id }

        for entry in otherEntries {
            let srcManager = entry.manager
            let srcWindow = entry.window

            // Move all tabs from secondary window into destination window
            let tabsToMove = srcManager.tabs
            for tab in tabsToMove {
                targetManager.transferTab(tabId: tab.id, from: srcManager)
            }

            // Close the secondary window safely
            DispatchQueue.main.async {
                srcWindow.close()
                WindowTabRegistry.shared.unregister(managerId: srcManager.id)
            }
        }

        targetWindow.makeKeyAndOrderFront(nil)
    }

    public func moveActiveTabToNewWindow(from tabManager: TabManager? = nil) {
        let mgr: TabManager
        if let tabManager {
            mgr = tabManager
        } else if let keyEntry = WindowTabRegistry.shared.getAllRegistered().first(where: { $0.window.isKeyWindow }) {
            mgr = keyEntry.manager
        } else {
            return
        }

        guard let activeId = mgr.selectedTabId, mgr.tabs.count > 1 else { return }
        guard let detachedManager = mgr.detachTabToNewManager(id: activeId) else { return }
        openDetachedWindow(with: detachedManager)
    }
}
