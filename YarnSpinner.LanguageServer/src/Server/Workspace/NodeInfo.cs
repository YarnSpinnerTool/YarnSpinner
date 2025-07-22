using Antlr4.Runtime;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Linq;

namespace YarnLanguageServer;

public record NodeInfo
{
    /// <summary>
    /// The title of the node, as stored in the program.
    /// </summary>
    [JsonProperty("uniqueTitle")]
    public string? UniqueTitle { get; set; } = null;

    /// <summary>
    /// The title of the node, as defined in the source code.
    /// </summary>
    /// <remarks>This may be different to the node's <see cref="UniqueTitle"/>.</remarks>
    [JsonProperty("sourceTitle")]
    public string? SourceTitle { get; set; } = null;

    /// <summary>
    /// The subtitle of the node, if present.
    /// </summary>
    /// <remarks>This value is null if a subtitle header is not present.</remarks>
    [JsonProperty("subtitle")]
    public string? Subtitle { get; set; } = null;

    [JsonProperty("nodeGroup")]
    /// <summary>
    /// The name of the node group the node is a member of, if any.
    /// </summary>
    public string? NodeGroupName { get; internal set; }

    /// <summary>
    /// Gets or sets the line on which body content starts.
    /// </summary>
    /// <remarks>
    /// This is the first line of the body that contains content. The line
    /// previous to this will contain the start-of-body delimiter.
    /// </remarks>
    [JsonProperty("bodyStartLine")]
    public int BodyStartLine { get; set; } = 0;

    /// <summary>
    /// Gets or sets the line on which body content stops.
    /// </summary>
    /// <remarks>
    /// This is the final line of the body that contains content. The line after
    /// this will contain the end-of-body delimiter.
    /// </remarks>
    [JsonProperty("bodyEndLine")]
    public int BodyEndLine { get; set; } = 0;

    /// <summary>
    /// Gets or sets the line on which the first header appears.
    /// </summary>
    [JsonProperty("headerStartLine")]
    public int HeaderStartLine { get; set; } = 0;

    [JsonProperty("headers")]
    public List<NodeHeader> Headers { get; init; } = new();

    [JsonProperty("jumps")]
    public List<NodeJump> Jumps { get; init; } = new();

    /// <summary>
    /// Gets or sets the text that can be shown as a short preview of the
    /// contents of this node.
    /// </summary>
    [JsonProperty("previewText")]
    public string PreviewText { get; set; } = string.Empty;

    internal YarnFileData? File { get; init; }

    internal IToken? TitleToken { get; set; }

    internal List<YarnActionReference> FunctionCalls { get; init; } = new();
    internal List<YarnActionReference> CommandCalls { get; init; } = new();
    internal List<IToken> VariableReferences { get; init; } = new();
    internal List<(string name, int lineIndex)> CharacterNames { get; init; } = new();

    [JsonProperty("containsExternalJumps")]
    public bool ContainsExternalJumps => this.Jumps.Any(j => this.File != null && j.DestinationFile != null && j.DestinationFile.Uri != this.File.Uri);

    /// <summary>
    /// Gets the computed complexity for this node.
    /// </summary>
    /// <remarks>
    /// If this node is not part of a node group, this value is -1.
    /// </remarks>
    [JsonProperty("nodeGroupComplexity")]
    public int NodeGroupComplexity { get; internal set; } = -1;

    /// <summary>
    /// Gets a value indicating whether this <see cref="NodeInfo"/> has a valid
    /// title.
    /// </summary>
    /// <remarks>
    /// This value is <see langword="true"/> when <see cref="UniqueTitle"/> is not
    /// <see langword="null"/>, empty or whitespace, and <see
    /// cref="TitleToken"/> is not null.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(UniqueTitle))]
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(TitleToken))]
    public bool HasTitle => !string.IsNullOrWhiteSpace(UniqueTitle) && TitleToken != null;

    internal Range? TitleHeaderRange
    {
        get
        {
            if (this.File == null || this.TitleToken == null)
            {
                return null;
            }

            var start = TextCoordinateConverter.GetPosition(this.File.LineStarts, TitleToken.StartIndex);
            var end = TextCoordinateConverter.GetPosition(this.File.LineStarts, TitleToken.StopIndex + 1);
            return new Range(start, end);
        }
    }
}
