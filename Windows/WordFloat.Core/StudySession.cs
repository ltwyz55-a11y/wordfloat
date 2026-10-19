using System;
using System.Collections.Generic;
using System.Linq;

namespace WordFloat.Core;

public sealed class StudySession
{
    public VocabularyStore Store { get; }
    public SettingsStore Settings { get; }
    public IReadOnlyList<VocabularyBook> Books { get; }
    public IReadOnlyList<BookEntry> Entries { get; private set; } = [];
    public string SelectedBookId { get; private set; } = "";
    public int Index { get; private set; }
    public BookEntry? Current => Index >= 0 && Index < Entries.Count ? Entries[Index] : null;
    public VocabularyBook? CurrentBook => Books.FirstOrDefault(b => b.Id == SelectedBookId);
    public LearningDetails Details { get; private set; } = new();
    public event Action? ChangingWord;
    public event Action? Changed;
    private bool revealed;
    public bool Revealed
    {
        get => revealed;
        set { if (revealed == value) return; revealed = value; Changed?.Invoke(); }
    }
    public StudySession(VocabularyStore store, SettingsStore settings)
    {
        Store = store; Settings = settings; Books = store.Books();
        var book = Books.FirstOrDefault(b => b.Id == settings.Value.SelectedBookId) ?? Books.FirstOrDefault();
        if (book is not null) SelectBook(book.Id);
    }
    public void SelectBook(string id)
    {
        if (!Books.Any(b => b.Id == id)) throw new ArgumentException("找不到这个词本", nameof(id));
        var loaded = Store.Entries(id);
        ChangingWord?.Invoke();
        Remember();
        SelectedBookId = id; Entries = loaded;
        var saved = Settings.Value.Positions.GetValueOrDefault(id);
        Index = loaded.Select((e, i) => (e, i)).FirstOrDefault(x => x.e.Id == saved).i;
        RefreshWord();
    }
    public void Advance(int delta)
    {
        if (Entries.Count == 0) return;
        ChangingWord?.Invoke();
        Index = CircularIndex.Next(Index, delta, Entries.Count);
        RefreshWord();
    }
    public void Jump(string wordId)
    {
        var index = Entries.Select((e, i) => (e, i)).Where(x => x.e.Id == wordId).Select(x => (int?)x.i).FirstOrDefault();
        if (index is null) return;
        ChangingWord?.Invoke(); Index = index.Value; RefreshWord();
    }
    private void RefreshWord()
    {
        revealed = false;
        Details = Current is null ? new() : Store.Details(Current.Id);
        Remember();
        Settings.Value.SelectedBookId = SelectedBookId;
        Settings.Save(); Changed?.Invoke();
    }
    private void Remember() { if (Current is not null) Settings.Value.Positions[SelectedBookId] = Current.Id; }
}
public static class CircularIndex
{
    public static int Next(int index, int delta, int count) => count <= 0 ? 0 : (int)((((long)index + delta) % count + count) % count);
}
