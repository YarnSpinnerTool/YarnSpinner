using System;
using System.Threading.Tasks;
using FluentAssertions;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Xunit;
using Xunit.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using System.Linq;
using System.Collections.Generic;

using System.IO;

using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using FluentAssertions.Execution;

#pragma warning disable CS0162

#pragma warning disable VSTHRD200 // async method names should end with "Async"

namespace YarnLanguageServer.Tests
{
    public class YarnLanguageServerTests : LanguageProtocolTestBase
    {
        public YarnLanguageServerTests(ITestOutputHelper outputHelper) : base(
            new JsonRpcTestOptions()
        //.ConfigureForXUnit(outputHelper)
        )
        {
        }

        TaskCompletionSource<List<Diagnostic>> ReceivedDiagnosticsNotification = new();

        /// <summary>
        /// Waits for diagnostics to be returned (via <see
        /// cref="ReceivedDiagnosticsNotification"/> being completed), and
        /// returns those diagnostics. If diagnostics are not returned before
        /// the specified timeout elapses, an exception is thrown.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for
        /// diagnostics.</param>
        /// <returns>A collection of <see cref="Diagnostic"/> objects.</returns>
        private async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(double timeout = 2f)
        {
            try
            {
                // Timeout.
                var winner = await Task.WhenAny(
                    ReceivedDiagnosticsNotification.Task,
                    Task.Delay(
                        TimeSpan.FromSeconds(timeout),
                        CancellationToken
                    )
                );
                ReceivedDiagnosticsNotification.Task.Should().BeSameAs(winner);

                Assert.Same(ReceivedDiagnosticsNotification.Task, winner);

                return await ReceivedDiagnosticsNotification.Task;
            }
            finally
            {
                // Get ready for the next call
                ReceivedDiagnosticsNotification = new();
            }
        }

        [Fact]
        public async Task Server_CanConnect()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

            client.ServerSettings.Should().NotBeNull();
            client.ClientSettings.Should().NotBeNull();
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
        public async Task Server_OnJumpCommand_ShouldReceiveNodeNameCompletions()
        {
            // Set up the server
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(PathToTestData, "Test.yarn");
            CompletionList? completions;

            // Start typing the jump command: start with the '<<'
            ChangeTextInDocument(client, filePath, new Position(8, 0), "<<");

            // Request completions at the end of the '<<'
            completions = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new()
                {
                    Uri = filePath
                },
                Position = new Position(8, 2),
            });

            completions.Should().Contain(c => c.Label == "jump" && c.Kind == CompletionItemKind.Keyword,
                "because we have not yet entered the word 'jump'.");

            // Type in the 'jump'.
            ChangeTextInDocument(client, filePath, new Position(8, 2), "jump ");

            // Request completions at the end of '<<jump '.
            completions = await client.RequestCompletion(new CompletionParams
            {
                TextDocument = new()
                {
                    Uri = filePath
                },
                Position = new Position(8, 7),
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

        private static void ChangeTextInDocument(ILanguageClient client, string fileURI, Position start, string text)
        {
            ChangeTextInDocument(client, fileURI, start, start, text);
        }

        private static void ChangeTextInDocument(ILanguageClient client, string fileURI, Position start, Position end, string text)
        {
            client.DidChangeTextDocument(new DidChangeTextDocumentParams
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Uri = fileURI,
                },
                ContentChanges = new[] {
                    new TextDocumentContentChangeEvent {
                        Range = new Range {
                            Start = start,
                            End = end
                        },
                        Text = text,
                    },
                }
            });
        }

        // [Fact]
        // public async Task Server_Should_Stay_Alive_When_Requests_Throw_An_Exception()
        // {
        //     var (client, _) = await Initialize(ConfigureClient, ConfigureServer);

        //     var result = await client.SendRequest("keepalive").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();

        //     Func<Task> a = () => client.SendRequest("throw").ReturningVoid(CancellationToken);
        //     await a.Should().ThrowAsync<InternalErrorException>();

        //     result = await client.SendRequest("keepalive").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();
        // }

        // [Fact]
        // public async Task Client_Should_Stay_Alive_When_Requests_Throw_An_Exception()
        // {
        //     var (_, server) = await Initialize(ConfigureClient, ConfigureServer);

        //     var result = await server.SendRequest("keepalive").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();

        //     Func<Task> a = () => server.SendRequest("throw").ReturningVoid(CancellationToken);
        //     await a.Should().ThrowAsync<InternalErrorException>();

        //     result = await server.SendRequest("keepalive").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();
        // }

        // [Fact]
        // public async Task Server_Should_Support_Links()
        // {
        //     var (client, _) = await Initialize(ConfigureClient, ConfigureServer);

        //     var result = await client.SendRequest("ka").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();

        //     Func<Task> a = () => client.SendRequest("t").ReturningVoid(CancellationToken);
        //     await a.Should().ThrowAsync<InternalErrorException>();

        //     result = await client.SendRequest("ka").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();
        // }

        // [Fact]
        // public async Task Client_Should_Support_Links()
        // {
        //     var (_, server) = await Initialize(ConfigureClient, ConfigureServer);

        //     var result = await server.SendRequest("ka").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();

        //     Func<Task> a = () => server.SendRequest("t").ReturningVoid(CancellationToken);
        //     await a.Should().ThrowAsync<InternalErrorException>();

        //     result = await server.SendRequest("ka").Returning<bool>(CancellationToken);
        //     result.Should().BeTrue();
        // }

        private void ConfigureClient(LanguageClientOptions options)
        {
            options.OnRequest("keepalive", ct => Task.FromResult(true));
            options.WithLink("keepalive", "ka");
            options.WithLink("throw", "t");
            options.OnRequest(
                "throw", async ct =>
                {
                    throw new NotSupportedException();
                    return Task.CompletedTask;
                }
            );

            options.OnPublishDiagnostics((diagnosticsParams) =>
            {
                var diagnostics = diagnosticsParams.Diagnostics.ToList();
                ReceivedDiagnosticsNotification.TrySetResult(diagnostics);
            });

            options.WithRootPath(PathToTestData);
        }

        private static string PathToTestData
        {
            get
            {
                var context = AppContext.BaseDirectory;

                var directoryContainingProject = GetParentDirectoryContainingFile(new DirectoryInfo(context), "*.csproj");

                if (directoryContainingProject != null)
                {
                    return System.IO.Path.Combine(directoryContainingProject.FullName, "TestData");
                }
                else
                {
                    throw new InvalidOperationException("Failed to find path containing .csproj!");
                }

                static DirectoryInfo? GetParentDirectoryContainingFile(DirectoryInfo directory, string filePattern)
                {
                    var current = directory;
                    do
                    {
                        if (current.EnumerateFiles(filePattern).Any())
                        {
                            return current;
                        }
                        current = current.Parent;
                    } while (current != null);

                    return null;
                }
            }
        }

        private void ConfigureServer(LanguageServerOptions options)
        {
            // options.OnRequest("keepalive", ct => Task.FromResult(true));
            // options.WithLink("keepalive", "ka");
            // options.WithLink("throw", "t");
            // options.OnRequest(
            //     "throw", async ct => {
            //         throw new NotSupportedException();
            //         return Task.CompletedTask;
            //     }
            // );

            YarnLanguageServer.ConfigureOptions(options);
        }
    }
}
