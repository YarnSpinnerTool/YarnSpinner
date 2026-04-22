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
---
// comment on its own line followed by blank line

Line: a line of dialogue #line:line_1234 #hashtag // comment following a line
-> option 1
->    option 2 with extra whitespace in front and after     // comment after whitespace
    line inside an option #hashtag // comment after line in option

<<if $x is true>>
    Content inside an if
    More content inside an if
    An inline expression { $x or false }
<<endif>>
===";

            var job = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(job);
            result.ContainsErrors.Should().Be(false);

            // When
            var parseResult = Yarn.Compiler.Utility.ParseSourceText(source);
            var node = SyntaxNode.VisitTree(parseResult.Tokens, parseResult.Tree);


            // Then
            node.Text.Should().Be(source, "round-tripping from source code to a syntax node tree should produce identical content");
        }
    }
}
