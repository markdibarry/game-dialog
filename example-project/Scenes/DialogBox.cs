using System;
using System.Threading.Tasks;
using GameDialog.Runner;
using Godot;

namespace ExampleProject;

[Tool]
public partial class DialogBox : MarginContainer
{
    public TextWriter TextWriter { get; set; } = null!;
    public DialogLine DialogLine { get; private set; } = null!;
    public MarginContainer NextArrow { get; set; } = null!;
    public event Action<DialogLine>? LineEnded;

    public override void _Ready()
    {
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

    public async ValueTask WriteDialogLine(DialogLine dialogLine)
    {
        DialogLine = dialogLine;
        // In Godot, when a new Control is created, it is incorrect size until the next frame.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        TextWriter.SetParsedText(dialogLine.Text);
        TextWriter.WriteNextLine();
    }

    private void HandleNext()
    {
        NextArrow.Hide();

        if (!TextWriter.IsComplete())
            TextWriter.WriteNextLine();
        else
            LineEnded?.Invoke(DialogLine);
    }

    private void OnFinishedWriting()
    {
        if (DialogLine == null)
            return;

        if (TextWriter.AutoProceedEnabled)
            HandleNext();
        else
            NextArrow.Show();
    }
}
