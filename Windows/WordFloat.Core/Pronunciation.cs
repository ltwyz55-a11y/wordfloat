using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace WordFloat.Core;

public sealed record SpeechVoice(string Id, string Language);
public sealed record SpeechPart(string Text, string VoiceId);
public interface ISpeechOutput : IDisposable
{
    IReadOnlyList<SpeechVoice> Voices { get; }
    void Stop();
    void Speak(IReadOnlyList<SpeechPart> parts);
}
public sealed class Pronunciation
{
    private readonly ISpeechOutput output;
    private readonly SettingsStore settings;
    public string? Message { get; private set; }
    public event Action? MessageChanged;
    public Pronunciation(ISpeechOutput output, SettingsStore settings) { this.output = output; this.settings = settings; }
    public string Accent
    {
        get => settings.Value.Accent;
        set { if (value is not ("en-US" or "en-GB")) return; Stop(); settings.Value.Accent = value; settings.Save(); }
    }
    public void Stop() => output.Stop();
    public void Speak(string text, bool chinese = false) => Play([(text,chinese)]);
    public void SpeakExample(BilingualExample example) => Play([(example.English,false),(example.Chinese,true)]);
    private void Play((string Text, bool Chinese)[] texts)
    {
        try
        {
            Stop();
            var parts = new List<SpeechPart>();
            var missing = new List<string>();
            var fallback = false;
            foreach (var (text,chinese) in texts)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                var prefix = chinese ? "zh" : "en";
                var preferred = chinese ? "zh-CN" : Accent;
                var candidates = output.Voices.Where(v => v.Language == prefix || v.Language.StartsWith(prefix+"-",StringComparison.OrdinalIgnoreCase)).ToArray();
                var voice = candidates.FirstOrDefault(v => v.Language == preferred) ?? candidates.FirstOrDefault();
                if (voice is null) { missing.Add(chinese ? "中文" : "英语"); continue; }
                fallback |= !chinese && voice.Language != preferred;
                parts.Add(new(text,voice.Id));
            }
            if (parts.Count > 0) output.Speak(parts);
            Message = missing.Count > 0 ? "未找到"+string.Join("、",missing.Distinct())+"语音，已跳过；请在 Windows 设置的语音选项中安装对应语言的声音。"
                : fallback ? "所选口音不可用，正在使用已安装的英语声音。" : null;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or COMException)
        { Message = "语音暂时不可用：" + e.Message; }
        MessageChanged?.Invoke();
    }
}
