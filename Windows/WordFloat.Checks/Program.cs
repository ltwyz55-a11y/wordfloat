using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using WordFloat.Core;

namespace WordFloat.Checks;

internal static partial class Program
{
    private static int passed;
    internal static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Check(string name, Action check) { check(); passed++; Console.WriteLine("PASS " + name); }
    private static void Main(string[] args)
    {
        var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var temporary = Path.Combine(Path.GetTempPath(), "WordFloat.Checks." + Guid.NewGuid());
        try
        {
            var path = VocabularyStore.ExtractBundled(temporary);
            using var store = new VocabularyStore(path);
            Check("nine complete offline books and preserved domain meanings", () =>
            {
                var books = store.Books();
                Require(books.Count == 9 && books.Sum(b => b.Count) == 27642, "Book counts");
                foreach (var book in books)
                {
                    var entries = store.Entries(book.Id);
                    Require(entries.Count == book.Count && entries.Select(e => e.Id).Distinct().Count() == book.Count, book.Id);
                    Require(entries.All(e => !string.IsNullOrWhiteSpace(e.Meaning)), "Empty meaning");
                }
                Require(store.Entries("automotive").Single(e => e.Id == "greenhouse").Meaning.Contains("车窗"), "Domain meaning lost");
            });
            Check("literal Chinese and English search and invalid book", () =>
            {
                Require(store.Entries("automotive", "GREENHOUSE").Count == 1, "English case folding");
                Require(store.Entries("automotive", "车窗").Count > 0, "Chinese search");
                Require(store.Entries("automotive", "' OR 1=1 --").Count == 0, "Bound query");
                Require(store.Entries("missing").Count == 0, "Missing book");
            });
            Check("all 12304 learning payloads equal the Mac source", () =>
            {
                using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Data/learning-details.json")));
                var count = 0;
                foreach (var item in source.RootElement.GetProperty("words").EnumerateObject())
                {
                    var expected = JsonSerializer.Deserialize<LearningDetails>(item.Value.GetRawText(), JsonDefaults.Options)!;
                    var actual = store.Details(item.Name);
                    Require(JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(actual), item.Name);
                    count++;
                }
                Require(count == 12304 && store.Details("__not_a_dictionary_word__").Examples.Length == 0, "Payload count");
                Require(store.Details("photograph").Etymologies.Any(e => e.Text.Contains("photo-")), "Missing sourced root");
            });
            Check("damaged extracted database is repaired from bundled resource", () =>
            {
                var repair = Path.Combine(temporary, "repair");
                var first = VocabularyStore.ExtractBundled(repair);
                File.WriteAllText(first, "damaged");
                Require(VocabularyStore.ExtractBundled(repair) == first, "Stable data path");
                using var repaired = new VocabularyStore(first);
                Require(repaired.Books().Count == 9, "Repair failed");
            });
            RunStudyChecks(store, temporary);
            Console.WriteLine($"WINDOWS_CORE_CHECKS_PASSED: {passed}");
        }
        finally { Directory.Delete(temporary, true); }
    }
    static partial void RunStudyChecks(VocabularyStore store, string temporary);
}
