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
        ParserRuleContext context = document.Parser.script();
        //Utility.PrintTokens((CommonTokenStream)document.Parser.TokenStream);
        //Utility.PrintTree(context);
        DialogScript dialogScript = new();
        List<Diagnostic> diagnostics = new();
        MainDialogVisitor visitor = new(dialogScript, diagnostics);
        visitor.Visit(context);
        CompilationResult result = new()
        {
            DialogScript = dialogScript,
            Diagnostics = diagnostics.Concat(document.GetDiagnostics()).ToList()
        };
        return result;
    }
}
