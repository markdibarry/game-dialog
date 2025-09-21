using System;
using System.Collections.Generic;
using GameDialog.Common;
using GameDialog.Pooling;
using Godot;

namespace GameDialog.Runner;

public abstract partial class DialogBase : Control
{
    public DialogBase()
    {
        Name = "Dialog";
        AnchorBottom = 1.0f;
        AnchorRight = 1.0f;
        SpeedMultiplier = 1;
    }

    protected List<string> SpeakerIds { get; private set; } = [];
    protected List<string> Strings { get; private set; } = [];
    protected List<float> Floats { get; private set; } = [];
    protected List<ushort[]> Instructions { get; private set; } = [];
    protected bool SpeedUpEnabled { get; set; }
    public TextStorage TextStorage { get; } = new();
    public double SpeedMultiplier { get; private set; }
    public bool AutoProceedGlobalEnabled { get; private set; }
    public float AutoProceedGlobalTimeout { get; private set; }

    public int? Next { get; set; }

    public event Action<DialogBase>? ScriptEnded;

    /// <summary>
    /// Called when the script encounters a dialog line.
    /// </summary>
    /// <param name="line">The dialog line</param>
    protected abstract void OnDialogLineStarted(DialogLine line);
    /// <summary>
    /// Called when the in-progress dialog line is resumed, usually from an awaited function.
    /// </summary>
    protected abstract void OnDialogLineResumed();
    /// <summary>
    /// Called when a dialog line finishes.
    /// </summary>
    /// <param name="line">The dialog line</param>
    protected virtual void OnDialogLineEnded(DialogLine line)
    {
        Next = line.Next;
        Pool.Return(line);
        Resume();
    }
    /// <summary>
    /// Called when the script encounters a choice set.
    /// </summary>
    /// <param name="choices">The choices</param>
    protected abstract void OnChoice(List<Choice> choices);
    /// <summary>
    /// Called when the script encounters a Hash Tag set.
    /// </summary>
    /// <param name="hashData">The hash data set</param>
    protected virtual void OnHash(Dictionary<string, string> hashData) { }
    /// <summary>
    /// Called when the script encounters a Speaker Hash Tag set.
    /// </summary>
    /// <param name="speakerId">The speaker id</param>
    /// <param name="hashData">The hash data set</param>
    protected virtual void OnSpeakerHash(string speakerId, Dictionary<string, string> hashData) { }
}
