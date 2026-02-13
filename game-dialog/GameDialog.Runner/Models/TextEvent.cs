using System;

namespace GameDialog.Runner;

/// <summary>
/// Represents a dialog text event.
/// </summary>
public struct TextEvent
{
    /// <summary>
    /// The event text content.
    /// </summary>
    public ReadOnlyMemory<char> Tag;
    /// <summary>
    /// The char index in the rendered text when the event triggers.
    /// </summary>
    public int TextIndex;
}
