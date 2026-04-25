using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;

#nullable enable

namespace YarnSpinner.Tests
{
    public partial class TreeExtractionTests : TestBase
    {
        public TreeExtractionTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

        [Fact]
        public void TestTreeExtraction()
        {
            // Given
            var source = @"// Comment in the headers
title: Start
custom_header: wooo
---
// comment on its own line followed by blank line

Line: a line of dialogue #line:line_1234 #hashtag // comment following a line
-> option 1
->    option 2 with extra whitespace in front and after     // comment after whitespace
    line inside an option #hashtag // comment after line in option

CharacterName: This is a very very long line of dialogue that's certain to overflow whatever text box is shown on screen. Gosh, it's long. The writer must have had too much coffee.

<<if $x is true>>
    Content inside an if
    More content inside an if
    An inline expression { $x or false }
<<endif>>

<<some_custom_command param1 {$x} ""quoted string param"" 4.2>>

=> Line group item 1 #hashtag:1
=> Line group item 2
===";

            var testScriptName = "TestScript.yarn";
            var job = CompilationJob.CreateFromString(testScriptName, source);
            var result = Compiler.Compile(job);
            result.ContainsErrors.Should().Be(false);

            // When
            var parseResult = Yarn.Compiler.Utility.ParseSourceText(source, testScriptName);
            var tree = SyntaxNode.VisitTree(parseResult);
            var syntaxNode = tree.RootNode;

            // Then
            syntaxNode.Text.Should().Be(source, "round-tripping from source code to a syntax node tree should produce identical content");
            syntaxNode.SyntaxTree.Name.Should().Be(testScriptName);

            var lines = syntaxNode.GetDescendants<LineStatementSyntaxNode>();
            lines.Should().NotBeEmpty();
            var line = lines.Should().Contain(l => l.LineText == "Line: a line of dialogue").Subject;
            line.LineID.Should().Be("line:line_1234");
            line.Hashtags.Should().Contain("hashtag");

            var options = syntaxNode.GetDescendants<OptionStatementSyntaxNode>().Should().ContainSingle().Subject;
            options.Options.Should().NotBeEmpty();
            options.Options.Should().Contain(l => l.LineText == "option 1");

            var lineGroup = syntaxNode.GetDescendants<LineGroupStatementSyntaxNode>().Should().ContainSingle().Subject;
            lineGroup.Lines.Should().NotBeEmpty();
            lineGroup.Lines.Should().Contain(l => l.LineText == "Line group item 1");

            var command = syntaxNode.GetDescendants<CommandStatementSyntaxNode>().Should().ContainSingle().Subject;
            command.CommandText.Should().Be(@"some_custom_command param1 {0} ""quoted string param"" 4.2");

            var node = syntaxNode.GetDescendants<NodeSyntaxNode>().Should().ContainSingle().Subject;
            node.Title.Should().Be("Start");
            node.Headers.Should().ContainKey("custom_header").WhoseValue.Should().Be("wooo");
        }
    }
}
