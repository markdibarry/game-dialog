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
    public static void AdjustEventIndices(ReadOnlySpan<char> oldText, ReadOnlySpan<char> parsedText, List<TextEvent> events)
    {
        if (events == null || events.Count == 0)
            return;

        int oLen = oldText.Length;
        int pLen = parsedText.Length;
        int oPos = 0;
        int pPos = 0;
        int eventIdx = 0;
        int offset = 0;

        while (oPos < oLen)
        {
            if (!TryAdjustEvent(events, ref eventIdx, oPos, offset))
                return;

            if (oldText[oPos] != '[')
            {
                oPos++;
                pPos++;
                continue;
            }

            int tagStart = oPos;
            int tagEnd = GetBracketEnd(oldText, oLen, tagStart);

            // Doesn't close
            if (tagEnd >= oLen)
            {
                oPos++;
                pPos++;
                continue;
            }

            int tagLength = tagEnd + 1 - tagStart;
            ReadOnlySpan<char> oldTag = oldText[tagStart..(tagEnd + 1)];

            // Same text, skip
            if (pPos + tagLength < pLen && oldTag.SequenceEqual(parsedText[pPos..(pPos + tagLength)]))
            {
                oPos += tagLength;
                pPos += tagLength;
                continue;
            }

            int tagNameStart = tagStart + 1;

            if (oldText[tagNameStart] == '/')
            {
                offset += tagLength;
                oPos += tagLength;
                pPos++;
                continue;
            }

            int tagNameEnd = DialogHelpers.GetNextNonIdentifier(oldText, tagNameStart);
            ReadOnlySpan<char> tagName = oldText[tagNameStart..tagNameEnd];

            if (tagName.SequenceEqual("img"))
            {
                int closeTagStart = tagEnd + 1;

                while (closeTagStart < oLen)
                {
                    if (oldText[closeTagStart] != '[')
                    {
                        closeTagStart++;
                        continue;
                    }

                    if (oldText[closeTagStart..].StartsWith("[/img]"))
                    {
                        int tagsLength = closeTagStart + "[/img]".Length - tagStart;
                        oPos += tagsLength;
                        int imgOffset = IsImgTagValid(oldText[oPos..], parsedText[pPos..]) ? 1 : 0;
                        offset += tagsLength - imgOffset;
                        pPos += 1 + imgOffset;
                        break;
                    }
                }

                continue;
            }

            offset += tagLength;
            oPos += tagLength;

            if (BBCode.IsReplaceTag(tagName))
            {
                offset--;
                pPos++;
            }
        }

        TryAdjustEvent(events, ref eventIdx, oPos, offset);

        static bool TryAdjustEvent(List<TextEvent> events, ref int eventIdx, int oPos, int offset)
        {
            TextEvent textEvent = events[eventIdx];

            while (textEvent.TextIndex == oPos)
            {
                ReadOnlySpan<char> span = textEvent.Tag.Span;
                int pOffset = span.StartsWith(BuiltIn.PROMPT) ? 1 : 0;
                events[eventIdx] = textEvent with { TextIndex = oPos - offset - pOffset };
                eventIdx++;

                if (eventIdx >= events.Count)
                    return false;

                textEvent = events[eventIdx];
            }

            return true;
        }

        static int GetBracketEnd(ReadOnlySpan<char> oldText, int oLen, int start)
        {
            int end = start;
            bool inQuote = false;

            while (end < oLen)
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

            return end;
        }

        // img tag only replaced with a space when valid
        static bool IsImgTagValid(ReadOnlySpan<char> oldText, ReadOnlySpan<char> parsedText)
        {
            int oldLastSpace = 0;
            int pLastSpace = 0;

            while (oldLastSpace < oldText.Length && oldText[oldLastSpace] == ' ')
                oldLastSpace++;

            while (pLastSpace < parsedText.Length && parsedText[pLastSpace] == ' ')
                pLastSpace++;

            return pLastSpace > oldLastSpace;
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
