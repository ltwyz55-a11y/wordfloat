using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class CardContent
{
    internal static FrameworkElement Build(StudySession study, Pronunciation audio, CardStyle style, bool revealed, bool preview = false)
    {
        var ink = UI.Brush(style.Text);
        var muted = UI.Brush(style.Text,0.77);
        var entry = study.Current;
        if (entry is null) return UI.Text("选择一个词本",16,muted);
        var stack = new StackPanel { Margin = new Thickness(18,revealed?6:0,18,revealed?14:0), VerticalAlignment = VerticalAlignment.Center };
        var word = UI.Text(entry.Spelling,entry.Spelling.Length > 23 ? 23 : 30,ink);
        word.FontWeight = FontWeights.Medium; word.TextAlignment = TextAlignment.Center;
        word.Width = 224;
        stack.SizeChanged+=(_,e)=>word.Width=Math.Max(1,e.NewSize.Width);
        stack.Children.Add(new Viewbox { Child=word, Stretch=Stretch.Uniform, StretchDirection=StretchDirection.DownOnly, MaxHeight=revealed?100:84 });
        if (!revealed) return stack;
        var phonetic = UI.Text(string.Join("   ",new[] {entry.PartOfSpeech,entry.Phonetic}.Where(s => s.Length>0)),12,muted);
        phonetic.TextAlignment = TextAlignment.Center; phonetic.Margin = new Thickness(0,8,0,9); stack.Children.Add(phonetic);
        var meaning = UI.Text(entry.Meaning,13,ink); meaning.TextAlignment = TextAlignment.Center; stack.Children.Add(meaning);
        var hint = UI.Text("↓ 下滑查看词源与例句",10,muted); hint.TextAlignment = TextAlignment.Center; hint.Margin = new Thickness(0,10,0,0); stack.Children.Add(hint);
        var details = study.Details;
        void Paragraph(string text, Brush? color = null, double size = 12)
        {
            var block = UI.Text(text,size,color ?? muted); block.Margin = new Thickness(0,0,0,8); stack.Children.Add(block);
        }
        void Section(string title)
        {
            stack.Children.Add(new Border { Height=1, Background=UI.Brush(style.Text,0.12), Margin=new Thickness(0,10,0,10) });
            var heading = UI.Text(title,11,ink); heading.FontWeight=FontWeights.SemiBold; heading.Margin=new Thickness(0,0,0,8); stack.Children.Add(heading);
        }
        Button Action(string title, Action action, string id)
        {
            var button = UI.Button(title,preview?()=>{}:action,ink,id); button.Focusable=false; button.FontSize=11; button.Padding=new Thickness(3,5,3,5); return button;
        }
        void Source(string title, string url, string id)
        {
            var button = Action(title,()=>UI.OpenLink(url),id); button.HorizontalAlignment=HorizontalAlignment.Left; button.Foreground=muted; stack.Children.Add(button);
        }
        if (details.DictionaryMeaning.Length > 0 && details.DictionaryMeaning != entry.Meaning)
        { Section("词典完整释义"); Paragraph(details.DictionaryMeaning); }
        Section("词根与词源");
        if (details.Etymologies.Length == 0) Paragraph("暂无可靠词源资料");
        else
        {
            foreach (var note in details.Etymologies) Paragraph(note.Text);
            Source("维基词典 · CC BY-SA 4.0",details.Etymologies[0].SourceURL,"etymology.source");
        }
        if (details.Forms.Length>0)
        {
            Section("词形变化"); foreach (var form in details.Forms) Paragraph(form.Label+"  "+form.Text);
        }
        Section("中英例句 · 通用用法");
        if (details.Examples.Length == 0) Paragraph("暂无双语例句");
        foreach (var example in details.Examples)
        {
            Paragraph(example.English,ink); Paragraph(example.Chinese);
            var buttons = new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(-3,0,0,4) };
            buttons.Children.Add(Action("▶ 英文",()=>audio.Speak(example.English),"example.english."+example.Id));
            buttons.Children.Add(Action("▶ 中文",()=>audio.Speak(example.Chinese,true),"example.chinese."+example.Id));
            buttons.Children.Add(Action("▶ 中英连读",()=>audio.SpeakExample(example),"example.pair."+example.Id));
            stack.Children.Add(buttons);
            Paragraph(example.Attribution,size:9);
            var links = new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(-3,0,0,12) };
            links.Children.Add(Action("查看来源",()=>UI.OpenLink(example.SourceURL),"example.source."+example.Id));
            links.Children.Add(Action("许可",()=>UI.OpenLink(example.Id.StartsWith("wiki-",StringComparison.Ordinal)?"https://creativecommons.org/licenses/by-sa/4.0/":"https://creativecommons.org/licenses/by/2.0/fr/"),"example.license."+example.Id));
            stack.Children.Add(links);
        }
        if (details.Senses.Length > 0)
        {
            Section("词典补充义项");
            foreach (var sense in details.Senses) Paragraph(string.Join(" · ",new[] {sense.PartOfSpeech,sense.Text}.Where(s=>s.Length>0)));
            Source("维基词典 · CC BY-SA 4.0",details.Senses[0].SourceURL,"senses.source");
        }
        if (details.EnglishDefinition.Length>0) { Section("英英释义"); Paragraph(details.EnglishDefinition); }
        Paragraph("词本释义：对应词本原始资料\n通用词典及词形：ECDICT · MIT\n词源及补充义项：维基词典贡献者\n例句可能使用不同词义；来源语料未经逐句审校。",size:9);
        Source("词源及补充义项许可","https://creativecommons.org/licenses/by-sa/4.0/","dictionary.license");
        return CardScrolling.Create(stack,style);
    }
    internal static Border Preview(StudySession study, Pronunciation audio, AppearanceController appearance, bool revealed)
    {
        var style = appearance.Style.Resolved(NativeWindow.IsDark);
        var surface = new Border { Width=260,Height=revealed?220:150 };
        UI.PaintSurface(surface,style,appearance.WallpaperPath);
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height=new GridLength(24) });
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height=new GridLength(36) });
        var handle = new Border { Width=28,Height=3,CornerRadius=new CornerRadius(2),Background=UI.Brush(style.Text,0.25) }; grid.Children.Add(handle);
        var body = Build(study,audio,style,revealed,true); Grid.SetRow(body,1); grid.Children.Add(body);
        var speaker = CardIcons.Speaker(UI.Brush(style.Text,0.78)); speaker.HorizontalAlignment=HorizontalAlignment.Center; speaker.VerticalAlignment=VerticalAlignment.Center;
        Grid.SetRow(speaker,2); grid.Children.Add(speaker); surface.Child=grid; return surface;
    }
}
