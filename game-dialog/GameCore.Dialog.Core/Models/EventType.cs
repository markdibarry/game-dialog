namespace GameCore.Dialog;

/// <summary>
/// TextEvent types
/// </summary>
public enum EventType
{
    /// <summary>
    /// An undefined event
    /// </summary>
    Undefined,
    /// <summary>
    /// An event that pauses the text momentarily.
    /// </summary>
    Pause,
    /// <summary>
    /// An event that changes the speed of the text being written.
    /// </summary>
    Speed,
    /// <summary>
    /// An event that makes the text auto-proceed at the end of the line.
    /// </summary>
    Auto,
    /// <summary>
    /// An event that requires a user prompt to continue.
    /// </summary>
    Prompt,
    /// <summary>
    /// An event that scrolls the text to the current line.
    /// </summary>
    Scroll
}