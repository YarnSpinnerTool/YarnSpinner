using System.Collections.Generic;
using Antlr4.Runtime;

using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer;

public record NodeInfo
{
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

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

    internal YarnFileData File { get; init; }

    internal IToken TitleToken { get; set; }

    internal List<YarnActionReference> FunctionCalls { get; init; } = new();
    internal List<YarnActionReference> CommandCalls { get; init; } = new();
    internal List<IToken> VariableReferences { get; init; } = new();
    internal List<(string Name, int LineIndex)> CharacterNames { get; init; } = new();

    /// <summary>
    /// Gets a value indicating whether this <see cref="NodeInfo"/> has a valid
    /// title.
    /// </summary>
    /// <remarks>
    /// This value is <see langword="true"/> when <see cref="Title"/> is not
    /// <see langword="null"/>, empty or whitespace, and <see
    /// cref="TitleToken"/> is not null.
    /// </remarks>
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title) && TitleToken != null;

    internal Range TitleHeaderRange {
        get {
            var start = TextCoordinateConverter.GetPosition(this.File.LineStarts, TitleToken.StartIndex);
            var end = TextCoordinateConverter.GetPosition(this.File.LineStarts, TitleToken.StopIndex);
            return new Range(start, end);
        }
    }

    //     position: { x: number, y: number } = { x: 0, y: 0 }
    //     destinations: string[] = []
    //     tags: string[] = []
    //     line: number = 0
    //     bodyLine: number = 0

    //     start: Position = { line: 0, character: 0 };
    //     end: Position = { line: 0, character: 0 };
    // }

}
