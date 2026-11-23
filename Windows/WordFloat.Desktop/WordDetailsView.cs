using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class WordDetailsView
{
    internal static ScrollViewer ForWord(StudySession study, Pronunciation audio, DictionaryWord word) =>
        Create(study, audio, word.Id, word.Spelling, word.Phonetic, word.PartOfSpeech, word.Meaning, "dictionary.speak");
    internal static ScrollViewer ForEntry(StudySession study, Pronunciation audio, BookEntry entry) =>
        Create(study, audio, entry.Id, entry.Spelling, entry.Phonetic, entry.PartOfSpeech, entry.Meaning, "word.speak");

    private static ScrollViewer Create(StudySession study, Pronunciation audio, string id, string spelling,
        string phonetic, string partOfSpeech, string meaning, string speakerId)
    {
        var ink = LibraryTheme.Ink; var muted = LibraryTheme.Muted;
        var content = new StackPanel { Margin = new Thickness(8, 0, 12, 16) };
        TextBlock Paragraph(Panel parent, string text, double size = 13, Brush? color = null)
        {
            var p = UI.Text(text, size, color ?? ink); p.Margin = new Thickness(0, 0, 0, 12); parent.Children.Add(p); return p;
        }
        StackPanel Section(string title, string tag)
        {
            var body = new StackPanel { Margin = new Thickness(2, 14, 2, 8) };
            var expander = new Expander { Header = title, Content = body, IsExpanded = false, FontSize = 13,
                Foreground = ink, Padding = new Thickness(0, 12, 0, 12), Tag = tag };
            System.Windows.Automation.AutomationProperties.SetName(expander, title);
            content.Children.Add(new Border { BorderBrush = LibraryTheme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = expander });
            return body;
        }
        void Source(Panel parent, string title, string url)
        {
            var link = UI.Button(title, () => UI.OpenLink(url), LibraryTheme.Accent);
            link.FontSize = 12; link.HorizontalAlignment = HorizontalAlignment.Left; parent.Children.Add(link);
        }
        var title = Paragraph(content, spelling, 28); title.FontWeight = FontWeights.Bold;
        var metadata = string.Join("   ", new[] { partOfSpeech, phonetic }.Where(s => s.Length > 0));
        if (metadata.Length > 0) Paragraph(content, metadata, 12, muted);
        var play = UI.SpeechButton("听发音", () => audio.Speak(spelling), LibraryTheme.Accent, speakerId);
        play.Margin = new Thickness(0, 0, 0, 20); content.Children.Add(play);
        Paragraph(content, meaning, 15);
        var details = study.Store.Details(id);
        if (details.DictionaryMeaning.Length > 0 && details.DictionaryMeaning != meaning)
            Paragraph(Section("完整释义", "details.meanings"), details.DictionaryMeaning);
        if (details.Etymologies.Length > 0)
        {
            var section = Section("词根与词源", "details.roots");
            foreach (var note in details.Etymologies)
            { Paragraph(section, note.Text); Source(section, "维基词典 · CC BY-SA 4.0", note.SourceURL); }
        }
        if (details.Forms.Length > 0)
        {
            var section = Section("词形变化", "details.forms");
            foreach (var form in details.Forms) Paragraph(section, form.Label + "  " + form.Text);
        }
        if (details.Examples.Length > 0)
        {
            var section = Section("双语例句", "details.examples");
            foreach (var example in details.Examples)
            {
                Paragraph(section, example.English); Paragraph(section, example.Chinese, 12, muted);
                var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
                buttons.Children.Add(UI.Button("英文", () => audio.Speak(example.English), LibraryTheme.Accent, "example.english." + example.Id));
                buttons.Children.Add(UI.Button("中文", () => audio.Speak(example.Chinese, true), LibraryTheme.Accent, "example.chinese." + example.Id));
                buttons.Children.Add(UI.Button("中英连读", () => audio.SpeakExample(example), LibraryTheme.Accent, "example.pair." + example.Id));
                section.Children.Add(buttons); Paragraph(section, example.Attribution, 12, muted);
                Source(section, "例句来源", example.SourceURL);
                Source(section, "例句许可", example.Id.StartsWith("wiki-", StringComparison.Ordinal) ? "https://creativecommons.org/licenses/by-sa/4.0/" : "https://creativecommons.org/licenses/by/2.0/fr/");
            }
        }
        if (details.Senses.Length > 0)
        {
            var section = Section("补充义项", "details.senses");
            foreach (var sense in details.Senses)
            {
                Paragraph(section, string.Join(" · ", new[] { sense.PartOfSpeech, sense.Text }.Where(s => s.Length > 0)));
                Source(section, "维基词典 · CC BY-SA 4.0", sense.SourceURL);
            }
        }
        if (details.EnglishDefinition.Length > 0)
            Paragraph(Section("英英释义", "details.english"), details.EnglishDefinition);
        var memberships = study.Store.Memberships(id);
        var domains = memberships.Where(e => !string.IsNullOrWhiteSpace(e.DomainMeaning) && e.Meaning != meaning).ToArray();
        if (domains.Length > 0)
        {
            var section = Section("领域释义", "details.domains");
            foreach (var entry in domains)
            {
                Paragraph(section, study.Books.FirstOrDefault(b => b.Id == entry.BookId)?.Name ?? entry.BookId, 12, muted);
                Paragraph(section, entry.Meaning);
            }
        }
        var sources = Section("来源与许可", "details.sources");
        Paragraph(sources, "收录于：" + string.Join("、", memberships.Select(e => study.Books.FirstOrDefault(b => b.Id == e.BookId)?.Name ?? e.BookId)), 12, muted);
        Paragraph(sources, "词本释义：对应词本原始资料\n通用词典及词形：ECDICT · MIT\n词源及补充义项：维基词典贡献者\n例句可能使用不同词义；来源语料未经逐句审校。", 12, muted);
        Source(sources, "词源与补充义项许可", "https://creativecommons.org/licenses/by-sa/4.0/");
        return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
}
