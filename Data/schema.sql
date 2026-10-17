PRAGMA foreign_keys = ON;
CREATE TABLE books (
 id TEXT PRIMARY KEY, name TEXT NOT NULL, category TEXT NOT NULL CHECK(category IN ('exam','professional')),
 subtitle TEXT NOT NULL DEFAULT '', source TEXT NOT NULL, version TEXT NOT NULL, sort_order INTEGER NOT NULL
);
CREATE TABLE words (
 id TEXT PRIMARY KEY, spelling TEXT NOT NULL, phonetic TEXT NOT NULL DEFAULT '',
 part_of_speech TEXT NOT NULL DEFAULT '', meaning TEXT NOT NULL CHECK(length(trim(meaning)) > 0), frequency INTEGER
);
CREATE TABLE entries (
 book_id TEXT NOT NULL REFERENCES books(id), word_id TEXT NOT NULL REFERENCES words(id),
 domain_meaning TEXT, position INTEGER NOT NULL,
 PRIMARY KEY(book_id, word_id), UNIQUE(book_id, position)
);
CREATE INDEX entries_order ON entries(book_id, position);
PRAGMA user_version = 1;
