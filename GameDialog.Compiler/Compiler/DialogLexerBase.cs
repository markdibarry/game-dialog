using Antlr4.Runtime;

namespace GameDialog.Compiler;

public abstract class DialogLexerBase : Lexer
{
    private readonly Stack<int> _indents = new();
    private readonly Queue<IToken> _pendingTokens = new();
    private IToken? _prevToken;

    public IndentType IndentMode { get; set; }

    public enum IndentType
    {
        Unset,
        Tabs,
        Spaces
    }

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
        if (_pendingTokens.Count > 0)
        {
            _prevToken = _pendingTokens.Dequeue();
            return _prevToken;
        }
        else if (InputStream.Size == 0)
        {
            HitEOF = true;
            return new CommonToken(Eof, "<EOF>");
        }

        IToken currentToken = base.NextToken();

        if (currentToken.Type == Eof)
            return HandleEOFToken(_prevToken, currentToken);

        if (currentToken.Type != DialogLexer.NEWLINE)
        {
            _prevToken = currentToken;
            return currentToken;
        }

        IToken nextToken = base.NextToken();

        // Skip ahead to the first newline that has something on it.
        while (nextToken.Type == DialogLexer.NEWLINE)
        {
            currentToken = nextToken;
            nextToken = base.NextToken();
        }

        int currentIndent = GetCurrentIndentation(currentToken);
        _prevToken = currentToken;
        _pendingTokens.Enqueue(currentToken);
        HandleIndentation(currentIndent);
        _pendingTokens.Enqueue(nextToken);

        return _pendingTokens.Dequeue();
    }

    private void HandleIndentation(int currentIndent)
    {
        int previousIndent = _indents.Count > 0 ? _indents.Peek() : 0;

        if (currentIndent > previousIndent)
        {
            _indents.Push(currentIndent);
            InsertToken($"INDENT: {previousIndent} -> {currentIndent}", DialogLexer.INDENT);
        }
        else
        {
            while (currentIndent < previousIndent)
            {
                previousIndent = _indents.Pop();
                InsertToken($"DEDENT: {currentIndent} <- {previousIndent}", DialogLexer.DEDENT);
                previousIndent = _indents.Count > 0 ? _indents.Peek() : 0;
            }
        }
    }

    private IToken HandleEOFToken(IToken? prevToken, IToken currentToken)
    {
        if (prevToken != null && prevToken.Type != DialogLexer.NEWLINE)
            InsertToken("NEWLINE", DialogLexer.NEWLINE);

        while (_indents.Count > 0)
        {
            var indent = _indents.Pop();
            InsertToken($"DEDENT: {indent}", DialogLexer.DEDENT);
        }

        _prevToken = null;
        _pendingTokens.Enqueue(currentToken);
        return _pendingTokens.Dequeue();
    }

    private int GetCurrentIndentation(IToken token)
    {
        int length = 0;
        bool containsSpaces = false;
        bool containsTabs = false;

        foreach (char c in token.Text)
        {
            if (c == ' ')
            {
                containsSpaces = true;
                length++;
            }
            else if (c == '\t')
            {
                containsTabs = true;
                length++;
            }
        }

        if (containsSpaces && containsTabs)
            throw new ArgumentException("Indentation contains tabs and spaces");
        else if (containsSpaces && IndentMode == IndentType.Tabs)
            throw new ArgumentException("Indentation contains spaces, but previously used tabs.");
        else if (containsTabs && IndentMode == IndentType.Spaces)
            throw new ArgumentException("Indentation contains tabs, but previously used spaces.");

        if (IndentMode == IndentType.Unset)
            IndentMode = containsTabs ? IndentType.Tabs : IndentType.Spaces;

        return length;
    }

    private void InsertToken(string text, int type)
    {
        int startIndex = TokenStartCharIndex + Text.Length;
        InsertToken(startIndex, startIndex - 1, text, type, Line, Column);
    }

    private void InsertToken(int startIndex, int stopIndex, string text, int type, int line, int column)
    {
        var tokenFactoryTuple = Tuple.Create((ITokenSource)this, (ICharStream)InputStream);

        CommonToken token = new(tokenFactoryTuple, type, DefaultTokenChannel, startIndex, stopIndex)
        {
            Text = text,
            Line = line,
            Column = column,
        };

        _pendingTokens.Enqueue(token);
    }
}