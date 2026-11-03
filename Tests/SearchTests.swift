import XCTest
@testable import WordCore

final class SearchTests: XCTestCase {
    func testDictionaryLookupAcrossBooksAndLiteralQueries() throws {
        let store = try VocabularyStore.bundled()
        let exact = try XCTUnwrap(store.lookup("  GREENHOUSE\n").first)
        XCTAssertEqual(exact.id, "greenhouse")
        XCTAssertFalse(exact.meaning.isEmpty)
        let memberships = try store.memberships(wordID: exact.id)
        XCTAssertGreaterThan(memberships.count, 1)
        XCTAssertTrue(memberships.contains { $0.bookID == "automotive" && $0.meaning.contains("车窗") })
        XCTAssertTrue(try store.lookup("车窗").contains { $0.id == "greenhouse" })
        let results = try store.lookup("a", limit: 1000)
        XCTAssertEqual(results.count, 100)
        XCTAssertEqual(Set(results.map(\.id)).count, results.count)
        XCTAssertEqual(try store.lookup("a", limit: 3).count, 3)
        XCTAssertEqual(try store.lookup("a", limit: 0), [])
        for query in [" ", "%_", "' OR 1=1 --", "zz-no-dictionary-result"] {
            XCTAssertTrue(try store.lookup(query).isEmpty)
        }
        XCTAssertTrue(try store.memberships(wordID: "__not_a_dictionary_word__").isEmpty)
    }
    @MainActor
    func testLookupDoesNotChangeLearningPosition() throws {
        let study = try StudySession(store: .bundled())
        study.advance(4)
        let entry = study.current
        _ = try study.store.lookup("photograph")
        _ = try study.store.memberships(wordID: "photograph")
        XCTAssertEqual(study.current, entry)
    }
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
