using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer.Diagnostics
{
    internal class SemanticErrors
    {
        public static IEnumerable<Diagnostic> GetErrors(YarnFileData yarnFile, Workspace workspace)
        {
            var results = Enumerable.Empty<Diagnostic>();

            results = results.Concat(WrongParameterCountFunctions(yarnFile, workspace));

            return results;
        }

        private static IEnumerable<Diagnostic> WrongParameterCountFunctions(YarnFileData yarnFile, Workspace workspace)
        {
            var results = new List<Diagnostic>();
            // Todo: create new config flag for this, functions can be defined in more places than C#.
            if (workspace.Configuration.CSharpLookup)
            {
                yarnFile.FunctionReferences.Concat(yarnFile.CommandReferences).ForEach(fi =>
                {
                    var defs = workspace.LookupFunctions(fi.Name).Where(def => def.IsCommand == fi.IsCommand);
                    if (!defs.Any()) { return; }
                    var def = defs.First();

                    if (def.MinParameterCount.HasValue && fi.ParameterCount < def.MinParameterCount)
                    {
                        results.Add(new Diagnostic
                        {
                            Message = $"Too few parameters. Expected at least {def.MinParameterCount}.",
                            Severity = DiagnosticSeverity.Error,
                            Range = fi.ExpressionRange,
                            Code = nameof(YarnDiagnosticCode.YRNCmdParamCnt),
                        });
                    }

                    if (def.MaxParameterCount.HasValue && fi.ParameterCount > def.MaxParameterCount)
                    {
                        results.Add(new Diagnostic
                        {
                            Message = $"Too many parameters. Expected at most {def.MaxParameterCount}.",
                            Severity = DiagnosticSeverity.Error,
                            Range = fi.ExpressionRange,
                            Code = nameof(YarnDiagnosticCode.YRNCmdParamCnt),
                        });
                    }
                });
            }

            return results;
        }
    }
}
