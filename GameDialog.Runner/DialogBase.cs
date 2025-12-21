using System;
using System.Collections.Generic;
using System.IO;
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
        SpeakerIds = [];
        DialogBridgeBase dialogBridge = DialogBridgeBase.InternalCreate(this);
        DialogStorage = new(dialogBridge);
        _state = new();
        _state.MemberStorage = DialogStorage;
        _exprParser = new(_state);
    }

    protected List<string> SpeakerIds { get; private set; }
    protected string[] Script => _state.Script;
    protected int LineIdx => _state.LineIdx;
    protected bool SpeedUpEnabled { get; set; }

    public int? Next { get; set; }
    public DialogStorage DialogStorage { get; }
    public double SpeedMultiplier { get; private set; }
    public bool AutoProceedGlobalEnabled { get; private set; }
    public float AutoProceedGlobalTimeout { get; private set; }

    private Validator? _validator;
    private readonly ExprParser _exprParser;
    private readonly ParserState _state;

    public event Action<DialogBase>? ScriptEnded;

    public void LoadScript(string path)
    {
        string gPath = Godot.ProjectSettings.GlobalizePath(path);

        if (!File.Exists(gPath))
            return;

        string[] script = File.ReadAllLines(gPath);
        _state.Script = script;
    }

    /// <summary>
    /// Called when the script encounters a dialog line.
    /// </summary>
    /// <param name="text"/>
    /// <param name="speakerIds"/>
    /// <paramref name="textEvents"/>
    protected abstract void OnDialogLineStarted(string text, ReadOnlySpan<string> speakerIds, ReadOnlySpan<TextEvent> textEvents);
    /// <summary>
    /// Called when the in-progress dialog line is resumed, usually from an awaited function.
    /// </summary>
    protected abstract void OnDialogLineResumed();
    /// <summary>
    /// Called when a dialog line finishes.
    /// </summary>
    protected virtual void OnDialogLineEnded()
    {
        Next = LineIdx + 1;
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
