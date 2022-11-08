using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class DialogDocument
{
    public DialogDocument(string fileName)
    {
        FileName = fileName;
        AntlrInputStream stream = new(string.Empty);
        Lexer = new(stream);
        CommonTokenStream tokens = new(Lexer);
        Parser = new(tokens);
        LexerErrorListener = new(fileName);
        ParserErrorListener = new(fileName);
        Lexer.AddErrorListener(LexerErrorListener);
        Parser.AddErrorListener(ParserErrorListener);
    }

    public LexerErrorListener LexerErrorListener { get; private set; }
    public ParserErrorListener ParserErrorListener { get; private set; }
    public DialogLexer Lexer { get; set; }
    public DialogParser Parser { get; set; }
    public string Text { get; set; }
    public string FileName { get; set; }

    public void PushChange(TextDocumentContentChangeEvent changeEvent)
    {
        //var range = changeEvent.Range;
        //var startIndex = LineStarts[range.Start.Line] + range.Start.Character;
        //var endIndex = LineStarts[range.End.Line] + range.End.Character;

        //var stringBuilder = new StringBuilder();

        //stringBuilder.Append(Text, 0, startIndex)
        //    .Append(changeEvent.Text)
        //    .Append(Text, endIndex, Text.Length - endIndex);

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
