import SwiftUI
import AppKit
import MacCompareKit

public struct AppCommands: Commands {
    public init() {}

    public var body: some Commands {
        CommandGroup(replacing: .newItem) {
            Button("New Text Compare") {
                NotificationCenter.default.post(name: .mcNewTextCompare, object: nil)
            }
            .keyboardShortcut("n", modifiers: .command)

            Button("New Folder Compare") {
                NotificationCenter.default.post(name: .mcNewFolderCompare, object: nil)
            }
            .keyboardShortcut("n", modifiers: [.command, .shift])

            Button("New 3-Way Merge") {
                NotificationCenter.default.post(name: .mcNewThreeWayMerge, object: nil)
            }
            .keyboardShortcut("m", modifiers: [.command, .option])

            Divider()

            Button("Open File...") {
                NotificationCenter.default.post(name: .mcOpenFile, object: nil)
            }
            .keyboardShortcut("o", modifiers: .command)

            Button("Save") {
                NotificationCenter.default.post(name: .mcSaveActive, object: nil)
            }
            .keyboardShortcut("s", modifiers: .command)

            Button("Close Tab") {
                NotificationCenter.default.post(name: .mcCloseActiveTab, object: nil)
            }
            .keyboardShortcut("w", modifiers: .command)
        }

        CommandMenu("Compare") {
            Button("Next Difference") {
                NotificationCenter.default.post(name: .mcNextDiff, object: nil)
            }
            .keyboardShortcut("]", modifiers: .command)

            Button("Previous Difference") {
                NotificationCenter.default.post(name: .mcPrevDiff, object: nil)
            }
            .keyboardShortcut("[", modifiers: .command)

            Divider()

            Button("Take Left") {
                NotificationCenter.default.post(name: .mcTakeLeft, object: nil)
            }
            .keyboardShortcut(.leftArrow, modifiers: [.command, .option])

            Button("Take Right") {
                NotificationCenter.default.post(name: .mcTakeRight, object: nil)
            }
            .keyboardShortcut(.rightArrow, modifiers: [.command, .option])

            Divider()

            Button("Toggle Ignore Whitespace") {
                NotificationCenter.default.post(name: .mcToggleIgnoreWhitespace, object: nil)
            }
            .keyboardShortcut("w", modifiers: [.command, .option])

            Button("Toggle Ignore Case") {
                NotificationCenter.default.post(name: .mcToggleIgnoreCase, object: nil)
            }
            .keyboardShortcut("c", modifiers: [.command, .option])
        }
    }
}
