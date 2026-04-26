using Microsoft.CodeAnalysis;
using System.Text;

namespace YarnSpinner.Diagnostics;

/// <summary>
/// An incremental source generator that takes a collection of markdown
/// documents provided as additional files, extracts diagnostic descriptor
/// information from them, and produces code that instantiates a
/// DiagnosticDescriptor object for each document.
/// </summary>
[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext initContext)
    {
        // Get all .md files provided to us
        IncrementalValuesProvider<AdditionalText> textFiles =
            initContext.AdditionalTextsProvider.Where(static file => file.Path.EndsWith(".md"));

        // Read their contents, extract the data, and return information about
        // it
        IncrementalValuesProvider<(string name, DiagnosticFrontMatter? diagnosticInfo, string content)> namesAndContents =
            textFiles.Select(static (text, cancellationToken) =>
            {
                var contents = text.GetText(cancellationToken)!.ToString();
                try
                {
                    var (data, markdown) = contents.GetFrontMatter<DiagnosticFrontMatter>();

                    return (
                        name: Path.GetFileNameWithoutExtension(text.Path),
                        diagnosticInfo: data,
                        content: markdown
                    );
                }
                catch (YamlDotNet.Core.YamlException e)
                {
                    throw new YamlDotNet.Core.YamlException(text.Path + ": " + e.Message);
                }
            });

        var diagnosticNames = namesAndContents.Collect();

        // For each document that describes a diagnostic, produce code that
        // instantiates a descriptor for that diagnostic.
        initContext.RegisterSourceOutput(namesAndContents, (spc, input) =>
            {
                var (name, diagnosticInfo, content) = input;

                if (diagnosticInfo == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(diagnosticInfo.Code) || string.IsNullOrEmpty(diagnosticInfo.Name))
                {
                    // TODO: report a diagnostic
                    return;
                }

                var descriptorType = diagnosticInfo.MessageValues.Count switch
                {
                    0 => "DiagnosticDescriptor0",
                    1 => "DiagnosticDescriptor1",
                    2 => "DiagnosticDescriptor2",
                    3 => "DiagnosticDescriptor3",
                    4 => "DiagnosticDescriptor4",
                    _ => throw new Exception($"Diagnostic {diagnosticInfo.Code} has too many message values ({diagnosticInfo.MessageValues.Count})"),
                };

                static string GetSeverityName(DiagnosticFrontMatter.Severity severity)
                {
                    return severity switch
                    {
                        DiagnosticFrontMatter.Severity.Error => "Error",
                        DiagnosticFrontMatter.Severity.Warning => "Warning",
                        DiagnosticFrontMatter.Severity.Info => "Info",
                        DiagnosticFrontMatter.Severity.None => "None",
                        _ => throw new Exception($"Unknown severity {severity}")
                    };
                }

                var defaultSeverity = diagnosticInfo.DefaultSeverity;
                var minimumSeverity = diagnosticInfo.MinimumSeverity;

                if (minimumSeverity == DiagnosticFrontMatter.Severity.NotSet)
                {
                    minimumSeverity = defaultSeverity;
                }

                var diagnosticType = @$"
            public static readonly {descriptorType} {diagnosticInfo.Name} = new(
                code: ""{diagnosticInfo.Code?.Escape()}"",
                messageTemplate: ""{diagnosticInfo.MessageTemplate?.Escape()}"",
                defaultSeverity: Diagnostic.DiagnosticSeverity.{GetSeverityName(defaultSeverity)},
                minimumSeverity: Diagnostic.DiagnosticSeverity.{GetSeverityName(minimumSeverity)},
                description: ""{diagnosticInfo.Description?.Escape()}""
            );";

                var commentSB = new StringBuilder();
                commentSB.AppendLine($"/// <summary>{diagnosticInfo.Code}: {diagnosticInfo.Description?.UnwrapAndTrim().EscapeXML()}</summary>");

                commentSB.AppendLine("/// <remarks>");
                if (string.IsNullOrEmpty(diagnosticInfo.Summary) == false)
                {
                    commentSB.AppendLine($"/// <para>{diagnosticInfo.Summary?.UnwrapAndTrim().EscapeXML()}</para>");
                }
                if (diagnosticInfo.MessageValues.Count > 0)
                {
                    commentSB.AppendLine("/// <para>Format placeholders:");
                    commentSB.AppendLine("/// <list type=\"number\">");
                    foreach (var value in diagnosticInfo.MessageValues)
                    {
                        commentSB.AppendLine("/// <item>" + value.UnwrapAndTrim().EscapeXML() + "</item>");
                    }
                    commentSB.AppendLine("/// </list></para>");


                }
                commentSB.AppendLine("/// </remarks>");

                var deprecationSB = new StringBuilder();

                if (string.IsNullOrWhiteSpace(diagnosticInfo.DeprecatedVersion) == false)
                {
                    // The diagnostic is deprecated; add an Obsolete attribute to it
                    deprecationSB.Append("[System.Obsolete");
                    if (diagnosticInfo.DeprecationNote != null)
                    {
                        // We have text we can include to explain the deprecation
                        deprecationSB.Append("(\"" + diagnosticInfo.DeprecationNote.Escape() + "\")");
                    }
                    deprecationSB.AppendLine("]");
                }

                spc.AddSource($"DiagnosticDescriptors.{name}", $@"
            namespace Yarn.Compiler {{

                public partial class DiagnosticDescriptor
                {{
                    {commentSB}
                    {deprecationSB}
                    {diagnosticType}
                }}
            }}");
            });

        // Additionally, provide code that creates a dictionary mapping codes to the descriptor object.
        initContext.RegisterSourceOutput(diagnosticNames, (spc, input) =>
        {

            var allValidDiagnostics = input.Where(v => v.diagnosticInfo != null
                                                       && !string.IsNullOrEmpty(v.diagnosticInfo.Code)
                                                       && !string.IsNullOrEmpty(v.diagnosticInfo.Name));


            var sourceSB = new StringBuilder();
            sourceSB.AppendLine("using System.Collections.Generic;");
            // dict may reference obsolete diagnostics, we don't want it to complain about that
            sourceSB.AppendLine("#pragma warning disable CS0618");
            sourceSB.AppendLine("namespace Yarn.Compiler { public partial class DiagnosticDescriptor {");
            sourceSB.AppendLine("/// <summary>Returns a dictionary mapping diagnostic codes to their corresponding descriptor object.</summary>");
            sourceSB.AppendLine("/// <remarks>This method is automatically generated.</remarks>");
            sourceSB.AppendLine("private static Dictionary<string, DiagnosticDescriptor> GetDescriptorDictionary() {");
            sourceSB.AppendLine("  return new() {");

            foreach (var (_, diagnosticInfo, _) in allValidDiagnostics)
            {
                sourceSB.AppendLine($"    {{\"{diagnosticInfo!.Code}\", DiagnosticDescriptor.{diagnosticInfo.Name}}},");
            }

            sourceSB.AppendLine("  };");
            sourceSB.AppendLine("}");
            sourceSB.AppendLine("} }");

            spc.AddSource("DiagnosticDescriptors.Dictionary", sourceSB.ToString());
        });
    }
}
