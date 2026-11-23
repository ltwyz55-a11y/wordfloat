using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WordFloat.Desktop;

internal static class LibraryIcons
{
    internal static FrameworkElement Create(string name, Brush ink, double size = 20)
    {
        var data = name switch
        {
            "brand" => "M 8 3 L 20 3 Q 22 3 22 5 L 22 17 M 4 7 L 16 7 Q 18 7 18 9 L 18 21 L 2 21 L 2 9 Q 2 7 4 7 Z",
            "search" => "M 16 10 A 6 6 0 1 1 4 10 A 6 6 0 1 1 16 10 M 15 15 L 21 21",
            "appearance" => "M 12 3 C 6 3 2 7 2 12 C 2 18 6 21 11 21 C 14 21 15 19 13 17 C 11 15 14 13 17 14 C 22 15 24 9 19 5 C 17 3 14 3 12 3 Z M 7 9 L 7.1 9 M 12 6 L 12.1 6 M 17 8 L 17.1 8 M 6 14 L 6.1 14",
            "settings" => "M 9 3 L 15 3 L 16 6 L 19 7 L 22 10 L 20 13 L 20 17 L 17 20 L 14 19 L 11 22 L 7 20 L 7 17 L 3 15 L 3 10 L 6 9 L 6 6 Z M 16 12 A 4 4 0 1 1 8 12 A 4 4 0 1 1 16 12",
            "check" => "M 5 12 L 10 17 L 20 6",
            _ => "M 12 6 C 9 3 5 3 2 5 L 2 20 C 5 18 9 18 12 21 C 15 18 19 18 22 20 L 22 5 C 19 3 15 3 12 6 Z M 12 6 L 12 21"
        };
        var path = new Path { Data = Geometry.Parse(data), Stroke = ink, StrokeThickness = 1.7,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round, Fill = Brushes.Transparent };
        var canvas = new Canvas { Width = 24, Height = 24 }; canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas, IsHitTestVisible = false };
    }
}
