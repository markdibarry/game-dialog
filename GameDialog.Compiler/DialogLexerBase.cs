using Antlr4.Runtime;

namespace GameDialog.Compiler;

public abstract class DialogLexerBase : Lexer
{
    private readonly Stack<int> _indents = new Stack<int>();
    private readonly Queue<IToken> _pendingTokens = new Queue<IToken>();
    private IToken? _prevToken;

    public DialogLexerBase(ICharStream input, TextWriter output, TextWriter errorOutput)
    : base(input, output, errorOutput)
    {
    }

    protected DialogLexerBase(ICharStream input)
    : base(input)
    {
    }

    public override IToken? NextToken()
    {
        if (HitEOF && _pendingTokens.Count > 0)
        {
            return _pendingTokens.Dequeue();
        }
        else if (InputStream.Size == 0)
        {
            HitEOF = true;
            return new CommonToken(Eof, "<EOF>");
        }
        return GetNextToken();
    }

    private IToken GetNextToken()
    {
        // If tokens are in the list, dequeue until empty
        if (_pendingTokens.Count > 0)
        {
            _prevToken = _pendingTokens.Dequeue();
            return _prevToken;
        }

        IToken currentToken = base.NextToken();
        // If EOF, handle Newline and Dedenting
        if (currentToken.Type == Eof)
        {
            HandleEOFToken(currentToken);
            return _pendingTokens.Dequeue();
        }
        IToken? prevToken = _prevToken;
        _prevToken = currentToken;
        if (prevToken == null || prevToken.Type != DialogLexer.NEWLINE)
            return currentToken;
        if (currentToken.Type == DialogLexer.NEWLINE)
            return currentToken;
        // If the previous token was a NEWLINE, and the current isn't, check for indentation
        int currentIndent = GetNewLineLength(prevToken);
        int previousIndent = _indents.Count > 0 ? _indents.Peek() : 0;

        if (currentIndent > previousIndent)
        {
            _indents.Push(currentIndent);
            InsertToken($"INDENT: {previousIndent} -> {currentIndent}", DialogLexer.INDENT);
        }
        else if (currentIndent < previousIndent)
        {
            while (currentIndent < previousIndent)
            {
                previousIndent = _indents.Pop();
                InsertToken($"DEDENT: {currentIndent} <- {previousIndent}", DialogLexer.DEDENT);
                previousIndent = _indents.Count > 0 ? _indents.Peek() : 0;
            }
        }
        _pendingTokens.Enqueue(currentToken);
        return _pendingTokens.Dequeue();
    }

    private void HandleEOFToken(IToken currentToken)
    {
        if (_prevToken != null && _prevToken.Type != DialogLexer.NEWLINE)
            InsertToken("NEWLINE", DialogLexer.NEWLINE);

        while (_indents.Count > 0)
        {
            var indent = _indents.Pop();
            InsertToken($"DEDENT: {indent}", DialogLexer.DEDENT);
        }
        _pendingTokens.Enqueue(currentToken);
    }

    private int GetNewLineLength(IToken currentToken)
    {
        if (currentToken.Type != DialogLexer.NEWLINE)
        {
            string ex = $"Expected {nameof(currentToken)} to be {nameof(DialogLexer.NEWLINE)}, not {currentToken.Type}";
            throw new ArgumentException(ex);
        }

        int length = 0;
        bool containsSpaces = false;
        bool containsTabs = false;

        foreach (char c in currentToken.Text)
        {
            if (c == ' ')
            {
                containsSpaces = true;
                length += 1;
            }
            else if (c == '\t')
            {
                containsTabs = true;
                length += 8;
            }
        }

        if (containsSpaces && containsTabs)
            throw new ArgumentException("Indentation contains tabs and spaces");
        return length;
    }

    private void InsertToken(string text, int type)
    {
        int startIndex = TokenStartCharIndex + Text.Length;
        InsertToken(startIndex, startIndex - 1, text, type, Line, Column);
    }

    private void InsertToken(int startIndex, int stopIndex, string text, int type, int line, int column)
    {
        var tokenFactorySourcePair = Tuple.Create((ITokenSource)this, (ICharStream)InputStream);

        CommonToken token = new(tokenFactorySourcePair, type, DefaultTokenChannel, startIndex, stopIndex)
        {
            Text = text,
            Line = line,
            Column = column,
        };

        _pendingTokens.Enqueue(token);
    }
}