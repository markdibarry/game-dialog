using System;
using System.Collections.Generic;
using System.Text;

namespace GameDialog.Runner;

public static class TextParser
{
    private static readonly StringBuilder s_sb = new();

    /// <summary>
    /// Takes text with BBCode removed and extracts events along with their character positions.
    /// </summary>
    /// <param name="fullText">The unparsed text.</param>
    /// <param name="parsedText">The text with BbCode removed. Can be retrieved via RichTextLabel.GetParsedText()</param>
    /// <param name="events">A list of text events to populate</param>
    /// <param name="handler">The text event parsing handler.</param>
    /// <returns>The text with BBCode preserved, but text events removed.</returns>
    public static string GetEventParsedText(
        string fullText,
        string parsedText,
        List<TextEvent>? events,
        ITextEventHandler? handler)
    {
        string parsedString = fullText;
        int appendStart = 0;
        int ri = 0; // rendered index
        int pi = 0; // bbcode parsed index
        int i = 0; // full text index

        while (i < fullText.Length)
        {
            // Is not in brackets or is escaped character
            if (fullText[i] != '[' || (i != 0 && fullText[i - 1] == '\\'))
            {
                i++;
                ri++;
                pi++;
                continue;
            }

            int bracketLength = GetBracketLength(fullText, i);
            int bracketClose = i + bracketLength - 1;

            // If doesn't close, ignore
            if (fullText[bracketClose] != ']')
            {
                i += bracketLength;
                ri += bracketLength;
                pi += bracketLength;
                continue;
            }

            // is bbCode, so only increase Text index
            if (pi >= parsedText.Length || parsedText[pi] != '[')
            {
                i += bracketLength;
                continue;
            }

            ReadOnlySpan<char> tagContent = fullText.AsSpan((i + 1)..bracketClose);
            s_sb.Append(fullText.AsSpan(appendStart..i));
            int prevSbLength = s_sb.Length;
            TextEvent textEvent;

            if (handler is not null)
                textEvent = handler.ParseTextEvent(tagContent, ri, s_sb);
            else
                textEvent = ParseTextEvent(tagContent, ri);

            ri += s_sb.Length - prevSbLength;

            if (textEvent.EventType != EventType.Undefined)
            {
                if (textEvent.EventType != EventType.Ignore)
                    events?.Add(textEvent);
            }
            else
            {
                s_sb.Append(fullText.AsSpan(i..(i + bracketLength)));
                ri += bracketLength;
            }

            i += bracketLength;
            pi += bracketLength;
            appendStart = i;
        }

        if (appendStart > 0)
        {
            s_sb.Append(fullText.AsSpan(appendStart..));
            parsedString = s_sb.ToString();
        }

        s_sb.Clear();

        return parsedString;
    }

    private static TextEvent ParseTextEvent(ReadOnlySpan<char> tagContent, int renderedIndex)
    {
        // currently only supports 'pause' and 'speed'
        if (tagContent.Contains(' '))
            return TextEvent.Undefined;

        bool isClosing = false;

        if (tagContent.StartsWith('/'))
        {
            isClosing = true;
            tagContent = tagContent[1..];
        }

        int equalsIndex = tagContent.IndexOf('=');

        ReadOnlySpan<char> tagKey = equalsIndex == -1 ? tagContent : tagContent[..equalsIndex];
        ReadOnlySpan<char> tagValue = equalsIndex == -1 ? string.Empty : tagContent[(equalsIndex + 1)..];

        TextEvent result = TextEvent.Undefined;

        if (tagKey.SequenceEqual("speed"))
            result = TryAddSpeedEvent(tagValue, renderedIndex);
        else if (tagKey.SequenceEqual("pause"))
            result = TryAddPauseEvent(tagValue, renderedIndex);

        return result;

        TextEvent TryAddSpeedEvent(ReadOnlySpan<char> value, int renderedIndex)
        {
            if (value.IsEmpty && isClosing)
            {
                return new(EventType.Speed, renderedIndex, 1);
            }
            else if (!value.IsEmpty && !isClosing)
            {
                if (!double.TryParse(value, out double parsedValue))
                    return TextEvent.Undefined;

                return new(EventType.Speed, renderedIndex, parsedValue);
            }

            return TextEvent.Undefined;
        }

        TextEvent TryAddPauseEvent(ReadOnlySpan<char> value, int renderedIndex)
        {
            if (isClosing || value.IsEmpty)
                return TextEvent.Undefined;

            if (!double.TryParse(value, out double parsedValue))
                return TextEvent.Undefined;

            return new(EventType.Pause, renderedIndex, parsedValue);
        }
    }

    private static int GetBracketLength(string text, int i)
    {
        int length = 1;
        i++;

        while (i < text.Length)
        {
            if (text[i - 1] != '\\')
            {
                if (text[i] == ']')
                    return ++length;
                else if (text[i] == '[')
                    return length;
            }

            length++;
            i++;
        }

        return length;
    }
}
