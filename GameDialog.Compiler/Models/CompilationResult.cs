using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class CompilationResult
{
    public CompilationResult(DocumentUri uri, ScriptData data, List<Diagnostic> diagnostics)
    {
        Uri = uri;
        ScriptData = data;
        Diagnostics = diagnostics;
    }

    public DocumentUri Uri { get; set; }
    public ScriptData ScriptData { get; set; }
    public List<Diagnostic> Diagnostics { get; set; }
}
