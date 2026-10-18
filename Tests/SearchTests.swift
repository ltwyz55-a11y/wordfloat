import XCTest
@testable import WordCore

final class SearchTests: XCTestCase {
    func testBookFiltersAndSearch() throws {
        let books = try VocabularyStore.bundled().books()
        XCTAssertEqual(BookSearch.filter(books, category: .professional, query: "").count, 3)
        XCTAssertEqual(BookSearch.filter(books, category: nil, query: " CET4 ").map(\.name), ["四级"])
        XCTAssertEqual(BookSearch.filter(books, category: nil, query: "汽车").map(\.id), ["automotive"])
        XCTAssertEqual(BookSearch.filter(books, category: .exam, query: "汽车"), [])
        XCTAssertEqual(BookSearch.filter(books, category: nil, query: "xyz-not-a-book"), [])
    }
    func testProfessionalSearchUsesDomainMeaningAndLiteralInput() throws {
        let store = try VocabularyStore.bundled()
        XCTAssertTrue(try store.entries(bookID: "automotive", search: "绿色房子").isEmpty)
        XCTAssertTrue(try store.entries(bookID: "automotive", search: "GREENHOUSE").first?.meaning.contains("车窗") == true)
        XCTAssertFalse(try store.entries(bookID: "materials", search: "杨氏").isEmpty)
        XCTAssertFalse(try store.entries(bookID: "materials", search: "Young's").isEmpty)
        XCTAssertTrue(try store.entries(bookID: "industrial", search: "%_").isEmpty)
    }
}
