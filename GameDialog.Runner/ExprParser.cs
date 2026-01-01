using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace GameDialog.Runner;

public static class ExprParser
{
    public static TextVariant Parse(ExprInfo exprInfo, IMemberStorage memberStorage, List<Error>? errors = null)
    {
        return Parse(exprInfo, false, memberStorage, errors);
    }

    public static VarType GetVarType(ExprInfo exprInfo, IMemberStorage memberStorage, List<Error>? errors = null)
    {
        return Parse(exprInfo, true, memberStorage, errors).VariantType;
    }

    private static TextVariant Parse(ExprInfo exprInfo, bool typeOnly, IMemberStorage storage, List<Error>? errors)
    {
        ReadOnlySpan<char> line = exprInfo.Line;
        int start = exprInfo.Start;
        int end = exprInfo.End;
        start = DialogHelpers.GetNextNonWhitespace(line, start);

        if (CheckInitializers(exprInfo, storage, errors))
            return new();

        bool isAwait = false;
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

        TextVariant result = ParseExpression(ref start, exprInfo, isAwait, typeOnly, storage, errors);

        if (!typeOnly && result.VariantType is not VarType.Void and not VarType.Undefined)
            TryAssign(firstToken, assignKind, result, exprInfo, storage, errors);

        return result;
    }

    public static bool TryGetExprInfo(
        ReadOnlySpan<char> line,
        int lineIdx,
        int start,
        List<Error>? errors,
        [NotNullWhen(true)] out ExprInfo exprInfo)
    {
        int end = start;

        while (end < line.Length && line[end] != ']' && line[end - 1] != '\\')
            end++;

        if (end >= line.Length)
        {
            errors?.AddError(lineIdx, start, line.Length, "Unterminated expression block. Missing closing ']'");
            exprInfo = new(line, lineIdx, start, line.Length);
            return false;
        }

        exprInfo = new(line, lineIdx, start, end);
        return true;
    }

    private static bool CheckInitializers(ExprInfo exprInfo, IMemberStorage memberStorage, List<Error>? errors)
    {
        ReadOnlySpan<char> line = exprInfo.Line;

        if (line[exprInfo.Start] != '@')
            return false;

        // Only handle if validating
        if (errors == null)
            return true;

        ReadOnlySpan<char> expr = line[exprInfo.Start..exprInfo.End];
        int lastChar = exprInfo.Start + expr.TrimEnd().Length;
        VarType varType = VarType.Undefined;

        if (expr.StartsWith("@bool "))
            varType = VarType.Bool;
        else if (expr.StartsWith("@string "))
            varType = VarType.String;
        else if (expr.StartsWith("@float "))
            varType = VarType.Float;

        if (varType == VarType.Undefined)
        {
            errors?.AddError(exprInfo, "Unknown initialization type.");
            return true;
        }

        int i = DialogHelpers.GetNextNonIdentifier(line, exprInfo.Start + 1);

        if (i == lastChar)
        {
            errors?.AddError(exprInfo, "No valid variable provided.");
            return true;
        }

        while (i < lastChar)
        {
            int ws = DialogHelpers.GetNextNonWhitespace(line, i) - i;

            if (ws <= 0)
            {
                errors?.AddError(exprInfo, "No valid variable provided.");
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
                errors?.AddError(exprInfo.LineIdx, start, exprInfo.End, "Invalid variable name.");
                return true;
            }

            if (memberStorage.GetVariableType(varName) != VarType.Undefined)
            {
                errors?.AddError(exprInfo.LineIdx, start, i, $"Cannot initialize existing variable '{varName}'.");
                return true;
            }
            else
            {
                if (varType == VarType.Bool)
                    memberStorage.SetVariable(varName, new(false));
                else if (varType == VarType.Float)
                    memberStorage.SetVariable(varName, new(0));
                else if (varType == VarType.String)
                    memberStorage.SetVariable(varName, new(string.Empty));
            }
        }

        return true;
    }

    private static void TryAssign(
        ReadOnlySpan<char> varName,
        AssignKind assignKind,
        TextVariant result,
        ExprInfo exprInfo,
        IMemberStorage memberStorage,
        List<Error>? errors)
    {
        if (assignKind == AssignKind.Undefined)
            return;

        if (!char.IsLetter(varName[0]) && varName[0] != '_')
        {
            errors?.AddError(exprInfo, "Variables must start with a letter or underscore.");
            return;
        }

        VarType varType = memberStorage.GetVariableType(varName);

        if (assignKind == AssignKind.Assign)
        {
            if (varType == VarType.Undefined || varType == result.VariantType)
                memberStorage.SetVariable(varName, result);
            else
                errors?.AddError(exprInfo, $"Cannot assign {result.VariantType} to {varType} '{varName}'.");

            return;
        }

        if (varType == VarType.Undefined)
        {
            errors?.AddError(exprInfo, "Cannot modify uninitialized variable.");
            return;
        }

        if (assignKind == AssignKind.Add)
        {
            if (varType == VarType.String)
            {
                if (result.VariantType == VarType.String)
                {
                    memberStorage.TryGetVariable(varName, out TextVariant value);
                    memberStorage.SetVariable(varName, new(value.String + result.String));
                }
                else if (result.VariantType == VarType.Float)
                {
                    memberStorage.TryGetVariable(varName, out TextVariant value);
                    memberStorage.SetVariable(varName, new(value.String + result.Float));
                }
                else
                {
                    errors?.AddError(exprInfo, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                    return;
                }
            }
            else if (varType == VarType.Float)
            {
                if (result.VariantType == VarType.Float)
                {
                    memberStorage.TryGetVariable(varName, out TextVariant value);
                    memberStorage.SetVariable(varName, new(value.Float + result.Float));
                }
                else
                {
                    errors?.AddError(exprInfo, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                    return;
                }
            }
            else
            {
                errors?.AddError(exprInfo, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                return;
            }
        }
        else if (assignKind == AssignKind.Sub)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                errors?.AddError(exprInfo, $"Cannot subtract {result.VariantType} from {varType} '{varName}'.");
                return;
            }

            memberStorage.TryGetVariable(varName, out TextVariant value);
            memberStorage.SetVariable(varName, new(value.Float - result.Float));
        }
        else if (assignKind == AssignKind.Mult)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                errors?.AddError(exprInfo, $"Cannot multiply {result.VariantType} with {varType} '{varName}'.");
                return;
            }

            memberStorage.TryGetVariable(varName, out TextVariant value);
            memberStorage.SetVariable(varName, new(value.Float * result.Float));
        }
        else if (assignKind == AssignKind.Div)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                errors?.AddError(exprInfo, $"Cannot divide {result.VariantType} by {varType} '{varName}'.");
                return;
            }

            if (result.Float == 0)
                return;

            memberStorage.TryGetVariable(varName, out TextVariant value);
            memberStorage.SetVariable(varName, new(value.Float / result.Float));
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

    private static TextVariant ParseExpression(
        ref int pos,
        ExprInfo exprInfo,
        bool isAwait,
        bool typeOnly,
        IMemberStorage storage,
        List<Error>? errors,
        int minLbp = 0)
    {
        ReadOnlySpan<char> line = exprInfo.Line;
        pos = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);
        TextVariant left = ParsePrefix(ref pos, exprInfo, isAwait, typeOnly, storage, errors);

        while (true)
        {
            pos = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);

            if (pos >= exprInfo.End)
                break;

            ReadOnlySpan<char> op = PeekOperator(ref pos, exprInfo);

            if (op.IsEmpty)
                break;

            (int lbp, int rbp) = InfixBindingPower(op);

            if (lbp == 0 || lbp <= minLbp)
                break;

            pos += op.Length; // Consume operator
            TextVariant right = ParseExpression(ref pos, exprInfo, isAwait, typeOnly, storage, errors, rbp);
            left = EvaluateBinary(op, left, right, exprInfo, errors);
        }

        return left;
    }

    private static TextVariant ParsePrefix(
        ref int pos,
        ExprInfo exprInfo,
        bool isAwait,
        bool typeOnly,
        IMemberStorage storage,
        List<Error>? errors)
    {
        ReadOnlySpan<char> line = exprInfo.Line;
        pos = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);

        if (pos >= exprInfo.End)
        {
            errors?.AddError(exprInfo, "Unexpected end of expression.");
            return new();
        }

        char c = line[pos];

        // Parenthesized expression
        if (c == '(')
        {
            pos++;
            TextVariant inner = ParseExpression(ref pos, exprInfo, isAwait, typeOnly, storage, errors);
            pos = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);

            if (pos >= exprInfo.End || line[pos] != ')')
            {
                errors?.AddError(exprInfo, "Mismatched parentheses");
                return new();
            }

            pos++;
            return inner;
        }

        if (c == '"')
            return HandleStringLiteral(ref pos, exprInfo, errors);

        if (char.IsDigit(c) || (c == '.' && pos + 1 < exprInfo.End && char.IsDigit(line[pos + 1])))
            return HandleNumberLiteral(ref pos, exprInfo, errors);

        // Identifier or keyword or function call
        if (char.IsLetter(c) || c == '_')
        {
            int start = pos;
            pos++;

            while (pos < exprInfo.End && (char.IsLetterOrDigit(line[pos]) || line[pos] == '_'))
                pos++;

            ReadOnlySpan<char> ident = line[start..pos];

            if (ident.SequenceEqual("true"))
                return new(true);

            if (ident.SequenceEqual("false"))
                return new(false);

            int j = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);
            bool isCall = j < exprInfo.End && line[j] == '(';

            if (!isCall)
                return HandleVariable(typeOnly, ident, exprInfo, storage, errors);

            return HandleFuncCall(ref pos, exprInfo, isAwait, typeOnly, start, ident, j, storage, errors);
        }

        // Unary operators
        if (c == '+' || c == '-' || c == '!')
        {
            pos++;
            TextVariant operand = ParseExpression(ref pos, exprInfo, isAwait, typeOnly, storage, errors, 9);
            return EvaluateUnary(c, operand, exprInfo, errors);
        }

        errors?.AddError(exprInfo, $"Unexpected character '{c}' at position {pos}");
        return new();

        static TextVariant HandleStringLiteral(ref int pos, ExprInfo exprInfo, List<Error>? errors)
        {
            ReadOnlySpan<char> line = exprInfo.Line;
            int start = pos + 1;
            pos++;

            while (pos < exprInfo.End)
            {
                if (line[pos] == '\\')
                {
                    pos += 2;
                    continue;
                }

                if (line[pos] == '"')
                    break;

                pos++;
            }

            if (pos >= exprInfo.End || line[pos] != '"')
                errors?.AddError(exprInfo, "Unterminated string literal");

            int end = pos;
            pos++; // skip closing

            return new(line[start..end].ToString());
        }

        static TextVariant HandleNumberLiteral(ref int pos, ExprInfo exprInfo, List<Error>? errors)
        {
            ReadOnlySpan<char> line = exprInfo.Line;
            int start = pos;
            pos++;

            while (pos < exprInfo.End)
            {
                char c2 = line[pos];

                if (char.IsDigit(c2) || c2 == '.')
                {
                    pos++;
                    continue;
                }

                if (c2 == 'e' || c2 == 'E')
                {
                    pos++;

                    if (pos < exprInfo.End && (line[pos] == '+' || line[pos] == '-'))
                        pos++;

                    while (pos < exprInfo.End && char.IsDigit(line[pos]))
                        pos++;
                }

                break;
            }

            ReadOnlySpan<char> span = line[start..pos];

            if (float.TryParse(span, out float f))
                return new(f);

            errors?.AddError(exprInfo, $"Invalid number literal '{span}'.");
            return new();
        }

        static TextVariant HandleVariable(
            bool typeOnly,
            ReadOnlySpan<char> ident,
            ExprInfo exprInfo,
            IMemberStorage storage,
            List<Error>? errors)
        {
            if (typeOnly)
            {
                VarType vt = storage.GetVariableType(ident);
                return vt switch
                {
                    VarType.Float => new(0),
                    VarType.String => new(string.Empty),
                    VarType.Bool => new(false),
                    _ => new()
                };
            }

            if (!storage.TryGetVariable(ident, out TextVariant varValue))
            {
                errors?.AddError(exprInfo, $"Undefined variable '{ident}'.");
                return new();
            }

            return varValue;
        }

        static TextVariant HandleFuncCall(
            ref int pos,
            ExprInfo exprInfo,
            bool isAwait,
            bool typeOnly,
            int start,
            ReadOnlySpan<char> ident,
            int j,
            IMemberStorage storage,
            List<Error>? errors)
        {
            ReadOnlySpan<char> line = exprInfo.Line;
            pos = j + 1; // move inside '('

            List<TextVariant> args = [];
            pos = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);

            if (pos < exprInfo.End && line[pos] != ')')
            {
                while (true)
                {
                    TextVariant arg = ParseExpression(ref pos, exprInfo, isAwait, typeOnly, storage, errors);
                    args.Add(arg);

                    pos = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);

                    if (pos >= exprInfo.End)
                    {
                        errors?.AddError(exprInfo, "Unterminated function call");
                        return new();
                    }

                    if (line[pos] == ')')
                        break;

                    if (line[pos] != ',')
                    {
                        errors?.AddError(exprInfo, "Expected ',' in argument list");
                        return new();
                    }

                    pos++; // consume comma
                    pos = DialogHelpers.GetNextNonWhitespace(line[..exprInfo.End], pos);
                }
            }

            if (pos >= exprInfo.End || line[pos] != ')')
            {
                errors?.AddError(exprInfo, "Mismatched parentheses in call");
                return new();
            }

            pos++; // consume ')'
            ReadOnlySpan<char> name = line[start..(start + ident.Length)];
            VarType funcType = storage.GetMethodReturnType(name);

            if (funcType == VarType.Undefined)
            {
                errors?.AddError(exprInfo, $"Undefined method '{name}'.");
                return new();
            }

            if (typeOnly)
            {
                return funcType switch
                {
                    VarType.Float => new(0),
                    VarType.String => new(string.Empty),
                    VarType.Bool => new(false),
                    _ => new()
                };
            }

            TextVariant funcValue;

            if (isAwait)
                funcValue = storage.CallAsyncMethod(name, args.ToArray());
            else
                funcValue = storage.CallMethod(name, args.ToArray());

            if (funcValue.VariantType == VarType.Undefined)
            {
                errors?.AddError(exprInfo, $"Undefined method or incorrect arguments for '{name}'.");
                return new();
            }

            return funcValue;
        }
    }

    private static ReadOnlySpan<char> PeekOperator(ref int pos, ExprInfo exprInfo)
    {
        ReadOnlySpan<char> line = exprInfo.Line;

        if (pos >= exprInfo.End)
            return [];

        int remaining = exprInfo.End - pos;

        if (remaining >= 2)
        {
            switch (line[pos..(pos + 2)])
            {
                case "==":
                case "!=":
                case ">=":
                case "<=":
                case "&&":
                case "||":
                    return line.Slice(pos, 2);
            }
        }

        return line[pos] switch
        {
            '+' or
            '-' or
            '*' or
            '/' or
            '>' or
            '<' => line.Slice(pos, 1),
            _ => [],
        };
    }

    private static (int lbp, int rbp) InfixBindingPower(ReadOnlySpan<char> op)
    {
        return op switch
        {
            "||" => (3, 3),
            "&&" => (4, 4),
            "==" => (5, 5),
            "!=" => (5, 5),
            ">" => (6, 6),
            "<" => (6, 6),
            ">=" => (6, 6),
            "<=" => (6, 6),
            "+" => (7, 7),
            "-" => (7, 7),
            "*" => (8, 8),
            "/" => (8, 8),
            _ => (0, 0),
        };
    }

    private static TextVariant EvaluateUnary(char op, TextVariant operand, ExprInfo exprInfo, List<Error>? errors)
    {
        switch (op)
        {
            case '+':
                if (operand.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Unary '+' requires numeric operand.");
                    break;
                }

                return new(operand.Float);
            case '-':
                if (operand.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Unary '-' requires numeric operand.");
                    break;
                }

                return new(-operand.Float);
            case '!':
                if (operand.VariantType != VarType.Bool)
                {
                    errors?.AddError(exprInfo, "Logical 'not' requires boolean operand.");
                    break;
                }

                return new(!operand.Bool);
        }

        return new();
    }

    private static TextVariant EvaluateBinary(
        ReadOnlySpan<char> op,
        TextVariant left,
        TextVariant right,
        ExprInfo exprInfo,
        List<Error>? errors)
    {
        switch (op)
        {
            case "+":
                if (left.VariantType == VarType.String || right.VariantType == VarType.String)
                {
                    string sLeft = string.Empty;
                    string sRight = string.Empty;

                    if (left.VariantType == VarType.String)
                        sLeft = left.String;
                    else if (left.VariantType == VarType.Float)
                        sLeft = left.Float.ToString();
                    
                    if (right.VariantType == VarType.String)
                        sRight = right.String;
                    else if (right.VariantType == VarType.Float)
                        sRight = right.Float.ToString();

                    return new(sLeft + sRight);
                }

                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '+' requires numeric operands.");
                    return new();
                }

                return new(left.Float + right.Float);
            case "-":
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '-' requires numeric operands.");
                    return new();
                }

                return new(left.Float - right.Float);
            case "*":
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '*' requires numeric operands.");
                    return new();
                }

                return new(left.Float * right.Float);
            case "/":
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '/' requires numeric operands.");
                    return new();
                }

                return new(left.Float / right.Float);
            case "==":
                if (left.VariantType == VarType.Float && right.VariantType == VarType.Float)
                    return new(left.Float == right.Float);

                if (left.VariantType == VarType.Bool && right.VariantType == VarType.Bool)
                    return new(left.Bool == right.Bool);

                if (left.VariantType == VarType.String && right.VariantType == VarType.String)
                    return new(left.String == right.String);

                return new(false);
            case "!=":
                if (left.VariantType == VarType.Float && right.VariantType == VarType.Float)
                    return new(left.Float != right.Float);

                if (left.VariantType == VarType.Bool && right.VariantType == VarType.Bool)
                    return new(left.Bool != right.Bool);

                if (left.VariantType == VarType.String && right.VariantType == VarType.String)
                    return new(left.String != right.String);

                return new(true);
            case ">":
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '>' requires numeric operands.");
                    return new();
                }

                return new(left.Float > right.Float);
            case "<":
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '<' requires numeric operands.");
                    return new();
                }

                return new(left.Float < right.Float);
            case ">=":
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '>=' requires numeric operands.");
                    return new();
                }

                return new(left.Float >= right.Float);
            case "<=":
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                {
                    errors?.AddError(exprInfo, "Operator '<=' requires numeric operands.");
                    return new();
                }

                return new(left.Float <= right.Float);
            case "&&":
                if (left.VariantType != VarType.Bool || right.VariantType != VarType.Bool)
                {
                    errors?.AddError(exprInfo, "Logical 'and' requires boolean operands.");
                    return new();
                }

                return new(left.Bool && right.Bool);
            case "||":
                if (left.VariantType != VarType.Bool || right.VariantType != VarType.Bool)
                {
                    errors?.AddError(exprInfo, "Logical 'or' requires boolean operands.");
                    return new();
                }

                return new(left.Bool || right.Bool);
            default:
                errors?.AddError(exprInfo, $"Unhandled operator '{op}'.");
                return new();
        }
    }

    private enum AssignKind
    {
        Undefined,
        Assign,
        Add,
        Sub,
        Mult,
        Div
    }
}