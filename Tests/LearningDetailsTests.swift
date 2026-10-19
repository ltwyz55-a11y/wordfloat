import XCTest
@testable import WordCore

final class LearningDetailsTests: XCTestCase {
    func testCompleteLearningDataRoundTripAndMissingEntry() throws {
        let store = try VocabularyStore(path: ":memory:", readOnly: false)
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        try store.execute(String(contentsOf: root.appendingPathComponent("Data/schema.sql")))
        try store.execute("INSERT INTO words VALUES ('design','design','','n.','设计',100)")
        var details = LearningDetails()
        details.dictionaryMeaning = "n. 设计；图样\nv. 设计；计划"
        details.englishDefinition = "A plan or drawing."
        details.senses = [DictionarySense(partOfSpeech: "名词", text: "构思", sourceURL: "https://zh.wiktionary.org/wiki/design")]
        details.etymologies = [EtymologyNote(text: "词源说明", sourceURL: "https://zh.wiktionary.org/wiki/design")]
        details.examples = [BilingualExample(id: "sample", english: "I like this design.", chinese: "我喜欢这个设计。", attribution: "原创示例", sourceURL: "")]
        details.forms = [WordForm(label: "复数", text: "designs")]
        let json = String(data: try JSONEncoder().encode(details), encoding: .utf8)!.replacingOccurrences(of: "'", with: "''")
        try store.execute("INSERT INTO learning_details VALUES ('design','\(json)')")
        XCTAssertEqual(try store.learningDetails(wordID: "design"), details)
        XCTAssertEqual(try store.learningDetails(wordID: "unknown"), LearningDetails())
        XCTAssertEqual(try store.learningDetails(wordID: "' OR 1=1 --"), LearningDetails())
    }
}
