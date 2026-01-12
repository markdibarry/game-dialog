using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;

namespace GameDialog.Server;

public static class GameDialogServer
{
    private static async Task Main(string[] args)
    {
        LanguageServerOptions options = new LanguageServerOptions()
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(x => x.AddSingleton<TextDocumentHandler>())
            .WithHandler<TextDocumentHandler>()
            .WithHandler<NotificationHandler>();
        var server = await LanguageServer.From(options).ConfigureAwait(false);
        await server.WaitForExit;
    }
}