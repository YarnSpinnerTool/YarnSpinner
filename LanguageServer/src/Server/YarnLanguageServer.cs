using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;

namespace YarnLanguageServer
{
    public class YarnLanguageServer
    {

        public static ILanguageServer server;

        private static async Task Main(string[] args)
        {
            if (args.Contains("--waitForDebugger"))
            {
                while (!Debugger.IsAttached) { await Task.Delay(100); }
            }

            server = await LanguageServer.From(
                options => ConfigureOptions(options)
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
            );

            await server.WaitForExit;
        }

        public static LanguageServerOptions ConfigureOptions(LanguageServerOptions options)
        {
            var workspace = new Workspace();

            options
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
                .WithHandler<Handlers.RenameHandler>()
                .WithHandler<Handlers.FileOperationsHandler>()
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
                })

                ;

            // Register 'List Nodes' command
            options.OnExecuteCommand<Container<NodeInfo>>(
                (commandParams) => ListNodesInDocumentAsync(workspace, commandParams),
                (_, _) => new ExecuteCommandRegistrationOptions
                {
                    Commands = new[] { Commands.ListNodes },
                }
            );

            return options;
        }

        private static Task<Container<NodeInfo>> ListNodesInDocumentAsync(Workspace workspace, ExecuteCommandParams<Container<NodeInfo>> commandParams)
        {
            var result = new List<NodeInfo>();

            var yarnDocumentUriString = commandParams.Arguments[0].ToString();

            Uri yarnDocumentUri = new (yarnDocumentUriString);

            if (workspace.YarnFiles.TryGetValue(yarnDocumentUri, out var yarnFile))
            {
                result = yarnFile.NodeInfos.ToList();
            }

            return Task.FromResult<Container<NodeInfo>>(result);
        }
    }
}
