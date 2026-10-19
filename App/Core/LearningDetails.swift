import Foundation

public struct DictionarySense: Codable, Equatable {
    public let partOfSpeech: String
    public let text: String
    public let sourceURL: String
}

public struct EtymologyNote: Codable, Equatable {
    public let text: String
    public let sourceURL: String
}

public struct BilingualExample: Codable, Equatable, Identifiable {
    public let id: String
    public let english: String
    public let chinese: String
    public let attribution: String
    public let sourceURL: String
}

public struct WordForm: Codable, Equatable {
    public let label: String
    public let text: String
}

public struct LearningDetails: Codable, Equatable {
    public var dictionaryMeaning: String = ""
    public var englishDefinition: String = ""
    public var senses: [DictionarySense] = []
    public var etymologies: [EtymologyNote] = []
    public var examples: [BilingualExample] = []
    public var forms: [WordForm] = []
    public init() {}
}
