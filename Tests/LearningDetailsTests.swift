import XCTest
@testable import WordCore

final class LearningDetailsTests: XCTestCase {
    func testBundledLearningContentPreservesFullMeaningsAndAttribution() throws {
        let store = try VocabularyStore.bundled()
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        let source = try JSONSerialization.jsonObject(with: Data(contentsOf: root.appendingPathComponent("Data/learning-details.json"))) as! [String: Any]
        let words = source["words"] as! [String: Any]
        XCTAssertEqual(words.count, 12304)
        for (id, raw) in words {
            let expected = try JSONDecoder().decode(LearningDetails.self, from: JSONSerialization.data(withJSONObject: raw))
            XCTAssertEqual(try store.learningDetails(wordID: id), expected, id)
        }
        let photograph = try store.learningDetails(wordID: "photograph")
        XCTAssertTrue(photograph.etymologies.contains { $0.text.contains("photo-") })
        let happy = try store.learningDetails(wordID: "happy")
        XCTAssertFalse(happy.examples.isEmpty)
        XCTAssertTrue(happy.examples.allSatisfy { !$0.attribution.isEmpty && !$0.chinese.isEmpty })
    }

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
