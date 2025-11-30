using System;

namespace GramCloneClient.Models;

public sealed class EditRequestEventArgs : EventArgs
{
    public EditRequestEventArgs(string text, EditingMode mode, ToneStyle tone)
    {
        Text = text;
        Mode = mode;
        Tone = tone;
    }

    public string Text { get; }
    public EditingMode Mode { get; }
    public ToneStyle Tone { get; }
}
