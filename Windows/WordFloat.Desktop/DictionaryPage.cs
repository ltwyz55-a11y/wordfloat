using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal sealed class DictionaryPage : Grid
{
    private readonly StudySession study;
    private readonly Pronunciation audio;
    private readonly Brush ink, muted;
    private readonly ListBox results;
    private readonly ContentControl preview = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly TextBlock count;
    internal DictionaryWord? SelectedWord => results.SelectedItem as DictionaryWord;
    internal DictionaryPage(StudySession study, Pronunciation audio, Brush ink, Brush muted, Brush panel)
    {
        this.study = study; this.audio = audio; this.ink = ink; this.muted = muted;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); ColumnDefinitions.Add(new ColumnDefinition());
        count = UI.Text("", 12, muted); count.Margin = new Thickness(0, 0, 0, 16); SetColumnSpan(count, 2); Children.Add(count);
        results = new ListBox { Background = Brushes.Transparent, Foreground = ink, BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 18, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        LibraryTheme.StyleList(results);
        var row = new FrameworkElementFactory(typeof(StackPanel));
        var spelling = new FrameworkElementFactory(typeof(TextBlock)); spelling.SetBinding(TextBlock.TextProperty, new Binding("Spelling"));
        spelling.SetValue(TextBlock.FontSizeProperty, 14.0); spelling.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        spelling.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); row.AppendChild(spelling);
        var meaning = new FrameworkElementFactory(typeof(TextBlock)); meaning.SetBinding(TextBlock.TextProperty, new Binding("Meaning"));
        meaning.SetValue(TextBlock.FontSizeProperty, 12.0); meaning.SetValue(TextBlock.ForegroundProperty, muted);
        meaning.SetValue(MarginProperty, new Thickness(0, 5, 0, 0)); meaning.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        meaning.SetValue(MaxHeightProperty, 36.0); meaning.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); row.AppendChild(meaning);
        results.ItemTemplate = new DataTemplate { VisualTree = row }; results.SelectionChanged += (_, _) => ShowDefinition();
        SetRow(results, 1); Children.Add(results); SetRow(preview, 1); SetColumn(preview, 1); Children.Add(preview);
        Unloaded += (_, _) => audio.Stop();
    }
    internal void Refresh(string query)
    {
        audio.Stop();
        try
        {
            var matches = study.Store.Lookup(query); results.ItemsSource = matches;
            count.Visibility = string.IsNullOrWhiteSpace(query) ? Visibility.Collapsed : Visibility.Visible;
            count.Text = matches.Count == 100 ? "前 100 条结果" : $"{matches.Count} 个词条";
            results.SelectedIndex = matches.Count > 0 ? 0 : -1;
            if (matches.Count == 0) preview.Content = UI.Text(string.IsNullOrWhiteSpace(query) ? "输入单词或中文释义" : "未找到词条", 14, muted);
        }
        catch (Exception error) when (error is InvalidOperationException or System.Text.Json.JsonException)
        { results.ItemsSource = null; preview.Content = UI.Text("无法查词：" + error.Message, 13, muted); }
    }
    private void ShowDefinition()
    {
        audio.Stop(); preview.Content = null;
        if (SelectedWord is not { } word) return;
        try { preview.Content = WordDetailsView.ForWord(study, audio, word); }
        catch (Exception error) when (error is InvalidOperationException or System.Text.Json.JsonException)
        { preview.Content = UI.Text("无法读取释义：" + error.Message, 13, muted); }
    }
}
