using MediatR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace YarnLanguageServer;

public record NodesChangedParams : IRequest, IEquatable<NodesChangedParams>
{
    public NodesChangedParams(Uri uri, List<NodeInfo> nodes)
    {
        this.Uri = uri;
        this.Nodes = nodes;
    }

    [JsonProperty("uri")]
    public Uri Uri { get; init; }

    [JsonProperty("nodes")]
    public List<NodeInfo> Nodes { get; init; }
}
