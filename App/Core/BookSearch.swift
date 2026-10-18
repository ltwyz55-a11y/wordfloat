import Foundation

public enum BookSearch {
    public static func filter(_ books: [VocabularyBook], category: BookCategory?, query: String) -> [VocabularyBook] {
        let term = query.trimmingCharacters(in: .whitespacesAndNewlines)
        return books.filter { book in
            (category == nil || book.category == category) &&
            (term.isEmpty || [book.name,book.id,book.subtitle,book.category.title].contains { $0.localizedCaseInsensitiveContains(term) })
        }
    }
}
