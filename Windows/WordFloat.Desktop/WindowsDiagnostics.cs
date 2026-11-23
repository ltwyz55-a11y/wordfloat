using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class WindowsDiagnostics
{
    private sealed class Engine : ISpeechOutput
    {
        public IReadOnlyList<SpeechVoice> Voices { get; }=[new("test-us","en-US"),new("test-gb","en-GB"),new("test-zh","zh-CN")];
        internal List<SpeechPart> Spoken { get; }=[];
        internal int Stops;
        public void Stop()=>Stops++;
        public void Speak(IReadOnlyList<SpeechPart> parts)=>Spoken.AddRange(parts);
        public void Dispose(){ }
    }
    private static Task Pause()=>Task.Delay(150);
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private static void Capture(Window window,string path)
    {
        window.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);
        image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);
    }
    internal static int Run(string folder)
    {
        folder=Path.GetFullPath(folder);Directory.CreateDirectory(folder);
        var checks=new List<string>();Exception? failure=null;var completed=false;
        void Report(bool passed)=>File.WriteAllText(Path.Combine(folder,"windows-checks.json"),JsonSerializer.Serialize(new
        {
            status=passed?"passed":"failed",version=AppInfo.Version,checks,error=failure?.ToString(),
            method="Native WPF state and control integration with a silent speech substitute",
            notVerified=new[]{"physical mouse/touchpad","physical audio output","DWM glass appearance on desktop","multiple physical monitors","other apps in fullscreen"}
        },JsonDefaults.Options));
        try
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var engine=new Engine();
            using var controller=new AppController(app,Path.Combine(folder,"temporary-settings"),engine);
            var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(45)};
            timeout.Tick+=(_,_)=>{timeout.Stop();failure=new TimeoutException("Windows diagnostics did not finish");controller.Quit();};
            app.Exit+=(_,e)=>
            {
                timeout.Stop();
                try
                {
                    Require(completed && failure is null,"Checks interrupted");
                    Require(controller.IsQuitting&&!controller.Card.IsVisible&&!controller.Library.IsVisible&&!controller.TrayVisible,"Closing library must quit and hide all windows and tray");
                    checks.Add("main-close-quits-card-and-tray");Report(true);e.ApplicationExitCode=0;
                }
                catch(Exception error){failure??=error;Report(false);e.ApplicationExitCode=1;}
                NativeWindow.DiagnosticDarkMode=null;
            };
            app.DispatcherUnhandledException+=(_,e)=>{failure=e.Exception;e.Handled=true;controller.Quit();};
            app.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    timeout.Start();controller.Start();controller.Study.SelectBook("automotive");controller.Study.Jump("greenhouse");controller.ShowCard();await Pause();
                    var card=controller.Card;var study=controller.Study;
                    Require(card.Topmost&&!card.ShowActivated&&!card.ShowInTaskbar,"Nonactivating floating configuration");
                    Require(card.ActualWidth==260&&card.ActualHeight==150,"Compact size");checks.Add("nonactivating-topmost-compact-card");
                    NativeWindow.DiagnosticDarkMode=false;card.RefreshTheme();await Pause();Capture(card,Path.Combine(folder,"card-light.png"));
                    card.ApplyAction(CardAction.SingleClick);await Pause();
                    Require(study.Revealed&&card.ActualHeight==220&&engine.Spoken.Count==0,"Reveal must remain silent");
                    Capture(card,Path.Combine(folder,"card-expanded.png"));
                    card.Speaker.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Require(engine.Spoken.Count==1&&engine.Spoken[0].Text=="greenhouse"&&study.Revealed,"Manual word speech");
                    card.ApplyAction(CardAction.SingleClick);await Pause();Require(!study.Revealed&&card.ActualHeight==150,"Collapse");
                    card.ApplyAction(CardAction.SingleClick);await Pause();Require(engine.Spoken.Count==1,"Repeated reveal stays silent");checks.Add("silent-reveal-collapse-and-manual-pronunciation");
                    card.ApplyAction(CardAction.OpenLibrary);await Pause();Require(controller.Library.IsVisible&&!card.IsVisible,"Open library");
                    Require(controller.Library.MatchingBooks("设计","professional").Count==2,"Chinese book filter");
                    Require(controller.Library.MatchingBooks("ielts").Single().Id=="ielts","English book filter");checks.Add("library-selection-and-book-search");
                    controller.Library.ShowBooks();Capture(controller.Library,Path.Combine(folder,"library-light.png"));
                    var information=AboutWindow.Create(controller.Library);information.Show();await Pause();
                    var notice=((TextBox)information.Content).Text;
                    Require(notice.Contains("隐私说明")&&notice.Contains("不主动收集或上传")&&notice.Contains("花生酱罐罐"),"Privacy and imported-source information");
                    Capture(information,Path.Combine(folder,"privacy-and-sources.png"));information.Close();checks.Add("privacy-and-source-information");
                    NativeWindow.DiagnosticDarkMode=true;controller.Library.RefreshTheme();await Pause();Capture(controller.Library,Path.Combine(folder,"library-dark.png"));
                    var lookupCurrent=study.Current;var lookupSpeech=engine.Spoken.Count;
                    controller.Library.SearchDictionary("  PHOTOGRAPH  ");await Pause();
                    Require(controller.Library.DictionarySelection=="photograph"&&engine.Spoken.Count==lookupSpeech,"Exact dictionary lookup stays silent");
                    var dictionarySpeaker=Descendants(controller.Library).OfType<Button>().First(b=>b.Tag as string=="dictionary.speak");dictionarySpeaker.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(engine.Spoken.Last().Text=="photograph"&&study.Current==lookupCurrent,"Dictionary speech and preserved study position");Capture(controller.Library,Path.Combine(folder,"dictionary-dark.png"));
                    NativeWindow.DiagnosticDarkMode=false;controller.Library.RefreshTheme();await Pause();Capture(controller.Library,Path.Combine(folder,"dictionary-light.png"));
                    controller.Library.SearchDictionary("车窗");await Pause();Capture(controller.Library,Path.Combine(folder,"dictionary-chinese.png"));
                    controller.Library.SearchDictionary("zz-no-dictionary-result");await Pause();Require(controller.Library.DictionarySelection is null,"No stale selection for empty lookup");
                    Require(!Descendants(controller.Library).OfType<Button>().Any(b=>b.Tag as string=="dictionary.speak"),"No stale speaker for empty lookup");Capture(controller.Library,Path.Combine(folder,"dictionary-empty.png"));
                    controller.Library.SearchDictionary("");await Pause();Require(study.Current==lookupCurrent,"Clearing lookup preserves progress");checks.Add("global-dictionary-lookup-manual-speech-and-clear");
                    study.SelectBook("cet4");study.Jump("photograph");controller.ShowCard();card.ApplyAction(CardAction.SingleClick);await Pause();
                    var scroll=card.Scroller!;Require(scroll.ExtentHeight>scroll.ViewportHeight*3,"Complete details remain scrollable");
                    var vertical=Descendants(scroll).OfType<ScrollBar>().First(b=>b.Orientation==Orientation.Vertical&&b.IsVisible);
                    ScrollBar.PageDownCommand.Execute(null,vertical);await Pause();
                    Require(scroll.VerticalOffset>0&&study.Current!.Id=="photograph","Custom scrollbar page command preserves word");
                    checks.Add("themed-scrollbar-page-navigation");
                    scroll.ScrollToVerticalOffset(140);await Pause();Require(scroll.VerticalOffset>0&&study.Current!.Id=="photograph","Vertical scroll preserves word");Capture(card,Path.Combine(folder,"learning-roots.png"));
                    var example=study.Details.Examples[0];
                    foreach(var (kind,expected) in new[]{("english",new[]{example.English}),("chinese",new[]{example.Chinese}),("pair",new[]{example.English,example.Chinese})})
                    {
                        var button=Descendants(card).OfType<Button>().First(b=>b.Tag as string=="example."+kind+"."+example.Id);button.BringIntoView();await Pause();
                        var before=engine.Spoken.Count;var stops=engine.Stops;button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Require(engine.Spoken.Skip(before).Select(p=>p.Text).SequenceEqual(expected)&&engine.Stops>stops&&study.Revealed,"Inline "+kind+" playback");
                    }
                    Capture(card,Path.Combine(folder,"learning-examples-dark.png"));
                    var stopCount=engine.Stops;card.ApplyAction(CardAction.Next);await Pause();Require(!study.Revealed&&engine.Stops>stopCount,"Flip stops old speech");
                    card.ApplyAction(CardAction.SingleClick);await Pause();Require(card.Scroller!.VerticalOffset==0,"New word starts at top");checks.Add("complete-details-bilingual-controls-and-scroll-reset");
                    study.Revealed=false;
                    foreach(var preset in CardPreset.All)
                    {
                        controller.Appearance.Apply(preset);await Pause();Capture(card,Path.Combine(folder,"theme-"+preset.Id+".png"));
                        study.Revealed=true;await Pause();Capture(card,Path.Combine(folder,"theme-"+preset.Id+"-expanded.png"));study.Revealed=false;
                    }
                    var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[]{70,120,80,255,90,150,100,255,80,140,90,255,60,110,70,255},8);
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var bytes=new MemoryStream();png.Save(bytes);controller.Appearance.SaveWallpaper(bytes.ToArray());await Pause();Capture(card,Path.Combine(folder,"theme-wallpaper.png"));
                    checks.Add("six-presets-and-managed-wallpaper-rendering");
                    controller.ShowLibrary();controller.Library.ShowAppearance();await Pause();Capture(controller.Library,Path.Combine(folder,"appearance-dark.png"));
                    NativeWindow.DiagnosticDarkMode=false;controller.Library.RefreshTheme();await Pause();Capture(controller.Library,Path.Combine(folder,"appearance-light.png"));
                    checks.Add("appearance-editor-light-and-dark");
                    controller.ShowCard();study.Revealed=false;await Pause();
                    var sameWord=study.Current!.Id;var spokenCount=engine.Spoken.Count;
                    card.Width=1;card.Height=1;await Pause();
                    Require(card.ActualWidth==220&&card.ActualHeight==130,"Minimum card size");Capture(card,Path.Combine(folder,"card-resize-minimum.png"));
                    card.Width=900;card.Height=900;await Pause();
                    Require(card.ActualWidth==520&&card.ActualHeight==420,"Maximum card size");Capture(card,Path.Combine(folder,"card-resize-maximum.png"));
                    card.Width=380;card.Height=240;await Pause();study.Revealed=true;await Pause();
                    Require(card.ActualWidth==380&&card.ActualHeight==220,"Independent expanded height");
                    card.Height=350;await Pause();study.Revealed=false;await Pause();
                    Require(card.ActualWidth==380&&card.ActualHeight==240,"Collapse restores custom size");
                    study.Revealed=true;await Pause();Require(card.ActualHeight==350,"Expanded size remembered");
                    Require(study.Current!.Id==sameWord&&engine.Spoken.Count==spokenCount,"Resize must not flip or pronounce words");Capture(card,Path.Combine(folder,"card-resize-expanded.png"));
                    checks.Add("bounded-card-size-and-independent-mode-heights");
                    completed=true;card.Show();controller.Library.Close();
                }
                catch(Exception error){failure=error;controller.Quit();}
            }));
            return app.Run();
        }
        catch(Exception error){failure=error;Report(false);return 1;}
    }
}
