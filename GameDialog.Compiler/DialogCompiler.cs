using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Concurrent;

namespace GameDialog.Compiler;

public class DialogCompiler
{
    public Dictionary<string, CompilationResult> Compile(ConcurrentDictionary<string, DialogDocument> documents)
    {
        Dictionary<string, CompilationResult> results = new();
        foreach(var kvp in documents)
            results.Add(kvp.Value.FileName, Compile(kvp.Value));
        return results;
    }

    public CompilationResult Compile(DialogDocument document)
    {
        MemberRegister memberRegister = new();
        ParserRuleContext context = document.Parser.script();
        Utility.PrintTokens((CommonTokenStream)document.Parser.TokenStream);
        Utility.PrintTree(context);
        DialogScript dialogScript = new();
        List<Diagnostic> diagnostics = new();
        // Get all speaker ids
        SpeakerIdVisitor speakerNameVisitor = new(dialogScript);
        speakerNameVisitor.Visit(context);

        MainDialogVisitor visitor = new(dialogScript, diagnostics, memberRegister);
        visitor.Visit(context);
        diagnostics.AddRange(document.GetDiagnostics());
        CompilationResult result = new()
        {
            DialogScript = dialogScript,
            Diagnostics = diagnostics
        };
        return result;
    }
}
