using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0162

#pragma warning disable VSTHRD200 // async method names should end with "Async"

namespace YarnLanguageServer.Tests
{
    public class CompletionTests : LanguageServerTestsBase
    {
        public CompletionTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        private static int GetNodeBodyLineNumber(Workspace workspace, string nodeName)
        {
            var projectContainingNode = workspace.Projects.Single(p => p.Nodes.Any(n => n.SourceTitle == nodeName));
            var node = projectContainingNode.Nodes.Single(n => n.SourceTitle == nodeName);
            return node.BodyStartLine;
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

            completionResults.Should().NotBeEmpty();
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
            var middleOfCommand = startOfCommand with
            {
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

            completionResults.Should().NotBeEmpty();
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
            completionResults.Should().NotBeEmpty();
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
            completionResults.Should().NotBeEmpty();
            completionResults.Should().AllSatisfy(item =>
            {
                item.Kind.Should().Be(CompletionItemKind.Method, "node names are expected when completing a jump command");
                item.TextEdit!.TextEdit!.Range.Start.Should().BeEquivalentTo(endOfJumpKeyword);
                item.TextEdit.TextEdit.Range.End.Should().BeEquivalentTo(middleOfNodeName);
            });
        }

        [Fact]
        public async Task Server_OnCompletionRequestedInSetStatement_OffersVariableNamesForAssignment()
        {
            // Given
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");
            var workspace = server.Workspace.GetService<Workspace>()!;
            var project = workspace.Projects.Single(p => p.Uri!.Path.Contains("Project1"));
            var insertionLineNumber = GetNodeBodyLineNumber(workspace, "CodeCompletionTests");

            ChangeTextInDocument(client, filePath, new Position(insertionLineNumber, 0), "<<set ");

            // When
            var completionResults = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
                Position = new Position(insertionLineNumber, "<<set ".Length)
            });

            var storedVariables = project.Variables.Where(v => v.IsInlineExpansion == false);
            var smartVariables = project.Variables.Where(v => v.IsInlineExpansion == true);


            // Then
            storedVariables.Should().AllSatisfy(v => completionResults.Should().Contain(res => res.Label == v.Name));
            smartVariables.Should().AllSatisfy(v => completionResults.Should().NotContain(res => res.Label == v.Name));
        }


        [Fact]
        public async Task Server_OnCompletionRequestedInSetStatement_OffersIdentifiersForValues()
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");
            var workspace = server.Workspace.GetService<Workspace>()!;
            var project = workspace.Projects.Single(p => p.Uri!.Path.Contains("Project1"));
            var insertionLineNumber = GetNodeBodyLineNumber(workspace, "CodeCompletionTests");

            ChangeTextInDocument(client, filePath, new Position(insertionLineNumber, 0), "<<set $x = ");

            // When
            var completionResults = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
                Position = new Position(insertionLineNumber, "<<set $x = ".Length)
            });

            var allFunctionsAndVariables = Enumerable.Concat(project.Variables, project.Functions.Select(a => a.Declaration));
            var allEnumCaseNames = project.Enums.SelectMany(e => e.EnumCases.Select(c => $"{e.Name}.{c.Key}"));

            allFunctionsAndVariables.Should().NotBeEmpty();
            allFunctionsAndVariables.Should().AllSatisfy(decl => decl.Should().NotBeNull());
            allEnumCaseNames.Should().NotBeEmpty();

            // Then
            // All functions and variables should be in the list of completions
            allFunctionsAndVariables.Should().AllSatisfy(decl => completionResults.Should().Contain(res => res.Label == decl!.Name));
            // All enum cases should be in the list of completions
            allEnumCaseNames.Should().AllSatisfy(caseName => completionResults.Should().Contain(res => res.Label == caseName));
        }

        [InlineData(["<<if "])]
        [InlineData(["<<elseif "])]
        [InlineData(["<<myCoolCommand {"])]
        [Theory]
        public async Task Server_OnCompletionRequestedInStatement_OffersIdentifiers(string expression)
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");
            var workspace = server.Workspace.GetService<Workspace>()!;
            var project = workspace.Projects.Single(p => p.Uri!.Path.Contains("Project1"));
            var insertionLineNumber = GetNodeBodyLineNumber(workspace, "CodeCompletionTests");

            ChangeTextInDocument(client, filePath, new Position(insertionLineNumber, 0), expression);

            // When
            var completionResults = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = filePath },
                Position = new Position(insertionLineNumber, expression.Length)
            });

            // Then
            completionResults.Should().NotBeEmpty();

            var allFunctionsAndVariables = Enumerable.Concat(project.Variables, project.Functions.Select(a => a.Declaration));
            var allEnumCaseNames = project.Enums.SelectMany(e => e.EnumCases.Select(c => $"{e.Name}.{c.Key}"));

            allFunctionsAndVariables.Should().NotBeEmpty();
            allFunctionsAndVariables.Should().AllSatisfy(decl => decl.Should().NotBeNull());
            allEnumCaseNames.Should().NotBeEmpty();

            // All functions and variables should be in the list of completions
            allFunctionsAndVariables.Should().AllSatisfy(decl => completionResults.Should().Contain(res => res.Label == decl!.Name));

            allEnumCaseNames.Should().AllSatisfy(caseName => completionResults.Should().Contain(res => res.Label == caseName));
        }

    }
}
