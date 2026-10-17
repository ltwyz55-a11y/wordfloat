import XCTest
@testable import WordCore

final class BundledDataTests: XCTestCase {
    func testEveryBookHasCompleteUsableEntries() throws {
        let store = try VocabularyStore.bundled()
        let books = try store.books()
        XCTAssertEqual(books.filter { $0.category == .exam }.count, 6)
        for book in books {
            let entries = try store.entries(bookID: book.id)
            XCTAssertEqual(entries.count, book.count)
            XCTAssertGreaterThan(entries.count, book.category == .exam ? 1000 : 299)
            XCTAssertEqual(Set(entries.map(\.word.id)).count, entries.count)
            XCTAssertEqual(entries.map(\.position), Array(0..<entries.count))
            XCTAssertTrue(entries.allSatisfy { !$0.meaning.isEmpty && !$0.word.spelling.isEmpty })
        }
    }
}
