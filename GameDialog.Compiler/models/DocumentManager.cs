using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Concurrent;

namespace GameDialog.Compiler;

public class DocumentManager
{
    public DocumentManager()
    {
        MemberRegister = new();
        _compiler = new(MemberRegister);
    }

    private readonly DialogCompiler _compiler;
    public ConcurrentDictionary<DocumentUri, DialogDocument> Documents { get; set; } = new();
    public MemberRegister MemberRegister { get; }


    public Dictionary<DocumentUri, CompilationResult> Compile() => _compiler.Compile(Documents);
}
