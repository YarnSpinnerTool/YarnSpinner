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

            results = results.Concat(VariableAlreadyDeclared(yarnFile, workspace));
            results = results.Concat(WrongParameterCountFunctions(yarnFile, workspace));

            return results;
        }

        private static IEnumerable<Diagnostic> VariableAlreadyDeclared(YarnFileData yarnFile, Workspace workspace)
        {
            var allVariables = workspace.GetVariables();
            var duplicates = allVariables
                .Where(workspaceDeclarations => yarnFile.DeclaredVariables.Any(dv => dv.Name == workspaceDeclarations.Name))
                .GroupBy(v => v.Name)
                .Where(g => g.Count() > 1)
                .SelectMany(grouping => grouping
                    .Where(item => item.DefinitionFile == yarnFile.Uri))
                ;
            var results = duplicates
                .Select(d => new Diagnostic
                {
                    Message = $" A variable named {d.Name} is already declared.",
                    Severity = DiagnosticSeverity.Error,
                    Range = d.DefinitionRange,
                    Code = new DiagnosticCode(nameof(YarnDiagnosticCode.YRNDupVarDec)),
                });

            return results;
        }

        private static IEnumerable<Diagnostic> WrongParameterCountFunctions(YarnFileData yarnFile, Workspace workspace)
        {
            var results = new List<Diagnostic>();
            // Todo: create new config flag for this, functions can be defined in more places than C#.
            if (workspace.Configuration.CSharpLookup)
            {
                yarnFile.FunctionInfos.Concat(yarnFile.CommandInfos).ForEach(fi =>
                {
                    var defs = workspace.LookupFunctions(fi.Name).Where(def => def.IsCommand == fi.IsCommand);
                    if (!defs.Any()) { return; }
                    var def = defs.First();

                    // TODO: probably cleaner to have min/max ranges on the number of parameters
                    if (fi.ParameterCount == def.Parameters.Count()) { return; }
                    if (fi.ParameterCount > def.Parameters.Count() &&
                        def.Parameters.Any() &&
                        def.Parameters.Last().IsParamsArray) { return; }
                    if (fi.ParameterCount < def.Parameters.Count() &&
                        fi.ParameterCount >=
                            def.Parameters.Count() - def.Parameters.Where(p => !string.IsNullOrWhiteSpace(p.DefaultValue)).Count()
                    ) { return; }

                    results.Add(new Diagnostic
                    {
                        Message = $"Incorrect number of parameters",
                        Severity = DiagnosticSeverity.Error,
                        Range = fi.ParametersRange,
                        Code = nameof(YarnDiagnosticCode.YRNCmdParamCnt),
                    });
                });
            }

            return results;
        }
    }
}
