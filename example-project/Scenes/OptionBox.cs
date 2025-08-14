using System;
using System.Collections.Generic;
using ExampleProject;
using GameDialog.Common;
using Godot;

public partial class OptionBox : MarginContainer
{
    private GridContainer _gridContainer = null!;
    public Dialog Dialog { get; set; } = null!;

    public override void _Ready()
    {
        _gridContainer = GetNode<GridContainer>("%GridContainer");
        _gridContainer.Columns = Dialog.OptionColumns;
    }

    public void Init(List<Choice> choices)
    {
        foreach (var choice in choices)
        {
            if (choice.Disabled)
                continue;

            Button button = new();
            button.Text = choice.Text;
            button.Pressed += () => OnButtonPressed(choice.Next);
            _gridContainer.AddChild(button);
        }

        Button firstButton = (Button)_gridContainer.GetChild(0);
        firstButton.GrabFocus();
    }

    public void OnButtonPressed(int next)
    {
        Dialog dialog = Dialog;
        dialog.Next = next;
        QueueFree();
        dialog.ProcessMode = ProcessModeEnum.Inherit;
        dialog.Resume();
    }
}
