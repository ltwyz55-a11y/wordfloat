using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class UI
{
    internal static SolidColorBrush Brush(string hex, double opacity = 1)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        color.A = (byte)Math.Round(Math.Clamp(opacity,0,1)*255);
        var brush = new SolidColorBrush(color); brush.Freeze(); return brush;
    }
    internal static TextBlock Text(string text, double size = 13, Brush? color = null) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Brush("#253B31"), TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"), LineStackingStrategy = LineStackingStrategy.MaxHeight
    };
    internal static Button Button(string title, Action action, Brush? color = null, string? id = null)
    {
        var button = new Button { Content = title, Foreground = color ?? Brush("#253B31"), Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Padding = new Thickness(8,5,8,5), FontSize = 12, Cursor = Cursors.Hand,
            Tag = id, ToolTip = title, VerticalContentAlignment = VerticalAlignment.Center };
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));
        border.SetBinding(Border.BackgroundProperty,new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty,new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
        presenter.SetBinding(ContentPresenter.MarginProperty,new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        template.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(UIElement.OpacityProperty,0.65) } });
        template.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty,0.35) } });
        button.Template = template;
        button.Click += (_,_) => action();
        System.Windows.Automation.AutomationProperties.SetName(button,title);
        return button;
    }
    internal static bool IsControlTarget(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or ScrollBar or TextBoxBase or ComboBox) return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
    internal static void OpenLink(string url)
    {
        if (!Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.Scheme != "https") return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception e) { MessageBox.Show("无法打开浏览器："+e.Message,AppInfo.Name); }
    }
    internal static void PaintSurface(Border surface, CardStyle style, string? wallpaper, bool nativeBackdrop)
    {
        surface.CornerRadius = new CornerRadius(18);
        surface.BorderThickness = new Thickness(1);
        surface.BorderBrush = Brush(style.Text,0.14);
        if (style.Mode == "wallpaper" && wallpaper is not null)
        {
            try
            {
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(wallpaper); bitmap.EndInit(); bitmap.Freeze();
            var group = new DrawingGroup();
            var imageBounds=new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight);
            group.Children.Add(new ImageDrawing(bitmap,imageBounds));
            group.Children.Add(new GeometryDrawing(Brush(style.Tint,style.Opacity),null,new RectangleGeometry(imageBounds)));
            surface.Background = new DrawingBrush(group) { Stretch = Stretch.UniformToFill };
            }
            catch (Exception e) when (e is System.IO.IOException or NotSupportedException or ArgumentException)
            { surface.Background = Brush(style.Tint); }
        }
        else
        {
            var opacity = style.Mode == "solid" ? 1 : nativeBackdrop ? Math.Max(0.08,style.Opacity) : style.Mode == "clear" ? Math.Max(0.22,style.Opacity) : Math.Max(0.9,style.Opacity);
            surface.Background = Brush(style.Tint,opacity);
        }
    }
}
