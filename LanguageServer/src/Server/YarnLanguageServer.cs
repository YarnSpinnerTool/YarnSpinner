using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace YarnLanguageServer
{
    internal class YarnLanguageServer
    {
        private static ILanguageServer server;

        private static async Task Main(string[] args)
        {
             // while (!Debugger.IsAttached){ await Task.Delay(100); }
             var workspace = new Workspace();

             server = await LanguageServer.From(
                options => options
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .WithServices(services => services.AddSingleton(workspace))
                    .WithHandler<Handlers.TextDocumentHandler>()
                    .WithHandler<Handlers.DocumentSymbolHandler>()
                    .WithHandler<Handlers.WorkspaceSymbolHandler>()
                    .WithHandler<Handlers.SemanticTokensHandler>()
                    .WithHandler<Handlers.DefinitionHandler>()
                    .WithHandler<Handlers.CodeLensHandler>()
                    .WithHandler<Handlers.ReferencesHandler>()
                    .WithHandler<Handlers.CompletionHandler>()
                    .WithHandler<Handlers.SignatureHelpHandler>()
                    .WithHandler<Handlers.HoverHandler>()
                    .WithHandler<Handlers.ConfigurationHandler>()
                    .WithHandler<Handlers.CodeActionHandler>()
                    .OnInitialize(async (server, request, token) =>
                    {
                        workspace.Root = request.RootPath;

                        // avoid re-initializing if possible by getting config settings in early
                        if (request.InitializationOptions != null)
                        {
                            workspace.Configuration.Initialize(request.InitializationOptions as Newtonsoft.Json.Linq.JArray);
                        }

                        workspace.Initialize(server);

                        await Task.CompletedTask;
                    })
                    .OnInitialized(async (server, request, response, token) =>
                    {
                        await Task.CompletedTask;
                    })
                    .OnStarted(async (server, token) =>
                    {
                        await Task.CompletedTask;
                    }));

             await server.WaitForExit;
        }
    }
}
