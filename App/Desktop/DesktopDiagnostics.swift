import AppKit
import SwiftUI
import WordCore

struct DiagnosticError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}

@MainActor
final class DiagnosticSpeechEngine: SpeechEngine {
    var voices = [SpeechVoice(id: "test-us", language: "en-US")]
    var spoken: [String] = []
    func stop() {}
    func speak(_ text: String, voiceID: String) { spoken.append(text) }
}

@MainActor
enum DesktopDiagnostics {
    static func pause(_ seconds: Double = 0.2) async { try? await Task.sleep(nanoseconds: UInt64(seconds * 1_000_000_000)) }
    static func require(_ condition: Bool, _ message: String) throws {
        if !condition { throw DiagnosticError(message: "Desktop check failed: " + message) }
    }
    static func capture(_ window: NSWindow, to path: URL) throws {
        guard let view = window.contentView else { throw DiagnosticError(message: "No content view") }
        view.layoutSubtreeIfNeeded()
        window.displayIfNeeded()
        guard let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { throw DiagnosticError(message: "Cannot create preview") }
        view.cacheDisplay(in: view.bounds, to: bitmap)
        guard let data = bitmap.representation(using: .png, properties: [:]) else { throw DiagnosticError(message: "Cannot encode preview") }
        try data.write(to: path)
    }
    static func post(_ type: NSEvent.EventType, panel: NSPanel, point: CGPoint, count: Int = 1) {
        if let event = NSEvent.mouseEvent(with: type, location: point, modifierFlags: [], timestamp: ProcessInfo.processInfo.systemUptime,
            windowNumber: panel.windowNumber, context: nil, eventNumber: 0, clickCount: count, pressure: type == .leftMouseUp ? 0 : 1) {
            NSApp.postEvent(event, atStart: false)
        }
    }
    static func click(_ panel: NSPanel, count: Int = 1) {
        let point = CGPoint(x: 130, y: panel.frame.height - 70)
        post(.leftMouseDown, panel: panel, point: point, count: count)
        post(.leftMouseUp, panel: panel, point: point, count: count)
    }
    static func run(app: AppDelegate, engine: DiagnosticSpeechEngine, folder: URL) async throws {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        guard let windows = app.windows, let main = app.mainWindow else { throw DiagnosticError(message: "Missing windows") }
        let study = windows.study, panel = windows.panel
        try study.selectBook("automotive")
        if let word = study.entries.first(where: { $0.word.id == "greenhouse" }) { study.jump(to: word) }
        app.showCard()
        panel.appearance = NSAppearance(named: .aqua)
        await pause(0.4)
        try require(panel.isVisible && !main.isVisible, "card visibility")
        try require(!panel.canBecomeKey && !panel.hidesOnDeactivate && panel.level == .floating, "nonactivating floating configuration")
        try require(panel.frame.size == CGSize(width:260,height:150), "collapsed card size")
        try capture(panel, to: folder.appendingPathComponent("card-light.png"))
        click(panel)
        await pause(NSEvent.doubleClickInterval + 0.3)
        try require(study.revealed, "single click reveals definition")
        try require(engine.spoken.count == 1, "single click speaks once")
        try require(panel.frame.height == 220, "expanded card height")
        try capture(panel, to: folder.appendingPathComponent("card-expanded.png"))
        panel.appearance = NSAppearance(named: .darkAqua)
        await pause()
        try capture(panel, to: folder.appendingPathComponent("card-dark.png"))
        let speechCount = engine.spoken.count
        click(panel, count:1)
        await pause(0.04)
        click(panel, count:2)
        await pause(NSEvent.doubleClickInterval + 0.3)
        try require(main.isVisible && !panel.isVisible, "double click opens library")
        try require(engine.spoken.count == speechCount, "double click does not trigger speech")
        main.appearance = NSAppearance(named: .aqua)
        await pause()
        try capture(main, to: folder.appendingPathComponent("library-light.png"))
        main.appearance = NSAppearance(named: .darkAqua)
        await pause()
        try capture(main, to: folder.appendingPathComponent("library-dark.png"))
        app.showCard()
        let oldIndex = study.index
        let y = panel.frame.height - 70
        post(.leftMouseDown,panel:panel,point:CGPoint(x:180,y:y))
        post(.leftMouseDragged,panel:panel,point:CGPoint(x:70,y:y))
        post(.leftMouseUp,panel:panel,point:CGPoint(x:70,y:y))
        await pause(0.3)
        try require(study.index == (oldIndex+1) % study.entries.count && !study.revealed, "left swipe advances and hides definition")
        try study.selectBook("materials")
        if let longest = study.entries.max(by: { $0.word.spelling.count < $1.word.spelling.count }) { study.jump(to: longest) }
        study.revealed = true
        panel.appearance = NSAppearance(named: .aqua)
        await pause()
        try capture(panel,to:folder.appendingPathComponent("card-long-term.png"))
        app.showLibrary()
        main.setContentSize(NSSize(width:800,height:580))
        main.appearance = NSAppearance(named: .aqua)
        await pause()
        try capture(main,to:folder.appendingPathComponent("library-compact.png"))
        _ = app.windowShouldClose(main)
        try require(panel.isVisible && !main.isVisible, "closing main window returns to card")
        let report: [String:Any] = ["status":"passed", "books":study.books.count,
            "entries":study.books.reduce(0) { $0+$1.count },
            "checks":["native-window-visibility","nonactivating-panel","single-click","double-click-cancellation","mouse-swipe","close-to-card","light-and-dark-previews"],
            "installedEnglishVoices":SystemSpeechEngine().voices.filter { $0.language.hasPrefix("en") }.map(\.language),
            "notVerified":["physical-trackpad", "physical-audio-output", "full-screen-other-apps", "physical-multiple-monitors"]]
        try JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]).write(to:folder.appendingPathComponent("desktop-checks.json"))
        print("DESKTOP_CHECKS_PASSED")
    }
}
