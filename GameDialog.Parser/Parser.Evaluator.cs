using GameDialog.Common;

namespace GameDialog.Parser;

public partial class Parser
{
    private Stack<TextVariant> _evalStack = new();
}

public enum TokenType : byte
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

public enum OpCode : byte
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

public enum Assoc : byte
{
    Left = 0,
    Right = 1
}

public readonly struct Token
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
    public readonly int Start; // inclusive
    public readonly int End;   // exclusive
}

public static partial class ExprParser
{
    private static Stack<Token> OpStack = [];
    private static List<Token> OutputQueue = [];
    private static Stack<TextVariant> EvalStack = [];
    // Replace with real resolvers/invokers.
    public static Func<ReadOnlySpan<char>, VarType> GetPredefinedVariable = (s) => VarType.Undefined;
    public static Func<ReadOnlySpan<char>, VarType> GetPredefinedMethod = (s) => VarType.Undefined;

    static readonly (int Prec, bool RightAssoc)[] _opInfo =
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
        (9, true),  // UPlus (unary +)
        (9, true),  // UMinus (unary -)
        (9, true)   // Not
    ];

    private static void HandleOperatorToken(Token opToken)
    {
        OpCode curOp = opToken.Op;
        (int Prec, bool RightAssoc) curInfo = _opInfo[(int)curOp];

        while (OpStack.Count > 0 && OpStack.Peek().Type == TokenType.Operator)
        {
            Token top = OpStack.Peek();
            (int Prec, bool RightAssoc) topInfo = _opInfo[(int)top.Op];
            bool shouldPop;

            if (topInfo.Prec > curInfo.Prec)
                shouldPop = true;
            else if (topInfo.Prec == curInfo.Prec && !curInfo.RightAssoc)
                shouldPop = true;
            else
                shouldPop = false;

            if (shouldPop)
            {
                OutputQueue.Add(OpStack.Pop());
                continue;
            }

            break;
        }

        OpStack.Push(opToken);
    }

    public static void ParseToRpn(ReadOnlySpan<char> expr)
    {
        OpStack.Clear();
        OutputQueue.Clear();
        EvalStack.Clear();

        int i = 0;
        bool prevWasOperand = false;

        while (i < expr.Length)
        {
            char c = expr[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Number (start with digit or '.' followed by digit)
            if (char.IsDigit(c) || (c == '.' && i + 1 < expr.Length && char.IsDigit(expr[i + 1])))
            {
                int start = i;
                i++;

                while (i < expr.Length)
                {
                    char c2 = expr[i];

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

                        if (i < expr.Length && (expr[i] == '+' || expr[i] == '-'))
                            i++;

                        while (i < expr.Length && char.IsDigit(expr[i]))
                            i++;

                        break;
                    }

                    break;
                }

                OutputQueue.Add(new(TokenType.Number, start, i));
                prevWasOperand = true;
                continue;
            }

            // Identifier or keyword (true/false) or function name
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                i++;

                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_'))
                    i++;

                ReadOnlySpan<char> ident = expr[start..i];

                if (ident.Length == 4 && ident.SequenceEqual("true"))
                {
                    OutputQueue.Add(new(TokenType.Bool, start, i));
                    prevWasOperand = true;
                    continue;
                }

                if (ident.Length == 5 && ident.SequenceEqual("false"))
                {
                    OutputQueue.Add(new(TokenType.Bool, start, i));
                    prevWasOperand = true;
                    continue;
                }

                // Look ahead to see if this is a function call (next non-space char is '(')
                int j = i;

                while (j < expr.Length && char.IsWhiteSpace(expr[j]))
                    j++;

                bool isCall = j < expr.Length && expr[j] == '(';

                if (isCall)
                {
                    // push the identifier onto the operator stack as a function marker
                    OpStack.Push(new(TokenType.Ident, start, i));
                    prevWasOperand = false;
                }
                else
                {
                    // regular identifier (variable)
                    OutputQueue.Add(new(TokenType.Ident, start, i));
                    prevWasOperand = true;
                }

                continue;
            }

            // String literal
            if (c == '"')
            {
                int start = i + 1;
                i++;

                while (i < expr.Length)
                {
                    if (expr[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (expr[i] == '"')
                        break;

                    i++;
                }

                if (i >= expr.Length || expr[i] != '"')
                    throw new FormatException("Unterminated string literal");

                int end = i;
                i++; // skip closing quote

                OutputQueue.Add(new(TokenType.StringRef, start, end));
                prevWasOperand = true;
                continue;
            }

            // Multi-char operators first
            if (i + 1 < expr.Length)
            {
                char c2 = expr[i + 1];

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

            // Single-char operators / punctuation
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
                case '!': // Logical Not
                    HandleOperatorToken(new(TokenType.Operator, i, i + 1, OpCode.Not));
                    i++;
                    prevWasOperand = false;
                    break;
                case '(':
                    OpStack.Push(new(TokenType.LeftParen, i, i + 1));
                    i++;
                    prevWasOperand = false;
                    break;
                case ')':
                    {
                        // pop until left paren
                        while (OpStack.Count > 0 && OpStack.Peek().Type != TokenType.LeftParen)
                            OutputQueue.Add(OpStack.Pop());

                        if (OpStack.Count == 0)
                            throw new FormatException("Mismatched parentheses");

                        // pop left paren
                        OpStack.Pop();

                        // if top of op stack is an ident (function), pop it to output as Function token
                        if (OpStack.Count > 0 && OpStack.Peek().Type == TokenType.Ident)
                        {
                            Token funcIdent = OpStack.Pop();
                            OutputQueue.Add(new(TokenType.Function, funcIdent.Start, funcIdent.End));
                        }

                        i++;
                        prevWasOperand = true;
                        break;
                    }
                case ',':
                    {
                        // pop operators until left paren
                        while (OpStack.Count > 0 && OpStack.Peek().Type != TokenType.LeftParen)
                            OutputQueue.Add(OpStack.Pop());

                        if (OpStack.Count == 0)
                            throw new FormatException("Comma outside of parentheses");

                        i++;
                        prevWasOperand = false;
                        break;
                    }
                default:
                    throw new FormatException($"Unexpected character '{c}' at position {i}");
            }
        }

        // End of input: pop remaining operators to output
        while (OpStack.Count > 0)
        {
            Token top = OpStack.Pop();

            if (top.Type == TokenType.LeftParen || top.Type == TokenType.RightParen)
                throw new FormatException("Mismatched parentheses");

            OutputQueue.Add(top);
        }
    }

    private static TextVariant ParseLiteral(ReadOnlySpan<char> expr, Token token)
    {
        ReadOnlySpan<char> span = expr[token.Start..token.End];

        if (token.Type == TokenType.Number)
        {
            if (float.TryParse(span, out float f))
                return new(f);

            throw new InvalidOperationException($"Invalid number literal '{span}'.");
        }

        if (token.Type == TokenType.Bool)
        {
            if (span.SequenceEqual("true"))
                return new(true);

            if (span.SequenceEqual("false"))
                return new(false);

            throw new InvalidOperationException($"Invalid bool literal '{span}'.");
        }

        if (token.Type == TokenType.StringRef)
            return new(span.ToString());

        throw new InvalidOperationException("ParseLiteral called with non-literal token.");
    }

    public static TextVariant EvaluateRpn(ReadOnlySpan<char> expr)
    {
        foreach (Token token in OutputQueue)
        {
            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.Bool:
                case TokenType.StringRef:
                    TextVariant lit = ParseLiteral(expr, token);
                    EvalStack.Push(lit);
                    break;
                case TokenType.Ident:
                    {
                        ReadOnlySpan<char> name = expr[token.Start..token.End];
                        TextVariant v = GetVariableValue(name);

                        if (v.VariantType == VarType.Void)
                            throw new InvalidOperationException($"Undefined variable '{name}'.");

                        EvalStack.Push(v);
                        break;
                    }
                case TokenType.Function:
                    {
                        ReadOnlySpan<char> name = expr[token.Start..token.End];
                        int argc = GetPredefinedMethodArgCount(name);

                        if (argc < 0)
                            throw new InvalidOperationException($"Function '{name}' is variadic; parser must provide argument-count convention.");

                        if (EvalStack.Count < argc)
                            throw new InvalidOperationException($"Function '{name}' expects {argc} arguments but stack contains {EvalStack.Count}.");

                        // Todo: pool
                        var args = new TextVariant[argc];

                        for (int i = argc - 1; i >= 0; i--)
                            args[i] = EvalStack.Pop();

                        TextVariant result = InvokePredefinedMethod(name, args);
                        EvalStack.Push(result);
                        break;
                    }
                case TokenType.Operator:
                    HandleOperator(expr, token, EvalStack);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled token type '{token.Type}'.");
            }
        }

        if (EvalStack.Count != 1)
            throw new InvalidOperationException("RPN evaluation did not produce a single result.");

        return EvalStack.Pop();
    }

    private static void HandleOperator(ReadOnlySpan<char> expr, Token token, Stack<TextVariant> stack)
    {
        switch (token.Op)
        {
            case OpCode.UPlus:
                {
                    if (stack.Count < 1)
                        throw new InvalidOperationException("Unary '+' requires one operand.");

                    TextVariant operand = stack.Pop();

                    if (operand.VariantType != VarType.Float)
                        throw new InvalidOperationException("Unary '+' requires numeric operand.");

                    stack.Push(new TextVariant(operand.Float));
                    break;
                }
            case OpCode.UMinus:
                {
                    if (stack.Count < 1)
                        throw new InvalidOperationException("Unary '-' requires one operand.");

                    TextVariant operand = stack.Pop();

                    if (operand.VariantType != VarType.Float)
                        throw new InvalidOperationException("Unary '-' requires numeric operand.");

                    stack.Push(new TextVariant(-operand.Float));
                    break;
                }
            case OpCode.Not:
                {
                    if (stack.Count < 1)
                        throw new InvalidOperationException("Logical 'not' requires one operand.");

                    TextVariant operand = stack.Pop();

                    if (operand.VariantType != VarType.Bool)
                        throw new InvalidOperationException("Logical 'not' requires boolean operand.");

                    stack.Push(new TextVariant(!operand.Bool));
                    break;
                }
            default:
                HandleBinaryOperator(expr, token, stack);
                break;
        }
    }

    private static void HandleBinaryOperator(ReadOnlySpan<char> expr, Token token, Stack<TextVariant> stack)
    {
        if (stack.Count < 2)
            throw new InvalidOperationException($"Operator '{token.Op}' requires two operands.");

        TextVariant right = stack.Pop();
        TextVariant left = stack.Pop();

        switch (token.Op)
        {
            case OpCode.Add:
                {
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

                        stack.Push(new (sLeft + sRight));
                    }
                    else
                    {
                        if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                            throw new InvalidOperationException($"Operator '{token.Op}' requires numeric operands.");

                        stack.Push(new(left.Float + right.Float));
                    }

                    break;
                }
            case OpCode.Sub:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    throw new InvalidOperationException($"Operator '{token.Op}' requires numeric operands.");

                stack.Push(new(left.Float - right.Float));
                break;
            case OpCode.Mul:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    throw new InvalidOperationException($"Operator '{token.Op}' requires numeric operands.");

                stack.Push(new(left.Float * right.Float));
                break;
            case OpCode.Div:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    throw new InvalidOperationException($"Operator '{token.Op}' requires numeric operands.");

                stack.Push(new(left.Float / right.Float));
                break;
            case OpCode.Equal:
                {
                    if (left.VariantType == VarType.Float && right.VariantType == VarType.Float)
                        stack.Push(new(left.Float == right.Float));
                    else if (left.VariantType == VarType.Bool && right.VariantType == VarType.Bool)
                        stack.Push(new(left.Bool == right.Bool));
                    else if (left.VariantType == VarType.String && right.VariantType == VarType.String)
                        stack.Push(new(left.String == right.String));
                    else
                        stack.Push(new(false));

                    break;
                }
            case OpCode.NotEqual:
                {
                    if (left.VariantType == VarType.Float && right.VariantType == VarType.Float)
                        stack.Push(new(left.Float != right.Float));
                    else if (left.VariantType == VarType.Bool && right.VariantType == VarType.Bool)
                        stack.Push(new(left.Bool != right.Bool));
                    else if (left.VariantType == VarType.String && right.VariantType == VarType.String)
                        stack.Push(new(left.String != right.String));
                    else
                        stack.Push(new(true));

                    break;
                }
            case OpCode.Greater:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    throw new InvalidOperationException("Operator '>' requires numeric operands.");

                stack.Push(new(left.Float > right.Float));
                break;
            case OpCode.Less:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    throw new InvalidOperationException("Operator '<' requires numeric operands.");

                stack.Push(new(left.Float < right.Float));
                break;
            case OpCode.GreaterEqual:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    throw new InvalidOperationException("Operator '>=' requires numeric operands.");

                stack.Push(new(left.Float >= right.Float));
                break;
            case OpCode.LessEqual:
                if (left.VariantType != VarType.Float || right.VariantType != VarType.Float)
                    throw new InvalidOperationException("Operator '<=' requires numeric operands.");

                stack.Push(new(left.Float <= right.Float));
                break;
            case OpCode.And:
                if (left.VariantType != VarType.Bool || right.VariantType != VarType.Bool)
                    throw new InvalidOperationException("Logical 'and' requires boolean operands.");
                
                stack.Push(new (left.Bool && right.Bool));
                break;
            case OpCode.Or:
                if (left.VariantType != VarType.Bool || right.VariantType != VarType.Bool)
                    throw new InvalidOperationException("Logical 'or' requires boolean operands.");
                
                stack.Push(new (left.Bool || right.Bool));
                break;
            default:
                throw new InvalidOperationException($"Unhandled operator '{token.Op}'.");
        }
    }
}