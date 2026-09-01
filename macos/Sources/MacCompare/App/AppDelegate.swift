import AppKit
import SwiftUI
import MacCompareKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(windowDidBecomeKey(_:)),
            name: NSWindow.didBecomeKeyNotification,
            object: nil
        )

        // Automatic background update check on startup
        if UpdateCheckerService.shared.isAutoCheckEnabled {
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) {
                UpdateCheckerService.shared.checkForUpdates(isUserInitiated: false)
            }
        }
    }

    @objc private func windowDidBecomeKey(_ notification: Notification) {
        guard let window = notification.object as? NSWindow else { return }
        window.tabbingMode = .disallowed
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }

    // MARK: - Dock Context Menu

    func applicationDockMenu(_ sender: NSApplication) -> NSMenu? {
        let menu = NSMenu()
        let languageManager = LanguageManager.shared

        let textItem = NSMenuItem(
            title: languageManager.text(.newTextCompare),
            action: #selector(newTextCompareFromDock),
            keyEquivalent: ""
        )
        textItem.target = self
        menu.addItem(textItem)

        let excelItem = NSMenuItem(
            title: languageManager.text(.newExcelCompare),
            action: #selector(newExcelCompareFromDock),
            keyEquivalent: ""
        )
        excelItem.target = self
        menu.addItem(excelItem)

        let wordItem = NSMenuItem(
            title: languageManager.text(.newWordCompare),
            action: #selector(newWordCompareFromDock),
            keyEquivalent: ""
        )
        wordItem.target = self
        menu.addItem(wordItem)

        let folderItem = NSMenuItem(
            title: languageManager.text(.newFolderCompare),
            action: #selector(newFolderCompareFromDock),
            keyEquivalent: ""
        )
        folderItem.target = self
        menu.addItem(folderItem)

        let threeWayItem = NSMenuItem(
            title: languageManager.text(.newThreeWayMerge),
            action: #selector(newThreeWayMergeFromDock),
            keyEquivalent: ""
        )
        threeWayItem.target = self
        menu.addItem(threeWayItem)

        return menu
    }

    @objc private func newTextCompareFromDock() {
        openOrAddTab(type: .textDiff)
    }

    @objc private func newExcelCompareFromDock() {
        openOrAddTab(type: .excelDiff)
    }

    @objc private func newWordCompareFromDock() {
        openOrAddTab(type: .wordDiff)
    }

    @objc private func newFolderCompareFromDock() {
        openOrAddTab(type: .folderDiff)
    }

    @objc private func newThreeWayMergeFromDock() {
        openOrAddTab(type: .threeWayMerge)
    }

    private func openOrAddTab(type: TabContentType) {
        NSApp.activate(ignoringOtherApps: true)
        let registered = WindowTabRegistry.shared.getAllRegistered()
        if let keyEntry = registered.first(where: { $0.window.isKeyWindow }) ?? registered.first {
            keyEntry.manager.addTab(type: type)
            keyEntry.window.makeKeyAndOrderFront(nil)
        } else {
            let newManager = TabManager(initialTabType: type)
            WindowManager.shared.openDetachedWindow(with: newManager)
        }
    }
}
