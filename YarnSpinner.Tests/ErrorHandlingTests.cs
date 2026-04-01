using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;

/*
this is a ruler to let you more easily check ranges of any line of text

012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890
_         1.        2.        3.        4.        5.        6.        7.        8.        9.        100.      _

*/
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

        [Theory]
        [InlineData("<<declare $x = \"hello\" as Number>>", "Type mismatch: expected Number, got string")]
        [InlineData("<<declare $x = true as Number>>", "Type mismatch: expected Number, got bool")]
        [InlineData("<<declare $x = \"true\" as bool>>", "Type mismatch: expected bool, got string")]
        [InlineData("<<declare $x = 123 as bool>>", "Type mismatch: expected bool, got Number")]
        [InlineData("<<declare $x = 123 as string>>", "Type mismatch: expected string, got Number")]
        [InlineData("<<declare $x = true as string>>", "Type mismatch: expected string, got bool")]
        public void TestDeclaredValueIsDifferentFromExplicitType(string input, string message)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
            diagnostic.Code.Should().Be("YS0002");

            diagnostic.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            diagnostic.Message.Should().Be(message);
        }
        
        [Theory]
        [InlineData("<<set $x = 5>>", "Variable '$x' is used but not declared. Declare it with: <<declare $x = value>>")]
        [InlineData("<<set $x = true>>", "Variable '$x' is used but not declared. Declare it with: <<declare $x = value>>")]
        [InlineData("<<set $x = \"value\">>", "Variable '$x' is used but not declared. Declare it with: <<declare $x = value>>")]
        public void TestImplictVariableGenerateShouldBeDeclaredWarning(string input, string message)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
            diagnostic.Code.Should().Be("YS0003");

            diagnostic.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);

            diagnostic.Message.Should().Be(message);
        }

        // is it worth having a test for the situation where you are missing a terminator and then you have a bunch of lines that look like a header followed by a ---?        
        [Theory]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator
", 3, 1)]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator", 2, 49)]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator===", 2, 52)]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator===
", 3, 1)]
        public void TestMissingBodyEndInternals(string input, int line, int character)
        {
            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be("YS0004");

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            diag.Message.Should().Be("Missing node delimiter");

            diag.Range.End.Line.Should().Be(line);
            diag.Range.Start.Line.Should().Be(line);
            diag.Range.Start.Character.Should().Be(0);
            diag.Range.End.Character.Should().Be(character);
        }

        [Theory]
        [InlineData(
@"title: Program
---
<<if true>>
    internal line
===", 4,0,4,3)]
    [InlineData(
@"title: Program
---
<<if true>>
    internal line
<<else>>
    second internal line
===", 6,0,6,3)]
    [InlineData(
@"title: Program
---
<<if true>>
    internal line
<<elseif 5 < 3>>
    second internal line
===", 6,0,6,3)]
    [InlineData(
@"title: Program
---
<<if true>>
    internal line
<<elseif 5 < 3>>
    second internal line
<<else>>
    third internal line
===", 8,0,8,3)]
    [InlineData(
@"title: Program
---
<<if true>>
    internal line
    \<<endif>>
===", 5,0,5,3)]
        public void TestMissingClosingScopeGeneratesDiagnostic(string input, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);

            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be("YS0007");

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            diag.Message.Should().Be($"Unclosed scope: missing <<endif>>");

            diag.Range.Should().Be(range);
        }

        [Theory]
        [InlineData(
@"title: A
---
<<detour B>>
<<jump C>>
===
title: B
---
This node has a single detour reference to it
===
title: C
---
This node has a single jump reference to it
===", 0, 8, 0, 9)]
        public void TestUnreferencedNodesCreateDiagnostics(string input, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);

            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be("YS0009");

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Info);

            diag.Message.Should().Be($"Node 'A' is never referenced");
            
            diag.Range.Should().Be(range);
        }

        [Theory]
        [InlineData("<<declare $somevar = 123>>", "$somevar", 1,0,1,27)]
        [InlineData("<<declare $somevar = \"hello\">>", "$somevar", 1,0,1,31)]
        [InlineData("<<declare $somevar = false>>", "$somevar", 1,0,1,29)]
        public void TestUnusedDeclaredVarsGenerateDiagnostic(string input, string varName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be("YS0010");

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Info);

            diag.Message.Should().Be($"Variable '{varName}' is declared but never used");

            diag.Range.Should().Be(range);
        }

        [Theory]
        [InlineData(@"title: A
when: $somevar == true
---
<<declare $somevar = false>>
===")]
        [InlineData(@"title: A
---
<<declare $somevar = false>>
<<set $somevar = true>>
===")]
        [InlineData(@"title: A
---
<<declare $somevar = false>>
<<if $somevar>>
    internal line
<<endif>>
===")]
        [InlineData(@"title: A
---
<<declare $somevar = false>>
the value is {$somevar}
===")]
        public void TestUsedVariablesShouldntGenerateDiagnostic(string input)
        {
            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);
            var diagnostic = result.Diagnostics.Should().NotContain(d => d.Code == "YS0010");
        }

        // this doesn't test the workflow of multiple files
        [Fact]
        public void TestDuplicateNonNodeGroupsShouldGenerateDiagnostics()
        {
            var input = 
@"title: A
---
This is a line
===
title: A
---
This is a line
===";

            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);
            
            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(0,0,0,9),
                new(4,0,4,9),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be("YS0011");

                diag.Message.Should().Be("Duplicate node title: 'A'");

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }

        [Fact]
        public void TestDuplicateNodeGroupsShouldNotGenerateDiagnostics()
        {
            var input = 
@"title: A
when: always
---
This is a line
===
title: A
when: always
---
This is a line
===";

            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);
            
            result.Diagnostics.Should().BeEmpty();
        }

        [Theory]
        [InlineData("<<detour B>>", "B", 2, 10)]
        [InlineData("<<jump B>>", "B", 2, 8)]
        public void TestUnknownNodeJumpsGenerateDiagnostics(string input, string nodeName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + nodeName.Length);

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be("YS0012");

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            diag.Message.Should().Be($"Jump to undefined node: '{nodeName}'");

            diag.Range.Should().Be(range);
        }
    }
}
