using System;
using System.Collections.Generic;
using System.Text;

namespace GameDialog.Runner;

public static class DialogHelpers
{
    public static int GetNextNonWhitespace(ReadOnlySpan<char> span, int startIdx)
    {
        int i = startIdx;

        while (i < span.Length && char.IsWhiteSpace(span[i]))
            i++;

        return i;
    }

    public static int GetNextNonIdentifier(ReadOnlySpan<char> span, int startIdx)
    {
        int i = startIdx;

        while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
            i++;

        return i;
    }

    public static ReadOnlySpan<char> StripLineComment(this ReadOnlySpan<char> line)
    {
        for (int i = 0; i < line.Length - 1; i++)
        {
            // matches "//" not preceded by a backslash
            if (line[i] == '/'
                && i + 1 < line.Length && line[i + 1] == '/'
                && (i == 0 || line[i - 1] != '\\'))
            {
                return line[..i];
            }
        }

        return line;
    }

    public static int GetNextNonEmptyLine(this string[] script, int lineIdx)
    {
        while (lineIdx < script.Length)
        {
            int i = 0;
            ReadOnlySpan<char> span = script[lineIdx].AsSpan();

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i < span.Length && !(span[i] == '/' && i + 1 < span.Length && span[i + 1] == '/'))
                break;

            lineIdx++;
        }

        return lineIdx;
    }

    public static void AddError(this List<Error> errors, int lineIdx, int charStart, int charEnd, string message)
    {
        errors.Add(new(lineIdx, charStart, charEnd, message));
    }

    public static void AppendChart(this StringBuilder sb, ParserState ps, string name)
    {
        if (sb.Length > 0)
            sb.AppendLine();

        for (int i = 0; i < ps.CurrentIndentCount; i++)
            sb.Append("--");

        sb.Append(name);
    }

    public static string GetSnippet(this ReadOnlySpan<char> span, int start)
    {
        if (start + 10 > span.Length)
            return span[start..].ToString();
        else
            return $"{span[start..(start + 10)]}...";
    }
}