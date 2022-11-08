using System.Collections.Concurrent;

namespace GameDialog.Compiler;

public class DocumentManager
{
    public ConcurrentDictionary<string, DialogDocument> Documents { get; set; } = new();
    private DialogCompiler _compiler = new();

    public Dictionary<string, CompilationResult> Compile() => _compiler.Compile(Documents);
}
