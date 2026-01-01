using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using GameDialog.Pooling;

namespace GameDialog.Runner;

public partial class DialogValidator : IMemberStorage
{
    public DialogValidator(
        ParserState state,
        List<VarDef> predefinedVarDefs,
        List<FuncDef> predefinedFuncDefs)
    {
        _state = state;
        _predefinedVarDefs = predefinedVarDefs;
        _predefinedFuncDefs = predefinedFuncDefs;
    }

    private readonly List<ReadOnlyMemory<char>> _sectionTitles = [];
    private readonly ParserState _state;
    private readonly List<VarDef> _predefinedVarDefs;
    private readonly List<FuncDef> _predefinedFuncDefs;
    private readonly List<VarDef> _localVarDefs = [];
    private List<Error>? _errors;
    private int _charIdx;
    private bool _lineVisited;
    private bool _exitSection;
    public StringBuilder? _chart;

    [GeneratedRegex(@"^\s*--\w+--\s*$")]
    private static partial Regex TitleRegex();
    [GeneratedRegex(@"^\s*\w+(?:,\s*\w+)*:\s")]
    private static partial Regex SpeakerRegex();
    [GeneratedRegex(@"^\s*if\s*\[")]
    private static partial Regex IfRegex();
    [GeneratedRegex(@"^\s*else if\s*\[")]
    private static partial Regex ElseIfRegex();

    public void ValidateScript(string text, List<Error> errors, StringBuilder? chart = null)
    {
        ParserState.ReadStringToList(text, _state.Script);
        ValidateScript(errors, chart);
    }

    public void ValidateScript(List<Error> errors, StringBuilder? chart = null)
    {
        if (_state.Script.Count == 0)
        {
            errors?.AddError(0, 0, 0, "Script is empty");
            return;
        }

        _errors = errors;
        _chart = chart;

        try
        {
            ValidateAllTitles();
            ValidateSections();
        }
        finally
        {
            Reset();
        }
    }

    private void Reset()
    {
        foreach (var def in _localVarDefs)
            def.ReturnToPool();

        _localVarDefs.Clear();
        _state.Reset();
        _sectionTitles.Clear();
        _errors = null;
        _chart = null;
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
            ReadOnlySpan<char> span = _state.Line;

            if (TryGetTitleRange(span, out (int Start, int Length) range))
            {
                ReadOnlyMemory<char> mem = _state.Script[_state.LineIdx];
                mem = mem.Slice(range.Start, range.Length);

                if (_sectionTitles.ContainsSequence(mem.Span))
                    _errors?.AddError(_state.LineIdx, 0, span.Length, $"Duplicate section title '{mem.Span}'");
                else
                    _sectionTitles.Add(mem);
            }

            MoveNextLine();
        }

        if (_sectionTitles.Count == 0)
            _errors?.AddError(0, 0, _state.Script[0].Length - 1, "No section titles found. Ensure your script has at least one title using letters, numbers or underscores.");
    }

    private void ValidateSections()
    {
        _lineVisited = false;
        _exitSection = false;
        _charIdx = 0;
        _state.MoveLine(0, _errors);

        while (_state.LineIdx < _state.Script.Count)
        {
            ReadOnlySpan<char> span = _state.Line;

            if (!TitleRegex().IsMatch(span))
                _errors?.AddError(_state.LineIdx, 0, _state.Script[0].Length - 1, "Section must start with a title.");

            if (_chart != null)
            {
                if (_state.LineIdx > 0)
                    _chart.AppendLine();

                if (TryGetTitleRange(span, out (int Start, int Length) range))
                    _chart.AppendChart(_state, $"Title: {span.Slice(range.Start, range.Length)}");
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
        ReadOnlySpan<char> line = _state.Line;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);
        ReadOnlySpan<char> trimmed = line[_charIdx..];

        if (TitleRegex().IsMatch(_state.Line))
        {
            _lineVisited = true;
            _exitSection = true;
            return;
        }

        if (trimmed.StartsWith('['))
        {
            ValidateExpressionBlock(ExprContext.Block);
        }
        else if (trimmed.StartsWith('?'))
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
                _errors?.AddError(_state.LineIdx, 0, _state.Line.Length - 1, "No matching if block.");
            else
                _errors?.AddError(_state.LineIdx, 0, _state.Line.Length - 1, "Invalid block.");
        }
    }

    private void ValidateExpressionBlock(ExprContext exprContext)
    {
        ReadOnlySpan<char> line = _state.Line;
        _charIdx++;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);
        _lineVisited = true;

        if (line[_charIdx] == '#')
            ValidateHashExpression(exprContext);
        else
            ValidateExpression(exprContext);
    }

    private void ValidateExpression(ExprContext exprContext)
    {
        if (!ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, _charIdx, _errors, out ExprInfo exprInfo))
        {
            _charIdx = _state.Line.Length;
            return;
        }

        ReadOnlySpan<char> line = _state.Line;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

        if (exprContext == ExprContext.Block)
            _chart?.AppendChart(_state, $"Expression: {line[_charIdx..exprInfo.End]}");

        if (_charIdx == exprInfo.End)
            return;

        bool isClosingTag = false;

        if (line[_charIdx] == '/')
        {
            isClosingTag = true;
            _charIdx++;
        }

        int start = _charIdx;
        int end = DialogHelpers.GetNextNonIdentifier(line, _charIdx);
        ReadOnlySpan<char> firstToken = line[start..end];

        if (BBCode.IsSupportedTag(firstToken))
        {
            if (exprContext != ExprContext.Line)
                _errors?.AddError(_state.LineIdx, _charIdx, exprInfo.End, "BBCode tags can only be used in a dialog line.");

            _charIdx = exprInfo.End;
            return;
        }

        if (BuiltIn.IsSupportedTag(firstToken))
        {
            if (exprContext == ExprContext.Choice)
            {
                _errors?.AddError(_state.LineIdx, _charIdx, exprInfo.End, "Built in tags cannot be used in a choice.");
                return;
            }

            start = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], end);
            ReadOnlySpan<char> restOfExpr = line[start..exprInfo.End];
            _charIdx = start;
            ValidateBuiltInTag(firstToken, restOfExpr, isClosingTag, exprContext);
            _charIdx = exprInfo.End;
            return;
        }

        exprInfo = exprInfo with { Start = _charIdx };
        ExprParser.Parse(exprInfo, this, _errors);
    }

    private void ValidateBuiltInTag(ReadOnlySpan<char> tagName, ReadOnlySpan<char> restOfExpr, bool isClosingTag, ExprContext exprContext)
    {
        int start = _charIdx;
        int end = _charIdx + restOfExpr.Length;
        bool isSingleToken = restOfExpr.IsWhiteSpace();
        bool isAssignment = restOfExpr.Length >= 2 && restOfExpr[0] == '=' && restOfExpr[1] != '=';

        if (isAssignment)
        {
            int i = 1;

            while (i < restOfExpr.Length && char.IsWhiteSpace(restOfExpr[i]))
                i++;

            start += i;
            restOfExpr = restOfExpr[i..];
        }

        if (tagName.SequenceEqual(BuiltIn.AUTO))
        {
            if (isAssignment)
            {
                ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, end);
                TextVariant result = ExprParser.Parse(exprInfo, this, _errors);

                if (isClosingTag)
                    _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' can only be assigned to or be part of a closing tag.");
                else if (result.VariantType != VarType.Float)
                    _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' must have a float value assigned.");
                else if (result.Float < 0)
                    _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot be less than 0.");
            }
            else if (!isSingleToken)
            {
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot have additional parameters.");
            }
        }
        else if (tagName.SequenceEqual(BuiltIn.END))
        {
            if (exprContext == ExprContext.Line)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot be used in a dialog line.");
            else if (isClosingTag)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot be a closing tag.");
            else if (!isSingleToken)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot have additional parameters.");
        }
        else if (tagName.SequenceEqual(BuiltIn.GOTO))
        {
            if (exprContext == ExprContext.Line)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot be used in a dialog line.");
            else if (isClosingTag)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot be a closing tag.");
            else if (isSingleToken)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' requires a section id.");
            else if (isAssignment)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' does not support assignment.");
            else if (!_sectionTitles.ContainsSequence(restOfExpr.Trim()))
                _errors?.AddError(_state.LineIdx, start, end, $"Section '{restOfExpr.Trim()}' does not exist.");
        }
        else if (tagName.SequenceEqual(BuiltIn.PAUSE))
        {
            ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, end);

            if (exprContext != ExprContext.Line)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' can only be used in a dialog line.");
            else if (!isAssignment)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' must have a float value assigned.");
            else if (isClosingTag)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot be a closing tag.");
            else if (ExprParser.Parse(exprInfo, this, _errors).VariantType != VarType.Float)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' must have a float value assigned.");
        }
        else if (tagName.SequenceEqual(BuiltIn.SPEED))
        {
            // TODO: Works in both dialog and standalone?
            if (isAssignment)
            {
                if (isClosingTag)
                    _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' can only be assigned to or be part of a closing tag.");

                ExprInfo exprInfo = new(_state.Line, _state.LineIdx, start, end);
                TextVariant result = ExprParser.Parse(exprInfo, this, _errors);

                if (result.VariantType != VarType.Float || result.Float < 0)
                    _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' must have a positive float value assigned.");
            }
            else if (!isClosingTag || !isSingleToken)
            {
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' can only be assigned to or be part of a closing tag.");
            }
        }
        else if (tagName.SequenceEqual(BuiltIn.SCROLL) || tagName.SequenceEqual(BuiltIn.PROMPT))
        {
            if (exprContext != ExprContext.Line)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' can only be used in a dialog line.");
            else if (isClosingTag)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot be a closing tag.");
            else if (!isSingleToken)
                _errors?.AddError(_state.LineIdx, start, end, $"Built-in tag '{tagName}' cannot have additional parameters.");
        }
    }

    private void ValidateHashExpression(ExprContext exprContext)
    {
        ReadOnlySpan<char> line = _state.Line;
        int end = _charIdx + 1;

        if (!ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, _charIdx + 1, _errors, out ExprInfo exprInfo))
        {
            _charIdx = _state.Line.Length;
            return;
        }

        if (exprContext == ExprContext.Choice)
        {
            _errors?.AddError(_state.LineIdx, _charIdx, exprInfo.End, "Hash expressions cannot be used in choices.");
            return;
        }

        if (exprContext != ExprContext.Line)
            _chart?.AppendChart(_state, $"Hash Expression: {line[_charIdx..exprInfo.End]}");

        while (end < exprInfo.End)
        {
            int start = end;
            end = DialogHelpers.GetNextNonIdentifier(line, end);

            if (end == start)
            {
                _errors?.AddError(_state.LineIdx, start - 1, end, "Invalid Hash Tag.");
                _charIdx = exprInfo.End;
                return;
            }

            int wsStart = end;
            end = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], end);

            if (end < exprInfo.End && line[end] != '=' && (line[end] != '#' || end - wsStart == 0))
            {
                _errors?.AddError(_state.LineIdx, start - 1, end, "Invalid Hash Tag.");
                _charIdx = exprInfo.End;
                return;
            }

            if (line[end] != '=')
            {
                end++;
                continue;
            }

            end++;
            int rightStart = end;
            bool inQuote = false;

            while (end < exprInfo.End)
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

            exprInfo = new(_state.Line, _state.LineIdx, rightStart, end);

            if (ExprParser.Parse(exprInfo, this, _errors).VariantType == VarType.Undefined)
            {
                _errors?.AddError(_state.LineIdx, rightStart, end, "Invalid Hash Tag assignment.");
                _charIdx = exprInfo.End;
                return;
            }

            end++;
        }
    }

    private void ValidateConditionBlock()
    {
        _lineVisited = true;
        _charIdx += "if".Length;
        IsValidConditionExpr();
        _chart?.AppendChart(_state, "If");
        MoveNextLine();

        if (_state.IndentChange <= 0)
        {
            _errors?.AddError(_state.LineIdx, 0, _state.Line.Length - 1, "Condition block must be indented.");
            return;
        }

        ValidateBlocks();

        if (_state.Dedents > 0)
            return;

        while (ElseIfRegex().IsMatch(_state.Line))
        {
            _lineVisited = true;
            _charIdx = DialogHelpers.GetNextNonWhitespace(_state.Line, _charIdx);
            _charIdx += "else if".Length;
            IsValidConditionExpr();
            _chart?.AppendChart(_state, "Else If");
            MoveNextLine();

            if (_state.IndentChange <= 0)
            {
                _errors?.AddError(_state.LineIdx, 0, _state.Line.Length - 1, "Condition block must be indented.");
                return;
            }

            ValidateBlocks();

            if (_state.Dedents > 0)
                return;
        }

        _charIdx = DialogHelpers.GetNextNonWhitespace(_state.Line, _charIdx);

        if (_state.Line[_charIdx..].StartsWith("else"))
        {
            _lineVisited = true;
            _charIdx += "else".Length;

            if (!_state.Line[_charIdx..].IsWhiteSpace())
            {
                _errors?.AddError(_state.LineIdx, 0, _charIdx, "Condition is invalid.");
                return;
            }

            _chart?.AppendChart(_state, "Else");
            MoveNextLine();

            if (_state.IndentChange <= 0)
            {
                _errors?.AddError(_state.LineIdx, 0, _state.Line.Length - 1, "Condition block must be indented.");
                return;
            }

            ValidateBlocks();
        }
    }

    private bool IsValidConditionExpr()
    {
        ReadOnlySpan<char> line = _state.Line;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

        if (_charIdx >= line.Length || line[_charIdx] != '[')
        {
            _errors?.AddError(_state.LineIdx, 0, line.Length - 1, "Condition is invalid.");
            return false;
        }

        _charIdx++;

        if (!ExprParser.TryGetExprInfo(_state.Line, _state.LineIdx, _charIdx, _errors, out ExprInfo exprInfo))
        {
            _charIdx = _state.Line.Length;
            return false;
        }

        TextVariant condResult = ExprParser.Parse(exprInfo, this, _errors);

        if (condResult.VariantType != VarType.Bool)
        {
            _errors?.AddError(_state.LineIdx, 0, _charIdx, "Condition is invalid.");
            return false;
        }

        return true;
    }

    private void ValidateChoiceBlock()
    {
        ReadOnlySpan<char> line = _state.Line;

        while (line[_charIdx..].StartsWith('?'))
        {
            _lineVisited = true;
            _charIdx++;
            _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

            if (IfRegex().IsMatch(line[_charIdx..]))
            {
                _charIdx += "if".Length;
                IsValidConditionExpr();
            }

            _chart?.AppendChart(_state, $"Choice: {line.GetSnippet(_charIdx)}");

            while (_charIdx < line.Length)
            {
                if (line[_charIdx] == '[' && line[_charIdx - 1] != '\\')
                    ValidateExpressionBlock(ExprContext.Choice);

                _charIdx++;
            }

            MoveNextLine();

            if (_state.IndentChange <= 0)
            {
                line = _state.Line;
                continue;
            }

            ValidateBlocks();

            if (_state.Dedents > 0)
                return;

            line = _state.Line;
        }
    }

    private void ValidateLineBlock()
    {
        _lineVisited = true;
        ReadOnlySpan<char> line = _state.Line;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);

        while (_charIdx < line.Length && line[_charIdx] != ':')
            _charIdx++;

        //ReadOnlySpan<char> speakerId = line[start..(_charIdx - 1)];
        _charIdx++;
        _charIdx = DialogHelpers.GetNextNonWhitespace(line, _charIdx);
        int originalIndent = _state.CurrentIndentLevel;
        bool isMultiline = false;

        if (line[_charIdx..].StartsWith("^^"))
        {
            isMultiline = true;
            _charIdx += 2;
        }

        _chart?.AppendChart(_state, $"Line: {line.GetSnippet(_charIdx)}");

        while (_charIdx < line.Length)
        {
            if (line[_charIdx] == '[' && line[_charIdx - 1] != '\\')
                ValidateExpressionBlock(ExprContext.Line);

            _charIdx++;
        }

        if (!isMultiline || line.Trim().EndsWith("^^"))
            return;

        MoveNextLine();
        _lineVisited = true;
        line = _state.Line;

        if (_state.IndentChange <= 0)
        {
            _errors?.AddError(_state.LineIdx, 0, _state.Line.Length - 1, "Multiline dialog must be indented.");
            return;
        }

        while (_state.LineIdx < _state.Script.Count)
        {
            _lineVisited = true;

            while (_charIdx < line.Length)
            {
                if (line[_charIdx] == '[' && (_charIdx == 0 || line[_charIdx - 1] != '\\'))
                    ValidateExpressionBlock(ExprContext.Line);

                _charIdx++;
            }

            MoveNextLine();

            if (line.Trim().EndsWith("^^"))
            {
                if (_state.CurrentIndentLevel <= originalIndent)
                    _state.Dedent();

                return;
            }

            line = _state.Line;
        }
    }

    public static bool IsTitleLine(ReadOnlySpan<char> line)
    {
        line = line.StripLineComment();
        return TitleRegex().IsMatch(line);
    }

    public static bool TryGetTitleRange(ReadOnlySpan<char> span, [NotNullWhen(true)] out (int start, int length) result)
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

    public bool TryGetVariable(ReadOnlySpan<char> key, out TextVariant value)
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
        foreach (VarDef varDef in _localVarDefs)
        {
            if (varDef.Name.AsSpan().SequenceEqual(varName))
                return varDef.Type;
        }

        foreach (VarDef varDef in _predefinedVarDefs)
        {
            if (varDef.Name.AsSpan().SequenceEqual(varName))
                return varDef.Type;
        }

        return VarType.Undefined;
    }

    public void SetVariable(ReadOnlySpan<char> varName, TextVariant value)
    {
        VarDef varDef = Pool.Get<VarDef>();
        varDef.Name = varName.ToString();
        varDef.Type = value.VariantType;
        _localVarDefs.Add(varDef);
    }

    public VarType GetMethodReturnType(ReadOnlySpan<char> methodName)
    {
        foreach (FuncDef funcDef in _predefinedFuncDefs)
        {
            if (funcDef.Name.AsSpan().SequenceEqual(methodName))
                return funcDef.ReturnType;
        }

        return VarType.Undefined;
    }

    public TextVariant CallMethod(ReadOnlySpan<char> methodName, ReadOnlySpan<TextVariant> args)
    {
        FuncDef? funcDef = null;

        foreach (FuncDef predFuncDef in _predefinedFuncDefs)
        {
            if (predFuncDef.Name.AsSpan().SequenceEqual(methodName))
            {
                funcDef = predFuncDef;
                break;
            }
        }

        if (funcDef == null)
            return TextVariant.Undefined;

        for (int i = 0; i < args.Length; i++)
        {
            if (i >= funcDef.ArgTypes.Count || args[i].VariantType != funcDef.ArgTypes[i])
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
        FuncDef? funcDef = null;

        foreach (FuncDef predFuncDef in _predefinedFuncDefs)
        {
            if (predFuncDef.Name.AsSpan().SequenceEqual(methodName))
                funcDef = predFuncDef;
        }

        if (funcDef == null || !funcDef.Awaitable)
            return TextVariant.Undefined;

        for (int i = 0; i < args.Length; i++)
        {
            if (i >= funcDef.ArgTypes.Count || args[i].VariantType != funcDef.ArgTypes[i])
                return TextVariant.Undefined;
        }

        return new();
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
