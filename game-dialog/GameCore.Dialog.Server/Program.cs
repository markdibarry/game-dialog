using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Server;

namespace GameCore.Dialog.Server;

public static class GameDialogServer
{
    public static TaskCompletionSource FirstTimeTCS { get; set; } = new();

    private static async Task Main(string[] args)
    {
        var server = await LanguageServer
            .From(o => o
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithServices(x => x.AddSingleton(x => new DialogRunner(null!)))
                .WithHandler<TextDocumentHandler>()
                .WithHandler<UpdateMembersHandler>()
                .WithHandler<GenerateTranslationHandler>()
                .OnInitialize(async (server, request, ct) =>
                {
                    try
                    {
                        await UpdateMembersHandler.CollectMembersAsync(request.RootPath!, ct);
                    }
                    finally
                    {
                        FirstTimeTCS.SetResult();
                    }
                }))
            .ConfigureAwait(false);
        await server.WaitForExit;
    }
}
