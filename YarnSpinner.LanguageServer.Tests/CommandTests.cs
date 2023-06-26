using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Xunit;
using Xunit.Abstractions;

namespace YarnLanguageServer.Tests;

#pragma warning disable VSTHRD200 // async methods should end in 'Async'

public class CommandTests : LanguageServerTestsBase
{
    public CommandTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task Server_CanListNodesInFile()
    {
        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        var result = await client.ExecuteCommand(new ExecuteCommandParams<Container<NodeInfo>>
        {
            Command = Commands.ListNodes,
            Arguments = new JArray {
                filePath
            }
        });

        result.Should().NotBeNullOrEmpty("because the file contains nodes");

        foreach (var node in result) {
            node.Title.Should().NotBeNullOrEmpty("because all nodes have a title");
            node.Headers.Should().NotBeNullOrEmpty("because all nodes have headers");
            node.BodyStartLine.Should().NotBe(0, "because bodies never start on the first line");
            node.HeaderStartLine.Should().NotBe(node.BodyStartLine, "because bodies never start at the same line as their headers");

            node.Headers.Should().Contain(h => h.Key == "title", "because all nodes have a header named 'title'")
                .Which.Value.Should().Be(node.Title, "because the 'title' header populates the Title property");

            if (node == result.First()) {
                node.HeaderStartLine.Should().Be(0, "because the first node begins on the first line");
            } else {
                node.HeaderStartLine.Should().NotBe(0, "because nodes after the first one begin on later lines");
            }
        }

        result.Should().Contain(n => n.Title == "Node2")
              .Which
              .Headers.Should().Contain(h => h.Key == "tags", "because Node2 has a 'tags' header")
              .Which
              .Value.Should().Be("wow incredible", "because Node2's 'tags' header has this value");

        result.Should().Contain(n => n.Title == "Start")
            .Which
            .Jumps.Should().NotBeNullOrEmpty("because the Start node contains jumps")
            .And
            .Contain(j => j.DestinationTitle == "Node2", "because the Start node has a jump to Node2");
    }

    [Fact]
    public async Task Server_OnAddNodeCommand_ReturnsTextEdit()
    {
        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        NodesChangedParams? nodeInfo;

        nodeInfo = await GetNodesChangedNotificationAsync();

        nodeInfo.Nodes.Should().HaveCount(2, "because the file has two nodes");

        var result = await client.ExecuteCommand(new ExecuteCommandParams<TextDocumentEdit>
        {
            Command = Commands.AddNode,
            Arguments = new JArray {
                filePath,
                new JObject(
                    new JProperty("position", "100,100")
                )
            }
        });

        result.Should().NotBeNull();
        result.Edits.Should().NotBeNullOrEmpty();
        result.TextDocument.Uri.ToString().Should().Be("file://" + filePath);

        ChangeTextInDocument(client, result);

        nodeInfo = await GetNodesChangedNotificationAsync();

        nodeInfo.Nodes.Should().HaveCount(3, "because we added a node");
        nodeInfo.Nodes.Should()
            .Contain(n => n.Title == "Node",
                "because the new node should be called Title")
            .Which.Headers.Should()
            .Contain(h => h.Key == "position" && h.Value == "100,100",
                "because we specified these coordinates when creating the node");
    }

    [Fact]
    public async Task Server_OnRemoveNodeCommand_ReturnsTextEdit()
    {
        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        NodesChangedParams? nodeInfo;

        nodeInfo = await GetNodesChangedNotificationAsync();

        nodeInfo.Nodes.Should().HaveCount(2, "because the file has two nodes");

        var result = await client.ExecuteCommand(new ExecuteCommandParams<TextDocumentEdit>
        {
            Command = Commands.RemoveNode,
            Arguments = new JArray {
                filePath,
                "Start"
            }
        });

        result.Should().NotBeNull();
        result.Edits.Should().NotBeNullOrEmpty();
        result.TextDocument.Uri.ToString().Should().Be("file://" + filePath);

        ChangeTextInDocument(client, result);

        nodeInfo = await GetNodesChangedNotificationAsync();

        nodeInfo.Nodes.Should().HaveCount(1, "because we removed a node");
        nodeInfo.Nodes.Should()
            .Contain(n => n.Title == "Node2",
                "because the only remaining node is Node2");
    }

    [Fact]
    public async Task Server_OnUpdateHeaderCommand_ReturnsTextEditCreatingHeader()
    {
        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        NodesChangedParams? nodeInfo;

        nodeInfo = await GetNodesChangedNotificationAsync();

        nodeInfo
            .Nodes.Should()
            .Contain(n => n.Title == "Start")
            .Which.Headers.Should()
            .NotContain(n => n.Key == "position", 
                "because this node doesn't have this header");


        var result = await client.ExecuteCommand(new ExecuteCommandParams<TextDocumentEdit>
        {
            Command = Commands.UpdateNodeHeader,
            Arguments = new JArray {
                filePath,
                "Start", // this node doesn't have this header, so we're creating it
                "position",
                "100,100"
            }
        });

        result.Should().NotBeNull();
        result.Edits.Should().NotBeNullOrEmpty();
        result.TextDocument.Uri.ToString().Should().Be("file://" + filePath);

        ChangeTextInDocument(client, result);

        nodeInfo = await GetNodesChangedNotificationAsync();

        nodeInfo.Nodes.Should()
            .Contain(n => n.Title == "Start")
            .Which.Headers.Should()
            .Contain(n => n.Key == "position",
                "because we added this new header")
            .Which.Value.Should()
            .Be("100,100",
                "because we specified this value");
    }

    [Fact]
    public async Task Server_OnUpdateHeaderCommand_ReturnsTextEditModifyingHeader()
    {
        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        NodesChangedParams? nodeInfo;

        nodeInfo = await GetNodesChangedNotificationAsync();

        const string headerName = "tags";
        const string headerOldValue = "wow incredible";
        const string headerNewValue = "something different";

        nodeInfo
            .Nodes.Should()
            .Contain(n => n.Title == "Node2")
            .Which.Headers.Should()
            .HaveCount(2)
            .And
            .Contain(n => n.Key == headerName && n.Value == headerOldValue,
                "because this node has this header");

        var result = await client.ExecuteCommand(new ExecuteCommandParams<TextDocumentEdit>
        {
            Command = Commands.UpdateNodeHeader,
            Arguments = new JArray {
                filePath,
                "Node2", // this node already has this header, so we're replacing it
                headerName,
                headerNewValue
            }
        });

        result.Should().NotBeNull();
        result.Edits.Should().NotBeNullOrEmpty();
        result.TextDocument.Uri.ToString().Should().Be("file://" + filePath);

        ChangeTextInDocument(client, result);

        nodeInfo = await GetNodesChangedNotificationAsync();

        nodeInfo.Nodes.Should()
            .Contain(n => n.Title == "Node2")
            .Which.Headers.Should()
            .HaveCount(2, "because we added no new headers")
            .And.Contain(n => n.Key == headerName,
                "because we updated this header")
            .Which.Value.Should()
            .Be(headerNewValue,
                "because we specified this value");
    }
}
