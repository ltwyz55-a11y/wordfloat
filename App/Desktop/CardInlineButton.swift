import AppKit
import SwiftUI

// A native control lets the card's click monitor distinguish buttons from text.
struct CardInlineButton: NSViewRepresentable {
    var title: String
    var identifier: String
    var color: Color
    var action: () -> Void

    final class Button: NSButton {
        override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
        override var acceptsFirstResponder: Bool { false }
    }
    final class Coordinator: NSObject {
        var action: () -> Void
        init(_ action: @escaping () -> Void) { self.action = action }
        @objc func invoke() { action() }
    }
    func makeCoordinator() -> Coordinator { Coordinator(action) }
    func makeNSView(context: Context) -> Button {
        let button = Button(title: title, target: context.coordinator, action: #selector(Coordinator.invoke))
        button.isBordered = false
        button.font = .systemFont(ofSize: 11, weight: .medium)
        button.setContentHuggingPriority(.required, for: .horizontal)
        return button
    }
    func updateNSView(_ button: Button, context: Context) {
        context.coordinator.action = action
        button.title = title
        button.identifier = NSUserInterfaceItemIdentifier(identifier)
        button.contentTintColor = NSColor(color)
        button.setAccessibilityLabel(title)
        button.toolTip = title
    }
}
