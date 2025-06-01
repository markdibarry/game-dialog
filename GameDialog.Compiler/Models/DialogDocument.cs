using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class DialogDocument
{
    public DialogDocument(DocumentUri uri, string text)
    {
        Uri = uri;
        AntlrInputStream stream = new(text);
        Lexer = new(stream);
        CommonTokenStream tokens = new(Lexer);
        Parser = new(tokens);
        LexerErrorListener = new(uri.Path);
        ParserErrorListener = new(uri.Path);
        Lexer.AddErrorListener(LexerErrorListener);
        Parser.AddErrorListener(ParserErrorListener);
    }

    public LexerErrorListener LexerErrorListener { get; private set; }
    public ParserErrorListener ParserErrorListener { get; private set; }
    public DialogLexer Lexer { get; set; }
    public DialogParser Parser { get; set; }
    public DocumentUri Uri { get; set; }
    private readonly List<Diagnostic> _diagnostics = [];

    public void UpdateText(string text)
    {
        AntlrInputStream stream = new(text);
        Lexer.SetInputStream(stream);
        CommonTokenStream tokens = new(Lexer);
        Parser.TokenStream = tokens;
    }

    public List<Diagnostic> GetDiagnostics()
    {
        _diagnostics.Clear();
        _diagnostics.AddRange(ParserErrorListener.Diagnostics);
        _diagnostics.AddRange(LexerErrorListener.Diagnostics);
        ParserErrorListener.Clear();
        LexerErrorListener.Clear();
        return _diagnostics;
    }
}
