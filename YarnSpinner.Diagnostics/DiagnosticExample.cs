using YamlDotNet.Serialization;

namespace YarnSpinner.Diagnostics;

public class DiagnosticExample
{
    [YamlMember(Alias = "script")]
    public string Script { get; set; } = string.Empty;
}
