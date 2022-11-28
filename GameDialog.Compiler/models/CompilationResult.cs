using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class CompilationResult
{
    public DocumentUri Uri { get; set; }
    public DialogScript DialogScript { get; set; }
    public List<Diagnostic> Diagnostics { get; set; }
}
