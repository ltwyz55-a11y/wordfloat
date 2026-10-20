import AppKit
import SwiftUI
import Combine
import WordCore

final class FloatingPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

private final class CardHostingView: NSHostingView<CardView> {
    override func resetCursorRects() {
        super.resetCursorRects()
        let w = bounds.width, h = bounds.height
        for rect in [NSRect(x:0,y:14,width:6,height:max(0,h-28)),NSRect(x:w-6,y:14,width:6,height:max(0,h-28))] {
            addCursorRect(rect, cursor: .resizeLeftRight)
        }
        for rect in [NSRect(x:14,y:0,width:max(0,w-28),height:6),NSRect(x:14,y:h-6,width:max(0,w-28),height:6)] {
            addCursorRect(rect, cursor: .resizeUpDown)
        }
        func diagonal(_ symbol: String) -> NSCursor {
            guard let image = NSImage(systemSymbolName:symbol, accessibilityDescription:"缩放卡片") else { return .crosshair }
            image.size = NSSize(width:18,height:18)
            return NSCursor(image:image,hotSpot:NSPoint(x:9,y:9))
        }
        let first = diagonal("arrow.up.left.and.arrow.down.right"), second = diagonal("arrow.up.right.and.arrow.down.left")
        addCursorRect(NSRect(x:0,y:0,width:14,height:14),cursor:isFlipped ? first : second)
        addCursorRect(NSRect(x:w-14,y:h-14,width:14,height:14),cursor:isFlipped ? first : second)
        addCursorRect(NSRect(x:w-14,y:0,width:14,height:14),cursor:isFlipped ? second : first)
        addCursorRect(NSRect(x:0,y:h-14,width:14,height:14),cursor:isFlipped ? second : first)
    }
    override func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        window?.invalidateCursorRects(for:self)
    }
}

@MainActor
final class WindowCoordinator: NSObject, NSWindowDelegate {
    let study: StudySession
    let audio: Pronunciation
    let appearance: CardAppearance
    let panel: FloatingPanel
    private let defaults: UserDefaults
    private var subscriptions: Set<AnyCancellable> = []
    private var screenObserver: NSObjectProtocol?
    private var events: CardEventHandler?
    private var sizing: CardSizing
    var openLibrary: (() -> Void)?

    init(study: StudySession, audio: Pronunciation, appearance: CardAppearance, defaults: UserDefaults = .standard) {
        self.study = study; self.audio = audio; self.appearance = appearance; self.defaults = defaults
        sizing = CardSizing(saved: defaults.array(forKey:"panel.size") as? [Double])
        panel = FloatingPanel(contentRect: NSRect(origin:.zero,size:sizing.size(revealed:study.revealed)),
            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        super.init()
        panel.title = "浮词 · 单词卡片"
        panel.level = .floating
        panel.isFloatingPanel = true
        panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .ignoresCycle]
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.isReleasedWhenClosed = false
        panel.delegate = self
        panel.minSize = CardSizing.minimum(revealed:study.revealed)
        panel.maxSize = CardSizing.maximum
        let hostingView = CardHostingView(rootView: CardView(study: study, audio: audio, appearance: appearance, onOpenLibrary: { [weak self] in self?.openLibrary?() }))
        // The coordinator owns the panel size; do not derive window constraints from SwiftUI.
        hostingView.sizingOptions = []
        panel.contentView = hostingView
        let screen = NSScreen.main?.visibleFrame ?? CGRect(x: 0, y: 0, width: 1440, height: 900)
        let saved = defaults.array(forKey: "panel.origin") as? [Double]
        let origin = saved?.count == 2 ? CGPoint(x: saved![0], y: saved![1]) : CGPoint(x: screen.maxX - 292, y: screen.maxY - 190)
        panel.setFrameOrigin(origin)
        restoreVisibility()
        // @Published emits before the property changes. Resize on the next main-queue
        // turn so the hosting view sees the new state instead of restoring the old height.
        study.$revealed.removeDuplicates().receive(on: DispatchQueue.main).sink { [weak self] revealed in
            guard let self else { return }
            let size = self.sizing.size(revealed:revealed)
            let previous = self.panel.frame
            self.panel.minSize = CardSizing.minimum(revealed:revealed)
            let next = CGRect(x: previous.minX, y: previous.maxY - size.height, width: size.width, height: size.height)
            self.panel.setFrame(WindowPlacement.clamp(next, to: self.screenFrames), display: true)
        }.store(in: &subscriptions)
        screenObserver = NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.restoreVisibility() }
        }
        events = CardEventHandler(panel: panel, study: study, audio: audio,
            resize: { [weak self] frame in self?.resizeCard(to:frame) },
            openLibrary: { [weak self] in self?.openLibrary?() })
        study.willChangeWord = { [weak self] in self?.events?.cancel(); self?.audio.stop() }
    }
    deinit { if let screenObserver { NotificationCenter.default.removeObserver(screenObserver) } }
    private var screenFrames: [CGRect] {
        let main = NSScreen.main
        return ([main].compactMap { $0 } + NSScreen.screens.filter { $0 != main }).map(\.visibleFrame)
    }
    func restoreVisibility() { panel.setFrame(WindowPlacement.clamp(panel.frame, to: screenFrames), display: true) }
    func showCard() { restoreVisibility(); panel.orderFrontRegardless() }
    func hideCard() { events?.cancel(); panel.orderOut(nil); audio.stop() }
    private func resizeCard(to frame: CGRect) {
        sizing.remember(frame.size,revealed:study.revealed)
        defaults.set(sizing.saved,forKey:"panel.size")
        panel.setFrame(WindowPlacement.clamp(frame,to:screenFrames),display:true)
    }
    func windowDidMove(_ notification: Notification) {
        let point = panel.frame.origin
        defaults.set([point.x, point.y], forKey: "panel.origin")
    }
}
