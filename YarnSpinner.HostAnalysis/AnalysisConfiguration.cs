#nullable enable

namespace Yarn.HostAnalysis
{
    using Microsoft.CodeAnalysis;
    using System.Linq;
    using System.Collections.Generic;

    public record class AnalysisConfiguration(bool SkipSourceGeneration, bool SkipYSLSGeneration, bool WriteDebugFiles)
    {
        public AnalysisConfiguration(): this(false, false, false) { }

        public AnalysisConfiguration(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions options): this(false, false, false)
        {
            if (options.TryGetValue("ys.should_write_debug_files", out var value))
            {
                if (bool.TryParse(value, out var shouldWriteDebugFiles))
                {
                    WriteDebugFiles = shouldWriteDebugFiles;
                }
            }
            if (options.TryGetValue("ys.should_skip_source_gen", out value))
            {
                if (bool.TryParse(value, out var shouldSkipSourceGen))
                {
                    SkipSourceGeneration = shouldSkipSourceGen;
                }
            }
            if (options.TryGetValue("ys.should_skip_ysls_gen", out value))
            {
                if (bool.TryParse(value, out var shouldSkipYSLS))
                {
                    SkipYSLSGeneration = shouldSkipYSLS;
                }
            }

            foreach (var id in diagIDs)
            {
                if (options.TryGetValue($"ys.severity.{id}", out value))
                {
                    switch (value)
                    {
                        case "Error":
                            modifiedSeverity[id] = DiagnosticSeverity.Error;
                            break;
                        case "Hidden":
                            modifiedSeverity[id] = DiagnosticSeverity.Hidden;
                            break;
                        case "Info":
                            modifiedSeverity[id] = DiagnosticSeverity.Info;
                            break;
                        case "Warning":
                            modifiedSeverity[id] = DiagnosticSeverity.Warning;
                            break;
                    }
                }
            }
        }

        private static readonly string[] diagIDs =
        {
            ActionDiagnostics.YS1000InternalErrorProcessingAction.Id,
            ActionDiagnostics.YS1001ActionMethodsMustBePublic.Id,
            ActionDiagnostics.YS1002ActionMethodsMustHaveAValidName.Id,
            ActionDiagnostics.YS1003CommandMethodsMustHaveAValidReturnType.Id,
            ActionDiagnostics.YS1004FunctionMethodsMustHaveAValidReturnType.Id,
            ActionDiagnostics.YS1005ActionsParamsArraysMustBeOfYarnTypes.Id,
            ActionDiagnostics.YS1006CancellationTokenInWrongLocation.Id,
            ActionDiagnostics.YS1007ArrayInWrongLocation.Id,
            ActionDiagnostics.YS1008ActionsParameterIsAnIncompatibleType.Id,
            ActionDiagnostics.YS1009InstanceActionIsOnAnIncompatibleType.Id,
            ActionDiagnostics.YS1010ParameterIsAnOut.Id,
            ActionDiagnostics.YS1011ConverterMethodIsNotStatic.Id,
            ActionDiagnostics.YS1012ConverterMethodIsNotPublic.Id,
            ActionDiagnostics.YS1013ConverterReturnsInvalidType.Id,
            ActionDiagnostics.YS1014IncorrectNumberOfConverterParameters.Id,
            ActionDiagnostics.YS1015ConverterHasInvalidInputParameter.Id,
            ActionDiagnostics.YS1016ConverterMissingOutParam.Id,
            ActionDiagnostics.YS1017ConverterTypeMismatch.Id,
            ActionDiagnostics.YS1018DuplicateConverter.Id,
            ActionDiagnostics.YS1019DuplicateAction.Id,
            ActionDiagnostics.YS1020UnableToResolveActionName.Id,
            ActionDiagnostics.YS1021ActionIsALambda.Id,
            ActionDiagnostics.YS1022ActionsEnumAttributedParameterIsOfIncompatibleType.Id,
            ActionDiagnostics.YS1023ActionsNodeAttributedParameterIsOfIncompatibleType.Id,
            ActionDiagnostics.YS1024ActionIsALocalFunction.Id,
            ActionDiagnostics.YS1025DirectActionIsPrivate.Id,
            ActionDiagnostics.YS1026FunctionUsesMetaToken.Id,
            ActionDiagnostics.YS1027ActionIsRegisteredAsADelegate.Id,
        };

        private Dictionary<string, DiagnosticSeverity> modifiedSeverity = new();

        // Don't generate source code for certain Yarn Spinner provided
        // assemblies - these always manually register any actions in them.
        private static readonly string[] prefixesToIgnore = 
        {
            "YarnSpinner.Unity",
            "YarnSpinner.Editor",
        };
        // But DO generate source code for the Samples assembly
        private static readonly string[] prefixesToKeep = 
        {
            "YarnSpinner.Unity.Samples",
        };

        public bool CanGenerateCode(string assemblyName)
        {
            if (prefixesToIgnore.Any(prefix => assemblyName.StartsWith(prefix)) && !prefixesToKeep.Any(prefix => assemblyName.StartsWith(prefix)))
            {
                return false;
            }
            return !SkipSourceGeneration;
        }
        public Diagnostic CreateElevatedDiagnostic(DiagnosticDescriptor descriptor, Location location, params object?[]? messageArguments)
        {
            return CreateElevatedDiagnosticWithSeverity(descriptor, descriptor.DefaultSeverity, location, messageArguments);
        }
        public Diagnostic CreateElevatedDiagnosticWithSeverity(DiagnosticDescriptor descriptor, DiagnosticSeverity severity, Location location, params object?[]? messageArguments)
        {
            // if there exists a modified severity we use that
            // otherwise we use the provided severity
            if (!modifiedSeverity.TryGetValue(descriptor.Id, out var diagnosticSeverity))
            {
                diagnosticSeverity = severity;
            }

            return Diagnostic.Create(descriptor, location, diagnosticSeverity, null, null, messageArguments);
        }
    }
}

namespace System.Runtime.CompilerServices { class IsExternalInit { } }