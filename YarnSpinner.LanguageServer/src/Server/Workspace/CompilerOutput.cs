using System.Collections.Generic;
using Newtonsoft.Json;

namespace YarnLanguageServer;

public record CompilerOutput
{
    [JsonProperty("data")]
    public byte[] Data { get; set; }

    [JsonProperty("stringTable")]
    public Dictionary<string, string> StringTable { get; set; }

    [JsonProperty("errors")]
    public string[] Errors { get; set; }
}
