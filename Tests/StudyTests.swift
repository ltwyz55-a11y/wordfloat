import XCTest
@testable import WordCore

@MainActor
final class StudyTests: XCTestCase {
    func testWrappingAndIndependentBookPositions() throws {
        let study = try StudySession(store: .bundled())
        let first = study.current
        study.revealed = true
        study.advance(-1)
        XCTAssertEqual(study.index, study.entries.count - 1)
        XCTAssertFalse(study.revealed)
        study.advance(1)
        XCTAssertEqual(study.current, first)
        study.advance(8)
        let remembered = study.current
        try study.selectBook("materials")
        study.advance(3)
        try study.selectBook("zk")
        XCTAssertEqual(study.current, remembered)
        try study.selectBook("materials")
        XCTAssertEqual(study.index, 3)
        XCTAssertThrowsError(try study.selectBook("nonexistent"))
        XCTAssertEqual(study.selectedBookID, "materials")
    }
    func testJumpAndChangeNotification() throws {
        let study = try StudySession(store: .bundled())
        var changes = 0
        study.willChangeWord = { changes += 1 }
        study.jump(to: study.entries[10])
        XCTAssertEqual(study.index, 10)
        study.advance(1)
        XCTAssertEqual(changes, 2)
    }
}
