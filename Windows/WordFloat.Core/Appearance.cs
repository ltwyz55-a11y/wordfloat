using System;
using System.Globalization;
using System.IO;

namespace WordFloat.Core;

public sealed record CardStyle
{
    public string? Preset { get; init; } = "system";
    public string Mode { get; init; } = "frosted";
    public string Tint { get; init; } = "#EBF0ED";
    public string Text { get; init; } = "#212629";
    public double Opacity { get; init; } = 0.18;
    public bool FollowsSystem { get; init; } = true;
    public string? WallpaperFile { get; init; }
    public CardStyle Normalized() => this with
    {
        Mode = Mode is "frosted" or "clear" or "solid" or "wallpaper" ? Mode : "frosted",
        Tint = ValidColor(Tint) ? Tint : "#EBF0ED",
        Text = ValidColor(Text) ? Text : "#212629",
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : 0.18,
        WallpaperFile = IsManagedFile(WallpaperFile) ? WallpaperFile : null
    };
    public double SurfaceOpacity(bool forceOpaque = false) => forceOpaque || Mode is "solid" or "wallpaper"
        ? 1 : Math.Clamp(double.IsFinite(Opacity) ? Opacity : 0.18, 0.04, 1);
    public CardStyle Resolved(bool dark) => !FollowsSystem ? this : this with
    {
        Tint = dark ? "#252B29" : "#EBF0ED", Text = dark ? "#F3F5F4" : "#212629"
    };
    public static bool ValidColor(string? value) => value is { Length: 7 } && value[0] == '#' && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    public static bool IsManagedFile(string? value) => value is not null && value.EndsWith(".png", StringComparison.Ordinal) && Guid.TryParseExact(value[..^4], "N", out _);
}
public sealed record CardPreset(string Id, string Title, CardStyle Style)
{
    public static readonly CardPreset[] All = [
        new("system", "跟随系统", new()),
        new("frosted", "雾面玻璃", new() { Preset="frosted", Mode="frosted", Tint="#303D38", Text="#FFFFFF", Opacity=0.32, FollowsSystem=false }),
        new("clear", "清透玻璃", new() { Preset="clear", Mode="clear", Tint="#1F2E30", Text="#FFFFFF", Opacity=0.12, FollowsSystem=false }),
        new("paper", "暖白纸卡", new() { Preset="paper", Mode="solid", Tint="#F7F2E3", Text="#403B33", Opacity=1, FollowsSystem=false }),
        new("sage", "鼠尾草", new() { Preset="sage", Mode="solid", Tint="#C4D4C2", Text="#294033", Opacity=1, FollowsSystem=false }),
        new("midnight", "午夜蓝", new() { Preset="midnight", Mode="solid", Tint="#1A263B", Text="#EBF2FC", Opacity=1, FollowsSystem=false })
    ];
    public override string ToString() => Title;
}
public sealed class AppearanceController
{
    private readonly SettingsStore settings;
    public string WallpaperDirectory { get; }
    public CardStyle Style => settings.Value.Appearance;
    public string? WallpaperPath => CardStyle.IsManagedFile(Style.WallpaperFile) ? Path.Combine(WallpaperDirectory, Style.WallpaperFile!) : null;
    public event Action? Changed;
    public AppearanceController(SettingsStore settings, string directory)
    {
        this.settings = settings; WallpaperDirectory = directory;
        settings.Value.Appearance = (settings.Value.Appearance ?? new()).Normalized();
        if (Style.WallpaperFile is not null && !File.Exists(WallpaperPath))
            settings.Value.Appearance = Style.Mode == "wallpaper" ? new() : Style with { WallpaperFile = null };
    }
    public void Apply(CardPreset preset) => Commit(preset.Style with { WallpaperFile = Style.WallpaperFile });
    public void Customize(string? mode = null, string? tint = null, string? text = null, double? opacity = null)
    {
        if (mode == "wallpaper" && !File.Exists(WallpaperPath)) return;
        Commit(Style with { Preset = null, FollowsSystem = false, Mode = mode ?? Style.Mode,
            Tint = tint ?? Style.Tint, Text = text ?? Style.Text, Opacity = opacity ?? Style.Opacity });
    }
    public void SaveWallpaper(byte[] png)
    {
        byte[] signature = [137,80,78,71,13,10,26,10];
        if (png.Length < 8 || png.Length > 50 * 1024 * 1024 || !png.AsSpan(0,8).SequenceEqual(signature))
            throw new ArgumentException("请选择有效且不超过 50 MB 的图片。");
        Directory.CreateDirectory(WallpaperDirectory);
        var file = Guid.NewGuid().ToString("N") + ".png";
        File.WriteAllBytes(Path.Combine(WallpaperDirectory, file), png);
        var previous = Style.WallpaperFile;
        Commit(Style with { WallpaperFile = file, Preset = null, Mode = "wallpaper", FollowsSystem = false, Tint = "#000000", Text = "#FFFFFF", Opacity = 0.28 });
        DeleteManaged(previous);
    }
    public void RemoveWallpaper()
    {
        var previous = Style.WallpaperFile;
        Commit((Style.Mode == "wallpaper" ? new CardStyle() : Style) with { WallpaperFile = null });
        DeleteManaged(previous);
    }
    private void DeleteManaged(string? file)
    {
        if (!CardStyle.IsManagedFile(file)) return;
        try { File.Delete(Path.Combine(WallpaperDirectory, file!)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    private void Commit(CardStyle style) { settings.Value.Appearance = style.Normalized(); settings.Save(); Changed?.Invoke(); }
}
