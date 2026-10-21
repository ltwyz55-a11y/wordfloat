using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal sealed class CardWindow : Window
{
    private readonly StudySession study;
    private readonly Pronunciation audio;
    private readonly AppearanceController appearance;
    private readonly Action openLibrary;
    private readonly Border surface = new();
    private readonly Border grip = new() { Width=28,Height=3,CornerRadius=new CornerRadius(2) };
    private readonly ContentControl body = new() { HorizontalContentAlignment=HorizontalAlignment.Stretch, VerticalContentAlignment=VerticalAlignment.Stretch, ClipToBounds=true };
    private readonly CardPointer pointer = new();
    private readonly HorizontalWheel wheel = new();
    private readonly DispatcherTimer clickTimer = new(DispatcherPriority.Input);
    private readonly Button previous, next;
    internal Button Speaker { get; }
    internal ScrollViewer? Scroller => body.Content as ScrollViewer;
    private bool ready;
    private bool applyingSize;
    private bool? renderedRevealed;
    private (ResizeEdges Edges,ScreenRect Frame,Point Point,double DpiX,double DpiY)? resizing;
    private int clickCount;
    internal CardWindow(StudySession study, Pronunciation audio, AppearanceController appearance, Action openLibrary)
    {
        this.study=study; this.audio=audio; this.appearance=appearance; this.openLibrary=openLibrary;
        var size=study.Settings.Value.CardSize.Size(study.Revealed);
        Title=AppInfo.Name; Width=size.Width; Height=size.Height;
        MinWidth=CardSizing.MinimumWidth;MaxWidth=CardSizing.MaximumWidth;MinHeight=CardSizing.MinimumHeight(study.Revealed);MaxHeight=CardSizing.MaximumHeight;
        NativeWindow.PrepareCard(this);
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height=new GridLength(24) });
        grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height=new GridLength(36) });
        var drag = new Grid { Background=Brushes.Transparent, Cursor=Cursors.SizeAll, ToolTip="拖动这里移动卡片" };
        drag.Children.Add(grip); drag.MouseLeftButtonDown += Drag; grid.Children.Add(drag);
        Grid.SetRow(body,1); grid.Children.Add(body);
        var footer = new Grid();
        for (var i=0;i<3;i++) footer.ColumnDefinitions.Add(new ColumnDefinition());
        previous=UI.Button("‹",()=>study.Advance(-1),id:"word.previous"); previous.ToolTip="上一个单词";
        Speaker=UI.Button("播放发音",()=>{ if (study.Current is { } current) audio.Speak(current.Spelling); },id:"word.speak");
        Speaker.ToolTip="播放发音";Speaker.MinWidth=36;Speaker.MinHeight=30;
        next=UI.Button("›",()=>study.Advance(1),id:"word.next"); next.ToolTip="下一个单词";
        var buttons=new[] {previous,Speaker,next};
        for (var i=0;i<buttons.Length;i++)
        { buttons[i].Focusable=false; buttons[i].HorizontalAlignment=HorizontalAlignment.Center; buttons[i].FontSize=i==1?13:19; Grid.SetColumn(buttons[i],i); footer.Children.Add(buttons[i]); }
        previous.Opacity=0; next.Opacity=0; Grid.SetRow(footer,2); grid.Children.Add(footer);
        surface.Child=grid; Content=surface;
        body.PreviewMouseLeftButtonDown += PointerDown;
        body.PreviewMouseMove += PointerMove;
        body.PreviewMouseLeftButtonUp += PointerUp;
        body.PreviewMouseWheel += (_,_)=>CancelInteraction();
        PreviewMouseLeftButtonDown += ResizeDown;
        PreviewMouseMove += ResizeMove;
        PreviewMouseLeftButtonUp += ResizeUp;
        LostMouseCapture+=(_,_)=>{if(resizing is not null)CancelInteraction();};
        MouseEnter += (_,_)=>{previous.Opacity=0.65;next.Opacity=0.65;};
        MouseLeave += (_,_)=>{previous.Opacity=0;next.Opacity=0;if(resizing is null)Mouse.OverrideCursor=null;};
        clickTimer.Interval=TimeSpan.FromMilliseconds(NativeWindow.DoubleClickMilliseconds);
        clickTimer.Tick += (_,_)=>{clickTimer.Stop();ApplyAction(CardAction.SingleClick);};
        SourceInitialized += (_,_)=>NativeWindow.Attach(this,HorizontalScroll,()=>{CancelInteraction();if(ready)NativeWindow.Restore(this,study.Settings);});
        Loaded += (_,_)=>{NativeWindow.Restore(this,study.Settings);ready=true;};
        LocationChanged += (_,_)=>{if(ready)NativeWindow.Remember(this,study.Settings);};
        SizeChanged+=(_,_)=>{if(ready&&!applyingSize)study.Settings.Value.CardSize.Remember(Width,Height,study.Revealed);};
        study.Changed += StudyChanged; study.ChangingWord += CancelInteraction;
        appearance.Changed += AppearanceChanged; audio.MessageChanged += UpdateSpeechMessage;
        Closed += (_,_)=>
        {
            CancelInteraction();
            study.Changed-=StudyChanged;study.ChangingWord-=CancelInteraction;
            appearance.Changed-=AppearanceChanged;audio.MessageChanged-=UpdateSpeechMessage;
        };
        Render(false);
    }
    private void StudyChanged()=>Render(false);
    private void AppearanceChanged()=>Render(true);
    private void Render(bool preserveScroll)
    {
        var offset=preserveScroll?Scroller?.VerticalOffset??0:0;
        var style=appearance.Style.Resolved(NativeWindow.IsDark);
        UI.PaintSurface(surface,style,appearance.WallpaperPath,!AllowsTransparency);
        var ink=UI.Brush(style.Text,0.78);
        grip.Background=UI.Brush(style.Text,0.25);
        previous.Foreground=ink;Speaker.Foreground=ink;next.Foreground=ink;
        Speaker.Content=CardIcons.Speaker(ink);
        Speaker.IsEnabled=study.Current is not null;
        body.Content=CardContent.Build(study,audio,style,study.Revealed);
        if(renderedRevealed!=study.Revealed)
        {
            CancelInteraction();applyingSize=true;
            try
            {
                var size=study.Settings.Value.CardSize.Size(study.Revealed);
                MinHeight=CardSizing.MinimumHeight(study.Revealed);Width=size.Width;Height=size.Height;
                renderedRevealed=study.Revealed;
            }
            finally{applyingSize=false;}
        }
        if(ready) Dispatcher.BeginInvoke(()=>NativeWindow.Restore(this,study.Settings),DispatcherPriority.Loaded);
        if(offset>0) Dispatcher.BeginInvoke(()=>Scroller?.ScrollToVerticalOffset(offset),DispatcherPriority.Loaded);
        NativeWindow.ApplyBackdrop(this);
        UpdateSpeechMessage();
    }
    private void UpdateSpeechMessage()=>Speaker.ToolTip=audio.Message??"播放发音";
    internal void RefreshTheme()=>Render(true);
    internal void ApplyAction(CardAction action)
    {
        switch(action)
        {
            case CardAction.SingleClick: if(study.Current is not null)study.Revealed=!study.Revealed;break;
            case CardAction.OpenLibrary: CancelInteraction();audio.Stop();openLibrary();break;
            case CardAction.Next: study.Advance(1);break;
            case CardAction.Previous: study.Advance(-1);break;
        }
    }
    internal void CancelInteraction()
    {
        clickTimer.Stop();pointer.Cancel();
        var wasResizing=resizing is not null;resizing=null;Mouse.OverrideCursor=null;
        if(body.IsMouseCaptured)body.ReleaseMouseCapture();
        if(IsMouseCaptured)ReleaseMouseCapture();
        if(wasResizing){study.Settings.Save();NativeWindow.Restore(this,study.Settings);NativeWindow.Remember(this,study.Settings);}
    }
    internal void Conceal()
    {
        CancelInteraction();audio.Stop();
        if(ready)NativeWindow.Remember(this,study.Settings);
        Hide();
    }
    private void Drag(object sender,MouseButtonEventArgs e)
    {
        if(e.ChangedButton!=MouseButton.Left)return;
        CancelInteraction();
        try { DragMove(); } catch(InvalidOperationException) { }
        NativeWindow.Restore(this,study.Settings);NativeWindow.Remember(this,study.Settings);e.Handled=true;
    }
    private void PointerDown(object sender,MouseButtonEventArgs e)
    {
        clickTimer.Stop();
        if(UI.IsControlTarget(e.OriginalSource as DependencyObject))return;
        var point=e.GetPosition(body);pointer.Begin(point.X,point.Y);clickCount=e.ClickCount;
        body.CaptureMouse();e.Handled=true;
    }
    private void PointerMove(object sender,MouseEventArgs e)
    {
        if(!body.IsMouseCaptured || e.LeftButton!=MouseButtonState.Pressed)return;
        var point=e.GetPosition(body);pointer.Move(point.X,point.Y);
    }
    private void PointerUp(object sender,MouseButtonEventArgs e)
    {
        if(!body.IsMouseCaptured)return;
        var point=e.GetPosition(body);var action=pointer.End(point.X,point.Y,clickCount);body.ReleaseMouseCapture();
        if(action==CardAction.SingleClick)clickTimer.Start();else ApplyAction(action);
        e.Handled=true;
    }
    private void HorizontalScroll(double delta)
    {
        if(resizing is not null)return;
        var point=Mouse.GetPosition(body);
        if(point.X<0||point.Y<0||point.X>body.ActualWidth||point.Y>body.ActualHeight)return;
        CancelInteraction();
        if(wheel.Consume(delta,Environment.TickCount64/1000.0) is int direction)study.Advance(direction);
    }
    private void ResizeDown(object sender,MouseButtonEventArgs e)
    {
        var point=e.GetPosition(this);
        var edges=CardResizing.HitTest(point.X,point.Y,ActualWidth,ActualHeight);
        if(edges!=ResizeEdges.None)
        {
            CancelInteraction();
            var dpi=VisualTreeHelper.GetDpi(this);
            resizing=(edges,NativeWindow.CardFrame(this),PointToScreen(point),dpi.DpiScaleX,dpi.DpiScaleY);
            Mouse.Capture(this,CaptureMode.Element);Mouse.OverrideCursor=ResizeCursor(edges);e.Handled=true;
        }
        else if(UI.IsControlTarget(e.OriginalSource as DependencyObject))CancelInteraction();
    }
    private static Cursor ResizeCursor(ResizeEdges edges)
    {
        if((edges&(ResizeEdges.Left|ResizeEdges.Right))==0)return Cursors.SizeNS;
        if((edges&(ResizeEdges.Top|ResizeEdges.Bottom))==0)return Cursors.SizeWE;
        return edges==(ResizeEdges.Left|ResizeEdges.Top)||edges==(ResizeEdges.Right|ResizeEdges.Bottom)?Cursors.SizeNWSE:Cursors.SizeNESW;
    }
    private bool UpdateResize(MouseEventArgs e)
    {
        if(resizing is not { } drag)return false;
        var point=PointToScreen(e.GetPosition(this));
        var frame=CardResizing.Frame(drag.Frame,(point.X-drag.Point.X)/drag.DpiX,(point.Y-drag.Point.Y)/drag.DpiY,drag.Edges,study.Revealed);
        applyingSize=true;
        try{NativeWindow.ResizeCard(this,frame);}
        finally{applyingSize=false;}
        study.Settings.Value.CardSize.Remember(frame.Width,frame.Height,study.Revealed);
        return true;
    }
    private void ResizeMove(object sender,MouseEventArgs e)
    {
        if(UpdateResize(e)){e.Handled=true;return;}
        var point=e.GetPosition(this);var edges=CardResizing.HitTest(point.X,point.Y,ActualWidth,ActualHeight);
        Mouse.OverrideCursor=edges==ResizeEdges.None?null:ResizeCursor(edges);
    }
    private void ResizeUp(object sender,MouseButtonEventArgs e)
    {
        if(!UpdateResize(e))return;
        CancelInteraction();e.Handled=true;
    }
}
