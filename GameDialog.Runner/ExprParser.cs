using System;
using System.Buffers;
using System.Collections.Generic;

namespace GameDialog.Runner;

public static class ExprParser
{
    public static TextVariant Parse(ReadOnlyMemory<char> line, int offsetStart, IMemberStorage storage)
    {
        ExprInfo.TryGetExprInfo(line, 0, offsetStart, null, out ExprInfo exprInfo);
        return Parse(exprInfo, storage);
    }

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
        ReadOnlySpan<char> expr = exprInfo.Span;
        int pos = 0;

        if (CheckInitializers(exprInfo, storage, errors))
            return new();

        bool isAwait = false;
        AssignKind assignKind = AssignKind.Undefined;

        if (expr.StartsWith("await "))
        {
            pos += "await ".Length;
            isAwait = true;
        }
        else
        {
            int assignStart = pos;
            assignStart = DialogHelpers.GetNextNonIdentifier(expr, assignStart);
            assignStart = DialogHelpers.GetNextNonWhitespace(expr, assignStart);
            assignKind = GetAssignKind(expr, assignStart);

            if (assignKind == AssignKind.Assign)
                pos = DialogHelpers.GetNextNonWhitespace(expr, assignStart + 1);
            else if (assignKind != AssignKind.Undefined)
                pos = DialogHelpers.GetNextNonWhitespace(expr, assignStart + 2);
        }

        int parenCount = 0;

        TextVariant result = ParseExpression(exprInfo, ref pos, ref parenCount, isAwait, typeOnly, storage, errors);

        if (!typeOnly && result.VariantType is not VarType.Void and not VarType.Undefined)
            TryAssign(exprInfo, assignKind, result, storage, errors);

        return result;
    }

    private static bool CheckInitializers(ExprInfo exprInfo, IMemberStorage memberStorage, List<Error>? errors)
    {
        ReadOnlySpan<char> expr = exprInfo.Span;

        if (expr.Length == 0 || expr[0] != '@')
            return false;

        // Only handle if validating
        if (errors == null)
            return true;

        VarType varType = VarType.Undefined;

        if (expr.StartsWith("@bool "))
            varType = VarType.Bool;
        else if (expr.StartsWith("@string "))
            varType = VarType.String;
        else if (expr.StartsWith("@float "))
            varType = VarType.Float;

        if (varType == VarType.Undefined)
        {
            errors?.AddError(exprInfo, 0, "Unknown initialization type.");
            return true;
        }

        int i = DialogHelpers.GetNextNonIdentifier(expr, 1);

        if (i >= expr.Length)
        {
            errors?.AddError(exprInfo, 0, "No valid variable provided.");
            return true;
        }

        while (i < expr.Length)
        {
            int ws = DialogHelpers.GetNextNonWhitespace(expr, i) - i;

            if (ws <= 0)
            {
                errors?.AddError(exprInfo, 0, "No valid variable provided.");
                return true;
            }

            i += ws;
            int start = i;
            i = DialogHelpers.GetNextNonIdentifier(expr, i);
            ReadOnlySpan<char> varName = expr[start..i];

            if (varName.Length == 0
                || (!char.IsLetter(varName[0]) && varName[0] != '_')
                || (i != expr.Length && !char.IsWhiteSpace(expr[i])))
            {
                errors?.AddError(exprInfo, start, "Invalid variable name.");
                return true;
            }

            if (memberStorage.GetVariableType(varName) != VarType.Undefined)
            {
                errors?.AddError(exprInfo, start, $"Cannot initialize existing variable '{varName}'.");
                return true;
            }
            else
            {
                if (varType == VarType.Bool)
                    memberStorage.SetValue(varName, new(false));
                else if (varType == VarType.Float)
                    memberStorage.SetValue(varName, new(0));
                else if (varType == VarType.String)
                    memberStorage.SetValue(varName, new(string.Empty));
            }
        }

        return true;
    }

    private static void TryAssign(
        ExprInfo exprInfo,
        AssignKind assignKind,
        TextVariant result,
        IMemberStorage memberStorage,
        List<Error>? errors)
    {
        if (assignKind == AssignKind.Undefined)
            return;

        int i = DialogHelpers.GetNextNonIdentifier(exprInfo.Span, 0);
        ReadOnlySpan<char> varName = exprInfo.Span[..i];

        if (!char.IsLetter(varName[0]) && varName[0] != '_')
        {
            errors?.AddError(exprInfo, 0, "Variables must start with a letter or underscore.");
            return;
        }

        VarType varType = memberStorage.GetVariableType(varName);

        if (assignKind == AssignKind.Assign)
        {
            if (varType == VarType.Undefined || varType == result.VariantType)
                memberStorage.SetValue(varName, result);
            else
                errors?.AddError(exprInfo, 0, $"Cannot assign {result.VariantType} to {varType} '{varName}'.");

            return;
        }

        if (varType == VarType.Undefined)
        {
            errors?.AddError(exprInfo, 0, "Cannot modify uninitialized variable.");
            return;
        }

        if (assignKind == AssignKind.Add)
        {
            if (varType == VarType.String)
            {
                if (result.VariantType == VarType.String)
                {
                    memberStorage.TryGetVariant(varName, out TextVariant value);
                    memberStorage.SetValue(varName, new(value.Chars.ToString() + result.Chars.ToString()));
                }
                else if (result.VariantType == VarType.Float)
                {
                    memberStorage.TryGetVariant(varName, out TextVariant value);
                    memberStorage.SetValue(varName, new(value.Chars.ToString() + result.Float));
                }
                else
                {
                    errors?.AddError(exprInfo, 0, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                    return;
                }
            }
            else if (varType == VarType.Float)
            {
                if (result.VariantType == VarType.Float)
                {
                    memberStorage.TryGetVariant(varName, out TextVariant value);
                    memberStorage.SetValue(varName, new(value.Float + result.Float));
                }
                else
                {
                    errors?.AddError(exprInfo, 0, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                    return;
                }
            }
            else
            {
                errors?.AddError(exprInfo, 0, $"Cannot add {result.VariantType} to {varType} '{varName}'.");
                return;
            }
        }
        else if (assignKind == AssignKind.Sub)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                errors?.AddError(exprInfo, 0, $"Cannot subtract {result.VariantType} from {varType} '{varName}'.");
                return;
            }

            memberStorage.TryGetVariant(varName, out TextVariant value);
            memberStorage.SetValue(varName, new(value.Float - result.Float));
        }
        else if (assignKind == AssignKind.Mult)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                errors?.AddError(exprInfo, 0, $"Cannot multiply {result.VariantType} with {varType} '{varName}'.");
                return;
            }

            memberStorage.TryGetVariant(varName, out TextVariant value);
            memberStorage.SetValue(varName, new(value.Float * result.Float));
        }
        else if (assignKind == AssignKind.Div)
        {
            if (result.VariantType != VarType.Float || varType != VarType.Float)
            {
                errors?.AddError(exprInfo, 0, $"Cannot divide {result.VariantType} by {varType} '{varName}'.");
                return;
            }

            if (result.Float == 0)
                return;

            memberStorage.TryGetVariant(varName, out TextVariant value);
            memberStorage.SetValue(varName, new(value.Float / result.Float));
        }
    }

    private static AssignKind GetAssignKind(ReadOnlySpan<char> expr, int start)
    {
        if (start + 1 >= expr.Length)
            return AssignKind.Undefined;

        char c1 = expr[start];
        char c2 = expr[start + 1];

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
        ExprInfo exprInfo,
        ref int pos,
        ref int parenCount,
        bool isAwait,
        bool typeOnly,
        IMemberStorage storage,
        List<Error>? errors,
        int minLbp = 0)
    {
        ReadOnlySpan<char> expr = exprInfo.Span;
        pos = DialogHelpers.GetNextNonWhitespace(expr, pos);
        TextVariant left = ParsePrefix(exprInfo, ref pos, ref parenCount, isAwait, typeOnly, storage, errors);

        while (true)
        {
            pos = DialogHelpers.GetNextNonWhitespace(expr, pos);

            if (pos >= expr.Length)
                break;

            if (expr[pos] == ',')
                break;

            if (expr[pos] == ')')
            {
                if (parenCount <= 0)
                {
                    pos++;
                    errors?.AddError(exprInfo, "Unexpected char ')'.");
                }

                break;
            }

            ReadOnlySpan<char> op = PeekOperator(pos, exprInfo);

            if (op.IsEmpty)
            {
                if (!expr[pos..].IsWhiteSpace())
                    errors?.AddError(exprInfo, "Expression invalid.");

                break;
            }

            (int lbp, int rbp) = InfixBindingPower(op);

            if (lbp == 0 || lbp <= minLbp)
                break;

            pos += op.Length; // Consume operator
            TextVariant right = ParseExpression(exprInfo, ref pos, ref parenCount, isAwait, typeOnly, storage, errors, rbp);
            left = EvaluateBinary(exprInfo, op, left, right, errors);
        }

        return left;
    }

    private static TextVariant ParsePrefix(
        ExprInfo exprInfo,
        ref int pos,
        ref int parenCount,
        bool isAwait,
        bool typeOnly,
        IMemberStorage storage,
        List<Error>? errors)
    {
        ReadOnlySpan<char> expr = exprInfo.Span;
        pos = DialogHelpers.GetNextNonWhitespace(expr, pos);

        if (pos >= expr.Length)
        {
            errors?.AddError(exprInfo, "Unexpected end of expression.");
            return new();
        }

        char c = expr[pos];

        // Parenthesized expression
        if (c == '(')
        {
            pos++;
            parenCount++;
            TextVariant inner = ParseExpression(exprInfo, ref pos, ref parenCount, isAwait, typeOnly, storage, errors);
            pos = DialogHelpers.GetNextNonWhitespace(expr, pos);

            if (pos >= expr.Length || expr[pos] != ')')
            {
                errors?.AddError(exprInfo, "Mismatched parentheses");
                return new();
            }

            parenCount--;
            pos++;
            return inner;
        }

        if (c == '"')
            return HandleStringLiteral(exprInfo, ref pos, errors);

        if (char.IsDigit(c) || (c == '.' && pos + 1 < expr.Length && char.IsDigit(expr[pos + 1])))
            return HandleNumberLiteral(exprInfo, ref pos, errors);

        // Identifier or keyword or function call
        if (char.IsLetter(c) || c == '_')
        {
            int start = pos;
            pos++;
            pos = DialogHelpers.GetNextNonIdentifier(expr, pos);
            ReadOnlySpan<char> ident = expr[start..pos];

            if (ident.SequenceEqual("true"))
                return new(true);

            if (ident.SequenceEqual("false"))
                return new(false);

            int nextNonWS = DialogHelpers.GetNextNonWhitespace(expr, pos);
            bool isCall = nextNonWS < expr.Length && expr[nextNonWS] == '(';

            if (!isCall)
                return HandleVariable(exprInfo, typeOnly, ident, storage, errors);

            pos = nextNonWS + 1; // Enter '('
            return HandleFuncCall(exprInfo, ref pos, ref parenCount, isAwait, typeOnly, ident, storage, errors);
        }

        // Unary operators
        if (c == '+' || c == '-' || c == '!')
        {
            pos++;
            TextVariant operand = ParseExpression(exprInfo, ref pos, ref parenCount, isAwait, typeOnly, storage, errors, 9);
            return EvaluateUnary(exprInfo, pos, c, operand, errors);
        }

        errors?.AddError(exprInfo, $"Unexpected character '{c}'");
        return new();

        static TextVariant HandleStringLiteral(ExprInfo exprInfo, ref int pos, List<Error>? errors)
        {
            ReadOnlySpan<char> expr = exprInfo.Span;
            int start = pos + 1;
            pos++;

            while (pos < expr.Length)
            {
                if (expr[pos] == '\\')
                {
                    pos += 2;
                    continue;
                }

                if (expr[pos] == '"')
                    break;

                pos++;
            }

            if (pos >= expr.Length || expr[pos] != '"')
                errors?.AddError(exprInfo, "Unterminated string literal");

            int end = pos;
            pos++; // skip closing

            return new(exprInfo.Memory[start..end]);
        }

        static TextVariant HandleNumberLiteral(ExprInfo exprInfo, ref int pos, List<Error>? errors)
        {
            ReadOnlySpan<char> expr = exprInfo.Span;
            int start = pos;
            pos++;

            while (pos < expr.Length)
            {
                char c2 = expr[pos];

                if (char.IsDigit(c2) || c2 == '.')
                {
                    pos++;
                    continue;
                }

                if (c2 == 'e' || c2 == 'E')
                {
                    pos++;

                    if (pos < expr.Length && (expr[pos] == '+' || expr[pos] == '-'))
                        pos++;

                    while (pos < expr.Length && char.IsDigit(expr[pos]))
                        pos++;
                }

                break;
            }

            ReadOnlySpan<char> span = expr[start..pos];

            if (float.TryParse(span, out float floatValue))
                return new(floatValue);

            errors?.AddError(exprInfo, $"Invalid number literal '{span}'.");
            return new();
        }

        static TextVariant HandleVariable(
            ExprInfo exprInfo,
            bool typeOnly,
            ReadOnlySpan<char> ident,
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

            if (!storage.TryGetVariant(ident, out TextVariant varValue))
            {
                errors?.AddError(exprInfo, $"Undefined variable '{ident}'.");
                return new();
            }

            return varValue;
        }

        static TextVariant HandleFuncCall(
            ExprInfo exprInfo,
            ref int pos,
            ref int parenCount,
            bool isAwait,
            bool typeOnly,
            ReadOnlySpan<char> methodName,
            IMemberStorage storage,
            List<Error>? errors)
        {
            ReadOnlySpan<char> expr = exprInfo.Span;
            FuncDef? funcDef = storage.GetMethodFuncDef(methodName);

            if (funcDef == null)
            {
                errors?.AddError(exprInfo, $"Undefined method '{methodName}'.");
                return new();
            }
            else if (isAwait && !funcDef.Awaitable)
            {
                errors?.AddError(exprInfo, $"Method '{methodName}' is not awaitable.");
                return new();
            }

            parenCount++;
            TextVariant funcValue;
            TextVariant[] args = ArrayPool<TextVariant>.Shared.Rent(funcDef.ArgTypes.Length);

            try
            {
                pos = DialogHelpers.GetNextNonWhitespace(expr, pos);
                int currArg = 0;

                while (pos < expr.Length && expr[pos] != ')')
                {
                    TextVariant arg = ParseExpression(exprInfo, ref pos, ref parenCount, isAwait, typeOnly, storage, errors);

                    VarType argType = funcDef.ArgTypes[currArg];

                    if (arg.VariantType != argType)
                    {
                        errors?.AddError(exprInfo, $"Method '{funcDef.Name}' expected type {argType}, but received {arg.VariantType}.");
                        return new();
                    }

                    args[currArg] = arg;
                    currArg++;
                    pos = DialogHelpers.GetNextNonWhitespace(expr, pos);

                    if (pos >= expr.Length)
                    {
                        errors?.AddError(exprInfo, "Unterminated function call");
                        return new();
                    }

                    if (expr[pos] == ',')
                    {
                        if (currArg >= funcDef.ArgTypes.Length)
                        {
                            errors?.AddError(exprInfo, $"Method '{funcDef.Name}' takes {funcDef.ArgTypes.Length} arguments.");
                            return new();
                        }

                        pos++;
                        pos = DialogHelpers.GetNextNonWhitespace(expr, pos);
                        continue;
                    }

                    if (expr[pos] != ')')
                    {
                        errors?.AddError(exprInfo, "Expected ',' in argument list");
                        return new();
                    }
                }

                if (pos >= expr.Length || expr[pos] != ')')
                {
                    errors?.AddError(exprInfo, "Mismatched parentheses in call");
                    return new();
                }

                parenCount--;
                pos++; // consume ')'

                if (typeOnly)
                {
                    return funcDef.ReturnType switch
                    {
                        VarType.Float => new(0),
                        VarType.String => new(string.Empty),
                        VarType.Bool => new(false),
                        _ => new()
                    };
                }

                if (isAwait)
                    funcValue = storage.CallAsyncMethod(methodName, args.AsSpan()[..funcDef.ArgTypes.Length]);
                else
                    funcValue = storage.CallMethod(methodName, args.AsSpan()[..funcDef.ArgTypes.Length]);
            }
            finally
            {
                ArrayPool<TextVariant>.Shared.Return(args);
            }

            if (funcValue.VariantType == VarType.Undefined)
            {
                errors?.AddError(exprInfo, $"Undefined method or incorrect arguments for '{methodName}'.");
                return new();
            }

            return funcValue;
        }
    }

    private static ReadOnlySpan<char> PeekOperator(int pos, ExprInfo exprInfo)
    {
        ReadOnlySpan<char> expr = exprInfo.Span;

        if (pos >= expr.Length)
            return default;

        int remaining = expr.Length - pos;

        if (remaining >= 2)
        {
            switch (expr[pos..(pos + 2)])
            {
                case "==":
                case "!=":
                case ">=":
                case "<=":
                case "&&":
                case "||":
                    return expr.Slice(pos, 2);
            }
        }

        ReadOnlySpan<char> result = expr[pos] switch
        {
            '+' or
            '-' or
            '*' or
            '/' or
            '>' or
            '<' => expr.Slice(pos, 1),
            _ => default,
        };

        return result;
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

    private static TextVariant EvaluateUnary(
        ExprInfo exprInfo,
        int pos,
        char op,
        TextVariant operand,
        List<Error>? errors)
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
        ExprInfo exprInfo,
        ReadOnlySpan<char> op,
        TextVariant left,
        TextVariant right,
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
                        sLeft = left.Chars.ToString();
                    else if (left.VariantType == VarType.Float)
                        sLeft = left.Float.ToString();
                    
                    if (right.VariantType == VarType.String)
                        sRight = right.Chars.ToString();
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
                    return new(left.Chars.Span.SequenceEqual(right.Chars.Span));

                return new(false);
            case "!=":
                if (left.VariantType == VarType.Float && right.VariantType == VarType.Float)
                    return new(left.Float != right.Float);

                if (left.VariantType == VarType.Bool && right.VariantType == VarType.Bool)
                    return new(left.Bool != right.Bool);

                if (left.VariantType == VarType.String && right.VariantType == VarType.String)
                    return new(!left.Chars.Span.SequenceEqual(right.Chars.Span));

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