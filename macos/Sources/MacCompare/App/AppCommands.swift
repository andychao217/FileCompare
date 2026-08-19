import SwiftUI
import AppKit

public struct AppCommands: Commands {
    public init() {}

    public var body: some Commands {
        CommandGroup(after: .newItem) {
            Button("New Text Compare") {
                // Trigger notification / action
            }
            .keyboardShortcut("n", modifiers: .command)

            Button("New Folder Compare") {
                // Trigger notification / action
            }
            .keyboardShortcut("n", modifiers: [.command, .shift])

            Button("New 3-Way Merge") {
                // Trigger notification / action
            }
            .keyboardShortcut("m", modifiers: [.command, .option])
        }

        CommandMenu("Compare") {
            Button("Next Difference") {
                // Jump next
            }
            .keyboardShortcut("]", modifiers: .command)

            Button("Previous Difference") {
                // Jump prev
            }
            .keyboardShortcut("[", modifiers: .command)

            Divider()

            Button("Take Left") {
                // Take left
            }
            .keyboardShortcut(.leftArrow, modifiers: [.command, .option])

            Button("Take Right") {
                // Take right
            }
            .keyboardShortcut(.rightArrow, modifiers: [.command, .option])
        }
    }
}
