using System;
using System.IO;
using System.Linq;
using WordFloat.Core;

namespace WordFloat.Checks;

internal static partial class Program
{
    static partial void RunStudyChecks(VocabularyStore store, string temporary)
    {
        var path = Path.Combine(temporary, "settings.json");
        Check("custom card dimensions survive restart and invalid settings recover", () =>
        {
            var sizePath=Path.Combine(temporary,"card-size.json");var prefs=new SettingsStore(sizePath);
            prefs.Value.CardSize.Remember(380,240,false);prefs.Value.CardSize.Remember(380,350,true);prefs.Save();
            var restored=new SettingsStore(sizePath).Value.CardSize;
            Require(restored.Size(false)==new CardSize(380,240)&&restored.Size(true)==new CardSize(380,350),"Stored card dimensions");
            File.WriteAllText(sizePath,"{\"CardSize\":null}");
            Require(new SettingsStore(sizePath).Value.CardSize.Size(false)==new CardSize(260,150),"Null legacy settings");
            File.WriteAllText(sizePath,"{\"CardSize\":{\"Width\":9999,\"CompactHeight\":-1,\"ExpandedHeight\":1}}");
            Require(new SettingsStore(sizePath).Value.CardSize.Size(false)==new CardSize(520,130)&&new SettingsStore(sizePath).Value.CardSize.ExpandedHeight==200,"Stored bounds normalization");
        });
        Check("circular navigation and silent reveal state", () =>
        {
            var study = new StudySession(store, new SettingsStore(path));
            study.SelectBook("automotive");
            var first = study.Current!.Id;
            var changes = 0; study.ChangingWord += () => changes++;
            study.Revealed = true;
            Require(changes == 0 && study.Revealed, "Reveal must not request word change or speech");
            study.Advance(-1);
            Require(study.Index == study.Entries.Count - 1 && !study.Revealed && changes == 1, "Previous wrap");
            study.Advance(1);
            Require(study.Current!.Id == first, "Next wrap");
            Require(CircularIndex.Next(0, int.MinValue, 3) == 1 && CircularIndex.Next(0, 1, 0) == 0, "Safe normalization");
        });
        Check("independent book positions survive process restart", () =>
        {
            var study = new StudySession(store, new SettingsStore(path));
            study.SelectBook("automotive"); study.Jump("greenhouse");
            study.SelectBook("cet4"); study.Jump("photograph");
            var restored = new StudySession(store, new SettingsStore(path));
            Require(restored.SelectedBookId == "cet4" && restored.Current!.Id == "photograph", "Current position restore");
            restored.SelectBook("automotive");
            Require(restored.Current!.Id == "greenhouse", "Independent position restore");
            Require(!restored.Revealed, "Never restore revealed state");
        });
        Check("stale positions and malformed preferences recover", () =>
        {
            var prefs = new SettingsStore(path);
            prefs.Value.SelectedBookId = "deleted-book"; prefs.Value.Positions["zk"] = "deleted-word"; prefs.Save();
            var study = new StudySession(store, new SettingsStore(path));
            Require(study.SelectedBookId == store.Books().First().Id && study.Index == 0, "Stale fallback");
            File.WriteAllText(path, "{broken");
            Require(new StudySession(store, new SettingsStore(path)).Current is not null, "Corrupt recovery");
            File.WriteAllText(path, "{\"Positions\":null,\"Accent\":\"bad\"}");
            Require(new SettingsStore(path).Value.Accent == "en-US", "Invalid accent");
        });
    }
}
