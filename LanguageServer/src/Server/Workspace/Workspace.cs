using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using YarnLanguageServer.Diagnostics;

namespace YarnLanguageServer
{
    internal class Workspace
    {
        public Dictionary<Uri, CSharpFileData> CSharpFiles { get; set; } = new Dictionary<Uri, CSharpFileData>();
        public Dictionary<Uri, JsonConfigFile> JsonConfigFiles { get; set; } = new Dictionary<Uri, JsonConfigFile>();
        public Dictionary<Uri, YarnFileData> YarnFiles { get; set; } = new Dictionary<Uri, YarnFileData>();
        public Configuration Configuration { get; protected set; }
        public string Root { protected get; set; }
        public ILanguageServer LanguageServer { get; protected set; }
        public List<(string YarnName, string DefinitionName, bool IsCommand, string FileName)> UnmatchedDefinitions { get; protected set; }

        public IEnumerable<Yarn.Compiler.Declaration> Declarations { get; private set; }

        private Dictionary<string, RegisteredDefinition> functionDefinitionCache = new Dictionary<string, RegisteredDefinition>();

        /// <summary>
        /// The diagnostics messages produced from the last time this workspace
        /// was updated.
        /// </summary>
        private IEnumerable<Yarn.Compiler.Diagnostic> Diagnostics = new List<Yarn.Compiler.Diagnostic>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Workspace"/> class.
        /// </summary>
        public Workspace()
        {
            Configuration = new Configuration(this);
        }

        public void Initialize(ILanguageServer languageServer)
        {            
            this.LanguageServer = languageServer;

            // If we don't have a root directory, attempting to enumerate the
            // files in 'Root' will throw an exception, which will cause big
            // problems since the server can't be initialized. Workaround: skip
            // over it all if we don't have a root.
            if (Root == null) {
                return;
            }

            LoadExternalInfo();

            var yarnFiles = System.IO.Directory.EnumerateFiles(Root, "*.yarn", System.IO.SearchOption.AllDirectories);
            yarnFiles = yarnFiles.Where(f => !f.Contains("PackageCache") && !f.Contains("Library"));
            foreach (var path in yarnFiles)
            {
                OpenFile(path, false);
            }
            UpdateWorkspace();
        }

        /// <summary>
        /// Opens and begins tracking a Yarn file.
        /// </summary>
        /// <param name="path">The path to the Yarn file.</param>
        /// <param name="updateWorkspace">Whether to update the workspace after opening.</param>
        /// <remarks>
        /// If you are opening multiple files at once (through repeated calls to <see cref="OpenFile"/>, pass false as the value for updateWorkspace, and call <see cref="UpdateWorkspace"/>.)
        /// </remarks>
        /// <returns>The new Yarn file data.</returns>
        public YarnFileData? OpenFile(string path, bool updateWorkspace = true)
        {
            try
            {
                var text = System.IO.File.ReadAllText(path);
                var uri = new Uri("file://" + path);

                var yarnFileData = new YarnFileData(text, uri, this);
                YarnFiles[uri] = yarnFileData;

                return yarnFileData;
            } catch (System.IO.IOException) {
                return null;
            } finally {
                if (updateWorkspace) {
                    UpdateWorkspace();
                }
            }
        }

        public YarnFileData? OpenFile(Uri uri) {
            try {
                var path = uri.AbsolutePath;
                var text = System.IO.File.ReadAllText(path);

                var yarnFileData = new YarnFileData(text, uri, this);
                YarnFiles[uri] = yarnFileData;

                return yarnFileData;
            } catch (System.IO.IOException) {
                return null;
            }
        }

        public void LoadExternalInfo()
        {
            var jsonWorkspaceFiles = System.IO.Directory.EnumerateFiles(Root, "*.ysls.json", System.IO.SearchOption.AllDirectories);
            foreach (var file in jsonWorkspaceFiles)
            {
                var uri = new Uri(file);
                var text = System.IO.File.ReadAllText(file);
                var docJsonConfig = new JsonConfigFile(text, uri, this, false);
                if (docJsonConfig != null)
                {
                    JsonConfigFiles[uri] = docJsonConfig;
                }
            }

            // TODO Refactor this out into a method like (manifestPathPrefix) => (docuri, doctext)
            try
            {
                var thisAssembly = typeof(Workspace).Assembly;
                var resources = thisAssembly.GetManifestResourceNames();
                var jsonAssemblyFiles = resources.Where(r => r.EndsWith("ysls.json"));

                foreach (var doc in jsonAssemblyFiles)
                {
                    var uri = new Uri("file:///assembly/" + doc); // a fake uri but we just need it for lookup and uniqueness
                    string text = new System.IO.StreamReader(thisAssembly.GetManifestResourceStream(doc)).ReadToEnd();
                    var docJsonConfig = new JsonConfigFile(text, uri, this, true);
                    if (docJsonConfig != null)
                    {
                        JsonConfigFiles[uri] = docJsonConfig;
                    }
                }
            }
            catch (Exception) { }

            UnmatchedDefinitions = JsonConfigFiles.SelectMany(jcf => jcf.Value.Definitions.Where(f => f.Value.Language == "csharp").Select(kv => (kv.Value.YarnName, kv.Value.DefinitionName, kv.Value.IsCommand, kv.Value.FileName))).ToList();

            if (Configuration.CSharpLookup)
            {
                CSharpFiles.Clear();

                var csharpWorkspaceFiles = System.IO.Directory.EnumerateFiles(Root, "*.cs", System.IO.SearchOption.AllDirectories);
                csharpWorkspaceFiles = csharpWorkspaceFiles.Where(f => !f.Contains("PackageCache") && !f.Contains("Library"));
                var unusedCSharpFiles = new List<string>();

                foreach (var file in csharpWorkspaceFiles)
                {
                    var text = System.IO.File.ReadAllText(file);
                    if (UnmatchedDefinitions.Any(ucn => !string.IsNullOrWhiteSpace(ucn.FileName) && file.Contains(ucn.FileName)) // if we know there's a file that we're looking for
                     || text.ContainsAny("YarnCommand", "AddCommandHandler", "AddFunction") // built in yarn spinner linking
                     || text.ContainsAny("yarnfunction", "yarncommand") // linked by structured comment
                    )
                    {
                        // add to list of csharp files of interest / subscribe to updates
                        var uri = new Uri(file);
                        CSharpFiles[uri] = new CSharpFileData(text, uri, this);
                    }
                    else { unusedCSharpFiles.Add(file); }
                }

                foreach (var fileEntry in CSharpFiles)
                {
                    // Even if we don't have anymore unmatched commands, this will clean up any leftover syntax trees
                    fileEntry.Value.LookForUnmatchedCommands(isLastTime: true);
                }


                if (Configuration.DeepCommandLookup)
                {
                    // If we don't have a definition name, we'll never find it by yarn name (already found everything by yarn name before deep command lookup.)
                    UnmatchedDefinitions = UnmatchedDefinitions.Where(d => d.DefinitionName.Any()).ToList();

                    foreach (var file in unusedCSharpFiles)
                    {
                        if (!UnmatchedDefinitions.Any())
                        {
                            break;
                        }

                        var text = System.IO.File.ReadAllText(file);
                        if (text.ContainsAny(UnmatchedDefinitions.Select(ucn => ucn.DefinitionName).ToArray()))
                        {
                            var uri = new Uri(file);
                            var fileData = new CSharpFileData(text, uri, this, true);
                            if (fileData.Definitions.Any())
                            {
                                CSharpFiles[uri] = fileData;
                            }
                        }
                    }
                }
            }
            else
            {
                // Probably excesive
                CSharpFiles.Clear();
            }

            FillFunctionDefinitionCache();
        }

        public IEnumerable<(Uri uri, string title, Range range)> GetNodeTitles()
        {
            var allNodeInfos = YarnFiles.Values.SelectMany(y => y.NodeInfos);

            return allNodeInfos
                .Select(n => (
                    n.File.Uri,
                    n.TitleToken.Text,
                    PositionHelper.GetRange(n.File.LineStarts, n.TitleToken))
                    )
                .Distinct();
        }

        public IEnumerable<string> GetVariableNames()
        {
            var allNodeInfos = YarnFiles.Values.SelectMany(y => y.NodeInfos);

            return allNodeInfos
                .SelectMany(n => n.VariableReferences)
                .Select(variableToken => variableToken.Text)
                .Distinct();
        }

        public IEnumerable<Yarn.Compiler.Declaration> GetVariables(string name = null, bool fuzzyMatch = false)
        {
            if (name == null)
            {
                return this.Declarations;
            }

            if (fuzzyMatch == false)
            {
                return this.Declarations.Where(d => d.Name == name);
            }

            // Todo: Refactor this part out and use for variables and functions
            var threshold = Configuration.DidYouMeanThreshold;
            var lev = new Fastenshtein.Levenshtein(name.ToLower());

            return Declarations.Select(declaration =>
                {
                    float distance = lev.DistanceFrom(declaration.Name.ToLower());
                    var normalizedDistance = distance / Math.Max(Math.Max(name.Length, declaration.Name.Length), 1);

                    if (distance <= 1
                        || declaration.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                        || name.Contains(declaration.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        // include strings that contain each other even if they don't meet the threshold
                        // usecase is more the user didn't finish typing instead of the user made a typo
                        normalizedDistance = Math.Min(normalizedDistance, threshold);
                    }

                    return (Declaration: declaration, Distance: normalizedDistance);
                })
                .Where(scoredfd => scoredfd.Distance <= threshold)
                .OrderBy(scorefd => scorefd.Distance)
                .Select(scoredfd => scoredfd.Declaration);
        }

        public IEnumerable<RegisteredDefinition> GetFunctions()
        {
            return functionDefinitionCache.Select(kvp => kvp.Value).Where(f => !f.IsCommand);
        }

        public IEnumerable<RegisteredDefinition> GetCommands()
        {
            return functionDefinitionCache.Select(kvp => kvp.Value).Where(f => f.IsCommand);
        }

        /// <summary>
        /// Get Function or Command definition information for command/functions registered with Yarn Spinner.
        /// </summary>
        /// <param name="functionName">Search string. Note that this is the registered Yarn name, not the name of what is defined in code.</param>
        /// <param name="fuzzyMatch">Match names that contain or are close to the input, and not just an exact match.</param>
        /// <returns>Enumerable of matches. Note that calling function needs to filter which of commands or functions that it wants.</returns>
        public IEnumerable<RegisteredDefinition> LookupFunctions(string functionName, bool fuzzyMatch = false)
        {
            var results = Enumerable.Empty<RegisteredDefinition>();

            if (!fuzzyMatch && functionDefinitionCache.TryGetValue(functionName, out var function))
            {
                return new List<RegisteredDefinition> { function };
            }
            else if (fuzzyMatch)
            {
                results = functionDefinitionCache.Select(kvp => kvp.Value);
            }


            if (fuzzyMatch) {
                var threshold = Configuration.DidYouMeanThreshold;
                var lev = new Fastenshtein.Levenshtein(functionName.ToLower());
                results = results
                    .Select(fd =>
                    {
                        float distance = lev.DistanceFrom(fd.YarnName.ToLower());
                        var normalizedDistance = distance / Math.Max(Math.Max(functionName.Length, fd.YarnName.Length), 1);

                        if (fd.YarnName.ToLower().Contains(functionName.ToLower()) || functionName.ToLower().Contains(fd.YarnName.ToLower()))
                        {
                            // include strings that contain each other even if they don't meet the threshold
                            // usecase is more the user didn't finish typing instead of the user made a typo
                            normalizedDistance = Math.Min(normalizedDistance, threshold);
                        }

                        return (fd, normalizedDistance);
                    })
                    .Where(scoredfd => scoredfd.normalizedDistance <= threshold)
                    .OrderBy(scorefd => scorefd.normalizedDistance)
                    .Select(scoredfd => scoredfd.fd);
            }

            return results;
        }

        internal void UpdateWorkspace()
        {
            // Compile all files in the workspace.
            var compilationJob = new Yarn.Compiler.CompilationJob
            {
                Files = YarnFiles.Select(pair => {
                    var uri = pair.Key;
                    var file = pair.Value;

                    return new Yarn.Compiler.CompilationJob.File
                    {
                        FileName = uri.ToString(),
                        Source = file.Text,
                    };
                }),
                CompilationType = Yarn.Compiler.CompilationJob.Type.DeclarationsOnly,
            };

            var result = Yarn.Compiler.Compiler.Compile(compilationJob);

            this.Diagnostics = result.Diagnostics;
            this.Declarations = result.Declarations;

            PublishDiagnostics();
        }

        public void PublishDiagnostics()
        {
            
            // Here are the diagnostics that might change depending on other things in the workspace
            // var diagnostics =  Warnings.GetWarnings(this, Workspace);
            // diagnostics = diagnostics.Concat(SemanticErrors.GetErrors(this, Workspace));
            // this.HasSemanticDiagnostics = diagnostics.Any();

            // diagnostics = diagnostics.Concat(CompilerDiagnostics);

            foreach (var filePair in this.YarnFiles) {

                    var uri = filePair.Key;
                    var diagnostics = this.Diagnostics
                        .Where(d => d.FileName == uri.ToString())
                        .Select(d => ConvertDiagnostic(d)).ToList();

                // Add warnings for this file
                diagnostics = diagnostics.Concat(Warnings.GetWarnings(filePair.Value, this)).ToList();

                LanguageServer.TextDocument.PublishDiagnostics(
                        new PublishDiagnosticsParams
                        {
                            Uri = uri,
                            Diagnostics = diagnostics,
                        }
                    );
            }

        }

        /// <summary>
        /// Converts a <see cref="Yarn.Compiler.Diagnostic"/> object to a <see
        /// cref="Diagnostic"/>.
        /// </summary>
        /// <param name="d">The <see cref="Yarn.Compiler.Diagnostic"/> to
        /// convert.</param>
        /// <returns>The converted <see cref="Diagnostic"/>.</returns>
        private static Diagnostic ConvertDiagnostic(Yarn.Compiler.Diagnostic d)
        {
            return new Diagnostic
            {
                Range = new Range(
                    d.Range.Start.Line,
                    d.Range.Start.Character,
                    d.Range.End.Line,
                    d.Range.End.Character
                ),
                Message = d.Message,
                Severity = d.Severity switch
                {
                    Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
                    Yarn.Compiler.Diagnostic.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                    Yarn.Compiler.Diagnostic.DiagnosticSeverity.Info => DiagnosticSeverity.Information,
                    _ => DiagnosticSeverity.Error,
                },
                Source = d.FileName,
            };
        }

        private void FillFunctionDefinitionCache()
        {
            functionDefinitionCache.Clear();

            var results = JsonConfigFiles.Values
                .Cast<IDefinitionsProvider>()
                .Concat(CSharpFiles.Values.Select(v => v))
                .SelectMany(fp => fp.Definitions.Values.Select(v => v));

            // Now we need to merge any json / c# versions of definitons, where json is the base, and c# fills in anything missing
            // TODO: There's probably a better way to do this merge
            // Test cases to consider, multiple json entries and no csharp entries. Multiple valid definitions in csharp.
            results = results
                .GroupBy(f => f.YarnName).Select(g =>
                {
                    if (g.Count() == 1)
                    {
                        return g.First();
                    }

                    if (g.All(f => f.DefinitionFile.ToString().EndsWith(".cs")))
                    {
                        return g.OrderBy(f => f.Priority).First();
                    }

                    var jsonDef = g.FirstOrDefault(f => f.DefinitionFile.ToString().EndsWith(".json"));

                    var csharpDef = g.OrderBy(f => f.Priority)
                        .FirstOrDefault(
                            f => f.DefinitionFile.ToString().EndsWith(".cs")
                                && !f.Signature.EndsWith("(?)")
                        );

                    if (string.IsNullOrWhiteSpace(csharpDef.YarnName))
                    {
                        csharpDef = g.FirstOrDefault(
                            f => f.DefinitionFile.ToString().EndsWith(".cs")
                        );
                    }

                    jsonDef.DefinitionFile = csharpDef.DefinitionFile;
                    jsonDef.DefinitionRange = csharpDef.DefinitionRange;
                    jsonDef.Parameters ??= csharpDef.Parameters;
                    jsonDef.MinParameterCount ??= csharpDef.MinParameterCount;
                    jsonDef.MaxParameterCount ??= csharpDef.MaxParameterCount;
                    jsonDef.Documentation ??= csharpDef.Documentation;
                    jsonDef.Signature ??= csharpDef.Signature;
                    jsonDef.Language = Utils.CSharpLanguageID;

                    // Recalculate these just in case
                    jsonDef.MinParameterCount = jsonDef.Parameters?.Count(p => p.DefaultValue == null && !p.IsParamsArray);
                    jsonDef.MaxParameterCount = jsonDef.Parameters?.Any(p => p.IsParamsArray) == true ? null : jsonDef.Parameters.Count();

                    return jsonDef;
                });
            foreach (var fd in results)
            {
                functionDefinitionCache[fd.YarnName] = fd;
            }

            foreach (var f in CSharpFiles)
            {
                var uri = f.Key;
                var file = f.Value;
                if (file.UnmatchableBridges.Any())
                {
                    foreach (var bridge in file.UnmatchableBridges)
                    {
                        if (!functionDefinitionCache.ContainsKey(bridge.yarnName))
                        {
                            functionDefinitionCache[bridge.yarnName] = new RegisteredDefinition
                            {
                                DefinitionFile = uri,
                                DefinitionName = bridge.yarnName,
                                DefinitionRange = bridge.definitionRange,
                                IsCommand = bridge.isCommand,
                                Language = Utils.CSharpLanguageID,
                                YarnName = bridge.yarnName,
                                Documentation = string.Empty,
                                Priority = 10,
                                Signature = $"{bridge.yarnName}(?)",
                            };
                        }
                    }
                }
            }
        }
    }
}