import XCTest
@testable import WordCore

final class WindowPlacementTests: XCTestCase {
    let main = CGRect(x: 0, y: 30, width: 1440, height: 850)
    func testDisconnectedMonitorAndExpandedCard() {
        let offscreen = CGRect(x: 2000, y: -400, width: 260, height: 220)
        XCTAssertTrue(main.contains(WindowPlacement.clamp(offscreen, to: [main])))
        let bottom = CGRect(x: 100, y: -40, width: 260, height: 220)
        XCTAssertEqual(WindowPlacement.clamp(bottom, to: [main]).minY, 30)
    }
    func testNegativeCoordinateMonitorPreserved() {
        let left = CGRect(x: -1280, y: 0, width: 1280, height: 800)
        let card = CGRect(x: -600, y: 200, width: 260, height: 150)
        XCTAssertEqual(WindowPlacement.clamp(card, to: [main, left]), card)
        let invalid = CGRect(x: Double.nan, y: 10, width: 260, height: 150)
        XCTAssertTrue(main.contains(WindowPlacement.clamp(invalid, to: [main])))
    }
}
