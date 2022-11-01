using System.Collections.Generic;
using Newtonsoft.Json;

namespace YarnLanguageServer;

public record VOStringExport
{
    [JsonProperty("file")]
    public byte[] File { get; set; }

    [JsonProperty("errors")]
    public string[] Errors { get; set; }
}
