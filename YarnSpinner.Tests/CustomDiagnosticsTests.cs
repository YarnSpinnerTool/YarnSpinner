using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;

#nullable enable

namespace YarnSpinner.Tests
{
    public class CustomDiagnosticsTests : TestBase
    {
        public CustomDiagnosticsTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

        [Fact]
        public void TestCustomDiagnosticsProvidersCanBeProvided()
        {
            // Given
            var source = """
            title: Start
            ---
            Here's a long line. It's really long. Too long for most, some would say.
            ===
            """;

            var job = CompilationJob.CreateFromString("MyCoolFile.yarn", source);
            job.CustomDiagnosticProviders = [
                new LineTooLongDiagnosticsProvider(60),
                new LineMustHaveSpecialHashtagProvider()
            ];

            // When
            var result = Compiler.Compile(job);

            // Then
            var lineTooLongDiag = result.Diagnostics.Should().Contain(d => d.Message == "Line is longer than 60 characters").Subject;
            lineTooLongDiag.Code.Should().Be(DiagnosticDescriptor.CustomDiagnostic.Code);
            lineTooLongDiag.FileName.Should().Be("MyCoolFile.yarn");
            lineTooLongDiag.Range.Start.Line.Should().Be(2);
            lineTooLongDiag.Range.Start.Character.Should().Be(0);
            lineTooLongDiag.Range.Start.Line.Should().Be(2);
            lineTooLongDiag.Range.End.Character.Should().Be(73);
            lineTooLongDiag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);

            var missingHashtagDiag = result.Diagnostics.Should().Contain(d => d.Message == "Line is missing #test").Subject;
            missingHashtagDiag.FileName.Should().Be("MyCoolFile.yarn");
            missingHashtagDiag.Range.Start.Line.Should().Be(2);
            missingHashtagDiag.Range.Start.Character.Should().Be(0);
            missingHashtagDiag.Range.Start.Line.Should().Be(2);
            missingHashtagDiag.Range.End.Character.Should().Be(73);
            missingHashtagDiag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);
        }

        private void ExpectLineToMatch(string lineText, string ruleText, bool expectMatch)
        {
            // Given
            var rule = CustomLineRule.Parse(ruleText);

            // When
            var parseResult = Yarn.Compiler.Utility.ParseSourceText(CreateTestNode(lineText));
            var tree = SyntaxNode.VisitTree(parseResult);
            var line = tree.RootNode.GetDescendants<LineStatementSyntaxNode>().Single();

            // Then
            var ruleCheck = rule.CheckLine(line);
            ruleCheck.Should().Be(expectMatch, $"'{lineText}' {(expectMatch ? "should" : "should not")} match '{ruleText}'");
        }

        [Theory]
        [InlineData("Here's another line of dialogue that's really really long, longer than 60 characters", true)]
        [InlineData("Here's a line of dialogue that's less than 60 chars.", false)]
        public void TestLineLengthsCanBeChecked(string lineText, bool matches)
        {
            ExpectLineToMatch(lineText, "length() > 60", matches);
        }

        [Theory]
        [InlineData("Here's a line. It mentions potatoes.", true)]
        [InlineData("Here's a line. It does not mention spuds.", false)]
        public void TestRulesCanCheckRegexes(string lineText, bool matches)
        {
            ExpectLineToMatch(lineText, @"matches(""potato"")", matches);
        }

        [Theory]
        [InlineData("Capsley: Here's a line!", true)]
        [InlineData("No character on this line", false)]
        [InlineData(@"Hey\: an escaped character is on this line", false)]
        public void TestCharacterNamesCanBeDetected(string lineText, bool matches)
        {
            ExpectLineToMatch(lineText, @"has_character()", matches);
        }

        [Theory]
        [InlineData("Capsley: Here's a line!", true)]
        [InlineData("No character on this line", false)]
        [InlineData(@"Hey\: an escaped character is on this line", false)]
        public void TestCharacterNamesCanBeExtracted(string lineText, bool matches)
        {
            ExpectLineToMatch(lineText, @"character() == ""Capsley""", matches);
        }

        [Theory]
        [InlineData("Capsley: Hey I'm missing a special hashtag", false)]
        [InlineData("Capsley: Hey I have the special hashtag #special", true)]
        [InlineData("This line has the special hashtag but no character #special", false)]
        [InlineData("This line has neither the special hashtag nor character", false)]
        public void TestRulesCanContainLogic(string lineText, bool matches)
        {
            ExpectLineToMatch(lineText, @"character() == ""Capsley"" and has_hashtag(""special"") ", matches);
        }

        [Theory]
        [InlineData("Capsley: This line has a character", false)]
        [InlineData("This line has no character", true)]
        public void TestRulesCanBeInverted(string lineText, bool matches)
        {
            ExpectLineToMatch(lineText, @"not(has_character())", matches);
        }

    }

    class LineMustHaveSpecialHashtagProvider : ICustomDiagnosticProvider
    {
        public void ProvideDiagnostics(IBuildContext context)
        {
            foreach (var stmt in context.LineStatements)
            {
                if (!stmt.Hashtags.Contains("test"))
                {
                    context.EmitDiagnostic(stmt, "Line is missing #test");
                }
            }
        }
    }

    class LineTooLongDiagnosticsProvider : ICustomDiagnosticProvider
    {
        public LineTooLongDiagnosticsProvider(int maxLength)
        {
            if (maxLength <= 0)
            {
                throw new System.ArgumentException("Minimum line length can't be less than 1");
            }
            this.MaxLength = maxLength;
        }

        public int MaxLength { get; }

        public void ProvideDiagnostics(IBuildContext context)
        {
            foreach (var statement in context.LineStatements)
            {
                if (statement.LineText.Length > MaxLength)
                {
                    context.EmitDiagnostic(statement, $"Line is longer than {MaxLength} characters", Diagnostic.DiagnosticSeverity.Warning);
                }
            }
        }
    }
}

