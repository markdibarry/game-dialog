using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

namespace GameDialog.Runner;

public partial class DialogBase
{
    private const int EndScript = -2;
    private const int SuspendScript = -1;
    private readonly Dictionary<string, string> _cacheDict = [];
    private readonly StringBuilder _sb = new();
    private readonly List<Choice> _choices = [];
    private readonly List<string> _speakerIds = [];
    private readonly List<TextEvent> _textEvents = [];

    [GeneratedRegex(@"^\s*--\w+--\s*$")]
    private static partial Regex TitleRegex();
    [GeneratedRegex(@"^\s*if\s*\[")]
    private static partial Regex IfRegex();
    [GeneratedRegex(@"^\s*else if\s*\[")]
    private static partial Regex ElseIfRegex();
    [GeneratedRegex(@"^\s*\w+(?:,\s*\w+)*:\s")]
    private static partial Regex SpeakerRegex();
    private static readonly string SingleLineTitle = "--SingleLine--";

    /// <summary>
    /// Loads a script from a path.
    /// </summary>
    /// <param name="path"></param>
    public void Load(string path)
    {
        string text;

        if (!FileAccess.FileExists(path))
            return;

        // Godot does not allow array pooling, so must use heavier approach
        using (var fs = FileAccess.Open(path, FileAccess.ModeFlags.Read))
            text = fs.GetAsText();

        _state.ReadStringToScript(text, path);
    }

    /// <summary>
    /// Loads a script from a string.
    /// </summary>
    /// <param name="text"></param>
    public void LoadFromText(string text)
    {
        _state.ReadStringToScript(text, string.Empty);
    }

    public void LoadSingleLine(string text)
    {
        _state.Reset();
        _state.Script.Clear();
        _state.Script.Add(SingleLineTitle.AsMemory());
        _state.Script.Add(text.AsMemory());
    }

    /// <summary>
    /// Updates Errors with issues.
    /// </summary>
    /// <param name="errors">The error list to populate</param>
    public void ValidateScript(List<Error> errors, StringBuilder? chart = null)
    {
        _validator ??= new(_state, DialogBridgeBase.InternalVarDefs, DialogBridgeBase.InternalFuncDefs);
        _validator.ValidateScript(errors, chart);
    }

    /// <summary>
    /// Begins a loaded dialog script.
    /// </summary>
    /// <param name="sectionId">Optional starting section id</param>
    public void Start(string sectionId = "")
    {
        int next = sectionId.Length > 0 ? GetSectionIndex(sectionId) : GetFirstSection();
        Resume(next);
    }

    private int GetFirstSection()
    {
        for (int i = 0; i < Script.Count; i++)
        {
            ReadOnlySpan<char> current = Script[i].Span.StripLineComment();

            if (!TitleRegex().IsMatch(current))
                continue;

            return i + 1;
        }

        return EndScript;
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
                return i + 1;
        }

        return EndScript;
    }

    public void Resume() => Resume(LineIdx + 1);

    public void Resume(int nextIndex)
    {
        // Next has not been set, so a line must be in progress.
        if (_inDialogLine)
        {
            OnDialogLineResumed();
            return;
        }

        try
        {
            while (nextIndex >= 0 && nextIndex < Script.Count)
                nextIndex = ReadStatement(nextIndex);

            if (nextIndex == SuspendScript)
                return;
        }
        catch (Exception) { }

        ScriptEnded?.Invoke(this);
    }

    protected void EndDialogLine()
    {
        _inDialogLine = false;
        Resume();
    }

    private int ReadStatement(int lineIdx)
    {
        _state.MoveLine(lineIdx);
        ReadOnlySpan<char> line = Script[LineIdx].Span;
        int start = DialogHelpers.GetNextNonWhitespace(line, 0);
        ReadOnlySpan<char> trimmed = line[start..];

        if (TitleRegex().IsMatch(trimmed))
            return EndScript;
        else if (trimmed.StartsWith('['))
            return ReadExpressionBlock();
        else if (trimmed.StartsWith("? "))
            return ReadChoiceBlock();
        else if (IfRegex().IsMatch(trimmed))
            return ReadConditionBlock();
        else if (ElseIfRegex().IsMatch(trimmed) || trimmed.StartsWith("else"))
            return GetLineSkipping(x => ElseIfRegex().IsMatch(x) || x.StartsWith("else"));
        else if (SpeakerRegex().IsMatch(trimmed))
            return ReadLineBlock();
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

    private TextVariant ReadExpression(ExprInfo exprInfo)
    {
        if (exprInfo.ExprType == ExprType.Hash)
        {
            ReadHashExpression(exprInfo);
            OnHash(_cacheDict);
            _cacheDict.Clear();
            return new();
        }

        return ExprParser.Parse(exprInfo, DialogStorage);
    }

    private int ReadExpressionBlock()
    {
        ReadOnlyMemory<char> mem = _state.MemLine;
        ReadOnlySpan<char> line = mem.Span;
        int charIdx = DialogHelpers.GetNextNonWhitespace(line, 0) + 1;

        if (!ExprInfo.TryGetExprInfo(mem, charIdx, out ExprInfo exprInfo))
            return EndScript;

        ExprType exprType = exprInfo.ExprType;

        if (exprType == ExprType.Hash)
        {
            ReadHashExpression(exprInfo);
            OnHash(_cacheDict);
            _cacheDict.Clear();
            return LineIdx + 1;
        }

        if (exprType == ExprType.BBCode)
            return EndScript;

        if (exprType == ExprType.BuiltIn)
            return ReadBuiltInTag(exprInfo);

        ExprParser.Parse(exprInfo, DialogStorage);
        return exprType == ExprType.Await ? SuspendScript : LineIdx + 1;
    }

    private void ReadHashExpression(ExprInfo exprInfo)
    {
        ReadOnlyMemory<char> mem = exprInfo.Memory;
        ReadOnlySpan<char> expr = exprInfo.Span;
        var altLookup = _cacheDict.GetAlternateLookup<ReadOnlySpan<char>>();
        int i = 1;

        while (i < expr.Length)
        {
            int start = i;
            i = DialogHelpers.GetNextNonIdentifier(expr, i);

            if (i == start)
                return;

            ReadOnlySpan<char> key = expr[start..i];
            int wsStart = i;
            i = DialogHelpers.GetNextNonWhitespace(expr, i);

            if (i < expr.Length && expr[i] != '=' && (expr[i] != '#' || i - wsStart == 0))
                return;

            if (expr[i] != '=')
            {
                altLookup[key] = string.Empty;
                i++;
                continue;
            }

            i++;
            int rightStart = i;
            bool inQuote = false;

            while (i < expr.Length)
            {
                if (inQuote)
                {
                    if (expr[i] == '"' && expr[i - 1] != '\\')
                        inQuote = false;
                }
                else
                {
                    if (expr[i] == '#' && expr[i - 1] == ' ')
                        break;

                    if (expr[i] == '"')
                        inQuote = true;
                }

                i++;
            }

            ExprInfo rightExprInfo = new(mem[rightStart..i], 0, 0);
            TextVariant result = ExprParser.Parse(rightExprInfo, DialogStorage);

            if (result.VariantType == VarType.Undefined)
                return;

            altLookup[key] = result.ToString();
            i++;
        }
    }

    private int ReadBuiltInTag(ExprInfo exprInfo)
    {
        ReadOnlyMemory<char> mem = exprInfo.Memory;
        ReadOnlySpan<char> expr = exprInfo.Span;
        bool isClosingTag = expr.Length > 0 && expr[0] == '/';
        int start = isClosingTag ? 1 : 0;
        int i = DialogHelpers.GetNextNonIdentifier(expr, start);
        ReadOnlySpan<char> firstToken = expr[start..i];
        i = DialogHelpers.GetNextNonWhitespace(expr, i);
        bool isAssignment = false;

        if (i < expr.Length && expr[i] == '=')
        {
            i++;
            isAssignment = true;
        }

        ReadOnlyMemory<char> restOfExpr = mem[i..];
        bool isSingleToken = restOfExpr.Span.IsWhiteSpace();

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

                TextVariant result = ExprParser.Parse(restOfExpr, 0, DialogStorage);

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

            return GetSectionIndex(restOfExpr.Span.Trim());
        }
        else if (firstToken.SequenceEqual(BuiltIn.SPEED))
        {
            if (isClosingTag || !isAssignment)
                return LineIdx + 1;

            TextVariant result = ExprParser.Parse(restOfExpr, 0, DialogStorage);

            if (result.VariantType != VarType.Float || result.Float < 0)
                return LineIdx + 1;

            SpeedMultiplier = result.Float;
        }
        else if (firstToken.SequenceEqual(BuiltIn.PAUSE))
        {
            if (isClosingTag || !isAssignment)
                return LineIdx + 1;

            TextVariant result = ExprParser.Parse(restOfExpr, 0, DialogStorage);

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

    private int ReadConditionBlock()
    {
        ReadOnlyMemory<char> mem = _state.MemLine;
        ReadOnlySpan<char> line = mem.Span;
        int baseIndent = _state.CurrentIndentLevel;
        int charIdx = baseIndent;
        charIdx += "if".Length;
        charIdx = DialogHelpers.GetNextNonWhitespace(line, charIdx) + 1;
        TextVariant result = ExprParser.Parse(mem, charIdx, DialogStorage);
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
            mem = _state.MemLine;
            line = mem.Span;
            charIdx = DialogHelpers.GetNextNonWhitespace(line, 0);
            charIdx += "else if".Length;
            charIdx = DialogHelpers.GetNextNonWhitespace(line, charIdx) + 1;
            result = ExprParser.Parse(mem, charIdx, DialogStorage);
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

        mem = _state.MemLine;
        line = mem.Span;
        charIdx = DialogHelpers.GetNextNonWhitespace(line, 0);

        if (line[charIdx..].StartsWith("else"))
            _state.MoveNextLine();

        return LineIdx;
    }

    private int ReadChoiceBlock()
    {
        if (IsPartOfEarlierChoiceSet())
            return GetLineSkipping(x => x.StartsWith("? "));

        FillChoices();
        OnChoice(_choices);
        _choices.Clear();
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
                    return prevLine[prevIndent..].StartsWith("? ");

                lineIdx--;
            }

            return false;
        }

        void FillChoices()
        {
            int currentIndent = DialogHelpers.GetNextNonWhitespace(Script[LineIdx].Span, 0);
            int lineIdx = LineIdx;

            while (lineIdx < Script.Count)
            {
                ReadOnlyMemory<char> mem = Script[lineIdx];
                ReadOnlySpan<char> line = mem.Span;
                int indent = DialogHelpers.GetNextNonWhitespace(line, 0);

                if (indent > currentIndent || line[indent..].StartsWith("//"))
                {
                    lineIdx++;
                    continue;
                }

                if (indent < currentIndent || !line[indent..].StartsWith("? "))
                    break;

                line = line.StripLineComment();
                mem = mem[..line.Length];
                int i = DialogHelpers.GetNextNonWhitespace(line, indent + 1);
                bool disabled = false;

                if (IfRegex().IsMatch(line[i..]))
                {
                    i += "if".Length;
                    i = DialogHelpers.GetNextNonWhitespace(line, i) + 1;
                    ExprInfo.TryGetExprInfo(mem, i, out ExprInfo exprInfo);
                    TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);

                    if (result.VariantType == VarType.Bool)
                        disabled = !result.Bool;

                    i = DialogHelpers.GetNextNonWhitespace(line, exprInfo.OffsetEnd + 1);
                }

                string key = $"{_state.RowPrefix}_Line{lineIdx}";
                string text = Tr(key);

                if (key != text)
                {
                    mem = text.AsMemory();
                    line = mem.Span;
                    i = 0;
                }

                int appendStart = i;

                while (i < line.Length)
                {
                    if (line[i] != '[' || (i > 0 && line[i - 1] == '\\'))
                    {
                        i++;
                        continue;
                    }

                    ExprInfo.TryGetExprInfo(mem, i, out ExprInfo exprInfo);
                    TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);
                    _sb.Append(line[appendStart..i]);
                    _sb.Append(result.ToString());
                    i = exprInfo.OffsetEnd + 1;
                    appendStart = i;
                }

                if (_sb.Length > 0)
                    _sb.Append(line[appendStart..i]);

                lineIdx++;

                _choices.Add(new Choice
                {
                    Text = _sb.Length > 0 ? _sb.ToString() : line[appendStart..i].ToString(),
                    Next = GetChoiceBranchLineIdx(lineIdx, currentIndent),
                    Disabled = disabled
                });
                _sb.Clear();
            }

            // Resolve choices with no branch
            for (int i = 0; i < _choices.Count; i++)
            {
                Choice choice = _choices[i];

                if (choice.Next == -1)
                    _choices[i] = choice with { Next = lineIdx };
            }
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

    private int ReadLineBlock()
    {
        int charIdx = 0;
        int speakerStart = 0;
        ReadOnlyMemory<char> mem = _state.MemLine;
        ReadOnlySpan<char> line = mem.Span;

        while (charIdx < line.Length)
        {
            if (line[charIdx] == ',' || line[charIdx] == ':')
            {
                _speakerIds.Add(line[speakerStart..charIdx].Trim().ToString());
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
        string key = $"{_state.RowPrefix}_Line{_state.LineIdx}";
        string text = Tr(key);

        if (key != text)
        {
            mem = text.AsMemory();
            line = mem.Span;
            charIdx = 0;
        }
        else if (line[charIdx..].StartsWith("^^"))
        {
            charIdx += 2;

            if (line.TrimEnd().EndsWith("^^"))
                line = line.TrimEnd()[..^2];
            else
                isLastLine = false;
        }

        int appendStart = charIdx;

        while (charIdx < line.Length)
        {
            CheckForTag(_textEvents, ref charIdx, line, mem, ref appendStart);
            charIdx++;

            if (charIdx < line.Length)
                continue;

            _sb.Append(line[appendStart..charIdx]);

            if (isLastLine)
                break;

            _state.MoveNextLine();
            mem = _state.MemLine;
            line = mem.Span;
            charIdx = DialogHelpers.GetNextNonWhitespace(line, 0);
            appendStart = charIdx;

            if (line.TrimEnd().EndsWith("^^"))
            {
                line = line.TrimEnd()[..^2];
                isLastLine = true;
            }
        }

        text = _sb.ToString();
        _sb.Clear();

        _inDialogLine = true;
        OnDialogLineStarted(text, _speakerIds, _textEvents);
        _speakerIds.Clear();
        _textEvents.Clear();
        return SuspendScript;

        void CheckForTag(
            List<TextEvent> textEvents,
            ref int charIdx,
            ReadOnlySpan<char> line,
            ReadOnlyMemory<char> mem,
            ref int appendStart)
        {
            if ((line[charIdx] == '[' || line[charIdx] == ']') && line[charIdx - 1] == '\\')
            {
                _sb.Append(line[appendStart..(charIdx - 1)]);
                appendStart = charIdx;
                return;
            }

            if (line[charIdx] != '[')
                return;

            int exprStart = charIdx;

            if (!ExprInfo.TryGetExprInfo(mem, exprStart, out ExprInfo exprInfo))
                return;

            charIdx = exprInfo.OffsetEnd;
            ExprType exprType = exprInfo.ExprType;

            if (exprType == ExprType.BBCode)
                return;

            _sb.Append(line[appendStart..exprStart]);
            appendStart = charIdx + 1;

            if (exprType == ExprType.Evaluation)
            {
                VarType varType = ExprParser.GetVarType(exprInfo, DialogStorage);

                if (varType is VarType.Float or VarType.String)
                {
                    TextVariant result = ExprParser.Parse(exprInfo, DialogStorage);
                    _sb.Append(result.ToString());
                    return;
                }
            }

            textEvents.Add(new()
            {
                Tag = mem[exprInfo.OffsetStart..exprInfo.OffsetEnd],
                TextIndex = _sb.Length,
                IsAwait = exprType == ExprType.Await
            });
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
}
