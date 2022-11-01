using System;
using System.Collections.Generic;
using MediatR;

namespace YarnLanguageServer;

public record NodesChangedParams : IRequest, IEquatable<NodesChangedParams>
{
    [Newtonsoft.Json.JsonProperty("uri")]
    public Uri Uri { get; init; } = null;

    [Newtonsoft.Json.JsonProperty("nodes")]
    public List<NodeInfo> Nodes { get; init; } = new();
}
