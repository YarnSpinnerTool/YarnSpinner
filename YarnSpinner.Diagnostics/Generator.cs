using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Microsoft.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace YarnSpinner.Diagnostics;

/// <summary>
/// Utility methods for working with Markdown and YAML frontmatter
/// </summary>
public static class MarkdownExtensions
{
    private static readonly IDeserializer YamlDeserializer =
        new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly MarkdownPipeline Pipeline
        = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .Build();

    private static readonly Regex EscapedCharactersRegex = new(@"[\n\r""]");
    private static readonly Regex NewlinesRegex = new(@"[\n\r]");
    private static readonly Regex MultipleSpacesRegex = new(@"\s{2,}");

    /// <summary>
    /// Escapes a string by adding a backslash \ in front of characters that
    /// can't appear inside a string literal in C#
    /// </summary>
    /// <param name="str">The string to escape</param>
    /// <returns>The escaped string</returns>
    public static string Escape(this string str)
    {
        return EscapedCharactersRegex.Replace(str, d => "\\" + d.Value);
    }

    /// <summary>
    /// Unwraps a string into a single line by replacing all newlines with
    /// spaces, and collapsing multiple spaces into a single space, and then
    /// trimming it.
    /// </summary>
    /// <param name="str">The string to unwrap.</param>
    /// <returns>The unwrapped string.</returns>
    public static string UnwrapAndTrim(this string str)
    {

        var working = NewlinesRegex.Replace(str, " ");
        working = MultipleSpacesRegex.Replace(working, " ");
        return working.Trim();
    }

    /// <summary>
    /// Replaces XML-unsafe characters (&lt; and &gt;) with HTML entities.
    /// </summary>
    /// <param name="str">The string to escape.</param>
    /// <returns>The escaped string.</returns>
    public static string EscapeXML(this string str)
    {
        return str.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>
    /// Parses a document containing markdown to get its front matter, and
    /// returns the parsed result and the remaining markdown.
    /// </summary>
    /// <typeparam name="T">The type of frontmatter to parse.</typeparam>
    /// <param name="markdown">The markdown to parse.</param>
    /// <returns>A tuple containing the parsed frontmatter and the remaining
    /// markdown text.</returns>
    /// <exception cref="YamlDotNet.Core.YamlException">Thrown when there is an
    /// exception encountered when parsing the frontmatter.</exception>
    public static (T?, string) GetFrontMatter<T>(this string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var block = document
            .Descendants<YamlFrontMatterBlock>()
            .FirstOrDefault();

        var writer = new StringWriter();
        var renderer = new Markdig.Renderers.Normalize.NormalizeRenderer(writer);
        Pipeline.Setup(renderer);

        if (block == null)
        {
            renderer.Render(document);
            // No frontmatter, so no YAML
            return (default, writer.ToString());
        }

        // Extract the YAML from the frontmatter...
        var yaml =
            block
            // this is not a mistake
            // we have to call .Lines 2x
            .Lines // StringLineGroup[]
            .Lines // StringLine[]
            .OrderByDescending(x => x.Line)
            .Select(x => $"{x}\n")
            .ToList()
            .Select(x => x.Replace("---", string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Aggregate((s, agg) => agg + s);

        // ...and deserialize it
        try
        {

            var frontmatter = YamlDeserializer.Deserialize<T>(yaml);

            // Remove the frontmatter from the markdown
            block.Remove();

            renderer.Render(document);

            // Finally, return our frontmatter alongside the normalised markdown
            return (frontmatter, writer.ToString());
        }
        catch (YamlDotNet.Core.YamlException e)
        {
            throw new YamlDotNet.Core.YamlException($"Exception when parsing markdown: line {e.Start.Line}: {e.Message}", e);
        }
    }
}

/// <summary>
/// Stores information extracted from a markdown document about a diagnostic.
/// </summary>
public class DiagnosticFrontMatter
{
    /// <summary>
    /// The severity of the diagnostic.
    /// </summary>
    public enum Severity
    {
        NotSet,
        [YamlMember(Alias = "error")]
        Error,
        [YamlMember(Alias = "warning")]
        Warning,
        [YamlMember(Alias = "info")]
        Info,
        [YamlMember(Alias = "none")]
        None,
    }

    public enum Source
    {
        [YamlMember(Alias = "compiler")]
        Compiler,
        [YamlMember(Alias = "languageserver")]
        LanguageServer,
    }
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "summary")]
    public string? Summary { get; set; }

    [YamlMember(Alias = "code")]
    public string? Code { get; set; }

    [YamlMember(Alias = "messageTemplate")]
    public string? MessageTemplate { get; set; }

    [YamlMember(Alias = "messageValues")]
    public List<string> MessageValues { get; set; } = [];

    [YamlMember(Alias = "defaultSeverity")]
    public Severity DefaultSeverity { get; set; } = Severity.Error;

    [YamlMember(Alias = "minimumSeverity")]
    public Severity MinimumSeverity { get; set; } = Severity.NotSet;

    [YamlMember(Alias = "published")]
    public string? PublishedVersion { get; set; }

    [YamlMember(Alias = "deprecated")]
    public string? DeprecatedVersion { get; set; }

    [YamlMember(Alias = "deprecation_note")]
    public string? DeprecationNote { get; set; }

    [YamlMember(Alias = "examples")]
    public List<DiagnosticExample> Examples { get; set; } = [];

    [YamlMember(Alias = "generated_in")]
    public Source GeneratedIn { get; set; } = Source.Compiler;

    public bool SkipTestGeneration => this.Examples.Any(e => e.Script == "skip_test_generation");
}

public class DiagnosticExample
{
    [YamlMember(Alias = "script")]
    public string Script { get; set; } = string.Empty;
}

public sealed class GeneratorOptions
{
    public GeneratorOptions(bool generateDescriptors, bool generateTests)
    {
        this.GenerateTests = generateTests;
        this.GenerateDescriptors = generateDescriptors;
    }

    public bool GenerateTests { get; }
    public bool GenerateDescriptors { get; }


}

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
        // Get options
        var options = initContext.AnalyzerConfigOptionsProvider.Select((options, _) =>
        {
            bool IsEnabled(string property) => options.GlobalOptions.TryGetValue(property, out var propertyEnabled) &&
                 IsFeatureEnabled(propertyEnabled);

            return new GeneratorOptions(
                IsEnabled("build_property.YarnSpinnerDiagnostics_GenerateDescriptors"),
                IsEnabled("build_property.YarnSpinnerDiagnostics_GenerateTestData")
            );
        });

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
        initContext.RegisterSourceOutput(namesAndContents.Combine(options), (spc, input) =>
            {
                var (nameAndContent, options) = input;
                var (name, diagnosticInfo, content) = nameAndContent;

                if (diagnosticInfo == null)
                {
                    return;
                }

                if (options != null && options.GenerateDescriptors == false)
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

                spc.AddSource($"DiagnosticDescriptors.{nameAndContent.name}", $@"
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
        initContext.RegisterSourceOutput(diagnosticNames.Combine(options), (spc, input) =>
        {
            var (value, options) = input;



            var allValidDiagnostics = value.Where(v => v.diagnosticInfo != null
                                                       && !string.IsNullOrEmpty(v.diagnosticInfo.Code)
                                                       && !string.IsNullOrEmpty(v.diagnosticInfo.Name));

            if (options != null && options.GenerateTests)
            {
                GenerateTestDataForDiagnostics(spc, allValidDiagnostics.Select(d => d.diagnosticInfo!));
            }

            if (options != null && options.GenerateDescriptors == false)
            {
                return;
            }

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

    public void GenerateTestDataForDiagnostics(SourceProductionContext spc, IEnumerable<DiagnosticFrontMatter> diagnostics)
    {
        string GetSourceForDiagnostic(DiagnosticFrontMatter diagnosticFrontMatter)
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (var example in diagnosticFrontMatter.Examples)
            {
                i++;
                var script = example.Script;

                // YamlDotNet is, for some reason, stripping the --- out of our
                // block strings, so our terrible workaround is to use "-=-" as
                // a placeholder for "---" in the YAML, and swap it out here. I
                // hate it!
                script = script.Replace("-=-", "---");

                sb.AppendLine($@"new object[] {{
                ""{diagnosticFrontMatter.Code}"",
                """"""
{script}"""""",
               }},");
            }
            return sb.ToString();
        }
        var source = @$"
        using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;
namespace YarnSpinner.Tests {{

public partial class GeneratedDiagnosticTests : TestBase {{
    public static IEnumerable<object[]> GetGeneratedTestData()
    {{
        return new List<object[]>() {{
            {string.Join("\n", diagnostics
                .Where(d => d.GeneratedIn == DiagnosticFrontMatter.Source.Compiler && d.SkipTestGeneration == false)
                .Select(d => GetSourceForDiagnostic(d)).Where(s => s.Length > 0))}
        }};
    }}

    public static IEnumerable<object[]> GetCompilerDiagnosticCodes()
    {{
        return new List<object[]>() {{
            {string.Join(",\n", diagnostics
                .Where(d => d.GeneratedIn == DiagnosticFrontMatter.Source.Compiler
                    && d.DeprecatedVersion == null
                    && d.SkipTestGeneration == false)
                .Select(d => $"new object[] {{\"{d.Code}\"}}"))}
        }};
    }}
}}

}}";

        spc.AddSource("YarnSpinner.Diagnostics.GeneratedTestData.cs", source);
    }

    private static bool IsFeatureEnabled(string enabledValue)
    {
        return StringComparer.OrdinalIgnoreCase.Equals("enable", enabledValue)
               || StringComparer.OrdinalIgnoreCase.Equals("enabled", enabledValue)
               || StringComparer.OrdinalIgnoreCase.Equals("true", enabledValue)
               || StringComparer.OrdinalIgnoreCase.Equals("yes", enabledValue)
               || StringComparer.OrdinalIgnoreCase.Equals("y", enabledValue)
               || StringComparer.OrdinalIgnoreCase.Equals("1", enabledValue);
    }
}
