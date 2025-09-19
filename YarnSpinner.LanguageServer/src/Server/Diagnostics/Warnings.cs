using MoreLinq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Linq;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer.Diagnostics
{
    internal static class Warnings
    {
        public static IEnumerable<Diagnostic> GetWarnings(YarnFileData yarnFile, Configuration? configuration)
        {
            var results = Enumerable.Empty<Diagnostic>();

            results = results.Concat(UnknownCommands(yarnFile));
            results = results.Concat(UndefinedFunctions(yarnFile, configuration));
            results = results.Concat(UndefinedJumpDestination(yarnFile));

            return results;
        }

        private static IEnumerable<Diagnostic> UnknownCommands(YarnFileData yarnFile)
        {
            if (yarnFile.Project == null)
            {
                // No known project for this file; no diagnostics we can produce
                yield break;
            }

            var knownCommands = yarnFile.Project.Commands;
            foreach (var commandReference in yarnFile.CommandReferences)
            {
                if (knownCommands.Any(c => c.YarnName == commandReference.Name) == false)
                {
                    // We don't know what command this is referring to.
                    yield return new Diagnostic
                    {
                        Message = $"Could not find {(commandReference.IsCommand ? "command" : "function")} definition for {commandReference.Name}",
                        Severity = DiagnosticSeverity.Warning,
                        Range = new Range(commandReference.ExpressionRange.Start, commandReference.ParametersRange.Start.Delta(0, -1)),
                        Code = nameof(YarnDiagnosticCode.YRNMsngCmdDef),
                        Data = JToken.FromObject((commandReference.Name, commandReference.IsCommand)),
                    };
                }
            }
        }

        private static IEnumerable<Diagnostic> UndefinedFunctions(YarnFileData yarnFile, Configuration? configuration)
        {
            // Todo: create new config flag for this, functions can be defined
            // in more places than C#.
            if (!(configuration?.CSharpLookup ?? false))
            {
                yield break;
            }

            var project = yarnFile.Project;
            var knownFunctions = project?.Functions;

            if (knownFunctions == null)
            {
                // No known functions; we can't produce any diagnostics
                yield break;
            }

            foreach (var functionReference in yarnFile.FunctionReferences)
            {
                if (!knownFunctions.Any(f => f.YarnName == functionReference.Name))
                {
                    // We don't know what function this is referring to.
                    yield return new Diagnostic
                    {
                        Message = $"Could not find {(functionReference.IsCommand ? "command" : "function")} definition for {functionReference.Name}",
                        Severity = DiagnosticSeverity.Warning,
                        Range = new Range(functionReference.ExpressionRange.Start, functionReference.ParametersRange.Start.Delta(0, -1)),
                        Code = nameof(YarnDiagnosticCode.YRNMsngCmdDef),
                        Data = JToken.FromObject((functionReference.Name, functionReference.IsCommand)), // Include enough info to do fuzzy string matching on defined function names
                    };
                }
            }
        }



        private static IEnumerable<Diagnostic> UndefinedJumpDestination(YarnFileData yarnFile)
        {
            var project = yarnFile.Project;

            if (project == null)
            {
                return Enumerable.Empty<Diagnostic>();
            }

            var undefinedJumpTargets = yarnFile.NodeJumps
                .Where(jump => !project.FindNodes(jump.DestinationTitle).Any());

            return undefinedJumpTargets.Select(t => new Diagnostic
            {
                Message = $"Jump to unknown node '{t.DestinationTitle}'",
                Severity = DiagnosticSeverity.Warning,
                Range = PositionHelper.GetRange(yarnFile.LineStarts, t.DestinationToken),
                Code = nameof(YarnDiagnosticCode.YRNMsngJumpDest),
                Data = JToken.FromObject(t.DestinationTitle),
            });
        }
    }
}
