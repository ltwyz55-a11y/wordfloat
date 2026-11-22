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
    let libraryNavigation = LibraryNavigation()
    var terminationCheck: (() -> Void)?
    private var statusItem: NSStatusItem?
    private var toggleItem: NSMenuItem?
    private var informationWindow: NSWindow?

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
            main.contentView = NSHostingView(rootView: LibraryView(study: study, audio: audio, appearance: appearance, navigation: libraryNavigation, onReturn: { [weak self] in self?.showCard() }))
            main.center()
            mainWindow = main
            installMenus()
            if let diagnosticIndex, let diagnosticEngine, arguments.indices.contains(diagnosticIndex+1) {
                terminationCheck = { prefs.removePersistentDomain(forName: suiteName) }
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
    @objc private func showPrivacy() {
        if informationWindow == nil {
            let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 680, height: 520),
                styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
            window.title = "隐私与词库来源"; window.isReleasedWhenClosed = false
            let scroll = NSScrollView(frame: window.contentView!.bounds)
            scroll.autoresizingMask = [.width, .height]; scroll.hasVerticalScroller = true
            let text = NSTextView(frame: scroll.bounds)
            text.isEditable = false; text.isRichText = false; text.isHorizontallyResizable = false
            text.isVerticallyResizable = true; text.autoresizingMask = [.width]
            text.textContainer?.widthTracksTextView = true; text.textContainerInset = NSSize(width: 20, height: 20)
            text.font = .systemFont(ofSize: 13); text.textColor = .textColor
            text.backgroundColor = .textBackgroundColor
            let privacy = "浮词隐私说明\n\n浮词无需账号，不包含广告、统计或追踪 SDK，不主动收集或上传个人信息、查词内容、学习进度或壁纸。词库保存在应用包内；学习进度、口音、卡片位置和外观保存在本机。图片由用户主动选择后复制到应用自己的本地目录，不修改原图。\n\n发音使用 macOS 系统语音。已安装的声音可离线使用；系统声音的下载和处理遵循 Apple 的系统设置。点击来源或许可链接会打开浏览器，外部网站有其自身隐私政策。\n\n恢复卡片默认外观不会清除学习进度。删除壁纸只移除应用保存的副本。卸载可能保留系统管理的本地偏好或容器数据；本应用没有服务器端账号或远程数据。\n\n本说明依据当前本地处理方式编写。如未来增加联网或数据收集功能，将先更新说明。\n\n词库来源与许可\n\n"
            let notice = Bundle.main.url(forResource: "DATA-NOTICE", withExtension: "md")
                .flatMap { try? String(contentsOf: $0, encoding: .utf8) } ?? "详见发行包中的 DATA-NOTICE.md 与 ECDICT-LICENSE.txt。"
            let license = Bundle.main.url(forResource: "ECDICT-LICENSE", withExtension: "txt")
                .flatMap { try? String(contentsOf: $0, encoding: .utf8) } ?? ""
            text.string = privacy + notice + "\n\n" + license
            scroll.documentView = text; window.contentView = scroll; window.center(); informationWindow = window
        }
        NSApp.setActivationPolicy(.regular)
        informationWindow?.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    func menuWillOpen(_ menu: NSMenu) { toggleItem?.title = windows?.panel.isVisible == true ? "隐藏单词卡片" : "显示单词卡片" }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        if sender === mainWindow { quit(); return false }
        return true
    }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showLibrary(); return true }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func applicationWillTerminate(_ notification: Notification) {
        windows?.hideCard()
        mainWindow?.orderOut(nil)
        if let statusItem { NSStatusBar.system.removeStatusItem(statusItem) }
        terminationCheck?()
    }

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
        menu.addItem(item("隐私与词库来源", #selector(showPrivacy)))
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
        appMenu.addItem(item("隐私与词库来源", #selector(showPrivacy)))
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
