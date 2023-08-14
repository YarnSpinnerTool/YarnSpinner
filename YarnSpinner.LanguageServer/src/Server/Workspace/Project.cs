using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace YarnLanguageServer
{
    internal class Project
    {
        public IEnumerable<YarnFileData> Files => yarnFiles.Values;
        public DocumentUri? Uri { get; init; }
        public bool IsImplicitProject { get; init; }

        internal IEnumerable<Yarn.Compiler.Declaration> Variables
        {
            get
            {
                if (LastCompilationResult.HasValue == false)
                {
                    return Enumerable.Empty<Yarn.Compiler.Declaration>();
                }

                var result = LastCompilationResult.Value;

                return result.Declarations.Where(d => d.IsVariable == true);
            }
        }

        internal IEnumerable<Yarn.Compiler.Diagnostic> Diagnostics
        {
            get
            {
                if (LastCompilationResult.HasValue == false)
                {
                    // If we haven't compiled, then we have no diagnostics
                    return Enumerable.Empty<Yarn.Compiler.Diagnostic>();
                }

                return LastCompilationResult.Value.Diagnostics;
            }
        }

        internal IActionSource? ActionSource { get; set; }
        internal IConfigurationSource? ConfigurationSource { get; set; }
        internal INotificationSender? NotificationSender { get; set; }

        private Yarn.Compiler.CompilationResult? LastCompilationResult { get; set; }

        private JsonConfigFile? DefinitionsFile { get; set; }

        public event System.Action? OnProjectCompiled;

        private readonly Yarn.Compiler.Project yarnProject;

        private readonly Dictionary<DocumentUri, YarnFileData> yarnFiles = new ();

        internal bool MatchesUri(DocumentUri uri)
        {
            if (uri.Equals(this.Uri)) {
                // This URI is for the project itself.
                return true;
            }

            if (this.IsImplicitProject) {
                return true;
            }

            return yarnProject.IsMatchingPath(uri.GetFileSystemPath());
        }

        public Project(string? projectFilePath, bool isImplicit = false)
        {
            this.IsImplicitProject = isImplicit;

            if (projectFilePath == null)
            {
                // The project path is null. The workspace may not exist on
                // disk.
                yarnProject = new Yarn.Compiler.Project();
                return;
            }

            if (Directory.Exists(projectFilePath))
            {
                // This project is a directory.
                yarnProject = new Yarn.Compiler.Project
                {
                    Path = projectFilePath,
                };
            }
            else if (File.Exists(projectFilePath))
            {
                // This project is being loaded from a file.
                yarnProject = Yarn.Compiler.Project.LoadFromFile(projectFilePath);
            }
            else
            {
                // We failed to create a project from this path
                throw new ArgumentException($"Cannot create a Project from path {projectFilePath}");
            }

            this.Uri = DocumentUri.FromFileSystemPath(projectFilePath);

            if (File.Exists(yarnProject.DefinitionsPath))
            {
                var definitionsText = File.ReadAllText(yarnProject.DefinitionsPath);
                DefinitionsFile = new JsonConfigFile(definitionsText, false);
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

        internal void ReloadProjectFromDisk(bool notifyOnComplete = true)
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

            CompileProject(notifyOnComplete, Yarn.Compiler.CompilationJob.Type.DeclarationsOnly);
        }

        public Yarn.Compiler.CompilationResult CompileProject(bool notifyOnComplete, Yarn.Compiler.CompilationJob.Type compilationType) {

            var functionDeclarations = Functions.Select(f => f.Declaration);

            var files = this.Files.Select(f => new Yarn.Compiler.CompilationJob.File
            {
                FileName = f.Uri.AbsolutePath,
                Source = f.Text,
            });

            var compilationJob = new Yarn.Compiler.CompilationJob
            {
                CompilationType = compilationType,
                Files = files,
                VariableDeclarations = functionDeclarations,
            };

            var compilationResult = Yarn.Compiler.Compiler.Compile(compilationJob);

            this.LastCompilationResult = compilationResult;

            if (notifyOnComplete)
            {
                OnProjectCompiled?.Invoke();
            }

            return compilationResult;
        }

        private IEnumerable<Yarn.Compiler.Declaration> FindDeclarations(IEnumerable<Yarn.Compiler.Declaration> declarations, string name, bool fuzzySearch)
        {
            if (fuzzySearch == false)
            {
                return declarations.Where(d => d.Name.Equals(name));
            }

            return Workspace.FuzzySearchItem(declarations.Select(d => (d.Name, d)), name, ConfigurationSource?.Configuration.DidYouMeanThreshold ?? Configuration.Defaults.DidYouMeanThreshold);
        }
    }
}
