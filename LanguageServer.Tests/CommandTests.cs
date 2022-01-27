using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Xunit;
using Xunit.Abstractions;
using YarnLanguageServer;
using System.Linq;

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
        var filePath = Path.Combine(PathToTestData, "Test.yarn");

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
    }
}
