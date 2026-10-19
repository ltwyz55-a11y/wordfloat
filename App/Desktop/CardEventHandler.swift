import AppKit
import WordCore

@MainActor
final class CardEventHandler {
    private weak var panel: NSPanel?
    private let study: StudySession
    private let audio: Pronunciation
    private let openLibrary: () -> Void
    private var monitor: Any?
    private var pendingClick: DispatchWorkItem?
    private var pointer = CardPointerGesture()
    private var scrolling = HorizontalScrollGesture()
    private var tracking = false

    init(panel: NSPanel, study: StudySession, audio: Pronunciation, openLibrary: @escaping () -> Void) {
        self.panel = panel; self.study = study; self.audio = audio; self.openLibrary = openLibrary
        monitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .leftMouseDragged, .leftMouseUp, .scrollWheel]) { [weak self] event in
            guard let self else { return event }
            return self.handle(event)
        }
    }
    deinit { if let monitor { NSEvent.removeMonitor(monitor) }; pendingClick?.cancel() }
    func cancel() { pendingClick?.cancel(); pendingClick = nil; pointer.cancel(); tracking = false }
    private func handle(_ event: NSEvent) -> NSEvent? {
        guard let panel, event.window === panel else { return event }
        let point = event.locationInWindow
        let content = point.y >= 36 && point.y < panel.frame.height - 24
        switch event.type {
        case .leftMouseDown:
            cancel()
            if let root = panel.contentView {
                var hit = root.hitTest(root.convert(point, from: nil))
                while let view = hit {
                    if view is NSControl { return event }
                    hit = view.superview
                }
            }
            tracking = content
            if content { pointer.begin(at: point); return nil }
        case .leftMouseDragged:
            if tracking { pointer.move(to: point); return nil }
        case .leftMouseUp:
            if tracking {
                tracking = false
                perform(pointer.end(at: point, clickCount: event.clickCount))
                return nil
            }
        case .scrollWheel:
            guard content else { return event }
            cancel()
            let horizontal = abs(event.scrollingDeltaX) > abs(event.scrollingDeltaY)
            if let delta = scrolling.consume(dx: event.scrollingDeltaX, dy: event.scrollingDeltaY,
                time: event.timestamp, begins: event.phase.contains(.began), momentum: !event.momentumPhase.isEmpty,
                precise: event.hasPreciseScrollingDeltas) {
                cancel(); study.advance(delta)
            }
            if horizontal { return nil }
        default: break
        }
        return event
    }
    private func perform(_ action: CardPointerAction) {
        switch action {
        case .none: break
        case .next: study.advance(1)
        case .previous: study.advance(-1)
        case .openLibrary: cancel(); audio.stop(); openLibrary()
        case .scheduleSingle:
            let click = DispatchWorkItem { [weak self] in
                guard let self, self.study.current != nil else { return }
                self.study.revealed.toggle()
            }
            pendingClick = click
            DispatchQueue.main.asyncAfter(deadline: .now() + NSEvent.doubleClickInterval, execute: click)
        }
    }
}
