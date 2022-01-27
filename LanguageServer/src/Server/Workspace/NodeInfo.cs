using System.Collections.Generic;
using Antlr4.Runtime;

using Newtonsoft.Json;

namespace YarnLanguageServer;

public record NodeInfo
{
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("bodyStartLine")]
    public int BodyStartLine { get; set; } = 0;

    [JsonProperty("headerStartLine")]
    public int HeaderStartLine { get; set; } = 0;

    [JsonProperty("headers")]
    public List<NodeHeader> Headers { get; init; } = new();

    [JsonProperty("jumps")]
    public List<NodeJump> Jumps { get; init; } = new();

    internal YarnFileData File { get; init; }

    internal IToken TitleToken { get; set; }

    internal List<YarnFunctionCall> FunctionCalls { get; init; } = new();
    internal List<YarnFunctionCall> CommandCalls { get; init; } = new();
    internal List<YarnVariableDeclaration> VariableDeclarations { get; init; } = new();
    internal List<IToken> VariableReferences { get; init; } = new();


    //     position: { x: number, y: number } = { x: 0, y: 0 }
    //     destinations: string[] = []
    //     tags: string[] = []
    //     line: number = 0
    //     bodyLine: number = 0

    //     start: Position = { line: 0, character: 0 };
    //     end: Position = { line: 0, character: 0 };
    // }

}
