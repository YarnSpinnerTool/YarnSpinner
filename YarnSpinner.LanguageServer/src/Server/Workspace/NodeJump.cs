using Antlr4.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace YarnLanguageServer;

public record NodeJump
{
    public NodeJump(string destinationTitle, IToken destinationToken, JumpType jumpType)
    {
        this.DestinationTitle = destinationTitle;
        this.DestinationToken = destinationToken;
        this.Type = jumpType;
    }

    [JsonProperty("destinationTitle")]
    public string DestinationTitle { get; init; }

    internal IToken DestinationToken { get; init; }

    [JsonProperty("type")]
    public JumpType Type { get; init; }

    /// <summary>
    /// A type of jump from one node to another.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum JumpType
    {
        /// <summary>
        /// The jump is a normal jump.
        /// </summary>
        Jump,

        /// <summary>
        /// The jump is a detour.
        /// </summary>
        Detour,
    }
}
