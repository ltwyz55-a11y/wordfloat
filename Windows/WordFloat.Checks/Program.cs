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
            Check("ten complete offline books and preserved domain meanings", () =>
            {
                var books = store.Books();
                Require(books.Count == 10 && books.Sum(b => b.Count) == 28591, "Book counts");
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
            Check("global dictionary lookup ranks exact words and preserves distinct domain memberships", () =>
            {
                Require(store.Lookup("  GREENHOUSE\n").First().Id=="greenhouse", "Exact case-insensitive match");
                var memberships=store.Memberships("greenhouse");
                Require(memberships.Count>1 && memberships.Any(e=>e.BookId=="automotive" && e.Meaning.Contains("车窗")), "Domain membership");
                Require(store.Lookup("车窗").Any(w=>w.Id=="greenhouse"), "Global domain lookup");
                var results=store.Lookup("a",1000);
                Require(results.Count==100 && results.Select(w=>w.Id).Distinct().Count()==100, "Bounded unique dictionary words");
                Require(store.Lookup("a",3).Count==3 && store.Lookup("a",0).Count==0, "Result limit");
                foreach(var query in new[]{" ","%_","' OR 1=1 --","zz-no-dictionary-result"})Require(store.Lookup(query).Count==0,"Literal or empty lookup");
                Require(store.Memberships("__not_a_dictionary_word__").Count==0,"Missing membership");
                var settings=new SettingsStore(Path.Combine(temporary,"lookup-settings.json"));
                var study=new StudySession(store,settings);study.Advance(4);var current=study.Current;
                store.Lookup("photograph");store.Memberships("photograph");Require(study.Current==current,"Lookup changed progress");
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
                Require(repaired.Books().Count == 10, "Repair failed");
            });
            RunStudyChecks(store, temporary);
            RunAppearanceChecks(temporary);
            RunInteractionChecks();
            RunSpeechChecks(temporary);
            Console.WriteLine($"WINDOWS_CORE_CHECKS_PASSED: {passed}");
        }
        finally { Directory.Delete(temporary, true); }
    }
    static partial void RunStudyChecks(VocabularyStore store, string temporary);
    static partial void RunAppearanceChecks(string temporary);
    static partial void RunInteractionChecks();
    static partial void RunSpeechChecks(string temporary);
}
