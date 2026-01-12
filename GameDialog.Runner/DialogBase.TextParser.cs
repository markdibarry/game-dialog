using System;
using System.Collections.Generic;

namespace GameDialog.Runner;

public partial class DialogBase
{
    /// <summary>
    /// Takes text with BBCode removed and extracts events along with their character positions
    /// </summary>
    /// <param name="oldText"></param>
    /// <param name="parsedText"></param>
    /// <param name="events"></param>
    /// <returns></returns>
    public static void AdjustIndices(ReadOnlySpan<char> oldText, ReadOnlySpan<char> parsedText, List<TextEvent> events)
    {
        if (events == null || events.Count == 0)
            return;

        int oldLen = oldText.Length;
        int pLen = parsedText.Length;
        int oldPos = 0;
        int pPos = 0;
        int eventIdx = 0;
        int offset = 0;

        while (oldPos < oldLen)
        {
            if (oldText[oldPos] == '[' && (oldPos == 0 || oldText[oldPos - 1] != '\\'))
            {
                int start = oldPos;
                int end = start;
                bool inQuote = false;

                while (end < oldLen)
                {
                    if (oldText[end] == ']' && !inQuote)
                        break;

                    if (oldText[end] == '"')
                    {
                        if (!inQuote)
                            inQuote = true;
                        else if (oldText[end - 1] != '\\')
                            inQuote = false;
                    }

                    end++;
                }

                if (end < oldLen)
                {
                    int tagLength = end + 1 - start;
                    ReadOnlySpan<char> oldTag = oldText[start..(end + 1)];

                    if (pPos + tagLength >= pLen
                        || !oldTag.SequenceEqual(parsedText[pPos..(pPos + tagLength)]))
                    {
                        offset += tagLength;
                        oldPos += tagLength;
                    }
                }
            }

            while (events[eventIdx].TextIndex == oldPos)
            {
                events[eventIdx] = events[eventIdx] with { TextIndex = oldPos - offset };
                eventIdx++;

                if (eventIdx >= events.Count)
                    return;
            }

            oldPos++;
            pPos++;
        }

        while (events[eventIdx].TextIndex == oldPos)
        {
            events[eventIdx] = events[eventIdx] with { TextIndex = oldPos - offset };
            eventIdx++;

            if (eventIdx >= events.Count)
                return;
        }
    }

    public (EventType, float) HandleTextEvent(TextEvent textEvent)
    {
        ExprInfo exprInfo = new(textEvent.Tag, 0, 0);
        ReadOnlySpan<char> expr = exprInfo.Span;

        if (exprInfo.ExprType != ExprType.BuiltIn)
        {
            ReadExpression(exprInfo);
            return default;
        }

        int start = 0;
        bool isClosingTag = false;

        if (expr[start] == '/')
        {
            isClosingTag = true;
            start++;
        }

        int end = DialogHelpers.GetNextNonIdentifier(expr, start);
        ReadOnlySpan<char> tagKey = expr[start..end];
        start = DialogHelpers.GetNextNonWhitespace(expr, end);
        ReadOnlySpan<char> tagValue = expr[start..];
        bool isSingleToken = tagValue.IsWhiteSpace();
        bool isAssignment = tagValue.Length >= 2 && tagValue[0] == '=' && tagValue[1] != '=';

        if (isAssignment)
        {
            int i = DialogHelpers.GetNextNonWhitespace(tagValue, 1);
            start += i;
            tagValue = expr[start..];
        }

        if (tagKey.SequenceEqual(BuiltIn.AUTO))
        {
            if (isAssignment)
            {
                ExprInfo autoExpr = new(exprInfo.Memory[start..], 0, 0);
                TextVariant result = ExprParser.Parse(autoExpr, DialogStorage);

                if (isClosingTag || result.VariantType != VarType.Float || result.Float < 0)
                    return default;

                return (EventType.Auto, result.Float);
            }

            if (!isSingleToken)
                return default;

            return (EventType.Auto, isClosingTag ? -1 : -2);
        }
        else if (tagKey.SequenceEqual(BuiltIn.PAUSE))
        {
            if (!isAssignment || isClosingTag)
                return default;

            ExprInfo pauseExpr = new(exprInfo.Memory[start..], 0, 0);
            TextVariant result = ExprParser.Parse(pauseExpr, DialogStorage);

            if (result.VariantType != VarType.Float || result.Float <= 0)
                return default;

            return (EventType.Pause, result.Float);
        }
        else if (tagKey.SequenceEqual(BuiltIn.SPEED))
        {
            if (isAssignment)
            {
                if (isClosingTag)
                    return default;

                ExprInfo speedExpr = new(exprInfo.Memory[start..], 0, 0);
                TextVariant result = ExprParser.Parse(speedExpr, DialogStorage);

                if (result.VariantType != VarType.Float || result.Float <= 0)
                    return default;

                return (EventType.Speed, result.Float);
            }

            if (!isClosingTag || !isSingleToken)
                return default;

            return (EventType.Speed, 1);
        }
        else if (tagKey.SequenceEqual(BuiltIn.SCROLL))
        {
            return (EventType.Scroll, 0);
        }
        else if (tagKey.SequenceEqual(BuiltIn.PROMPT))
        {
            return (EventType.Prompt, 0);
        }

        return default;
    }
}
