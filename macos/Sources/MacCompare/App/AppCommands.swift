import SwiftUI
import AppKit

public struct AppCommands: Commands {
    public init() {}

    public var body: some Commands {
        CommandGroup(after: .newItem) {
            Button("New Text Compare") {
                NSApp.sendAction(#selector(AppDelegate.newTextCompareAction), to: nil, from: nil)
            }
            .keyboardShortcut("n", modifiers: .command)

            Button("New Folder Compare") {
                NSApp.sendAction(#selector(AppDelegate.newFolderCompareAction), to: nil, from: nil)
            }
            .keyboardShortcut("n", modifiers: [.command, .shift])

            Button("New 3-Way Merge") {
                NSApp.sendAction(#selector(AppDelegate.newThreeWayMergeAction), to: nil, from: nil)
            }
            .keyboardShortcut("m", modifiers: [.command, .option])
        }

        CommandMenu("Compare") {
            Button("Next Difference") {
                NSApp.sendAction(#selector(AppDelegate.nextDiffAction), to: nil, from: nil)
            }
            .keyboardShortcut("]", modifiers: .command)

            Button("Previous Difference") {
                NSApp.sendAction(#selector(AppDelegate.prevDiffAction), to: nil, from: nil)
            }
            .keyboardShortcut("[", modifiers: .command)

            Divider()

            Button("Take Left") {
                NSApp.sendAction(#selector(AppDelegate.takeLeftAction), to: nil, from: nil)
            }
            .keyboardShortcut(.leftArrow, modifiers: [.command, .option])

            Button("Take Right") {
                NSApp.sendAction(#selector(AppDelegate.takeRightAction), to: nil, from: nil)
            }
            .keyboardShortcut(.rightArrow, modifiers: [.command, .option])
        }
    }
}
