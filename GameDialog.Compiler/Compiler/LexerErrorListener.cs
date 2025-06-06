using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class LexerErrorListener : IAntlrErrorListener<int>
{
    public LexerErrorListener(string fileName)
    {
        _filename = fileName;
    }

    private readonly string _filename;
    private readonly List<Diagnostic> _diagnostics = [];
    public IReadOnlyCollection<Diagnostic> Diagnostics => _diagnostics.AsReadOnly();

    public void Clear() => _diagnostics.Clear();

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        Diagnostic diagnostic = new()
        {
            Source = _filename,
            Range = new(line - 1, charPositionInLine, line - 1, charPositionInLine + 1),
            Message = msg,
            Severity = DiagnosticSeverity.Error
        };
        _diagnostics.Add(diagnostic);
    }
}
