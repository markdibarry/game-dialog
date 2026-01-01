using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameDialog.Pooling;

namespace GameDialog.Runner;

public partial class DialogBase
{
    private const int EndScript = -2;
    private const int SuspendScript = -1;
    private readonly Dictionary<string, string> _cacheDict = [];
    private readonly StringBuilder _sb = new();

    /// <summary>
    /// Updates Errors with issues.
    /// </summary>
    /// <param name="errors">The error list to populate</param>
    public void ValidateScript(List<Error> errors, StringBuilder? chart = null)
    {
        _validator ??= new(_state, [], []);
        _validator.ValidateScript(errors, chart);
    }

    /// <summary>
    /// Begins a loaded dialog script.
    /// </summary>
    /// <param name="sectionId">Optional starting section id</param>
    public void StartScript(string sectionId = "")
    {
        Next = sectionId.Length > 0 ? GetSectionIndex(sectionId) : 0;
        Resume();
    }

    private int GetSectionIndex(ReadOnlySpan<char> sectionId)
    {
        for (int i = 0; i < Script.Count; i++)
        {
            ReadOnlySpan<char> current = Script[i].Span.StripLineComment();

            if (!TitleRegex().IsMatch(current))
                continue;

            int j = DialogHelpers.GetNextNonWhitespace(current, 0);
            j += 2; // Skip the first two dashes
            int start = j;

            while (j < current.Length && current[j] != '-')
                j++;

            if (current[start..j].SequenceEqual(sectionId))
                return i;
        }

        return -1;
    }

    public void Resume()
    {
        // Next has not been set, so a line must be in progress.
        if (Next == null)
        {
            OnDialogLineResumed();
            return;
        }

        int? nextIndex = Next;
        Next = null;

        while (nextIndex >= 0 && nextIndex < Script.Count)
            nextIndex = ReadStatement(nextIndex.Value);

        if (nextIndex == SuspendScript)
            return;

        ScriptEnded?.Invoke(this);
    }

    public void ReadNext(int? nextIndex = null)
    {
        if (!nextIndex.HasValue)
            nextIndex = Next ?? EndScript;

        Next = null;

        while (nextIndex >= 0 && nextIndex < Script.Count)
            nextIndex = ReadStatement(nextIndex.Value);

        if (nextIndex == SuspendScript)
            return;

        ScriptEnded?.Invoke(this);
    }

    private int ReadStatement(int lineIdx)
    {
        _state.MoveLine(lineIdx);
        ReadOnlySpan<char> line = Script[LineIdx].Span;
        int start = DialogHelpers.GetNextNonWhitespace(line, 0);
        ReadOnlySpan<char> trimmed = line[start..];

        if (TitleRegex().IsMatch(trimmed))
            return LineIdx + 1;
        else if (trimmed.StartsWith('['))
            return HandleExpressionStatement();
        else if (trimmed.StartsWith('?'))
            return HandleChoiceStatement();
        else if (IfRegex().IsMatch(trimmed))
            return HandleConditionalStatement();
        else if (ElseIfRegex().IsMatch(trimmed) || trimmed.StartsWith("else"))
            return GetLineSkipping(x => ElseIfRegex().IsMatch(x) || x.StartsWith("else"));
        else if (SpeakerRegex().IsMatch(trimmed))
            return HandleLineStatement();
        else
            return EndScript;

        // int HandleSpeakerStatement()
        // {
        //     int next = instr[1];
        //     int speakerId = instr[2];
        //     StateSpan<ushort> span = new(instr, 3);
        //     GetHashResult(span, _cacheDict);
        //     OnSpeakerHash(SpeakerIds[speakerId], _cacheDict);
        //     _cacheDict.Clear();
        //     return next;
        // }
    }

    private int HandleConditionalStatement()
    {
        ReadOnlySpan<char> line = _state.Line;
        int baseIndent = _state.CurrentIndentLevel;
        int charIdx = baseIndent;
        charIdx += "if".Length;
        charIdx = DialogHelpers.GetNextNonWhitespace(line, charIdx) + 1;
        ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, charIdx, null, out ExprInfo exprInfo);
        TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);
        _state.MoveNextLine();

        if (result.VariantType == VarType.Bool && result.Bool)
            return LineIdx;

        // Skip if branch
        while (LineIdx < Script.Count)
        {
            if (_state.CurrentIndentLevel < baseIndent)
                return LineIdx;
            else if (_state.CurrentIndentLevel == baseIndent)
                break;

            _state.MoveNextLine();
        }

        while (LineIdx < Script.Count && ElseIfRegex().IsMatch(Script[LineIdx].Span))
        {
            line = _state.Line;
            charIdx = DialogHelpers.GetNextNonWhitespace(line, 0);
            charIdx += "else if".Length;
            charIdx = DialogHelpers.GetNextNonWhitespace(line, charIdx) + 1;
            ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, charIdx, null, out exprInfo);
            result = ExprParser.Parse(exprInfo, DialogStorage);
            _state.MoveNextLine();

            if (result.VariantType == VarType.Bool && result.Bool)
                return LineIdx;

            // skip else if branch
            while (LineIdx < Script.Count)
            {
                if (_state.CurrentIndentLevel < baseIndent)
                    return LineIdx;
                else if (_state.CurrentIndentLevel == baseIndent)
                    break;

                _state.MoveNextLine();
            }
        }

        if (LineIdx >= Script.Count)
            return EndScript;

        line = _state.Line;
        charIdx = DialogHelpers.GetNextNonWhitespace(line, 0);

        if (line[charIdx..].StartsWith("else"))
            _state.MoveNextLine();

        return LineIdx;
    }

    private int HandleChoiceStatement()
    {
        if (IsPartOfEarlierChoiceSet())
            return GetLineSkipping(x => x.StartsWith('?'));

        List<Choice> choices = GetChoices();
        OnChoice(choices);
        ListPool.Return(choices);
        return SuspendScript;

        bool IsPartOfEarlierChoiceSet()
        {
            int currentIndent = DialogHelpers.GetNextNonWhitespace(Script[LineIdx].Span, 0);
            int lineIdx = LineIdx - 1;

            while (lineIdx >= 0)
            {
                ReadOnlySpan<char> prevLine = Script[lineIdx].Span;
                int prevIndent = DialogHelpers.GetNextNonWhitespace(prevLine, 0);

                if (prevIndent >= prevLine.Length || prevLine[prevIndent..].StartsWith("//"))
                {
                    lineIdx--;
                    continue;
                }

                if (prevIndent < currentIndent)
                    return false;

                if (prevIndent == currentIndent)
                    return prevLine[prevIndent..].StartsWith('?');

                lineIdx--;
            }

            return false;
        }

        List<Choice> GetChoices()
        {
            List<Choice> choices = ListPool.Get<Choice>();
            int currentIndent = DialogHelpers.GetNextNonWhitespace(Script[LineIdx].Span, 0);
            int lineIdx = LineIdx;

            while (lineIdx < Script.Count)
            {
                ReadOnlySpan<char> line = Script[lineIdx].Span;
                int indent = DialogHelpers.GetNextNonWhitespace(line, 0);

                if (indent > currentIndent || line[indent..].StartsWith("//"))
                {
                    lineIdx++;
                    continue;
                }

                if (indent < currentIndent || line[indent] != '?')
                    break;

                line = line.StripLineComment();
                int choiceStart = DialogHelpers.GetNextNonWhitespace(line, indent + 1);
                bool disabled = false;

                if (IfRegex().IsMatch(line[choiceStart..]))
                {
                    choiceStart += "if".Length;
                    choiceStart = DialogHelpers.GetNextNonWhitespace(line, choiceStart) + 1;
                    ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, choiceStart, null, out ExprInfo exprInfo);
                    TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

                    if (result.VariantType == VarType.Bool)
                        disabled = !result.Bool;

                    choiceStart = DialogHelpers.GetNextNonWhitespace(line, exprInfo.End + 1);
                }

                lineIdx++;

                choices.Add(new Choice
                {
                    Text = line[choiceStart..].ToString(),
                    Next = GetChoiceBranchLineIdx(lineIdx, currentIndent),
                    Disabled = disabled
                });
            }

            // Resolve choices with no branch
            for (int i = 0; i < choices.Count; i++)
            {
                Choice choice = choices[i];

                if (choice.Next == -1)
                    choices[i] = choice with { Next = lineIdx };
            }

            return choices;
        }

        int GetChoiceBranchLineIdx(int lineIdx, int indent)
        {
            while (lineIdx < Script.Count)
            {
                ReadOnlySpan<char> line = Script[lineIdx].Span;
                int currentIndent = DialogHelpers.GetNextNonWhitespace(line, 0);

                if (line[currentIndent..].StartsWith("//"))
                {
                    lineIdx++;
                    continue;
                }

                if (currentIndent <= indent)
                    return -1;

                return lineIdx;
            }

            return -1;
        }
    }

    /// <summary>
    /// Gets the next line index that does not satisfy the given condition.
    /// </summary>
    /// <param name="condition"></param>
    /// <returns></returns>
    private int GetLineSkipping(Func<ReadOnlySpan<char>, bool> condition)
    {
        int currentIndent = DialogHelpers.GetNextNonWhitespace(Script[LineIdx].Span, 0);
        int lineIdx = LineIdx + 1;

        while (lineIdx < Script.Count)
        {
            ReadOnlySpan<char> line = Script[lineIdx].Span;
            int indent = DialogHelpers.GetNextNonWhitespace(line, 0);

            if (indent >= line.Length || line[indent..].StartsWith("//"))
            {
                lineIdx++;
                continue;
            }

            if (indent < currentIndent)
                return lineIdx;

            if (indent == currentIndent && !condition(line[indent..]))
                return lineIdx;

            lineIdx++;
        }

        return EndScript;
    }

    private TextVariant HandleExpression(int lineIdx, int startChar)
    {
        ReadOnlySpan<char> line = Script[lineIdx].Span.StripLineComment();
        int charIdx = DialogHelpers.GetNextNonWhitespace(line, startChar);

        if (!ExprParser.TryGetExprInfo(line, lineIdx, charIdx, null, out ExprInfo exprInfo))
            return TextVariant.Undefined;

        if (line[charIdx] == '#')
        {
            HandleHashExpression(charIdx, exprInfo.End);
            OnHash(_cacheDict);
            _cacheDict.Clear();
            return new();
        }

        return ExprParser.Parse(exprInfo, DialogStorage);
    }

    private int HandleExpressionStatement()
    {
        ReadOnlySpan<char> line = _state.Line;
        int charIdx = DialogHelpers.GetNextNonWhitespace(line, 0) + 1;

        if (!ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, charIdx, null, out ExprInfo exprInfo))
            return EndScript;

        charIdx = DialogHelpers.GetNextNonWhitespace(line, charIdx);

        if (line[charIdx] == '#')
        {
            HandleHashExpression(charIdx, exprInfo.End);
            OnHash(_cacheDict);
            _cacheDict.Clear();
            return LineIdx + 1;
        }

        bool isClosingTag = false;

        if (line[charIdx] == '/')
        {
            isClosingTag = true;
            charIdx++;
        }

        int start = charIdx;
        int end = DialogHelpers.GetNextNonIdentifier(line, charIdx);
        ReadOnlySpan<char> firstToken = line[start..end];

        if (BBCode.IsSupportedTag(firstToken))
            return EndScript;

        start = end;
        ReadOnlySpan<char> restOfExpr = line[start..exprInfo.End];
        bool isAssignment = restOfExpr.Length >= 2 && restOfExpr[0] == '=' && restOfExpr[1] != '=';

        if (isAssignment)
        {
            start++;
            restOfExpr = restOfExpr[1..];
        }

        if (BuiltIn.IsSupportedTag(firstToken))
            return HandleBuiltInTag(start, firstToken, restOfExpr, isClosingTag, isAssignment);

        if (!isAssignment)
        {
            if (firstToken.SequenceEqual("await"))
            {
                exprInfo = exprInfo with { Start = start };
                ExprParser.Parse(exprInfo, DialogStorage);
                return SuspendScript;
            }
            else
            {
                start = charIdx;
                exprInfo = exprInfo with { Start = start };
                ExprParser.Parse(exprInfo, DialogStorage);
                return LineIdx + 1;
            }
        }

        charIdx = start;
        VarType varType = DialogStorage.GetVariableType(firstToken);

        while (char.IsWhiteSpace(line[charIdx]) || line[charIdx] == '=')
            charIdx++;

        exprInfo = exprInfo with { Start = start };
        TextVariant exprResult = ExprParser.Parse(exprInfo, DialogStorage);

        if (varType == VarType.Undefined || varType == exprResult.VariantType)
            DialogStorage.SetVariable(firstToken, exprResult);

        return LineIdx + 1;
    }

    private void HandleHashExpression(int exprStart, int exprEnd)
    {
        ReadOnlySpan<char> line = _state.Line;
        int end = exprStart + 1;

        while (end < exprEnd)
        {
            int start = end;

            while (end < exprEnd && char.IsLetterOrDigit(line[end]) || line[end] == '_')
                end++;

            if (end == start)
                return;

            ReadOnlySpan<char> key = line[start..end];
            int wsStart = end;
            end = DialogHelpers.GetNextNonWhitespace(line[..exprEnd], end);

            if (end < exprEnd && line[end] != '=' && (line[end] != '#' || end - wsStart == 0))
                return;

            if (line[end] != '=')
            {
                _cacheDict[key.ToString()] = string.Empty;
                end++;
                continue;
            }

            end++;
            int rightStart = end;
            bool inQuote = false;

            while (end < exprEnd)
            {
                if (inQuote)
                {
                    if (line[end] == '"' && line[end - 1] != '\\')
                        inQuote = false;
                }
                else
                {
                    if (line[end] == '#' && line[end - 1] == ' ')
                        break;

                    if (line[end] == '"')
                        inQuote = true;
                }

                end++;
            }

            ExprInfo exprInfo = new(_state.Line, _state.LineIdx, rightStart, end);
            TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

            if (result.VariantType == VarType.Undefined)
                return;

            _cacheDict[key.ToString()] = result.ToString();
            end++;
        }
    }

    private int HandleBuiltInTag(
        int start,
        ReadOnlySpan<char> firstToken,
        ReadOnlySpan<char> restOfExpr,
        bool isClosingTag,
        bool isAssignment)
    {
        int end = start + restOfExpr.Length;
        bool isSingleToken = restOfExpr.IsWhiteSpace();

        if (firstToken.SequenceEqual(BuiltIn.AUTO))
        {
            float value;

            if (isSingleToken)
            {
                value = isClosingTag ? -2 : -1;
            }
            else
            {
                if (isClosingTag || !isAssignment)
                    return LineIdx + 1;

                ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, end);
                TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

                if (result.VariantType != VarType.Float)
                    return LineIdx + 1;

                value = result.Float;
            }

            AutoProceedGlobalEnabled = value != -2;
            AutoProceedGlobalTimeout = value;
            return LineIdx + 1;
        }
        else if (firstToken.SequenceEqual(BuiltIn.END))
        {
            return EndScript;
        }
        else if (firstToken.SequenceEqual(BuiltIn.GOTO))
        {
            if (isSingleToken || isClosingTag || isAssignment)
                return LineIdx + 1;

            return GetSectionIndex(restOfExpr.Trim());
        }
        else if (firstToken.SequenceEqual(BuiltIn.SPEED))
        {
            if (isClosingTag || !isAssignment)
                return LineIdx + 1;

            ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, end);
            TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

            if (result.VariantType != VarType.Float || result.Float < 0)
                return LineIdx + 1;

            SpeedMultiplier = result.Float;
        }
        else if (firstToken.SequenceEqual(BuiltIn.PAUSE))
        {
            if (isClosingTag || !isAssignment)
                return LineIdx + 1;

            ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, end);
            TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

            if (result.VariantType != VarType.Float || result.Float <= 0)
                return LineIdx + 1;

            HandlePauseAsync(result.Float);
            return SuspendScript;
        }

        return LineIdx + 1;

        async void HandlePauseAsync(float time)
        {
            await Task.Delay((int)(time * 1000));
            Resume();
        }
    }

    private int HandleLineStatement()
    {
        List<string> speakerIds = ListPool.Get<string>();
        List<TextEvent> textEvents = ListPool.Get<TextEvent>();
        ReadOnlySpan<char> line = _state.Line;
        int charIdx = 0;
        int speakerStart = 0;

        while (charIdx < line.Length)
        {
            if (line[charIdx] == ',' || line[charIdx] == ':')
            {
                speakerIds.Add(line[speakerStart..charIdx].Trim().ToString());
                charIdx++;
                speakerStart = charIdx;

                if (line[charIdx - 1] == ':')
                    break;
                else
                    continue;
            }

            charIdx++;
        }

        charIdx++; // Skip the first space after the colon
        bool isLastLine = true;

        if (line[charIdx..].StartsWith("^^"))
        {
            charIdx += 2;

            if (line.TrimEnd().EndsWith("^^"))
                line = line.TrimEnd()[..^2];
            else
                isLastLine = false;
        }

        int lineStart = charIdx;

        while (charIdx < line.Length)
        {
            if (line[charIdx] == '[' && line[charIdx - 1] != '\\')
            {
                int exprStart = charIdx + 1;

                if (ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, exprStart, null, out ExprInfo exprInfo))
                {
                    charIdx = exprInfo.End;

                    if (TryGetTextEvent(exprStart, exprInfo.End, out TextEvent textEvent))
                    {
                        textEvents.Add(textEvent);
                        _sb.Append(line[lineStart..(exprStart - 1)]);
                        _sb.Append($"[{textEvents.Count - 1}]");
                        lineStart = charIdx + 1;
                    }
                }
            }

            charIdx++;

            if (charIdx >= line.Length)
            {
                _sb.Append(line[lineStart..charIdx]);

                if (isLastLine)
                    break;

                _state.MoveNextLine();
                line = _state.Line;
                charIdx = DialogHelpers.GetNextNonWhitespace(line, 0);
                lineStart = charIdx;

                if (line.TrimEnd().EndsWith("^^"))
                {
                    line = line.TrimEnd()[..^2];
                    isLastLine = true;
                }
            }
        }

        string text = _sb.ToString();
        _sb.Clear();
        OnDialogLineStarted(text, CollectionsMarshal.AsSpan(speakerIds), CollectionsMarshal.AsSpan(textEvents));
        ListPool.Return(speakerIds);
        ListPool.Return(textEvents);
        return SuspendScript;
    }

    private bool TryGetTextEvent(int exprStart, int exprEnd, out TextEvent textEvent)
    {
        textEvent = TextEvent.Undefined;
        ReadOnlySpan<char> line = _state.Line;
        int start = DialogHelpers.GetNextNonWhitespace(line, exprStart);
        bool isClosingTag = false;

        if (line[start] == '/')
        {
            isClosingTag = true;
            start++;
        }

        int end = start;

        while (end < exprEnd && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
            end++;

        ReadOnlySpan<char> firstToken = line[start..end];

        if (BBCode.IsSupportedTag(firstToken))
            return false;

        start = DialogHelpers.GetNextNonWhitespace(line[..exprEnd], end);
        ReadOnlySpan<char> restOfExpr = line[start..exprEnd];

        if (!BuiltIn.IsSupportedTag(firstToken))
        {
            EventType eventType = line[exprStart..].StartsWith("await ") ? EventType.Await : EventType.Evaluate;

            if (eventType == EventType.Evaluate)
            {
                ExprInfo exprInfo = new(_state.Line, _state.LineIdx, exprStart, exprEnd);
                VarType varType = ExprParser.GetVarType(exprInfo, DialogStorage);

                if (varType == VarType.Float || varType == VarType.String)
                    eventType = EventType.Append;
            }

            textEvent = new()
            {
                EventType = eventType,
                Param1 = LineIdx,
                Param2 = exprStart
            };
            return true;
        }

        bool isSingleToken = restOfExpr.IsWhiteSpace();
        bool isAssignment = restOfExpr.Length >= 2 && restOfExpr[0] == '=' && restOfExpr[1] != '=';

        if (isAssignment)
        {
            int i = DialogHelpers.GetNextNonWhitespace(restOfExpr, 1);
            start += i;
        }

        if (firstToken.SequenceEqual(BuiltIn.AUTO))
        {
            if (isAssignment)
            {
                ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, exprEnd);
                TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

                if (isClosingTag || result.VariantType != VarType.Float || result.Float < 0)
                    return false;

                textEvent = new()
                {
                    EventType = EventType.Auto,
                    Param1 = result.Float
                };
                return true;
            }

            if (!isSingleToken)
                return false;

            textEvent = new()
            {
                EventType = EventType.Auto,
                Param1 = isClosingTag ? -1 : -2
            };
            return true;
        }
        else if (firstToken.SequenceEqual(BuiltIn.END))
        {
            return false;
        }
        else if (firstToken.SequenceEqual(BuiltIn.GOTO))
        {
            return false;
        }
        else if (firstToken.SequenceEqual(BuiltIn.PAUSE))
        {
            if (!isAssignment || isClosingTag)
                return false;

            ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, exprEnd);
            TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

            if (result.VariantType != VarType.Float || result.Float <= 0)
                return false;

            textEvent = new()
            {
                EventType = EventType.Pause,
                Param1 = result.Float
            };
            return true;
        }
        else if (firstToken.SequenceEqual(BuiltIn.SPEED))
        {
            if (isAssignment)
            {
                if (isClosingTag)
                    return false;

                ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, exprEnd);
                TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

                if (result.VariantType != VarType.Float || result.Float <= 0)
                    return false;

                textEvent = new()
                {
                    EventType = EventType.Speed,
                    Param1 = result.Float
                };
                return true;
            }

            if (!isClosingTag || !isSingleToken)
                return false;

            textEvent = new()
            {
                EventType = EventType.Speed,
                Param1 = 1
            };
            return true;
        }
        else if (firstToken.SequenceEqual(BuiltIn.SCROLL))
        {
            textEvent = new() { EventType = EventType.Scroll };
            return true;
        }
        else if (firstToken.SequenceEqual(BuiltIn.PROMPT))
        {
            textEvent = new() { EventType = EventType.Prompt };
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^\s*--\w+--\s*$")]
    private static partial Regex TitleRegex();
    [GeneratedRegex(@"^\s*if\s*\[")]
    private static partial Regex IfRegex();
    [GeneratedRegex(@"^\s*else if\s*\[")]
    private static partial Regex ElseIfRegex();
    [GeneratedRegex(@"^\s*\w+(?:,\s*\w+)*:\s")]
    private static partial Regex SpeakerRegex();
}
