using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace GameDialog.Compiler;

public class DialogCompiler
{
    public MemberRegister MemberRegister { get; } = new();
    public ConcurrentDictionary<DocumentUri, DialogDocument> Documents { get; } = [];

    public void ClearMemberRegister()
    {
        MemberRegister.FuncDefs.Clear();
        MemberRegister.VarDefs.Clear();
    }

    public Dictionary<DocumentUri, CompilationResult> Compile()
    {
        Dictionary<DocumentUri, CompilationResult> results = [];

        foreach (var kvp in Documents)
            results.Add(kvp.Value.Uri, Compile(kvp.Value));

        return results;
    }

    public CompilationResult Compile(DialogDocument document)
    {
        document.Parser.Reset();
        ParserRuleContext context = document.Parser.script();
        ScriptData scriptData = new();
        List<Diagnostic> diagnostics = [];
        MemberRegister memberRegister = new(MemberRegister);
        // Get all speaker ids
        SpeakerIdVisitor speakerNameVisitor = new(scriptData);
        speakerNameVisitor.Visit(context);

        MainDialogVisitor visitor = new(scriptData, diagnostics, memberRegister);
        visitor.Visit(context);
        diagnostics.AddRange(document.GetDiagnostics());
        CompilationResult result = new(document.Uri, scriptData, diagnostics);
        // File.WriteAllText("/mnt/general_storage/Coding/godot/Projects/Longterm/game-dialog/tokens.txt", Utility.PrintTokens((CommonTokenStream)document.Parser.TokenStream));
        // File.WriteAllText("/mnt/general_storage/Coding/godot/Projects/Longterm/game-dialog/logger.txt", Utility.PrintTree(context));
        return result;
    }

    public void UpdateDoc(DocumentUri uri, string text)
    {
        foreach (var existing in Documents)
        {
            string path = existing.Value.Uri.GetFileSystemPath();

            if (!File.Exists(path))
                RemoveDoc(existing.Value.Uri);
        }

        if (!Documents.TryGetValue(uri, out DialogDocument? doc))
        {
            doc = new(uri, text);
            Documents[uri] = doc;
        }

        doc.UpdateText(text);
    }

    public void RemoveDoc(DocumentUri uri)
    {
        Documents.Remove(uri, out var value);
    }
}
