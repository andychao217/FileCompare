import SwiftUI
#if os(macOS)
import AppKit

/// A high-performance single-line code editor that intercepts Enter (newline insertion)
/// and Backspace (backward line deletion and merging) with accurate cursor tracking.
public struct CodeLineTextField: NSViewRepresentable {
    @Binding var text: String
    var onCommit: () -> Void
    var onInsertNewline: (Int) -> Void
    var onDeleteBackwardAtStart: () -> Void
    var onCancel: () -> Void

    public init(
        text: Binding<String>,
        onCommit: @escaping () -> Void,
        onInsertNewline: @escaping (Int) -> Void,
        onDeleteBackwardAtStart: @escaping () -> Void,
        onCancel: @escaping () -> Void
    ) {
        self._text = text
        self.onCommit = onCommit
        self.onInsertNewline = onInsertNewline
        self.onDeleteBackwardAtStart = onDeleteBackwardAtStart
        self.onCancel = onCancel
    }

    public func makeCoordinator() -> Coordinator {
        Coordinator(self)
    }

    public func makeNSView(context: Context) -> NSTextField {
        let textField = CustomNSTextField()
        textField.delegate = context.coordinator
        textField.isBordered = false
        textField.drawsBackground = false
        textField.focusRingType = .none
        textField.font = NSFont.monospacedSystemFont(ofSize: 12, weight: .regular)
        textField.textColor = .textColor
        textField.stringValue = text
        textField.lineBreakMode = .byClipping
        textField.maximumNumberOfLines = 1
        textField.cell?.wraps = false
        textField.cell?.isScrollable = true

        DispatchQueue.main.async {
            textField.window?.makeFirstResponder(textField)
        }
        return textField
    }

    public func updateNSView(_ nsView: NSTextField, context: Context) {
        if nsView.stringValue != text {
            nsView.stringValue = text
        }
    }

    public class Coordinator: NSObject, NSTextFieldDelegate {
        var parent: CodeLineTextField

        init(_ parent: CodeLineTextField) {
            self.parent = parent
        }

        public func controlTextDidChange(_ obj: Notification) {
            if let textField = obj.object as? NSTextField {
                parent.text = textField.stringValue
            }
        }

        public func control(_ control: NSControl, textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
            if commandSelector == #selector(NSResponder.insertNewline(_:)) {
                let range = textView.selectedRange()
                let offset = range.location == NSNotFound ? textView.string.count : range.location
                parent.onInsertNewline(offset)
                return true
            } else if commandSelector == #selector(NSResponder.deleteBackward(_:)) {
                let range = textView.selectedRange()
                if range.location == 0 && range.length == 0 {
                    parent.onDeleteBackwardAtStart()
                    return true
                }
            } else if commandSelector == #selector(NSResponder.cancelOperation(_:)) {
                parent.onCancel()
                return true
            }
            return false
        }
    }

    public class CustomNSTextField: NSTextField {}
}
#endif
