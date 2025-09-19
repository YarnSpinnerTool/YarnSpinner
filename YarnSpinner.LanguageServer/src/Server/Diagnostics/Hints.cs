
using MoreLinq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Linq;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer.Diagnostics
{
    internal static class Hints
    {
        public static IEnumerable<Diagnostic> GetHints(YarnFileData yarnFile, Configuration? configuration)
        {
            var results = Enumerable.Empty<Diagnostic>();

            results = results.Concat(UndeclaredVariables(yarnFile));

            return results;
        }
        private static IEnumerable<Diagnostic> UndeclaredVariables(YarnFileData yarnFile)
        {
            var project = yarnFile.Project;

            if (project == null)
            {
                // No project, so no diagnostics we can produce
                return Enumerable.Empty<Diagnostic>();
            }

            // Find all variable references in this file where the declaration,
            // if any, is an implicit one. If it is, then we should suggest that
            // the user create a declaration for it.
            var undeclaredVariables = yarnFile.VariableReferences
                .Where(@ref => project.FindVariables(@ref.Text)
                    .FirstOrDefault()?
                    .IsImplicit ?? false
                );

            return undeclaredVariables.Select(v => new Diagnostic
            {
                Message = "Variable should be declared",
                Severity = DiagnosticSeverity.Hint,
                Range = PositionHelper.GetRange(yarnFile.LineStarts, v),
                Code = nameof(YarnDiagnosticCode.YRNMsngVarDec),
                Data = JToken.FromObject(v.Text),
            });
        }
    }
}
