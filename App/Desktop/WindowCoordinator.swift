import AppKit
import SwiftUI
import Combine
import WordCore

final class FloatingPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

@MainActor
final class WindowCoordinator: NSObject, NSWindowDelegate {
    let study: StudySession
    let audio: Pronunciation
    let panel: FloatingPanel
    private let defaults: UserDefaults
    private var subscriptions: Set<AnyCancellable> = []
    private var screenObserver: NSObjectProtocol?
    private var events: CardEventHandler?
    var openLibrary: (() -> Void)?

    init(study: StudySession, audio: Pronunciation, defaults: UserDefaults = .standard) {
        self.study = study; self.audio = audio; self.defaults = defaults
        panel = FloatingPanel(contentRect: NSRect(x: 0, y: 0, width: 260, height: 150),
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
        panel.contentView = NSHostingView(rootView: CardView(study: study, audio: audio, onOpenLibrary: { [weak self] in self?.openLibrary?() }))
        let screen = NSScreen.main?.visibleFrame ?? CGRect(x: 0, y: 0, width: 1440, height: 900)
        let saved = defaults.array(forKey: "panel.origin") as? [Double]
        let origin = saved?.count == 2 ? CGPoint(x: saved![0], y: saved![1]) : CGPoint(x: screen.maxX - 292, y: screen.maxY - 190)
        panel.setFrameOrigin(origin)
        restoreVisibility()
        study.$revealed.removeDuplicates().sink { [weak self] revealed in
            guard let self else { return }
            let height: CGFloat = revealed ? 220 : 150
            let previous = self.panel.frame
            let next = CGRect(x: previous.minX, y: previous.maxY - height, width: 260, height: height)
            self.panel.setFrame(WindowPlacement.clamp(next, to: self.screenFrames), display: true)
        }.store(in: &subscriptions)
        screenObserver = NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification, object: nil, queue: .main) { [weak self] _ in
            Task { @MainActor in self?.restoreVisibility() }
        }
        events = CardEventHandler(panel: panel, study: study, audio: audio, openLibrary: { [weak self] in self?.openLibrary?() })
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
    func windowDidMove(_ notification: Notification) {
        let point = panel.frame.origin
        defaults.set([point.x, point.y], forKey: "panel.origin")
    }
}
