using System.Collections.Generic;
using System.Linq;
using GameDialog.Pooling;

namespace GameDialog.Runner;

public class DialogLine : IPoolable
{
    /// <summary>
    /// The ids of the speakers present in the line.
    /// </summary>
    public List<string> SpeakerIds { get; } = [];
    /// <summary>
    /// Text containing BBCode and Event tags
    /// </summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>
    /// The next instruction index to navigate to.
    /// </summary>
    public int Next { get; set; }

    public bool SameSpeakers(DialogLine secondLine)
    {
        if (SpeakerIds.Count != secondLine.SpeakerIds.Count)
            return false;

        foreach (string id in SpeakerIds)
        {
            if (!secondLine.SpeakerIds.Any(x => x == id))
                return false;
        }

        return true;
    }

    public bool AnySpeakers(DialogLine secondLine)
    {
        foreach (string id in SpeakerIds)
        {
            if (secondLine.SpeakerIds.Any(x => x == id))
                return true;
        }

        return false;
    }

    public void ClearObject()
    {
        Next = default;
        SpeakerIds.Clear();
        Text = string.Empty;
    }
}
