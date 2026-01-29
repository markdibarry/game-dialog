using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GameDialog.Runner;

public partial class DialogValidator : IMemberStorage
{
    public DialogValidator(
        ParserState state,
        Dictionary<string, VarDef> predefinedVarDefs,
        Dictionary<string, FuncDef> predefinedFuncDefs)
    {
        _state = state;
        _predefinedVarDefs = predefinedVarDefs;
        _predefinedFuncDefs = predefinedFuncDefs;
    }

    public TranslationFileType TranslationMode { get; set; }
    private readonly List<ReadOnlyMemory<char>> _sectionTitles = [];
    private readonly ParserState _state;
    private readonly Dictionary<string, VarDef> _predefinedVarDefs;
    private readonly Dictionary<string, FuncDef> _predefinedFuncDefs;
    private readonly List<VarDef> _localVarDefs = [];
    private readonly StringBuilder _sb = new();
    private List<Error>? _errors;
    private StringBuilder? _chart;
    private StreamWriter? _sw;
    private int _charIdx;
    private bool _lineVisited;
    private bool _exitSection;

    [GeneratedRegex(@"^\s*--\w+--\s*$")]
    private static partial Regex TitleRegex();
    [GeneratedRegex(@"^\s*\w+(?:,\s*\w+)*:\s")]
    private static partial Regex SpeakerRegex();
    [GeneratedRegex(@"^\s*if\s*\[")]
    private static partial Regex IfRegex();
    [GeneratedRegex(@"^\s*else if\s*\[")]
    private static partial Regex ElseIfRegex();

    public void ValidateScript(List<Error> errors, StringBuilder? chart = null, StreamWriter? sw = null)
    {
        if (_state.Script.Count == 0)
        {
            errors?.AddError(0, 0, 0, "Script is empty");
            return;
        }

        _errors = errors;
        _chart = chart;
        _sw = sw;

        try
        {
            ValidateAllTitles();
            ValidateSections();
        }
        catch(Exception)
        {
            errors?.AddError(_state.LineIdx, 0, _state.SpanLine.Length, "Dialog line invalid.");
        }
        finally
        {
            Reset();
        }
    }

    private void Reset()
    {
        _localVarDefs.Clear();
        _state.Reset();
        _sectionTitles.Clear();
        _sb.Clear();
        _errors = null;
        _chart = null;
        _sw = null;
        _charIdx = 0;
        _lineVisited = false;
        _exitSection = false;
    }

    private void MoveNextLine()
    {
        _charIdx = 0;
        _lineVisited = false;
        _exitSection = false;
        _state.MoveNextLine(_errors);
    }

    private void ValidateAllTitles()
    {
        _state.MoveLine(0, _errors);

        while (_state.LineIdx < _state.Script.Count)
        {
            ReadOnlySpan<char> span = _state.SpanLine;

            if (TryGetTitleRange(span, out (int Start, int Length) range))
            {
                ReadOnlyMemory<char> mem = _state.MemLine.Slice(range.Start, range.Length);

                if (_sectionTitles.ContainsSequence(mem.Span))
                    _errors?.AddError(_state.LineIdx, 0, span.Length, $"Duplicate section title '{mem.Span}'");
                else
                    _sectionTitles.Add(mem);
            }

            MoveNextLine();
        }

        if (_sectionTitles.Count == 0)
            _errors?.AddError(0, 0, _state.Script[0].Length, "No section titles found. Ensure your script has at least one title using letters, numbers or underscores.");
    }

    private void ValidateSections()
    {
        _lineVisited = false;
        _exitSection = false;
        _charIdx = 0;
        _state.MoveLine(0, _errors);

        while (_state.LineIdx < _state.Script.Count)
        {
            ReadOnlySpan<char> line = _state.SpanLine;

            if (!TitleRegex().IsMatch(line))
                _errors?.AddError(_state.LineIdx, 0, line.Length, "Section must start with a title.");

            if (_chart != null)
            {
                if (_state.LineIdx > 0)
                    _chart.AppendLine();

                if (TryGetTitleRange(line, out (int Start, int Length) range))
                    _chart.AppendChart(_state, $"Title: {line.Slice(range.Start, range.Length)}");
            }

            MoveNextLine();
            ValidateBlocks();
        }
    }

    private void ValidateBlocks()
    {
        while (_state.LineIdx < _state.Script.Count)
        {
            ValidateBlock();

            if (_exitSection || _state.Dedent())
                break;

            if (!_lineVisited)
                continue;

            MoveNextLine();

            if (_exitSection || _state.Dedent())
                break;
        }
    }

    private void ValidateBlock()
    {
        ReadOnlySpan<char> line = _state.SpanLine;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);
        ReadOnlySpan<char> trimmed = line[_charIdx..];

        if (TitleRegex().IsMatch(line))
        {
            _lineVisited = true;
            _exitSection = true;
            return;
        }

        if (trimmed.StartsWith('['))
        {
            ValidateExpressionBlock(ExprContext.Block);
        }
        else if (trimmed.StartsWith("? "))
        {
            ValidateChoiceBlock();
        }
        else if (IfRegex().IsMatch(trimmed))
        {
            ValidateConditionBlock();
        }
        else if (SpeakerRegex().IsMatch(trimmed))
        {
            ValidateLineBlock();
        }
        else
        {
            _lineVisited = true;

            if (ElseIfRegex().IsMatch(trimmed) || trimmed.StartsWith("else"))
                _errors?.AddError(_state.LineIdx, 0, line.Length - 1, "No matching if block.");
            else
                _errors?.AddError(_state.LineIdx, 0, line.Length - 1, "Invalid block.");
        }
    }

    private void ValidateExpressionBlock(ExprContext exprContext)
    {
        _lineVisited = true;
        ReadOnlyMemory<char> mem = _state.MemLine;
        ReadOnlySpan<char> line = mem.Span;

        if (!ExprInfo.TryGetExprInfo(mem, _state.LineIdx, _charIdx, _errors, out ExprInfo exprInfo))
        {
            _charIdx = line.Length;
            return;
        }

        if (exprContext == ExprContext.Block && !line[(exprInfo.OffsetEnd + 1)..].IsWhiteSpace())
        {
            _errors?.AddError(_state.LineIdx, _charIdx, line.Length, "Expression block has unexpected chars following it.");
            return;
        }

        ExprType exprType = exprInfo.ExprType;

        if (exprType == ExprType.Hash)
        {
            ValidateHashExpression(exprInfo, exprContext);
            return;
        }

        if (exprContext == ExprContext.Block)
            _chart?.AppendChart(_state, $"Expression: {exprInfo.Span}");

        _charIdx = exprInfo.OffsetEnd;

        if (exprInfo.Span.Length == 0)
            return;

        if (exprType == ExprType.BBCode)
        {
            if (exprContext != ExprContext.Line)
                _errors?.AddError(exprInfo, "BBCode tags can only be used in a dialog line.");

            return;
        }

        if (exprType != ExprType.BuiltIn)
        {
            ExprParser.Parse(exprInfo, this, _errors);
            return;
        }

        ValidateBuiltInTag(exprInfo, exprContext);
    }

    private void ValidateBuiltInTag(ExprInfo exprInfo, ExprContext exprContext)
    {
        if (exprContext == ExprContext.Choice)
        {
            _errors?.AddError(exprInfo, "Built in tags cannot be used in a choice.");
            return;
        }

        ReadOnlySpan<char> expr = exprInfo.Span;
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
            start++;
            start = DialogHelpers.GetNextNonWhitespace(expr, start);
            tagValue = expr[start..];
        }

        if (tagKey.SequenceEqual(BuiltIn.AUTO))
        {
            if (isAssignment)
            {
                ExprInfo rightExprInfo = new(exprInfo.Memory, _state.LineIdx, exprInfo.OffsetStart + start);
                TextVariant result = ExprParser.Parse(rightExprInfo, this, _errors);

                if (isClosingTag)
                    _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' can only be assigned to or be part of a closing tag.");
                else if (result.VariantType != VarType.Float)
                    _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' must have a float value assigned.");
                else if (result.Float < 0)
                    _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot be less than 0.");
            }
            else if (!isSingleToken)
            {
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot have additional parameters.");
            }
        }
        else if (tagKey.SequenceEqual(BuiltIn.END))
        {
            if (exprContext == ExprContext.Line)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot be used in a dialog line.");
            else if (isClosingTag)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot be a closing tag.");
            else if (!isSingleToken)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot have additional parameters.");
        }
        else if (tagKey.SequenceEqual(BuiltIn.GOTO))
        {
            if (exprContext == ExprContext.Line)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot be used in a dialog line.");
            else if (isClosingTag)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot be a closing tag.");
            else if (isSingleToken)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' requires a section id.");
            else if (isAssignment)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' does not support assignment.");
            else if (!_sectionTitles.ContainsSequence(tagValue.Trim()))
                _errors?.AddError(exprInfo, $"Section '{tagValue.Trim()}' does not exist.");
        }
        else if (tagKey.SequenceEqual(BuiltIn.PAUSE))
        {
            ExprInfo rightExprInfo = new(exprInfo.Memory, _state.LineIdx, exprInfo.OffsetStart + start);

            if (exprContext == ExprContext.Choice)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' can not be used in a choice.");
            else if (!isAssignment)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' must have a float value assigned.");
            else if (isClosingTag)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' cannot be a closing tag.");
            else if (ExprParser.Parse(rightExprInfo, this, _errors).VariantType != VarType.Float)
                _errors?.AddError(exprInfo, $"Built-in tag '{tagKey}' must have a float value assigned.");
        }
        else if (tagKey.SequenceEqual(BuiltIn.SPEED))
        {
            // TODO: Works in both dialog and standalone?
            if (isAssignment)
            {
                if (isClosingTag)
                    _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagKey}' can only be assigned to or be part of a closing tag.");

                ExprInfo rightExprInfo = new(exprInfo.Memory, _state.LineIdx, exprInfo.OffsetStart + start);
                TextVariant result = ExprParser.Parse(rightExprInfo, this, _errors);

                if (result.VariantType != VarType.Float || result.Float < 0)
                    _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagKey}' must have a positive float value assigned.");
            }
            else if (!isClosingTag || !isSingleToken)
            {
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagKey}' can only be assigned to or be part of a closing tag.");
            }
        }
        else if (tagKey.SequenceEqual(BuiltIn.SCROLL) || tagKey.SequenceEqual(BuiltIn.PROMPT) || tagKey.SequenceEqual(BuiltIn.PAGE))
        {
            if (exprContext != ExprContext.Line)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagKey}' can only be used in a dialog line.");
            else if (isClosingTag)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagKey}' cannot be a closing tag.");
            else if (!isSingleToken)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagKey}' cannot have additional parameters.");
        }
    }

    private void ValidateHashExpression(ExprInfo exprInfo, ExprContext exprContext)
    {
        ReadOnlySpan<char> expr = exprInfo.Span;
        int i = 1;

        if (exprContext == ExprContext.Choice)
        {
            _errors?.AddError(exprInfo, "Hash expressions cannot be used inside a Choice.");
            return;
        }

        if (exprContext != ExprContext.Line)
            _chart?.AppendChart(_state, $"Hash Expression: {exprInfo.Span}");

        _charIdx = exprInfo.OffsetEnd;

        while (i < expr.Length)
        {
            int start = i;
            i = DialogHelpers.GetNextNonIdentifier(expr, i);

            if (i == start)
            {
                _errors?.AddError(exprInfo, start - 1, "Invalid Hash Tag.");
                return;
            }

            int wsStart = i;
            i = DialogHelpers.GetNextNonWhitespace(expr, i);

            if (i < expr.Length && expr[i] != '=' && (expr[i] != '#' || i - wsStart == 0))
            {
                _errors?.AddError(exprInfo, start - 1, "Invalid Hash Tag.");
                return;
            }

            if (i >= expr.Length || expr[i] != '=')
            {
                i++;
                continue;
            }

            i++;
            int rightStart = i;
            bool inQuote = false;

            while (i < expr.Length)
            {
                if (expr[i] == '#' && expr[i - 1] == ' ')
                    break;

                if (expr[i] == '"')
                {
                    if (!inQuote)
                        inQuote = true;
                    else if (expr[i - 1] != '\\')
                        inQuote = false;
                }

                i++;
            }

            ExprInfo rightExprInfo = new(exprInfo.Memory[rightStart..i], _state.LineIdx, rightStart + exprInfo.OffsetStart);

            if (ExprParser.Parse(rightExprInfo, this, _errors).VariantType == VarType.Undefined)
            {
                _errors?.AddError(rightExprInfo, "Invalid Hash Tag assignment.");
                return;
            }

            i++;
        }
    }

    private void ValidateConditionBlock()
    {
        int lineStart = _charIdx;
        _lineVisited = true;
        _charIdx += "if".Length;
        ReadOnlyMemory<char> mem = _state.MemLine;
        ReadOnlySpan<char> line = mem.Span;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

        if (!ExprInfo.TryGetExprInfo(mem, _state.LineIdx, _charIdx, _errors, out ExprInfo exprInfo)
            || ExprParser.Parse(exprInfo, this, _errors).VariantType != VarType.Bool
            || !line[(exprInfo.OffsetEnd + 1)..].IsWhiteSpace())
        {
            _charIdx = line.Length;
            _errors?.AddError(_state.LineIdx, lineStart, line.Length, "Condition is invalid.");
        }

        _chart?.AppendChart(_state, "If");
        MoveNextLine();

        if (_state.IndentChange <= 0)
        {
            _errors?.AddError(_state.LineIdx, 0, _state.SpanLine.Length, "Condition block must be indented.");
            return;
        }

        ValidateBlocks();

        if (_state.Dedents > 0)
            return;

        mem = _state.MemLine;
        line = mem.Span;

        while (ElseIfRegex().IsMatch(line))
        {
            lineStart = _charIdx;
            _lineVisited = true;
            _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);
            _charIdx += "else if".Length;
            _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

            if (!ExprInfo.TryGetExprInfo(mem, _state.LineIdx, _charIdx, _errors, out exprInfo)
                || ExprParser.Parse(exprInfo, this, _errors).VariantType != VarType.Bool
                || !line[(exprInfo.OffsetEnd + 1)..].IsWhiteSpace())
            {
                _charIdx = line.Length;
                _errors?.AddError(_state.LineIdx, lineStart, line.Length, "Condition is invalid.");
            }

            _chart?.AppendChart(_state, "Else If");
            MoveNextLine();

            if (_state.IndentChange <= 0)
            {
                _errors?.AddError(_state.LineIdx, 0, _state.SpanLine.Length, "Condition block must be indented.");
                return;
            }

            ValidateBlocks();

            if (_state.Dedents > 0)
                return;

            mem = _state.MemLine;
            line = mem.Span;
        }

        _charIdx = DialogHelpers.GetNextNonWhitespace(_state.SpanLine, _charIdx);

        if (_state.SpanLine[_charIdx..].StartsWith("else"))
        {
            _lineVisited = true;
            _charIdx += "else".Length;

            if (!_state.SpanLine[_charIdx..].IsWhiteSpace())
            {
                _errors?.AddError(_state.LineIdx, 0, _charIdx, "Condition is invalid.");
                return;
            }

            _chart?.AppendChart(_state, "Else");
            MoveNextLine();

            if (_state.IndentChange <= 0)
            {
                _errors?.AddError(_state.LineIdx, 0, _state.SpanLine.Length, "Condition block must be indented.");
                return;
            }

            ValidateBlocks();
        }
    }

    private void ValidateChoiceBlock()
    {
        ReadOnlySpan<char> line = _state.SpanLine;

        while (line[_charIdx..].StartsWith("? "))
        {
            int lineStart = _charIdx;
            _lineVisited = true;
            _charIdx++;
            _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

            if (IfRegex().IsMatch(line[_charIdx..]))
            {
                _charIdx += "if".Length;
                _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

                if (!ExprInfo.TryGetExprInfo(_state.MemLine, _state.LineIdx, _charIdx, _errors, out ExprInfo exprInfo)
                    || ExprParser.Parse(exprInfo, this, _errors).VariantType != VarType.Bool)
                {
                    _charIdx = line.Length;
                    _errors?.AddError(_state.LineIdx, lineStart, line.Length, "Condition is invalid.");
                }
                else
                {
                    _charIdx = DialogHelpers.GetNextNonWhitespace(line, exprInfo.OffsetEnd + 1);
                }
            }

            _chart?.AppendChart(_state, $"Choice: {line.GetSnippet(_charIdx)}");
            int choiceTextStart = _charIdx;

            while (_charIdx < line.Length)
            {
                if (line[_charIdx] == '[')
                    ValidateExpressionBlock(ExprContext.Choice);

                _charIdx++;
            }

            if (_sw != null)
            {
                ReadOnlySpan<char> choiceText = line[choiceTextStart..];

                if (!choiceText.IsWhiteSpace())
                {
                    if (TranslationMode == TranslationFileType.CSV)
                    {
                        _sw.WriteLine();
                        _sw.Write(_state.RowPrefix);
                        _sw.Write("_Line");
                        _sw.Write(_state.LineIdx);
                        _sw.Write(',');
                        WriteEscapedCsvField(_sw, choiceText);
                    }
                    else
                    {
                        _sw.WriteLine();
                        _sw.Write("msgctxt \"");
                        _sw.Write(_state.RowPrefix);
                        _sw.Write("_Line");
                        _sw.Write(_state.LineIdx);
                        _sw.WriteLine("\"");
                        _sw.Write("msgid \"");
                        _sw.Write(choiceText);
                        _sw.WriteLine("\"");
                        _sw.WriteLine("msgstr \"\"");
                    }
                }
            }

            MoveNextLine();

            if (_state.IndentChange <= 0)
            {
                line = _state.SpanLine;
                continue;
            }

            ValidateBlocks();

            if (_state.Dedents > 0)
                return;

            line = _state.SpanLine;
        }
    }

    private void ValidateLineBlock()
    {
        _lineVisited = true;
        ReadOnlySpan<char> line = _state.SpanLine;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

        while (_charIdx < line.Length && line[_charIdx] != ':')
            _charIdx++;

        //ReadOnlySpan<char> speakerId = line[start..(_charIdx - 1)];
        _charIdx++;

        if (line[_charIdx] == ' ')
            _charIdx++; // Skip the first space after the colon

        int originalIndent = _state.CurrentIndentLevel;
        int appendStart = _charIdx;
        bool isLastLine = !line[_charIdx..].TrimStart().StartsWith("^^");

        if (!isLastLine)
        {
            _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx) + 2;
            appendStart = _charIdx;

            if (line[_charIdx..].TrimEnd().EndsWith("^^"))
            {
                line = line.TrimEnd()[..^2];
                isLastLine = true;
            }
        }

        _chart?.AppendChart(_state, $"Line: {line.GetSnippet(_charIdx)}");

        while (_charIdx < line.Length)
        {
            if (line[_charIdx] == '[')
                ValidateExpressionBlock(ExprContext.Line);

            _charIdx++;
        }

        if (_sw != null)
        {
            if (isLastLine)
                WriteTranslation(_sw, line[appendStart..]);
            else
                _sb.Append(line[appendStart..]);
        }

        if (isLastLine)
            return;

        while (_state.LineIdx < _state.Script.Count && !isLastLine)
        {
            MoveNextLine();
            _lineVisited = true;
            int prevLineLength = line.Length;
            line = _state.SpanLine;
            _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);
            appendStart = _charIdx;

            if (_state.CurrentIndentLevel <= originalIndent)
            {
                _errors?.AddError(_state.LineIdx - 1, 0, prevLineLength, "Multiline dialog must be indented.");
                _state.Dedent();
                return;
            }

            if (line.TrimEnd().EndsWith("^^"))
            {
                line = line.TrimEnd()[..^2];
                isLastLine = true;
            }

            while (_charIdx < line.Length)
            {
                if (line[_charIdx] == '[')
                    ValidateExpressionBlock(ExprContext.Line);

                _charIdx++;
            }

            _sb.Append(line[appendStart..]);
        }

        if (_sw != null)
        {
            WriteTranslation(_sw, _sb.ToString());
            _sb.Clear();
        }

        MoveNextLine();

        if (_state.CurrentIndentLevel <= originalIndent)
            _state.Dedent();

        void WriteTranslation(StreamWriter sw, ReadOnlySpan<char> text)
        {
            if (TranslationMode == TranslationFileType.CSV)
            {
                sw.WriteLine();
                sw.Write(_state.RowPrefix);
                sw.Write("_Line");
                sw.Write(_state.LineIdx);
                sw.Write(',');
                WriteEscapedCsvField(sw, text);
            }
            else
            {
                sw.WriteLine();
                sw.Write("msgctxt \"");
                sw.Write(_state.RowPrefix);
                sw.Write("_Line");
                sw.Write(_state.LineIdx);
                sw.WriteLine("\"");
                sw.Write("msgid \"");
                sw.Write(text);
                sw.WriteLine("\"");
                sw.WriteLine("msgstr \"\"");
            }
        }
    }

    private static bool TryGetTitleRange(ReadOnlySpan<char> span, [NotNullWhen(true)] out (int start, int length) result)
    {
        result = (-1, -1);

        if (!TitleRegex().IsMatch(span))
            return false;

        int i = DialogHelpers.GetNextNonWhitespace(span, 0) + 2;
        int start = i;
        i = DialogHelpers.GetNextNonIdentifier(span, i);
        result = (start, i - start);
        return true;
    }

    public bool TryGetVariant(ReadOnlySpan<char> key, out TextVariant value)
    {
        VarType varType = GetVariableType(key);

        value = varType switch
        {
            VarType.Float => new(0),
            VarType.String => new(string.Empty),
            VarType.Bool => new(false),
            _ => default
        };

        return varType != VarType.Undefined;
    }

    public VarType GetVariableType(ReadOnlySpan<char> varName)
    {
        var lookup = _predefinedVarDefs.GetAlternateLookup<ReadOnlySpan<char>>();

        if (lookup.TryGetValue(varName, out VarDef varDef))
            return varDef.Type;

        foreach (VarDef def in _localVarDefs)
        {
            if (def.Name.AsSpan().SequenceEqual(varName))
                return def.Type;
        }

        return VarType.Undefined;
    }

    public void SetValue(ReadOnlySpan<char> varName, TextVariant value)
    {
        VarDef varDef = new()
        {
            Name = varName.ToString(),
            Type = value.VariantType
        };
        _localVarDefs.Add(varDef);
    }

    public VarType GetMethodReturnType(ReadOnlySpan<char> methodName)
    {
        FuncDef? funcDef = GetMethodFuncDef(methodName);
        return funcDef?.ReturnType ?? VarType.Undefined;
    }

    public FuncDef? GetMethodFuncDef(ReadOnlySpan<char> methodName)
    {
        var lookup = _predefinedFuncDefs.GetAlternateLookup<ReadOnlySpan<char>>();

        if (lookup.TryGetValue(methodName, out FuncDef? funcDef))
            return funcDef;

        return null;
    }

    public TextVariant CallMethod(ReadOnlySpan<char> methodName, ReadOnlySpan<TextVariant> args)
    {
        FuncDef? funcDef = GetMethodFuncDef(methodName);

        if (funcDef == null)
            return TextVariant.Undefined;

        if (args.Length != funcDef.ArgTypes.Length)
            return TextVariant.Undefined;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].VariantType != funcDef.ArgTypes[i])
                return TextVariant.Undefined;
        }

        return funcDef.ReturnType switch
        {
            VarType.Float => new(0),
            VarType.String => new(string.Empty),
            VarType.Bool => new(false),
            _ => new()
        };
    }

    public TextVariant CallAsyncMethod(ReadOnlySpan<char> methodName, ReadOnlySpan<TextVariant> args)
    {
        var lookup = _predefinedFuncDefs.GetAlternateLookup<ReadOnlySpan<char>>();

        if (!lookup.TryGetValue(methodName, out FuncDef? funcDef) || !funcDef.Awaitable)
            return TextVariant.Undefined;

        if (args.Length != funcDef.ArgTypes.Length)
            return TextVariant.Undefined;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].VariantType != funcDef.ArgTypes[i])
                return TextVariant.Undefined;
        }

        return new();
    }

    private static readonly char[] s_escapeChars = ['"', ',', '\n', '\r'];

    static void WriteEscapedCsvField(StreamWriter sw, ReadOnlySpan<char> span)
    {
        if (span.IndexOfAny(s_escapeChars) < 0)
        {
            sw.Write(span);
            return;
        }

        sw.Write('"');

        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];

            if (c == '"')
            {
                sw.Write('"');
                sw.Write('"');
            }
            else
            {
                sw.Write(c);
            }
        }

        sw.Write('"');
    }
}

public enum ExprContext
{
    Block,
    Line,
    Choice
}

public class Error
{
    public Error(int line, int start, int end, string message)
    {
        Line = line;
        Start = start;
        End = end;
        Message = message;
    }

    public int Line { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public string Message { get; set; }
}

