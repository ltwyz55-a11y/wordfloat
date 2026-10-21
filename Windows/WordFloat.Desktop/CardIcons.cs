using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WordFloat.Desktop;

internal static class CardIcons
{
    internal static FrameworkElement Speaker(Brush ink)
    {
        // A fixed vector viewport keeps the speaker and its two sound waves crisp at any DPI.
        var path=new Path
        {
            Data=Geometry.Parse("M 11 5 L 6 9 L 3 9 L 3 15 L 6 15 L 11 19 Z M 15 8 C 17.5 10.2 17.5 13.8 15 16 M 18 5 C 22 8.8 22 15.2 18 19"),
            Stroke=ink,StrokeThickness=1.7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,
            StrokeLineJoin=PenLineJoin.Round,Fill=Brushes.Transparent
        };
        var canvas=new Canvas{Width=24,Height=24,IsHitTestVisible=false};canvas.Children.Add(path);
        return new Viewbox{Width=20,Height=20,Child=canvas,IsHitTestVisible=false,Stretch=Stretch.Uniform};
    }
}
