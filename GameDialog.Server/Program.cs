using OmniSharp.Extensions.LanguageServer.Server;

namespace GameDialog.Server;

public class GameDialogServer
{
    private static async Task Main(string[] args)
    {
        LanguageServerOptions options = new LanguageServerOptions()
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithHandler<TextDocumentHandler>();
        var server = await LanguageServer.From(options).ConfigureAwait(false);
        await server.WaitForExit;
    }
}