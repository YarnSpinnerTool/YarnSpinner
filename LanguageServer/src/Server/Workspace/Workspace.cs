using System;
using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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

        private Dictionary<string, RegisteredDefinition> functionDefinitionCache = new Dictionary<string, RegisteredDefinition>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Workspace"/> class.
        /// </summary>
        public Workspace()
        {
            Configuration = new Configuration(this);
        }

        public void Initialize(ILanguageServer languageServer = null)
        {
            if (languageServer != null)
            {
                this.LanguageServer = languageServer;
            }

            var jsonWorkspaceFiles = System.IO.Directory.EnumerateFiles(Root, "*.ysls.json", System.IO.SearchOption.AllDirectories);
            foreach (var file in jsonWorkspaceFiles)
            {
                var uri = new Uri(file);
                var text = System.IO.File.ReadAllText(file);
                var docJsonConfig = new JsonConfigFile(text, uri, this, false);
                if (docJsonConfig != null) {
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
                    if (docJsonConfig != null) {
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

            var yarnFiles = System.IO.Directory.EnumerateFiles(Root, "*.yarn", System.IO.SearchOption.AllDirectories);
            yarnFiles = yarnFiles.Where(f => !f.Contains("PackageCache") && !f.Contains("Library"));
            foreach (var file in yarnFiles)
            {
                var text = System.IO.File.ReadAllText(file);
                var uri = new Uri(file);
                YarnFiles[uri] = new YarnFileData(text, uri, this);
            }
        }

        public IEnumerable<(Uri uri, string title, Range range)> GetNodeTitles()
        {
            return YarnFiles.Values.SelectMany(yarnFile => yarnFile.NodeTitles.Select(titleToken => (yarnFile.Uri, titleToken.Text, PositionHelper.GetRange(yarnFile.LineStarts, titleToken))).Distinct()).Distinct();
        }

        public IEnumerable<string> GetVariableNames()
        {
            return YarnFiles.Values.SelectMany(yarnFile => yarnFile.Variables.Select(variableToken => variableToken.Text).Distinct()).Distinct();
        }

        public IEnumerable<YarnVariableDeclaration> GetVariables(string name = null, bool fuzzyMatch = false)
        {
            var results = Enumerable.Empty<YarnVariableDeclaration>();
            foreach (var fileEntry in YarnFiles)
            {
                var file = fileEntry.Value;
                if (!fuzzyMatch)
                {
                    results = results.Concat(file.DeclaredVariables.Where(v => name == null || v.Name == name));
                }
                else if (fuzzyMatch)
                {
                    results = results.Concat(file.DeclaredVariables);
                }
            }

            if (fuzzyMatch)
            {
                // Todo: Refactor this part out and use for variables and functions
                var threshold = Configuration.DidYouMeanThreshold;
                var lev = new Fastenshtein.Levenshtein(name.ToLower());
                results = results
                    .Select(fd =>
                    {
                        float distance = lev.DistanceFrom(fd.Name.ToLower());
                        var normalizedDistance = distance / Math.Max(Math.Max(name.Length, fd.Name.Length), 1);

                        if (distance <= 1 || fd.Name.ToLower().Contains(name.ToLower()) || name.ToLower().Contains(fd.Name.ToLower()))
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

        private void FillFunctionDefinitionCache()
        {
            functionDefinitionCache.Clear();

            var results = JsonConfigFiles.Values.Select(v => (IDefinitionsProvider)v).Concat(CSharpFiles.Values.Select(v => v)).SelectMany(fp => fp.Definitions.Values.Select(v => v));

            // Now we need to merge any json / c# versions of definitons, where json is the base, and c# fills in anything missing
            // TODO: There's probably a better way to do this merge
            // Test cases to consider, multiple json entries and no csharp entries. Multiple valid definitions in csharp.
            results = results
                .GroupBy(f => f.YarnName).Select(g => {
                    if (g.Count() == 1) { return g.First(); }

                    var jsonDef = g.FirstOrDefault(f => f.DefinitionFile.ToString().EndsWith(".json"));
                    var csharpDef = g.FirstOrDefault(f => f.DefinitionFile.ToString().EndsWith(".cs") && !f.Signature.EndsWith("(?)"));
                    if (string.IsNullOrWhiteSpace(csharpDef.YarnName)) { csharpDef = g.FirstOrDefault(f => f.DefinitionFile.ToString().EndsWith(".cs")); }

                    jsonDef.DefinitionFile = csharpDef.DefinitionFile;
                    jsonDef.DefinitionRange = csharpDef.DefinitionRange;
                    jsonDef.Parameters = jsonDef.Parameters ?? csharpDef.Parameters;
                    jsonDef.MinParameterCount = jsonDef.MinParameterCount ?? csharpDef.MinParameterCount;
                    jsonDef.MaxParameterCount = jsonDef.MaxParameterCount ?? csharpDef.MaxParameterCount;
                    jsonDef.Documentation = jsonDef.Documentation ?? csharpDef.Documentation;
                    jsonDef.Signature = jsonDef.Signature ?? csharpDef.Signature;
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
                                Signature = $"{bridge.yarnName}(?)",
                            };
                        }
                    }
                }
            }
        }
    }
}