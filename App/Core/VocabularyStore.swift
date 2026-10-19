import Foundation
import SQLite3

public struct StoreError: LocalizedError {
    public let message: String
    public var errorDescription: String? { message }
}

public final class VocabularyStore {
    private var db: OpaquePointer?
    public static func bundled() throws -> VocabularyStore {
        // A packaged app must be self-contained; never silently use a developer build directory.
        if Bundle.main.bundleURL.pathExtension == "app" {
            guard let url = Bundle.main.url(forResource: "vocabulary", withExtension: "sqlite") else {
                throw StoreError(message: "缺少内置词库，请重新安装应用。")
            }
            return try VocabularyStore(path: url.path)
        }
        guard let url = Bundle.module.url(forResource: "vocabulary", withExtension: "sqlite", subdirectory: "Resources") else {
            throw StoreError(message: "缺少内置词库，请重新安装应用。")
        }
        return try VocabularyStore(path: url.path)
    }
    public init(path: String, readOnly: Bool = true) throws {
        let flags = readOnly ? SQLITE_OPEN_READONLY : SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE
        guard sqlite3_open_v2(path, &db, flags | SQLITE_OPEN_FULLMUTEX, nil) == SQLITE_OK else {
            let error = failure()
            sqlite3_close(db); db = nil
            throw error
        }
        try execute("PRAGMA foreign_keys = ON")
    }
    deinit { sqlite3_close(db) }
    private func failure() -> StoreError {
        StoreError(message: db.map { String(cString: sqlite3_errmsg($0)) } ?? "无法打开词库")
    }
    func execute(_ sql: String) throws {
        guard sqlite3_exec(db, sql, nil, nil, nil) == SQLITE_OK else { throw failure() }
    }
    private func query<T>(_ sql: String, _ values: [String] = [], row: (OpaquePointer) throws -> T) throws -> [T] {
        var statement: OpaquePointer?
        guard sqlite3_prepare_v2(db, sql, -1, &statement, nil) == SQLITE_OK, let statement else { throw failure() }
        defer { sqlite3_finalize(statement) }
        let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
        for (index, value) in values.enumerated() {
            guard sqlite3_bind_text(statement, Int32(index + 1), value, -1, transient) == SQLITE_OK else { throw failure() }
        }
        var result: [T] = []
        while true {
            switch sqlite3_step(statement) {
            case SQLITE_ROW: result.append(try row(statement))
            case SQLITE_DONE: return result
            default: throw failure()
            }
        }
    }
    private func text(_ row: OpaquePointer, _ column: Int32) -> String {
        sqlite3_column_text(row, column).map { String(cString: $0) } ?? ""
    }
    public func books() throws -> [VocabularyBook] {
        try query("""
        SELECT b.id,b.name,b.category,b.subtitle,b.source,b.version,count(e.word_id)
        FROM books b LEFT JOIN entries e ON e.book_id=b.id GROUP BY b.id ORDER BY b.sort_order
        """) { row in
            guard let category = BookCategory(rawValue: text(row, 2)) else { throw StoreError(message: "词库分类格式错误") }
            return VocabularyBook(id: text(row, 0), name: text(row, 1), category: category,
                subtitle: text(row, 3), source: text(row, 4), version: text(row, 5), count: Int(sqlite3_column_int(row, 6)))
        }
    }
    public func entries(bookID: String, search: String = "") throws -> [BookEntry] {
        let term = search.trimmingCharacters(in: .whitespacesAndNewlines)
        return try query("""
        SELECT w.id,w.spelling,w.phonetic,w.part_of_speech,w.meaning,w.frequency,e.domain_meaning,e.position
        FROM entries e JOIN words w ON w.id=e.word_id WHERE e.book_id=?
        AND (?='' OR instr(lower(w.spelling),lower(?))>0 OR instr(COALESCE(NULLIF(e.domain_meaning,''),w.meaning),?)>0)
        ORDER BY e.position
        """, [bookID, term, term, term]) { row in
            let word = VocabularyWord(id: text(row, 0), spelling: text(row, 1), phonetic: text(row, 2), partOfSpeech: text(row, 3), meaning: text(row, 4), frequency: sqlite3_column_type(row, 5) == SQLITE_NULL ? nil : Int(sqlite3_column_int(row, 5)))
            return BookEntry(bookID: bookID, word: word, domainMeaning: sqlite3_column_type(row, 6) == SQLITE_NULL ? nil : text(row, 6), position: Int(sqlite3_column_int(row, 7)))
        }
    }
    public func learningDetails(wordID: String) throws -> LearningDetails {
        try query("SELECT payload FROM learning_details WHERE word_id=?", [wordID]) { row in
            guard let data = text(row, 0).data(using: .utf8) else {
                throw StoreError(message: "学习资料格式错误")
            }
            return try JSONDecoder().decode(LearningDetails.self, from: data)
        }.first ?? LearningDetails()
    }
}
