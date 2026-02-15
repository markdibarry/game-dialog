using System.Collections.Generic;
using GameCore.Dialog;
using Godot;

namespace ExampleProject;

public partial class OptionBox : MarginContainer
{
    private GridContainer _gridContainer = null!;
    public DialogBox DialogBox { get; set; } = null!;

    public override void _Ready()
    {
        _gridContainer = GetNode<GridContainer>("%GridContainer");
        _gridContainer.Columns = DialogBox.OptionColumns;
    }

    public void Init(IReadOnlyList<Choice> choices)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            Choice choice = choices[i];

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
        DialogBox dialogBox = DialogBox;
        QueueFree();
        dialogBox.ProcessMode = ProcessModeEnum.Inherit;
        dialogBox.DialogRunner.Resume(next);
    }
}
