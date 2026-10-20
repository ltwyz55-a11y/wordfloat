using System;
using System.Collections.Generic;

namespace WordFloat.Core;

// Opening, navigating and closing a card must never initialize the optional SAPI service.
public sealed class DeferredSpeechOutput(Func<ISpeechOutput> create) : ISpeechOutput
{
    private ISpeechOutput? output;
    private bool disposed;
    private ISpeechOutput Output
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return output ??= create();
        }
    }
    public IReadOnlyList<SpeechVoice> Voices => Output.Voices;
    public void Speak(IReadOnlyList<SpeechPart> parts) => Output.Speak(parts);
    public void Stop() => output?.Stop();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        output?.Dispose();
    }
}
