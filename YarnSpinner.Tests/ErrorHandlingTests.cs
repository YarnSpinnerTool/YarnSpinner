using System.Collections.Generic;
using System.Linq;
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


    public class ErrorHandlingTests : TestBase
    {
        public ErrorHandlingTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void TestMalformedIfStatement()
        {
            var source = CreateTestNode(@"<<if true>> // error: no endif");

            var result = Compiler.Compile(CompilationJob.CreateFromString("<input>", source));

            result.Diagnostics.Should().Contain(d => d.Message.Contains("Unclosed scope: expected an <<endif>> to match the <<if>> statement on line 3"));
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

        [Theory]
        [InlineData("<<set test = 1>>", 2, 6, 2, 10)]
        [InlineData("<<declare test = 1>>", 2, 10, 2, 14)]
        public void TestInvalidVariableNameInSetOrDeclare(string source, params int[] range)
        {
            PerformCommonSingleDiagLineTest(
                input: source,
                code: DiagnosticDescriptor.SyntaxError.Code,
                message: "Syntax error: Variable names need to start with a $",
                severity: Diagnostic.DiagnosticSeverity.Error,
                range: new Range(range[0], range[1], range[2], range[3]),
                allowOthers: true);
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
            var diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticDescriptor.CommandFollowingLine.Code).Subject;
            diagnostic.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            diagnostic.Message.Should().Be($"Command \"<<after command>>\" found following a line of dialogue. Commands should start on a new line.");

            var range = new Range(4, 34, 4, 51);
            diagnostic.Range.Should().Be(range);

            var context = "this is the line before a command <<after command>>\n                                  ^^^^^^^^^^^^^^^^^";
            diagnostic.Context.Should().Be(context);

            // our command before line warning
            diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticDescriptor.LineContentAfterCommand.Code).Subject;
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
        [InlineData("some command>>", "YS0021", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 12, 2, 14 })]
        [InlineData("<some command>>", "YS0021", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 13, 2, 15 })]
        [InlineData("some command>", "YS0021", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 12, 2, 13 })]
        [InlineData("<some command>", "YS0048", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 0, 2, 14 })]
        [InlineData("<<some command", "YS0006", Diagnostic.DiagnosticSeverity.Error, new int[] { 2, 14, 2, 15 })]
        [InlineData("<<some command>", "YS0006", Diagnostic.DiagnosticSeverity.Error, new int[] { 2, 14, 2, 15 })]
        public void TestUnbalancedCommandTerminalsGenerateDiagnostics(string input, string code, Diagnostic.DiagnosticSeverity severity, int[] rangeValues)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == code, $"{input} should produce this error").Subject;
            diagnostic.Severity.Should().Be(severity);

            rangeValues.Length.Should().Be(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);
            diagnostic.Range.Should().Be(range);
        }

        [Theory]
        [InlineData("set $foo = 5", "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 0, 2, 3 })]
        [InlineData("declare $foo = 5", "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 0, 2, 7 })]
        [InlineData("jump here", "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 0, 2, 4 })]
        [InlineData("jump {$here}", "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 0, 2, 4 })]
        [InlineData("detour here", "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 0, 2, 6 })]
        [InlineData("detour {$here}", "YS0022", Diagnostic.DiagnosticSeverity.Warning, new int[] { 2, 0, 2, 6 })]
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
        [InlineData("<<declare $x = \"hello\" as Number>>", "$x is declared to be a Number, but its initial value '\"hello\"' is a String")]
        [InlineData("<<declare $x = true as Number>>", "$x is declared to be a Number, but its initial value 'true' is a Bool")]
        [InlineData("<<declare $x = \"true\" as bool>>", "$x is declared to be a Bool, but its initial value '\"true\"' is a String")]
        [InlineData("<<declare $x = 123 as bool>>", "$x is declared to be a Bool, but its initial value '123' is a Number")]
        [InlineData("<<declare $x = 123 as string>>", "$x is declared to be a String, but its initial value '123' is a Number")]
        [InlineData("<<declare $x = true as string>>", "$x is declared to be a String, but its initial value 'true' is a Bool")]
        public void TestDeclaredValueIsDifferentFromExplicitType(string input, string message)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diagnostic = result.Diagnostics.Where(d => d.Severity == Diagnostic.DiagnosticSeverity.Error).Should().ContainSingle().Subject;
            diagnostic.Code.Should().Be(DiagnosticDescriptor.DeclarationValueDoesntMatchType.Code);

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

            var diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticDescriptor.UndefinedVariable.Code).Subject;
            diagnostic.Code.Should().Be(DiagnosticDescriptor.UndefinedVariable.Code);

            diagnostic.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);

            diagnostic.Message.Should().Be(message);
        }

        // is it worth having a test for the situation where you are missing a terminator and then you have a bunch of lines that look like a header followed by a ---?        
        [Theory]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator
", 3, 0)]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator", 2, 48)]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator===", 2, 51)]
        [InlineData(
@"title: Program
---
This node is missing it's end of body terminator===
", 3, 0)]
        public void TestMissingBodyEndInternals(string input, int line, int character)
        {
            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be(DiagnosticDescriptor.MissingDelimiter.Code);

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            diag.Message.Should().Be("Missing node delimiter");

            diag.Range.End.Line.Should().Be(line);
            diag.Range.Start.Line.Should().Be(line);
            diag.Range.Start.Character.Should().Be(character);
            diag.Range.End.Character.Should().Be(character + 1);
        }

        [Theory()]
        [InlineData(
@"title: Program
---
<<once>>
    internal line
===", "once", 4, 0, 4, 3)]
        [InlineData(
@"title: Program
---
<<once>>
    internal line
<<else>>
===", "once", 5, 0, 5, 3)]
        [InlineData(
@"title: Program
---
<<if true>>
    internal line
===", "if", 4, 0, 4, 3)]
        [InlineData(
@"title: Program
---
<<if true>>
    internal line
<<else>>
    second internal line
===", "if", 6, 0, 6, 3)]
        [InlineData(
@"title: Program
---
<<if true>>
    internal line
<<elseif 5 < 3>>
    second internal line
===", "if", 6, 0, 6, 3)]
        [InlineData(
@"title: Program
---
<<if true>>
    internal line
<<elseif 5 < 3>>
    second internal line
<<else>>
    third internal line
===", "if", 8, 0, 8, 3)]
        [InlineData(
@"title: Program
---
<<if true>>
    internal line
    \<<endif>>
===", "if", 5, 0, 5, 3)]
        public void TestMissingClosingScopeGeneratesDiagnostic(string input, string type, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);

            var job = CompilationJob.CreateFromString("input", input);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be(DiagnosticDescriptor.UnclosedScope.Code);

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            diag.Message.Should().Be($"Unclosed scope: expected an <<end{type}>> to match the <<{type}>> statement on line 3");

            diag.Range.Should().Be(range);
        }

        [Theory(Skip = "This diagnostic should be moved to the language server - whether or not a it's a problem that a node is unreferenced depends on the use case; additionally, at least one node will almost always be unreferenced, being the entry point")]
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
            diag.Code.Should().Be(DiagnosticDescriptor.UnreferencedNode.Code);
            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.None);

            diag.Message.Should().Be($"Node 'A' is never referenced");

            diag.Range.Should().Be(range);
        }

        [Theory]
        [InlineData("<<declare $somevar = 123>>", "$somevar", 2, 10, 2, 18)]
        [InlineData("<<declare $somevar = \"hello\">>", "$somevar", 2, 10, 2, 18)]
        [InlineData("<<declare $somevar = false>>", "$somevar", 2, 10, 2, 18)]
        public void TestUnusedDeclaredVarsGenerateDiagnostic(string input, string varName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(4);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[2], rangeValues[3]);

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle().Subject;
            diag.Code.Should().Be(DiagnosticDescriptor.UnusedVariable.Code);

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
            var diagnostic = result.Diagnostics.Should().NotContain(d => d.Code == DiagnosticDescriptor.UnusedVariable.Code);
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
                diag.Code.Should().Be(DiagnosticDescriptor.DuplicateNodeTitle.Code);

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
            diag.Code.Should().Be(DiagnosticDescriptor.UndefinedNode.Code);

            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning);

            diag.Message.Should().Be($"Jump to undefined node: '{nodeName}'");

            diag.Range.Should().Be(range);
        }

        private void PerformCommonSingleDiagLineTest(string input, string code, string message, Diagnostic.DiagnosticSeverity severity, Range range, bool allowOthers = false)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            job.Library = new Yarn.Library();
            job.Library.RegisterFunction("visited", (string nodeName) => true);

            var result = Compiler.Compile(job);
            Diagnostic diag;

            if (allowOthers)
            {
                diag = result.Diagnostics.Should().ContainSingle(d => d.Code == code, $"{input} should produce this diagnostic and no others").Subject;
            }
            else
            {
                diag = result.Diagnostics.Should().ContainSingle($"{input} should produce this diagnostic").Subject;
            }
            diag.Code.Should().Be(code);

            diag.Severity.Should().Be(severity);

            diag.Message.Should().Be(message);

            diag.Range.Should().Be(range);
        }
        private void PerformCommonSingleDiagNodeTest(string input, string code, string message, Diagnostic.DiagnosticSeverity severity, Range range, bool allowOthers = false)
        {
            var job = CompilationJob.CreateFromString("<input>", input);
            job.Library = new Yarn.Dialogue.StandardLibrary();
            job.Library.RegisterFunction("visited", (string nodeName) => true);

            var result = Compiler.Compile(job);


            Diagnostic diag;

            if (allowOthers)
            {
                diag = result.Diagnostics.Should().ContainSingle(d => d.Code == code).Subject;
            }
            else
            {
                diag = result.Diagnostics.Should().ContainSingle().Subject;
            }
            diag.Code.Should().Be(code);

            diag.Severity.Should().Be(severity);

            diag.Message.Should().Be(message);

            diag.Range.Should().Be(range);
        }

        [Theory]
        [InlineData("{visited(1)}", "1", "Number", 2, 9)]
        [InlineData("{visited(true)}", "true", "Bool", 2, 9)]
        public void TestKnownFunctionWithWrongParametersGeneratesDiagnostic(
            string input,
            string parameterValue,
            string parameterType,
            params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + parameterValue.Length);

            PerformCommonSingleDiagLineTest(
                input,
                DiagnosticDescriptor.TypeCheckerError.Code,
                $"{parameterValue} ({parameterType}) is not convertible to String",
                Diagnostic.DiagnosticSeverity.Error,
                range);
        }

        [Theory]
        [InlineData("{visited()}", "visited", 0, 2, 1)]
        [InlineData("{visited(\"node\", 1)}", "visited", 2, 2, 1)]
        [InlineData("{visited(\"node\", true)}", "visited", 2, 2, 1)]
        [InlineData("{visited(\"node\", \"node\")}", "visited", 2, 2, 1)]
        public void TestKnownFunctionWithIncorrectNumberOfParametersGeneratesDiagnostic(string input, string functionName, int paramCount, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + functionName.Length);

            PerformCommonSingleDiagLineTest(
                input,
                DiagnosticDescriptor.WrongFunctionParameters.Code,
                $"Invalid function call: {functionName} expects 1 parameter, not {paramCount}",
                Diagnostic.DiagnosticSeverity.Error,
                range);
        }

        [Theory]

        [InlineData(
        @"title: A
when: visited(1)
---
line
===", "1", "Number", 1, 14)]
        [InlineData(
        @"title: A
when: visited(true)
---
line
===", "true", "Bool", 1, 14)]

        public void TestKnownFunctionWithWrongParameterInWhenGeneratesDiagnostic(string input, string paramValue, string paramType, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + paramValue.Length);

            PerformCommonSingleDiagNodeTest(
                input,
                DiagnosticDescriptor.TypeCheckerError.Code,
                $"{paramValue} ({paramType}) is not convertible to String",
                Diagnostic.DiagnosticSeverity.Error,
                range, allowOthers: true);
        }

        [Theory]
        [InlineData(
@"title: A
when: visited()
---
line
===", "visited", 0, 1, 6)]
        [InlineData(
@"title: A
when: visited(""node"", 1)
---
line
===", "visited", 2, 1, 6)]
        [InlineData(
@"title: A
when: visited(""node"", true)
---
line
===", "visited", 2, 1, 6)]
        [InlineData(
@"title: A
when: visited(""node"", ""node"")
---
line
===", "visited", 2, 1, 6)]
        public void TestKnownFunctionWithIncorrectNumberOfParametersInWhenGeneratesDiagnostic(string input, string functionName, int paramCount, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + functionName.Length);

            PerformCommonSingleDiagNodeTest(
                input,
                DiagnosticDescriptor.WrongFunctionParameters.Code,
                $"Invalid function call: {functionName} expects 1 parameter, not {paramCount}",
                Diagnostic.DiagnosticSeverity.Error,
                range, allowOthers: true);
        }

        [Theory(Skip = "This test needs to be run in the language server, which has knowledge of commands")]
        [InlineData("<<made up command>>", "made", 2, 3)]
        [InlineData("<<made {1}>>", "made", 2, 2)]
        public void TestUnknownCommandGeneratesDiag(string input, string commandName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + commandName.Length);

            PerformCommonSingleDiagLineTest(input, DiagnosticDescriptor.UnknownCommand.Code, $"Invalid command: {commandName}", Diagnostic.DiagnosticSeverity.Error, range);
        }

        [Theory(Skip = "Must be handled by Language Server because the compiler doesn't know about valid commands")]
        [InlineData("<<wait 1 2>>", "wait", 2, 2)]
        [InlineData("<<wait \"hello\">>", "wait", 2, 2)]
        [InlineData("<<wait true>>", "wait", 2, 2)]
        // TODO: move to LSP
        public void TestKnownCommandWithWrongParametersGeneratesDiag(string input, string commandName, params int[] rangeValues)
        {
            rangeValues.Should().HaveCount(2);
            var range = new Range(rangeValues[0], rangeValues[1], rangeValues[0], rangeValues[1] + commandName.Length);

            PerformCommonSingleDiagLineTest(input, DiagnosticDescriptor.WrongCommandParameterCount.Code, $"Invalid command: {commandName}", Diagnostic.DiagnosticSeverity.Error, range);
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


        [Theory(Skip = "This error should be produced in the language server")]
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

            PerformCommonSingleDiagNodeTest(
                input: input,
                code: DiagnosticDescriptor.CyclicDependency.Code,
                message: $"Cyclic dependency detected: {cycle}",
                severity: Diagnostic.DiagnosticSeverity.Warning,
                range: range);
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
                diag.Code.Should().Be(DiagnosticDescriptor.LinesCantHaveLineAndShadowTag.Code);

                diag.Message.Should().Be("Lines cannot have both a '#line' tag and a '#shadow' tag.");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }

        [Fact]
        public void TestDuplicateLineIdsGeneratesWarnings()
        {
            // TODO: This should also test cases where the duplicate line is in
            // a different file
            var input = "first line #line:abc123\nsecond line #line:abc123";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 11, 2, 23),
                new(3, 12, 3, 24),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be(DiagnosticDescriptor.DuplicateLineID.Code);

                diag.Message.Should().Be("Duplicate line ID 'line:abc123'");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }

        [Fact]
        public void MultpleDuplicateLineIDsGeneratesWarning()
        {
            var input = "the line #line:abc123 #line:abc123";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 9, 2, 21),
                new(2, 22, 2, 34),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be(DiagnosticDescriptor.MultipleLineOrShadowIDsOnALine.Code);

                diag.Message.Should().Be("Dialogue has multiple '#line' or '#shadow' IDs.");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }
        [Fact]
        public void MultpleDifferentLineIDsGeneratesWarning()
        {
            var input = "the line #line:abc123 #line:def456";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 9, 2, 21),
                new(2, 22, 2, 34),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be(DiagnosticDescriptor.MultipleLineOrShadowIDsOnALine.Code);

                diag.Message.Should().Be("Dialogue has multiple '#line' or '#shadow' IDs.");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }
        [Fact]
        public void MultpleDuplicateShadowIDsGeneratesWarning()
        {
            var input = "the line #shadow:abc123 #shadow:abc123";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 9, 2, 23),
                new(2, 24, 2, 38),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be(DiagnosticDescriptor.MultipleLineOrShadowIDsOnALine.Code);

                diag.Message.Should().Be("Dialogue has multiple '#line' or '#shadow' IDs.");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }
        [Fact]
        public void MultpleDifferentShadowIDsGeneratesWarning()
        {
            var input = "the line #shadow:abc123 #shadow:def456";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 9, 2, 23),
                new(2, 24, 2, 38),
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be(DiagnosticDescriptor.MultipleLineOrShadowIDsOnALine.Code);

                diag.Message.Should().Be("Dialogue has multiple '#line' or '#shadow' IDs.");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }
        [Fact]
        public void MultipleConflictingIssuesAroundIDsGeneratesWarning()
        {
            var input = "this line has both a line id and a shadow #line:abc123 #shadow:def123 #shadow:ghi123";

            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().HaveCount(3);

            var ranges = new HashSet<Range>()
            {
                new(2, 42, 2, 54),
                new(2, 55, 2, 69),
                new(2, 70, 2, 84)
            };

            foreach (var diag in result.Diagnostics)
            {
                diag.Code.Should().Be(DiagnosticDescriptor.LinesCantHaveLineAndShadowTag.Code);

                diag.Message.Should().Be("Lines cannot have both a '#line' tag and a '#shadow' tag.");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
            ranges.Should().BeEmpty();
        }

        [Theory]
        [InlineData("$abc", "$", 6)]
        [InlineData(".abc", ".", 6)]
        [InlineData("123abc", "1", 6)]
        [InlineData("abc.123", ".", 9)]
        public void TestInvalidNodeNamesGenerateDiagnostics(string input, string unexpected, int column)
        {
            var content = $"title:{input}\n---\nline of text\n===";
            var message = $"Unexpected '{unexpected}' in node title. Titles can only contain letters, numbers, and underscores.";
            var range = new Range(0, column, 0, column + 1);

            PerformCommonSingleDiagNodeTest(content, DiagnosticDescriptor.InvalidNodeName.Code, message, Diagnostic.DiagnosticSeverity.Error, range);
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

        [Fact]
        public void TestMissingIDInNodeTitleGeneratesDiagnostics()
        {
            var content = "title: \n---\ncontent\n===\n";
            var range = new Range(0, 7, 0, 8);
            PerformCommonSingleDiagNodeTest(content, DiagnosticDescriptor.SyntaxError.Code, "Syntax error: missing ID at '\\n'", Diagnostic.DiagnosticSeverity.Error, range);
        }

        // same as the above but it's for subtitles
        [Theory]
        [InlineData("$abc", "$", 9)]
        [InlineData(".abc", ".", 9)]
        [InlineData("123abc", "1", 9)]
        [InlineData("abc.123", ".", 12)]
        public void TestInvalidSubtitleNamesGenerateDiagnostics(string input, string unexpected, int column)
        {
            var content = $"title:start\nsubtitle:{input}\nwhen: always\n---\nline of text\n===";
            var message = $"Unexpected '{unexpected}' in node subtitle. Titles can only contain letters, numbers, and underscores.";
            var range = new Range(1, column, 1, column + unexpected.Length);

            PerformCommonSingleDiagNodeTest(content, DiagnosticDescriptor.InvalidNodeName.Code, message, Diagnostic.DiagnosticSeverity.Error, range);
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

            var redeclarations = result.Diagnostics.Where(d => d.Code == DiagnosticDescriptor.RedeclarationOfExistingVariable.Code).ToList();

            redeclarations.Should().HaveCount(2);

            var ranges = new HashSet<Range>()
            {
                new(2, 0, 2, 20),
                new(3, 0, 3, 23),
            };

            foreach (var diag in redeclarations)
            {
                diag.Code.Should().Be(DiagnosticDescriptor.RedeclarationOfExistingVariable.Code);

                diag.Message.Should().Be("Redeclaration of existing variable $var");

                diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

                ranges.Should().Contain(diag.Range);
                ranges.Remove(diag.Range);
            }
        }

        [Theory]
        [InlineData("{$undeclared}", "$undeclared", 2, 1)]
        public void TestLinesWithUndeclaredVariablesGenerateDiagnostics(string input, string invalidExpression, int diagLine, int diagColumn)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);
            var diag = result.Diagnostics.Where(s => s.Code == DiagnosticDescriptor.ExpressionTypeUndetermined.Code).Should().ContainSingle().Subject;
            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            var expectedRange = new Range(diagLine, diagColumn, diagLine, diagColumn + invalidExpression.Length);
            diag.Message.Should().Be($"Can't determine the type of the expression {invalidExpression}.");
            diag.Range.Should().Be(expectedRange);
        }

        [Theory]
        [InlineData("<<set $x = $y>>", "$x", 2, 6)]
        public void TestLinesWithVariablesSetToValuesOfUnknownTypeGenerateDiagnostic(string input, string invalidExpression, int diagLine, int diagColumn)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diags = result.Diagnostics.Where(s => s.Code == DiagnosticDescriptor.ExpressionTypeUndetermined.Code).ToList();

            diags.Should().HaveCount(2);
            diags.Should().ContainSingle(d => d.Range == new Range(diagLine, diagColumn, diagLine, diagColumn + invalidExpression.Length))
                .Which.Message.Should().Be("Can't determine the type of the expression $x.");
        }

        [Theory]
        [InlineData("<<declare $x = (1)>>\n<<set $x = 2>>", "$x", 3, 6)]
        public void TestAttemptingToAssignValuesToSmartVariablesGeneratesDiagnostic(string input, string invalidExpression, int diagLine, int diagColumn)
        {
            var source = CreateTestNode(input, "Start");
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Where(s => s.Code == DiagnosticDescriptor.SmartVariableReadOnly.Code).Should().ContainSingle().Subject;
            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            var expectedRange = new Range(diagLine, diagColumn, diagLine, diagColumn + invalidExpression.Length);
            diag.Message.Should().Match($"{invalidExpression} cannot be modified (it's a smart variable and is always equal to *)");
            diag.Range.Should().Be(expectedRange);
        }

        [Theory]
        [InlineData(@"title: Group
when: always
---
Content
===
title: Group
---
Content
===", 5, 0, 5, 13)]
        [InlineData(@"title: Group
---
Content
===
title: Group
when: always
---
Content
===", 0, 0, 0, 13)]
        public void TestNodeInGroupWithMissingWhenClauseGeneratesDiagnostic(string source, params int[] range)
        {

            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diag = result.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticDescriptor.NodeGroupMissingWhen.Code).Subject;
            diag.Range.Should().Be(new Range(range[0], range[1], range[2], range[3]));
            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);
            diag.Message.Should().Be("All nodes in the group 'Group' must have a 'when' clause (use 'when: always' if you want this node to not have any conditions).");

        }

        [Fact]
        public void TestNodeInGroupWithDuplicateSubtitleGeneratesDiagnostic()
        {
            var source = @"title: Group
when: always
subtitle: x
---
Content
===
title: Group
when: always
subtitle: x
---
Content
===";
            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diags = result.Diagnostics.Where(d => d.Code == DiagnosticDescriptor.DuplicateSubtitle.Code).ToList();
            diags.Should().HaveCount(2);

            diags.Should().AllSatisfy(d => d.Message.Should().Be("More than one node in group Group has subtitle x."));
            diags.Should().AllSatisfy(d => d.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error));
            diags[0].Range.Should().Be(new Range(2, 0, 2, "subtitle: x".Length));
            diags[1].Range.Should().Be(new Range(8, 0, 8, "subtitle: x".Length));

        }

        [Fact]
        public void TestEmptyNodesGenerateDiagnostics()
        {
            var source = @"title: NonEmpty
---
Not empty, so included
===
title: Empty
---
===
title: EmptyWithComment
---
// only has a comment
===
";

            var job = CompilationJob.CreateFromString("<input>", source);
            var result = Compiler.Compile(job);

            var diags = result.Diagnostics.Where(d => d.Code == DiagnosticDescriptor.EmptyNode.Code).ToList();
            diags.Should().HaveCount(2);

            diags.Should().AllSatisfy(d => d.Message.Should().Match("Node \"*\" is empty and will not be included in the compiled output."));
            diags.Should().AllSatisfy(d => d.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Warning));
            diags[0].Range.Should().Be(new Range(4, 0, 6, "===".Length));
            diags[1].Range.Should().Be(new Range(7, 0, 10, "===".Length));
        }

        [Theory]
        [InlineData("<<enum Example>>\n<<case Test>>\n<<endenum>>", 3, 2, 0, 4, 11, "enums")]
        [InlineData("=> Line 1\n=> Line 2", 3, 2, 0, 3, 10, "line groups")]
        [InlineData("Here's a line <<once>>", 3, 2, 14, 2, 22, "'once' conditions")]
        [InlineData("<<once>>\nLine\n<<endonce>>", 3, 2, 0, 4, 11, "'once' statements")]
        public void TestUsingLanguageFeaturesFromFutureLanguageVersionGeneratesDiagnostic(string source, int minLanguageVersion, int diagStartLine, int diagStartColumn, int diagEndLine, int diagEndColumn, string diagMessage)
        {
            // Test on a version before the minimum version; we should see the expected error
            var expectedFailingJob = CompilationJob.CreateFromString("<input>", CreateTestNode(source));
            expectedFailingJob.LanguageVersion = minLanguageVersion - 1;
            var expectedFailingResult = Compiler.Compile(expectedFailingJob);

            var diag = expectedFailingResult.Diagnostics.Should()
                .ContainSingle(d => d.Code == DiagnosticDescriptor.LanguageVersionTooLow.Code, "we are below the minimum version for this language feature")
                .Subject;
            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            var expectedRange = new Range(diagStartLine, diagStartColumn, diagEndLine, diagEndColumn);
            diag.Range.Should().Be(expectedRange);
            diag.Message.Should().Match($"Language feature \"{diagMessage}\" is not available at language version {expectedFailingJob.LanguageVersion}; it requires version {minLanguageVersion} or later");

            // Test on a version that IS at the minimum version; we should NOT see the expected error
            var expectedPassingJob = CompilationJob.CreateFromString("<input>", CreateTestNode(source));
            expectedPassingJob.LanguageVersion = minLanguageVersion;
            var expectedPassingResult = Compiler.Compile(expectedPassingJob);
            expectedPassingResult.Diagnostics.Should()
                .NotContain(d => d.Code == DiagnosticDescriptor.LanguageVersionTooLow.Code, "we are at the minimum version for this language feature");
        }

        [Theory]
        [InlineData("<<enum Example>>\n<<case Test = max(1,2)>>\n<<endenum>>", 2, 0, 4, 11)]
        public void TestEnumsWithNonConstantRawValuesGenerateDiagnostic(string source, int diagStartLine, int diagStartColumn, int diagEndLine, int diagEndColumn)
        {

            var expectedFailingJob = CompilationJob.CreateFromString("<input>", CreateTestNode(source));
            var expectedFailingResult = Compiler.Compile(expectedFailingJob);

            var diag = expectedFailingResult.Diagnostics.Should()
                .ContainSingle(d => d.Code == DiagnosticDescriptor.InvalidLiteralValue.Code)
                .Subject;
            diag.Severity.Should().Be(Diagnostic.DiagnosticSeverity.Error);

            var expectedRange = new Range(diagStartLine, diagStartColumn, diagEndLine, diagEndColumn);
            diag.Range.Should().Be(expectedRange);
            diag.Message.Should().Match($"Expected a constant type");
        }

        [Theory]
        [InlineData("<<set $x = Test.Failure>>", "Test doesn't have a member named Failure", 5, 11, 5, 23)]
        [InlineData("<<declare $x = Test.Failure>>", "Test doesn't have a member named Failure", 5, 15, 5, 27)]
        public void TestEnumsWithInvalidCasesGenerateDiagnostics(string source, string messagePattern, int diagStartLine, int diagStartColumn, int diagEndLine, int diagEndColumn)
        {
            var preamble = @"<<enum Test>>
<<case Item>>
<<endenum>>
";
            PerformCommonSingleDiagLineTest(input: preamble + source,
                                            code: DiagnosticDescriptor.InvalidMemberAccess.Code,
                                            message: messagePattern,
                                            severity: Diagnostic.DiagnosticSeverity.Error,
                                            range: new Range(diagStartLine, diagStartColumn, diagEndLine, diagEndColumn),
                                            allowOthers: true);
        }

        [Fact]
        public void TestNodesWithMissingHeadersGenerateDiagnostics()
        {
            PerformCommonSingleDiagNodeTest("test: something\n---\ncontent\n===",
                                            DiagnosticDescriptor.NodeMissingTitle.Code,
                                            "Nodes must have a title",
                                            Diagnostic.DiagnosticSeverity.Error,
                                            new Range(2, 0, 2, 8));

        }

        [Fact]
        public void TestNodesWithMultipleHeadersGenerateDiagnostics()
        {
            PerformCommonSingleDiagNodeTest("title: Test\ntitle: Test2\n---\ncontent\n===",
                                            DiagnosticDescriptor.NodeHasMoreThanOneTitle.Code,
                                            "Nodes must have a single title header",
                                            Diagnostic.DiagnosticSeverity.Error,
                                            new Range(1, 0, 1, 13));

        }

        [Theory]
        [InlineData(Diagnostic.DiagnosticSeverity.Error)]
        [InlineData(Diagnostic.DiagnosticSeverity.Warning)]
        [InlineData(Diagnostic.DiagnosticSeverity.Info)]
        [InlineData(Diagnostic.DiagnosticSeverity.None)]
        public void TestDiagnosticsCanHaveOverriddenSeverities(Diagnostic.DiagnosticSeverity severity)
        {
            // Create code that causes a warning
            var source = CreateTestNode("<<declare $x = 1>>");

            var job = CompilationJob.CreateFromString("<input>", source);

            // Specify that this diagnostic has this severity
            job.DiagnosticSeverities = new Dictionary<string, Diagnostic.DiagnosticSeverity>()
            {
                {DiagnosticDescriptor.UnusedVariable.Code, severity}
            };

            // Compile; the diagnostic shuold have this severity
            var result = Compiler.Compile(job);
            var diag = result.Diagnostics.Should().Contain(d => d.Code == DiagnosticDescriptor.UnusedVariable.Code).Subject;

            diag.Severity.Should().Be(severity);
        }


        [Fact]
        public void TestCompilerGeneratesMarkupDiagnostics()
        {
            // this is just checking a single diag comes over
            // ensuring the right diags are generated is over in the markuptests
            var source = CreateTestNode("A line with [a]invalid markup");
            var job = CompilationJob.CreateFromString("<input>", source);
            job.CompilationType = CompilationJob.Type.StringsOnly;

            var result = Compiler.Compile(job);
            var diag = result.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticDescriptor.MarkupFailedToParse.Code);
        }

        [Fact]
        public void TestCompilerGeneratesUnreachableCodeDiagnostics()
        {
            // Given
            var source = CreateTestNode("""
                <<return>>
                Here's a line of dialogue
                """);

            // When
            var job = CompilationJob.CreateFromString("<input>", source);
            job.CompilationType = CompilationJob.Type.FullCompilation;
            job.Options = new Dictionary<string, string>() { { Compiler.Options.GenerateBlockGraph, "true" } };

            var result = Compiler.Compile(job);

            // Then
            result.Diagnostics.Should().ContainSingle(d => d.Code == DiagnosticDescriptor.UnreachableCode.Code);
        }
    }
}
