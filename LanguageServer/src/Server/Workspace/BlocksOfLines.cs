using System.Collections.Generic;
using Newtonsoft.Json;

namespace YarnLanguageServer;

public record BlocksOfLines
{
    [JsonProperty("lineBlocks")]
    public byte[] LineBlocks { get; set; }

    [JsonProperty("errors")]
    public string[] Errors { get; set; }
}
