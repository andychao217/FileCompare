import SwiftUI
import AppKit
import MacCompareKit

public struct AppCommands: Commands {
    public init() {}

    public var body: some Commands {
        CommandGroup(after: .appInfo) {
            Button(LanguageManager.shared.text(.settings)) {
                NotificationCenter.default.post(name: .mcOpenSettings, object: nil)
            }
            .keyboardShortcut(",", modifiers: .command)
        }

        CommandGroup(replacing: .newItem) {
            Button(LanguageManager.shared.text(.newTextCompare)) {
                NotificationCenter.default.post(name: .mcNewTextCompare, object: nil)
            }
            .keyboardShortcut("n", modifiers: .command)

            Button(LanguageManager.shared.text(.newFolderCompare)) {
                NotificationCenter.default.post(name: .mcNewFolderCompare, object: nil)
            }
            .keyboardShortcut("n", modifiers: [.command, .shift])

            Button(LanguageManager.shared.text(.newThreeWayMerge)) {
                NotificationCenter.default.post(name: .mcNewThreeWayMerge, object: nil)
            }
            .keyboardShortcut("m", modifiers: [.command, .option])

            Divider()

            Button(LanguageManager.shared.text(.openFile)) {
                NotificationCenter.default.post(name: .mcOpenFile, object: nil)
            }
            .keyboardShortcut("o", modifiers: .command)

            Button(LanguageManager.shared.text(.save)) {
                NotificationCenter.default.post(name: .mcSaveActive, object: nil)
            }
            .keyboardShortcut("s", modifiers: .command)

            Button(LanguageManager.shared.text(.closeTab)) {
                NotificationCenter.default.post(name: .mcCloseActiveTab, object: nil)
            }
            .keyboardShortcut("w", modifiers: .command)
        }

        CommandMenu(LanguageManager.shared.text(.compare)) {
            Button(LanguageManager.shared.text(.nextDiff)) {
                NotificationCenter.default.post(name: .mcNextDiff, object: nil)
            }
            .keyboardShortcut("]", modifiers: .command)

            Button(LanguageManager.shared.text(.prevDiff)) {
                NotificationCenter.default.post(name: .mcPrevDiff, object: nil)
            }
            .keyboardShortcut("[", modifiers: .command)

            Divider()

            Button(LanguageManager.shared.text(.takeLeft)) {
                NotificationCenter.default.post(name: .mcTakeLeft, object: nil)
            }
            .keyboardShortcut(.leftArrow, modifiers: [.command, .option])

            Button(LanguageManager.shared.text(.takeRight)) {
                NotificationCenter.default.post(name: .mcTakeRight, object: nil)
            }
            .keyboardShortcut(.rightArrow, modifiers: [.command, .option])

            Divider()

            Button(LanguageManager.shared.text(.ignoreWhitespace)) {
                NotificationCenter.default.post(name: .mcToggleIgnoreWhitespace, object: nil)
            }
            .keyboardShortcut("w", modifiers: [.command, .option])

            Button(LanguageManager.shared.text(.ignoreCase)) {
                NotificationCenter.default.post(name: .mcToggleIgnoreCase, object: nil)
            }
            .keyboardShortcut("c", modifiers: [.command, .option])
        }
    }
}
