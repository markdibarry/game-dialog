using GameCore.Dialog;
using Godot;

namespace ExampleProject;

public partial class DialogArea : Area2D
{
    [Export(PropertyHint.File, "*.dia")]
    public string DialogPath { get; set; } = string.Empty;
    public int TimesTalked { get; set; }
    public Label? Label { get; set; }

    public override void _Ready()
    {
        Label = GetNodeOrNull<Label>("Label");
        Modulate = Colors.Gray;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public void OnBodyEntered(Node2D node2D)
    {
        if (node2D is not Character character || Game.Root.TestScene.ProcessMode == ProcessModeEnum.Disabled)
            return;

        character.DialogArea = this;
        Modulate = Colors.White;

        if (Label is null)
            return;

        Tween tween = CreateTween();
        tween.TweenProperty(Label, "position:y", -40, 0.20);
    }

    public void OnBodyExited(Node2D node2D)
    {
        if (node2D is not Character character || Game.Root.TestScene.ProcessMode == ProcessModeEnum.Disabled)
            return;

        character.DialogArea = null;
        Modulate = Colors.Gray;
        
        if (Label is null)
            return;

        Tween tween = CreateTween();
        tween.TweenProperty(Label, "position:y", -35, 0.20);
    }

    public void RunDialog()
    {
        Game.Root.TestScene.ProcessMode = ProcessModeEnum.Disabled;
        PackedScene packedScene = GD.Load<PackedScene>("./Scenes/GUI/DialogBox.tscn");
        DialogBox dialogBox = packedScene.Instantiate<DialogBox>();
        Game.Root.GUI.AddChild(dialogBox);
        dialogBox.DialogRunner.ScriptEnded += RemoveDialog;
        dialogBox.DialogRunner.Load(DialogPath);
        TimesTalked++;
        dialogBox.DialogRunner.Start();
    }

    public static void RemoveDialog(DialogRunner dialogRunner)
    {
        dialogRunner.Context.QueueFree();
        Game.Root.TestScene.SetDeferred(Node.PropertyName.ProcessMode, (int)ProcessModeEnum.Inherit);
    }
}
