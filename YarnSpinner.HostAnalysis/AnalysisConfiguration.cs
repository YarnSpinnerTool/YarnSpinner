// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable

namespace Yarn.HostAnalysis
{
    using Microsoft.CodeAnalysis;
    using System.Linq;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a config bundle for the source analyser and generator to make use of.
    /// Currently has very limited functionality and is primarily for development but may be of use to some advanced users.
    /// </summary>
    /// <remarks>
    /// To make use of this in Unity create a new file called <c>Default.globalconfig</c> in the root of the Assets folder.
    /// The name and location of this file are not optional!
    /// This file is an ini file and you can set various keys to control how the source generator and analyser work.
    /// <para>
    /// <c>ys.should_write_debug_files</c> will make it so the generated C# files are also written to disk in the temp folder.
    /// You will have had to make the temporary folder first and it's location will change on an OS by OS basis.
    /// </para>
    /// <para>
    /// <c>ys.should_skip_source_gen</c> will make it so source generation is skipped entirely.
    /// This is probably not something you want to do...
    /// </para>
    /// <para>
    /// <c>ys.should_skip_ysls_gen</c> will make it so YSLS generation is skipped.
    /// This is probably not something you want to do...
    /// </para>
    /// <para>
    /// <c>ys.severity.id</c> Allows you to raise or lower the severity of a diagnostic.
    /// The id must be the replaced with the id of one of the <see cref="ActionDiagnostics"/> defined diagnostic codes.
    /// Allowed values (in order of severity) are:
    /// <list type="bullet">
        /// <item>
        /// Hidden
        /// </item>
        /// <item>
        /// Info
        /// </item>
        /// <item>
        /// Warning
        /// </item>
        /// <item>
        /// Error
        /// </item>
    /// </list>
    /// These values are case sensitive!
    /// </para>
    /// </remarks>
    /// <param name="SkipSourceGeneration">Should source generation be skipped entirely?</param>
    /// <param name="SkipYSLSGeneration"></param>
    /// <param name="WriteDebugFiles"></param>
    public record class AnalysisConfiguration(bool SkipSourceGeneration, bool SkipYSLSGeneration, bool WriteDebugFiles)
    {
        /// <inheritdoc cref="AnalysisConfiguration(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions)"/>
        public AnalysisConfiguration(): this(false, false, false) { }

        /// <summary>
        /// Creates a new configuration record from a roslyn analyser configuration.
        /// This is the way to make these because the config file made in Unity will be automatically provided without needing to hook up and additional file pipeline.
        /// </summary>
        /// <param name="options"></param>
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
            "Unity.",
            "UnityEditor.",
            "UnityEngine.",
            "UniTask",
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
        public Diagnostic CreateElevatedDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArguments)
        {
            return CreateElevatedDiagnosticWithSeverity(descriptor, descriptor.DefaultSeverity, location, messageArguments);
        }
        public Diagnostic CreateElevatedDiagnosticWithSeverity(DiagnosticDescriptor descriptor, DiagnosticSeverity severity, Location? location, params object?[]? messageArguments)
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