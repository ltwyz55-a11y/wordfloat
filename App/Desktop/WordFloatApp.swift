import AppKit
import SwiftUI
import WordCore

@main
struct WordFloatApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    var body: some Scene { Settings { EmptyView() } }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate, NSMenuDelegate {
    var windows: WindowCoordinator?
    var mainWindow: NSWindow?
    private var statusItem: NSStatusItem?
    private var toggleItem: NSMenuItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        do {
            let arguments = ProcessInfo.processInfo.arguments
            let diagnosticIndex = arguments.firstIndex(of: "--diagnostics")
            let diagnosticEngine = diagnosticIndex == nil ? nil : DiagnosticSpeechEngine()
            let suiteName = "WordFloat.Diagnostics.\(UUID().uuidString)"
            let prefs = diagnosticIndex == nil ? UserDefaults.standard : UserDefaults(suiteName: suiteName)!
            let study = try StudySession(store: .bundled(), defaults: prefs)
            let audio = Pronunciation(engine: diagnosticEngine, defaults: prefs)
            let appearance = CardAppearance(defaults: prefs, wallpaperDirectory: diagnosticIndex == nil ? nil : FileManager.default.temporaryDirectory.appendingPathComponent(suiteName))
            let windows = WindowCoordinator(study: study, audio: audio, appearance: appearance, defaults: prefs)
            self.windows = windows
            windows.openLibrary = { [weak self] in self?.showLibrary() }
            let main = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 900, height: 620),
                styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
            main.title = "浮词 · 单词本"
            main.titlebarAppearsTransparent = true
            main.isReleasedWhenClosed = false
            main.minSize = NSSize(width: 800, height: 610)
            main.delegate = self
            main.contentView = NSHostingView(rootView: LibraryView(study: study, audio: audio, onReturn: { [weak self] in self?.showCard() }))
            main.center()
            mainWindow = main
            installMenus()
            if let diagnosticIndex, let diagnosticEngine, arguments.indices.contains(diagnosticIndex+1) {
                Task { @MainActor in
                    do {
                        try await DesktopDiagnostics.run(app: self, engine: diagnosticEngine, folder: URL(fileURLWithPath: arguments[diagnosticIndex+1]))
                        prefs.removePersistentDomain(forName: suiteName)
                        NSApp.terminate(nil)
                    } catch {
                        fputs("\(error.localizedDescription)\n", stderr)
                        prefs.removePersistentDomain(forName: suiteName)
                        exit(1)
                    }
                }
                return
            }
            if UserDefaults.standard.bool(forKey: "hasLaunched") { showCard() }
            else { showLibrary(); UserDefaults.standard.set(true, forKey: "hasLaunched") }
        } catch {
            if ProcessInfo.processInfo.arguments.contains("--diagnostics") {
                fputs("\(error.localizedDescription)\n", stderr)
                exit(1)
            }
            NSAlert(error: error).runModal()
            NSApp.terminate(nil)
        }
    }
    @objc func showLibrary() {
        windows?.hideCard()
        NSApp.setActivationPolicy(.regular)
        mainWindow?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
    @objc func showCard() {
        mainWindow?.orderOut(nil)
        NSApp.setActivationPolicy(.accessory)
        windows?.showCard()
    }
    @objc private func toggleCard() {
        guard let windows else { return }
        if windows.panel.isVisible { windows.hideCard() }
        else { showCard() }
    }
    @objc private func quit() { NSApp.terminate(nil) }
    func menuWillOpen(_ menu: NSMenu) { toggleItem?.title = windows?.panel.isVisible == true ? "隐藏单词卡片" : "显示单词卡片" }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        if sender === mainWindow { showCard(); return false }
        return true
    }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showLibrary(); return true }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func applicationWillTerminate(_ notification: Notification) { windows?.audio.stop() }

    private func installMenus() {
        let menu = NSMenu()
        menu.delegate = self
        func item(_ title: String, _ action: Selector, key: String = "") -> NSMenuItem {
            let result = NSMenuItem(title: title, action: action, keyEquivalent: key)
            result.target = self
            return result
        }
        toggleItem = item("显示单词卡片", #selector(toggleCard))
        menu.addItem(toggleItem!)
        menu.addItem(item("打开单词本", #selector(showLibrary)))
        menu.addItem(.separator())
        menu.addItem(item("退出浮词", #selector(quit), key: "q"))
        let status = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        status.button?.image = NSImage(systemSymbolName: "square.stack.3d.up", accessibilityDescription: "浮词")
        status.button?.toolTip = "浮词"
        status.menu = menu
        statusItem = status

        let menuBar = NSMenu()
        let appMenu = NSMenu(title: "浮词")
        appMenu.addItem(item("打开单词本", #selector(showLibrary), key: "1"))
        appMenu.addItem(item("悬浮学习", #selector(showCard), key: "2"))
        appMenu.addItem(.separator())
        appMenu.addItem(item("退出浮词", #selector(quit), key: "q"))
        let appItem = NSMenuItem(); appItem.submenu = appMenu; menuBar.addItem(appItem)
        let editMenu = NSMenu(title: "编辑")
        for (title, action, key) in [("撤销", "undo:", "z"), ("剪切", "cut:", "x"), ("拷贝", "copy:", "c"), ("粘贴", "paste:", "v"), ("全选", "selectAll:", "a")] {
            editMenu.addItem(NSMenuItem(title: title, action: Selector(action), keyEquivalent: key))
        }
        let editItem = NSMenuItem(); editItem.submenu = editMenu; menuBar.addItem(editItem)
        NSApp.mainMenu = menuBar
    }
}
