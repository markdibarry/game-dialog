using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using GameDialog.Common;

namespace GameDialog.Parser;

public partial class Parser
{
    private IndentStyle _indentStyle;
    private int _indentLevel;
    private HashSet<string> _sectionTitles = [];
    private List<Error> _errors = [];

    public void ValidateScript(string[] fileLines)
    {
        if (fileLines.Length == 0)
        {
            _errors.Add(new(0, 0, 0, "Script is empty"));
            return;
        }

        ValidateTitles(fileLines);
        ValidateSections(fileLines);
    }

    private void ValidateTitles(string[] fileLines)
    {
        for (int i = 0; i < fileLines.Length; i++)
        {
            string line = fileLines[i];
            var span = line.AsSpan();

            if (TryGetTitle(span, out string? title))
            {
                if (!_sectionTitles.Add(title))
                {
                    AddError(i, 0, line.Length, $"Duplicate section title '{title}'");
                }
            }
        }

        if (_sectionTitles.Count == 0)
            AddError(0, 0, fileLines[0].Length - 1, "No section titles found. Ensure your script has at least one title in the format --Title--");
    }

    private void ValidateSections(string[] fileLines)
    {
        _indentStyle = IndentStyle.Unset;
        _indentLevel = 0;
        
        for (int i = 0; i < fileLines.Length; i++)
        {
            string line = fileLines[i];

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var span = line.AsSpan();
            int indentChange = GetIndentation(i, span);
            var trimmed = span.TrimStart();

            if (trimmed.StartsWith("/*"))
                SkipMultilineComment(fileLines, ref i);
            if (trimmed.StartsWith('['))
                ValidateExpressionBlock(trimmed, ref i);
            else if (trimmed.StartsWith('?'))
                ValidateChoiceBlock(trimmed, ref i);
            else if (trimmed.StartsWith("if "))
                ValidateConditionBlock(trimmed, ref i);
            else
                ValidateLineBlock(trimmed, ref i);
        }
    }

    private void SkipMultilineComment(string[] fileLines, ref int lineIdx)
    {
        for (int i = lineIdx; i < fileLines.Length; i++)
        {
            var line = fileLines[i].AsSpan().Trim();

            if (line.EndsWith("*/"))
            {
                lineIdx = i + 1;
                return;
            }
        }

        AddError(lineIdx, 0, fileLines[lineIdx].Length, "Unterminated multiline comment");
    }

    private TextVariant ValidateExpressionBlock(ReadOnlySpan<char> line, ref int lineIdx)
    {
        line = StripLineComment(line);
        line = line.TrimEnd();

        if (!line.EndsWith(']'))
        {
            AddError(lineIdx, 0, line.Length, "Unterminated expression block. Missing closing ']'");
            return new();
        }

        int i = 1;
        SkipSpaces(line, ref i);
        line = line[..^1].TrimEnd();

        if (line[i] == '#')
            ValidateHashExpression(line, ref lineIdx, ref i);
        else if (AssignStartRegex().IsMatch(line[i..]))
            ValidateAssignExpression(line, ref lineIdx, ref i);
        else
            return ValidateExpression(line, ref lineIdx, ref i);

        return new();
    }

    public static void SkipSpaces(ReadOnlySpan<char> line, ref int lineIdx)
    {
        while (lineIdx < line.Length && char.IsWhiteSpace(line[lineIdx]))
            lineIdx++;
    }

    private TextVariant ValidateExpression(ReadOnlySpan<char> line, ref int lineIdx, ref int lineChar)
    {
        return new();
    }
    
    private TextVariant ValidateAssignExpression(ReadOnlySpan<char> line, ref int lineIdx, ref int lineChar)
    {
        return new();
    }

    private void ValidateConditionBlock(ReadOnlySpan<char> line, ref int lineIdx)
    {

    }

    private void ValidateChoiceBlock(ReadOnlySpan<char> line, ref int lineIdx)
    {

    }

    private void ValidateLineBlock(ReadOnlySpan<char> line, ref int lineIdx)
    {
        
    }

    private void ValidateHashExpression(ReadOnlySpan<char> line, ref int lineIdx, ref int lineChar)
    {

    }

    public static bool TryGetTitle(ReadOnlySpan<char> span, [NotNullWhen(true)] out string? title)
    {
        title = null;
        span = StripLineComment(span);

        if (!TitleRegex().IsMatch(span))
            return false;

        Regex.ValueMatchEnumerator matches = WordRegex().EnumerateMatches(span);
        matches.MoveNext();
        ValueMatch valueMatch = matches.Current;
        title = span.Slice(valueMatch.Index, valueMatch.Length).ToString();
        return true;
    }

    private int GetIndentation(int lineIdx, ReadOnlySpan<char> line)
    {
        if (_indentStyle == IndentStyle.Unset)
        {
            if (line.StartsWith(' '))
                _indentStyle = IndentStyle.Spaces;
            else if (line.StartsWith('\t'))
                _indentStyle = IndentStyle.Tabs;
        }
        else if (_indentStyle == IndentStyle.Spaces)
            return HandleIndent(lineIdx, line, ' ', '\t');
        else if (_indentStyle == IndentStyle.Tabs)
            return HandleIndent(lineIdx, line, '\t', ' ');

        return 0;

        int HandleIndent(int lineIdx, ReadOnlySpan<char> line, char correct, char incorrect)
        {
            int currentIndentLevel = 0;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == incorrect)
                {
                    AddError(lineIdx, i, i + 1, "Mixed indentation detected");
                    return 0;
                }
                else if (line[i] != correct)
                {
                    currentIndentLevel++;
                }
                else
                {
                    int prev = _indentLevel;
                    _indentLevel = currentIndentLevel;
                    return currentIndentLevel - prev;
                }
            }

            return 0;
        }
    }

    private static ReadOnlySpan<char> StripLineComment(ReadOnlySpan<char> line)
    {
        foreach (var valueMatch in CommentRegex().EnumerateMatches(line))
            line = line[..valueMatch.Index];

        return line;
    }

    private enum IndentStyle
    {
        Unset,
        Spaces,
        Tabs
    }

    [GeneratedRegex(@"^\s*--\w+--\s*$")]
    private static partial Regex TitleRegex();
    [GeneratedRegex(@"\w+")]
    private static partial Regex WordRegex();
    [GeneratedRegex(@"\w+\s*=\s*(?:""|\w)")]
    private static partial Regex AssignStartRegex();
    [GeneratedRegex(@"true|false")]
    private static partial Regex BooleanRegex();
    private const string HashOrAssign = @"#\w+(?:\s*=\s*(?:\w+|"".*""))?";
    private const string HashExpression = "^" + HashOrAssign + @"(\s+" + HashOrAssign + ")*$";
    [GeneratedRegex(HashExpression)]
    private static partial Regex HashExpressionRegex();
    [GeneratedRegex("")]
    private static partial Regex ExpressionRegex();
    [GeneratedRegex(@"^#\w+$")]
    private static partial Regex HashRegex();
    [GeneratedRegex(@"^#\w+\s*=\s*(?:\w+|"".*"")$")]
    private static partial Regex HashAssignRegex();
    [GeneratedRegex(@"//(?:.*?)$")]
    private static partial Regex CommentRegex();

    private void AddError(int line, int start, int end, string message)
    {
        _errors.Add(new(line, start, end, message));
    }
}

public class Error
{
    public Error(int line, int start, int end, string message)
    {
        Line = line;
        Start = start;
        End = end;
        Message = message;
    }

    public int Line { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public string Message { get; set; }
}

