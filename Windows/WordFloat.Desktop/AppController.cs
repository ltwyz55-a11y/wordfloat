using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WordFloat.Core;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WordFloat.Desktop;

internal sealed class AppController : IDisposable
{
    private readonly Application app;
    private readonly ISpeechOutput speech;
    private readonly Forms.NotifyIcon tray;
    private readonly Drawing.Icon trayIcon;
    private bool quitting,disposed;
    internal SettingsStore Settings { get; }
    internal VocabularyStore Store { get; }
    internal StudySession Study { get; }
    internal Pronunciation Audio { get; }
    internal AppearanceController Appearance { get; }
    internal CardWindow Card { get; }
    internal LibraryWindow Library { get; }
    internal bool IsQuitting=>quitting;
    internal bool TrayVisible=>!disposed && tray.Visible;
    internal AppController(Application app,string directory,ISpeechOutput? speech=null)
    {
        this.app=app;this.speech=speech??new WindowsSpeechOutput();
        Settings=new SettingsStore(Path.Combine(directory,"settings.json"));
        Store=new VocabularyStore(VocabularyStore.ExtractBundled(Path.Combine(directory,"Cache")));
        Study=new StudySession(Store,Settings);Audio=new Pronunciation(this.speech,Settings);
        Appearance=new AppearanceController(Settings,Path.Combine(directory,"Wallpapers"));
        Study.ChangingWord+=Audio.Stop;
        Card=new CardWindow(Study,Audio,Appearance,ShowLibrary);
        Library=new LibraryWindow(Study,Audio,Appearance,ShowCard,Quit);
        Library.AppearanceFactory=()=>new AppearancePage(Study,Audio,Appearance);
        app.MainWindow=Library;
        using(var icon=typeof(AppController).Assembly.GetManifestResourceStream("WordFloat.Icon"))
            if(icon is not null){var bitmap=BitmapFrame.Create(icon,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);bitmap.Freeze();Library.Icon=bitmap;}
        trayIcon=Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??(Drawing.Icon)Drawing.SystemIcons.Application.Clone();
        tray=new Forms.NotifyIcon{Text=AppInfo.Name,Icon=trayIcon,Visible=true};
        var menu=new Forms.ContextMenuStrip();
        var toggle=menu.Items.Add("显示单词卡片",null,(_,_)=>OnUi(()=>{if(Card.IsVisible)Card.Conceal();else ShowCard();}));
        menu.Items.Add("打开单词本",null,(_,_)=>OnUi(ShowLibrary));
        menu.Items.Add(new Forms.ToolStripSeparator());
        var us=(Forms.ToolStripMenuItem)menu.Items.Add("美式英语",null,(_,_)=>OnUi(()=>{Audio.Accent="en-US";Library.RefreshTheme();}));
        var gb=(Forms.ToolStripMenuItem)menu.Items.Add("英式英语",null,(_,_)=>OnUi(()=>{Audio.Accent="en-GB";Library.RefreshTheme();}));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出浮词",null,(_,_)=>OnUi(Quit));
        menu.Opening+=(_,_)=>{toggle.Text=Card.IsVisible?"隐藏单词卡片":"显示单词卡片";us.Checked=Audio.Accent=="en-US";gb.Checked=Audio.Accent=="en-GB";};
        tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>OnUi(ShowLibrary);
        SystemEvents.UserPreferenceChanged+=PreferenceChanged;
    }
    internal void Start()
    {
        if(Settings.Value.HasLaunched)ShowCard();else ShowLibrary();
        Settings.Value.HasLaunched=true;Settings.Save();
    }
    internal void ShowCard()
    {
        if(quitting)return;
        Library.Hide();Card.Show();NativeWindow.Restore(Card,Settings);
    }
    internal void ShowLibrary()
    {
        if(quitting)return;
        Card.Conceal();Library.RefreshTheme();Library.Show();
        if(Library.WindowState==WindowState.Minimized)Library.WindowState=WindowState.Normal;
        Library.Activate();
    }
    internal void OnUi(Action action)
    {
        if(app.Dispatcher.HasShutdownStarted)return;
        if(app.Dispatcher.CheckAccess())action();else app.Dispatcher.BeginInvoke(action);
    }
    private void PreferenceChanged(object sender,UserPreferenceChangedEventArgs e)=>OnUi(()=>{Card.RefreshTheme();Library.RefreshTheme();});
    internal void Quit()
    {
        if(quitting)return;quitting=true;
        Card.Conceal();Library.CloseForQuit();Card.Close();Dispose();app.Shutdown(0);
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        SystemEvents.UserPreferenceChanged-=PreferenceChanged;
        Study.ChangingWord-=Audio.Stop;
        tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();trayIcon.Dispose();
        speech.Dispose();Store.Dispose();Settings.Save();
    }
}
