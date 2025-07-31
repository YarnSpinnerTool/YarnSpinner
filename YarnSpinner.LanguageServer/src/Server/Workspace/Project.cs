using OmniSharp.Extensions.LanguageServer.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yarn.Compiler;

namespace YarnLanguageServer
{
    public record ProjectInfo
    {
        public DocumentUri? Uri { get; set; }
        public IEnumerable<DocumentUri> Files { get; set; } = Array.Empty<DocumentUri>();
        public bool IsImplicitProject { get; set; }
    }

    internal class Project
    {
        public IEnumerable<YarnFileData> Files => yarnFiles.Values;
        public DocumentUri? Uri { get; init; }
        public bool IsImplicitProject { get; init; }

        CancellationTokenSource? currentCompilationCTS = null;

        internal IEnumerable<Yarn.Compiler.Declaration> Variables
        {
            get
            {
                if (LastCompilationResult == null)
                {
                    return Enumerable.Empty<Yarn.Compiler.Declaration>();
                }

                return LastCompilationResult.Declarations.Where(d => d.IsVariable == true);
            }
        }

        internal IEnumerable<Yarn.EnumType> Enums
        {
            get
            {
                if (LastCompilationResult == null)
                {
                    return Enumerable.Empty<Yarn.EnumType>();
                }

                var enums = LastCompilationResult.UserDefinedTypes.OfType<Yarn.EnumType>();
                return enums;
            }
        }

        internal IEnumerable<Yarn.Compiler.Diagnostic> Diagnostics
        {
            get
            {
                if (LastCompilationResult == null)
                {
                    // If we haven't compiled, then we have no diagnostics
                    return Enumerable.Empty<Yarn.Compiler.Diagnostic>();
                }

                return LastCompilationResult.Diagnostics;
            }
        }

        internal IActionSource? ActionSource { get; set; }
        internal IConfigurationSource? ConfigurationSource { get; set; }
        internal INotificationSender? NotificationSender { get; set; }

        private Yarn.Compiler.CompilationResult? LastCompilationResult { get; set; }

        private JsonConfigFile? DefinitionsFile { get; set; }

        public event System.Action<Yarn.Compiler.CompilationResult>? OnProjectCompiled;

        private readonly Yarn.Compiler.Project yarnProject;

        private readonly Dictionary<DocumentUri, YarnFileData> yarnFiles = new();

        internal bool MatchesUri(DocumentUri uri)
        {
            if (uri.Equals(this.Uri))
            {
                // This URI is for the project itself.
                return true;
            }

            if (this.IsImplicitProject)
            {
                return true;
            }

            return yarnProject.IsMatchingPath(uri.GetFileSystemPath());
        }

        public Project(string? projectFilePath, string? workspaceRoot = null, bool isImplicit = false)
        {
            this.IsImplicitProject = isImplicit;

            if (projectFilePath == null)
            {
                // The project path is null. The workspace may not exist on
                // disk.
                yarnProject = new Yarn.Compiler.Project
                {
                    WorkspaceRootPath = workspaceRoot,
                };
                return;
            }

            if (Directory.Exists(projectFilePath))
            {
                // This project is a directory.
                yarnProject = new Yarn.Compiler.Project
                {
                    Path = projectFilePath,
                    WorkspaceRootPath = workspaceRoot,
                };
            }
            else if (File.Exists(projectFilePath))
            {
                // This project is being loaded from a file.
                yarnProject = Yarn.Compiler.Project.LoadFromFile(projectFilePath, workspaceRoot);
            }
            else
            {
                // We failed to create a project from this path
                throw new ArgumentException($"Cannot create a Project from path {projectFilePath}");
            }

            this.Uri = DocumentUri.FromFileSystemPath(projectFilePath);

            foreach (var definitionPath in yarnProject.DefinitionsFiles)
            {
                try
                {
                    if (File.Exists(definitionPath))
                    {
                        var definitionsText = File.ReadAllText(definitionPath);
                        var newFile = new JsonConfigFile(definitionsText, false);
                        if (DefinitionsFile == null)
                        {
                            DefinitionsFile = newFile;
                        }
                        else
                        {
                            DefinitionsFile.MergeWith(newFile);
                        }
                    }
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    // TODO: handle parse failure
                }
                catch (IOException)
                {
                    // TODO: handle read failure
                }
            }
        }

        internal YarnFileData AddNewFile(Uri uri, string text)
        {
            var document = new YarnFileData(text, uri, this.NotificationSender);
            this.yarnFiles.Add(uri, document);
            document.Project = this;
            return document;
        }

        internal IEnumerable<Yarn.Compiler.Declaration> FindVariables(string name, bool fuzzySearch = false)
        {
            return FindDeclarations(Variables, name, fuzzySearch);
        }

        internal IEnumerable<string> FindNodes(string name, bool fuzzySearch = false)
        {
            var nodeNames = this.yarnFiles.Values
                .SelectMany(file => file.NodeInfos.Select(node => node.UniqueTitle))
                .NonNull()
                .Distinct();

            var nodeGroupNames = this.yarnFiles.Values.SelectMany(file => file.NodeGroupNames).Distinct();

            return FindNodeNames(nodeNames.Concat(nodeGroupNames), name, fuzzySearch);
        }

        /// <summary>
        /// Finds actions of the given type that match a name.
        /// </summary>
        /// <param name="name">The name of the action to search for.</param>
        /// <param name="actionType">The type of the action to search
        /// for.</param>
        /// <param name="fuzzySearch">Whether to perform fuzzy
        /// searching.</param>
        /// <returns>The collection of actions of the given type that match the
        /// name.</returns>
        internal IEnumerable<Action> FindActions(string name, ActionType actionType, bool fuzzySearch = false)
        {
            // If we have a definitions file, get actions from it
            var localDeclarations = DefinitionsFile?.GetActions() ?? Enumerable.Empty<Action>();

            var declarations = ActionSource?.GetActions()
                .Concat(localDeclarations)
                .Where(a => a.Type == actionType) ?? Enumerable.Empty<Action>();

            if (fuzzySearch == false)
            {
                return declarations.Where(d => d.YarnName.Equals(name));
            }

            return Workspace.FuzzySearchItem(
                declarations.Select(d => (d.YarnName, d)),
                name,
                ConfigurationSource?.Configuration.DidYouMeanThreshold ?? Configuration.Defaults.DidYouMeanThreshold
            );
        }

        internal YarnFileData? GetFileData(Uri documentUri)
        {
            if (this.yarnFiles.TryGetValue(documentUri, out var result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }

        internal IEnumerable<NodeInfo> Nodes => yarnFiles.Values.SelectMany(file => file.NodeInfos);

        internal IEnumerable<string> NodeGroupNames => yarnFiles.Values.SelectMany(file => file.NodeGroupNames);

        internal IEnumerable<Action> Functions => AllActions.Where(a => a.Type == ActionType.Function);

        internal IEnumerable<Action> Commands => AllActions.Where(a => a.Type == ActionType.Command);

        private IEnumerable<Action> AllActions
        {
            get
            {
                var localDeclarations = this.DefinitionsFile?.GetActions() ?? Enumerable.Empty<Action>();
                var workspaceDeclarations = ActionSource?.GetActions() ?? Enumerable.Empty<Action>();
                return workspaceDeclarations.Concat(localDeclarations);
            }
        }

        internal int FileVersion => yarnProject.FileVersion;

        internal async Task ReloadProjectFromDiskAsync(bool notifyOnComplete, CancellationToken cancellationToken)
        {
            IEnumerable<string> sourceFilePaths = this.yarnProject.SourceFiles;

            yarnFiles.Clear();

            foreach (var path in sourceFilePaths)
            {
                var uri = DocumentUri.FromFileSystemPath(path);
                var text = File.ReadAllText(path);
                var fileData = new YarnFileData(text, uri.ToUri(), this.NotificationSender);
                fileData.Project = this;
                this.yarnFiles.Add(uri, fileData);
            }

            await CompileProjectAsync(notifyOnComplete, Yarn.Compiler.CompilationJob.Type.TypeCheck, cancellationToken);
        }

        public struct CompileProjectOptions
        {
            public bool NotifyOnComplete;
            public bool ThrowOnCancellation;
        }

        public async Task<Yarn.Compiler.CompilationResult> CompileProjectAsync(bool notifyOnComplete, Yarn.Compiler.CompilationJob.Type compilationType, CancellationToken cancellationToken)
        {
            // If there's an existing cancellation token source for this project, cancel it now
            if (currentCompilationCTS != null)
            {
                await currentCompilationCTS.CancelAsync();
                currentCompilationCTS.Dispose();
            }

            currentCompilationCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var functionDeclarations = Functions.Select(f => f.Declaration).NonNull().ToArray();

            IEnumerable<Yarn.Compiler.ISourceInput> inputs = this.Files.Select(f => (ISourceInput)(new Yarn.Compiler.CompilationJob.File
            {
                FileName = f.Uri.AbsolutePath,
                Source = f.Text,
            }));

            var compilationJob = new Yarn.Compiler.CompilationJob
            {
                CompilationType = compilationType,
                Inputs = inputs,
                Declarations = functionDeclarations,
                LanguageVersion = this.yarnProject.FileVersion,
                CancellationToken = cancellationToken,
            };

            var compilationResult = await Task.Run(() =>
            {
                var compilationResult = Yarn.Compiler.Compiler.Compile(compilationJob);
                return compilationResult;
            }).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);

            // This compilation is cancelled. Early out.
            cancellationToken.ThrowIfCancellationRequested();

            this.LastCompilationResult = compilationResult;


            // For all jumps in all files, attempt to identify the file that the
            // target of the jump is in, and store it in the jump info
            var nodesToFiles = this.Files
                .SelectMany(f => f.NodeInfos
                    .Where(n => n.SourceTitle != null))
                    .ToLookup(n => n.SourceTitle!);


            foreach (var file in this.Files)
            {
                foreach (var jump in file.NodeJumps)
                {
                    var nodesWithTitle = nodesToFiles.FirstOrDefault(n => n.Key == jump.DestinationTitle);
                    jump.DestinationFile = nodesWithTitle?.FirstOrDefault()?.File;
                }
            }

            if (notifyOnComplete)
            {
                OnProjectCompiled?.Invoke(compilationResult);
            }
            currentCompilationCTS.Dispose();
            currentCompilationCTS = null;

            return compilationResult;
        }

        async internal Task<DebugOutput> GetDebugOutputAsync(CancellationToken cancellationToken)
        {
            var compilationResult = await this.CompileProjectAsync(false, Yarn.Compiler.CompilationJob.Type.FullCompilation, cancellationToken);

            var variables = compilationResult.Declarations
                .Where(decl => decl.IsVariable)
                .Select(decl => new DebugOutput.Variable
                {
                    Name = decl.Name,
                    Type = decl.Type?.ToString() ?? "unknown",
                    IsSmartVariable = decl.IsInlineExpansion,
                    ExpressionJSON = decl.IsInlineExpansion ? new ExpressionToJSONVisitor().Visit(decl.InitialValueParserContext).JSONValue : null,
                });

            var projectDebugOutput = new DebugOutput
            {
                SourceProjectUri = this.Uri,
                Variables = variables.ToList(),
            };

            return projectDebugOutput;
        }

        private IEnumerable<Yarn.Compiler.Declaration> FindDeclarations(IEnumerable<Yarn.Compiler.Declaration> declarations, string name, bool fuzzySearch)
        {
            if (fuzzySearch == false)
            {
                return declarations.Where(d => d.Name.Equals(name));
            }

            return Workspace.FuzzySearchItem(declarations.Select(d => (d.Name, d)), name, ConfigurationSource?.Configuration.DidYouMeanThreshold ?? Configuration.Defaults.DidYouMeanThreshold);
        }

        private IEnumerable<string> FindNodeNames(IEnumerable<string> nodeNames, string name, bool fuzzySearch)
        {
            if (fuzzySearch == false)
            {
                return nodeNames.Where(n => n.Equals(name));
            }

            return Workspace.FuzzySearchItem(nodeNames.Select(n => (n, n)), name, ConfigurationSource?.Configuration.DidYouMeanThreshold ?? Configuration.Defaults.DidYouMeanThreshold)
                .Select(n => n);
        }
    }
}
