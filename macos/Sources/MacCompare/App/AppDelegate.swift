import AppKit
import SwiftUI
import MacCompareKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // App lifecycle setup
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }

    @objc func newTextCompareAction() {
        NotificationCenter.default.post(name: .init("NewTextCompare"), object: nil)
    }

    @objc func newFolderCompareAction() {
        NotificationCenter.default.post(name: .init("NewFolderCompare"), object: nil)
    }

    @objc func newThreeWayMergeAction() {
        NotificationCenter.default.post(name: .init("NewThreeWayMerge"), object: nil)
    }

    @objc func nextDiffAction() {
        NotificationCenter.default.post(name: .init("NextDiff"), object: nil)
    }

    @objc func prevDiffAction() {
        NotificationCenter.default.post(name: .init("PrevDiff"), object: nil)
    }

    @objc func takeLeftAction() {
        NotificationCenter.default.post(name: .init("TakeLeft"), object: nil)
    }

    @objc func takeRightAction() {
        NotificationCenter.default.post(name: .init("TakeRight"), object: nil)
    }
}
