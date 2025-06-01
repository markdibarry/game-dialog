using System.Text.Json;
using System.Text.RegularExpressions;
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
        _server = languageServer;
        _configuration = configuration;
    }

    private readonly ILanguageServerFacade _server;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly DialogCompiler _compiler = new();
    private readonly TextDocumentSelector _documentSelector = new(new TextDocumentFilter() { Pattern = "**/*.dia" });
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new(uri, Constants.LanguageId);
    }

    public override Task<Unit> Handle(
        DidOpenTextDocumentParams notification,
        CancellationToken cancellationToken)
    {
        _compiler.ClearMemberRegister();
        string rootPath = _server.ClientSettings.RootPath!;
        _compiler.MemberRegister.SetMembersFromFile(Constants.DialogBridgeBaseName, rootPath);
        _compiler.MemberRegister.SetMembersFromFile(Constants.DialogBridgeName, rootPath);
        _compiler.UpdateDoc(notification.TextDocument.Uri, notification.TextDocument.Text);
        Dictionary<DocumentUri, CompilationResult> results = _compiler.Compile();
        PublishDiagnostics(results);
        return Unit.Task;
    }

    public override Task<Unit> Handle(
        DidChangeTextDocumentParams notification,
        CancellationToken cancellationToken)
    {
        if (!notification.ContentChanges.Any())
            return Unit.Task;

        _compiler.UpdateDoc(notification.TextDocument.Uri, notification.ContentChanges.First().Text);
        Dictionary<DocumentUri, CompilationResult> results = _compiler.Compile();
        PublishDiagnostics(results);
        return Unit.Task;
    }

    public override Task<Unit> Handle(
        DidCloseTextDocumentParams notification,
        CancellationToken cancellationToken)
    {
        return Unit.Task;
    }

    public override Task<Unit> Handle(
        DidSaveTextDocumentParams notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(notification.Text))
            return Unit.Task;

        _compiler.UpdateDoc(notification.TextDocument.Uri, notification.Text);
        Dictionary<DocumentUri, CompilationResult> results = _compiler.Compile();

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

            if (bool.TryParse(_configuration[Constants.ConfigCSVTranslationEnabled], out bool csvEnabled) && csvEnabled)
            {
                string csvDirectory = _configuration[Constants.ConfigCSVTranslationLocation];
                _ = bool.TryParse(_configuration[Constants.ConfigCSVTranslationSeparateFiles], out bool separateFiles);

                if (string.IsNullOrEmpty(csvDirectory))
                    csvDirectory = pathDirectory;

                if (!Directory.Exists(csvDirectory))
                {
                    _server.Window.ShowError($"CSV Translation location is invalid. Please check your settings.");
                    continue;
                }

                CreateTranslationCSV(fileName, csvDirectory, kvp.Value.ScriptData, separateFiles);
            }

            CreateJsonFile(fileName, pathDirectory, kvp.Value.ScriptData);
        }

        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions()
        {
            DocumentSelector = _documentSelector,
            Change = Change,
            Save = new SaveOptions() { IncludeText = true }
        };
    }

    private static void CreateJsonFile(string fileName, string pathDirectory, ScriptData scriptData)
    {
        string jsonFilePath = $"{pathDirectory}{Path.DirectorySeparatorChar}{fileName}.json";
        string jsonString = JsonSerializer.Serialize(scriptData, _jsonOptions);
        File.WriteAllText(jsonFilePath, jsonString);
    }

    private static void CreateTranslationCSV(
        string fileName,
        string pathDirectory,
        ScriptData scriptData,
        bool separateFiles)
    {
        string fileSuffix = separateFiles ? $"_{fileName}" : string.Empty;
        string csvPath = $"{pathDirectory}{Path.DirectorySeparatorChar}DialogTranslation{fileSuffix}.csv";
        string keyPrefix = $"Dialog_{fileName}_";
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

        for (int i = 0; i < scriptData.Strings.Count; i++)
        {
            string key = keyPrefix + i;
            string text = scriptData.Strings[i];
            records.Add($"{key},{ConvertToCsvCell(text)}{commas}");
            scriptData.Strings[i] = key;
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

    [GeneratedRegex("[,\"\\r\\n]")]
    private static partial Regex RegexCsvEscapable();

    public class NumericStringComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // Split the strings into parts (numbers and non-numbers)
            var xParts = System.Text.RegularExpressions.Regex.Split(x, @"(\d+)");
            var yParts = System.Text.RegularExpressions.Regex.Split(y, @"(\d+)");

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
