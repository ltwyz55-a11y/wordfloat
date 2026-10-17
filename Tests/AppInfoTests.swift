import XCTest
@testable import WordCore

final class AppInfoTests: XCTestCase {
    func testAppName() { XCTAssertEqual(AppInfo.name, "浮词") }
}
