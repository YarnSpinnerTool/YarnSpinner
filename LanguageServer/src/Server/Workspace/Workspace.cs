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

            if (Configuration.CSharpLookup)
            {
                var csharpWorkspaceFiles = System.IO.Directory.EnumerateFiles(Root, "*.cs", System.IO.SearchOption.AllDirectories);
                csharpWorkspaceFiles = csharpWorkspaceFiles.Where(f => !f.Contains("PackageCache") && !f.Contains("Library"));
                foreach (var file in csharpWorkspaceFiles)
                {
                    var text = System.IO.File.ReadAllText(file);
                    if (text.IndexOf("YarnCommand") > 0
                    || text.IndexOf("AddCommandHandler") > 0
                    || text.IndexOf("AddFunction") > 0)
                    {
                        // add to list of csharp files of interest / subscribe to updates
                        var uri = new Uri(file);
                        CSharpFiles[uri] = new CSharpFileData(text, uri);
                    }
                }
            }
            else
            {
                // Probably excesive
                CSharpFiles.Clear();
            }

            // TODO Refactor this out into a method like (manifestPathPrefix) => (docuri, doctext)
            try
            {
                var thisAssembly = typeof(Workspace).Assembly;
                var resources = thisAssembly.GetManifestResourceNames();
                var docs = resources.Where(r => r.StartsWith("YarnLanguageServer.src.Server.Documentation."));

                foreach (var doc in docs)
                {
                    var hmm = new Uri(thisAssembly.Location);
                    var docUri = new Uri(System.IO.Path.GetDirectoryName(new Uri(thisAssembly.Location).AbsolutePath));
                    string docText;
                    using (var reader = new System.IO.StreamReader(thisAssembly.GetManifestResourceStream(doc)))
                    {
                        docText = reader.ReadToEnd();
                    }

                    var docJsonConfig = new JsonConfigFile(docText, docUri);
                    if (docJsonConfig != null) { JsonConfigFiles[docUri] = docJsonConfig; }
                }
            }
            catch (Exception) { }

            var yarnFiles = System.IO.Directory.EnumerateFiles(Root, "*.yarn", System.IO.SearchOption.AllDirectories);
            foreach (var file in yarnFiles)
            {
                var text = System.IO.File.ReadAllText(file);
                var uri = new Uri(file);
                YarnFiles[uri] = new YarnFileData(text, uri, this);
            }
        }

        public IEnumerable<IFunctionDefinitionsProvider> FunctionDefinitionsProviders()
        {
            return JsonConfigFiles.Values.Select(v => (IFunctionDefinitionsProvider)v).Concat(CSharpFiles.Values.Select(v => v));
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

        public IEnumerable<RegisteredFunction> GetFunctions()
        {
            var results = Enumerable.Empty<RegisteredFunction>();
            foreach (var file in FunctionDefinitionsProviders())
            {
                results = results.Concat(file.FunctionDefinitions.Where(kv => !kv.Value.IsCommand).Select(kv => kv.Value));
            }

            return results;
        }

        public IEnumerable<RegisteredFunction> GetCommands()
        {
            var results = Enumerable.Empty<RegisteredFunction>();
            foreach (var file in FunctionDefinitionsProviders())
            {
                results = results.Concat(file.FunctionDefinitions.Where(kv => kv.Value.IsCommand).Select(kv => kv.Value));
            }

            return results;
        }

        /// <summary>
        /// Get Function or Command definition information for command/functions registered with Yarn Spinner.
        /// </summary>
        /// <param name="functionName">Search string. Note that this is the registered Yarn name, not the name of what is defined in code.</param>
        /// <param name="fuzzyMatch">Match names that contain or are close to the input, and not just an exact match.</param>
        /// <returns>Enumerable of matches. Note that calling function needs to filter which of commands or functions that it wants.</returns>
        public IEnumerable<RegisteredFunction> LookupFunctions(string functionName, bool fuzzyMatch = false)
        {
            var results = Enumerable.Empty<RegisteredFunction>();

            foreach (var file in FunctionDefinitionsProviders())
            {
                if (!fuzzyMatch && file.FunctionDefinitions.TryGetValue(functionName, out var function))
                {
                    return new List<RegisteredFunction> { function };
                }
                else if (fuzzyMatch)
                {
                    results = results.Concat(file.FunctionDefinitions.Values);
                }
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
    }
}