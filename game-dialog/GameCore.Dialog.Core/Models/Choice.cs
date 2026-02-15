namespace GameCore.Dialog;

/// <summary>
/// Represents a dialog choice option.
/// </summary>
public readonly struct Choice
{
    /// <summary>
    /// Creates a Choice
    /// </summary>
    /// <param name="next">The next line index to be read.</param>
    /// <param name="text">The displayed text for this choice.</param>
    /// <param name="disabled">If true, this choice is not enabled.</param>
    public Choice(int next, string text, bool disabled)
    {
        Next = next;
        Text = text;
        Disabled = disabled;
    }

    /// <summary>
    /// The next line index to be read.
    /// </summary>
    public int Next { get; init; }
    /// <summary>
    /// The displayed text for this choice.
    /// </summary>
    public string Text { get; init; }
    /// <summary>
    /// If true, this choice is not enabled.
    /// </summary>
    public bool Disabled { get; init; }
}
