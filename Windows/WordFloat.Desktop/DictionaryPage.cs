using System;
using System.Linq;
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
    private readonly Brush ink,muted;
    private readonly ListBox results;
    private readonly ContentControl preview=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};
    private readonly TextBlock count;
    internal DictionaryWord? SelectedWord => results.SelectedItem as DictionaryWord;
    internal DictionaryPage(StudySession study,Pronunciation audio,Brush ink,Brush muted,Brush panel)
    {
        this.study=study;this.audio=audio;this.ink=ink;this.muted=muted;
        RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition());
        ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});ColumnDefinitions.Add(new ColumnDefinition());
        var title=UI.Text("词典查询",25,ink);title.Margin=new Thickness(0,0,0,8);SetColumnSpan(title,2);Children.Add(title);
        count=UI.Text("",11,muted);count.Margin=new Thickness(0,0,0,16);SetRow(count,1);SetColumnSpan(count,2);Children.Add(count);
        results=new ListBox{Background=panel,Foreground=ink,BorderThickness=new Thickness(0),Margin=new Thickness(0,0,18,0),HorizontalContentAlignment=HorizontalAlignment.Stretch};
        VirtualizingPanel.SetIsVirtualizing(results,true);VirtualizingPanel.SetVirtualizationMode(results,VirtualizationMode.Recycling);
        var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(MarginProperty,new Thickness(6,8,6,8));
        var spelling=new FrameworkElementFactory(typeof(TextBlock));spelling.SetBinding(TextBlock.TextProperty,new Binding("Spelling"));spelling.SetValue(TextBlock.FontSizeProperty,14.0);spelling.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);row.AppendChild(spelling);
        var meaning=new FrameworkElementFactory(typeof(TextBlock));meaning.SetBinding(TextBlock.TextProperty,new Binding("Meaning"));meaning.SetValue(TextBlock.FontSizeProperty,11.0);meaning.SetValue(TextBlock.ForegroundProperty,muted);meaning.SetValue(MarginProperty,new Thickness(0,5,0,0));meaning.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);meaning.SetValue(MaxHeightProperty,34.0);meaning.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);row.AppendChild(meaning);
        results.ItemTemplate=new DataTemplate{VisualTree=row};results.SelectionChanged+=(_,_)=>ShowDefinition();SetRow(results,2);Children.Add(results);
        SetRow(preview,2);SetColumn(preview,1);Children.Add(preview);
        Unloaded+=(_,_)=>audio.Stop();
    }
    internal void Refresh(string query)
    {
        audio.Stop();
        try
        {
            var matches=study.Store.Lookup(query);results.ItemsSource=matches;
            count.Text=matches.Count==100?"前 100 条结果":$"{matches.Count} 个词条";
            results.SelectedIndex=matches.Count>0?0:-1;
            if(matches.Count==0)preview.Content=UI.Text("未找到词条",14,muted);
        }
        catch(Exception error)when(error is InvalidOperationException or System.Text.Json.JsonException)
        {results.ItemsSource=null;preview.Content=UI.Text("无法查词："+error.Message,14,muted);}
    }
    private void ShowDefinition()
    {
        audio.Stop();preview.Content=null;
        if(SelectedWord is not { } word)return;
        try
        {
            var content=new StackPanel{Margin=new Thickness(4,0,10,16)};
            void Paragraph(string text,double size=13,Brush? color=null){var p=UI.Text(text,size,color??ink);p.Margin=new Thickness(0,0,0,12);content.Children.Add(p);}
            void Section(string title){content.Children.Add(new Border{Height=1,Background=UI.Brush(NativeWindow.IsDark?"#445248":"#D7DED6"),Margin=new Thickness(0,8,0,14)});Paragraph(title,11,muted);}
            void Source(string title,string url){var button=UI.Button(title,()=>UI.OpenLink(url),muted);button.HorizontalAlignment=HorizontalAlignment.Left;content.Children.Add(button);}
            Paragraph(word.Spelling,28);Paragraph(string.Join("   ",new[]{word.PartOfSpeech,word.Phonetic}.Where(s=>s.Length>0)),12,muted);
            var play=UI.SpeechButton("听发音",()=>audio.Speak(word.Spelling),ink,"dictionary.speak");play.Margin=new Thickness(0,0,0,16);content.Children.Add(play);
            Paragraph(word.Meaning,15);
            var memberships=study.Store.Memberships(word.Id);
            Paragraph("收录于："+string.Join("、",memberships.Select(e=>study.Books.FirstOrDefault(b=>b.Id==e.BookId)?.Name??e.BookId)),11,muted);
            foreach(var entry in memberships.Where(e=>!string.IsNullOrWhiteSpace(e.DomainMeaning) && e.Meaning!=word.Meaning))
            {Section(study.Books.FirstOrDefault(b=>b.Id==entry.BookId)?.Name??entry.BookId);Paragraph(entry.Meaning);}
            var details=study.Store.Details(word.Id);
            if(details.DictionaryMeaning.Length>0 && details.DictionaryMeaning!=word.Meaning){Section("词典完整释义");Paragraph(details.DictionaryMeaning);}
            Section("词根与词源");
            if(details.Etymologies.Length==0)Paragraph("暂无可靠词源资料",12,muted);
            else {foreach(var note in details.Etymologies)Paragraph(note.Text);Source("维基词典 · CC BY-SA 4.0",details.Etymologies[0].SourceURL);}
            if(details.Forms.Length>0){Section("词形变化");foreach(var form in details.Forms)Paragraph(form.Label+"  "+form.Text);}
            Section("中英例句 · 通用用法");
            if(details.Examples.Length==0)Paragraph("暂无双语例句",12,muted);
            foreach(var example in details.Examples)
            {
                Paragraph(example.English);Paragraph(example.Chinese,12,muted);
                var buttons=new WrapPanel();buttons.Children.Add(UI.Button("英文",()=>audio.Speak(example.English),ink));buttons.Children.Add(UI.Button("中文",()=>audio.Speak(example.Chinese,true),ink));buttons.Children.Add(UI.Button("中英连读",()=>audio.SpeakExample(example),ink));content.Children.Add(buttons);
                Paragraph(example.Attribution,10,muted);Source("查看来源",example.SourceURL);
                Source("例句许可",example.Id.StartsWith("wiki-",StringComparison.Ordinal)?"https://creativecommons.org/licenses/by-sa/4.0/":"https://creativecommons.org/licenses/by/2.0/fr/");
            }
            if(details.Senses.Length>0){Section("词典补充义项");foreach(var sense in details.Senses)Paragraph(string.Join(" · ",new[]{sense.PartOfSpeech,sense.Text}.Where(s=>s.Length>0)));Source("维基词典 · CC BY-SA 4.0",details.Senses[0].SourceURL);}
            if(details.EnglishDefinition.Length>0){Section("英英释义");Paragraph(details.EnglishDefinition);}
            Section("内容来源");Paragraph("词本释义：对应词本原始资料\n通用词典及词形：ECDICT · MIT\n词源及补充义项：维基词典贡献者\n例句可能使用不同词义；来源语料未经逐句审校。",10,muted);Source("词源与补充义项许可","https://creativecommons.org/licenses/by-sa/4.0/");
            preview.Content=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        }
        catch(Exception error)when(error is InvalidOperationException or System.Text.Json.JsonException){preview.Content=UI.Text("无法读取释义："+error.Message,14,muted);}
    }
}
