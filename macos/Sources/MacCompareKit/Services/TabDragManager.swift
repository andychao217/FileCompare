import Foundation
import SwiftUI
import AppKit

@MainActor
public final class TabDragManager {
    public static let shared = TabDragManager()

    private var floatingPanel: NSPanel?
    private var dragEventMonitor: Any?
    private var activeTabId: UUID?
    private var sourceTabManager: TabManager?
    private var initialMouseLocation: NSPoint = .zero
    private var isDragging = false

    private init() {}

    public func startDragging(tab: TabItem, from tabManager: TabManager) {
        guard !isDragging else { return }
        self.isDragging = true
        self.activeTabId = tab.id
        self.sourceTabManager = tabManager
        self.initialMouseLocation = NSEvent.mouseLocation

        // Create and position floating tab proxy
        createFloatingPanel(for: tab)

        // Install local monitor to follow mouse across entire application
        dragEventMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDragged, .leftMouseUp, .mouseMoved]) { [weak self] event in
            guard let self = self else { return event }

            if event.type == .leftMouseDragged || event.type == .mouseMoved {
                self.handleMouseDragged()
            } else if event.type == .leftMouseUp {
                self.handleMouseUp()
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
                    .fill(Color(nsColor: .windowBackgroundColor).opacity(0.95))
                    .shadow(color: Color.black.opacity(0.3), radius: 6, y: 3)
            )
            .padding(4)
        )

        panel.contentView = hosting
        let mouse = NSEvent.mouseLocation
        panel.setFrameOrigin(NSPoint(x: mouse.x - 70, y: mouse.y - 16))
        self.floatingPanel = panel
    }

    private func handleMouseDragged() {
        let currentMouse = NSEvent.mouseLocation
        let dx = currentMouse.x - initialMouseLocation.x
        let dy = currentMouse.y - initialMouseLocation.y
        let distance = sqrt(dx * dx + dy * dy)

        if distance > 10, let panel = floatingPanel {
            panel.setFrameOrigin(NSPoint(x: currentMouse.x - 70, y: currentMouse.y - 16))
            if !panel.isVisible {
                panel.orderFront(nil)
            }
        }
    }

    private func handleMouseUp() {
        if let monitor = dragEventMonitor {
            NSEvent.removeMonitor(monitor)
            dragEventMonitor = nil
        }

        floatingPanel?.orderOut(nil)
        floatingPanel = nil

        guard let tabId = activeTabId,
              let sourceManager = sourceTabManager else {
            cleanup()
            return
        }

        let mousePos = NSEvent.mouseLocation
        let dx = mousePos.x - initialMouseLocation.x
        let dy = mousePos.y - initialMouseLocation.y
        let distance = sqrt(dx * dx + dy * dy)

        if distance > 25 {
            // Find all visible standard windows under mouse
            let windowsUnderMouse = NSApp.windows.filter { window in
                window.isVisible && window.styleMask.contains(.titled) && window.frame.contains(mousePos)
            }

            var mergedIntoTarget = false
            for win in windowsUnderMouse {
                if let rootHosting = win.contentView as? NSHostingView<MainWindowView> {
                    let targetManager = rootHosting.rootView.currentTabManager
                    if targetManager.id != sourceManager.id {
                        // Merge into target window!
                        targetManager.transferTab(tabId: tabId, from: sourceManager)
                        mergedIntoTarget = true
                        break
                    }
                }
            }

            // If not merged into another window, and dragged away from source window -> Tear off to new window!
            if !mergedIntoTarget && sourceManager.tabs.count > 1 {
                let sourceWindowContainsMouse = NSApp.windows.contains { win in
                    guard let rootHosting = win.contentView as? NSHostingView<MainWindowView>,
                          rootHosting.rootView.currentTabManager.id == sourceManager.id else { return false }
                    return win.frame.contains(mousePos)
                }

                // If released outside the source window (or dragged far enough away):
                if !sourceWindowContainsMouse || distance > 100 {
                    if let detachedManager = sourceManager.detachTabToNewManager(id: tabId) {
                        let newTopLeft = NSPoint(x: mousePos.x - 100, y: mousePos.y + 20)
                        WindowManager.shared.openDetachedWindow(with: detachedManager, at: newTopLeft)
                    }
                }
            }
        }

        cleanup()
    }

    private func cleanup() {
        activeTabId = nil
        sourceTabManager = nil
        isDragging = false
    }
}
