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

    public DialogDocument(DocumentUri uri)
        :this(uri, string.Empty)
    {
    }

    public LexerErrorListener LexerErrorListener { get; private set; }
    public ParserErrorListener ParserErrorListener { get; private set; }
    public DialogLexer Lexer { get; set; }
    public DialogParser Parser { get; set; }
    public string Text { get; set; } = string.Empty;
    public DocumentUri Uri { get; set; }

    public void UpdateText(string text)
    {
        Text = text;
        AntlrInputStream stream = new(Text);
        Lexer.SetInputStream(stream);
        CommonTokenStream tokens = new(Lexer);
        Parser.TokenStream = tokens;
    }

    public List<Diagnostic> GetDiagnostics()
    {
        List<Diagnostic> diagnostics = ParserErrorListener.Diagnostics.Concat(LexerErrorListener.Diagnostics).ToList();
        ParserErrorListener.Clear();
        LexerErrorListener.Clear();
        return diagnostics;
    }
}
