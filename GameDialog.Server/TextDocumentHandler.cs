using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace GameDialog.Server;

public partial class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    public TextDocumentHandler(ILanguageServerFacade languageServer, ILanguageServerConfiguration configuration)
    {
        _server = languageServer;
        _configuration = configuration;
        _memberRegister = new();
        _parserState = new();
        _validator = new(_parserState, _memberRegister.PredefinedVarDefs, _memberRegister.PredefinedFuncDefs);
    }

    private readonly ILanguageServerFacade _server;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly ParserState _parserState;
    private readonly DialogValidator _validator;
    private readonly MemberRegister _memberRegister;
    private bool _membersInitialized;

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new(uri, Constants.LanguageId);
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken ct)
    {
        if (notification.TextDocument.Uri.Path.EndsWith(".cs"))
            return Unit.Task;

        List<Error> errors = [];
        TrySetMembers();
        _parserState.ReadStringToScript(notification.TextDocument.Text, default);
        _validator.ValidateScript(errors, null);
        PublishDiagnostics(notification.TextDocument.Uri, errors);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken ct)
    {
        if (notification.TextDocument.Uri.Path.EndsWith(".cs"))
            return Unit.Task;

        if (!notification.ContentChanges.Any())
            return Unit.Task;

        try
        {
            List<Error> errors = [];
            TrySetMembers();
            _parserState.ReadStringToScript(notification.ContentChanges.First().Text, default);
            _validator.ValidateScript(errors, null);
            PublishDiagnostics(notification.TextDocument.Uri, errors);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken ct)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken ct)
    {
        if (!notification.TextDocument.Uri.Path.EndsWith(".cs"))
            return Unit.Task;

        GenerateMembersFile(notification.TextDocument.Uri);
        return Unit.Task;
    }

    private void GenerateMembersFile(DocumentUri uri)
    {
        if (!uri.Path.EndsWith(".cs"))
            return;

        TrySetMembers(true);
        return;
    }

    private void TrySetMembers(bool force = false)
    {
        if (_membersInitialized && !force)
            return;

        string rootPath = _server.ClientSettings.RootPath!;
        _memberRegister.SetMembersFromFile(Constants.DialogBridgeName, rootPath, force);
        _membersInitialized = true;
    }

    public IList<string> CreateTranslation(bool isCSV)
    {
        string rootPath = _server.ClientSettings.RootPath!;
        string[] filePaths = Directory.GetFiles(rootPath, "*.dia", SearchOption.AllDirectories);

        if (filePaths.Length == 0)
        {
            _server.Window.ShowError("No .dia files exist in this project.");
            return [];
        }

        string translationDirectory = _configuration[Constants.ConfigTranslationLocation];

        if (string.IsNullOrEmpty(translationDirectory))
            translationDirectory = rootPath;

        if (!Directory.Exists(translationDirectory))
        {
            _server.Window.ShowError("Translation location is invalid. Please check your settings.");
            return [];
        }

        string transPath = $"{translationDirectory}{Path.DirectorySeparatorChar}DialogTranslation";
        transPath += isCSV ? ".csv" : ".pot";
        // TODO: Add default language
        List<string> filesWithErrors = [];
        List<Error> errors = [];
        TrySetMembers();
        using StreamWriter sw = new(transPath);

        if (isCSV)
        {
            _validator.TranslationMode = TranslationFileType.CSV;
            sw.Write("keys,en");
        }
        else
        {
            _validator.TranslationMode = TranslationFileType.POT;
            sw.WriteLine("msgid \"\"");
            sw.WriteLine("msgstr \"\"");
        }

        foreach (string filePath in filePaths)
        {
            _parserState.ReadFileToScript(filePath, rootPath);
            _validator.ValidateScript(errors, null, sw);

            if (errors.Count > 0)
                filesWithErrors.Add(filePath);

            errors.Clear();
        }

        return filesWithErrors;
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
