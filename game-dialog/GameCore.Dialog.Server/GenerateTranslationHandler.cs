using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace GameCore.Dialog.Server;

public class GenerateTranslationHandler : IJsonRpcRequestHandler<GenerateTranslationRequest, GenerateTranslationResponse>
{
    public GenerateTranslationHandler(ILanguageServerFacade server, ILanguageServerConfiguration configuration, DialogRunner dialogRunner)
    {
        _server = server;
        _configuration = configuration;
        _dialogRunner = dialogRunner;
    }

    private readonly ILanguageServerFacade _server;
    private readonly ILanguageServerConfiguration _configuration;
    private readonly DialogRunner _dialogRunner;

    public async Task<GenerateTranslationResponse> Handle(GenerateTranslationRequest request, CancellationToken cancellationToken)
    {
        List<string> filesWithErrors = CreateTranslation(request.IsCSV);
        return new() { Data = filesWithErrors };
    }

    private List<string> CreateTranslation(bool isCSV)
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
        using StreamWriter sw = new(transPath);

        if (isCSV)
        {
            DialogRunner.TranslationFileType = TranslationFileType.CSV;
            sw.Write("keys,en");
        }
        else
        {
            DialogRunner.TranslationFileType = TranslationFileType.POT;
            sw.WriteLine("msgid \"\"");
            sw.WriteLine("msgstr \"\"");
        }

        foreach (string filePath in filePaths)
        {
            _dialogRunner.LoadFromFile(filePath, rootPath);
            _dialogRunner.ValidateScript(errors, null, sw);

            if (errors.Count > 0)
                filesWithErrors.Add(filePath);

            errors.Clear();
        }

        return filesWithErrors;
    }

}

[Method("dialog/generateTranslation")]
public class GenerateTranslationRequest : IRequest<GenerateTranslationResponse>
{
    public bool IsCSV { get; set; }
}

public class GenerateTranslationResponse
{
    public IList<string> Data { get; set; } = [];
}