using Antlr4.Runtime;
using Newtonsoft.Json;

namespace YarnLanguageServer;

public record NodeJump
{
    [JsonProperty("destinationTitle")]
    public string DestinationTitle { get; init; }

    internal IToken DestinationToken { get; init; }
}