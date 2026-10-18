import XCTest
@testable import WordCore

@MainActor
final class PersistenceTests: XCTestCase {
    func testRelaunchRestoresBothBooks() throws {
        let name = "WordFloatTests.\(UUID().uuidString)"
        let prefs = UserDefaults(suiteName: name)!
        defer { prefs.removePersistentDomain(forName: name) }
        let original = try StudySession(store: .bundled(), defaults: prefs)
        original.advance(12)
        let firstWord = original.current?.word.id
        try original.selectBook("industrial")
        original.advance(28)
        let restored = try StudySession(store: .bundled(), defaults: prefs)
        XCTAssertEqual(restored.selectedBookID, "industrial")
        XCTAssertEqual(restored.current?.word.id, original.current?.word.id)
        try restored.selectBook("zk")
        XCTAssertEqual(restored.current?.word.id, firstWord)
    }
    func testCorruptedAndStaleSnapshotsRecover() throws {
        let name = "WordFloatTests.\(UUID().uuidString)"
        let prefs = UserDefaults(suiteName: name)!
        defer { prefs.removePersistentDomain(forName: name) }
        prefs.set(Data("invalid json".utf8), forKey: "study.snapshot.v1")
        XCTAssertEqual(try StudySession(store: .bundled(), defaults: prefs).index, 0)
        let stale = #"{"selectedBookID":"missing","positions":{"zk":"removed-word"}}"#
        prefs.set(Data(stale.utf8), forKey: "study.snapshot.v1")
        let restored = try StudySession(store: .bundled(), defaults: prefs)
        XCTAssertEqual(restored.selectedBookID, "zk")
        XCTAssertEqual(restored.index, 0)
    }
}
