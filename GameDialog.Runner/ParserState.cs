using System;
using System.Collections.Generic;

namespace GameDialog.Runner;

public class ParserState
{
    public string[] Script { get; set; } = null!;
    public ReadOnlySpan<char> Line
    {
        get
        {
            if (LineIdx >= Script.Length)
                return [];

            ReadOnlySpan<char> span = Script[LineIdx].AsSpan();

            if (_commentStart != -1)
                span = span[.._commentStart];

            return span;
        }
    }

    public int CurrentIndentLevel => _indents.Count > 0 ? _indents.Peek() : 0;
    public int CurrentIndentCount => _indents.Count;
    public int IndentChange => CurrentIndentLevel - _prevIndentLevel;
    public int LineIdx { get; private set; }
    public int Dedents { get; private set; }
    public List<Error>? Errors { get; set; }
    public IMemberStorage MemberStorage { get; set; } = null!;

    private IndentStyle _indentStyle;
    private readonly Stack<int> _indents = new();
    private int _prevIndentLevel;
    private int _commentStart;

    public void Reset()
    {
        _commentStart = -1;
        _indents.Clear();
        Dedents = 0;
        _indentStyle = IndentStyle.Unset;
        _prevIndentLevel = 0;
        Script = null!;
        Errors = null;
    }

    public bool Dedent()
    {
        if (Dedents <= 0)
            return false;

        Dedents--;
        return true;
    }

    public void MoveNextLine()
    {
        MoveLine(LineIdx + 1);
    }

    public void MoveLine(int lineIdx)
    {
        _commentStart = -1;

        if (Script == null || lineIdx < 0)
        {
            LineIdx = 0;
            UpdateIndentation();
            return;
        }

        LineIdx = Script.GetNextNonEmptyLine(lineIdx);

        if (LineIdx < Script.Length)
        {
            ReadOnlySpan<char> span = Script[LineIdx].AsSpan();
            ReadOnlySpan<char> stripped = span.StripLineComment();

            if (span.Length != stripped.Length)
                _commentStart = stripped.Length;
        }

        UpdateIndentation();
    }

    private void UpdateIndentation()
    {
        ReadOnlySpan<char> line = Line;
        Dedents = 0;

        if (_indentStyle == IndentStyle.Unset)
        {
            if (line.StartsWith(' '))
                _indentStyle = IndentStyle.Spaces;
            else if (line.StartsWith('\t'))
                _indentStyle = IndentStyle.Tabs;
            else
                return;
        }

        if (_indentStyle == IndentStyle.Spaces)
            HandleIndent(line, ' ', '\t');
        else if (_indentStyle == IndentStyle.Tabs)
            HandleIndent(line, '\t', ' ');

        void HandleIndent(ReadOnlySpan<char> line, char correct, char incorrect)
        {
            int newIndent = 0;

            while (newIndent < line.Length)
            {
                char c = line[newIndent];

                if (c == incorrect)
                {
                    Errors?.AddError(LineIdx, newIndent, newIndent + 1, "Mixed indentation detected");
                    return;
                }

                if (c != correct)
                    break;

                newIndent++;
            }

            _prevIndentLevel = CurrentIndentLevel;

            if (newIndent > CurrentIndentLevel)
            {
                _indents.Push(newIndent);
                return;
            }

            while (newIndent < CurrentIndentLevel)
            {
                Dedents++;
                _prevIndentLevel = _indents.Pop();
            }
        }
    }

    private enum IndentStyle
    {
        Unset,
        Spaces,
        Tabs
    }
}