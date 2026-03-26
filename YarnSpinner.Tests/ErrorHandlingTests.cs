using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{
    public class ErrorHandlingTests : AsyncTestBase
    {
        public ErrorHandlingTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void TestMalformedIfStatement()
        {
            var source = CreateTestNode(@"<<if true>> // error: no endif");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains("Expected an <<endif>> to match the <<if>> statement on line 3"));
        }

        [Fact]
        public void TestExtraneousElse()
        {
            var source = CreateTestNode(@"
            <<if true>>
            One
            <<else>>
            Two
            <<else>>
            Three
            <<endif>>");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains("More than one <<else>> statement in an <<if>> statement isn't allowed"));
            result.Diagnostics.Should().Contain(d => d.Message.Contains("Unexpected \"endif\" while reading a statement"));

        }

        [Fact]
        public void TestEmptyCommand()
        {
            var source = CreateTestNode(@"
            <<>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains("Command text expected"));
        }

        [Fact]
        public void TestInvalidVariableNameInSetOrDeclare()
        {
            var source1 = CreateTestNode(@"
            <<set test = 1>>
            ");

            var source2 = CreateTestNode(@"
            <<declare test = 1>>
            ");

            foreach (var source in new[] { source1, source2 })
            {

                var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

                result.Diagnostics.Should().Contain(d => d.Message == "Variable names need to start with a $");
            }
        }

        [Fact]
        public void TestInvalidFunctionCall()
        {
            var source = CreateTestNode("<<if someFunction(>><<endif>>");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains(@"Unclosed command: missing >>"));
        }

        // testing that warnings are generated for empty nodes
        [Fact]
        public void TestEmptyNodesGenerateWarnings()
        {
            var source = CreateTestNode("", "Start");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<inputs>", source));

            var warning = result.Diagnostics.Should().ContainSingle().Subject;
            // there should be only one diagnostic: a warning about empty nodes
            warning.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);
            warning.Message.Should().Be("Node \"Start\" is empty and will not be included in the compiled output.");
        }

        [Fact]
        public void TestLineCommandOverlapGeneratesDiagnostics()
        {
            /*
            there is currently a bug that means if you have a yarn file with content like
                this is the line before a command <<after command>>
                <<before command>> this is a line following a command
            you will only get the error, and no warning because the line with "<<before command>> this is a line following a command" will be lexed in command mode
            and basically come out as gobbledygook of tokens which we can't trust and therefore can't reliably make an error for them
            */
            var originalText = 
@"title: Program
---
this is a line with a valid conditional <<if true>>
<<before command>> this is a line following a command
this is the line before a command <<after command>>
===
";

            var job = CompilationJob.CreateFromString("input", originalText);
            var result = Compiler.Compile(job);

            // we expect a YS0020 and a YS0019
            result.Diagnostics.Should().HaveCount(2);

            // the command after line warning
            var diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == "YS0020").Subject;
            diagnostic.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);
            
            diagnostic.Message.Should().Be($"Command \"<<after command>>\" found following a line of dialogue. Commands should start on a new line.");
            
            var range = new Range(4, 34, 4, 51);
            diagnostic.Range.Should().Be(range);

            var context = "this is the line before a command <<after command>>\n                                  ^^^^^^^^^^^^^^^^^";
            diagnostic.Context.Should().Be(context);

            // our command before line warning
            diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == "YS0019").Subject;
            diagnostic.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);
            
            diagnostic.Message.Should().Be($"Dialogue \"this is a line following a command\" content found following a command. Commands should be on their own line.");
            
            range = new Range(3, 19, 3, 53);
            diagnostic.Range.Should().Be(range);

            // currently this doesn't work because we generate this warning via a listener
            // and the listener doesn't seem to get given the entire token stream so looking for newlines fails
            // so we can't then go "it's this range inside the string" without the full line
            // context = "<<before command>> this is a line following a command\n                  ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^";
            // diagnostic.Context.Should().Be(context);
        }

        [Theory]
        [InlineData("some command>>",     "YS0021", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 12, 2, 14})]
        [InlineData("<some command>>",    "YS0021", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 13, 2, 15})]
        [InlineData("some command>",      "YS0021", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 12, 2, 13})]
        [InlineData("<some command>",     "YS0048", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 0,  2, 14})]
        [InlineData("<<some command",     "YS0006", Diagnostic.DiagnosticSeverity.Error, new int[] {2, 14,  2, 15})]
        [InlineData("<<some command>",    "YS0006", Diagnostic.DiagnosticSeverity.Error, new int[] {2, 14,  2, 15})]
        /*
        012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890
        _         1.        2.        3.        4.        5.        6.        7.        8.        9.        100.      _
        */
        public void TestUnbalancedCommandTerminalsGenerateDiagnostics(string input, string code, Diagnostic.DiagnosticSeverity severity, int[] rangeValues)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == code).Subject;
            diagnostic.Severity.Should().Be(severity);

            rangeValues.Length.Should().Be(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);
            diagnostic.Range.Should().Be(range);
        }

        [Theory]
        [InlineData("set $foo = 5",       "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 0,  2, 3})]
        [InlineData("declare $foo = 5",   "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 0,  2, 7})]
        [InlineData("jump here",          "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 0,  2, 4})]
        [InlineData("jump {$here}",       "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 0,  2, 4})]
        [InlineData("detour here",        "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 0,  2, 6})]
        [InlineData("detour {$here}",     "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] {2, 0,  2, 6})]
        public void TestBuiltInCommandsMissingTerminalsGenerateDiagnostics(string input, string code, Diagnostic.DiagnosticSeverity severity, int[] rangeValues)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == code).Subject;
            diagnostic.Severity.Should().Be(severity);

            rangeValues.Length.Should().Be(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);
            diagnostic.Range.Should().Be(range);
        }

        [Theory]
        [InlineData("I declare $foo = 5")]
        [InlineData("I set $foo = 5")]
        [InlineData("I set foo = 5")]
        [InlineData("jump 123abc")]
        [InlineData("detour 123abc")]
        public void TestValidCommandAlikeLinesDontGenerateDiagnostics(string input)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().BeEmpty();
        }
    }
}
