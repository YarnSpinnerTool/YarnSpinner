using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;
using YarnLanguageServer.Diagnostics;
using YarnLanguageServer.Handlers;

namespace YarnLanguageServer.Tests;

#pragma warning disable VSTHRD200 // async methods should end in 'Async'

public class CodeActionTests : LanguageServerTestsBase
{
    public CodeActionTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task Server_FixesJumpDestinationTypo()
    {
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        Task<NodesChangedParams> getInitialNodesChanged = GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        NodesChangedParams? nodeInfo = await getInitialNodesChanged;
        var workspace = server.Workspace.GetService<Workspace>();
        var diagnostics = workspace!.GetDiagnostics().SelectMany(d => d.Value);

        var jumpToWarning = diagnostics.FirstOrDefault(d => d.Code.HasValue && d.Code.Value.String == nameof(YarnDiagnosticCode.YRNMsngJumpDest));

        jumpToWarning.Should().NotBeNull("Expecting a warning for the missing jump destination");

        var codeActionHandler = new CodeActionHandler(workspace);
        var CodeActionParams = new CodeActionParams
        {
            Context = new CodeActionContext { Diagnostics = Container.From(jumpToWarning!)}, 
            TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath)) 
        };

        var commandOrCodeActions = await codeActionHandler.Handle(CodeActionParams, default);

        var typoFix = commandOrCodeActions.FirstOrDefault(c => c.CodeAction?.Title?.Contains("Rename to 'JumpToTest'") ?? false);
        typoFix.Should().NotBeNull("Expecting a code action to fix the jump destination typo");

        var typoFixEdit = typoFix!.CodeAction!.Edit;
        typoFixEdit.Should().NotBeNull("Expecting the typo fix action to have a workspace edit");

        // Remember how many nodes we had before making the change
        var nodeCount = nodeInfo.Nodes.Count;

        // Remember how many jumps we had to JumpToTests we had before making the change
        var jumpCount = nodeInfo.Nodes.Find(n => n.Title == "JumpToTests")?.Jumps.Count ?? 0;

        // Expect to receive a 'nodes changed' notification
        Task<NodesChangedParams> nodesChangedAfterRemovingNode = GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        ChangeTextInDocuments(client, typoFixEdit!);

        nodeInfo = await nodesChangedAfterRemovingNode;

        nodeInfo.Nodes.Should().HaveCount(nodeCount, "because didn't change any nodes");
        nodeInfo.Nodes.Find(n => n.Title == "JumpToTests")!.Jumps.Should().HaveCount(jumpCount + 1, "because we have a new jump to the JumpToTests node");
    }

    [Fact]
    public async Task Server_CreatesNewNodeBasedOnJumpTarget()
    {
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        Task<NodesChangedParams> getInitialNodesChanged = GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        NodesChangedParams? nodeInfo = await getInitialNodesChanged;
        var workspace = server.Workspace.GetService<Workspace>();
        var diagnostics = workspace!.GetDiagnostics().SelectMany(d => d.Value);

        var jumpToWarning = diagnostics.FirstOrDefault(d => d.Code.HasValue && d.Code.Value.String == nameof(YarnDiagnosticCode.YRNMsngJumpDest));

        jumpToWarning.Should().NotBeNull("Expecting a warning for the missing jump destination");


        var codeActionHandler = new CodeActionHandler(workspace);
        var CodeActionParams = new CodeActionParams
        {
            Context = new CodeActionContext { Diagnostics = Container.From(jumpToWarning!) },
            TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(filePath))
        };
        var commandOrCodeActions = await codeActionHandler.Handle(CodeActionParams, default);

        var generateNodeFix = commandOrCodeActions.FirstOrDefault(c => 
        c.CodeAction?.Title?.Contains("Generate node 'Jump2Tests'") ?? false);

        generateNodeFix.Should().NotBeNull("Expecting a code action to generate a new node");

        var generateNodeFixEdit = generateNodeFix!.CodeAction!.Edit;
        generateNodeFixEdit.Should().NotBeNull("Expecting the typo fix action to have a workspace edit");

        // Remember how many nodes we had before making the change
        var nodeCount = nodeInfo.Nodes.Count;

        // Remember how many jumps we had to JumpToTests we had before making the change
        var jumpCount = nodeInfo.Nodes.Find(n => n.Title == "JumpToTests")?.Jumps.Count ?? 0;

        // Expect to receive a 'nodes changed' notification
        Task<NodesChangedParams> nodesChangedAfterRemovingNode = GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        ChangeTextInDocuments(client, generateNodeFixEdit!);

        nodeInfo = await nodesChangedAfterRemovingNode;

        var jumpToNode = nodeInfo.Nodes.Find(n => n.Title == "JumpToTests");
        var jump2Node = nodeInfo.Nodes.Find(n => n.Title == "Jump2Tests");
        jump2Node.Should().NotBeNull("because we have created a new node");
        nodeInfo.Nodes.Should().HaveCount(nodeCount + 1, "because we have added a single new node");
        jumpToNode!.Jumps.Should().HaveCount(jumpCount, "because we are jumping to the new generated node, not the exisiting JumpToTests node");
        jump2Node!.Jumps.Should().HaveCount(1, "because we have a single jump to the new generated node (the one that the quick action was for)");
    }

}
