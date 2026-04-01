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
        [InlineData("<<detour B>>", "B", 2, 9)]
        [InlineData("<<jump B>>", "B", 2, 7)]
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

        private void PerformCommonSingleDiagLineTest(string input, string code, string message, Diagnostic.DiagnosticSeverity severity, Range range)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be(code);

            diag.Severity.Should().Be(severity);

            diag.Message.Should().Be(message);

            diag.Range.Should().Be(range);
        }
        private void PerformCommonSingleDiagNodeTest(string input, string code, string message, Diagnostic.DiagnosticSeverity severity, Range range)
        {
            var job = CompilationJob.CreateFromString("<input>", input);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be(code);

            diag.Severity.Should().Be(severity);

            diag.Message.Should().Be(message);

            diag.Range.Should().Be(range);
        }

        [Theory]
        [InlineData("{made_up_function()}", "made_up_function", 2, 1)]
        [InlineData("{made_up_function(1,2,3,4)}", "made_up_function", 2, 1)] // the range shouldn't change from the above as it's still an unknown function
        [InlineData("<<if made_up_function()>>\n\tnormal line\n<<endif>>", "made_up_function", 2, 5)]
        public void TestUnknownFunctionGeneratesDiagnostic(string input, string functionName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + functionName.Length);

            PerformCommonSingleDiagLineTest(input, "YS0013", $"Invalid function call: {functionName}", Diagnostic.DiagnosticSeverity.Error, range);
        }

        [Theory]
        [InlineData(
@"title: A
when: made_up_function()
---
normal line
===", "made_up_function", 1, 6)]
        [InlineData(
@"title: A
when: made_up_function(1,2,3,4)
---
normal line
===", "made_up_function", 1, 6)]
        public void TestUnknownFunctionInWhensGenerateDiagnostics(string input, string functionName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + functionName.Length);

            PerformCommonSingleDiagNodeTest(input, "YS0013", $"Invalid function call: {functionName}", Diagnostic.DiagnosticSeverity.Error, range);
        }

        [Theory]
        [InlineData("{visited()}", "visited", 2, 1)]
        [InlineData("{visited(1)}", "visited", 2, 1)]
        [InlineData("{visited(true)}", "visited", 2, 1)]
        [InlineData("{visited(\"node\", 1)}", "visited", 2, 1)]
        [InlineData("{visited(\"node\", true)}", "visited", 2, 1)]
        [InlineData("{visited(\"node\", \"node\")}", "visited", 2, 1)]
        public void TestKnownFunctionWithIncorrectNumberOfParametersGeneratesDiagnostic(string input, string functionName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + functionName.Length);

            PerformCommonSingleDiagLineTest(input, "YS0013", $"Invalid function call: {functionName}", Diagnostic.DiagnosticSeverity.Error, range);
        }

        [Theory]
        [InlineData(
@"title: A
when: visited()
---
line
===", "visited", 1, 6)]
        [InlineData(
@"title: A
when: visited(1)
---
line
===", "visited", 1, 6)]
        [InlineData(
@"title: A
when: visited(true)
---
line
===", "visited", 1, 6)]
        [InlineData(
@"title: A
when: visited(""node"", 1)
---
line
===", "visited", 1, 6)]
        [InlineData(
@"title: A
when: visited(""node"", true)
---
line
===", "visited", 1, 6)]
        [InlineData(
@"title: A
when: visited(""node"", ""node"")
---
line
===", "visited", 1, 6)]
        public void TestKnownFunctionWithIncorrectNumberOfParametersInWhenGeneratesDiagnostic(string input, string functionName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + functionName.Length);

            PerformCommonSingleDiagNodeTest(input, "YS0013", $"Invalid function call: {functionName}", Diagnostic.DiagnosticSeverity.Error, range);
        }

        [Theory]
        [InlineData("<<made up command>>", "made", 2, 3)]
        [InlineData("<<made {1}>>", "made", 2, 2)]
        public void TestUnknownCommandGeneratesDiag(string input, string commandName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + commandName.Length);

            PerformCommonSingleDiagLineTest(input, "YS0014", $"Invalid command: {commandName}", Diagnostic.DiagnosticSeverity.Error, range);
        }

        [Theory]
        [InlineData("<<wait 1 2>>", "wait", 2, 2)]
        [InlineData("<<wait \"hello\">>", "wait", 2, 2)]
        [InlineData("<<wait true>>", "wait", 2, 2)]
        public void TestKnownCommandWithWrongParametersGeneratesDiag(string input, string commandName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + commandName.Length);

            PerformCommonSingleDiagLineTest(input, "YS0014", $"Invalid command: {commandName}", Diagnostic.DiagnosticSeverity.Error, range);
        }
        [Fact]
        public void TestEscapedUnknownCommandsDontGenerateDiagnostics()
        {
            var source = CreateTestNode("\\<<made up command>>", "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().BeEmpty();
        }
        // how to handle interpolated command names and how to handle interpolated parameters?

        [Theory]
        [InlineData(
@"title: A
---
<<jump B>>
===
title: B
---
<<jump C>>
===
title: C
---
<<jump A>>
===", "A -> B -> C", 8, 0, 8, 9)]
        [InlineData(
@"title: A
---
<<detour B>>
===
title: B
---
<<detour C>>
===
title: C
---
<<detour A>>
===", "A -> B -> C", 8, 0, 8, 9)]
        public void TestCyclicNodesGenerateDiagnostic(string input, string cycle, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);

            PerformCommonSingleDiagNodeTest(input, "YS0015", $"Cyclic dependency detected: {cycle}", Diagnostic.DiagnosticSeverity.Warning, range);
        }

        [Fact]
        public void TestDialogueWithBothLineAndShadowIDGenerateDiag()
        {
            var input = "this line has both a line id and a shadow #line:abc123 #shadow:def123\nthis line has both a line id and a shadow #line:def123";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 42, 2, 54),
                new(2, 55, 2, 69),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be("YS0017");

                diag.Message.Should().Be("Lines cannot have both a '#line' tag and a '#shadow' tag.");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }

        [Fact]
        public void TestDuplicateLineIdsGeneratesWarnings()
        {
            var input = "first line #line:abc123\nsecond line #line:abc123";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 1, 2, 23),
                new(3, 12, 3, 24),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be("YS0018");

                diag.Message.Should().Be("Duplicate line ID 'line:abc123'");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }

        [Theory]
        [InlineData("$abc", 7)]
        [InlineData(".abc", 7)]
        [InlineData("123abc", 7)]
        [InlineData("abc.123", 7)]
        public void TestInvalidNodeNamesGenerateDiagnostics(string input, int column)
        {
            var content = $"title:{input}\n---\nline of text\n===";
            var message = $"The node title '{input}' contains invalid characters. Titles can only contain letters, numbers, and underscores.";
            var range = new Range(0, column, 0, column + input.Length);

            PerformCommonSingleDiagNodeTest(content, "YS0027", message, Diagnostic.DiagnosticSeverity.Error, range);
        }
        [Theory]
        [InlineData("ḀBC")]
        [InlineData("_abc")]
        [InlineData("_")]
        public void TestValidButNotBasicAsFuckTitlesDoesNotGenerateDiagnostic(string input)
        {
            var content = $"title:{input}\n---\nline of text\n===";
            var job = CompilationJob.CreateFromString("<input>", content);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().BeEmpty();
        }
        
        // same as the above but it's for subtitles
        [Theory]
        [InlineData("$abc", 7)]
        [InlineData(".abc", 7)]
        [InlineData("123abc", 7)]
        [InlineData("abc.123", 7)]
        public void TestInvalidSubtitleNamesGenerateDiagnostics(string input, int column)
        {
            var content = $"title:start\nsubtitle:{input}\n---\nline of text\n===";
            var message = $"The node subtitle '{input}' contains invalid characters. Titles can only contain letters, numbers, and underscores.";
            var range = new Range(0, column, 0, column + input.Length);

            PerformCommonSingleDiagNodeTest(content, "YS0027", message, Diagnostic.DiagnosticSeverity.Error, range);
        }
        [Theory]
        [InlineData("ḀBC")]
        [InlineData("_abc")]
        [InlineData("_")]
        public void TestValidButNotBasicAsFuckSubtitlesDoesNotGenerateDiagnostic(string input)
        {
            var content = $"title:start\nsubtitle:{input}\n---\nline of text\n===";
            var job = CompilationJob.CreateFromString("<input>", content);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().BeEmpty();
        }

        [Fact]
        public void TestRedeclaredVariablesGeneratesDiagnostics()
        {
            var input = "<<declare $var = 5>>\n<<declare $var = true>>";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 0, 2, 20),
                new(3, 0, 3, 23),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be("YS0039");

                diag.Message.Should().Be("Redeclaration of existing variable $var");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }
    }
}
