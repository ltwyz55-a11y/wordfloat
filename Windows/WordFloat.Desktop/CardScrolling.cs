using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal static class CardScrolling
{
    internal static ScrollViewer Create(FrameworkElement content,CardStyle style)
    {
        var viewer=new ScrollViewer
        {
            Content=content,Background=Brushes.Transparent,BorderThickness=new Thickness(0),
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
            PanningMode=PanningMode.VerticalOnly,CanContentScroll=false
        };
        var track=new FrameworkElementFactory(typeof(CardScrollTrack),"PART_Track");
        var template=new ControlTemplate(typeof(ScrollBar)){VisualTree=track};
        var bar=new Style(typeof(ScrollBar));
        bar.Setters.Add(new Setter(FrameworkElement.OverridesDefaultStyleProperty,true));
        bar.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));
        bar.Setters.Add(new Setter(Control.ForegroundProperty,ThumbBrush(style)));
        bar.Setters.Add(new Setter(FrameworkElement.WidthProperty,12.0));
        bar.Setters.Add(new Setter(FrameworkElement.MinWidthProperty,12.0));
        // Leave the outer six pixels for the card's edge-resize gesture.
        bar.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,3,6,3)));
        bar.Setters.Add(new Setter(UIElement.FocusableProperty,false));
        bar.Setters.Add(new Setter(Control.TemplateProperty,template));
        viewer.Resources[typeof(ScrollBar)]=bar;
        return viewer;
    }
    private static Brush ThumbBrush(CardStyle style)
    {
        if(SystemParameters.HighContrast)return SystemColors.ControlTextBrush;
        var tint=(Color)ColorConverter.ConvertFromString(style.Tint);
        // Shift only brightness, retaining the card tint's colour family.
        var anchor=tint.R*0.2126+tint.G*0.7152+tint.B*0.0722>140?0:255;
        byte Blend(byte channel)=>(byte)Math.Round(channel*0.48+anchor*0.52);
        var brush=new SolidColorBrush(Color.FromRgb(Blend(tint.R),Blend(tint.G),Blend(tint.B)));brush.Freeze();return brush;
    }
}

// Track's thumb/page buttons are CLR properties; construct them before ScrollBar applies its template.
internal sealed class CardScrollTrack : Track
{
    public CardScrollTrack()
    {
        Orientation=Orientation.Vertical;IsDirectionReversed=true;
        Bind(MinimumProperty,"Minimum");Bind(MaximumProperty,"Maximum");Bind(ViewportSizeProperty,"ViewportSize");
        SetBinding(ValueProperty,new Binding("Value"){RelativeSource=RelativeSource.TemplatedParent,Mode=BindingMode.TwoWay});
        Thumb=new Thumb{Focusable=false,IsTabStop=false,MinHeight=24,Cursor=Cursors.Arrow,OverridesDefaultStyle=true,Template=ThumbTemplate()};
        Thumb.SetBinding(Control.ForegroundProperty,new Binding("Foreground"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(ScrollBar),1)});
        DecreaseRepeatButton=PageButton(ScrollBar.PageUpCommand);IncreaseRepeatButton=PageButton(ScrollBar.PageDownCommand);
    }
    private void Bind(DependencyProperty property,string path)=>SetBinding(property,new Binding(path){RelativeSource=RelativeSource.TemplatedParent});
    private static RepeatButton PageButton(RoutedCommand command)
    {
        var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        return new RepeatButton{Focusable=false,IsTabStop=false,Command=command,OverridesDefaultStyle=true,Template=new ControlTemplate(typeof(RepeatButton)){VisualTree=hit}};
    }
    private static ControlTemplate ThumbTemplate()
    {
        var hit=new FrameworkElementFactory(typeof(Grid));hit.SetValue(Panel.BackgroundProperty,Brushes.Transparent);
        var pill=new FrameworkElementFactory(typeof(Border),"Pill");
        pill.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));pill.SetValue(FrameworkElement.MarginProperty,new Thickness(3,0,3,0));
        pill.SetValue(UIElement.OpacityProperty,SystemParameters.HighContrast?1.0:0.62);
        pill.SetBinding(Border.BackgroundProperty,new Binding("Foreground"){RelativeSource=RelativeSource.TemplatedParent});hit.AppendChild(pill);
        var template=new ControlTemplate(typeof(Thumb)){VisualTree=hit};
        template.Triggers.Add(new Trigger{Property=UIElement.IsMouseOverProperty,Value=true,Setters={new Setter(UIElement.OpacityProperty,0.9,"Pill")}});
        template.Triggers.Add(new Trigger{Property=Thumb.IsDraggingProperty,Value=true,Setters={new Setter(UIElement.OpacityProperty,1.0,"Pill")}});
        return template;
    }
}
