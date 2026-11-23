using System;
using System.IO;
using System.Linq;
using WordFloat.Core;

namespace WordFloat.Checks;

internal static partial class Program
{
    static partial void RunAppearanceChecks(string temporary)
    {
        var path = Path.Combine(temporary, "appearance.json");
        var wallpapers = Path.Combine(temporary, "wallpapers");
        Check("six presets and custom colors persist", () =>
        {
            var appearance = new AppearanceController(new SettingsStore(path), wallpapers);
            foreach (var preset in CardPreset.All)
            {
                appearance.Apply(preset);
                Require(new AppearanceController(new SettingsStore(path), wallpapers).Style == preset.Style, preset.Id);
            }
            appearance.Customize(tint: "#AABBCC", text: "#112233", opacity: 0.6);
            var restored = new AppearanceController(new SettingsStore(path), wallpapers);
            Require(restored.Style.Tint == "#AABBCC" && restored.Style.Text == "#112233" && restored.Style.Opacity == 0.6, "Custom color restore");
            Require(!restored.Style.FollowsSystem && restored.Style.Preset is null, "Custom style");
        });
        Check("translucent surface respects saved opacity without an opaque fallback", () =>
        {
            Require(new CardStyle().SurfaceOpacity()==0.18,"Default glass became solid");
            Require(CardPreset.All.First(p=>p.Id=="frosted").Style.SurfaceOpacity()==0.32,"Frosted alpha");
            Require(CardPreset.All.First(p=>p.Id=="clear").Style.SurfaceOpacity()==0.12,"Clear alpha");
            Require(new CardStyle{Opacity=0.55}.SurfaceOpacity()==0.55,"Custom alpha ignored");
            Require(new CardStyle{Mode="solid",Opacity=0.1}.SurfaceOpacity()==1,"Solid preset transparency");
            Require(new CardStyle().SurfaceOpacity(true)==1,"Accessible solid fallback");
        });
        Check("managed wallpaper restore and safe removal", () =>
        {
            var appearance = new AppearanceController(new SettingsStore(path), wallpapers);
            appearance.SaveWallpaper([137,80,78,71,13,10,26,10,0]);
            var saved = appearance.WallpaperPath!;
            Require(File.Exists(saved) && appearance.Style.Mode == "wallpaper", "Save managed image");
            appearance.Apply(CardPreset.All.First());
            Require(File.Exists(saved), "Switching preset must retain managed wallpaper");
            appearance.Customize(mode: "wallpaper");
            Require(new AppearanceController(new SettingsStore(path), wallpapers).WallpaperPath == saved, "Restore wallpaper");
            appearance.RemoveWallpaper();
            Require(!File.Exists(saved) && appearance.Style.WallpaperFile is null, "Remove owned copy");
            try { appearance.SaveWallpaper([1,2,3]); throw new Exception("Invalid image accepted"); } catch (ArgumentException) { }
        });
        Check("invalid appearance and path traversal normalize", () =>
        {
            var style = new CardStyle { Tint="bad", Text="#FFFFFF", Mode="bad", Opacity=double.NaN, WallpaperFile="../../private.png" }.Normalized();
            Require(style.Tint == "#EBF0ED" && style.Mode == "frosted" && style.Opacity == 0.18 && style.WallpaperFile is null, "Unsafe style");
            var prefs = new SettingsStore(path);
            prefs.Value.Appearance = new CardStyle { Mode="wallpaper", WallpaperFile=Guid.NewGuid().ToString("N")+".png" }; prefs.Save();
            Require(new AppearanceController(new SettingsStore(path), wallpapers).Style == new CardStyle(), "Missing wallpaper fallback");
            Require(new CardStyle().Resolved(true).Text == "#F3F5F4", "System dark mode");
        });
    }
}
