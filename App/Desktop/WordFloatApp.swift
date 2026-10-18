import AppKit
import SwiftUI
import WordCore

@main
struct WordFloatApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    var body: some Scene { Settings { EmptyView() } }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    var windows: WindowCoordinator?
    func applicationDidFinishLaunching(_ notification: Notification) {
        do {
            let study = try StudySession(store: .bundled(), defaults: .standard)
            let audio = Pronunciation(defaults: .standard)
            windows = WindowCoordinator(study: study, audio: audio)
            windows?.showCard()
        } catch {
            NSAlert(error: error).runModal()
            NSApp.terminate(nil)
        }
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func applicationWillTerminate(_ notification: Notification) { windows?.audio.stop() }
}
