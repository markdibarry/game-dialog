using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;

namespace GameDialog.Runner;

internal partial class DialogReader
{
    public DialogReader(Dialog dialog)
    {
        _dialog = dialog;
    }

    public List<Choice>? CacheChoiceList { get; set; }
    public List<string>? CacheSpeakerList { get; set; }
    public Dictionary<string, string>? CacheHashDict { get; set; }

    private const int EndScript = -2;
    private const int SuspendScript = -1;

    private readonly Dialog _dialog;
    private readonly StringBuilder _sb = new();
    private DialogValidator? _validator;
    private readonly ParserState _state = new();
    private bool _inDialogLine;
    private List<ReadOnlyMemory<char>> Script => _state.Script;
    private int LineIdx => _state.LineIdx;

    [GeneratedRegex(@"^\s*--\w+--\s*$")]
    private static partial Regex TitleRegex();
    [GeneratedRegex(@"^\s*if\s*\[")]
    private static partial Regex IfRegex();
    [GeneratedRegex(@"^\s*else if\s*\[")]
    private static partial Regex ElseIfRegex();
    [GeneratedRegex(@"^\s*\w+(?:,\s*\w+)*:\s")]
    private static partial Regex SpeakerRegex();
    private static readonly string SingleLineTitle = "--SingleLine--";

    public void Clear()
    {
        _state.Reset();
        _state.Script.Clear();
    }

    /// <summary>
    /// Loads a script from a path.
    /// </summary>
    /// <param name="path"></param>
    public void Load(string path)
    {
        string text;

        if (!Godot.FileAccess.FileExists(path))
            return;

        // Godot does not allow array pooling, so must use heavier approach
        using (var fs = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read))
            text = fs.GetAsText();

        _state.ReadStringToScript(text, path);
    }

    /// <summary>
    /// Loads a script from a string.
    /// </summary>
    /// <param name="text"></param>
    public void LoadFromText(string text)
    {
        Clear();
        _state.ReadStringToScript(text, string.Empty);
    }

    internal void LoadSingleLine(string text)
    {
        Clear();
        _state.Script.Add(SingleLineTitle.AsMemory());
        _state.Script.Add(text.AsMemory());
    }

    /// <summary>
    /// Loads a script from a path using System.IO
    /// Faster, but not able to run in exports.
    /// </summary>
    /// <param name="filePath">The filepath to read from</param>
    /// <param name="rootPath">The project's root path</param>
    public void LoadFromFile(string filePath, string rootPath)
    {
        Clear();
        _state.ReadStringToScript(filePath, rootPath);
    }

    /// <summary>
    /// Updates Errors with issues.
    /// </summary>
    /// <param name="errors">The error list to populate</param>
    /// <param name="chart">The StringBuilder for generating a chart</param>
    /// <param name="sw">The StreamWriter for generating translations</param>
    public void ValidateScript(List<Error> errors, StringBuilder? chart = null, StreamWriter? sw = null)
    {
        _validator ??= new(_dialog.DialogStorage, _state);
        _validator.ValidateScript(errors, chart, sw);
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

    /// <summary>
    /// Resumes the dialog.
    /// </summary>
    public void Resume() => Resume(LineIdx + 1);

    /// <summary>
    /// Resumes the dialog.
    /// </summary>
    /// <param name="nextIndex">The next index to read.</param>
    internal void Resume(int nextIndex)
    {
        // Next has not been set, so a line must be in progress.
        if (_inDialogLine)
        {
            _dialog.InvokeDialogLineResumed();
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

        _dialog.InvokeScriptEnded();
    }

    internal void EndDialogLine()
    {
        _inDialogLine = false;
        Resume();
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

    private TextVariant ReadLineExpression(ExprInfo exprInfo)
    {
        if (exprInfo.ExprType == ExprType.Hash)
        {
            CacheHashDict?.Clear();
            Dictionary<string, string> dict = CacheHashDict ?? [];
            ReadHashExpression(exprInfo, dict);
            _dialog.InvokeHashRead(dict);
            return new();
        }

        return ExprParser.Parse(exprInfo, _dialog.DialogStorage);
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
            CacheHashDict?.Clear();
            Dictionary<string, string> dict = CacheHashDict ?? [];
            ReadHashExpression(exprInfo, dict);
            _dialog.InvokeHashRead(dict);
            return LineIdx + 1;
        }

        if (exprType == ExprType.BBCode)
            return EndScript;

        if (exprType == ExprType.BuiltIn)
            return ReadBuiltInTag(exprInfo);

        ExprParser.Parse(exprInfo, _dialog.DialogStorage);
        return exprType == ExprType.Await ? SuspendScript : LineIdx + 1;
    }

    private void ReadHashExpression(ExprInfo exprInfo, Dictionary<string, string> dict)
    {
        ReadOnlyMemory<char> mem = exprInfo.Memory;
        ReadOnlySpan<char> expr = exprInfo.Span;
        var altLookup = dict.GetAlternateLookup<ReadOnlySpan<char>>();
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
            TextVariant result = ExprParser.Parse(rightExprInfo, _dialog.DialogStorage);

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

                TextVariant result = ExprParser.Parse(new(restOfExpr, 0, 0), _dialog.DialogStorage);

                if (result.VariantType != VarType.Float)
                    return LineIdx + 1;

                value = result.Float;
            }

            _dialog.GlobalAutoProceedEnabled = value != -2;
            _dialog.GlobalAutoProceedTimeout = value;
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

            TextVariant result = ExprParser.Parse(new(restOfExpr, 0, 0), _dialog.DialogStorage);

            if (result.VariantType != VarType.Float || result.Float < 0)
                return LineIdx + 1;

            _dialog.GlobalSpeedMultiplier = result.Float;
        }
        else if (firstToken.SequenceEqual(BuiltIn.PAUSE))
        {
            if (isClosingTag || !isAssignment)
                return LineIdx + 1;

            TextVariant result = ExprParser.Parse(new(restOfExpr, 0, 0), _dialog.DialogStorage);

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
        TextVariant result = ExprParser.Parse(mem, charIdx, _dialog.DialogStorage);
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
            result = ExprParser.Parse(mem, charIdx, _dialog.DialogStorage);
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

        CacheChoiceList?.Clear();
        List<Choice> choices = CacheChoiceList ?? [];
        FillChoices(choices);
        _dialog.InvokeChoiceRead(choices);
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

        void FillChoices(List<Choice> choices)
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
                    TextVariant result = ExprParser.Parse(exprInfo, _dialog.DialogStorage);

                    if (result.VariantType == VarType.Bool)
                        disabled = !result.Bool;

                    i = DialogHelpers.GetNextNonWhitespace(line, exprInfo.OffsetEnd + 1);
                }

                if (Dialog.TranslationFileType != TranslationFileType.None && _state.RowPrefix.Length > 0)
                {
                    string key = $"{_state.RowPrefix}_Line{lineIdx}";
                    string text;

                    if (Dialog.TranslationFileType == TranslationFileType.CSV)
                        text = TranslationServer.Translate(key);
                    else
                        text = TranslationServer.Translate(mem[i..line.Length].ToString(), key);

                    if (key != text)
                    {
                        mem = text.AsMemory();
                        line = mem.Span;
                        i = 0;
                    }
                }

                int appendStart = i;

                while (i < line.Length)
                {
                    if (line[i] != '[')
                    {
                        i++;
                        continue;
                    }

                    ExprInfo.TryGetExprInfo(mem, i, out ExprInfo exprInfo);
                    TextVariant result = ExprParser.Parse(exprInfo, _dialog.DialogStorage);
                    _sb.Append(line[appendStart..i]);
                    _sb.Append(result.ToString());
                    i = exprInfo.OffsetEnd + 1;
                    appendStart = i;
                }

                if (_sb.Length > 0)
                    _sb.Append(line[appendStart..i]);

                lineIdx++;

                choices.Add(new Choice(
                    text: _sb.Length > 0 ? _sb.ToString() : line[appendStart..i].ToString(),
                    next: GetChoiceBranchLineIdx(lineIdx, currentIndent),
                    disabled: disabled
                ));
                _sb.Clear();
            }

            // Resolve choices with no branch
            for (int i = 0; i < choices.Count; i++)
            {
                Choice choice = choices[i];

                if (choice.Next == -1)
                    choices[i] = choice with { Next = lineIdx };
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
        ReadOnlySpan<char> line = _state.SpanLine;
        CacheSpeakerList?.Clear();
        List<string> speakerIds = CacheSpeakerList ?? [];

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

        if (line[charIdx] == ' ')
            charIdx++; // Skip the first space after the colon

        string text = GetLineText(line, charIdx);
        _inDialogLine = true;
        _dialog.InvokeDialogLineStarted(text, speakerIds);
        return SuspendScript;
    }

    private string GetLineText(ReadOnlySpan<char> line, int charIdx)
    {
        if (Dialog.TranslationFileType == TranslationFileType.CSV && _state.RowPrefix.Length > 0)
        {
            string key = $"{_state.RowPrefix}_Line{_state.LineIdx}";
            string translatedText = TranslationServer.Translate(key);

            if (key != translatedText)
                return translatedText;
        }

        // Check if is a multiline
        if (!line[charIdx..].TrimStart().StartsWith("^^"))
            return GetTextOrPotTranslation(line[charIdx..].ToString());

        charIdx = DialogHelpers.GetNextNonWhitespace(line, charIdx) + 2;
        line = line[charIdx..];

        if (line.TrimEnd().EndsWith("^^"))
            return GetTextOrPotTranslation(line.TrimEnd()[..^2].ToString());

        _sb.Append(line);
        bool isLastLine = false;

        while (LineIdx < Script.Count && !isLastLine)
        {
            _state.MoveNextLine();
            line = _state.SpanLine;
            int lineStart = DialogHelpers.GetNextNonWhitespace(line, 0);

            if (line.TrimEnd().EndsWith("^^"))
            {
                line = line.TrimEnd()[..^2];
                isLastLine = true;
            }

            _sb.Append(line[lineStart..]);
        }

        string text = GetTextOrPotTranslation(_sb.ToString());
        _sb.Clear();
        return text;

        string GetTextOrPotTranslation(string text)
        {
            if (Dialog.TranslationFileType != TranslationFileType.POT || _state.RowPrefix.Length == 0)
                return text;

            string key = $"{_state.RowPrefix}_Line{_state.LineIdx}";
            string translatedText = TranslationServer.Translate(text, key);

            if (!translatedText.SequenceEqual(text))
                return translatedText;

            return text;
        }
    }

    internal string ParseEventsFromText(string text, List<TextEvent> textEvents)
    {
        if (text.Length == 0)
            return text;

        ReadOnlyMemory<char> mem = text.AsMemory();
        ReadOnlySpan<char> line = text.AsSpan();
        int charIdx = 0;
        int appendStart = charIdx;

        while (charIdx < line.Length)
        {
            CheckForTag(textEvents, mem, line, ref charIdx, ref appendStart);
            charIdx++;
        }

        if (_sb.Length == 0 && appendStart == 0)
            return text;

        _sb.Append(line[appendStart..]);
        string result = _sb.ToString();
        _sb.Clear();
        return result;

        void CheckForTag(
            List<TextEvent> textEvents,
            ReadOnlyMemory<char> mem,
            ReadOnlySpan<char> line,
            ref int charIdx,
            ref int appendStart)
        {
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
                VarType varType = ExprParser.GetVarType(exprInfo, _dialog.DialogStorage);

                if (varType is VarType.Float or VarType.String)
                {
                    TextVariant result = ExprParser.Parse(exprInfo, _dialog.DialogStorage);
                    _sb.Append(result.ToString());
                    return;
                }
            }

            if (exprType == ExprType.BuiltIn)
            {
                int tagNameEnd = DialogHelpers.GetNextNonIdentifier(line, exprInfo.OffsetStart);
                ReadOnlySpan<char> tagName = line[exprInfo.OffsetStart..tagNameEnd];

                if (tagName.SequenceEqual(BuiltIn.PAGE))
                {
                    textEvents.Add(new()
                    {
                        Tag = BuiltIn.PROMPT.AsMemory(),
                        TextIndex = _sb.Length
                    });
                    _sb.Append("[br]");
                    textEvents.Add(new()
                    {
                        Tag = BuiltIn.SCROLL.AsMemory(),
                        TextIndex = _sb.Length
                    });
                    return;
                }
            }

            textEvents.Add(new()
            {
                Tag = mem[exprInfo.OffsetStart..exprInfo.OffsetEnd],
                TextIndex = _sb.Length
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

    /// <summary>
    /// Adjusts TextEvent indices based on comparing the text before and after setting the RichTextLabel.
    /// An alternative to setting the RichTextLabel.Text twice.
    /// </summary>
    internal static void AdjustEventIndices(ReadOnlySpan<char> eventParsedText, ReadOnlySpan<char> displayedText, List<TextEvent> events)
    {
        if (events == null || events.Count == 0)
            return;

        int oLen = eventParsedText.Length;
        int pLen = displayedText.Length;
        int oPos = 0;
        int pPos = 0;
        int eventIdx = 0;
        int offset = 0;

        while (oPos < oLen)
        {
            if (!TryAdjustEvent(events, ref eventIdx, oPos, offset))
                return;

            if (eventParsedText[oPos] != '[')
            {
                oPos++;
                pPos++;
                continue;
            }

            int tagStart = oPos;
            int tagEnd = GetBracketEnd(eventParsedText, oLen, tagStart);

            // Doesn't close
            if (tagEnd >= oLen)
            {
                oPos++;
                pPos++;
                continue;
            }

            int tagLength = tagEnd + 1 - tagStart;
            ReadOnlySpan<char> oldTag = eventParsedText[tagStart..(tagEnd + 1)];

            // Same text, skip
            if (pPos + tagLength < pLen && oldTag.SequenceEqual(displayedText[pPos..(pPos + tagLength)]))
            {
                oPos += tagLength;
                pPos += tagLength;
                continue;
            }

            int tagNameStart = tagStart + 1;

            if (eventParsedText[tagNameStart] == '/')
            {
                offset += tagLength;
                oPos += tagLength;
                pPos++;
                continue;
            }

            int tagNameEnd = DialogHelpers.GetNextNonIdentifier(eventParsedText, tagNameStart);
            ReadOnlySpan<char> tagName = eventParsedText[tagNameStart..tagNameEnd];

            if (tagName.SequenceEqual("img"))
            {
                int closeTagStart = tagEnd + 1;

                while (closeTagStart < oLen)
                {
                    if (eventParsedText[closeTagStart] != '[')
                    {
                        closeTagStart++;
                        continue;
                    }

                    if (eventParsedText[closeTagStart..].StartsWith("[/img]"))
                    {
                        int tagsLength = closeTagStart + "[/img]".Length - tagStart;
                        oPos += tagsLength;
                        int imgOffset = IsImgTagValid(eventParsedText[oPos..], displayedText[pPos..]) ? 1 : 0;
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

    internal bool TryEvaluateExpression(ReadOnlyMemory<char> text)
    {
        try
        {
            ExprInfo exprInfo = new(text, 0, 0);
            TextVariant result = ReadLineExpression(exprInfo);
            return result.VariantType != VarType.Undefined;
        }
        catch (Exception) { }

        return false;
    }

    internal bool TryParseBuiltInEvent(TextEvent textEvent, out EventType eventType, out float parameter)
    {
        eventType = EventType.Undefined;
        parameter = 0;
        ExprInfo exprInfo = new(textEvent.Tag, 0, 0);
        ReadOnlySpan<char> expr = exprInfo.Span;

        if (exprInfo.ExprType != ExprType.BuiltIn)
            return false;

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
                TextVariant result = ExprParser.Parse(autoExpr, _dialog.DialogStorage);

                if (isClosingTag || result.VariantType != VarType.Float || result.Float < 0)
                    return false;

                eventType = EventType.Auto;
                parameter = result.Float;
                return true;
            }

            if (!isSingleToken)
                return false;

            eventType = EventType.Auto;
            parameter = isClosingTag ? -2 : -1;
            return true;
        }
        else if (tagKey.SequenceEqual(BuiltIn.PAUSE))
        {
            if (!isAssignment || isClosingTag)
                return false;

            ExprInfo pauseExpr = new(exprInfo.Memory[start..], 0, 0);
            TextVariant result = ExprParser.Parse(pauseExpr, _dialog.DialogStorage);

            if (result.VariantType != VarType.Float || result.Float <= 0)
                return false;

            eventType = EventType.Pause;
            parameter = result.Float;
            return true;
        }
        else if (tagKey.SequenceEqual(BuiltIn.SPEED))
        {
            if (isAssignment)
            {
                if (isClosingTag)
                    return false;

                ExprInfo speedExpr = new(exprInfo.Memory[start..], 0, 0);
                TextVariant result = ExprParser.Parse(speedExpr, _dialog.DialogStorage);

                if (result.VariantType != VarType.Float || result.Float <= 0)
                    return false;

                eventType = EventType.Speed;
                parameter = result.Float;
                return true;
            }

            if (!isClosingTag || !isSingleToken)
                return false;

            eventType = EventType.Speed;
            parameter = 1;
            return true;
        }
        else if (tagKey.SequenceEqual(BuiltIn.SCROLL))
        {
            eventType = EventType.Scroll;
            return true;
        }
        else if (tagKey.SequenceEqual(BuiltIn.PROMPT))
        {
            eventType = EventType.Prompt;
            return true;
        }

        return false;
    }
}
