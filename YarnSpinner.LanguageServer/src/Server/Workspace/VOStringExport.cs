using Newtonsoft.Json;

namespace YarnLanguageServer;

public record VOStringExport
{
    [JsonProperty("file")]
    public byte[] File { get; set; }

    [JsonProperty("errors")]
    public string[] Errors { get; set; } = System.Array.Empty<string>();

    public VOStringExport(byte[] file, string[] errors)
    {
        this.File = file;
        this.Errors = errors;
    }
}
