using Antlr4.Runtime;
using Newtonsoft.Json;

namespace YarnLanguageServer;

public record NodeHeader
{
    /// <summary>
    /// Gets the name of the header.
    /// </summary>
    [JsonProperty("key")]
    public string Key { get; init; }

    /// <summary>
    /// Gets the value of the header.
    /// </summary>
    [JsonProperty("value")]
    public string Value { get; init; }

    /// <summary>
    /// Gets the token at which the header's key appears.
    /// </summary>
    internal IToken KeyToken { get; init; }

    /// <summary>
    /// Gets the token at which the header's value appears.
    /// </summary>
    internal IToken ValueToken { get; init; }
}
