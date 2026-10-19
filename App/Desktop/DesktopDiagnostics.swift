import AppKit
import SwiftUI
import WordCore

struct DiagnosticError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}

@MainActor
final class DiagnosticSpeechEngine: SpeechEngine {
    var voices = [SpeechVoice(id: "test-us", language: "en-US"), SpeechVoice(id: "test-zh", language: "zh-CN")]
    var spoken: [String] = []
    var stops = 0
    func stop() { stops += 1 }
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
        // Click directly on the Chinese definition, then assert the native frame shrinks.
        let meaningPoint = CGPoint(x: 130, y: 110)
        let spokenBeforeCollapse = engine.spoken.count
        post(.leftMouseDown, panel: panel, point: meaningPoint)
        post(.leftMouseUp, panel: panel, point: meaningPoint)
        await pause(NSEvent.doubleClickInterval + 0.3)
        try require(!study.revealed, "definition click hides meaning")
        try require(panel.frame.size == CGSize(width: 260, height: 150), "definition click restores minimum card size (frame: \(panel.frame), minimum: \(panel.minSize), content minimum: \(panel.contentMinSize), content: \(String(describing: panel.contentView?.frame)))")
        try require(engine.spoken.count == spokenBeforeCollapse, "collapse does not repeat speech")
        try capture(panel, to: folder.appendingPathComponent("card-collapsed-again.png"))
        click(panel)
        await pause(NSEvent.doubleClickInterval + 0.3)
        try require(study.revealed && panel.frame.height == 220, "card expands again after collapse")
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
        app.showCard()
        try require(panel.isVisible && !main.isVisible, "floating study action returns to card")
        try await checkLearning(windows: windows, engine: engine, folder: folder)
        try await checkAppearance(app: app, folder: folder)
        let report: [String:Any] = ["status":"passed", "books":study.books.count,
            "entries":study.books.reduce(0) { $0+$1.count },
            "checks":["native-window-visibility","nonactivating-panel","single-click","definition-click-collapse","repeated-expand-collapse","double-click-cancellation","mouse-swipe","return-to-card","close-quits-and-hides-all-windows","light-and-dark-previews","six-appearance-presets","live-card-recolor","managed-wallpaper-import","wallpaper-collapse","appearance-settings-light-dark-compact","scrollable-learning-details","inline-English-Chinese-pair-buttons","scroll-reset-on-word-change","example-speech-stops-on-word-change","collapse-after-scrolling"],
            "installedEnglishVoices":SystemSpeechEngine().voices.filter { $0.language.hasPrefix("en") }.map(\.language),
            "notVerified":["physical-trackpad", "physical-audio-output", "full-screen-other-apps", "physical-multiple-monitors"]]
        // Exercise the native close-button path and only record success during actual termination.
        let cleanup = app.terminationCheck
        app.terminationCheck = {
            cleanup?()
            do {
                try require(!panel.isVisible && !main.isVisible, "termination hides both main window and card")
                try JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]).write(to:folder.appendingPathComponent("desktop-checks.json"))
                print("DESKTOP_CHECKS_PASSED")
            } catch {
                fputs("\(error.localizedDescription)\n", stderr)
                exit(1)
            }
        }
        // Deliberately show both windows to verify cleanup even if the card is still visible.
        app.showLibrary()
        windows.showCard()
        main.performClose(nil)
        await pause(0.5)
        throw DiagnosticError(message: "Closing the main window did not terminate the application")
    }

    private static func descendants(_ view: NSView) -> [NSView] {
        [view] + view.subviews.flatMap { descendants($0) }
    }

    private static func checkLearning(windows: WindowCoordinator, engine: DiagnosticSpeechEngine, folder: URL) async throws {
        let study = windows.study, panel = windows.panel
        try study.selectBook("cet4")
        guard let entry = study.entries.first(where: { $0.word.id == "photograph" }) else {
            throw DiagnosticError(message: "Missing learning fixture")
        }
        study.jump(to: entry)
        study.revealed = true
        await pause(0.4)
        let details = try study.store.learningDetails(wordID: entry.word.id)
        guard let root = panel.contentView,
              let scroll = descendants(root).compactMap({ $0 as? NSScrollView }).first,
              let document = scroll.documentView, let example = details.examples.first else {
            throw DiagnosticError(message: "Missing scrollable learning content")
        }
        try require(document.frame.height > scroll.contentView.bounds.height * 3, "long details are laid out beyond viewport")
        let originalIndex = study.index
        if let cg = CGEvent(scrollWheelEvent2Source: nil, units: .pixel, wheelCount: 1, wheel1: -140, wheel2: 0, wheel3: 0),
           let event = NSEvent(cgEvent: cg) { scroll.scrollWheel(with: event) }
        await pause()
        try require(scroll.contentView.bounds.origin.y > 0 && study.index == originalIndex && study.revealed, "vertical wheel scrolls details without changing word")
        try capture(panel, to: folder.appendingPathComponent("learning-roots.png"))
        for (kind, expected) in [("english", [example.english]), ("chinese", [example.chinese]), ("pair", [example.english, example.chinese])] {
            let id = "example.\(kind).\(example.id)"
            guard let button = descendants(root).compactMap({ $0 as? NSButton }).first(where: { $0.identifier?.rawValue == id }) else {
                throw DiagnosticError(message: "Missing inline button: \(id)")
            }
            button.scrollToVisible(button.bounds.insetBy(dx: 0, dy: -70))
            await pause()
            let rect = button.convert(button.bounds, to: nil)
            let point = CGPoint(x: rect.midX, y: rect.midY)
            try require(point.y > 36 && point.y < panel.frame.height - 24, "inline button inside viewport")
            let before = engine.spoken.count, stops = engine.stops
            post(.leftMouseDown, panel: panel, point: point)
            post(.leftMouseUp, panel: panel, point: point)
            await pause(NSEvent.doubleClickInterval + 0.2)
            try require(Array(engine.spoken.dropFirst(before)) == expected, "inline \(kind) speech sequence")
            try require(engine.stops > stops && study.revealed && study.index == originalIndex, "inline playback cancels old speech without collapsing or flipping")
        }
        try capture(panel, to: folder.appendingPathComponent("learning-examples-light.png"))
        panel.appearance = NSAppearance(named: .darkAqua)
        await pause()
        try capture(panel, to: folder.appendingPathComponent("learning-examples-dark.png"))
        // Text remains a collapse target even deep within the details.
        scroll.contentView.scroll(to: CGPoint(x: 0, y: 0))
        scroll.reflectScrolledClipView(scroll.contentView)
        await pause()
        click(panel)
        await pause(NSEvent.doubleClickInterval + 0.2)
        try require(!study.revealed && panel.frame.height == 150, "collapse after scrolling")
        study.revealed = true
        await pause()
        let stops = engine.stops
        study.advance(1)
        await pause()
        try require(!study.revealed && engine.stops > stops, "word change stops example speech")
        study.revealed = true
        await pause()
        let freshScroll = descendants(root).compactMap { $0 as? NSScrollView }.first
        try require(freshScroll?.contentView.bounds.origin.y == 0, "new word starts at top")
    }

    private static func checkAppearance(app: AppDelegate, folder: URL) async throws {
        guard let windows = app.windows, let main = app.mainWindow else { return }
        let appearance = windows.appearance, study = windows.study, panel = windows.panel
        defer {
            appearance.removeWallpaper()
            try? FileManager.default.removeItem(at: appearance.wallpaperDirectory)
            app.libraryNavigation.showsAppearance = false
        }
        try study.selectBook("automotive")
        if let word = study.entries.first(where: { $0.word.id == "greenhouse" }) { study.jump(to: word) }
        for preset in CardPreset.allCases {
            appearance.apply(preset)
            await pause()
            try require(appearance.style.preset == preset && panel.frame.height == 150, "apply preset without changing compact size")
            try capture(panel, to: folder.appendingPathComponent("theme-\(preset.rawValue).png"))
        }
        appearance.apply(.frosted)
        app.libraryNavigation.showsAppearance = true
        app.showLibrary()
        main.setContentSize(NSSize(width:900,height:760))
        await pause(0.4)
        try capture(main, to: folder.appendingPathComponent("appearance-light.png"))
        main.appearance = NSAppearance(named: .darkAqua)
        await pause()
        try capture(main, to: folder.appendingPathComponent("appearance-dark.png"))
        main.setContentSize(NSSize(width:800,height:580))
        await pause()
        try capture(main, to: folder.appendingPathComponent("appearance-compact.png"))
        // Deterministic test image; no personal photo is copied into diagnostics or the app.
        let bitmap = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:360,pixelsHigh:240,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
        for y in 0..<240 {
            for x in 0..<360 {
                let offset = y * bitmap.bytesPerRow + x * 4
                bitmap.bitmapData![offset] = UInt8(45 + x / 3)
                bitmap.bitmapData![offset + 1] = UInt8(90 + y / 3)
                bitmap.bitmapData![offset + 2] = UInt8(110 + x / 5)
                bitmap.bitmapData![offset + 3] = 255
            }
        }
        let source = folder.appendingPathComponent("test-wallpaper.png")
        try bitmap.representation(using: .png, properties: [:])!.write(to: source)
        try appearance.importWallpaper(from: source)
        try require(appearance.wallpaper != nil && appearance.style.mode == .wallpaper, "wallpaper import")
        app.showCard()
        await pause()
        try capture(panel, to: folder.appendingPathComponent("theme-wallpaper.png"))
        appearance.setText(CardRGB(1, 0.86, 0.57))
        await pause()
        try capture(panel, to: folder.appendingPathComponent("theme-custom-text.png"))
        click(panel)
        await pause(NSEvent.doubleClickInterval + 0.3)
        try require(study.revealed && panel.frame.height == 220, "wallpaper card reveals")
        click(panel)
        await pause(NSEvent.doubleClickInterval + 0.3)
        try require(!study.revealed && panel.frame.height == 150, "wallpaper card collapses")
        app.showLibrary()
        main.appearance = NSAppearance(named: .aqua)
        main.setContentSize(NSSize(width:900,height:760))
        await pause()
        try capture(main, to: folder.appendingPathComponent("appearance-wallpaper.png"))
    }
}
