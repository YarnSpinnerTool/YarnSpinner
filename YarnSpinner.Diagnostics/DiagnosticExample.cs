using YamlDotNet.Serialization;

namespace YarnSpinner.Diagnostics;

public class DiagnosticExample
{
    /// <summary>
    /// The script that will produce this diagnostic.
    /// </summary>
    [YamlMember(Alias = "script")]
    public string Script { get; set; } = string.Empty;

    /// <summary>
    /// The language version that this example should be compiled under. If this is not specified, the latest version supported by the compiler is used.
    /// </summary>
    /// <remarks>
    /// Some diagnostics are only applied on specific versions of Yarn Spinner. Using this property allows you to restrict this example case to a specific version. 
    /// </remarks>
    [YamlMember(Alias = "project_version")]
    public int LanguageVersion { get; set; } = -1;


}
