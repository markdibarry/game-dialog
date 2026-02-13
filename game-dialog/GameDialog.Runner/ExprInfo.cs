using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Runner;

internal readonly ref struct ExprInfo
{
    public ExprInfo(ReadOnlyMemory<char> content, int lineIdx, int offsetStart)
    {
        Memory = content;
        LineIdx = lineIdx;
        OffsetStart = offsetStart;
    }

    public readonly ReadOnlyMemory<char> Memory;
    public readonly ReadOnlySpan<char> Span => Memory.Span;
    public readonly int LineIdx;
    public readonly int OffsetStart;
    public readonly int OffsetEnd => OffsetStart + Memory.Length;
    public readonly ExprType ExprType => GetExprType();

    private ExprType GetExprType()
    {
        if (Span.Length == 0)
            return ExprType.None;

        int start = 0;

        if (Span[start] == '#')
            return ExprType.Hash;

        if (Span[start] == '/')
            start++;

        int end = DialogHelpers.GetNextNonIdentifier(Span, start);
        ReadOnlySpan<char> firstToken = Span[start..end];

        ExprType exprType;

        if (BBCode.IsSupportedTag(firstToken))
            exprType = ExprType.BBCode;
        else if (BuiltIn.IsSupportedTag(firstToken))
            exprType = ExprType.BuiltIn;
        else if (Span.StartsWith("await "))
            exprType = ExprType.Await;
        else
            exprType = ExprType.Evaluation;

        return exprType;
    }

    public static bool TryGetExprInfo(
        ReadOnlyMemory<char> line,
        int offsetStart,
        [NotNullWhen(true)] out ExprInfo exprInfo)
    {
        return TryGetExprInfo(line, 0, offsetStart, null, out exprInfo);
    }

    public static bool TryGetExprInfo(
        ReadOnlyMemory<char> line,
        int lineIdx,
        int offsetStart,
        List<Error>? errors,
        [NotNullWhen(true)] out ExprInfo exprInfo)
    {
        int exprStart = offsetStart;
        var span = line.Span;

        if (span[exprStart] == '[')
            exprStart++;

        exprStart = DialogHelpers.GetNextNonWhitespace(span, exprStart);
        int exprEnd = exprStart;
        bool inQuote = false;

        while (exprEnd < span.Length)
        {
            if (span[exprEnd] == ']' && !inQuote)
                break;

            if (span[exprEnd] == '"')
            {
                if (!inQuote)
                    inQuote = true;
                else if (span[exprEnd - 1] != '\\')
                    inQuote = false;
            }

            exprEnd++;
        }

        if (exprEnd >= span.Length)
        {
            errors?.AddError(lineIdx, exprStart, span.Length, "Unterminated expression block. Missing closing ']'");
            exprInfo = default;
            return false;
        }

        exprInfo = new(line[exprStart..exprEnd], lineIdx, exprStart);
        return true;
    }
}
