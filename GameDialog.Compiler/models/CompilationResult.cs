using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class CompilationResult
{
    public DialogScript DialogScript { get; set; }
    public List<Diagnostic> Diagnostics { get; set; }
}
