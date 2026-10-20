import XCTest
@testable import WordCore

final class CardSizingTests: XCTestCase {
    func testBoundsAndIndependentHeightsRestore() {
        var sizing = CardSizing()
        XCTAssertEqual(sizing.size(revealed: false), CGSize(width: 260, height: 150))
        sizing.remember(CGSize(width: 400, height: 280), revealed: false)
        sizing.remember(CGSize(width: 900, height: 10), revealed: true)
        let restored = CardSizing(saved: sizing.saved)
        XCTAssertEqual(restored.size(revealed: true), CGSize(width: 520, height: 200))
        XCTAssertEqual(restored.size(revealed: false), CGSize(width: 520, height: 280))
        XCTAssertEqual(CardSizing(saved: [.nan, .infinity, -.infinity]), CardSizing())
        XCTAssertEqual(CardSizing(saved: [0]).saved, [260,150,220])
    }
    func testEdgesAndOppositeAnchorAtBounds() {
        let frame = CGRect(x: 100, y: 200, width: 260, height: 150)
        XCTAssertEqual(CardResizeEdges.hitTest(CGPoint(x: 130, y: 70), size: frame.size), [])
        XCTAssertEqual(CardResizeEdges.hitTest(CGPoint(x: 12, y: 12), size: frame.size), [.left,.bottom])
        XCTAssertEqual(CardResizeEdges.hitTest(CGPoint(x: 2, y: 70), size: frame.size), .left)
        let small = CardResizeEdges([.left,.bottom]).frame(from: frame, translation: CGPoint(x: 200, y: 200), revealed: false)
        XCTAssertEqual(small.size, CGSize(width: 220, height: 130))
        XCTAssertEqual(small.maxX, frame.maxX); XCTAssertEqual(small.maxY, frame.maxY)
        let large = CardResizeEdges([.right,.top]).frame(from: frame, translation: CGPoint(x: 900, y: 900), revealed: false)
        XCTAssertEqual(large, CGRect(x: 100, y: 200, width: 520, height: 420))
        XCTAssertEqual(CardResizeEdges.right.frame(from: frame, translation: CGPoint(x: CGFloat.nan,y: 0), revealed: false), frame)
    }
}
