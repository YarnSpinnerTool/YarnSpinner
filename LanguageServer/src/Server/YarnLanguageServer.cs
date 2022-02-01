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
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

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

            // Register 'Add Nodes' command
            options.OnExecuteCommand<TextDocumentEdit>(
                (commandParams) => AddNodeToDocumentAsync(workspace, commandParams),
                (_, _) => new ExecuteCommandRegistrationOptions
                {
                    Commands = new[] { Commands.AddNode },
                }
            );

            // Register 'Remove Node' command
            options.OnExecuteCommand<TextDocumentEdit>(
                (commandParams) => RemoveNodeFromDocumentAsync(workspace, commandParams),
                (_, _) => new ExecuteCommandRegistrationOptions
                {
                    Commands = new[] { Commands.RemoveNode },
                }
            );

            // Register 'Update Header' command
            options.OnExecuteCommand<TextDocumentEdit>(
                (commandParams) => UpdateNodeHeaderAsync(workspace, commandParams),
                (_, _) => new ExecuteCommandRegistrationOptions
                {
                    Commands = new[] { Commands.UpdateNodeHeader },
                }
            );

            return options;
        }

        private static Task<TextDocumentEdit> AddNodeToDocumentAsync(Workspace workspace, ExecuteCommandParams<TextDocumentEdit> commandParams)
        {
            var yarnDocumentUriString = commandParams.Arguments[0].ToString();

            var headers = new Dictionary<string, string>();

            if (commandParams.Arguments.Count >= 2) {
                var headerObject = commandParams.Arguments[1] as JObject;

                foreach (var property in headerObject) {
                    headers.Add(property.Key, property.Value.ToString());
                }
            }

            Uri yarnDocumentUri = new (yarnDocumentUriString);

            if (workspace.YarnFiles.TryGetValue(yarnDocumentUri, out var yarnFile) == false)
            {
                // Try and add this file to the workspace
                yarnFile = workspace.OpenFile(yarnDocumentUri);
                if (yarnFile == null)
                {
                    // Failed to open it. Return no change.
                    return Task.FromResult(new TextDocumentEdit
                    {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier
                        {
                            Uri = yarnDocumentUri,
                        },
                        Edits = new List<TextEdit>(),
                    });
                }
            }

            // Work out the edit needed to add a node.

            // Figure out the name of the new node.
            var allNodeTitles = workspace.YarnFiles.Values.SelectMany(yf => yf.NodeInfos).Select(n => n.Title);

            var candidateCount = 0;
            var candidateName = "Node";

            while (allNodeTitles.Contains(candidateName))
            {
                candidateCount += 1;
                candidateName = $"Node{candidateCount}";
            }

            var newNodeText = new System.Text.StringBuilder()
                .AppendLine($"title: {candidateName}");
           
            // Add the headers
            foreach (var h in headers) {
                newNodeText.AppendLine($"{h.Key}: {h.Value}");
            }

            newNodeText
                .AppendLine("---")
                .AppendLine()
                .AppendLine("===");

            Position position;

            // First, are there any nodes at all?
            if (yarnFile.NodeInfos.Count == 0)
            {
                // No nodes. Add one at the start.
                position = new Position(0, 0);
            }
            else
            {
                var lastLineIsEmpty = yarnFile.Text.EndsWith('\n');

                int lastLineIndex = yarnFile.LineCount - 1;

                if (lastLineIsEmpty)
                {
                    // The final line ends with a newline. Insert the node
                    // there.
                    position = new Position(lastLineIndex, 0);
                }
                else
                {
                    // The final line does not end with a newline. Insert a
                    // newline at the end of the last line, followed by the new
                    // text.
                    var endOfLastLine = yarnFile.GetLineLength(lastLineIndex);
                    newNodeText.Insert(0, Environment.NewLine);
                    position = new Position(lastLineIndex, endOfLastLine);
                }
            }

            // Return the edit that adds this node
            return Task.FromResult(new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = yarnDocumentUri,
                },
                Edits = new[] {
                    new TextEdit {
                        Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(position, position),
                        NewText = newNodeText.ToString(),
                    },
                },
            });
        }

        private static Task<TextDocumentEdit> RemoveNodeFromDocumentAsync(Workspace workspace, ExecuteCommandParams<TextDocumentEdit> commandParams)
        {
            var yarnDocumentUriString = commandParams.Arguments[0].ToString();

            var nodeTitle = commandParams.Arguments[1].ToString();

            Uri yarnDocumentUri = new (yarnDocumentUriString);

            TextDocumentEdit emptyResult = new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = yarnDocumentUri,
                },
                Edits = new List<TextEdit>(),
            };

            if (workspace.YarnFiles.TryGetValue(yarnDocumentUri, out var yarnFile) == false)
            {
                // Try and add this file to the workspace
                yarnFile = workspace.OpenFile(yarnDocumentUri);
                if (yarnFile == null)
                {
                    workspace.LanguageServer.Window.ShowMessage(new ShowMessageParams
                    {
                        Message = $"Can't remove node: failed to open file ${yarnDocumentUri}",
                        Type = MessageType.Error,
                    });

                    // Failed to open it. Return no change.
                    return Task.FromResult(emptyResult);
                }
            }

            // First: does this file contain a node with this title?
            var nodes = yarnFile.NodeInfos.Where(n => n.Title == nodeTitle);

            if (nodes.Count() != 1) {
                // We need precisely 1 node to remove.
                var multipleNodesMessage = $"multiple nodes named {nodeTitle} exist in this file";
                var noNodeMessage = $"no node named {nodeTitle} exists in this file";
                workspace.LanguageServer.Window.ShowMessage(new ShowMessageParams
                {
                    Message = $"Can't remove node: {(nodes.Any() ? multipleNodesMessage : noNodeMessage)}. Modify the source code directly.",
                    Type = MessageType.Error,
                });
                return Task.FromResult(emptyResult);
            }

            var node = nodes.Single();

            // Work out the edit needed to remove the node.
            var deletionStart = new Position(node.HeaderStartLine, 0);

            // Stop deleting at the start of the line after the end-of-body
            // delimiter (which is 2 lines down from the final line of body
            // text)
            var deletionEnd = new Position(node.BodyEndLine + 2, 0);

            // Return the edit that removes this node
            return Task.FromResult(new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = yarnDocumentUri,
                },
                Edits = new[] {
                    new TextEdit {
                        Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(deletionStart, deletionEnd),
                        NewText = string.Empty,
                    },
                },
            });
        }

        private static Task<TextDocumentEdit> UpdateNodeHeaderAsync(Workspace workspace, ExecuteCommandParams<TextDocumentEdit> commandParams)
        {
            var yarnDocumentUriString = commandParams.Arguments[0].ToString();

            var nodeTitle = commandParams.Arguments[1].ToString();

            var headerKey = commandParams.Arguments[2].ToString();

            var headerValue = commandParams.Arguments[3].ToString();

            Uri yarnDocumentUri = new (yarnDocumentUriString);

            TextDocumentEdit emptyResult = new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = yarnDocumentUri,
                },
                Edits = new List<TextEdit>(),
            };

            if (workspace.YarnFiles.TryGetValue(yarnDocumentUri, out var yarnFile) == false)
            {
                // Try and add this file to the workspace
                yarnFile = workspace.OpenFile(yarnDocumentUri);
                if (yarnFile == null)
                {
                    // Failed to open it. Return no change.
                    return Task.FromResult(emptyResult);
                }
            }

            // Does this file contain a node with this title?
            var nodes = yarnFile.NodeInfos.Where(n => n.Title == nodeTitle);

            if (nodes.Count() != 1) {
                // We need precisely 1 node to modify.
                var multipleNodesMessage = $"multiple nodes named {nodeTitle} exist in this file";
                var noNodeMessage = $"no node named {nodeTitle} exists in this file";
                workspace.LanguageServer.Window.ShowMessage(new ShowMessageParams
                {
                    Message = $"Can't update header node: {(nodes.Any() ? multipleNodesMessage : noNodeMessage)}. Modify the source code directly.",
                    Type = MessageType.Error,
                });
                return Task.FromResult(emptyResult);
            }

            var node = nodes.Single();

            // Does this node contain a header with this title?
            var existingHeader = node.Headers.Find(h => h.Key == headerKey);

            var headerText = $"{headerKey}: {headerValue}";

            Position startPosition;
            Position endPosition;

            if (existingHeader != null) {
                // Create an edit to replace it
                var line = existingHeader.KeyToken.Line - 1;
                startPosition = new Position(line, 0);
                endPosition = new Position(line, yarnFile.GetLineLength(line));
            } else {
                // Create an edit to insert it immediately before the body start
                // delimiter
                var line = node.BodyStartLine - 1;
                startPosition = new Position(line, 0);
                endPosition = new Position(line, 0);
                
                // Add a newline so that the delimiter stays on its own line
                headerText += Environment.NewLine;
            }

            // Return the edit that creates or updates this header
            return Task.FromResult(new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = yarnDocumentUri,
                },
                Edits = new[] {
                    new TextEdit {
                        Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(startPosition, endPosition),
                        NewText = headerText,
                    },
                },
            });
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
