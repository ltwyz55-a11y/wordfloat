import Foundation

public enum BookCategory: String, Codable, CaseIterable {
    case exam, professional
    public var title: String { self == .exam ? "考试词汇" : "专业领域" }
}

public struct VocabularyWord: Codable, Equatable, Identifiable {
    public let id: String
    public let spelling: String
    public let phonetic: String
    public let partOfSpeech: String
    public let meaning: String
    public let frequency: Int?
    public init(id: String, spelling: String, phonetic: String = "", partOfSpeech: String = "", meaning: String, frequency: Int? = nil) {
        self.id = id; self.spelling = spelling; self.phonetic = phonetic
        self.partOfSpeech = partOfSpeech; self.meaning = meaning; self.frequency = frequency
    }
}

public struct VocabularyBook: Codable, Equatable, Identifiable {
    public let id: String
    public let name: String
    public let category: BookCategory
    public let subtitle: String
    public let source: String
    public let version: String
    public let count: Int
    public init(id: String, name: String, category: BookCategory, subtitle: String = "", source: String, version: String, count: Int = 0) {
        self.id = id; self.name = name; self.category = category; self.subtitle = subtitle
        self.source = source; self.version = version; self.count = count
    }
}

public struct BookEntry: Codable, Equatable, Identifiable {
    public let bookID: String
    public let word: VocabularyWord
    public let domainMeaning: String?
    public let position: Int
    public var id: String { bookID + ":" + word.id }
    public var meaning: String {
        guard let value = domainMeaning, !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return word.meaning }
        return value
    }
    public init(bookID: String, word: VocabularyWord, domainMeaning: String? = nil, position: Int) {
        self.bookID = bookID; self.word = word; self.domainMeaning = domainMeaning; self.position = position
    }
}
