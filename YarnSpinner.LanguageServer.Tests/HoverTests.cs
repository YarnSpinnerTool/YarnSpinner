using System.Threading.Tasks;
using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;
using System.Linq;

using System.IO;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol;

#pragma warning disable CS0162

#pragma warning disable VSTHRD200 // async method names should end with "Async"

namespace YarnLanguageServer.Tests
{
    public class HoverTests : LanguageServerTestsBase
    {
        public HoverTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }


        [Fact]
        public async Task Server_OnHoverVariable_ShouldReceiveHoverInfo()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

            // Hover at the start of a line; no hover information is NOT expected to
            // be returned
            var invalidHoverPosiition = new Position
            {
                Line = 16,
                Character = 14,
            };

            // var expectedInvalidHoverResult = await client.RequestHover(new HoverParams
            // {
            //     Position = invalidHoverPosiition,
            //     TextDocument = new TextDocumentIdentifier { Uri = filePath },
            // });

            // expectedInvalidHoverResult.Should().BeNull();

            // Hover in the middle of the variable '$myVar'; hover information
            // is expected to be returned
            var validHoverPosition = new Position
            {
                Line = 18,
                Character = 14,
            };

            var expectedValidHoverResult = await client.RequestHover(new HoverParams
            {
                Position = validHoverPosition,
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
            });

            expectedValidHoverResult.Should().NotBeNull();
            expectedValidHoverResult?.Contents.Should().NotBeNull();
        }


        [Fact]
        public async Task Server_OnHoverCommands_GivesInfo()
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");
        
            // When
            var hoverResult = await client.RequestHover(new HoverParams
            {
                Position = new Position { Line = 23, Character = 10 },
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
            });


            // Then
            
            hoverResult?.Contents.MarkedStrings?.ElementAt(0).Language.Should().Be("text");
            hoverResult?.Contents.MarkedStrings?.ElementAt(0).Value.Should().Be("instance_command_no_params");

            hoverResult?.Contents.MarkedStrings?.ElementAt(1).Language.Should().Be("csharp");
            hoverResult?.Contents.MarkedStrings?.ElementAt(1).Value.Should().Contain("InstanceCommandNoParams()");

            hoverResult?.Contents.MarkedStrings?.ElementAt(2).Language.Should().Be("text");
            hoverResult?.Contents.MarkedStrings?.ElementAt(2).Value.Should().Contain("This is an example of an instance command with no parameters.");
        }

        [Fact]
        public async Task Server_OnJumpToDefinition_GivesExpectedRange()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

            var definitionsResult = await client.RequestDefinition(new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
                Position = new Position { Line = 10, Character = 9 },
            });

            var workspace = server.Services.GetService<Workspace>()!;
            var project = workspace.GetProjectsForUri(filePath).Single();
            var node = project.Nodes.Should()
                .ContainSingle(n => n.Title == "Node2", "the project should contain a single node called Node2")
                .Subject;

            var definition = definitionsResult.Should().ContainSingle().Subject;
            definition.IsLocation.Should().BeTrue("the definition request should match exactly one node");

            DocumentUri expectedUri = DocumentUri.FromFileSystemPath(filePath);
            var location = definition.Location!;
            location.Uri.Should().Be(expectedUri, "the location should be aiming at the file that the node is contained in");

            var file = project.Files.First(f => DocumentUri.From(f.Uri).Equals(expectedUri));
            var text = file.GetRange(location.Range);
            text.Should().Be("Node2", "the range of the location should match the node's title exactly");
        }
    }
}
