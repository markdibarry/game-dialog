using System;
using System.Collections.Generic;
using System.Text;
using GameDialog.Common;

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
    public static string GetEventParsedText(
        string fullText,
        string parsedText,
        List<TextEvent>? events,
        DialogBase? dialog)
    {
        string parsedString = fullText;
        int appendStart = 0;
        int ri = 0; // rendered index
        int pi = 0; // bbcode parsed index
        int i = 0; // full text index

        while (i < fullText.Length)
        {
            // handle escaped brackets
            if (fullText[i] == '\\'
                && i != fullText.Length - 1
                && (fullText[i + 1] == '[' || fullText[i + 1] == ']'))
            {
                if (i != 0)
                    s_sb.Append(fullText.AsSpan(appendStart..i));

                s_sb.Append(fullText[i + 1]);
                i += 2;
                ri++;
                pi += 2;
                appendStart = i;
                continue;
            }

            // Is not in brackets or is escaped character
            if (fullText[i] != '[')
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
            if (pi + bracketLength > parsedText.Length
                || !parsedText.AsSpan(pi, bracketLength).SequenceEqual(fullText.AsSpan(i, bracketLength)))
            {
                i += bracketLength;
                continue;
            }

            ReadOnlySpan<char> tagContent = fullText.AsSpan((i + 1)..bracketClose);
            s_sb.Append(fullText.AsSpan(appendStart..i));
            int prevSbLength = s_sb.Length;
            TextEvent textEvent;

            if (dialog is not null)
                textEvent = dialog.ParseTextEvent(tagContent, ri, s_sb);
            else
                textEvent = ParseTextEventString(tagContent, ri);

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

        static int GetBracketLength(string text, int i)
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

    public TextEvent ParseTextEvent(ReadOnlySpan<char> tagContent, int renderedIndex, StringBuilder sb)
    {
        if (!int.TryParse(tagContent, out int instIndex))
            return ParseTextEventString(tagContent, renderedIndex);

        ushort[] instr = Instructions[instIndex];
        ushort instructionType = instr[0];
        // ushort unusedNextIndex = instr[1];
        ushort opCode = instr[2];
        ushort floatIndex = instr[3];

        if (instructionType == InstructionType.Hash)
        {
            return new(EventType.Hash, renderedIndex, instIndex);
        }
        else if (instructionType == InstructionType.Speaker)
        {
            return new(EventType.Speaker, renderedIndex, instIndex);
        }
        else if (instructionType == InstructionType.Instruction)
        {
            return opCode switch
            {
                OpCode.Assign or
                OpCode.MultAssign or
                OpCode.DivAssign or
                OpCode.AddAssign or
                OpCode.SubAssign => AddAsTextEvent(),
                OpCode.String or
                OpCode.Float or
                OpCode.Mult or
                OpCode.Div or
                OpCode.Add or
                OpCode.Sub or
                OpCode.Var or
                OpCode.Func => AppendResult(),
                OpCode.Speed => AddSpeedEvent(),
                OpCode.Pause => AddPauseEvent(),
                OpCode.Auto => HandleAuto(),
                OpCode.Prompt => HandlePrompt(),
                OpCode.Page => HandlePage(),
                _ => TextEvent.Undefined
            };
        }

        return TextEvent.Undefined;

        TextEvent AddAsTextEvent()
        {
            return new(EventType.Instruction, renderedIndex, instIndex);
        }

        TextEvent AppendResult()
        {
            string result = string.Empty;
            VarType returnType = GetReturnType(instr);

            switch (returnType)
            {
                case VarType.String:
                    result = GetStringInstResult(instr);
                    break;
                case VarType.Float:
                    result = GetFloatInstResult(instr).ToString();
                    break;
                case VarType.Void:
                    bool isAwait = instr[4] == 1;
                    return new(EventType.Instruction, renderedIndex, instIndex, isAwait);
            }

            sb.Append(result.AsSpan());
            return TextEvent.Ignore;
        }

        TextEvent AddSpeedEvent()
        {
            float speed = Floats[floatIndex];
            return new TextEvent(EventType.Speed, renderedIndex, speed);
        }

        TextEvent AddPauseEvent()
        {
            float time = Floats[floatIndex];
            return new TextEvent(EventType.Pause, renderedIndex, time);
        }

        TextEvent HandleAuto()
        {
            float time = Floats[floatIndex];
            return new TextEvent(EventType.Auto, renderedIndex, time);
        }

        TextEvent HandlePrompt()
        {
            return new TextEvent(EventType.Prompt, renderedIndex - 1, 0);
        }

        TextEvent HandlePage()
        {
            return new TextEvent(EventType.Page, renderedIndex - 1, 0);
        }
    }

    public static TextEvent ParseTextEventString(ReadOnlySpan<char> tagContent, int renderedIndex)
    {
        bool isClosing = false;

        if (tagContent.StartsWith('/'))
        {
            isClosing = true;
            tagContent = tagContent[1..];
        }

        int equalsIndex = tagContent.IndexOf('=');
        ReadOnlySpan<char> tagKey = equalsIndex == -1 ? tagContent : tagContent[..equalsIndex].Trim();
        ReadOnlySpan<char> tagValue = equalsIndex == -1 ? string.Empty : tagContent[(equalsIndex + 1)..].Trim();
        TextEvent result = TextEvent.Undefined;

        if (tagKey.SequenceEqual(BuiltIn.SPEED))
            result = TryAddSpeedEvent(tagValue, renderedIndex, isClosing);
        else if (tagKey.SequenceEqual(BuiltIn.PAUSE))
            result = TryAddPauseEvent(tagValue, renderedIndex, isClosing);
        else if (tagKey.SequenceEqual(BuiltIn.PROMPT))
            return new TextEvent(EventType.Prompt, renderedIndex - 1, 0);
        else if (tagKey.SequenceEqual(BuiltIn.PAGE))
            return new TextEvent(EventType.Page, renderedIndex - 1, 0);

        return result;

        static TextEvent TryAddSpeedEvent(ReadOnlySpan<char> value, int renderedIndex, bool isClosing)
        {
            double mult = 1;

            if (!isClosing && !double.TryParse(value, out mult))
                return TextEvent.Undefined;

            return new(EventType.Speed, renderedIndex, mult);
        }

        static TextEvent TryAddPauseEvent(ReadOnlySpan<char> value, int renderedIndex, bool isClosing)
        {
            if (isClosing || value.IsEmpty)
                return TextEvent.Undefined;

            if (!double.TryParse(value, out double time))
                return TextEvent.Undefined;

            return new(EventType.Pause, renderedIndex, time);
        }
    }

    public void HandleTextEvent(TextEvent textEvent)
    {
        int value = (int)textEvent.Value;

        switch (textEvent.EventType)
        {
            case EventType.Instruction:
                EvaluateInstructions(Instructions[value]);
                break;
            case EventType.Hash:
                ushort[] hashInstr = Instructions[value];
                StateSpan<ushort> hashSpan = new(hashInstr, 2);
                GetHashResult(hashSpan, _cacheDict);
                OnHash(_cacheDict);
                _cacheDict.Clear();
                break;
            case EventType.Speaker:
                ushort[] speakerInstr = Instructions[value];
                ushort speakerId = speakerInstr[2];
                StateSpan<ushort> speakerSpan = new(speakerInstr, 3);
                GetHashResult(speakerSpan, _cacheDict);
                OnSpeakerHash(SpeakerIds[speakerId], _cacheDict);
                _cacheDict.Clear();
                break;
        }
    }
}
