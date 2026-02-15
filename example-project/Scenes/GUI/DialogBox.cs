using System;
using System.Collections.Generic;
using GameCore.Dialog;
using Godot;

namespace ExampleProject;

[Tool]
public partial class DialogBox : MarginContainer
{
    private static readonly char[] _punctuation = ['?', '!', '.'];

    public int OptionColumns { get; private set; } = 1;

    public DialogRunner DialogRunner { get; set; } = null!;
    public DialogTextLabel DialogText { get; set; } = null!;
    public PanelContainer NameContainer { get; set; } = null!;
    public Label NameLabel { get; set; } = null!;
    public List<string> SpeakerIds { get; private set; } = [];
    public MarginContainer NextArrow { get; set; } = null!;
    public AudioStreamPlayer ClackSound { get; set; } = null!;

    public override void _Ready()
    {
        NameContainer = GetNode<PanelContainer>("%NameContainer");
        NameLabel = GetNode<Label>("%NameLabel");
        NextArrow = GetNode<MarginContainer>("%NextArrow");
        ClackSound = GetNode<AudioStreamPlayer>("ClackSound");

        DialogRunner = new(this);
        DialogRunner.DialogLineStarted += OnDialogLineStarted;
        DialogRunner.DialogLineResumed += OnDialogLineResumed;
        DialogRunner.ChoiceRead += OnChoiceRead;
        DialogRunner.HashRead += OnHashRead;
        DialogText = GetNode<DialogTextLabel>("%DialogText");
        DialogText.DialogRunner = DialogRunner;
        DialogText.FinishedWriting += OnFinishedWriting;
        DialogText.CharWritten += OnCharWritten;
    }

    public override void _Input(InputEvent inputEvent)
    {
        GetViewport().SetInputAsHandled();

        if (inputEvent.IsActionPressed("ui_accept"))
        {
            if (DialogText.Writing)
            {
                DialogText.IsSpeedUpEnabled = true;
            }
            else
            {
                HandleNext();
                DialogText.IsSpeedUpEnabled = false;
            }
        }
        else if (inputEvent.IsActionReleased("ui_accept"))
        {
            DialogText.IsSpeedUpEnabled = false;
        }

        inputEvent.Dispose();
    }

    public void OnDialogLineStarted(string text, IReadOnlyList<string> speakerIds)
    {
        WriteDialogLine(text, speakerIds);
    }

    protected void OnDialogLineResumed()
    {
        DialogText.Resume();
    }

    public void OnChoiceRead(IReadOnlyList<Choice> choices)
    {
        ProcessMode = ProcessModeEnum.Disabled;
        PackedScene packedScene = GD.Load<PackedScene>("./Scenes/GUI/OptionBox.tscn");
        OptionBox optionBox = packedScene.Instantiate<OptionBox>();
        optionBox.DialogBox = this;
        Game.Root.GUI.AddChild(optionBox);
        optionBox.Init(choices);
    }

    public void OnHashRead(IReadOnlyDictionary<string, string> hashData)
    {
        if (hashData.TryGetValue("OptionColumns", out string? columnString)
            && int.TryParse(columnString, out int columns))
        {
            OptionColumns = Math.Max(columns, 1);
        }
    }

    public void OnCharWritten(int i, char c)
    {
        // Play clack after every other char
        if (c != ' ' && i % 2 == 0)
            ClackSound.Play();

        if (ShouldPause())
            DialogText.PauseTimer += 0.5;

        bool ShouldPause()
        {
            if (!_punctuation.Contains(c))
                return false;

            // Don't pause if there isn't a space after the punctuation.
            return i < DialogText.TotalChars - 1 && DialogText.CachedText[i + 1] == ' ';
        }
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

        DialogText.SetDialogText(text);
        // In Godot, when a new Control is created, it is incorrect size until the next frame.
        DialogText.CallDeferred(nameof(DialogTextLabel.WriteNextPage));
    }

    private void HandleNext()
    {
        NextArrow.Hide();

        if (!DialogText.IsComplete())
            DialogText.WriteNextPage();
        else
            DialogRunner.EndDialogLine();
    }

    private void OnFinishedWriting()
    {
        if (DialogText.AutoProceedEnabled)
            HandleNext();
        else
            NextArrow.Show();
    }
}
