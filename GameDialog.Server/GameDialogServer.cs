using GameDialog.Compiler;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using Microsoft.Extensions.Logging;

namespace GameDialog.Server;

public class GameDialogServer
{
    private static void Main(string[] args)
    {
        MainAsync(args).Wait();
    }

    private static async Task MainAsync(string[] args)
    {
        LanguageServerOptions options = new LanguageServerOptions()
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithHandler<TextDocumentHandler>();
        options.OnInitialize(
            async (server, request, token) =>
            {
                try
                {
                    server.Log("Initializing the server...");
                    await Task.CompletedTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    server.Window.ShowError($"Server init failed. {ex}");
                    await Task.FromException(ex).ConfigureAwait(false);
                }
            });
        options.OnInitialized(
            async (server, request, response, token) =>
            {
                server.Log("Initialized!");
                await Task.CompletedTask.ConfigureAwait(false);
            });
        options.OnStarted(
            async (server, token) =>
            {
                server.Log("Started!");
                await Task.CompletedTask.ConfigureAwait(false);
            });
        LanguageServer server = await LanguageServer.From(options).ConfigureAwait(false);
        await server.WaitForExit.ConfigureAwait(false);
    }
}