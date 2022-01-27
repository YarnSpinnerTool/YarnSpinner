using Antlr4.Runtime;

namespace YarnLanguageServer;

public record NodeJump
{
    public string DestinationTitle { get; init; }
    internal IToken DestinationToken { get; init; }
}