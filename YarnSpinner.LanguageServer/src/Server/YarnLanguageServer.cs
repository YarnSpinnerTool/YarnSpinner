using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Server;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LanguageServer.Tests")]

namespace YarnLanguageServer
{
    public class YarnLanguageServer
    {

        public static ILanguageServer server;

        private static async Task Main(string[] args)
        {
            if (args.Contains("--waitForDebugger"))
            {
                while (!Debugger.IsAttached) { await Task.Delay(100).ConfigureAwait(false); }
            }

            server = await LanguageServer.From(
                options => ConfigureOptions(options)
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
            ).ConfigureAwait(false);

            await server.WaitForExit.ConfigureAwait(false);
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
                    try {
                        workspace.Root = request.RootPath;

                        server.Log("Server initialize.");

                        // avoid re-initializing if possible by getting config settings in early
                        if (request.InitializationOptions != null)
                        {
                            workspace.Configuration.Initialize(request.InitializationOptions as Newtonsoft.Json.Linq.JArray);
                        }
                        
                        workspace.Initialize(server);
                        await Task.CompletedTask.ConfigureAwait(false);
                    } catch (Exception e) {
                        server.Window.ShowError($"Yarn Spinner language server failed to start: {e}");
                        await Task.FromException(e).ConfigureAwait(false);
                    }
                })
                .OnInitialized(async (server, request, response, token) =>
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                })
                .OnStarted(async (server, token) =>
                {
                    await Task.CompletedTask.ConfigureAwait(false);
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

            // Register 'Compile' command
            options.OnExecuteCommand<CompilerOutput>(
                (commandParams) => CompileCurrentProject(workspace, commandParams),
                (_, _) => new ExecuteCommandRegistrationOptions
                {
                    Commands = new[] { Commands.CompileCurrentProject },
                }
            );

            // Register 'extract voiceovers' command
            options.OnExecuteCommand<VOStringExport>(
                (commandParams) => ExtractVoiceoverSpreadsheet(workspace, commandParams), (_,_) => new ExecuteCommandRegistrationOptions
                {
                    Commands = new[] { Commands.ExtractSpreadsheet },
                }
            );

            // register graph dialogue command
            options.OnExecuteCommand<string>(
                (commandParams) => GenerateDialogueGraph(workspace, commandParams), (_,_) => new ExecuteCommandRegistrationOptions
                {
                    Commands = new[] { Commands.CreateDialogueGraph },
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

            var project = workspace.GetProjectsForUri(yarnDocumentUri).FirstOrDefault();
            var yarnFile = project?.GetFileData(yarnDocumentUri);

            if (yarnFile == null)
            {
                workspace.ShowMessage($"Can't add node: {yarnDocumentUri} is not a part of any project", MessageType.Error);

                // We don't know what this file is, and no project claims it.
                // Return no change.
                return Task.FromResult(new TextDocumentEdit
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier
                    {
                        Uri = yarnDocumentUri,
                    },
                    Edits = new List<TextEdit>(),
                });
            
            }

            // Work out the edit needed to add a node.

            // Figure out the name of the new node.
            var allNodeTitles = project.Files.SelectMany(yf => yf.NodeInfos).Select(n => n.Title);

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

            var project = workspace.GetProjectsForUri(yarnDocumentUri).FirstOrDefault();
            var yarnFile = project?.GetFileData(yarnDocumentUri);

            if (yarnFile == null)
            {
                workspace.ShowMessage($"Can't remove node: {yarnDocumentUri} is not a part of any project", MessageType.Error);

                // Failed to open it. Return no change.
                return Task.FromResult(emptyResult);
            }

            // First: does this file contain a node with this title?
            var nodes = yarnFile.NodeInfos.Where(n => n.Title == nodeTitle);

            if (nodes.Count() != 1) {
                // We need precisely 1 node to remove.
                var multipleNodesMessage = $"multiple nodes named {nodeTitle} exist in this file";
                var noNodeMessage = $"no node named {nodeTitle} exists in this file";

                workspace.ShowMessage(
                    $"Can't remove node: {(nodes.Any() ? multipleNodesMessage : noNodeMessage)}. Modify the source code directly.",
                    MessageType.Error
                );

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

            var project = workspace.GetProjectsForUri(yarnDocumentUri).FirstOrDefault();
            var yarnFile = project?.GetFileData(yarnDocumentUri);

            if (yarnFile == null)
            {
                workspace.ShowMessage($"Can't add header: {yarnDocumentUri} is not a part of any project", MessageType.Error);

                // Failed to get the Yarn file. Return no change.
                return Task.FromResult(emptyResult);
            }

            // Does this file contain a node with this title?
            var nodes = yarnFile.NodeInfos.Where(n => n.Title == nodeTitle);

            if (nodes.Count() != 1) {
                // We need precisely 1 node to modify.
                var multipleNodesMessage = $"multiple nodes named {nodeTitle} exist in this file";
                var noNodeMessage = $"no node named {nodeTitle} exists in this file";
                workspace.ShowMessage(
                    $"Can't update header node: {(nodes.Any() ? multipleNodesMessage : noNodeMessage)}. Modify the source code directly.",
                    MessageType.Error
                );
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

            var project = workspace.GetProjectsForUri(yarnDocumentUri).FirstOrDefault ();

            if (project == null) {
                // We don't have a project for this file. Return the empty collection.
                return Task.FromResult(new Container<NodeInfo>());
            }

            var yarnFile = project.GetFileData(yarnDocumentUri);

            if (yarnFile != null)
            {
                result = yarnFile.NodeInfos.ToList();
            }

            return Task.FromResult<Container<NodeInfo>>(result);
        }

        private static Task<CompilerOutput> CompileCurrentProject(Workspace workspace, ExecuteCommandParams<CompilerOutput> commandParams)
        {
            if (commandParams.Arguments == null) {
                throw new ArgumentException(Commands.CompileCurrentProject + " expects arguments");
            }

            var projectOrDocumentUri = new Uri(commandParams.Arguments[0].ToString());

            // TODO: Handle what to do when multiple projects match the given
            // URL. Right now, this just errors.
            var project = workspace.GetProjectsForUri(projectOrDocumentUri).Single();

            // Recompile the project, and indicate that we'd like full
            // compilation (which will produce the compiled program's bytecode.)
            // This will also have the effect of updating the workspace's
            // diagnostics.
            var result = project.CompileProject(true, Yarn.Compiler.CompilationJob.Type.FullCompilation);

            var errors = result.Diagnostics.Where(d => d.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error).Select(d => d.ToString());

            if (errors.Any())
            {
                // The compilation produced errors. Return a failed compilation.
                workspace.ShowMessage("Compilation failed. See the Problems tab for details.", MessageType.Error);

                return Task.FromResult(new CompilerOutput
                {
                    Data = Array.Empty<byte>(),
                    StringTable = new Dictionary<string, string>(),
                    Errors = errors.ToArray(),
                });
            }

            var strings = new Dictionary<string, string>();
            foreach (var line in result.StringTable)
            {
                strings[line.Key] = line.Value.text;
            }

            return Task.FromResult(new CompilerOutput
            {
                Data = result.Program?.ToByteArray() ?? Array.Empty<byte>(),
                StringTable = strings,
                Errors = Array.Empty<string>(),
            });
        }

        private static Task<string> GenerateDialogueGraph(Workspace workspace, ExecuteCommandParams<string> commandParams)
        {
            if (commandParams.Arguments == null) {
                throw new ArgumentException(Commands.CreateDialogueGraph + " expects arguments");
            }

            var projectOrDocumentUri = new Uri(commandParams.Arguments[0].ToString());
            var project = workspace.GetProjectsForUri(projectOrDocumentUri);

            // alright so first we get the text of every file in every project
            var fileText = project
                .SelectMany(p => p.Files)
                .DistinctBy(f => f.Uri)
                .Select(pair => { return pair.Text; })
                .ToArray();

            // then we give that to the util that generates the runs
            var graph = Yarn.Compiler.Utility.DetermineNodeConnections(fileText);

            // then we build up the dot/mermaid file (copy from ysc)
            string graphString;

            // I hate this
            var format = commandParams.Arguments[1].ToString();
            var clustering = commandParams.Arguments[2].ToObject<bool>();

            if (format.Equals("dot"))
            {
                graphString = DrawDot(graph, clustering);
            }
            else
            {
                graphString = DrawMermaid(graph, clustering);
            }
            
            // then we send that back over
            return Task.FromResult(graphString);
        }

        // copied from YSC
        private static string DrawMermaid(List<List<Yarn.Compiler.GraphingNode>> graph, bool clustering)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("flowchart TB");

            int i = 0;
            foreach (var cluster in graph)
            {
                if (cluster.Count == 0)
                {
                    continue;
                }

                if (clustering)
                {
                    sb.AppendLine($"\tsubgraph a{i}");
                }
                foreach (var node in cluster)
                {
                    foreach (var jump in node.jumps)
                    {
                        sb.AppendLine($"\t{node.node}-->{jump}");
                    }
                }
                if (clustering)
                {
                    sb.AppendLine("\tend");
                }
                i++;
            }
            return sb.ToString();
        }
        private static string DrawDot(List<List<Yarn.Compiler.GraphingNode>> graph, bool clustering)
        {
            // using three individual builders is a bit lazy but it means I can turn stuff on and off as needed
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            System.Text.StringBuilder links = new System.Text.StringBuilder();
            System.Text.StringBuilder sub = new System.Text.StringBuilder();
            sb.AppendLine("digraph dialogue {");

            if (clustering)
            {
                int i = 0;
                foreach (var cluster in graph)
                {
                    if (cluster.Count == 0)
                    {
                        continue;
                    }
                    
                    // they need to be named clusterSomething to be clustered
                    sub.AppendLine($"\tsubgraph cluster{i}{{");
                    sub.Append("\t\t");
                    foreach (var node in cluster)
                    {
                        sub.Append($"{node.node} ");
                    }
                    sub.AppendLine(";");
                    sub.AppendLine("\t}");
                    i++;
                }
            }

            foreach (var cluster in graph)
            {
                foreach (var connection in cluster)
                {
                    if (connection.hasPositionalInformation)
                    {
                        sb.AppendLine($"\t{connection.node} [");
                        sb.AppendLine($"\t\tpos = \"{connection.position.x},{connection.position.y}\"");
                        sb.AppendLine("\t]");
                    }

                    foreach (var link in connection.jumps)
                    {
                        links.AppendLine($"\t{connection.node} -> {link};");
                    }
                }
            }   

            sb.Append(links);
            sb.Append(sub);

            sb.AppendLine("}");
            return sb.ToString();
        }


        private static Task<VOStringExport> ExtractVoiceoverSpreadsheet(Workspace workspace, ExecuteCommandParams<VOStringExport> commandParams)
        {
            if (commandParams.Arguments == null) {
                throw new ArgumentException(Commands.ExtractSpreadsheet + " expects arguments");
            }

            var projectOrDocumentUri = new Uri(commandParams.Arguments[0].ToString());
            var project = workspace.GetProjectsForUri(projectOrDocumentUri);

            var allFilesInProject = project
                .SelectMany(p => p.Files)
                .DistinctBy(p => p.Uri);

            // compiling the whole workspace so we can get access to the program to make sure it works
            var job = new Yarn.Compiler.CompilationJob
            {
                Files = allFilesInProject.Select(file =>
                {
                    return new Yarn.Compiler.CompilationJob.File
                    {
                        FileName = file.Uri.ToString(),
                        Source = file.Text,
                    };
                }),
                // Perform a full compilation so that we can produce a basic
                // block analysis of the file
                CompilationType = Yarn.Compiler.CompilationJob.Type.FullCompilation,
            };

            var result = Yarn.Compiler.Compiler.Compile(job);

            byte[] fileData = {};
            var errorMessages = result.Diagnostics
                .Where(d => d.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error)
                .Select(d => d.Message)
                .ToArray();

            if (errorMessages.Length == 0)
            {
                // We have no errors, so we can run through the nodes and build
                // up our blocks of lines.
                var lineBlocks = Yarn.Compiler.Utility.ExtractStringBlocks(result.Program.Nodes.Values).Select(bs => bs.ToArray()).ToArray();

                // Get the parameters from the command, substituting defaults as
                // needed
                string format;
                string[] columns;
                string defaultName;
                bool useCharacters;

                if (commandParams.Arguments.Count > 1) {
                    format = commandParams.Arguments[1].ToString();
                } else {
                    format = "xlsx";
                }

                if (commandParams.Arguments.Count > 2) {
                    columns = commandParams.Arguments[2].ToObject<string[]>();
                } else {
                    columns = new[] { "id", "text" };
                }

                if (commandParams.Arguments.Count > 3) {
                    defaultName = commandParams.Arguments[3].ToString();
                } else {
                    defaultName = "Player";
                }

                if (commandParams.Arguments.Count > 4) {
                    useCharacters = commandParams.Arguments[4].ToObject<bool>();
                } else {
                    useCharacters = true;
                }

                fileData = StringExtractor.ExportStrings(lineBlocks, result.StringTable, columns, format, defaultName, useCharacters);
            }

            var output = new VOStringExport
            {
                File = fileData,
                Errors = errorMessages,
            };
            return Task.FromResult(output);
        }
    }
}
