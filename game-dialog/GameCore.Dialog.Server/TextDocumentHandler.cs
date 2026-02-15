using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Dialog;
using MediatR;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace GameCore.Dialog.Server;

public partial class TextDocumentHandler : TextDocumentSyncHandlerBase
{
    public TextDocumentHandler(ILanguageServerFacade languageServer, DialogRunner dialogRunner)
    {
        _server = languageServer;
        _dialogRunner = dialogRunner;
    }

    private readonly ILanguageServerFacade _server;
    private readonly DialogRunner _dialogRunner;

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new(uri, Constants.LanguageId);
    }

    public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken ct)
    {
        if (!GameDialogServer.FirstTimeTCS.Task.IsCompleted)
            await GameDialogServer.FirstTimeTCS.Task;

        if (UpdateMembersHandler.Processing)
            return Unit.Value;

        List<Error> errors = [];
        _dialogRunner.LoadFromText(notification.TextDocument.Text);
        _dialogRunner.ValidateScript(errors);
        PublishDiagnostics(notification.TextDocument.Uri, errors);
        return Unit.Value;
    }

    public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken ct)
    {
        if (!notification.ContentChanges.Any())
            return Unit.Value;

        try
        {
            if (!GameDialogServer.FirstTimeTCS.Task.IsCompleted)
                await GameDialogServer.FirstTimeTCS.Task;

            if (UpdateMembersHandler.Processing)
                return Unit.Value;

            List<Error> errors = [];
            _dialogRunner.LoadFromText(notification.ContentChanges.First().Text);
            _dialogRunner.ValidateScript(errors);
            PublishDiagnostics(notification.TextDocument.Uri, errors);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return Unit.Value;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken ct)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken ct)
    {
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = new TextDocumentSelector(new TextDocumentFilter() { Pattern = "**/*.dia" }),
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
                .ConvertAll(x => new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic()
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
