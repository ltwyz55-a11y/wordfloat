using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using WordFloat.Core;

namespace WordFloat.Desktop;

internal sealed class WindowsSpeechOutput : ISpeechOutput
{
    private readonly SpeechSynthesizer? synthesizer;
    public IReadOnlyList<SpeechVoice> Voices { get; } = [];
    public WindowsSpeechOutput()
    {
        try
        {
            synthesizer = new SpeechSynthesizer { Rate = -1 };
            Voices = synthesizer.GetInstalledVoices().Where(v => v.Enabled)
                .Select(v => new SpeechVoice(v.VoiceInfo.Name,v.VoiceInfo.Culture.Name)).ToArray();
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }
    public void Stop() { synthesizer?.SpeakAsyncCancelAll(); }
    public void Speak(IReadOnlyList<SpeechPart> parts)
    {
        if (synthesizer is null) return;
        var prompt = new PromptBuilder();
        foreach (var part in parts)
        {
            prompt.StartVoice(part.VoiceId);
            prompt.AppendText(part.Text);
            prompt.EndVoice();
        }
        synthesizer.SpeakAsync(prompt);
    }
    public void Dispose() { Stop(); synthesizer?.Dispose(); }
}
