using System;

namespace WordFloat.Core;

public readonly record struct CardSize(double Width,double Height);
public sealed class CardSizing
{
    public double Width { get; set; } = 260;
    public double CompactHeight { get; set; } = 150;
    public double ExpandedHeight { get; set; } = 220;
    public const double MinimumWidth=220, MaximumWidth=520, MaximumHeight=420;
    public static double MinimumHeight(bool revealed)=>revealed?200:130;
    public CardSize Size(bool revealed)=>Clamp(Width,revealed?ExpandedHeight:CompactHeight,revealed);
    public static CardSize Clamp(double width,double height,bool revealed)=>new(
        Bound(width,MinimumWidth,MaximumWidth,260),Bound(height,MinimumHeight(revealed),MaximumHeight,revealed?220:150));
    public void Normalize()
    {
        Width=Bound(Width,220,520,260);CompactHeight=Bound(CompactHeight,130,420,150);ExpandedHeight=Bound(ExpandedHeight,200,420,220);
    }
    public void Remember(double width,double height,bool revealed)
    {
        var size=Clamp(width,height,revealed);Width=size.Width;
        if(revealed)ExpandedHeight=size.Height;else CompactHeight=size.Height;
    }
    private static double Bound(double value,double min,double max,double fallback)=>double.IsFinite(value)?Math.Clamp(value,min,max):fallback;
}
[Flags]
public enum ResizeEdges { None=0,Left=1,Right=2,Top=4,Bottom=8 }
public static class CardResizing
{
    public static ResizeEdges HitTest(double x,double y,double width,double height)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y)||x<0||y<0||x>width||y>height)return ResizeEdges.None;
        var horizontal=x<14?ResizeEdges.Left:x>width-14?ResizeEdges.Right:ResizeEdges.None;
        var vertical=y<14?ResizeEdges.Top:y>height-14?ResizeEdges.Bottom:ResizeEdges.None;
        if(horizontal!=0&&vertical!=0)return horizontal|vertical;
        return x<6?ResizeEdges.Left:x>width-6?ResizeEdges.Right:y<6?ResizeEdges.Top:y>height-6?ResizeEdges.Bottom:ResizeEdges.None;
    }
    public static ScreenRect Frame(ScreenRect start,double dx,double dy,ResizeEdges edges,bool revealed)
    {
        if(edges==ResizeEdges.None||!double.IsFinite(dx)||!double.IsFinite(dy))return start;
        var size=CardSizing.Clamp(start.Width+(edges.HasFlag(ResizeEdges.Left)?-dx:edges.HasFlag(ResizeEdges.Right)?dx:0),
            start.Height+(edges.HasFlag(ResizeEdges.Top)?-dy:edges.HasFlag(ResizeEdges.Bottom)?dy:0),revealed);
        return new(edges.HasFlag(ResizeEdges.Left)?start.Right-size.Width:start.X,
            edges.HasFlag(ResizeEdges.Top)?start.Bottom-size.Height:start.Y,size.Width,size.Height);
    }
}
