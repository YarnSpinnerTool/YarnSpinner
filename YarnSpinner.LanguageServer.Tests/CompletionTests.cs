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
    public class CompletionTests : LanguageServerTestsBase
    {
        public CompletionTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task Server_OnCompletingStartOfCommand_ReturnsValidCompletions()
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

            var startOfCommand = new Position
            {
                Character = 2,
                Line = 22
            };

            // When
            var completionResults = await client.RequestCompletion(new CompletionParams
            {
                Position = startOfCommand,
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
            });

            // Then
            completionResults.Items.Should().NotBeEmpty();

            completionResults.Should().NotContain(i => i.Kind == CompletionItemKind.Snippet, "we are in the middle of a command and inserting a snippet is not appropriate");

            completionResults.Should().AllSatisfy(item =>
            {
                item.TextEdit!.TextEdit!.Range.Start.Should().BeEquivalentTo(startOfCommand, "the completion item's range should be the end of the << character");
                item.TextEdit.TextEdit.Range.End.Should().BeEquivalentTo(startOfCommand, "the completion item's range should be the end of the << character");
            });
        }

        [Fact]
        public async Task Server_OnCompletingPartialCommand_ReturnsValidCompletionRange()
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

            var startOfCommand = new Position
            {
                Character = 2,
                Line = 22
            };
            var middleOfCommand = startOfCommand with {
                Character = 4
            };

            var expectedLineText = "<<static_command_no_docs>>";
            var lines = File.ReadAllLines(filePath).ElementAt(middleOfCommand.Line).Should().Be(expectedLineText);

            // When
            var completionResults = await client.RequestCompletion(new CompletionParams
            {
                Position = middleOfCommand,
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
            });


            // Then
            completionResults.Should().Contain(item => item.Kind == CompletionItemKind.Function, "the completion list should contain functions");

            completionResults.Should().AllSatisfy(result =>
            {
                result.TextEdit!.TextEdit!.Range.Start.Should().BeEquivalentTo(startOfCommand, "the completion item's edit should start at the end of the << token");
                result.TextEdit.TextEdit.Range.End.Should().BeEquivalentTo(middleOfCommand, "the completion item's edit should end at the request position");
            });        
        }

        [Fact]
        public async Task Server_OnCompletingJumpCommand_ReturnsNodeNames()
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

            var endOfJumpKeyword = new Position
            {
                Character = 7,
                Line = 10
            };
            
            var expectedLineText = "<<jump Node2>>";
            var lines = File.ReadAllLines(filePath).ElementAt(endOfJumpKeyword.Line).Should().Be(expectedLineText);

            // When
            var completionResults = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
                Position = endOfJumpKeyword
            });

            // Then
            completionResults.Should().AllSatisfy(item =>
            {
                item.Kind.Should().Be(CompletionItemKind.Method, "node names are expected when completing a jump command");
                item.TextEdit!.TextEdit!.Range.Start.Should().BeEquivalentTo(endOfJumpKeyword);
                item.TextEdit.TextEdit.Range.End.Should().BeEquivalentTo(endOfJumpKeyword);
            });
        }

        [Fact]
        public async Task Server_OnCompletingPartialJumpCommand_ReturnsNodeNames()
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

            var endOfJumpKeyword = new Position
            {
                Character = 7,
                Line = 10
            };
            var middleOfNodeName = endOfJumpKeyword with
            {
                Character = 9,
            };
            
            var expectedLineText = "<<jump Node2>>";
            var lines = File.ReadAllLines(filePath).ElementAt(endOfJumpKeyword.Line).Should().Be(expectedLineText);

            // When
            var completionResults = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
                Position = middleOfNodeName
            });

            // Then
            completionResults.Should().AllSatisfy(item =>
            {
                item.Kind.Should().Be(CompletionItemKind.Method, "node names are expected when completing a jump command");
                item.TextEdit!.TextEdit!.Range.Start.Should().BeEquivalentTo(endOfJumpKeyword);
                item.TextEdit.TextEdit.Range.End.Should().BeEquivalentTo(middleOfNodeName);
            });
        }

    }
}
