using System;
using System.Collections.Generic;
using System.Linq;

namespace WordFloat.Core;

public enum CardAction { None, SingleClick, OpenLibrary, Next, Previous }
public sealed class CardPointer
{
    private (double X, double Y)? start;
    private bool moved;
    public void Begin(double x, double y) { start = (x,y); moved = false; }
    public void Move(double x, double y)
    {
        if (start is { } p && Math.Sqrt((x-p.X)*(x-p.X)+(y-p.Y)*(y-p.Y)) > 6) moved = true;
    }
    public CardAction End(double x, double y, int clicks)
    {
        if (start is not { } p) return CardAction.None;
        Move(x,y); start = null;
        var dx = x-p.X; var dy = y-p.Y;
        if (moved) return Math.Abs(dx) >= 44 && Math.Abs(dx) > Math.Abs(dy)*1.3 ? dx < 0 ? CardAction.Next : CardAction.Previous : CardAction.None;
        return clicks == 2 ? CardAction.OpenLibrary : clicks == 1 ? CardAction.SingleClick : CardAction.None;
    }
    public void Cancel() { start = null; moved = false; }
}
public sealed class HorizontalWheel
{
    private double total, last = double.NegativeInfinity;
    private bool consumed;
    public int? Consume(double delta, double seconds)
    {
        if (seconds - last > 0.25) { total = 0; consumed = false; }
        last = seconds;
        if (consumed) return null;
        total += delta;
        if (Math.Abs(total) < 80) return null;
        consumed = true;
        return total < 0 ? 1 : -1;
    }
}
public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
public static class WindowPlacement
{
    // Inputs and outputs are physical pixels, avoiding mixed-monitor DIP ambiguity.
    public static (double X, double Y) Restore(double? x, double? y, double width, double height, IReadOnlyList<ScreenRect> screens)
    {
        if (screens.Count == 0) return (0,0);
        if (x is null || y is null || !double.IsFinite(x.Value) || !double.IsFinite(y.Value))
            return (Math.Max(screens[0].X,screens[0].Right-width-24), Math.Max(screens[0].Y,screens[0].Bottom-height-24));
        var original = new ScreenRect(x.Value,y.Value,width,height);
        var screen = screens.OrderByDescending(s => Intersection(original,s)).ThenBy(s => Distance(original,s)).First();
        return (Math.Clamp(x.Value,screen.X,Math.Max(screen.X,screen.Right-width)),Math.Clamp(y.Value,screen.Y,Math.Max(screen.Y,screen.Bottom-height)));
    }
    private static double Intersection(ScreenRect a, ScreenRect b) => Math.Max(0,Math.Min(a.Right,b.Right)-Math.Max(a.X,b.X))*Math.Max(0,Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Y,b.Y));
    private static double Distance(ScreenRect a, ScreenRect b) => Math.Pow(a.X+a.Width/2-b.X-b.Width/2,2)+Math.Pow(a.Y+a.Height/2-b.Y-b.Height/2,2);
}
