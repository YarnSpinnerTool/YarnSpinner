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
    public class LanguageServerTests : LanguageServerTestsBase
    {
        public LanguageServerTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task Server_CanConnect()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

            client.ServerSettings.Should().NotBeNull();
            client.ClientSettings.Should().NotBeNull();
        }

        [Fact]
        public async Task Server_OnEnteringACommand_ShouldReceiveCompletions()
        {
            // Set up the server
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(PathToTestData, "Test.yarn");

            // Start typing a command
            ChangeTextInDocument(client, filePath, new Position(8, 0), "<<");

            // Request completions at the end of the '<<'
            var completions = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new()
                {
                    Uri = filePath
                },
                Position = new Position(8, 2),
            });

            using (new AssertionScope())
            {
                completions.Should().Contain(c => c.Label == "set" && c.Kind == CompletionItemKind.Keyword,
                    "because 'set' is a keyword in Yarn");

                completions.Should().Contain(c => c.Label == "declare" && c.Kind == CompletionItemKind.Keyword,
                    "because 'declare' is part of the Yarn syntax ");

                completions.Should().Contain(c => c.Label == "jump" && c.Kind == CompletionItemKind.Keyword,
                    "because 'jump' is part of the Yarn syntax ");

                completions.Should().Contain(c => c.Label == "wait" && c.Kind == CompletionItemKind.Function,
                    "because 'wait' is a built-in Yarn command");

                completions.Should().Contain(c => c.Label == "stop" && c.Kind == CompletionItemKind.Function,
                     "because 'stop' is a built-in Yarn command");

                completions.Should().NotContain(c => c.Label == "Start",
                    "because 'Start' is a node name, and not a command");
            }
        }

        [Fact]
        public async Task Server_OnOpeningDocument_SendsNodesChangedNotification()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

            var nodeInfo = await GetNodesChangedNotificationAsync();

            nodeInfo.Should().NotBeNull("because this notification always carries a parameters object");
            nodeInfo.Nodes.Should().NotBeNullOrEmpty("because this notification always contains a list of node infos, even if it's empty");

            nodeInfo.Nodes.Should().Contain(ni => ni.Title == "Start", "because this file contains a node with this title");

            nodeInfo.Nodes.Should()
                .Contain(
                    ni => ni.Title == "Node2", 
                    "because this file contains a node with this title")
                .Which.Headers.Should()
                .Contain(
                    h => h.Key == "tags" && h.Value == "wow incredible", 
                    "because this node contains a tags header"
                );
        }

        [Fact]
        public async Task Server_OnChangingDocument_SendsNodesChangedNotification()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            
            var filePath = Path.Combine(PathToTestData, "Test.yarn");

            NodesChangedParams? nodeInfo;
            
            nodeInfo = await GetNodesChangedNotificationAsync();

            nodeInfo.Uri.ToString().Should().Be("file://" + filePath, "because this is the URI of the file we opened");

            nodeInfo.Nodes.Should().HaveCount(2, "because there are two nodes in the file before we make changes");

            ChangeTextInDocument(client, filePath, new Position(19, 0), "title: Node3\n---\n===\n");

            nodeInfo = await GetNodesChangedNotificationAsync();

            nodeInfo.Nodes.Should().HaveCount(3, "because we added a new node");
            nodeInfo.Nodes.Should().Contain(n => n.Title == "Node3", "because the new node we added has this title");
        }

        [Fact]
        public async Task Server_OnInvalidChanges_ProducesSyntaxErrors()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

            {
                var errors = (await GetDiagnosticsAsync()).Where(d => d.Severity == DiagnosticSeverity.Error);

                errors.Should().BeNullOrEmpty("because the original document contains no syntax errors");
            }

            // Introduce an error
            var filePath = Path.Combine(PathToTestData, "Test.yarn");
            ChangeTextInDocument(client, filePath, new Position(8, 0), "<<set");

            {
                var errors = (await GetDiagnosticsAsync()).Where(d => d.Severity == DiagnosticSeverity.Error);

                errors.Should().NotBeNullOrEmpty("because we have introduced a syntax error");
            }

            // Remove the error
            ChangeTextInDocument(client, filePath, new Position(8, 0), new Position(8, 5), "");

            {
                var errors = (await GetDiagnosticsAsync()).Where(d => d.Severity == DiagnosticSeverity.Error);

                errors.Should().BeNullOrEmpty("because the syntax error was removed");
            }
        }

        [Fact]
        public async Task Server_OnJumpCommand_ShouldReceiveNodeNameCompletions()
        {
            // Set up the server
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(PathToTestData, "Test.yarn");
            CompletionList? completions;

            // The line in Test.yarn we're inserting the new jump command on.
            const int Line = 11;

            // Start typing the jump command: start with the '<<'
            ChangeTextInDocument(client, filePath, new Position(Line, 0), "<<");

            // Request completions at the end of the '<<'
            completions = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new()
                {
                    Uri = filePath
                },
                Position = new Position(Line, 2),
            });

            completions.Should().Contain(c => c.Label == "jump" && c.Kind == CompletionItemKind.Keyword,
                "because we have not yet entered the word 'jump'.");

            // Type in the 'jump'.
            ChangeTextInDocument(client, filePath, new Position(Line, 2), "jump ");

            // Request completions at the end of '<<jump '.
            completions = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new()
                {
                    Uri = filePath
                },
                Position = new Position(Line, 7),
            });

            using (new AssertionScope())
            {
                completions.Should().NotContain(c => c.Label == "jump",
                    "because we have finished typing in 'jump'");

                completions.Should().Contain(c => c.Label == "Start",
                    "because 'Start' is a node we could jump to");

                completions.Should().NotContain(c => c.Label == "Node2",
                    "because while 'Node2' is a node we could jump to, we're currently in a syntax error");
            }
        }
    }
}
