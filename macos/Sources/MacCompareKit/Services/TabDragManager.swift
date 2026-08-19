import Foundation
import SwiftUI
import AppKit

public enum TabDragHitResult {
    case sameWindowTabBar(manager: TabManager, window: NSWindow, insertIndex: Int)
    case otherWindowTabBar(manager: TabManager, window: NSWindow, insertIndex: Int)
    case none
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
        self.sourceWindow = NSApp.windows.first { window in
            guard let hosting = window.contentView as? NSHostingView<MainWindowView> else { return false }
            return hosting.rootView.currentTabManager.id == tabManager.id
        } ?? NSApp.keyWindow

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

        if distance > 8, let panel = floatingPanel {
            panel.setFrameOrigin(NSPoint(x: mouse.x - 70, y: mouse.y - 16))
            if !panel.isVisible {
                panel.orderFront(nil)
            }
        }

        // Perform Hit Testing
        let hit = performHitTest(mousePos: mouse)
        switch hit {
        case .sameWindowTabBar(let manager, _, let insertIndex):
            self.activeDropZoneManagerId = manager.id
            self.activeDropZoneIndex = insertIndex
        case .otherWindowTabBar(let manager, _, let insertIndex):
            self.activeDropZoneManagerId = manager.id
            self.activeDropZoneIndex = insertIndex
        case .none:
            self.activeDropZoneManagerId = nil
            self.activeDropZoneIndex = nil
        }
    }

    private func performHitTest(mousePos: NSPoint) -> TabDragHitResult {
        let visibleWindows = NSApp.windows.filter { $0.isVisible && $0.styleMask.contains(.titled) }

        for window in visibleWindows {
            // Check top tab bar region (~ 42px height at the top of the window frame)
            let barHeight: CGFloat = 42
            let barFrame = NSRect(
                x: window.frame.minX,
                y: window.frame.maxY - barHeight,
                width: window.frame.width,
                height: barHeight
            )

            if barFrame.contains(mousePos) {
                guard let hosting = window.contentView as? NSHostingView<MainWindowView> else { continue }
                let manager = hosting.rootView.currentTabManager

                let localX = max(0, mousePos.x - barFrame.minX)
                let tabWidth: CGFloat = 130
                let calculatedIndex = min(max(0, Int(round((localX - 16) / tabWidth))), manager.tabs.count)

                if let srcManager = sourceTabManager, srcManager.id == manager.id {
                    return .sameWindowTabBar(manager: manager, window: window, insertIndex: calculatedIndex)
                } else {
                    return .otherWindowTabBar(manager: manager, window: window, insertIndex: calculatedIndex)
                }
            }
        }

        return .none
    }

    private func handleMouseUp() {
        let mouse = NSEvent.mouseLocation
        let hit = performHitTest(mousePos: mouse)

        guard let tabId = draggingTabId,
              let srcManager = sourceTabManager else {
            cancelDrag()
            return
        }

        switch hit {
        case .otherWindowTabBar(let targetManager, let targetWindow, let insertIndex):
            // Cross-window merge
            targetManager.transferTab(tabId: tabId, from: srcManager, toIndex: insertIndex)
            targetWindow.makeKeyAndOrderFront(nil)
            if srcManager.tabs.isEmpty {
                sourceWindow?.close()
            }

        case .sameWindowTabBar(let manager, _, let insertIndex):
            // Same window reorder
            manager.transferTab(tabId: tabId, from: srcManager, toIndex: insertIndex)

        case .none:
            // Drag-to-Tear-Off: Create standalone window
            let dx = mouse.x - initialMouseLocation.x
            let dy = mouse.y - initialMouseLocation.y
            let distance = sqrt(dx * dx + dy * dy)

            if distance > 20 {
                if let detached = srcManager.detachTabToNewManager(id: tabId) {
                    let origin = NSPoint(x: mouse.x - 120, y: mouse.y + 20)
                    WindowManager.shared.openDetachedWindow(with: detached, at: origin)
                    if srcManager.tabs.isEmpty {
                        sourceWindow?.close()
                    }
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
