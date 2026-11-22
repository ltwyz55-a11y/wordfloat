import XCTest
@testable import WordCore

final class BundledDataTests: XCTestCase {
    func testEveryBookHasCompleteUsableEntries() throws {
        let store = try VocabularyStore.bundled()
        let books = try store.books()
        XCTAssertEqual(books.filter { $0.category == .exam }.count, 7)
        for book in books {
            let entries = try store.entries(bookID: book.id)
            XCTAssertEqual(entries.count, book.count)
            XCTAssertGreaterThan(entries.count, book.id == "cambridge-listening-4-19" ? 948 : book.category == .exam ? 1000 : 299)
            XCTAssertEqual(Set(entries.map(\.word.id)).count, entries.count)
            XCTAssertEqual(entries.map(\.position), Array(0..<entries.count))
            XCTAssertTrue(entries.allSatisfy { !$0.meaning.isEmpty && !$0.word.spelling.isEmpty })
        }
    }
    func testImportedListeningBookPreservesSourceOrderAndMeanings() throws {
        let store = try VocabularyStore.bundled()
        let entries = try store.entries(bookID: "cambridge-listening-4-19")
        XCTAssertEqual(entries.count, 949)
        XCTAssertEqual(entries.first?.word.spelling, "innovation")
        XCTAssertEqual(entries.last?.word.spelling, "accurate")
        XCTAssertEqual(entries[509].word.id, "situational")
        XCTAssertEqual(entries[509].meaning, "原文件未提供释义")
        XCTAssertTrue(entries[0].meaning.contains("创新"))
        XCTAssertTrue(entries[149].word.phonetic.count > 0)
        XCTAssertTrue(try store.lookup("situational").contains { $0.id == "situational" })
        for entry in entries { _ = try store.learningDetails(wordID: entry.word.id) }
    }
}
