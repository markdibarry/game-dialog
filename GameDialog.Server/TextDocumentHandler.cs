﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GameDialog.Compiler;
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
        _compiler = new();
        _server = languageServer;
        _configuration = configuration;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILanguageServerFacade _server;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly DialogCompiler _compiler = new();

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new(uri, Constants.LanguageId);
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken ct)
    {
        _compiler.ClearMemberRegister();
        string rootPath = _server.ClientSettings.RootPath!;
        _compiler.MemberRegister.SetMembersFromFile(Constants.DialogBridgeBaseName, rootPath, false);
        _compiler.MemberRegister.SetMembersFromFile(Constants.DialogBridgeName, rootPath, true);
        _compiler.UpdateDoc(notification.TextDocument.Uri, notification.TextDocument.Text);
        Dictionary<DocumentUri, CompilationResult> results = _compiler.Compile();
        PublishDiagnostics(results);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken ct)
    {
        if (!notification.ContentChanges.Any())
            return Unit.Task;

        _compiler.UpdateDoc(notification.TextDocument.Uri, notification.ContentChanges.First().Text);
        Dictionary<DocumentUri, CompilationResult> results = _compiler.Compile();
        PublishDiagnostics(results);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken ct)
    {
        _compiler.RemoveDoc(notification.TextDocument.Uri);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(notification.Text))
            return Unit.Task;

        _compiler.ClearMemberRegister();
        string rootPath = _server.ClientSettings.RootPath!;
        _compiler.MemberRegister.SetMembersFromFile(Constants.DialogBridgeBaseName, rootPath, false);
        _compiler.MemberRegister.SetMembersFromFile(Constants.DialogBridgeName, rootPath, true);
        DocumentUri uri = notification.TextDocument.Uri;
        _compiler.UpdateDoc(uri, notification.Text);

        if (!_compiler.Documents.TryGetValue(uri, out DialogDocument? doc))
        {
            _server.Window.ShowError($"No JSON document was generated. File {uri} not found.");
            return Unit.Task;
        }

        CompilationResult result = _compiler.Compile(doc);

        // Do not generate files with errors
        if (result.Diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
        {
            _server.Window.ShowError($"No JSON document was generated. File {uri} contains errors.");
            return Unit.Task;
        }

        string uriPath = uri.GetFileSystemPath();
        string fileName = Path.GetFileNameWithoutExtension(uriPath);
        string pathDirectory = Path.GetDirectoryName(uriPath) ?? string.Empty;
        CreateTranslationCSV(fileName, pathDirectory, result.ScriptData);
        CreateJsonFile(fileName, pathDirectory, result.ScriptData);

        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = new(new TextDocumentFilter() { Pattern = "**/*.dia" }),
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions() { IncludeText = true }
        };
    }

    private static void CreateJsonFile(string fileName, string pathDirectory, ScriptData scriptData)
    {
        string jsonFilePath = $"{pathDirectory}{Path.DirectorySeparatorChar}{fileName}.json";
        string jsonString = JsonSerializer.Serialize(scriptData, _jsonOptions);
        File.WriteAllText(jsonFilePath, jsonString);
    }

    private void CreateTranslationCSV(string fileName, string pathDirectory, ScriptData scriptData)
    {
        string csvEnabledString = _configuration[Constants.ConfigCSVTranslationEnabled];

        if (!bool.TryParse(csvEnabledString, out bool csvEnabled) || !csvEnabled)
            return;

        string csvDirectory = _configuration[Constants.ConfigCSVTranslationLocation];

        if (string.IsNullOrEmpty(csvDirectory))
            csvDirectory = pathDirectory;

        if (!Directory.Exists(csvDirectory))
        {
            _server.Window.ShowError($"CSV Translation location is invalid. Please check your settings.");
            return;
        }

        string separateFilesString = _configuration[Constants.ConfigCSVTranslationSeparateFiles];
        _ = bool.TryParse(separateFilesString, out bool separateFiles);
        string fileSuffix = separateFiles ? $"_{fileName}" : string.Empty;
        string csvPath = $"{csvDirectory}{Path.DirectorySeparatorChar}DialogTranslation{fileSuffix}.csv";
        string keyPrefix = $"{fileName}_";
        // TODO: Add default language
        string header = "keys,en";
        List<string> records = [];

        if (File.Exists(csvPath))
        {
            string existingHeader = File.ReadLines(csvPath).First();

            if (existingHeader.StartsWith("keys,"))
                header = existingHeader;

            records = File.ReadLines(csvPath)
                .Skip(1)
                .Where(x => !x.StartsWith(keyPrefix))
                .ToList();
        }

        string commas = new(',', header.Count(x => x == ',') - 1);

        for (int i = 0; i < scriptData.DialogStringIndices.Count; i++)
        {
            string key = keyPrefix + i;
            int index = scriptData.DialogStringIndices[i];
            string text = scriptData.Strings[index];
            records.Add($"{key},{ConvertToCsvCell(text)}{commas}");
            scriptData.Strings[index] = key;
        }

        records.Sort(new NumericStringComparer());
        File.WriteAllText(csvPath, header + Environment.NewLine);
        File.AppendAllLines(csvPath, records);

        static string ConvertToCsvCell(string str)
        {
            bool mustQuote = RegexCsvEscapable().IsMatch(str); ;
            return mustQuote ? $"\"{str.Replace("\"", "\"\"")}\"" : str;
        }
    }

    [GeneratedRegex("[,\"\\r\\n]")]
    private static partial Regex RegexCsvEscapable();

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

    /// <summary>
    /// Temporary until Numeric String sorting is introduced in .NET 10.
    /// </summary>
    private class NumericStringComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xParts = Regex.Split(x, @"(\d+)");
            var yParts = Regex.Split(y, @"(\d+)");

            for (int i = 0; i < Math.Min(xParts.Length, yParts.Length); i++)
            {
                int result;
                // Compare numeric parts as integers
                if (int.TryParse(xParts[i], out int xNum) && int.TryParse(yParts[i], out int yNum))
                {
                    result = xNum.CompareTo(yNum);
                }
                else
                {
                    // Compare non-numeric parts as strings
                    result = string.Compare(xParts[i], yParts[i], StringComparison.Ordinal);
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return xParts.Length.CompareTo(yParts.Length);
        }
    }
}
