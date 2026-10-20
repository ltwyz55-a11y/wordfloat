using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WordFloat.Core;

public sealed class AppSettings
{
    public string SelectedBookId { get; set; } = "";
    public Dictionary<string, string> Positions { get; set; } = [];
    public string Accent { get; set; } = "en-US";
    public double? CardLeft { get; set; }
    public double? CardTop { get; set; }
    public CardSizing CardSize { get; set; } = new();
    public bool HasLaunched { get; set; }
    public CardStyle Appearance { get; set; } = new();
}
public sealed class SettingsStore
{
    public string Path { get; }
    public AppSettings Value { get; }
    public string? LastError { get; private set; }
    public SettingsStore(string path)
    {
        Path = path;
        try { Value = File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonDefaults.Options) ?? new() : new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Value = new(); }
        Value.Positions ??= [];
        Value.CardSize ??= new(); Value.CardSize.Normalize();
        if (Value.Accent is not ("en-US" or "en-GB")) Value.Accent = "en-US";
        if (Value.CardLeft is double x && !double.IsFinite(x)) Value.CardLeft = null;
        if (Value.CardTop is double y && !double.IsFinite(y)) Value.CardTop = null;
    }
    public void Save()
    {
        var temporary = Path + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(Value, JsonDefaults.Options));
            File.Move(temporary, Path, true);
            LastError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LastError = "无法保存本地设置：" + e.Message;
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }
}
