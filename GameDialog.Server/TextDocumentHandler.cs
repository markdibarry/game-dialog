using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GameDialog.Runner;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace GameDialog.Server;

public partial class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    public TextDocumentHandler(ILanguageServerFacade languageServer, ILanguageServerConfiguration configuration)
    {
        _memberRegister = new();
        ParserState parserState = new();
        ExprParser exprParser = new(parserState);
        _validator = new(parserState, exprParser, _memberRegister.PredefinedVarDefs, _memberRegister.PredefinedFuncDefs);
        _server = languageServer;
        _configuration = configuration;
    }

    private readonly ILanguageServerFacade _server;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly Validator _validator;
    private readonly MemberRegister _memberRegister = new();

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new(uri, Constants.LanguageId);
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken ct)
    {
        if (notification.TextDocument.Uri.Path.EndsWith(".cs"))
            return Unit.Task;

        string rootPath = _server.ClientSettings.RootPath!;
        _memberRegister.SetMembersFromFile(Constants.DialogBridgeName, rootPath, false);
        List<Error> errors = [];
        string[] lines = notification.TextDocument.Text.Split(Environment.NewLine);
        _validator.ValidateScript(lines, errors);
        PublishDiagnostics(notification.TextDocument.Uri, errors);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken ct)
    {
        if (notification.TextDocument.Uri.Path.EndsWith(".cs"))
            return Unit.Task;

        if (!notification.ContentChanges.Any())
            return Unit.Task;

        List<Error> errors = [];
        string[] lines = notification.ContentChanges.First().Text.Split(Environment.NewLine);
        StringBuilder sb = new();
        _validator.ValidateScript(lines, errors, sb);
        PublishDiagnostics(notification.TextDocument.Uri, errors);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken ct)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken ct)
    {
        CompileAndGenerateFile(notification.TextDocument.Uri, notification.Text);
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = new(
                new() { Pattern = "**/*.dia" },
                new() { Pattern = "**/DialogBridge.cs" }),
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions() { IncludeText = true }
        };
    }

    private void CompileAndGenerateFile(DocumentUri uri, string? text)
    {
        string rootPath = _server.ClientSettings.RootPath!;

        if (uri.Path.EndsWith(".cs"))
        {
            _memberRegister.SetMembersFromFile(Constants.DialogBridgeName, rootPath, true);
            return;
        }

        if (string.IsNullOrEmpty(text))
            return;

        string uriPath = uri.GetFileSystemPath();
        string fileName = Path.GetFileNameWithoutExtension(uriPath);
        //string pathDirectory = Path.GetDirectoryName(uriPath) ?? string.Empty;
        //CreateTranslationCSV(fileName, pathDirectory, result.ScriptData);
    }

    public List<string> CompileAndGenerateAllFiles()
    {
        return [];
        // string rootPath = _server.ClientSettings.RootPath!;
        // string[] filePaths = Directory.GetFiles(rootPath, "*.dia", SearchOption.AllDirectories);

        // if (filePaths.Length == 0)
        //     return [];

        // _memberRegister.SetMembersFromFile(Constants.DialogBridgeName, rootPath, false);
        // List<string> filesWithErrors = [];

        // foreach (string filePath in filePaths)
        // {
        //     string fileName = Path.GetFileNameWithoutExtension(filePath);
        //     DocumentUri uri = DocumentUri.FromFileSystemPath(filePath);
        //     string docText = File.ReadAllText(filePath);

        //     if (result.Diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
        //     {
        //         filesWithErrors.Add(filePath);
        //         continue;
        //     }

        //     //string pathDirectory = Path.GetDirectoryName(filePath)!;
        //     //CreateTranslationCSV(fileName, pathDirectory, result.ScriptData);
        // }

        // return filesWithErrors;
    }

    // private void CreateTranslationCSV(string fileName, string pathDirectory, ScriptData scriptData)
    // {
    //     string csvEnabledString = _configuration[Constants.ConfigCSVTranslationEnabled];

    //     if (!bool.TryParse(csvEnabledString, out bool csvEnabled) || !csvEnabled)
    //         return;

    //     string csvDirectory = _configuration[Constants.ConfigCSVTranslationLocation];

    //     if (string.IsNullOrEmpty(csvDirectory))
    //         csvDirectory = pathDirectory;

    //     if (!Directory.Exists(csvDirectory))
    //     {
    //         _server.Window.ShowError($"CSV Translation location is invalid. Please check your settings.");
    //         return;
    //     }

    //     string separateFilesString = _configuration[Constants.ConfigCSVTranslationSeparateFiles];
    //     _ = bool.TryParse(separateFilesString, out bool separateFiles);
    //     string fileSuffix = separateFiles ? $"_{fileName}" : string.Empty;
    //     string csvPath = $"{csvDirectory}{Path.DirectorySeparatorChar}DialogTranslation{fileSuffix}.csv";
    //     string keyPrefix = $"{fileName}_";
    //     // TODO: Add default language
    //     string header = "keys,en";
    //     List<string> records = [];

    //     if (File.Exists(csvPath))
    //     {
    //         string existingHeader = File.ReadLines(csvPath).First();

    //         if (existingHeader.StartsWith("keys,"))
    //             header = existingHeader;

    //         records = File.ReadLines(csvPath)
    //             .Skip(1)
    //             .Where(x => !x.StartsWith(keyPrefix))
    //             .ToList();
    //     }

    //     string commas = new(',', header.Count(x => x == ',') - 1);

    //     for (int i = 0; i < scriptData.DialogStringIndices.Count; i++)
    //     {
    //         string key = keyPrefix + i;
    //         int index = scriptData.DialogStringIndices[i];
    //         string text = scriptData.Strings[index];
    //         records.Add($"{key},{ConvertToCsvCell(text)}{commas}");
    //         scriptData.Strings[index] = key;
    //     }

    //     records.Sort(StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering));
    //     File.WriteAllText(csvPath, header + Environment.NewLine);
    //     File.AppendAllLines(csvPath, records);

    //     static string ConvertToCsvCell(string str)
    //     {
    //         bool mustQuote = RegexCsvEscapable().IsMatch(str);
    //         return mustQuote ? $"\"{str.Replace("\"", "\"\"")}\"" : str;
    //     }
    // }

    [GeneratedRegex("[,\"\\r\\n]")]
    private static partial Regex RegexCsvEscapable();

    private void PublishDiagnostics(DocumentUri uri, List<Error> errors)
    {
        PublishDiagnosticsParams diagnostics = new()
        {
            Uri = uri,
            Diagnostics = errors
                .ConvertAll(x => new Diagnostic()
                {
                    Source = uri.Path,
                    Range = new(x.Line, x.Start, x.Line, x.End),
                    Message = x.Message,
                    Severity = DiagnosticSeverity.Error
                })
        };
        _server.TextDocument.PublishDiagnostics(diagnostics);
    }
}
