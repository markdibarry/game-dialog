using Antlr4.Runtime;

namespace GameDialog.Compiler;

public abstract class DialogLexerBase : Lexer
{
    private readonly Stack<int> _indents = new();
    private readonly Queue<IToken> _pendingTokens = new();
    private IToken? _prevToken;
    private const int TabSize = 8;

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

        IToken? prevToken = _prevToken;
        _prevToken = currentToken;

        if (prevToken != null && prevToken.Type != DialogLexer.NEWLINE)
            return currentToken;

        IToken nextToken = base.NextToken();

        // Skip over any extra NEWLINE and WS NEWLINE combos
        while (currentToken.Type == DialogLexer.NEWLINE || nextToken.Type == DialogLexer.NEWLINE)
        {
            currentToken = nextToken;
            nextToken = base.NextToken();
        }

        if (currentToken.Type == Eof)
            return HandleEOFToken(_prevToken, currentToken);

        _prevToken = nextToken;
        int currentIndent = GetCurrentIndentation(currentToken);
        HandleIndentation(currentIndent);
        _pendingTokens.Enqueue(currentToken);
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

        _pendingTokens.Enqueue(currentToken);
        return _pendingTokens.Dequeue();
    }

    private int GetCurrentIndentation(IToken currentToken)
    {
        if (currentToken.Type != DialogLexer.WS)
            return 0;

        int length = 0;
        bool containsSpaces = false;
        bool containsTabs = false;

        foreach (char c in currentToken.Text)
        {
            if (c == ' ')
            {
                if (IndentMode == IndentType.Unset)
                    IndentMode = IndentType.Spaces;

                containsSpaces = true;
                length += 1;
            }
            else if (c == '\t')
            {
                if (IndentMode == IndentType.Unset)
                    IndentMode = IndentType.Tabs;

                containsTabs = true;
                length += TabSize;
            }
        }

        if (containsSpaces && containsTabs)
            throw new ArgumentException("Indentation contains tabs and spaces");
        else if (containsSpaces && IndentMode == IndentType.Tabs)
            throw new ArgumentException("Indentation contains spaces, but previously used tabs.");
        else if (containsTabs && IndentMode == IndentType.Spaces)
            throw new ArgumentException("Indentation contains tabs, but previously used spaces.");

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