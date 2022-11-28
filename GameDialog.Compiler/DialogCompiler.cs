using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Concurrent;

namespace GameDialog.Compiler;

public class DialogCompiler
{
    public DialogCompiler(MemberRegister memberRegister)
    {
        _memberRegister = memberRegister;
    }

    private readonly MemberRegister _memberRegister;

    public Dictionary<DocumentUri, CompilationResult> Compile(ConcurrentDictionary<DocumentUri, DialogDocument> documents)
    {
        Dictionary<DocumentUri, CompilationResult> results = new();
        foreach(var kvp in documents)
            results.Add(kvp.Value.Uri, Compile(kvp.Value));
        return results;
    }

    public CompilationResult Compile(DialogDocument document)
    {
        ParserRuleContext context = document.Parser.script();
        //Utility.PrintTokens((CommonTokenStream)document.Parser.TokenStream);
        //Utility.PrintTree(context);
        DialogScript dialogScript = new();
        List<Diagnostic> diagnostics = new();
        // Get all speaker ids
        SpeakerIdVisitor speakerNameVisitor = new(dialogScript);
        speakerNameVisitor.Visit(context);

        MainDialogVisitor visitor = new(dialogScript, diagnostics, _memberRegister);
        visitor.Visit(context);
        diagnostics.AddRange(document.GetDiagnostics());
        CompilationResult result = new()
        {
            Uri = document.Uri,
            DialogScript = dialogScript,
            Diagnostics = diagnostics
        };
        return result;
    }
}
