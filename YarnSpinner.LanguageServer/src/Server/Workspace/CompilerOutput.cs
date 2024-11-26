using System.Collections.Generic;
using Newtonsoft.Json;

namespace YarnLanguageServer;

public record MetadataOutput
{
    [JsonProperty("id")]
    public string ID { get; set; }

    [JsonProperty("node")]
    public string Node { get; set; }

    [JsonProperty("lineNumber")]
    public string LineNumber { get; set; }

    [JsonProperty("tags")]
    public string[] Tags { get; set; }
}
;

public record CompilerOutput
{
    [JsonProperty("data")]
    public string Data { get; set; }

    [JsonProperty("stringTable")]
    public Dictionary<string, string> StringTable { get; set; }

    [JsonProperty("metadataTable")]
    public Dictionary<string, MetadataOutput> MetadataTable { get; set; }

    [JsonProperty("errors")]
    public string[] Errors { get; set; }
}
