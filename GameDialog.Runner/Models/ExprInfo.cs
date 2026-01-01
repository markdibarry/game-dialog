using System;

namespace GameDialog.Runner;

public ref struct ExprInfo
{
    public ExprInfo(ReadOnlySpan<char> line, int lineIdx, int start, int end)
    {
        Line = line;
        LineIdx = lineIdx;
        Start = start;
        End = end;
    }

    public ReadOnlySpan<char> Line;
    public int LineIdx;
    public int Start;
    public int End;
}