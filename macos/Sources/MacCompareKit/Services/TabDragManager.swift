import Foundation
import SwiftUI
import AppKit

public enum TabDragHitResult {
    case sameWindowTabBar(manager: TabManager, window: NSWindow, insertIndex: Int)
    case otherWindowTabBar(manager: TabManager, window: NSWindow, insertIndex: Int)
    case none
}

@MainActor
public final class TabBarRegistry {
    public static let shared = TabBarRegistry()

    private var registeredViews: [UUID: (manager: TabManager, view: NSView)] = [:]

    private init() {}

    public func register(tabManager: TabManager, view: NSView) {
        registeredViews[tabManager.id] = (tabManager, view)
    }

    public func unregister(tabManager: TabManager) {
        registeredViews.removeValue(forKey: tabManager.id)
    }

    public func findHit(mousePos: NSPoint, sourceManagerId: UUID) -> TabDragHitResult {
        for (_, entry) in registeredViews {
            guard let window = entry.view.window, window.isVisible else { continue }
            let windowRect = entry.view.convert(entry.view.bounds, to: nil)
            let screenFrame = window.convertToScreen(windowRect)

            if screenFrame.contains(mousePos) {
                let localX = max(0, mousePos.x - screenFrame.minX)
                let tabWidth: CGFloat = 130
                let index = min(max(0, Int(round((localX - 16) / tabWidth))), entry.manager.tabs.count)

                if entry.manager.id == sourceManagerId {
                    return .sameWindowTabBar(manager: entry.manager, window: window, insertIndex: index)
                } else {
                    return .otherWindowTabBar(manager: entry.manager, window: window, insertIndex: index)
                }
            }
        }
        return .none
    }
}

public struct TabBarFrameTracker: NSViewRepresentable {
    public let tabManager: TabManager

    public init(tabManager: TabManager) {
        self.tabManager = tabManager
    }

    public func makeNSView(context: Context) -> TrackerNSView {
        let view = TrackerNSView()
        view.tabManager = tabManager
        return view
    }

    public func updateNSView(_ nsView: TrackerNSView, context: Context) {
        nsView.tabManager = tabManager
        if nsView.window != nil {
            TabBarRegistry.shared.register(tabManager: tabManager, view: nsView)
        }
    }

    public final class TrackerNSView: NSView {
        var tabManager: TabManager?

        public override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            if window != nil, let manager = tabManager {
                TabBarRegistry.shared.register(tabManager: manager, view: self)
            }
        }

        public override func removeFromSuperview() {
            if let manager = tabManager {
                TabBarRegistry.shared.unregister(tabManager: manager)
            }
            super.removeFromSuperview()
        }
    }
}

@MainActor
@Observable
public final class TabDragManager {
    public static let shared = TabDragManager()

    public var activeDropZoneManagerId: UUID?
    public var activeDropZoneIndex: Int?
    public var draggingTabId: UUID?

    private var floatingPanel: NSPanel?
    private var dragEventMonitor: Any?
    private var keyEventMonitor: Any?
    private var sourceTabManager: TabManager?
    private var sourceWindow: NSWindow?
    private var initialMouseLocation: NSPoint = .zero
    private var isDragging = false

    private init() {}

    public func startDragging(tab: TabItem, from tabManager: TabManager) {
        guard !isDragging else { return }
        self.isDragging = true
        self.draggingTabId = tab.id
        self.sourceTabManager = tabManager
        self.initialMouseLocation = NSEvent.mouseLocation

        // Locate source NSWindow
        self.sourceWindow = NSApp.keyWindow ?? NSApp.windows.first(where: { $0.isKeyWindow || $0.isVisible })

        // Create floating proxy panel
        createFloatingPanel(for: tab)

        // Install Drag & Mouse Tracking Monitor
        dragEventMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDragged, .leftMouseUp, .mouseMoved]) { [weak self] event in
            guard let self = self else { return event }

            if event.type == .leftMouseDragged || event.type == .mouseMoved {
                self.handleMouseDragged()
            } else if event.type == .leftMouseUp {
                self.handleMouseUp()
            }
            return event
        }

        // Install ESC Key Monitor to cancel drag
        keyEventMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard let self = self else { return event }
            if event.keyCode == 53 { // ESC key
                self.cancelDrag()
                return nil
            }
            return event
        }
    }

    private func createFloatingPanel(for tab: TabItem) {
        let panel = NSPanel(
            contentRect: NSRect(x: 0, y: 0, width: 140, height: 32),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.level = .floating
        panel.ignoresMouseEvents = true

        let hosting = NSHostingView(rootView:
            HStack(spacing: 6) {
                Image(systemName: tab.type.iconName)
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.accentColor)
                Text(tab.displayTitle)
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundColor(.primary)
                    .lineLimit(1)
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(
                RoundedRectangle(cornerRadius: 6)
                    .fill(Color(nsColor: .windowBackgroundColor).opacity(0.92))
                    .shadow(color: Color.black.opacity(0.35), radius: 8, y: 3)
            )
            .padding(4)
        )

        panel.contentView = hosting
        let mouse = NSEvent.mouseLocation
        panel.setFrameOrigin(NSPoint(x: mouse.x - 70, y: mouse.y - 16))
        self.floatingPanel = panel
    }

    private func handleMouseDragged() {
        let mouse = NSEvent.mouseLocation
        let dx = mouse.x - initialMouseLocation.x
        let dy = mouse.y - initialMouseLocation.y
        let distance = sqrt(dx * dx + dy * dy)

        if distance > 6, let panel = floatingPanel {
            panel.setFrameOrigin(NSPoint(x: mouse.x - 70, y: mouse.y - 16))
            if !panel.isVisible {
                panel.orderFront(nil)
            }
        }

        // Hit Testing using live TabBarRegistry
        guard let srcManager = sourceTabManager else { return }
        let hit = TabBarRegistry.shared.findHit(mousePos: mouse, sourceManagerId: srcManager.id)

        switch hit {
        case .sameWindowTabBar(let manager, _, let insertIndex),
             .otherWindowTabBar(let manager, _, let insertIndex):
            self.activeDropZoneManagerId = manager.id
            self.activeDropZoneIndex = insertIndex
        case .none:
            self.activeDropZoneManagerId = nil
            self.activeDropZoneIndex = nil
        }
    }

    private func handleMouseUp() {
        let mouse = NSEvent.mouseLocation
        guard let tabId = draggingTabId,
              let srcManager = sourceTabManager else {
            cancelDrag()
            return
        }

        let hit = TabBarRegistry.shared.findHit(mousePos: mouse, sourceManagerId: srcManager.id)
        let sourceWin = self.sourceWindow

        switch hit {
        case .otherWindowTabBar(let targetManager, let targetWindow, let insertIndex):
            // Cross-window merge
            targetManager.transferTab(tabId: tabId, from: srcManager, toIndex: insertIndex)
            targetWindow.makeKeyAndOrderFront(nil)
            if srcManager.tabs.isEmpty {
                DispatchQueue.main.async {
                    sourceWin?.close()
                }
            }

        case .sameWindowTabBar(let manager, _, let insertIndex):
            // Same window reorder
            if srcManager.tabs.count > 1 {
                manager.transferTab(tabId: tabId, from: srcManager, toIndex: insertIndex)
            }

        case .none:
            let dx = mouse.x - initialMouseLocation.x
            let dy = mouse.y - initialMouseLocation.y
            let distance = sqrt(dx * dx + dy * dy)

            if distance > 20 {
                if srcManager.tabs.count > 1 {
                    // Tear-Off: Create standalone window
                    if let detached = srcManager.detachTabToNewManager(id: tabId) {
                        let origin = NSPoint(x: mouse.x - 120, y: mouse.y + 20)
                        WindowManager.shared.openDetachedWindow(with: detached, at: origin)
                    }
                } else {
                    // Only 1 tab in source window: Reposition the window smoothly to mouse drop location
                    sourceWin?.setFrameTopLeftPoint(NSPoint(x: mouse.x - 120, y: mouse.y + 20))
                }
            }
        }

        cleanup()
    }

    public func cancelDrag() {
        cleanup()
    }

    private func cleanup() {
        if let monitor = dragEventMonitor {
            NSEvent.removeMonitor(monitor)
            dragEventMonitor = nil
        }
        if let monitor = keyEventMonitor {
            NSEvent.removeMonitor(monitor)
            keyEventMonitor = nil
        }

        floatingPanel?.orderOut(nil)
        floatingPanel = nil

        draggingTabId = nil
        sourceTabManager = nil
        sourceWindow = nil
        activeDropZoneManagerId = nil
        activeDropZoneIndex = nil
        isDragging = false
    }
}
