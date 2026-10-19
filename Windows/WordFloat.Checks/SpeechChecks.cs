using System.Collections.Generic;
using System.IO;
using System.Linq;
using WordFloat.Core;

namespace WordFloat.Checks;

internal static partial class Program
{
    private sealed class FakeSpeech : ISpeechOutput
    {
        public IReadOnlyList<SpeechVoice> Voices { get; set; } = [new("us","en-US"),new("gb","en-GB"),new("zh","zh-CN")];
        public List<string> Events { get; } = [];
        public void Stop() => Events.Add("stop");
        public void Speak(IReadOnlyList<SpeechPart> parts) { Events.AddRange(parts.Select(p => p.VoiceId+":"+p.Text)); }
        public void Dispose() { }
    }
    static partial void RunSpeechChecks(string temporary)
    {
        Check("bilingual sequence cancels old speech and respects accent", () =>
        {
            var engine = new FakeSpeech(); var audio = new Pronunciation(engine,new SettingsStore(Path.Combine(temporary,"speech.json")));
            audio.SpeakExample(new("test","A useful design.","一个有用的设计。","原创示例",""));
            Require(engine.Events.SequenceEqual(new[] {"stop","us:A useful design.","zh:一个有用的设计。"}),"Bilingual order");
            audio.Accent="en-GB"; audio.Speak("surface");
            Require(engine.Events.TakeLast(2).SequenceEqual(new[] {"stop","gb:surface"}),"Accent switch");
            audio.Stop(); Require(engine.Events.Last()=="stop","Word change cancellation");
        });
        Check("missing language never uses an unrelated voice", () =>
        {
            var engine = new FakeSpeech { Voices=[new("au","en-AU")] };
            var audio = new Pronunciation(engine,new SettingsStore(Path.Combine(temporary,"speech-fallback.json")));
            audio.Speak("draft"); Require(engine.Events.Last()=="au:draft" && audio.Message is not null,"English fallback");
            audio.Speak("中文",true); Require(engine.Events.Last()=="stop" && audio.Message!.Contains("中文"),"Chinese must not use English");
            engine.Voices=[]; audio.Speak("draft"); Require(audio.Message!.Contains("英语"),"Missing English");
        });
    }
}
