using System;
using System.Collections.Generic;
using GameDialog.Common;
using GameDialog.Runner;
using Godot;

namespace ExampleProject;

public partial class Dialog : DialogBase
{
    public DialogBox? DialogBox { get; set; }

    protected override void OnDialogLineStarted(DialogLine line)
    {
        DialogBox ??= CreateDialogBox();
        _ = DialogBox.WriteDialogLine(line);
    }

    protected override void OnDialogLineResumed()
    {
        DialogBox?.TextWriter.Resume();
    }

    protected override void OnChoice(List<Choice> choices)
    {
    }

    protected override void OnHash(Dictionary<string, string> hashData)
    {
    }

    private DialogBox CreateDialogBox()
    {
        PackedScene packedScene = GD.Load<PackedScene>("./Scenes/DialogBox.tscn");
        DialogBox newBox = packedScene.Instantiate<DialogBox>();
        newBox.LineEnded += OnDialogLineEnded;
        AddChild(newBox);
        newBox.TextWriter.Dialog = this;
        newBox.TextWriter.SpeedMultiplier = SpeedMultiplier;
        return newBox;
    }
}