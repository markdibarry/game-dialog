using System;
using System.Collections.Generic;
using GameDialog.Runner;
using Godot;

namespace ExampleProject;

[Tool]
public partial class DialogBox : MarginContainer
{
    public PanelContainer NameContainer { get; set; } = null!;
    public Label NameLabel { get; set; } = null!;
    public TextWriter TextWriter { get; set; } = null!;
    public List<string> SpeakerIds { get; private set; } = [];
    public MarginContainer NextArrow { get; set; } = null!;
    public event Action? LineEnded;

    public override void _Ready()
    {
        NameContainer = GetNode<PanelContainer>("%NameContainer");
        NameLabel = GetNode<Label>("%NameLabel");
        NextArrow = GetNode<MarginContainer>("%NextArrow");
        TextWriter = GetNode<TextWriter>("%PagedText");
        TextWriter.FinishedWriting += OnFinishedWriting;
    }

    public override void _Input(InputEvent inputEvent)
    {
        GetViewport().SetInputAsHandled();

        if (inputEvent.IsActionPressed("ui_accept"))
        {
            if (TextWriter.Writing)
            {
                TextWriter.IsSpeedUpEnabled = true;
            }
            else
            {
                HandleNext();
                TextWriter.IsSpeedUpEnabled = false;
            }
        }
        else if (inputEvent.IsActionReleased("ui_accept"))
        {
            TextWriter.IsSpeedUpEnabled = false;
        }

        inputEvent.Dispose();
    }

    public void WriteDialogLine(string text, IReadOnlyList<string> speakerIds)
    {
        SpeakerIds.Clear();
        SpeakerIds.AddRange(speakerIds);

        if (SpeakerIds.Count == 0)
        {
            NameContainer.Visible = false;
        }
        else
        {
            NameContainer.Visible = true;
            NameLabel.Text = string.Join(", ", SpeakerIds);
        }

        TextWriter.SetDialogText(text);
        // In Godot, when a new Control is created, it is incorrect size until the next frame.
        TextWriter.CallDeferred(TextWriter.MethodName.WriteNextPage);
    }

    private void HandleNext()
    {
        NextArrow.Hide();

        if (!TextWriter.IsComplete())
            TextWriter.WriteNextPage();
        else
            LineEnded?.Invoke();
    }

    private void OnFinishedWriting()
    {
        if (TextWriter.AutoProceedEnabled)
            HandleNext();
        else
            NextArrow.Show();
    }
}
