using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YarnLanguageServer.Diagnostics;

namespace YarnLanguageServer
{
    internal interface IActionSource
    {
        internal IEnumerable<Action> GetActions();
    }

    internal interface IConfigurationSource
    {
        internal Configuration Configuration { get; }
    }

    public class Workspace : INotificationSender, IActionSource, IConfigurationSource
    {
        public string? Root { get; internal set; }
        public IWindowLanguageServer? Window => LanguageServer?.Window;

        internal Configuration Configuration { get; set; } = new Configuration();
        internal IEnumerable<Project> Projects { get; set; } = Array.Empty<Project>();

        private ILanguageServer? LanguageServer { get; set; }

        /// <summary>
        /// Have we shown a warning about the workspace having no root folder?
        /// (We only want to show it once.)
        /// </summary>
        private bool hasShownNullRootWarning = false;

        /// <summary>
        /// Gets the projects that include the file at <paramref name="uri"/>.
        /// </summary>
        /// <param name="uri">The uri to get projects for.</param>
        /// <returns>The collection of projects that include uri.</returns>
        internal IEnumerable<Project> GetProjectsForUri(DocumentUri uri)
        {
            foreach (var project in Projects)
            {
                if (project.MatchesUri(uri))
                {
                    yield return project;
                }
            }
        }

        /// <summary>
        /// The collection of actions defined in the workspace's C# files.
        /// </summary>
        private HashSet<Action> workspaceActions = new HashSet<Action>();

        Configuration IConfigurationSource.Configuration => this.Configuration;

        /// <summary>
        /// Sends the DidChangeNodesNotification message to the client, which
        /// contains semantic information about the nodes in this file.
        /// </summary>
        /// <seealso cref="Commands.DidChangeNodesNotification"/>
        public void PublishNodeInfos()
        {
            foreach (var file in this.Projects.SelectMany(p => p.Files))
            {
                this.LanguageServer?.SendNotification(
                    Commands.DidChangeNodesNotification,
                    new NodesChangedParams(file.Uri, file.NodeInfos)
                );

            }
        }

        public void SendNotification<T>(string method, T @params)
        {
            this.LanguageServer?.SendNotification<T>(method, @params);
        }

        IEnumerable<Action> IActionSource.GetActions() => this.workspaceActions;

        /// <summary>
        /// Returns the collection of Action objects that are pre-defined in the
        /// Yarn language.
        /// </summary>
        /// <returns>The list of pre-defined actions.</returns>
        /// <exception cref="InvalidOperationException">Thrown when loading the
        /// list of pre-defined actions fails.</exception>
        internal static IEnumerable<Action> GetPredefinedActions()
        {
            var predefinedActions = new List<Action>();

            var thisAssembly = typeof(Workspace).Assembly;
            var resources = thisAssembly.GetManifestResourceNames();
            var jsonAssemblyFiles = resources.Where(r => r.EndsWith("ysls.json"));

            foreach (var doc in jsonAssemblyFiles)
            {
                Stream? stream = thisAssembly.GetManifestResourceStream(doc);

                if (stream == null)
                {
                    throw new InvalidOperationException($"Failed to read manifest resource stream for {doc}");
                }

                string text = new StreamReader(stream).ReadToEnd();
                var docJsonConfig = new JsonConfigFile(text, true);

                predefinedActions.AddRange(docJsonConfig.GetActions());
            }

            return predefinedActions;
        }

        internal static IEnumerable<T> FuzzySearchItem<T>(IEnumerable<(string name, T item)> items, string name, float threshold)
        {
            var lev = new Fastenshtein.Levenshtein(name.ToLower());

            return items.Select(searchItem =>
                {
                    float distance = lev.DistanceFrom(searchItem.name.ToLower());
                    var normalizedDistance = distance / Math.Max(Math.Max(name.Length, searchItem.name.Length), 1);

                    if (distance <= 1
                        || searchItem.name.Contains(name, StringComparison.OrdinalIgnoreCase)
                        || name.Contains(searchItem.name, StringComparison.OrdinalIgnoreCase))
                    {
                        // include strings that contain each other even if they don't meet the threshold
                        // usecase is more the user didn't finish typing instead of the user made a typo
                        normalizedDistance = Math.Min(normalizedDistance, threshold);
                    }

                    return (searchItem.item, Distance: normalizedDistance);
                })
                .Where(scoredfd => scoredfd.Distance <= threshold)
                .OrderBy(scorefd => scorefd.Distance)
                .Select(scoredfd => scoredfd.item);
        }

        /// <summary>
        /// Initializes this Workspace without a language server.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be
        /// used to cancel initialization.</param>
        /// <remarks>
        /// Workspaces deliver information about the changing state of the
        /// project via their language server. If a Workspace has no language
        /// server, it will not report on any changes.
        /// </remarks>
        internal async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Initializing the workspace without a language server cannot be
            // cancelled.
            await ReloadWorkspaceAsync(cancellationToken);
        }

        internal async Task ReloadWorkspaceAsync(CancellationToken cancellationToken)
        {
            // Find all actions defined in the workspace
            if (this.Root != null)
            {
                this.workspaceActions = new HashSet<Action>(this.FindWorkspaceActions(this.Root));
            }
            else
            {
                this.workspaceActions = new HashSet<Action>();
            }

            // Find all actions built in to this DLL
            try
            {
                IEnumerable<Action> predefinedActions = GetPredefinedActions();

                foreach (var action in predefinedActions)
                {
                    this.workspaceActions.Add(action);
                }
            }
            catch (Exception e)
            {
                LanguageServer?.LogError($"Error while loading built-in actions: " + e);
            }

            var projects = new List<Project>();

            if (this.Root == null)
            {
                // We don't have a root folder. The language server won't be
                // able to find any additional resources, so we should warn the
                // user about this. (This can happen when the user double-clicks
                // on a file in Unity, which will open the file directly in VS
                // Code without a root folder.)
                if (hasShownNullRootWarning == false)
                {
                    LanguageServer?.Window.ShowWarning($"This window does not have a folder to work in. Yarn Spinner features will not work as expected. [Open your project's folder](command:vscode.openFolder) for full feature support.");
                    hasShownNullRootWarning = true;
                }
            }
            else
            {
                // Find all .yarnprojects in the root and create Projects out of
                // them
                var yarnProjectFiles = Directory.EnumerateFiles(Root, "*.yarnproject", new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive });

                // Create a project for each .yarnproject in the workspace.
                this.Projects = yarnProjectFiles.Select(path =>
                {
                    try
                    {
                        return new Project(path, this.Root);
                    }
                    catch (System.Exception e)
                    {
                        this.LanguageServer?.LogError($"Failed to create a project for {path}: " + e.ToString());
                        return null;
                    }
                }).NonNull().ToList();
            }

            if (!this.Projects.Any())
            {
                // There are no .yarnprojects in the workspace. Create a new
                // 'implicit' project at the root of the workspace that owns ALL
                // Yarn files, as a convenience.
                //
                // (We only do this if there are no .yarnproject files. This has
                // the consequence where if a workspace does have project files,
                // and a yarn file is not included in any of them, it is not
                // considered to be part of the workspace and will not be
                // compiled. This is consistent with how Yarn Spinner for Unity
                // works - if a file is not in a project, it is not compiled.)
                Project implicitProject = new(Root, Root, isImplicit: true);
                this.Projects = new[] { implicitProject };

                // Additionally, if the workspace contains an actions definition
                // file, use that. (If there's more than one, that's a warning -
                // only the first one we find will be used.)
                if (Root != null)
                {
                    var definitionFiles = Directory.EnumerateFiles(Root, "*.ysls.json", SearchOption.AllDirectories);

                    if (definitionFiles.Any())
                    {
                        string definitionFilePath = definitionFiles.First();

                        if (definitionFiles.Count() > 1)
                        {
                            Window?.ShowWarning($"Multiple .ysls.json files were found in the workspace. Only the first one found ({definitionFilePath}) will be used.");
                        }

                        try
                        {
                            var definitionFile = new JsonConfigFile(File.ReadAllText(definitionFilePath), false);

                            foreach (var action in definitionFile.GetActions())
                            {
                                this.workspaceActions.Add(action);
                            }
                        }
                        catch (Exception e)
                        {
                            LanguageServer?.LogError($"Failed to load actions definition file {definitionFilePath}: {e}");
                        }
                    }
                }
            }

            var reloadTasks = new List<Task>();

            // Configure each project in the workspace
            foreach (var project in this.Projects)
            {
                project.ActionSource = this;
                project.ConfigurationSource = this;
                project.NotificationSender = this;

                // When a project reloads, publish diagnostics.
                project.OnProjectCompiled += (compilationResult) =>
                {
                    PublishDiagnostics();
                    PublishNodeInfos();
                };

                // Reload the project without notifying. (When we load a
                // workspace, all projects will reload at once, so we'll wait
                // until they're all created.)
                reloadTasks.Add(project.ReloadProjectFromDiskAsync(false, cancellationToken));
            }
            await Task.WhenAll(reloadTasks);

            this.PublishDiagnostics();
            this.PublishNodeInfos();
        }

        internal Dictionary<Uri, IEnumerable<Diagnostic>> GetDiagnostics()
        {
            var result = new Dictionary<Uri, IEnumerable<Diagnostic>>();

            IEnumerable<Yarn.Compiler.Diagnostic> diagnostics = this.Projects
                .SelectMany(p => p.Diagnostics);

            foreach (var file in this.Projects.SelectMany(p => p.Files))
            {
                var uri = file.Uri;
                var diags = diagnostics
                        .Where(d => d.FileName == uri.AbsolutePath)
                        .Select(d => d.AsLSPDiagnostic());

                // Add warnings for this file
                diags = diags.Concat(Warnings.GetWarnings(file, this.Configuration));

                // Add hints for this file
                diags = diags.Concat(Hints.GetHints(file, this.Configuration));

                // Add the resulting list to the dictionary.
                result[uri] = diags;
            }

            return result;
        }

        internal void PublishDiagnostics()
        {
            foreach (var pair in GetDiagnostics())
            {
                var uri = pair.Key;
                var diags = pair.Value;

                // Publish diagnostics for this file
                LanguageServer?.TextDocument.PublishDiagnostics(
                    new PublishDiagnosticsParams
                    {
                        Uri = uri,
                        Diagnostics = diags.ToList(),
                    }
                );
            }
        }

        /// <summary>
        /// Initializes this Workspace without a language server.
        /// </summary>
        /// <inheritdoc cref="Initialize" path="/remarks"/>
        /// <param name="server">The language server to use.</param>
        internal async Task InitializeAsync(ILanguageServer server, CancellationToken cancellationToken)
        {
            this.LanguageServer = server;
            await InitializeAsync(cancellationToken);
        }

        /// <summary>
        /// Delivers a message to the user, through the configured language
        /// server.
        /// </summary>
        /// <remarks>
        /// If this Workspace was not initialized with a language server, this
        /// method performs no action.
        /// </remarks>
        /// <param name="message">The text of the message to deliver.</param>
        /// <param name="messageType">The type of the message to deliver.</param>
        internal void ShowMessage(string message, MessageType messageType)
        {
            this.LanguageServer?.Window.ShowMessage(new ShowMessageParams
            {
                Message = message,
                Type = messageType,
            });
        }

        public bool IsAnyProjectCompiling
        {
            get
            {
                foreach (var project in Projects)
                {
                    if (project.IsCompiling)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private IEnumerable<Action> FindWorkspaceActions(string root)
        {
            var csharpWorkspaceFiles = System.IO.Directory.EnumerateFiles(root, "*.cs", System.IO.SearchOption.AllDirectories);

            // Filter out any C# files that are in Unity directories not
            // directly authored by the user
            csharpWorkspaceFiles = csharpWorkspaceFiles.Where(f => !f.Contains("PackageCache") && !f.Contains("Library"));

            foreach (var file in csharpWorkspaceFiles)
            {
                var text = System.IO.File.ReadAllText(file);

                if (!text.ContainsAny("YarnCommand", "YarnFunction", "AddCommandHandler", "AddFunction"))
                {
                    // This C# file doesn't contain any Yarn functions, so skip
                    // it
                    continue;
                }

                var uri = new Uri(file);
                foreach (var action in CSharpFileData.ParseActionsFromCode(text, uri))
                {
                    yield return action;
                }
            }
        }
    }

    internal static class EnumerableExtension
    {
        public static IEnumerable<T> NonNull<T>(this IEnumerable<T?> enumerable)
            where T : class
        {
            return enumerable.Where(item => item != null)!;
        }
    }
}
