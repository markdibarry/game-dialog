using System;
using System.Collections.Generic;
using GameDialog.Common;
using GameDialog.Runner;
using Godot;

namespace ExampleProject;

public partial class Dialog : DialogBase
{
    public DialogBox? DialogBox { get; set; }
    public int OptionColumns { get; private set; } = 1;

    protected override void OnDialogLineStarted(DialogLine line)
    {
        DialogBox ??= CreateDialogBox();
        // Gives access to script when parsing
        DialogBox.TextWriter.Dialog = this;
        _ = DialogBox.WriteDialogLine(line);
    }

    protected override void OnDialogLineResumed()
    {
        DialogBox?.TextWriter.Resume();
    }

    protected override void OnChoice(List<Choice> choices)
    {
        ProcessMode = ProcessModeEnum.Disabled;
        PackedScene packedScene = GD.Load<PackedScene>("./Scenes/GUI/OptionBox.tscn");
        OptionBox optionBox = packedScene.Instantiate<OptionBox>();
        optionBox.Dialog = this;
        Game.Root.GUI.AddChild(optionBox);
        optionBox.Init(choices);
    }

    protected override void OnHash(Dictionary<string, string> hashData)
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
        newBox.LineEnded += OnDialogLineEnded;
        AddChild(newBox);
        return newBox;
    }
}