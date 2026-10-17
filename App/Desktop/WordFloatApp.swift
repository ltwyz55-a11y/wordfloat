import SwiftUI
import WordCore

@main
struct WordFloatApp: App {
    var body: some Scene {
        WindowGroup {
            Text(AppInfo.name).font(.largeTitle).frame(width: 900, height: 620)
        }
    }
}
