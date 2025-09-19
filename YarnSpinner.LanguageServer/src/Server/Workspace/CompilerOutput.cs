using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Generic;

namespace YarnLanguageServer;

public record MetadataOutput
{
    [JsonProperty("id")]
    public string? ID { get; set; }

    [JsonProperty("node")]
    public string? Node { get; set; }

    [JsonProperty("lineNumber")]
    public string? LineNumber { get; set; }

    [JsonProperty("tags")]
    public string[]? Tags { get; set; }
}

public record CompilerOutput
{
    [JsonProperty("data")]
    public string? Data { get; set; }

    [JsonProperty("stringTable")]
    public Dictionary<string, string>? StringTable { get; set; }

    [JsonProperty("metadataTable")]
    public Dictionary<string, MetadataOutput>? MetadataTable { get; set; }

    [JsonProperty("errors")]
    public string[]? Errors { get; set; }
}


public record DocumentStateOutput
{
    [JsonProperty("uri")]
    public string? Uri { get; set; }
    [JsonProperty("nodes")]
    public List<NodeInfo>? Nodes { get; set; } = new();

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DocumentState
    {
        Unknown,
        NotFound,
        InvalidUri,
        ContainsErrors,
        Valid,
    };

    [JsonProperty("state")]
    public DocumentState State { get; set; } = DocumentState.Unknown;

    private DocumentStateOutput()
    {
    }

    public static readonly DocumentStateOutput InvalidUri = new() { State = DocumentState.InvalidUri };

    public DocumentStateOutput(DocumentUri uri)
    {
        this.Uri = uri.ToString();
    }

}
