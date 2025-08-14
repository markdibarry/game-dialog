using System;
using GameDialog.Runner;
using Godot;

namespace ExampleProject;

public partial class DialogArea : Area2D
{
    [Export(PropertyHint.File, "*.json")]
    public string DialogPath { get; set; } = string.Empty;
    public int TimesTalked { get; set; }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public void OnBodyEntered(Node2D node2D)
    {
        if (node2D is Character character)
        {
            character.DialogArea = this;
        }
    }

    public void OnBodyExited(Node2D node2D)
    {
        if (node2D is Character character)
        {
            character.DialogArea = null;
        }
    }

    public void RunDialog()
    {
        Game.Root.TestScene.ProcessMode = ProcessModeEnum.Disabled;
        Dialog dialog = new();
        dialog.ScriptEnded += RemoveDialog;
        dialog.LoadScript(DialogPath);
        Game.Root.GUI.AddChild(dialog);
        TimesTalked++;
        dialog.StartScript();
    }

    public void RemoveDialog(DialogBase dialog)
    {
        dialog.QueueFree();
        Game.Root.TestScene.SetDeferred(Node.PropertyName.ProcessMode, (int)ProcessModeEnum.Inherit);
    }
}
