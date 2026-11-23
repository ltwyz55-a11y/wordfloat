using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WordFloat.Core;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WordFloat.Desktop;

internal sealed class AppearancePage : UserControl
{
    private readonly StudySession study;
    private readonly Pronunciation audio;
    private readonly AppearanceController appearance;
    private readonly ContentControl preview = new() { HorizontalContentAlignment=HorizontalAlignment.Center };
    private readonly CheckBox reveal = new() { Content="显示释义", Margin=new Thickness(0,12,0,18) };
    private readonly ComboBox mode = new() { Margin=new Thickness(0,6,0,16), Padding=new Thickness(6), Foreground=UI.Brush("#253B31"), Background=Brushes.White };
    private readonly Slider opacity = new() { Minimum=0,Maximum=1,TickFrequency=0.01,Margin=new Thickness(0,8,0,18) };
    private readonly Button tint, text, remove;
    private readonly TextBlock opacityLabel = new();
    private bool updating;
    private Brush Ink => UI.Brush(NativeWindow.IsDark?"#EDF3EF":"#253B31");
    private Brush Muted => UI.Brush(NativeWindow.IsDark?"#A6B6AC":"#738078");
    private sealed record ModeItem(string Id,string Title) { public override string ToString()=>Title; }
    private sealed class DialogOwner(IntPtr handle) : Forms.IWin32Window { public IntPtr Handle=>handle; }
    internal AppearancePage(StudySession study,Pronunciation audio,AppearanceController appearance)
    {
        this.study=study;this.audio=audio;this.appearance=appearance;
        var root=new DockPanel();
        var heading=new DockPanel{Margin=new Thickness(0,0,0,18)};
        var import=UI.Button("图片壁纸…",ImportWallpaper,Ink,"appearance.import");DockPanel.SetDock(import,Dock.Right);heading.Children.Add(import);heading.Children.Add(UI.Text("卡片外观",25,Ink));
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var settings=new StackPanel();settings.Children.Add(preview);reveal.Foreground=Muted;reveal.HorizontalAlignment=HorizontalAlignment.Center;
        reveal.Checked+=(_,_)=>UpdatePreview();reveal.Unchecked+=(_,_)=>UpdatePreview();settings.Children.Add(reveal);
        settings.Children.Add(UI.Text("预设",12,Muted));
        var presets=new WrapPanel{Margin=new Thickness(0,7,0,18)};
        foreach(var preset in CardPreset.All)
        {
            var button=UI.Button(preset.Title,()=>appearance.Apply(preset),Ink,"preset."+preset.Id);button.Margin=new Thickness(0,0,8,6);button.Background=UI.Brush(NativeWindow.IsDark?"#303F34":"#E7EDE4");presets.Children.Add(button);
        }
        settings.Children.Add(presets);settings.Children.Add(UI.Text("背景效果",12,Muted));
        mode.ItemsSource=new[]{new ModeItem("frosted","雾面半透明"),new ModeItem("clear","清透半透明"),new ModeItem("solid","纯色"),new ModeItem("wallpaper","图片壁纸")};
        mode.SelectionChanged+=(_,_)=>{if(!updating && mode.SelectedItem is ModeItem item){appearance.Customize(mode:item.Id);if(appearance.Style.Mode!=item.Id)UpdatePreview();}};settings.Children.Add(mode);
        var colors=new Grid{Margin=new Thickness(0,0,0,18)};colors.ColumnDefinitions.Add(new ColumnDefinition());colors.ColumnDefinitions.Add(new ColumnDefinition());
        var tintGroup=new StackPanel{Margin=new Thickness(0,0,10,0)};tintGroup.Children.Add(UI.Text("卡片颜色",12,Muted));
        tint=UI.Button("",()=>PickColor(false),Ink,"appearance.tint");tint.Margin=new Thickness(0,7,0,0);tintGroup.Children.Add(tint);colors.Children.Add(tintGroup);
        var textGroup=new StackPanel{Margin=new Thickness(10,0,0,0)};textGroup.Children.Add(UI.Text("字体颜色",12,Muted));text=UI.Button("",()=>PickColor(true),Ink,"appearance.text");text.Margin=new Thickness(0,7,0,0);textGroup.Children.Add(text);Grid.SetColumn(textGroup,1);colors.Children.Add(textGroup);settings.Children.Add(colors);
        opacityLabel.FontSize=12;opacityLabel.Foreground=Muted;settings.Children.Add(opacityLabel);
        opacity.ValueChanged+=(_,_)=>{if(!updating)appearance.Customize(opacity:opacity.Value);};settings.Children.Add(opacity);
        var actions=new WrapPanel();remove=UI.Button("移除图片",appearance.RemoveWallpaper,Ink,"appearance.remove");actions.Children.Add(remove);actions.Children.Add(UI.Button("恢复默认",()=>appearance.Apply(CardPreset.All[0]),Ink,"appearance.reset"));settings.Children.Add(actions);
        root.Children.Add(new ScrollViewer{Content=settings,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});Content=root;
        Loaded+=(_,_)=>{appearance.Changed+=UpdatePreview;study.Changed+=UpdatePreview;UpdatePreview();};
        Unloaded+=(_,_)=>{appearance.Changed-=UpdatePreview;study.Changed-=UpdatePreview;};
        UpdatePreview();
    }
    private void UpdatePreview()
    {
        updating=true;
        try
        {
            preview.Content=CardContent.Preview(study,audio,appearance,reveal.IsChecked==true);
            foreach(ModeItem item in mode.Items)if(item.Id==appearance.Style.Mode)mode.SelectedItem=item;
            opacity.Value=appearance.Style.Opacity;opacity.IsEnabled=appearance.Style.Mode!="solid";
            opacityLabel.Text=appearance.Style.Mode=="wallpaper"?$"图片蒙层  {appearance.Style.Opacity:P0}":appearance.Style.Mode=="solid"?"背景不透明度  100%":$"背景不透明度  {appearance.Style.Opacity:P0}";
            Swatch(tint,appearance.Style.Tint);Swatch(text,appearance.Style.Text);
            remove.IsEnabled=appearance.WallpaperPath is not null;
        }
        finally{updating=false;}
    }
    private static void Swatch(Button button,string hex)
    {
        var color=(Color)ColorConverter.ConvertFromString(hex);button.Background=UI.Brush(hex);button.Content="选择颜色  "+hex;
        button.Foreground=UI.Brush(color.R*0.299+color.G*0.587+color.B*0.114>145?"#17281F":"#FFFFFF");
    }
    private void PickColor(bool font)
    {
        var hex=font?appearance.Style.Text:appearance.Style.Tint;
        using var dialog=new Forms.ColorDialog{FullOpen=true,AnyColor=true,Color=Drawing.ColorTranslator.FromHtml(hex)};
        var owner=Window.GetWindow(this);
        if(dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(owner).Handle))!=Forms.DialogResult.OK)return;
        var value=$"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        if(font)appearance.Customize(text:value);else appearance.Customize(tint:value);
    }
    private void ImportWallpaper()
    {
        var dialog=new OpenFileDialog{Title="选择卡片壁纸",Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",Multiselect=false};
        if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;
        try
        {
            var info=new FileInfo(dialog.FileName);
            if(info.Length<=0 || info.Length>50*1024*1024)throw new ArgumentException("请选择 50 MB 以内的图片。");
            using var source=File.OpenRead(dialog.FileName);
            var frame=BitmapFrame.Create(source,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
            if(frame.PixelWidth<=0 || frame.PixelHeight<=0 || (long)frame.PixelWidth*frame.PixelHeight>100_000_000)throw new ArgumentException("图片尺寸过大，请选择较小的图片。");
            var ratio=Math.Min(1,1600.0/Math.Max(frame.PixelWidth,frame.PixelHeight));
            var width=Math.Max(1,(int)(frame.PixelWidth*ratio));source.Position=0;
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=width;image.StreamSource=source;image.EndInit();image.Freeze();
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var bytes=new MemoryStream();encoder.Save(bytes);appearance.SaveWallpaper(bytes.ToArray());
        }
        catch(Exception e) when(e is IOException or NotSupportedException or ArgumentException or UnauthorizedAccessException)
        {MessageBox.Show(Window.GetWindow(this),"无法导入图片："+e.Message,AppInfo.Name,MessageBoxButton.OK,MessageBoxImage.Information);}
    }
}
