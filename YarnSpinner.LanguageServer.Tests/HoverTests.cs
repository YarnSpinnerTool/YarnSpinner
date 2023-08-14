using System.Threading.Tasks;
using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;
using System.Linq;

using System.IO;
using FluentAssertions.Execution;

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
    }
}
