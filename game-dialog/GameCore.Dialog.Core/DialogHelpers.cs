using System;
using System.Collections.Generic;
using System.Text;

namespace GameCore.Dialog;

internal static class DialogHelpers
{
    internal static int GetNextNonWhitespace(ReadOnlySpan<char> span, int startIdx)
    {
        int i = startIdx;

        while (i < span.Length && char.IsWhiteSpace(span[i]))
            i++;

        return i;
    }

    internal static int GetNextNonIdentifier(ReadOnlySpan<char> span, int startIdx)
    {
        int i = startIdx;

        while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
            i++;

        return i;
    }

    internal static ReadOnlySpan<char> StripLineComment(this ReadOnlySpan<char> line)
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

    internal static ReadOnlyMemory<char> StripLineComment(this ReadOnlyMemory<char> line)
    {
        ReadOnlySpan<char> span = StripLineComment(line.Span);
        return line[..span.Length];
    }

    internal static bool ContainsSequence(this List<ReadOnlyMemory<char>> col, ReadOnlySpan<char> seq)
    {
        foreach (var item in col)
        {
            ReadOnlySpan<char> span = item.Span;

            if (span.Length != seq.Length)
                continue;

            if (span.SequenceEqual(seq))
                return true;
        }

        return false;
    }

    internal static int GetNextNonEmptyLine(this List<ReadOnlyMemory<char>> script, int lineIdx)
    {
        while (lineIdx < script.Count)
        {
            int i = 0;
            ReadOnlySpan<char> span = script[lineIdx].Span;

            while (i < span.Length && char.IsWhiteSpace(span[i]))
                i++;

            if (i < span.Length && !(span[i] == '/' && i + 1 < span.Length && span[i + 1] == '/'))
                break;

            lineIdx++;
        }

        return lineIdx;
    }

    internal static void AddError(this List<Error> errors, ExprInfo exprInfo, string message)
    {
        errors.AddError(exprInfo, 0, message);
    }

    internal static void AddError(this List<Error> errors, ExprInfo exprInfo, int pos, string message)
    {
        int start = exprInfo.OffsetStart + pos;
        int end = exprInfo.OffsetStart + exprInfo.Span.Length;
        errors.Add(new(exprInfo.LineIdx, start, end, message));
    }

    internal static void AddError(this List<Error> errors, int lineIdx, int charStart, int charEnd, string message)
    {
        errors.Add(new(lineIdx, charStart, charEnd, message));
    }

    internal static void AppendChart(this StringBuilder sb, ParserState ps, string name)
    {
        if (sb.Length > 0)
            sb.AppendLine();

        for (int i = 0; i < ps.CurrentIndentCount; i++)
            sb.Append("--");

        sb.Append(name);
    }

    internal static string GetSnippet(this ReadOnlySpan<char> span, int start)
    {
        if (start + 10 > span.Length)
            return span[start..].ToString();
        else
            return $"{span[start..(start + 10)]}...";
    }
}