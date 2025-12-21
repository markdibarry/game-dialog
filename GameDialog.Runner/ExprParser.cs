using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Runner;

public class ExprParser
{
    public ExprParser(ParserState state)
    {
        _state = state;
    }

    private readonly Stack<Token> _opStack = [];
    private readonly List<Token> _outputQueue = [];
    private readonly Stack<TextVariant> _evalStack = [];
    private readonly ParserState _state;
    private int _exprStart;
    private int _exprEnd;
    private int _lineIdx;

    public TextVariant Parse(int start, int end)
    {
        return Parse(_state.Line, _state.LineIdx, start, end, false);
    }

    public TextVariant Parse(ReadOnlySpan<char> line, int lineIdx, int start, int end)
    {
        return Parse(line, lineIdx, start, end, false);
    }

    public VarType GetVarType(int start, int end)
    {
        return Parse(_state.Line, _state.LineIdx, start, end, true).VariantType;
    }

    private TextVariant Parse(ReadOnlySpan<char> line, int lineIdx, int start, int end, bool typeOnly)
    {
        start = DialogHelpers.GetNextNonWhitespace(line, start);
        _exprStart = start;
        _exprEnd = end;
        _lineIdx = lineIdx;
        bool isAwait = false;

        if (CheckInitializers(line))
            return new();

        int i = start;

        while (i < end && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
            i++;

        ReadOnlySpan<char> firstToken = line[start..i];
        AssignKind assignKind = AssignKind.Undefined;

        if (firstToken.SequenceEqual("await") && i < end && char.IsWhiteSpace(line[i]))
        {
            start += "await ".Length;
            isAwait = true;
        }
        else
        {
            i = DialogHelpers.GetNextNonWhitespace(line, i);
            assignKind = GetAssignKind(line, i, end);

            if (assignKind == AssignKind.Assign)
                start = DialogHelpers.GetNextNonWhitespace(line, i + 1);
            else if (assignKind != AssignKind.Undefined)
                start = DialogHelpers.GetNextNonWhitespace(line, i + 2);
        }

        ParseToRpn(line, start);
        TextVariant result = EvaluateRpn(line, _state.MemberStorage, isAwait, typeOnly);

        if (!typeOnly && result.VariantType is not VarType.Void and not VarType.Undefined)
            TryAssign(firstToken, assignKind, result);

        Reset();
        return result;
    }

    public bool CheckInitializers(ReadOnlySpan<char> line)
    {
        if (line[_exprStart] != '@')
            return false;

        // Only handle if validating
        if (_state.Errors == null)
            return true;

        ReadOnlySpan<char> expr = line[_exprStart.._exprEnd];
        int lastChar = _exprStart + expr.TrimEnd().Length;
        VarType varType = VarType.Undefined;

        if (expr.StartsWith("@bool "))
            varType = VarType.Bool;
        else if (expr.StartsWith("@string "))
            varType = VarType.String;
        else if (expr.StartsWith("@float "))
            varType = VarType.Float;

        if (varType == VarType.Undefined)
        {
            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Unknown initialization type.");
            return true;
        }

        int i = DialogHelpers.GetNextNonIdentifier(line, _exprStart + 1);

        if (i == lastChar)
        {
            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "No valid variable provided.");
            return true;
        }

        while (i < lastChar)
        {
            int ws = DialogHelpers.GetNextNonWhitespace(line, i) - i;

            if (ws <= 0)
            {
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "No valid variable provided.");
                return true;
            }

            i += ws;
            int start = i;
            i = DialogHelpers.GetNextNonIdentifier(line, i);
            ReadOnlySpan<char> varName = line[start..i];

            if (varName.Length == 0
                || (!char.IsLetter(varName[0]) && varName[0] != '_')
                || (i != lastChar && !char.IsWhiteSpace(line[i])))
            {
                _state.Errors?.AddError(_lineIdx, start, _exprEnd, "Invalid variable name.");
                return true;
            }

            if (_state.MemberStorage.GetVariableType(varName) != VarType.Undefined)
            {
                _state.Errors?.AddError(_lineIdx, start, i, $"Cannot initialize existing variable '{varName}'.");
                return true;
            }
            else
            {
                if (varType == VarType.Bool)
                    _state.MemberStorage.SetVariable(varName, new(false));
                else if (varType == VarType.Float)
                    _state.MemberStorage.SetVariable(varName, new(0));
                else if (varType == VarType.String)
                    _state.MemberStorage.SetVariable(varName, new(string.Empty));
            }
        }

        return true;
    }

    private void TryAssign(ReadOnlySpan<char> varName, AssignKind assignKind, TextVariant result)
    {
        if (assignKind == AssignKind.Undefined)
            return;

        if (!char.IsLetter(varName[0]) && varName[0] != '_')
        {
            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Variables must start with a letter or underscore.");
            return;
        }

        VarType varType = _state.MemberStorage.GetVariableType(varName);

        if (assignKind == AssignKind.Assign)
        {
            if (varType == VarType.Undefined || varType == result.VariantType)
                _state.MemberStorage.SetVariable(varName, result);
            else
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Cannot assign {result.VariantType} to {varType} '{varName}'.");

            return;
        }

        if (varType == VarType.Undefined)
        {
            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Cannot modify uninitialized variable.");
            return;
        }

        if (assignKind == AssignKind.Add)
        {
            if (varType == VarType.String)
            {
                if (result.VariantType == VarType.String)
                {
                    _state.MemberStorage.TryGetVariable(varName, out TextVariant value);
                    _state.MemberStorage.SetVariable(varName, new(value.String + result.String));
                }
                else if (result.VariantType == VarType.Float)
                {
                    _state.MemberStorage.TryGetVariable(varName, out TextVariant value);
                    _state.MemberStorage.SetVariable(varName, new(value.String + result.Float));
                }
                else
                {
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                    return;
                }
            }
            else if (varType == VarType.Float)
            {
                if (result.VariantType == VarType.Float)
                {
                    _state.MemberStorage.TryGetVariable(varName, out TextVariant value);
                    _state.MemberStorage.SetVariable(varName, new(value.Float + result.Float));
                }
                else
                {
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                    return;
                }
            }
            else
            {
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                return;
            }
        }
        else if (assignKind == AssignKind.Sub)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Cannot subtract {result.VariantType} from {varType} '{varName}'.");
                return;
            }

            _state.MemberStorage.TryGetVariable(varName, out TextVariant value);
            _state.MemberStorage.SetVariable(varName, new(value.Float - result.Float));
        }
        else if (assignKind == AssignKind.Mult)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Cannot multiply {result.VariantType} with {varType} '{varName}'.");
                return;
            }

            _state.MemberStorage.TryGetVariable(varName, out TextVariant value);
            _state.MemberStorage.SetVariable(varName, new(value.Float * result.Float));
        }
        else if (assignKind == AssignKind.Div)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Cannot divide {result.VariantType} by {varType} '{varName}'.");
                return;
            }

            if (result.Float == 0)
                return;

            _state.MemberStorage.TryGetVariable(varName, out TextVariant value);
            _state.MemberStorage.SetVariable(varName, new(value.Float / result.Float));
        }
    }

    private static AssignKind GetAssignKind(ReadOnlySpan<char> line, int start, int end)
    {
        if (start >= end - 1)
            return AssignKind.Undefined;

        char c1 = line[start];
        char c2 = line[start + 1];

        if (c1 == '=' && c2 != '=')
            return AssignKind.Assign;

        if (c2 != '=')
            return AssignKind.Undefined;

        return c1 switch
        {
            '+' => AssignKind.Add,
            '-' => AssignKind.Sub,
            '*' => AssignKind.Mult,
            '/' => AssignKind.Div,
            _ => AssignKind.Undefined,
        };
    }

    public bool TryGetExprEndIdx(int start, [NotNullWhen(true)] out int end)
    {
        return TryGetExprEndIdx(_state.Line, _state.LineIdx, start, out end);
    }

    public bool TryGetExprEndIdx(ReadOnlySpan<char> line, int lineIdx, int start, [NotNullWhen(true)] out int end)
    {
        end = start;

        while (end < line.Length && line[end] != ']' && line[end - 1] != '\\')
            end++;

        if (end >= line.Length)
        {
            _state.Errors?.AddError(lineIdx, start, line.Length, "Unterminated expression block. Missing closing ']'");
            return false;
        }

        return true;
    }

    private void Reset()
    {
        _opStack.Clear();
        _outputQueue.Clear();
        _evalStack.Clear();
        _exprStart = 0;
        _exprEnd = 0;
        _lineIdx = 0;
    }

    private void ParseToRpn(ReadOnlySpan<char> line, int expStart)
    {
        int i = expStart;
        bool prevWasOperand = false;

        while (i < _exprEnd)
        {
            char c = line[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Number (start with digit or '.' followed by digit)
            if (char.IsDigit(c) || (c == '.' && i + 1 < _exprEnd && char.IsDigit(line[i + 1])))
            {
                int start = i;
                i++;

                while (i < _exprEnd)
                {
                    char c2 = line[i];

                    if (char.IsDigit(c2))
                    {
                        i++;
                        continue;
                    }

                    if (c2 == '.')
                    {
                        i++;
                        continue;
                    }

                    if (c2 == 'e' || c2 == 'E')
                    {
                        i++;

                        if (i < _exprEnd && (line[i] == '+' || line[i] == '-'))
                            i++;

                        while (i < _exprEnd && char.IsDigit(line[i]))
                            i++;

                        break;
                    }

                    break;
                }

                _outputQueue.Add(new(TokenType.Number, start, i));
                prevWasOperand = true;
                continue;
            }

            // Identifier or keyword (true/false) or function name
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                i++;

                while (i < _exprEnd && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                    i++;

                ReadOnlySpan<char> ident = line[start..i];

                if (ident.Length == 4 && ident.SequenceEqual("true"))
                {
                    _outputQueue.Add(new(TokenType.Bool, start, i));
                    prevWasOperand = true;
                    continue;
                }

                if (ident.Length == 5 && ident.SequenceEqual("false"))
                {
                    _outputQueue.Add(new(TokenType.Bool, start, i));
                    prevWasOperand = true;
                    continue;
                }

                // Look ahead to see if this is a function call (next non-space char is '(')
                int j = i;

                while (j < _exprEnd && char.IsWhiteSpace(line[j]))
                    j++;

                bool isCall = j < _exprEnd && line[j] == '(';

                if (isCall)
                {
                    // push the identifier onto the operator stack as a function marker
                    _opStack.Push(new(TokenType.Ident, start, i));
                    prevWasOperand = false;
                }
                else
                {
                    // variable
                    _outputQueue.Add(new(TokenType.Ident, start, i));
                    prevWasOperand = true;
                }

                continue;
            }

            // String literal
            if (c == '"')
            {
                int start = i + 1;
                i++;

                while (i < _exprEnd)
                {
                    if (line[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (line[i] == '"')
                        break;

                    i++;
                }

                if (i >= _exprEnd || line[i] != '"')
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Unterminated string literal");

                int end = i;
                i++; // skip closing quote

                _outputQueue.Add(new(TokenType.StringRef, start, end));
                prevWasOperand = true;
                continue;
            }

            // Multi-char operators
            if (i + 1 < _exprEnd)
            {
                char c2 = line[i + 1];

                if (c == '=' && c2 == '=')
                {
                    HandleOperatorToken(new(TokenType.Operator, i, i + 2, OpCode.Equal));
                    i += 2;
                    prevWasOperand = false;
                    continue;
                }

                if (c == '!' && c2 == '=')
                {
                    HandleOperatorToken(new(TokenType.Operator, i, i + 2, OpCode.NotEqual));
                    i += 2;
                    prevWasOperand = false;
                    continue;
                }

                if (c == '>' && c2 == '=')
                {
                    HandleOperatorToken(new(TokenType.Operator, i, i + 2, OpCode.GreaterEqual));
                    i += 2;
                    prevWasOperand = false;
                    continue;
                }

                if (c == '<' && c2 == '=')
                {
                    HandleOperatorToken(new(TokenType.Operator, i, i + 2, OpCode.LessEqual));
                    i += 2;
                    prevWasOperand = false;
                    continue;
                }

                if (c == '&' && c2 == '&')
                {
                    HandleOperatorToken(new(TokenType.Operator, i, i + 2, OpCode.And));
                    i += 2;
                    prevWasOperand = false;
                    continue;
                }

                if (c == '|' && c2 == '|')
                {
                    HandleOperatorToken(new(TokenType.Operator, i, i + 2, OpCode.Or));
                    i += 2;
                    prevWasOperand = false;
                    continue;
                }
            }

            // Single-char operators
            switch (c)
            {
                case '+':
                    OpCode addCode = prevWasOperand ? OpCode.Add : OpCode.UPlus;
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, addCode));
                    i++;
                    prevWasOperand = false;
                    break;
                case '-':
                    OpCode subCode = prevWasOperand ? OpCode.Sub : OpCode.UMinus;
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, subCode));
                    i++;
                    prevWasOperand = false;
                    break;
                case '*':
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, OpCode.Mul));
                    i++;
                    prevWasOperand = false;
                    break;
                case '/':
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, OpCode.Div));
                    i++;
                    prevWasOperand = false;
                    break;
                case '>':
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, OpCode.Greater));
                    i++;
                    prevWasOperand = false;
                    break;
                case '<':
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, OpCode.Less));
                    i++;
                    prevWasOperand = false;
                    break;
                case '!': // Not
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, OpCode.Not));
                    i++;
                    prevWasOperand = false;
                    break;
                case '(':
                    _opStack.Push(new(TokenType.LeftParen, i, i + 1));
                    i++;
                    prevWasOperand = false;
                    break;
                case ')':
                    // pop until left paren
                    while (_opStack.Count > 0 && _opStack.Peek().Type != TokenType.LeftParen)
                        _outputQueue.Add(_opStack.Pop());

                    if (_opStack.Count == 0)
                    {
                        _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Mismatched parentheses");
                        return;
                    }

                    // pop left paren
                    _opStack.Pop();

                    // if top of op stack is an ident (function), pop it to output as Function token
                    if (_opStack.Count > 0 && _opStack.Peek().Type == TokenType.Ident)
                    {
                        Token funcIdent = _opStack.Pop();
                        _outputQueue.Add(new(TokenType.Function, funcIdent.Start, funcIdent.End));
                    }

                    i++;
                    prevWasOperand = true;
                    break;
                case ',':
                    // pop operators until left paren
                    while (_opStack.Count > 0 && _opStack.Peek().Type != TokenType.LeftParen)
                        _outputQueue.Add(_opStack.Pop());

                    if (_opStack.Count == 0)
                        _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Comma outside of parentheses");

                    i++;
                    prevWasOperand = false;
                    break;
                default:
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Unexpected character '{c}' at position {i}");
                    return;
            }
        }

        // End of input: pop remaining operators to output
        while (_opStack.Count > 0)
        {
            Token top = _opStack.Pop();

            if (top.Type == TokenType.LeftParen || top.Type == TokenType.RightParen)
            {
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Mismatched parentheses");
                return;
            }

            _outputQueue.Add(top);
        }
    }

    private TextVariant EvaluateRpn(ReadOnlySpan<char> line, IMemberStorage memberStorage, bool isAwait, bool typeOnly)
    {
        foreach (Token token in _outputQueue)
        {
            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.Bool:
                case TokenType.StringRef:
                    TextVariant lit = ParseLiteral(line, token);
                    _evalStack.Push(lit);
                    break;
                case TokenType.Ident:
                    {
                        ReadOnlySpan<char> name = line[token.Start..token.End];

                        if (typeOnly)
                        {
                            _evalStack.Push(memberStorage.GetVariableType(name) switch
                            {
                                VarType.Float => new(0),
                                VarType.String => new(string.Empty),
                                VarType.Bool => new(false),
                                _ => new()
                            });
                            break;
                        }

                        if (!memberStorage.TryGetVariable(name, out TextVariant varValue))
                        {
                            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Undefined variable '{name}'.");
                            return new();
                        }

                        _evalStack.Push(varValue);
                        break;
                    }
                case TokenType.Function:
                    {
                        ReadOnlySpan<char> name = line[token.Start..token.End];
                        VarType funcType = memberStorage.GetMethodReturnType(name);
                        TextVariant funcValue;

                        if (funcType == VarType.Undefined)
                        {
                            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Undefined method '{name}'.");
                            return new();
                        }

                        if (typeOnly)
                        {
                            funcValue = funcType switch
                            {
                                VarType.Float => new(0),
                                VarType.String => new(string.Empty),
                                VarType.Bool => new(false),
                                _ => new()
                            };
                            _evalStack.Push(funcValue);
                            break;
                        }

                        int argCount = _evalStack.Count;
                        TextVariant[] argsArray = ArrayPool<TextVariant>.Shared.Rent(argCount);
                        Span<TextVariant> args = argsArray.AsSpan()[..argCount];

                        for (int i = argCount - 1; i >= 0; i--)
                            args[i] = _evalStack.Pop();

                        if (isAwait)
                            funcValue = memberStorage.CallAsyncMethod(name, args);
                        else
                            funcValue = memberStorage.CallMethod(name, args);

                        ArrayPool<TextVariant>.Shared.Return(argsArray);

                        if (funcValue.VariantType == VarType.Undefined)
                        {
                            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Undefined method or incorrect arguments for '{name}'.");
                            return new();
                        }

                        _evalStack.Push(funcValue);
                        break;
                    }
                case TokenType.Operator:
                    HandleOperator(token, _evalStack);
                    break;
                default:
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Unhandled token type '{token.Type}'.");
                    return new();
            }
        }

        if (_evalStack.Count != 1)
        {
            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Invalid expression.");
            return new();
        }

        return _evalStack.Pop();
    }

    private void HandleOperatorToken(Token opToken)
    {
        OpCode curOp = opToken.Op;
        (int Prec, bool RightAssoc) curInfo = OpInfo[(int)curOp];

        while (_opStack.Count > 0 && _opStack.Peek().Type == TokenType.Operator)
        {
            Token top = _opStack.Peek();
            (int Prec, bool RightAssoc) topInfo = OpInfo[(int)top.Op];
            bool shouldPop = false;

            if (topInfo.Prec > curInfo.Prec)
                shouldPop = true;
            else if (topInfo.Prec == curInfo.Prec && !curInfo.RightAssoc)
                shouldPop = true;

            if (shouldPop)
            {
                _outputQueue.Add(_opStack.Pop());
                continue;
            }

            break;
        }

        _opStack.Push(opToken);
    }

    private TextVariant ParseLiteral(ReadOnlySpan<char> line, Token token)
    {
        ReadOnlySpan<char> span = line[token.Start..token.End];

        if (token.Type == TokenType.Number)
        {
            if (float.TryParse(span, out float f))
                return new(f);

            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Invalid number literal '{span}'.");
            return new();
        }

        if (token.Type == TokenType.Bool)
        {
            if (span.SequenceEqual("true"))
                return new(true);

            if (span.SequenceEqual("false"))
                return new(false);

            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Invalid bool literal '{span}'.");
            return new();
        }

        if (token.Type == TokenType.StringRef)
            return new(span.ToString());

        _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Invalid token '{span}'.");
        return new();
    }

    private void HandleOperator(Token token, Stack<TextVariant> stack)
    {
        switch (token.Op)
        {
            case OpCode.UPlus:
                if (stack.Count < 1 || stack.Peek().VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Unary '+' requires numeric operand.");
                else
                    stack.Push(new TextVariant(stack.Pop().Float));

                break;
            case OpCode.UMinus:
                if (stack.Count < 1 || stack.Peek().VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Unary '-' requires numeric operand.");
                else
                    stack.Push(new TextVariant(-stack.Pop().Float));

                break;
            case OpCode.Not:
                if (stack.Count < 1 || stack.Peek().VariantType != VarType.Bool)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Logical 'not' requires boolean operand.");
                else
                    stack.Push(new TextVariant(!stack.Pop().Bool));

                break;
            default:
                HandleBinaryOperator(token, stack);
                break;
        }
    }

    private void HandleBinaryOperator(Token token, Stack<TextVariant> stack)
    {
        if (stack.Count < 2)
        {
            _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Operator '{token.Op}' requires two operands.");
            return;
        }

        TextVariant right = stack.Pop();
        TextVariant left = stack.Pop();

        switch (token.Op)
        {
            case OpCode.Add:
                if (left.VariantType == VarType.String || right.VariantType == VarType.String)
                {
                    string sLeft;
                    string sRight;

                    if (left.VariantType == VarType.String)
                        sLeft = left.String;
                    else if (left.VariantType == VarType.Float)
                        sLeft = left.Float.ToString();
                    else
                        sLeft = string.Empty;

                    if (right.VariantType == VarType.String)
                        sRight = right.String;
                    else if (right.VariantType == VarType.Float)
                        sRight = right.Float.ToString();
                    else
                        sRight = string.Empty;

                    stack.Push(new(sLeft + sRight));
                }
                else
                {
                    if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                        _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Operator '{token.Op}' requires numeric operands.");
                    else
                        stack.Push(new(left.Float + right.Float));
                }

                break;
            case OpCode.Sub:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Operator '{token.Op}' requires numeric operands.");
                else
                    stack.Push(new(left.Float - right.Float));

                break;
            case OpCode.Mul:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Operator '{token.Op}' requires numeric operands.");
                else
                    stack.Push(new(left.Float * right.Float));

                break;
            case OpCode.Div:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Operator '{token.Op}' requires numeric operands.");
                else
                    stack.Push(new(left.Float / right.Float));

                break;
            case OpCode.Equal:
                if (left.VariantType == VarType.Float && right.VariantType == VarType.Float)
                    stack.Push(new(left.Float == right.Float));
                else if (left.VariantType == VarType.Bool && right.VariantType == VarType.Bool)
                    stack.Push(new(left.Bool == right.Bool));
                else if (left.VariantType == VarType.String && right.VariantType == VarType.String)
                    stack.Push(new(left.String == right.String));
                else
                    stack.Push(new(false));

                break;
            case OpCode.NotEqual:
                if (left.VariantType == VarType.Float && right.VariantType == VarType.Float)
                    stack.Push(new(left.Float != right.Float));
                else if (left.VariantType == VarType.Bool && right.VariantType == VarType.Bool)
                    stack.Push(new(left.Bool != right.Bool));
                else if (left.VariantType == VarType.String && right.VariantType == VarType.String)
                    stack.Push(new(left.String != right.String));
                else
                    stack.Push(new(true));

                break;
            case OpCode.Greater:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Operator '>' requires numeric operands.");
                else
                    stack.Push(new(left.Float > right.Float));

                break;
            case OpCode.Less:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Operator '<' requires numeric operands.");
                else
                    stack.Push(new(left.Float < right.Float));

                break;
            case OpCode.GreaterEqual:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Operator '>=' requires numeric operands.");
                else
                    stack.Push(new(left.Float >= right.Float));

                break;
            case OpCode.LessEqual:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Operator '<=' requires numeric operands.");
                else
                    stack.Push(new(left.Float <= right.Float));

                break;
            case OpCode.And:
                if (left.VariantType != VarType.Bool || right.VariantType != VarType.Bool)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Logical 'and' requires boolean operands.");
                else
                    stack.Push(new(left.Bool && right.Bool));

                break;
            case OpCode.Or:
                if (left.VariantType != VarType.Bool || right.VariantType != VarType.Bool)
                    _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, "Logical 'or' requires boolean operands.");
                else
                    stack.Push(new(left.Bool || right.Bool));

                break;
            default:
                _state.Errors?.AddError(_lineIdx, _exprStart, _exprEnd, $"Unhandled operator '{token.Op}'.");
                return;
        }
    }

    private enum TokenType : byte
    {
        None = 0,
        Number,
        Bool,
        StringRef,
        Ident,
        Operator,
        LeftParen,
        RightParen,
        Comma,
        Function
    }

    private enum OpCode : byte
    {
        None = 0,
        Add,
        Sub,
        Mul,
        Div,
        Equal,
        NotEqual,
        Greater,
        Less,
        GreaterEqual,
        LessEqual,
        And,
        Or,
        UPlus,
        UMinus,
        Not
    }

    private static readonly (int Prec, bool RightAssoc)[] OpInfo =
    [
        (0, false), // None
        (7, false), // Add
        (7, false), // Sub
        (8, false), // Mul
        (8, false), // Div
        (5, false), // Equal
        (5, false), // NotEqual
        (6, false), // Greater
        (6, false), // Less
        (6, false), // GreaterEqual
        (6, false), // LessEqual
        (4, false), // And
        (3, false), // Or
        (9, true),  // UPlus
        (9, true),  // UMinus
        (9, true)   // Not
    ];

    private enum AssignKind
    {
        Undefined,
        Assign,
        Add,
        Sub,
        Mult,
        Div
    }

    private enum Assoc : byte
    {
        Left = 0,
        Right = 1
    }

    private readonly struct Token
    {
        public Token(TokenType type, int start, int end, OpCode op = OpCode.None)
        {
            Type = type;
            Start = start;
            End = end;
            Op = op;
        }

        public readonly TokenType Type;
        public readonly OpCode Op;
        /// <summary>
        /// Inclusive Start
        /// </summary>
        public readonly int Start;
        /// <summary>
        /// Exclusive End
        /// </summary>
        public readonly int End;
    }
}

