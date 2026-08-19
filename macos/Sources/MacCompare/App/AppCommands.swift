import SwiftUI
import AppKit
import MacCompareKit

public struct AppCommands: Commands {
    private var languageManager: LanguageManager { LanguageManager.shared }

    public init() {}

    public var body: some Commands {
        CommandGroup(replacing: .newItem) {
            Button(languageManager.text(.newTextCompare)) {
                NotificationCenter.default.post(name: .mcNewTextCompare, object: nil)
            }
            .keyboardShortcut("n", modifiers: .command)

            Button(languageManager.text(.newFolderCompare)) {
                NotificationCenter.default.post(name: .mcNewFolderCompare, object: nil)
            }
            .keyboardShortcut("n", modifiers: [.command, .shift])

            Button(languageManager.text(.newThreeWayMerge)) {
                NotificationCenter.default.post(name: .mcNewThreeWayMerge, object: nil)
            }
            .keyboardShortcut("m", modifiers: [.command, .option])

            Divider()

            Button(languageManager.text(.openFile)) {
                NotificationCenter.default.post(name: .mcOpenFile, object: nil)
            }
            .keyboardShortcut("o", modifiers: .command)

            Button(languageManager.text(.save)) {
                NotificationCenter.default.post(name: .mcSaveActive, object: nil)
            }
            .keyboardShortcut("s", modifiers: .command)

            Button(languageManager.text(.closeTab)) {
                NotificationCenter.default.post(name: .mcCloseActiveTab, object: nil)
            }
            .keyboardShortcut("w", modifiers: .command)
        }

        CommandGroup(replacing: .saveItem) {
            // Handled in newItem group
        }

        CommandGroup(after: .windowList) {
            Button(languageManager.text(.moveTabToNewWindow)) {
                WindowManager.shared.moveActiveTabToNewWindow()
            }

            Button(languageManager.text(.mergeAllWindows)) {
                WindowManager.shared.mergeAllWindows()
            }
        }

        CommandMenu(languageManager.text(.compare)) {
            Button(languageManager.text(.nextDiff)) {
                NotificationCenter.default.post(name: .mcNextDiff, object: nil)
            }
            .keyboardShortcut("]", modifiers: .command)

            Button(languageManager.text(.prevDiff)) {
                NotificationCenter.default.post(name: .mcPrevDiff, object: nil)
            }
            .keyboardShortcut("[", modifiers: .command)

            Divider()

            Button(languageManager.text(.takeLeft)) {
                NotificationCenter.default.post(name: .mcTakeLeft, object: nil)
            }
            .keyboardShortcut(.leftArrow, modifiers: [.command, .option])

            Button(languageManager.text(.takeRight)) {
                NotificationCenter.default.post(name: .mcTakeRight, object: nil)
            }
            .keyboardShortcut(.rightArrow, modifiers: [.command, .option])

            Divider()

            Button(languageManager.text(.ignoreWhitespace)) {
                NotificationCenter.default.post(name: .mcToggleIgnoreWhitespace, object: nil)
            }
            .keyboardShortcut("w", modifiers: [.command, .option])

            Button(languageManager.text(.ignoreCase)) {
                NotificationCenter.default.post(name: .mcToggleIgnoreCase, object: nil)
            }
            .keyboardShortcut("c", modifiers: [.command, .option])
        }
    }
}
