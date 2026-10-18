import Foundation
import Combine

@MainActor
public final class StudySession: ObservableObject {
    public let books: [VocabularyBook]
    public let store: VocabularyStore
    @Published public private(set) var selectedBookID: String = ""
    @Published public private(set) var entries: [BookEntry] = []
    @Published public private(set) var index = 0
    @Published public var revealed = false
    public var willChangeWord: (() -> Void)?
    private var positions: [String: String] = [:]
    private let defaults: UserDefaults?
    private struct Snapshot: Codable {
        let selectedBookID: String
        let positions: [String: String]
    }
    private static let storageKey = "study.snapshot.v1"
    public var current: BookEntry? { entries.indices.contains(index) ? entries[index] : nil }
    public var currentBook: VocabularyBook? { books.first { $0.id == selectedBookID } }

    public init(store: VocabularyStore, defaults: UserDefaults? = nil) throws {
        self.store = store
        self.defaults = defaults
        self.books = try store.books()
        let saved = defaults?.data(forKey: Self.storageKey).flatMap { try? JSONDecoder().decode(Snapshot.self, from: $0) }
        positions = saved?.positions ?? [:]
        let initial = books.first { $0.id == saved?.selectedBookID } ?? books.first
        if let initial { try selectBook(initial.id) }
    }
    public func selectBook(_ id: String) throws {
        guard books.contains(where: { $0.id == id }) else { throw StoreError(message: "找不到这个词本") }
        let loaded = try store.entries(bookID: id)
        willChangeWord?()
        if let current { positions[selectedBookID] = current.word.id }
        selectedBookID = id
        entries = loaded
        index = loaded.firstIndex { $0.word.id == positions[id] } ?? 0
        revealed = false
        save()
    }
    public func advance(_ delta: Int) {
        guard !entries.isEmpty else { return }
        willChangeWord?()
        index = ((index + delta % entries.count) % entries.count + entries.count) % entries.count
        positions[selectedBookID] = current?.word.id
        revealed = false
        save()
    }
    public func jump(to entry: BookEntry) {
        guard entry.bookID == selectedBookID, let target = entries.firstIndex(where: { $0.id == entry.id }) else { return }
        willChangeWord?()
        index = target
        positions[selectedBookID] = current?.word.id
        revealed = false
        save()
    }
    private func save() {
        if let current { positions[selectedBookID] = current.word.id }
        if let data = try? JSONEncoder().encode(Snapshot(selectedBookID: selectedBookID, positions: positions)) {
            defaults?.set(data, forKey: Self.storageKey)
        }
    }
}
