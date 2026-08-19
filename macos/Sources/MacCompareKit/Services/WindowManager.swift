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
        window.tabbingMode = .preferred
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
        if let keyWindow = NSApp.keyWindow {
            keyWindow.mergeAllWindows(nil)
        } else if let firstWindow = NSApp.windows.first(where: { $0.isVisible }) {
            firstWindow.mergeAllWindows(nil)
        }
    }

    public func moveActiveTabToNewWindow(from tabManager: TabManager) {
        guard let activeId = tabManager.selectedTabId, tabManager.tabs.count > 1 else { return }
        guard let detachedManager = tabManager.detachTabToNewManager(id: activeId) else { return }
        openDetachedWindow(with: detachedManager)
    }
}
