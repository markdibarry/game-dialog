using System;
using System.Collections.Generic;
using System.Text;

namespace GameDialog.Runner;

public partial class DialogBase
{
    private static readonly StringBuilder s_sb = new();

    /// <summary>
    /// Takes text with BBCode removed and extracts events along with their character positions
    /// </summary>
    /// <param name="fullText"></param>
    /// <param name="parsedText"></param>
    /// <param name="events"></param>
    /// <returns></returns>
    public string GetEventParsedText(string fullText, string parsedText, List<TextEvent> events)
    {
        ReadOnlySpan<char> fullSpan = fullText;
        ReadOnlySpan<char> parsedSpan = parsedText;
        int appendStart = 0;
        int rStart = 0;
        int pStart = 0; // bbcode parsed index
        int i = 0; // full text index

        while (i < fullSpan.Length)
        {
            char c = fullSpan[i];

            // handle escaped brackets
            if (c == '\\'
                && i != fullSpan.Length - 1
                && (fullSpan[i + 1] == '[' || fullSpan[i + 1] == ']'))
            {
                if (i != 0)
                    s_sb.Append(fullSpan[appendStart..i]);

                s_sb.Append(fullSpan[i + 1]);
                i += 2;
                appendStart = i;
                continue;
            }

            // Is not in brackets or is escaped character
            if (c != '[')
            {
                i++;
                continue;
            }

            int tagLength = GetTagLength(fullSpan, i);
            int tagClose = i + tagLength - 1;

            // If doesn't close or is BBCode, ignore
            if (fullSpan[tagClose] != ']'
                || !int.TryParse(fullSpan[(i + 1)..tagClose], out int eventIndex)
                || eventIndex >= events.Count)
            {
                i += tagLength;
                continue;
            }

            rStart += GetRenderIncrease(parsedSpan, fullSpan.Slice(i, tagLength), ref pStart);
            s_sb.Append(fullSpan[appendStart..i]);
            TextEvent textEvent = events[eventIndex];
            int textIndex = textEvent.EventType switch
            {
                EventType.Scroll or EventType.Prompt => rStart - 1,
                _ => rStart,
            };
            events[eventIndex] = textEvent with { TextIndex = textIndex };

            if (textEvent.EventType == EventType.Append)
            {
                TextVariant result = HandleExpression((int)textEvent.Param1, (int)textEvent.Param2);
                string resultString = string.Empty;

                if (result.VariantType == VarType.String)
                    resultString = result.String;
                else if (result.VariantType == VarType.Float)
                    resultString = result.Float.ToString();

                s_sb.Append(resultString);
                rStart += resultString.Length;
            }

            i += tagLength;
            pStart += tagLength;
            appendStart = i;
        }

        if (appendStart > 0)
        {
            s_sb.Append(fullSpan[appendStart..]);
            string result = s_sb.ToString();
            s_sb.Clear();
            return result;
        }

        return fullText;

        static int GetTagLength(ReadOnlySpan<char> text, int i)
        {
            int length = 1;
            i++;

            while (i < text.Length)
            {
                char c = text[i];

                if (c == '\\' && i < text.Length - 1)
                {
                    length += 2;
                    i += 2;
                    continue;
                }

                if (c == ']')
                    return ++length;
                else if (c == '[')
                    return length;

                length++;
                i++;
            }

            return length;
        }

        static int GetRenderIncrease(ReadOnlySpan<char> span, ReadOnlySpan<char> match, ref int startIndex)
        {
            int matchLen = match.Length;

            if (matchLen > span.Length - startIndex)
                return -1;

            int limit = span.Length - matchLen;
            int ri = 0;

            while (startIndex <= limit)
            {
                if (span[startIndex] == '\\'
                    && startIndex != span.Length - 1
                    && (span[startIndex + 1] == '[' || span[startIndex + 1] == ']'))
                {
                    startIndex += 2;
                    ri++;
                    continue;
                }

                if (span[startIndex] == match[0] && span.Slice(startIndex, matchLen).SequenceEqual(match))
                    return ri;

                ri++;
                startIndex++;
            }

            return -1;
        }
    }

    public void HandleTextEvent(TextEvent textEvent)
    {
        switch (textEvent.EventType)
        {
            case EventType.Evaluate:
            case EventType.Await:
                HandleExpression((int)textEvent.Param1, (int)textEvent.Param2);
                break;
            // case EventType.Hash:
            //     ushort[] hashInstr = Instructions[value];
            //     StateSpan<ushort> hashSpan = new(hashInstr, 2);
            //     GetHashResult(hashSpan, _cacheDict);
            //     OnHash(_cacheDict);
            //     _cacheDict.Clear();
            //     break;
            // case EventType.Speaker:
            //     ushort[] speakerInstr = Instructions[value];
            //     ushort speakerId = speakerInstr[2];
            //     StateSpan<ushort> speakerSpan = new(speakerInstr, 3);
            //     GetHashResult(speakerSpan, _cacheDict);
            //     OnSpeakerHash(SpeakerIds[speakerId], _cacheDict);
            //     _cacheDict.Clear();
            //     break;
        }
    }
}
