using System;
using System.Text.Json;

namespace WordFloat.Core;

public sealed record VocabularyBook(string Id, string Name, string Category, string Subtitle, string Source, string Version, int Count)
{
    public string CategoryTitle => Category == "exam" ? "考试词汇" : "专业领域";
    public override string ToString() => $"{Name}  ·  {Count:N0} 词";
}
public sealed record BookEntry(string BookId, string Id, string Spelling, string Phonetic, string PartOfSpeech, string DictionaryMeaning, int? Frequency, string? DomainMeaning, int Position)
{
    public string Meaning => string.IsNullOrWhiteSpace(DomainMeaning) ? DictionaryMeaning : DomainMeaning;
    public override string ToString() => Spelling;
}
public sealed record DictionaryWord(string Id, string Spelling, string Phonetic, string PartOfSpeech, string Meaning, int? Frequency);
public sealed record DictionarySense(string PartOfSpeech, string Text, string SourceURL);
public sealed record EtymologyNote(string Text, string SourceURL);
public sealed record BilingualExample(string Id, string English, string Chinese, string Attribution, string SourceURL);
public sealed record WordForm(string Label, string Text);
public sealed record LearningDetails
{
    public string DictionaryMeaning { get; init; } = "";
    public string EnglishDefinition { get; init; } = "";
    public DictionarySense[] Senses { get; init; } = [];
    public EtymologyNote[] Etymologies { get; init; } = [];
    public BilingualExample[] Examples { get; init; } = [];
    public WordForm[] Forms { get; init; } = [];
}
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
}
