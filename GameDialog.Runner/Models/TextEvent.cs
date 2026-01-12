using System;

namespace GameDialog.Runner;

public struct TextEvent
{
    public ReadOnlyMemory<char> Tag;
    public int TextIndex;
    public bool IsAwait;
}
