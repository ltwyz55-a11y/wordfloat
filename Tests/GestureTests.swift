import XCTest
@testable import WordCore

final class GestureTests: XCTestCase {
    func testDragDoesNotBecomeClickAndDirectionIsStable() {
        var gesture = CardPointerGesture()
        gesture.begin(at: CGPoint(x: 100,y:100))
        XCTAssertEqual(gesture.end(at: CGPoint(x: 40,y:102),clickCount:1), .next)
        gesture.begin(at: .zero)
        XCTAssertEqual(gesture.end(at: CGPoint(x: 60,y:2),clickCount:1), .previous)
        gesture.begin(at: .zero)
        gesture.move(to: CGPoint(x: 20,y:1))
        XCTAssertEqual(gesture.end(at: .zero,clickCount:1), .none)
        gesture.begin(at: .zero)
        XCTAssertEqual(gesture.end(at: CGPoint(x: 1,y:80),clickCount:1), .none)
    }
    func testClickDoubleClickAndCancellation() {
        var gesture = CardPointerGesture()
        gesture.begin(at: .zero)
        XCTAssertEqual(gesture.end(at: .zero,clickCount:1), .scheduleSingle)
        gesture.begin(at: .zero)
        XCTAssertEqual(gesture.end(at: .zero,clickCount:2), .openLibrary)
        gesture.begin(at: .zero); gesture.cancel()
        XCTAssertEqual(gesture.end(at: .zero,clickCount:1), .none)
    }
    func testOneWordPerScrollAndNoMomentumOrVerticalNavigation() {
        var gesture = HorizontalScrollGesture()
        XCTAssertNil(gesture.consume(dx:-20,dy:0,time:1,begins:true,momentum:false,precise:true))
        XCTAssertEqual(gesture.consume(dx:-25,dy:0,time:1.1,begins:false,momentum:false,precise:true),1)
        XCTAssertNil(gesture.consume(dx:-100,dy:0,time:1.2,begins:false,momentum:false,precise:true))
        XCTAssertNil(gesture.consume(dx:-100,dy:0,time:1.3,begins:false,momentum:true,precise:true))
        XCTAssertNil(gesture.consume(dx:1,dy:80,time:2,begins:true,momentum:false,precise:true))
        XCTAssertEqual(gesture.consume(dx:45,dy:0,time:3,begins:true,momentum:false,precise:true),-1)
    }
}
