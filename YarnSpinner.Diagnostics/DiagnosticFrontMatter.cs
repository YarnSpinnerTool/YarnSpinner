using Microsoft.CodeAnalysis;
using YamlDotNet.Serialization;

namespace YarnSpinner.Diagnostics;

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
