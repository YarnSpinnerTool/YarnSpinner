using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer.Diagnostics
{
    internal static class Warnings
    {
        public static IEnumerable<Diagnostic> GetWarnings(YarnFileData yarnFile, Workspace workspace)
        {
            var results = Enumerable.Empty<Diagnostic>();

            results = results.Concat(UndefinedFunctions(yarnFile, workspace));
            results = results.Concat(UndeclaredVariables(yarnFile, workspace));

            return results;
        }

        private static IEnumerable<Diagnostic> UndefinedFunctions(YarnFileData yarnFile, Workspace workspace)
        {
            // Todo: create new config flag for this, functions can be defined in more places than C#.
            if (workspace.Configuration.CSharpLookup)
            {
                var undefinedFunctions = yarnFile.FunctionInfos.Concat(yarnFile.CommandInfos)
                    .Where(fi => !workspace.LookupFunctions(fi.Name).Any());

                var warnings = undefinedFunctions.Select(f => new Diagnostic
                {
                    Message = $"Could not find {(f.IsCommand ? "command" : "function")} definition for {f.Name}",
                    Severity = DiagnosticSeverity.Warning,
                    Range = new Range(f.ExpressionRange.Start, f.ParametersRange.Start.Delta(0, -1)),
                    Code = nameof(YarnDiagnosticCode.YRNMsngCmdDef),
                    Data = JToken.FromObject((Name: f.Name, IsCommand: f.IsCommand)), // Include enough info to do fuzzy string matching on defined function names
                });
                return warnings;
            }

            return Enumerable.Empty<Diagnostic>();
        }

        private static IEnumerable<Diagnostic> UndeclaredVariables(YarnFileData yarnFile, Workspace workspace)
        {
            var undeclaredVariables = yarnFile.Variables.Where(v => !workspace.GetVariables(v.Text).Any());

            var warnings = undeclaredVariables.Select(v => new Diagnostic
            {
                Message = $"Could not find variable declaration",
                Severity = DiagnosticSeverity.Warning,
                Range = PositionHelper.GetRange(yarnFile.LineStarts, v),
                Code = nameof(YarnDiagnosticCode.YRNMsngVarDec),
                Data = JToken.FromObject(v.Text),
            });
            return warnings;
        }
    }
}