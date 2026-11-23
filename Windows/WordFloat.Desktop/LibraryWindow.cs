using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal sealed class LibraryWindow : Window
{
    private readonly StudySession study;
    private readonly Pronunciation audio;
    private readonly AppearanceController appearance;
    private readonly Action returnToCard, quit;
    private readonly DispatcherTimer searchTimer = new() { Interval=TimeSpan.FromMilliseconds(150) };
    private ContentControl center = new();
    private TextBlock progress = new(), status = new();
    private string category="all", page="books", bookQuery="", wordQuery="";
    private bool ready, allowClose;
    private TextBox lookupInput = new();
    private string lookupQuery = "";
    private DictionaryPage? dictionaryPage;
    private bool HasLookup => lookupQuery.Trim().Length > 0;
    private Brush Ink => UI.Brush(NativeWindow.IsDark?"#EDF3EF":"#253B31");
    private Brush Muted => UI.Brush(NativeWindow.IsDark?"#A6B6AC":"#738078");
    private Brush Panel => UI.Brush(NativeWindow.IsDark?"#29322D":"#FFFFFF");
    internal Func<FrameworkElement>? AppearanceFactory { get; set; }
    internal LibraryWindow(StudySession study,Pronunciation audio,AppearanceController appearance,Action returnToCard,Action quit)
    {
        this.study=study;this.audio=audio;this.appearance=appearance;this.returnToCard=returnToCard;this.quit=quit;
        Title="浮词 · 单词本";Width=960;Height=700;MinWidth=840;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");
        searchTimer.Tick+=(_,_)=>{searchTimer.Stop();if(HasLookup)ShowLookup();else if(page=="words")ShowWords();else if(page=="books")RefreshBooks();};
        study.Changed+=StudyChanged;audio.MessageChanged+=ShowStatus;
        Closing+=(_,e)=>{if(!allowClose){e.Cancel=true;Dispatcher.BeginInvoke(quit);}};
        Closed+=(_,_)=>{searchTimer.Stop();study.Changed-=StudyChanged;audio.MessageChanged-=ShowStatus;};
        PreviewKeyDown+=(_,e)=>
        {
            if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.F){lookupInput.Focus();lookupInput.SelectAll();e.Handled=true;return;}
            if(e.Key==Key.Escape && HasLookup){lookupInput.Clear();lookupInput.Focus();e.Handled=true;return;}
            if(e.Key==Key.Escape && page=="words"){page="books";ShowPage();e.Handled=true;}
            if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.D1){ShowBooks();e.Handled=true;}
            if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.D2){returnToCard();e.Handled=true;}
            if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.Q){quit();e.Handled=true;}
        };
        BuildShell();ready=true;
    }
    internal void CloseForQuit(){allowClose=true;Close();}
    internal void RefreshTheme(){BuildShell();}
    private void BuildShell()
    {
        Background=UI.Brush(NativeWindow.IsDark?"#1C2420":"#F8FAF7");Foreground=Ink;
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});grid.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar=new DockPanel{Margin=new Thickness(20,28,16,20)};
        var brand=new StackPanel();var brandName=UI.Text("浮词",25,Ink);brandName.Margin=new Thickness(0,0,0,24);brand.Children.Add(brandName);
        foreach(var (title,id) in new[]{("全部词本","all"),("考试词汇","exam"),("专业领域","professional")})
        {
            var button=UI.Button(title,()=>{category=id;ShowBooks();},Ink);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,2,0,2);brand.Children.Add(button);
        }
        brand.Children.Add(UI.Button("查单词",()=>{lookupInput.Focus();lookupInput.SelectAll();},Ink));
        var appearanceButton=UI.Button("卡片外观",ShowAppearance,Ink);appearanceButton.Margin=new Thickness(0,20,0,0);brand.Children.Add(appearanceButton);
        DockPanel.SetDock(brand,Dock.Top);sidebar.Children.Add(brand);
        var bottom=new StackPanel();
        progress=UI.Text("",11,Muted);progress.Margin=new Thickness(0,0,0,14);bottom.Children.Add(progress);
        bottom.Children.Add(UI.Text("发音口音",10,Muted));
        var accent=new ComboBox{ItemsSource=new[]{"美式英语","英式英语"},SelectedIndex=audio.Accent=="en-GB"?1:0,Margin=new Thickness(0,5,0,14),Padding=new Thickness(5),Foreground=UI.Brush("#253B31"),Background=Brushes.White};
        accent.SelectionChanged+=(_,_)=>audio.Accent=accent.SelectedIndex==1?"en-GB":"en-US";bottom.Children.Add(accent);
        var start=UI.Button("悬浮学习",returnToCard,Brushes.White,"library.return");start.Background=UI.Brush("#346C53");start.Padding=new Thickness(12);bottom.Children.Add(start);
        var about=UI.Button("关于与隐私",()=>AboutWindow.Show(this),Muted,"library.about");about.FontSize=10;about.Margin=new Thickness(0,10,0,0);bottom.Children.Add(about);
        DockPanel.SetDock(bottom,Dock.Bottom);sidebar.Children.Add(bottom);sidebar.Children.Add(new Border());
        var sideBorder=new Border{Background=UI.Brush(NativeWindow.IsDark?"#242D27":"#EEF2EC"),Child=sidebar};grid.Children.Add(sideBorder);
        var content=new DockPanel{Margin=new Thickness(28,25,28,16)};
        status=UI.Text("",11,Muted);status.Margin=new Thickness(0,10,0,0);DockPanel.SetDock(status,Dock.Bottom);content.Children.Add(status);
        var lookupBar=new DockPanel{Margin=new Thickness(0,0,0,20)};
        var clear=UI.Button("清空",()=>{lookupInput.Clear();lookupInput.Focus();},Muted,"dictionary.clear");DockPanel.SetDock(clear,Dock.Right);lookupBar.Children.Add(clear);
        lookupInput=SearchBox(lookupQuery,"查单词或中文释义",q=>
        {
            lookupQuery=q;audio.Stop();searchTimer.Stop();
            if(!HasLookup)ShowPage();
            else {center.Content=UI.Text("正在查词…",13,Muted);searchTimer.Start();}
        });
        lookupInput.Margin=new Thickness(0);lookupInput.Tag="dictionary.search";
        lookupInput.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){searchTimer.Stop();if(HasLookup)ShowLookup();e.Handled=true;}};
        var inputHost=new Grid();inputHost.Children.Add(lookupInput);
        var placeholder=UI.Text("查单词或中文释义",12,Muted);placeholder.Margin=new Thickness(11,0,0,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;
        placeholder.Visibility=lookupQuery.Length==0?Visibility.Visible:Visibility.Collapsed;inputHost.Children.Add(placeholder);
        lookupInput.TextChanged+=(_,_)=>placeholder.Visibility=lookupInput.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
        clear.Visibility=lookupQuery.Length==0?Visibility.Collapsed:Visibility.Visible;
        lookupInput.TextChanged+=(_,_)=>clear.Visibility=lookupInput.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
        lookupBar.Children.Add(inputHost);DockPanel.SetDock(lookupBar,Dock.Top);content.Children.Add(lookupBar);
        center=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};content.Children.Add(center);Grid.SetColumn(content,1);grid.Children.Add(content);
        Content=grid;ShowPage();StudyChanged();ShowStatus();
    }
    private void StudyChanged()
    {
        progress.Text=study.CurrentBook is { } book?$"{book.Name} · {study.Index+1:N0} / {book.Count:N0}":"选择一个词本";
        if(ready && page=="books" && !HasLookup)RefreshBooks();
    }
    private void ShowStatus()
    {
        status.Text=audio.Message??study.Settings.LastError??"";
        status.Visibility=status.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
    }
    internal void ShowBooks(){page="books";lookupInput.Clear();ShowPage();}
    internal void ShowAppearance(){page="appearance";lookupInput.Clear();ShowPage();}
    private void ShowPage()
    {
        searchTimer.Stop();
        if(HasLookup){ShowLookup();return;}
        if(page=="appearance")center.Content=AppearanceFactory?.Invoke()??UI.Text("卡片外观",22,Ink);
        else if(page=="words")ShowWords();
        else ShowBookPage();
    }
    private void ShowLookup()
    {
        if(!HasLookup)return;
        if(center.Content is not DictionaryPage)dictionaryPage=new DictionaryPage(study,audio,Ink,Muted,Panel);
        center.Content=dictionaryPage;dictionaryPage!.Refresh(lookupQuery);
    }
    internal void SearchDictionary(string query){lookupInput.Text=query;searchTimer.Stop();if(HasLookup)ShowLookup();}
    internal string? DictionarySelection => dictionaryPage?.SelectedWord?.Id;
    private TextBox SearchBox(string text,string placeholder,Action<string> changed)
    {
        var input=new TextBox{Text=text,ToolTip=placeholder,FontSize=13,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,0,0,16),Background=Panel,Foreground=Ink,CaretBrush=Ink,BorderBrush=UI.Brush(NativeWindow.IsDark?"#445248":"#D7DED6")};
        System.Windows.Automation.AutomationProperties.SetName(input,placeholder);
        input.TextChanged+=(_,_)=>changed(input.Text);return input;
    }
    private WrapPanel? bookCards;
    private TextBlock? emptyBooks;
    private void ShowBookPage()
    {
        var dock=new DockPanel();
        var heading=UI.Text(category=="exam"?"考试词汇":category=="professional"?"专业领域":"单词本",25,Ink);heading.Margin=new Thickness(0,0,0,6);DockPanel.SetDock(heading,Dock.Top);dock.Children.Add(heading);
        var search=SearchBox(bookQuery,"搜索词本名称或领域",q=>{bookQuery=q;searchTimer.Stop();searchTimer.Start();});DockPanel.SetDock(search,Dock.Top);dock.Children.Add(search);
        var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};
        var browse=UI.Button("浏览词条",()=>{page="words";wordQuery="";ShowPage();},Ink,"library.browse");browse.HorizontalAlignment=HorizontalAlignment.Left;footer.Children.Add(browse);
        DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
        bookCards=new WrapPanel();emptyBooks=UI.Text("没有找到词本",14,Muted);
        var cards=new StackPanel();cards.Children.Add(emptyBooks);cards.Children.Add(bookCards);
        dock.Children.Add(new ScrollViewer{Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        center.Content=dock;RefreshBooks();
    }
    internal IReadOnlyList<VocabularyBook> MatchingBooks(string query,string filter="all")=>study.Books.Where(b=>(filter=="all"||b.Category==filter)&&(b.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||b.Id.Contains(query,StringComparison.OrdinalIgnoreCase)||b.Subtitle.Contains(query,StringComparison.OrdinalIgnoreCase)||b.CategoryTitle.Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray();
    private void RefreshBooks()
    {
        if(bookCards is null || page!="books")return;
        bookCards.Children.Clear();var books=MatchingBooks(bookQuery.Trim(),category);emptyBooks!.Visibility=books.Count==0?Visibility.Visible:Visibility.Collapsed;
        foreach(var book in books)
        {
            var button=UI.Button("",()=>study.SelectBook(book.Id),Ink,"book."+book.Id);button.Padding=new Thickness(0);button.Margin=new Thickness(0,0,12,12);button.Width=260;
            var lines=new StackPanel{Margin=new Thickness(17,13,17,13)};var title=UI.Text(book.Name,18,Ink);title.FontWeight=FontWeights.Medium;lines.Children.Add(title);
            var count=UI.Text($"{book.Count:N0} 词",11,Muted);count.Margin=new Thickness(0,8,0,0);lines.Children.Add(count);
            button.Content=new Border{Background=Panel,BorderBrush=UI.Brush(study.SelectedBookId==book.Id?"#659F79":NativeWindow.IsDark?"#38463C":"#E0E6DE"),BorderThickness=new Thickness(study.SelectedBookId==book.Id?2:1),CornerRadius=new CornerRadius(12),Child=lines};
            bookCards.Children.Add(button);
        }
    }
    private ListBox? words;
    private ContentControl? wordPreview;
    private void ShowWords()
    {
        if(page!="words")return;
        // Reuse the existing search input while typing, so focus and the caret stay stable.
        if(words is not null && center.Content is Grid existing && existing.Tag as string=="words") { RefreshWords();return; }
        var grid=new Grid{Tag="words"};grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});grid.ColumnDefinitions.Add(new ColumnDefinition());
        var top=new DockPanel{Margin=new Thickness(0,0,0,16)};
        var back=UI.Button("← 词本",()=>{page="books";ShowPage();},Muted);DockPanel.SetDock(back,Dock.Right);top.Children.Add(back);top.Children.Add(UI.Text(study.CurrentBook?.Name??"词条",24,Ink));Grid.SetColumnSpan(top,2);grid.Children.Add(top);
        var search=SearchBox(wordQuery,"搜索英文或中文释义",q=>{wordQuery=q;searchTimer.Stop();searchTimer.Start();});Grid.SetRow(search,1);Grid.SetColumnSpan(search,2);grid.Children.Add(search);
        words=new ListBox{DisplayMemberPath="Spelling",Background=Panel,Foreground=Ink,BorderThickness=new Thickness(0),Margin=new Thickness(0,0,18,0)};
        VirtualizingPanel.SetIsVirtualizing(words,true);VirtualizingPanel.SetVirtualizationMode(words,VirtualizationMode.Recycling);
        words.SelectionChanged+=(_,_)=>PreviewWord(words.SelectedItem as BookEntry);Grid.SetRow(words,2);grid.Children.Add(words);
        wordPreview=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};Grid.SetRow(wordPreview,2);Grid.SetColumn(wordPreview,1);grid.Children.Add(wordPreview);
        center.Content=grid;RefreshWords();
    }
    private void RefreshWords()
    {
        if(words is null)return;
        var results=study.Store.Entries(study.SelectedBookId,wordQuery);words.ItemsSource=results;
        words.SelectedItem=results.FirstOrDefault(e=>e.Id==study.Current?.Id)??results.FirstOrDefault();
        if(results.Count==0)wordPreview!.Content=UI.Text("没有找到词条",14,Muted);
    }
    private void PreviewWord(BookEntry? entry)
    {
        if(wordPreview is null || entry is null)return;
        var dock=new DockPanel();
        var start=UI.Button("从这个词继续 →",()=>{study.Jump(entry.Id);returnToCard();},Brushes.White,"word.start");start.Background=UI.Brush("#346C53");start.Margin=new Thickness(0,14,0,0);DockPanel.SetDock(start,Dock.Bottom);dock.Children.Add(start);
        var content=new StackPanel();content.Children.Add(UI.Text(entry.Spelling,27,Ink));
        var phonetic=UI.Text(string.Join("   ",new[]{entry.PartOfSpeech,entry.Phonetic}.Where(s=>s.Length>0)),12,Muted);phonetic.Margin=new Thickness(0,10,0,12);content.Children.Add(phonetic);
        content.Children.Add(UI.SpeechButton("听发音",()=>audio.Speak(entry.Spelling),Ink,"word.speak"));
        var meaning=UI.Text(entry.Meaning,14,Ink);meaning.Margin=new Thickness(0,18,0,0);content.Children.Add(meaning);
        var details=study.Store.Details(entry.Id);
        if(details.DictionaryMeaning.Length>0 && details.DictionaryMeaning!=entry.Meaning)
        {var general=UI.Text("词典释义\n"+details.DictionaryMeaning,12,Muted);general.Margin=new Thickness(0,18,0,0);content.Children.Add(general);}
        dock.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});wordPreview.Content=dock;
    }
}
