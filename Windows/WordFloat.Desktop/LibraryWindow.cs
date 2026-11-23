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
    private TextBlock status = new(), pageTitle = new();
    private readonly Dictionary<string, Button> navigation = new();
    private string category="all", page="books", bookQuery="", wordQuery="";
    private bool ready, allowClose;
    private TextBox lookupInput = new();
    private string lookupQuery = "";
    private DictionaryPage? dictionaryPage;
    private Brush Ink => LibraryTheme.Ink;
    private Brush Muted => LibraryTheme.Muted;
    private Brush Panel => LibraryTheme.Surface;
    internal Func<FrameworkElement>? AppearanceFactory { get; set; }
    internal LibraryWindow(StudySession study,Pronunciation audio,AppearanceController appearance,Action returnToCard,Action quit)
    {
        this.study=study;this.audio=audio;this.appearance=appearance;this.returnToCard=returnToCard;this.quit=quit;
        Title="浮词";Width=1080;Height=760;MinWidth=840;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=UI.Font;FontSize=13;UseLayoutRounding=true;
        searchTimer.Tick+=(_,_)=>{searchTimer.Stop();if(page=="dictionary")ShowLookup();else if(page=="words")RefreshWords();else if(page=="books")RefreshBooks();};
        study.Changed+=StudyChanged;audio.MessageChanged+=ShowStatus;
        Closing+=(_,e)=>{if(!allowClose){e.Cancel=true;Dispatcher.BeginInvoke(quit);}};
        Closed+=(_,_)=>{searchTimer.Stop();study.Changed-=StudyChanged;audio.MessageChanged-=ShowStatus;};
        PreviewKeyDown+=(_,e)=>
        {
            if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.F){ShowDictionary();lookupInput.Focus();lookupInput.SelectAll();e.Handled=true;return;}
            if(e.Key==Key.Escape && page=="dictionary"){lookupInput.Clear();lookupInput.Focus();e.Handled=true;return;}
            if(e.Key==Key.Escape && page=="words"){ShowBooks();e.Handled=true;}
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
        searchTimer.Stop();dictionaryPage=null;bookCards=null;words=null;navigation.Clear();
        Background=LibraryTheme.Canvas;Foreground=Ink;
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});grid.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar=new DockPanel{Margin=new Thickness(16,28,16,20)};
        var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(8,4,0,34)};
        brand.Children.Add(LibraryIcons.Create("brand",Ink,28));
        var brandName=UI.Text("浮词",23,Ink);brandName.FontWeight=FontWeights.Bold;brandName.Margin=new Thickness(12,0,0,0);brand.Children.Add(brandName);
        DockPanel.SetDock(brand,Dock.Top);sidebar.Children.Add(brand);
        var bottom=NavigationButton("设置","settings","settings",()=>Navigate("settings"));
        DockPanel.SetDock(bottom,Dock.Bottom);sidebar.Children.Add(bottom);
        var links=new StackPanel();
        links.Children.Add(NavigationButton("词本","books","book",ShowBooks));
        links.Children.Add(NavigationButton("查词","dictionary","search",ShowDictionary));
        links.Children.Add(NavigationButton("外观","appearance","appearance",ShowAppearance));sidebar.Children.Add(links);
        grid.Children.Add(new Border{Background=LibraryTheme.Sidebar,BorderBrush=LibraryTheme.Line,BorderThickness=new Thickness(0,0,1,0),Child=sidebar});
        var content=new DockPanel{Margin=new Thickness(28,24,28,20)};
        var header=new DockPanel{Margin=new Thickness(0,0,0,24)};
        var start=LibraryTheme.PrimaryButton("悬浮学习",returnToCard,"library.return");start.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(start,Dock.Right);header.Children.Add(start);
        pageTitle=LibraryTheme.Heading("词本");header.Children.Add(pageTitle);
        var headerLine=new Border{Child=header,BorderBrush=LibraryTheme.Line,BorderThickness=new Thickness(0,0,0,1),Margin=new Thickness(0,0,0,20)};
        DockPanel.SetDock(headerLine,Dock.Top);content.Children.Add(headerLine);
        status=UI.Text("",12,Muted);status.Margin=new Thickness(0,10,0,0);DockPanel.SetDock(status,Dock.Bottom);content.Children.Add(status);
        center=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};content.Children.Add(center);Grid.SetColumn(content,1);grid.Children.Add(content);
        Content=grid;ShowPage();ShowStatus();
    }
    private Button NavigationButton(string title,string id,string icon,Action action)
    {
        var button=UI.Button(title,action,Muted,"navigation."+id);
        button.Margin=new Thickness(0,3,0,3);button.Padding=new Thickness(12,12,12,12);button.HorizontalContentAlignment=HorizontalAlignment.Left;
        navigation.Add(id,button);return button;
    }
    private void UpdateNavigation()
    {
        var active=page=="words"?"books":page;
        foreach(var (id,button) in navigation)
        {
            var (icon,title)=id switch{"dictionary"=>("search","查词"),"appearance"=>("appearance","外观"),"settings"=>("settings","设置"),_=>("book","词本")};
            var selected=id==active;var color=selected?LibraryTheme.Accent:Muted;
            button.Background=selected?LibraryTheme.Selection:Brushes.Transparent;
            var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(LibraryIcons.Create(icon,color));
            var text=UI.Text(title,14,color);text.Margin=new Thickness(12,0,0,0);text.FontWeight=selected?FontWeights.Bold:FontWeights.Normal;row.Children.Add(text);button.Content=row;
        }
        pageTitle.Text=page switch{"dictionary"=>"查词","appearance"=>"外观","settings"=>"设置","words"=>study.CurrentBook?.Name??"词条",_=>"词本"};
    }
    private void StudyChanged()
    {
        if(ready && page=="books")RefreshBooks();
    }
    private void ShowStatus()
    {
        status.Text=audio.Message??study.Settings.LastError??"";
        status.Visibility=status.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
    }
    private void Navigate(string destination)
    {
        audio.Stop();page=destination;ShowPage();
    }
    internal void ShowBooks()=>Navigate("books");
    internal void ShowAppearance()=>Navigate("appearance");
    private void ShowDictionary()
    {
        if(page!="dictionary")Navigate("dictionary");
        lookupInput.Focus();
    }
    private void ShowPage()
    {
        searchTimer.Stop();UpdateNavigation();
        if(page=="appearance")center.Content=AppearanceFactory?.Invoke()??UI.Text("外观",16,Ink);
        else if(page=="dictionary")ShowDictionaryPage();
        else if(page=="settings")ShowSettings();
        else if(page=="words")ShowWords();
        else ShowBookPage();
    }
    private void ShowSettings()
    {
        var settings=new StackPanel{MaxWidth=440,HorizontalAlignment=HorizontalAlignment.Left};
        var label=UI.Text("发音口音",14,Ink);label.FontWeight=FontWeights.Bold;settings.Children.Add(label);
        var accent=new ComboBox{ItemsSource=new[]{"美式英语","英式英语"},SelectedIndex=audio.Accent=="en-GB"?1:0,Margin=new Thickness(0,12,0,24),Padding=new Thickness(10),MinWidth=240,Foreground=Ink,Background=Panel};
        accent.SelectionChanged+=(_,_)=>audio.Accent=accent.SelectedIndex==1?"en-GB":"en-US";settings.Children.Add(accent);
        settings.Children.Add(UI.Button("关于与隐私",()=>AboutWindow.Show(this),Ink,"library.about"));center.Content=settings;
    }
    private void ShowDictionaryPage()
    {
        var dock=new DockPanel();
        lookupInput=SearchBox(lookupQuery,"搜索单词或中文释义",q=>{lookupQuery=q;audio.Stop();searchTimer.Stop();searchTimer.Start();});
        lookupInput.Tag="dictionary.search";lookupInput.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){searchTimer.Stop();ShowLookup();e.Handled=true;}};
        DockPanel.SetDock(lookupInput,Dock.Top);dock.Children.Add(lookupInput);
        dictionaryPage=new DictionaryPage(study,audio,Ink,Muted,Panel);dock.Children.Add(dictionaryPage);center.Content=dock;ShowLookup();
    }
    private void ShowLookup()
    {
        if(page=="dictionary")dictionaryPage?.Refresh(lookupQuery);
    }
    internal void SearchDictionary(string query)
    {
        ShowDictionary();lookupInput.Text=query;searchTimer.Stop();ShowLookup();
    }
    internal string? DictionarySelection => dictionaryPage?.SelectedWord?.Id;
    private TextBox SearchBox(string text,string placeholder,Action<string> changed)
    {
        var input=new TextBox{Text=text,ToolTip=placeholder,FontSize=13,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,0,0,16),Background=Panel,Foreground=Ink,CaretBrush=Ink,BorderBrush=LibraryTheme.Line};
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
