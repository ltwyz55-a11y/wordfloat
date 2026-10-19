using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace WordFloat.Core;

public sealed class VocabularyStore : IDisposable
{
    private readonly NativeSqlite database;
    public VocabularyStore(string path) { database = new NativeSqlite(path); }
    public static string ExtractBundled(string directory)
    {
        using var source = typeof(VocabularyStore).Assembly.GetManifestResourceStream("WordFloat.Vocabulary") ?? throw new InvalidOperationException("缺少内置词库，请重新安装浮词。");
        var hash = Convert.ToHexString(SHA256.HashData(source));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "vocabulary-" + hash[..16] + ".sqlite");
        if (File.Exists(path))
        {
            using var existing = File.OpenRead(path);
            if (Convert.ToHexString(SHA256.HashData(existing)) == hash) return path;
        }
        source.Position = 0;
        var temporary = path + "." + Guid.NewGuid() + ".tmp";
        try
        {
            using (var target = File.Create(temporary)) source.CopyTo(target);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return path;
    }
    public IReadOnlyList<VocabularyBook> Books() => database.Read("""
        SELECT b.id,b.name,b.category,b.subtitle,b.source,b.version,count(e.word_id)
        FROM books b LEFT JOIN entries e ON e.book_id=b.id GROUP BY b.id ORDER BY b.sort_order
        """).Select(r => new VocabularyBook(r[0]!, r[1]!, r[2]!, r[3]!, r[4]!, r[5]!, int.Parse(r[6]!, CultureInfo.InvariantCulture))).ToArray();
    public IReadOnlyList<BookEntry> Entries(string bookId, string search = "")
    {
        var query = search.Trim();
        return database.Read("""
            SELECT e.book_id,w.id,w.spelling,w.phonetic,w.part_of_speech,w.meaning,w.frequency,e.domain_meaning,e.position
            FROM entries e JOIN words w ON w.id=e.word_id WHERE e.book_id=?
            AND (?='' OR instr(lower(w.spelling),lower(?))>0 OR instr(COALESCE(NULLIF(e.domain_meaning,''),w.meaning),?)>0)
            ORDER BY e.position
            """, bookId, query, query, query).Select(r => new BookEntry(r[0]!, r[1]!, r[2]!, r[3]!, r[4]!, r[5]!,
                r[6] is null ? null : int.Parse(r[6]!, CultureInfo.InvariantCulture), r[7], int.Parse(r[8]!, CultureInfo.InvariantCulture))).ToArray();
    }
    public LearningDetails Details(string wordId)
    {
        var rows = database.Read("SELECT payload FROM learning_details WHERE word_id=?", wordId);
        return rows.Count == 0 ? new LearningDetails() : JsonSerializer.Deserialize<LearningDetails>(rows[0][0]!, JsonDefaults.Options) ?? new LearningDetails();
    }
    public void Dispose() => database.Dispose();
}
