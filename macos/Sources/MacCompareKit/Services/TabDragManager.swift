import Foundation
import SwiftUI
import AppKit

public enum TabDragHitResult {
    case sameWindowTabBar(manager: TabManager, window: NSWindow, insertIndex: Int)
    case otherWindowTabBar(manager: TabManager, window: NSWindow, insertIndex: Int)
    case none
}

@MainActor
public final class WindowTabRegistry {
    public static let shared = WindowTabRegistry()

    private var registeredManagers: [UUID: TabManager] = [:]
    private var windowBindings: [UUID: NSWindow] = [:]

    private init() {}

    public func register(manager: TabManager, window: NSWindow) {
        registeredManagers[manager.id] = manager
        windowBindings[manager.id] = window
    }

    public func unregister(managerId: UUID) {
        registeredManagers.removeValue(forKey: managerId)
        windowBindings.removeValue(forKey: managerId)
    }

    public func getManager(for id: UUID) -> TabManager? {
        registeredManagers[id]
    }

    public func getWindow(for managerId: UUID) -> NSWindow? {
        windowBindings[managerId]
    }

    public func findHit(mousePos: NSPoint, sourceManagerId: UUID) -> TabDragHitResult {
        // Find visible titled windows
        for (mgrId, window) in windowBindings {
            guard window.isVisible, !window.isMiniaturized else { continue }
            guard let manager = registeredManagers[mgrId] else { continue }

            // Top Tab Bar frame in macOS screen coordinates
            let barHeight: CGFloat = 40
            let barRect = NSRect(
                x: window.frame.minX,
                y: window.frame.maxY - barHeight,
                width: window.frame.width,
                height: barHeight
            )

            if barRect.contains(mousePos) {
                let localX = max(0, mousePos.x - barRect.minX)
                let tabWidth: CGFloat = 130
                let calculatedIndex = min(max(0, Int(round((localX - 16) / tabWidth))), manager.tabs.count)

                if mgrId == sourceManagerId {
                    return .sameWindowTabBar(manager: manager, window: window, insertIndex: calculatedIndex)
                } else {
                    return .otherWindowTabBar(manager: manager, window: window, insertIndex: calculatedIndex)
                }
            }
        }

        return .none
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

        // If only 1 tab in the entire app (1 window with 1 tab), dragging cannot tear off or merge
        let allManagers = NSApp.windows.filter { $0.isVisible && $0.styleMask.contains(.titled) }
        if tabManager.tabs.count <= 1 && allManagers.count <= 1 {
            return
        }

        self.isDragging = true
        self.draggingTabId = tab.id
        self.sourceTabManager = tabManager
        self.initialMouseLocation = NSEvent.mouseLocation
        self.sourceWindow = WindowTabRegistry.shared.getWindow(for: tabManager.id) ?? NSApp.keyWindow

        createFloatingPanel(for: tab)

        dragEventMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDragged, .leftMouseUp, .mouseMoved]) { [weak self] event in
            guard let self = self else { return event }

            if event.type == .leftMouseDragged || event.type == .mouseMoved {
                self.handleMouseDragged()
            } else if event.type == .leftMouseUp {
                self.handleMouseUp()
            }
            return event
        }

        keyEventMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard let self = self else { return event }
            if event.keyCode == 53 { // ESC
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

        guard let srcManager = sourceTabManager else { return }
        let hit = WindowTabRegistry.shared.findHit(mousePos: mouse, sourceManagerId: srcManager.id)

        switch hit {
        case .sameWindowTabBar(let manager, _, let insertIndex):
            if manager.tabs.count > 1 {
                self.activeDropZoneManagerId = manager.id
                self.activeDropZoneIndex = insertIndex
            } else {
                self.activeDropZoneManagerId = nil
                self.activeDropZoneIndex = nil
            }
        case .otherWindowTabBar(let manager, _, let insertIndex):
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

        let hit = WindowTabRegistry.shared.findHit(mousePos: mouse, sourceManagerId: srcManager.id)
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

            // Tear-Off: ONLY if source window has more than 1 tab
            if distance > 25 && srcManager.tabs.count > 1 {
                if let detached = srcManager.detachTabToNewManager(id: tabId) {
                    let origin = NSPoint(x: mouse.x - 120, y: mouse.y + 20)
                    WindowManager.shared.openDetachedWindow(with: detached, at: origin)
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
