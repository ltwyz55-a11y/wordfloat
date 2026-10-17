import XCTest
@testable import WordCore

final class ModelsTests: XCTestCase {
    func testDomainMeaningAndRoundTrip() throws {
        let word = VocabularyWord(id: "draft", spelling: "draft", meaning: "草稿")
        let entry = BookEntry(bookID: "industrial", word: word, domainMeaning: "拔模斜度", position: 7)
        let restored = try JSONDecoder().decode(BookEntry.self, from: JSONEncoder().encode(entry))
        XCTAssertEqual(restored, entry)
        XCTAssertEqual(restored.meaning, "拔模斜度")
        XCTAssertEqual(BookEntry(bookID: "exam", word: word, domainMeaning: " ", position: 0).meaning, "草稿")
        XCTAssertNotEqual(entry.id, BookEntry(bookID: "exam", word: word, position: 0).id)
    }
}
