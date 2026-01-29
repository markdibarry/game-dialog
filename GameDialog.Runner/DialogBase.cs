using System;
using System.Collections.Generic;
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
        DialogStorage = new(DialogBridgeBase.InternalCreate(this));
    }

    public static TranslationFileType TranslationFileType { get; set; }

    public DialogStorage DialogStorage { get; }
    public double SpeedMultiplier { get; private set; }
    public bool AutoProceedGlobalEnabled { get; private set; }
    public float AutoProceedGlobalTimeout { get; private set; }

    public event Action<DialogBase>? ScriptEnded;

    /// <summary>
    /// Called when the script encounters a dialog line.
    /// </summary>
    /// <param name="text"/>
    /// <param name="speakerIds"/>
    protected abstract void OnDialogLineStarted(string text, IReadOnlyList<string> speakerIds);
    /// <summary>
    /// Called when the in-progress dialog line is resumed, usually from an awaited function.
    /// </summary>
    protected abstract void OnDialogLineResumed();
    /// <summary>
    /// Called when the script encounters a choice set.
    /// </summary>
    /// <param name="choices">The choices</param>
    protected abstract void OnChoice(IReadOnlyList<Choice> choices);
    /// <summary>
    /// Called when the script encounters a Hash Tag set.
    /// </summary>
    /// <param name="hashData">The hash data set</param>
    protected virtual void OnHash(IReadOnlyDictionary<string, string> hashData) { }
}
