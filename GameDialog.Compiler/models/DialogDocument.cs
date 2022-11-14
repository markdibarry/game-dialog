using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class DialogDocument
{
    public DialogDocument(string fileName, string text)
    {
        FileName = fileName;
        AntlrInputStream stream = new(text);
        Lexer = new(stream);
        CommonTokenStream tokens = new(Lexer);
        Parser = new(tokens);
        LexerErrorListener = new(fileName);
        ParserErrorListener = new(fileName);
        Lexer.AddErrorListener(LexerErrorListener);
        Parser.AddErrorListener(ParserErrorListener);
    }

    public DialogDocument(string fileName)
        :this(fileName, string.Empty)
    {
    }

    public LexerErrorListener LexerErrorListener { get; private set; }
    public ParserErrorListener ParserErrorListener { get; private set; }
    public DialogLexer Lexer { get; set; }
    public DialogParser Parser { get; set; }
    public string Text { get; set; } = string.Empty;
    public string FileName { get; set; }

    public void PushChange(TextDocumentContentChangeEvent changeEvent)
    {
        Text = changeEvent.Text;
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
