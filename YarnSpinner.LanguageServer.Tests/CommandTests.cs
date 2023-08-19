using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Antlr4.Runtime.Tree;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;

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

        nodeInfo = await GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        nodeInfo.Nodes.Should().HaveCount(4, "because the file has four nodes");

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

        nodeInfo = await GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        nodeInfo.Nodes.Should().HaveCount(5, "because we added a node");
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
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        Task<NodesChangedParams> getInitialNodesChanged = GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

        NodesChangedParams? nodeInfo = await getInitialNodesChanged;

        nodeInfo.Nodes.Should().HaveCount(4, "because the file has four nodes");

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

        nodeInfo = await GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        nodeInfo.Nodes.Should().HaveCount(3, "because we removed a node");
    }

    [Fact]
    public async Task Server_OnUpdateHeaderCommand_ReturnsTextEditCreatingHeader()
    {
        var getInitialNodesChanged = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");
        Task<NodesChangedParams> task = GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(getInitialNodesChanged));

        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

        NodesChangedParams? nodeInfo = await task;

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
                getInitialNodesChanged,
                "Start", // this node doesn't have this header, so we're creating it
                "position",
                "100,100"
            }
        });

        result.Should().NotBeNull();
        result.Edits.Should().NotBeNullOrEmpty();
        result.TextDocument.Uri.ToString().Should().Be("file://" + getInitialNodesChanged);

        ChangeTextInDocument(client, result);

        nodeInfo = await GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(getInitialNodesChanged));

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
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");
        Task<NodesChangedParams> getInitialNodesChanged = GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

        // Set up the server
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

        NodesChangedParams? nodeInfo = await getInitialNodesChanged;

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

        nodeInfo = await GetNodesChangedNotificationAsync(n => n.Uri.ToString().Contains(filePath));

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

    [Fact]
    public async Task Server_OnGettingVoiceoverSpreadsheet_ReturnsData()
    {
        // Given
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        // When
        var result = await client.ExecuteCommand(new ExecuteCommandParams<VOStringExport>
        {
            Command = Commands.ExtractSpreadsheet,
            Arguments = new JArray(
                DocumentUri.FromFileSystemPath(filePath).ToString()
            )
        });

        // Then
        result.Errors.Should().BeEmpty();
        result.File.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Server_OnGettingGraph_ReturnsData()
    {
        // Given
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        // When
        var result = await client.ExecuteCommand(new ExecuteCommandParams<string>
        {
            Command = Commands.CreateDialogueGraph,
            Arguments = new JArray(
                DocumentUri.FromFileSystemPath(filePath).ToString(),
                "dot",
                "true"
            )
        });

        // Then
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Server_OnCompilingProject_GetsResult()
    {
        // Given
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Test.yarn");

        // When
        var result = await client.ExecuteCommand(new ExecuteCommandParams<CompilerOutput>
        {
            Command = Commands.CompileCurrentProject,
            Arguments = new JArray(
                DocumentUri.FromFileSystemPath(filePath).ToString()
            )
        });

        // Then
        result.Errors.Should().BeEmpty();
        result.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Server_OnGetDebugInfo_ReturnsDebugInfo()
    {
        // Given
        var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
        var project1Path = Path.Combine(TestUtility.PathToTestWorkspace, "Project1", "Project1.yarnproject");
    
        // When
        var result = await client.ExecuteCommand(new ExecuteCommandParams<Container<DebugOutput>>
        {
            Command = Commands.GenerateDebugOutput, 
        });

        // Then
        result.Should().HaveCount(2, "there are two projects in the workspace");

        var firstProject = result.Should().Contain(info => DocumentUri.GetFileSystemPath(info.SourceProjectUri!) == project1Path).Subject;

        firstProject.Variables.Should().NotBeEmpty();
        firstProject.Variables.Should().ContainEquivalentOf(new
        {
            Name = "$myVar",
            Type = "String",
            IsSmartVariable = false
        });
        
        firstProject.Variables.Should().ContainEquivalentOf(new
        {
            Name = "$playerCanAffordPies",
            Type = "Bool",
            IsSmartVariable = true
        });
    }

    private static YarnSpinnerParser Parse(string input, int initialLexerMode = -1) {
        var lexer = new YarnSpinnerLexer(Antlr4.Runtime.CharStreams.fromstring(input));
        if (initialLexerMode >= 0) {
            lexer.PushMode(initialLexerMode);
        }
        var tokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
        var parser = new YarnSpinnerParser(tokenStream);
        return parser;
    }

    [Fact]
    public void ExpressionFormatter_GivenExpressions_CanProduceObjects()
    {
        // Given
        var input = "$foo and ($bar or not true)";

        // When
        
        var expressionParseTree = Parse(input, YarnSpinnerLexer.ExpressionMode).expression();

        expressionParseTree.Should().NotBeNull();

        var andExpression = new ExpressionToJSONVisitor().Visit(expressionParseTree);

        // Then
        andExpression.Type.Should().Be(JSONExpression.ExpressionType.And);
        andExpression.Children.Should().HaveCount(2);

        var fooReference = andExpression.Children.ElementAt(0)!;
        fooReference.Type.Should().Be(JSONExpression.ExpressionType.Literal);
        fooReference.Literal.Should().Be("$foo");
        fooReference.Children.Should().BeEmpty();

        var orExpression = andExpression.Children.ElementAt(1)!;
        orExpression.Type.Should().Be(JSONExpression.ExpressionType.Or);
        orExpression.Children.Should().HaveCount(2);

        var barReference = orExpression.Children.ElementAt(0);
        barReference.Type.Should().Be(JSONExpression.ExpressionType.Literal);
        barReference.Literal.Should().Be("$bar");
        barReference.Children.Should().BeEmpty();

        var notExpression = orExpression.Children.ElementAt(1);
        notExpression.Type.Should().Be(JSONExpression.ExpressionType.Not);
        notExpression.Children.Should().ContainSingle();

        var trueConstant = notExpression.Children.Single();
        trueConstant.Type.Should().Be(JSONExpression.ExpressionType.Constant);
        trueConstant.Constant.Should().Be(true);
        trueConstant.Children.Should().BeEmpty();

    }
}
