using System;
using System.Collections.Generic;
using GameDialog.Runner;
using Godot;

namespace ExampleProject;

public partial class Dialog : DialogBase
{
    public DialogBox? DialogBox { get; set; }
    public int OptionColumns { get; private set; } = 1;

    protected override void OnDialogLineStarted(
        string text,
        IReadOnlyList<string> speakerIds,
        IReadOnlyList<TextEvent> textEvents)
    {
        DialogBox ??= CreateDialogBox();
        // Gives access to script when parsing
        DialogBox.TextWriter.Dialog = this;
        DialogBox.WriteDialogLine(text, speakerIds, textEvents);
    }

    protected override void OnDialogLineResumed()
    {
        DialogBox?.TextWriter.Resume();
    }

    protected override void OnChoice(IReadOnlyList<Choice> choices)
    {
        ProcessMode = ProcessModeEnum.Disabled;
        PackedScene packedScene = GD.Load<PackedScene>("./Scenes/GUI/OptionBox.tscn");
        OptionBox optionBox = packedScene.Instantiate<OptionBox>();
        optionBox.Dialog = this;
        Game.Root.GUI.AddChild(optionBox);
        optionBox.Init(choices);
    }

    protected override void OnHash(IReadOnlyDictionary<string, string> hashData)
    {
        if (hashData.TryGetValue("OptionColumns", out string? columnString)
            && int.TryParse(columnString, out int columns))
        {
            OptionColumns = Math.Max(columns, 1);
        }
    }

    private DialogBox CreateDialogBox()
    {
        PackedScene packedScene = GD.Load<PackedScene>("./Scenes/GUI/DialogBox.tscn");
        DialogBox newBox = packedScene.Instantiate<DialogBox>();
        newBox.LineEnded += EndDialogLine;
        AddChild(newBox);
        return newBox;
    }
}