import XCTest
@testable import WordCore

final class StoreTests: XCTestCase {
    func testEmptyBookSearchAndDomainMeaning() throws {
        let store = try VocabularyStore(path: ":memory:", readOnly: false)
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        try store.execute(String(contentsOf: root.appendingPathComponent("Data/schema.sql")))
        try store.execute("""
        INSERT INTO books VALUES ('auto','汽车设计','professional','','test','1',0),('empty','空词本','exam','','test','1',1);
        INSERT INTO words VALUES ('draft','draft','','n.','草稿',10),('a','A-pillar','','n.','A柱',NULL);
        INSERT INTO entries VALUES ('auto','a',NULL,0),('auto','draft','拔模斜度',1);
        """)
        XCTAssertEqual(try store.books().map(\.count), [2, 0])
        XCTAssertEqual(try store.entries(bookID: "empty"), [])
        XCTAssertEqual(try store.entries(bookID: "auto").map(\.word.spelling), ["A-pillar", "draft"])
        XCTAssertEqual(try store.entries(bookID: "auto", search: "DRAFT").first?.meaning, "拔模斜度")
        XCTAssertEqual(try store.entries(bookID: "auto", search: "拔模").count, 1)
        XCTAssertEqual(try store.entries(bookID: "auto", search: "%' OR 1=1 --").count, 0)
        XCTAssertThrowsError(try store.execute("INSERT INTO entries VALUES ('missing','draft',NULL,0)"))
    }
}
