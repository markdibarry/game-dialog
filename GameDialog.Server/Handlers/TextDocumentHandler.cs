﻿using CsvHelper;
using GameDialog.Compiler;
using MediatR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace GameDialog.Server;

public class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    private readonly ILanguageServerFacade _server;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly DocumentManager _documentManager = new();
    private readonly DocumentSelector _documentSelector = new(new DocumentFilter() { Pattern = "**/*.dia" });
    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public TextDocumentHandler(ILanguageServerFacade languageServer, ILanguageServerConfiguration configuration)
    {
        _server = languageServer;
        _configuration = configuration;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "gamedialog");
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken cancellationToken)
    {
        _documentManager.MemberRegister.VarDefs.Clear();
        _documentManager.MemberRegister.FuncDefs.Clear();
        SetCustomMembersFromFile("DialogBridgeBase.cs");
        SetCustomMembersFromFile("DialogBridge.cs");
        UpdateDoc(notification.TextDocument.Uri, notification.TextDocument.Text);
        Dictionary<DocumentUri, CompilationResult> results = _documentManager.Compile();
        PublishDiagnostics(results);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken cancellationToken)
    {
        if (!notification.ContentChanges.Any())
            return Unit.Task;
        UpdateDoc(notification.TextDocument.Uri, notification.ContentChanges.First().Text);
        Dictionary<DocumentUri, CompilationResult> results = _documentManager.Compile();
        PublishDiagnostics(results);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(notification.Text))
            return Unit.Task;
        UpdateDoc(notification.TextDocument.Uri, notification.Text);
        Dictionary<DocumentUri, CompilationResult> results = _documentManager.Compile();
        foreach (var kvp in results)
        {
            // Do not generate files with errors
            if (kvp.Value.Diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
            {
                _server.Window.ShowError($"No JSON document was generated. File {kvp.Key} contains errors.");
                continue;
            }

            string uriPath = kvp.Key.GetFileSystemPath();
            string fileName = Path.GetFileNameWithoutExtension(uriPath);
            string pathDirectory = Path.GetDirectoryName(uriPath) ?? string.Empty;

            if (bool.TryParse(_configuration["gamedialog:EnableCSVTranslation"], out bool csvEnabled) && csvEnabled)
            {
                string csvDirectory = _configuration["gamedialog:CSVTranslationLocation"];
                if (string.IsNullOrEmpty(csvDirectory))
                    csvDirectory = pathDirectory;
                CreateTranslationCSV(fileName, csvDirectory, kvp.Value.DialogScript);
            }

            CreateJsonFile(fileName, pathDirectory, kvp.Value.DialogScript);
        }
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = _documentSelector,
            Change = Change,
            Save = new SaveOptions() { IncludeText = true }
        };
    }

    private static void CreateJsonFile(string fileName, string pathDirectory, DialogScript dialogScript)
    {
        string jsonFilePath = $"{pathDirectory}\\{fileName}.json";
        string jsonString = JsonSerializer.Serialize(dialogScript);
        File.WriteAllText(jsonFilePath, jsonString);
    }

    private static void CreateTranslationCSV(string fileName, string pathDirectory, DialogScript dialogScript)
    {
        string csvPath = $"{pathDirectory}\\DialogTranslation.csv";
        if (!File.Exists(csvPath))
            File.WriteAllText(csvPath, string.Empty);
        string keyPrefix = $"Dialog_{fileName}_";
        List<TranslationRow>? records = null;

        using (StreamReader reader = new(csvPath))
        using (CsvReader csv = new(reader, CultureInfo.InvariantCulture))
        {
            records = csv.GetRecords<TranslationRow>()
                .Where(x => !x.Keys.StartsWith(keyPrefix))
                .ToList();
        }

        using (StreamWriter writer = new(csvPath))
        using (CsvWriter csv = new(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteHeader<TranslationRow>();
            csv.NextRecord();
            int i = 0;
            foreach (Line line in dialogScript.Lines)
            {
                TranslationRow row = new() { Keys = $"{keyPrefix}{i++}", En = line.Text };
                csv.WriteRecord(row);
                line.Text = row.Keys;
                csv.NextRecord();
            }
            foreach (Choice choice in dialogScript.Choices)
            {
                TranslationRow row = new() { Keys = $"{keyPrefix}{i++}", En = choice.Text };
                csv.WriteRecord(row);
                choice.Text = row.Keys;
                csv.NextRecord();
            }
            foreach (var record in records)
            {
                csv.WriteRecord(record);
                csv.NextRecord();
            }
        }
    }

    private void UpdateDoc(DocumentUri uri, string text)
    {
        if (!_documentManager.Documents.TryGetValue(uri, out DialogDocument? doc))
        {
            doc = new(uri);
            _documentManager.Documents[uri] = doc;
        }

        doc.UpdateText(text);
    }

    private void PublishDiagnostics(Dictionary<DocumentUri, CompilationResult> results)
    {
        foreach (var kvp in results)
        {
            PublishDiagnosticsParams diagnostics = new()
            {
                Uri = kvp.Key,
                Diagnostics = kvp.Value.Diagnostics
            };
            _server.TextDocument.PublishDiagnostics(diagnostics);
        }
    }

    private void SetCustomMembersFromFile(string fileName)
    {
        var rootPath = _server.ClientSettings.RootPath;
        if (string.IsNullOrEmpty(rootPath))
            return;
        var files = Directory.GetFiles(rootPath, fileName, SearchOption.AllDirectories);
        if (files.Length != 1)
            return;
        string code = new StreamReader(files[0]).ReadToEnd();
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
        CompilationUnitSyntax? root = tree.GetCompilationUnitRoot();
        var members = root.DescendantNodes().OfType<MemberDeclarationSyntax>();
        foreach (var member in members)
        {
            if (member is PropertyDeclarationSyntax propDeclaration)
            {
                VarType varType = GetVarType(propDeclaration.Type.ToString());
                if (varType == VarType.Undefined)
                    continue;
                VarDef varDef = new(propDeclaration.Identifier.Text, varType);
                _documentManager.MemberRegister.VarDefs.Add(varDef);
            }
            else if (member is MethodDeclarationSyntax methodDeclaration)
            {
                // ignore override methods
                if (methodDeclaration.Modifiers.Any(x => x.Text == "override"))
                    continue;
                FuncDef? funcDef = GetFuncDef(methodDeclaration);
                if (funcDef != null)
                    _documentManager.MemberRegister.FuncDefs.Add(funcDef);
            }
        }
    }

    private VarType GetVarType(string typeName)
    {
        return typeName switch
        {
            "float" => VarType.Float,
            "string" => VarType.String,
            "bool" => VarType.Bool,
            "void" => VarType.Void,
            _ => VarType.Undefined
        };
    }

    private FuncDef? GetFuncDef(MethodDeclarationSyntax node)
    {
        VarType returnType = GetVarType(node.ReturnType.ToString());
        if (returnType == VarType.Undefined)
            return null;
        string funcName = node.Identifier.Text;
        List<Argument> args = new();
        bool argsValid = true;
        foreach (var parameter in node.ParameterList.Parameters)
        {
            if (parameter.Type == null)
                return null;
            VarType paramType = GetVarType(parameter.Type.ToString());
            if (paramType == VarType.Undefined)
                return null;
            args.Add(new(paramType, parameter.Default != null));
        }
        if (!argsValid)
            return null;
        FuncDef funcDef = new(funcName, returnType, args);
        return funcDef;
    }
}
