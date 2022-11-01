using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;

using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;

#pragma warning disable CS0162

#pragma warning disable VSTHRD200 // async method names should end with "Async"

namespace YarnLanguageServer.Tests
{
    public class LanguageServerTestsBase : LanguageProtocolTestBase
    {
        public LanguageServerTestsBase(ITestOutputHelper outputHelper) : base(
            new JsonRpcTestOptions()
        //.ConfigureForXUnit(outputHelper)
        )
        {
        }

        TaskCompletionSource<List<Diagnostic>> ReceivedDiagnosticsNotification = new();

        TaskCompletionSource<NodesChangedParams> NodesChangedNotification = new();

        protected static string PathToTestData
        {
            get
            {
                var context = AppContext.BaseDirectory;

                var directoryContainingProject = GetParentDirectoryContainingFile(new DirectoryInfo(context), "*.csproj");

                if (directoryContainingProject != null)
                {
                    return Path.Combine(directoryContainingProject.FullName, "TestData");
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

        protected static void ChangeTextInDocument(ILanguageClient client, string fileURI, Position start, string text)
        {
            ChangeTextInDocument(client, fileURI, start, start, text);
        }

        protected static void ChangeTextInDocument(ILanguageClient client, string fileURI, Position start, Position end, string text)
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

        protected static void ChangeTextInDocument(ILanguageClient client, TextDocumentEdit documentEdit)
        {
            foreach (var edit in documentEdit.Edits) {
                client.DidChangeTextDocument(new DidChangeTextDocumentParams
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier
                    {
                        Uri = documentEdit.TextDocument.Uri,
                    },
                    ContentChanges = new[] {
                        new TextDocumentContentChangeEvent {
                            Range = edit.Range,
                            Text = edit.NewText,
                        },
                    }
                });
            }
        }

        protected void ConfigureClient(LanguageClientOptions options)
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

            void OnNodesChangedNotification(NodesChangedParams nodesChangedParams)
            {
                NodesChangedNotification.TrySetResult(nodesChangedParams);
            }

            options.OnNotification(Commands.DidChangeNodesNotification, (Action<NodesChangedParams>)OnNodesChangedNotification);

            options.WithRootPath(PathToTestData);
        }

        protected static void ConfigureServer(LanguageServerOptions options)
        {
            YarnLanguageServer.ConfigureOptions(options);
        }

        protected async Task<T> GetTaskResultOrTimeoutAsync<T>(TaskCompletionSource<T> task, Action onCompletion, double timeout = 2f) {
            try
            {
                // Timeout.
                var winner = await Task.WhenAny(
                    task.Task,
                    Task.Delay(
                        TimeSpan.FromSeconds(timeout),
                        CancellationToken
                    )
                );
                task.Task.Should().BeSameAs(winner, "because the result should arrive within {0} seconds", timeout);

                return await task.Task;
            }
            finally
            {
                // Get ready for the next call
                onCompletion();
            }
        }

        /// <summary>
        /// Waits for diagnostics to be returned (via <see
        /// cref="ReceivedDiagnosticsNotification"/> being completed), and
        /// returns those diagnostics. If diagnostics are not returned before
        /// the specified timeout elapses, an exception is thrown.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for
        /// diagnostics.</param>
        /// <returns>A collection of <see cref="Diagnostic"/> objects.</returns>
        protected async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(double timeout = 2f)
        {
            return await GetTaskResultOrTimeoutAsync(
                ReceivedDiagnosticsNotification, 
                () => ReceivedDiagnosticsNotification = new(),
                timeout
            );
        }

        protected async Task<NodesChangedParams> GetNodesChangedNotificationAsync(double timeout = 2f) {
            return await GetTaskResultOrTimeoutAsync(
                NodesChangedNotification, 
                () => NodesChangedNotification = new(),
                timeout
            );
        }
    }
}
