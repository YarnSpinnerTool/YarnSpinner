using CLDRPlurals;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Yarn;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{
    public class LanguageTests : TestBase
    {
        public LanguageTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {

            // Register some additional functions
            dialogue.Library.RegisterFunction("add_three_operands", delegate (int a, int b, int c)
            {
                return a + b + c;
            });

            dialogue.Library.RegisterFunction("set_objective_complete", (string objective) => true);
            dialogue.Library.RegisterFunction("is_objective_active", (string objective) => true);
        }

        [Fact]
        public void TestExampleScript()
        {
            runtimeErrorsCauseFailures = false;
            var path = Path.Combine(TestDataPath, "Example.yarn");
            var testPath = Path.ChangeExtension(path, ".testplan");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            this.LoadTestPlan(testPath);

            RunStandardTestcase();
        }

        [Fact]
        public void TestEndOfNotesWithOptionsNotAdded()
        {
            var path = Path.Combine(TestDataPath, "SkippedOptions.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            dialogue.OptionsHandler = delegate (OptionSet optionSets)
            {
                throw new InvalidOperationException("Options should not be shown to the user in this test.");
            };

            dialogue.SetNode();
            dialogue.Continue();

        }

        [Fact]
        public void TestNodeHeaders()
        {
            var path = Path.Combine(TestDataPath, "Headers.yarn");
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            result.Program.Nodes.Count.Should().Be(6);

            foreach (var tag in new[] { "one", "two", "three" })
            {
                result.Program.Nodes["Tags"].Tags.Should().Contain(tag);
            }

            var headers = new Dictionary<string, List<(string, string)>>();
            headers.Add("EmptyTags", new List<(string, string)>{
                ("title","EmptyTags"),
                ("tags", null)
            });
            headers.Add("ArbitraryHeaderWithValue", new List<(string, string)>{
                ("title", "ArbitraryHeaderWithValue"),
                ("arbitraryheader", "some-arbitrary-text")
            });
            headers.Add("Tags", new List<(string, string)>{
                ("title", "Tags"),("tags",
                 "one two three")
            });
            headers.Add("SingleTagOnly", new List<(string, string)>{
                ("title", "SingleTagOnly")
            });
            headers.Add("Comments", new List<(string, string)>{
                ("title", "Comments"),
                ("tags", "one two three")
            });
            headers.Add("LotsOfHeaders", new List<(string, string)>{
                ("contains", "lots"),
                ("title", "LotsOfHeaders"),
                ("this", "node"),
                ("of", null),
                ("blank", null),
                ("others", "are"),
                ("headers", ""),
                ("some", "are"),
                ("not", "")
            });

            headers.Count.Should().Be(result.Program.Nodes.Count);
            foreach (var pair in headers)
            {
                result.Program.Nodes[pair.Key].Headers.Count.Should().Be(pair.Value.Count);

                // go through each item in the headers and ensure they are in the header list
                foreach (var header in result.Program.Nodes[pair.Key].Headers)
                {
                    var match = pair.Value.Where(t => t.Item1.Equals(header.Key)).First();
                    match.Item1.Should().Be(header.Key);

                    if (match.Item2 == null)
                    {
                        header.Value.Should().BeNullOrEmpty();
                    }
                    else
                    {
                        match.Item2.Should().Be(header.Value);
                    }
                }
            }

            // result.FileTags.Should().Contain("version:2");
            result.FileTags.Keys.Should().Contain(path);
            result.FileTags.Should().ContainSingle();
            result.FileTags[path].Should().ContainSingle();
            result.FileTags[path].Should().Contain("file_header");
        }

        [Fact]
        public void TestInvalidCharactersInNodeTitle()
        {
            var path = Path.Combine(TestDataPath, "InvalidNodeTitle.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().NotBeEmpty();

        }

        [Fact]
        public void TestNumberPlurals()
        {

            (string, double, PluralCase)[] cardinalTests = new[] {

                // English
                ("en", 1, PluralCase.One),
                ("en", 2, PluralCase.Other),
                ("en", 1.1, PluralCase.Other),

                // Arabic
                ("ar", 0, PluralCase.Zero),
                ("ar", 1, PluralCase.One),
                ("ar", 2, PluralCase.Two),
                ("ar", 3, PluralCase.Few),
                ("ar", 11, PluralCase.Many),
                ("ar", 100, PluralCase.Other),
                ("ar", 0.1, PluralCase.Other),

                // Polish
                ("pl", 1, PluralCase.One),
                ("pl", 2, PluralCase.Few),
                ("pl", 3, PluralCase.Few),
                ("pl", 4, PluralCase.Few),
                ("pl", 5, PluralCase.Many),
                ("pl", 1.1, PluralCase.Other),

                // Icelandic
                ("is", 1, PluralCase.One),
                ("is", 21, PluralCase.One),
                ("is", 31, PluralCase.One),
                ("is", 41, PluralCase.One),
                ("is", 51, PluralCase.One),
                ("is", 0, PluralCase.Other),
                ("is", 4, PluralCase.Other),
                ("is", 100, PluralCase.Other),
                ("is", 3.0, PluralCase.Other),
                ("is", 4.0, PluralCase.Other),
                ("is", 5.0, PluralCase.Other),

                // Russian
                ("ru", 1, PluralCase.One),
                ("ru", 2, PluralCase.Few),
                ("ru", 3, PluralCase.Few),
                ("ru", 5, PluralCase.Many),
                ("ru", 0, PluralCase.Many),
                ("ru", 0.1, PluralCase.Other),


            };

            (string, int, PluralCase)[] ordinalTests = new[] {
                // English
                ("en", 1, PluralCase.One),
                ("en", 2, PluralCase.Two),
                ("en", 3, PluralCase.Few),
                ("en", 4, PluralCase.Other),
                ("en", 11, PluralCase.Other),
                ("en", 21, PluralCase.One),

                // Welsh
                ("cy", 0, PluralCase.Zero),
                ("cy", 7, PluralCase.Zero),
                ("cy", 1, PluralCase.One),
                ("cy", 2, PluralCase.Two),
                ("cy", 3, PluralCase.Few),
                ("cy", 4, PluralCase.Few),
                ("cy", 5, PluralCase.Many),
                ("cy", 10, PluralCase.Other),

            };

            foreach (var test in cardinalTests)
            {
                CLDRPlurals.NumberPlurals.GetCardinalPluralCase(test.Item1, test.Item2).Should().Be(test.Item3);
            }

            foreach (var test in ordinalTests)
            {
                CLDRPlurals.NumberPlurals.GetOrdinalPluralCase(test.Item1, test.Item2).Should().Be(test.Item3);
            }


        }

        [Theory]
        [MemberData(nameof(FileSources), "TestCases")]
        [MemberData(nameof(FileSources), "Issues")]
        public void TestCompilationShouldNotBeCultureDependent(string file)
        {
            var path = Path.Combine(TestDataPath, file);

            var source = File.ReadAllText(path);

            var targetCultures = new[] {
                "en",
                "zh-Hans",
                "ru",
                "es-US",
                "es",
                "sw",
                "ar",
                "pt-BR",
                "de",
                "fr",
                "fr-FR",
                "ja",
                "pl",
                "ko",
            };

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var (invariantParseResult, _) = Utility.ParseSource(source);

            var invariantCompilationJob = CompilationJob.CreateFromString("input", source);
            invariantCompilationJob.AllowPreviewFeatures = true;
            invariantCompilationJob.Library = dialogue.Library;

            var invariantResult = Compiler.Compile(invariantCompilationJob);

            var invariantDiagnostics = invariantResult.Diagnostics.Select(d => d.ToString());
            var invariantProgram = invariantResult.Program;
            var invariantStringTable = invariantResult.StringTable.Values.Select(s => s.ToString());
            var invariantParseTree = FormatParseTreeAsText(invariantParseResult.Tree);

            foreach (var cultureName in targetCultures)
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);

                var (targetParseResult, _) = Utility.ParseSource(source);

                var targetCompilationJob = CompilationJob.CreateFromString("input", source);
                targetCompilationJob.AllowPreviewFeatures = true;
                targetCompilationJob.Library = dialogue.Library;

                var targetResult = Compiler.Compile(targetCompilationJob);

                var targetDiagnostics = targetResult.Diagnostics.Select(d => d.ToString());
                var targetProgram = targetResult.Program;
                var targetStringTable = targetResult.StringTable.Values.Select(s => s.ToString());
                var targetParseTree = FormatParseTreeAsText(targetParseResult.Tree);

                targetParseTree.Should().Be(invariantParseTree);
                targetDiagnostics.Should().ContainInOrder(invariantDiagnostics);
                targetProgram.Should().Be(invariantProgram);
                targetStringTable.Should().ContainInOrder(invariantStringTable);

            }

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        // Test every file in Tests/TestCases
        [Theory(Timeout = 1000)]
        [MemberData(nameof(FileSources), "TestCases")]
        [MemberData(nameof(FileSources), "TestCases/ParseFailures")]
        [MemberData(nameof(FileSources), "Issues")]
        public void TestSources(string file)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"INFO: Loading file {file}");

            storage.Clear();

            var scriptFilePath = Path.Combine(TestDataPath, file);

            // Attempt to compile this. If there are errors, we do not expect an
            // exception to be thrown.
            CompilationJob compilationJob = CompilationJob.CreateFromFiles(scriptFilePath);
            compilationJob.Library = dialogue.Library;

            compilationJob.AllowPreviewFeatures = true;

            var testPlanFilePath = Path.ChangeExtension(scriptFilePath, ".testplan");

            bool testPlanExists = File.Exists(testPlanFilePath);

            if (testPlanExists == false)
            {
                // No test plan for this file exists, which indicates that
                // the file is not expected to compile. We'll actually make
                // it a test failure if it _does_ compile.

                var result = Compiler.Compile(compilationJob);
                result.Diagnostics.Should().NotBeEmpty("{0} is expected to have compile errors", file);
                result.Diagnostics.Should().AllSatisfy(d => d.Range.IsValid.Should().BeTrue($"{d} should have a valid range"), "all diagnostics should have a valid position");
            }
            else
            {
                // Compile the job, and expect it to succeed.
                var result = Compiler.Compile(compilationJob);

                result.Diagnostics.Should().BeEmpty("{0} is expected to have no compile errors", file);

                result.Program.Should().NotBeNull();

                LoadTestPlan(testPlanFilePath);

                dialogue.SetProgram(result.Program);
                stringTable = result.StringTable;

                // three basic dummy functions that can be used to test inference
                dialogue.Library.RegisterFunction("dummy_bool", () => true);
                dialogue.Library.RegisterFunction("dummy_number", () => 1);
                dialogue.Library.RegisterFunction("dummy_string", () => "string");

                // If this file contains a Start node, run the test case
                // (otherwise, we're just testing its parsability, which
                // we did in the last line)
                if (dialogue.NodeExists("Start"))
                {
                    RunStandardTestcase();
                }
            }

        }

        // Test every file in Tests/TestCases
        [Theory(Timeout = 1000)]
        [MemberData(nameof(ValidFileSources), "TestCases")]
        public void TestBasicBlockExtraction(string file)
        {
            Console.ForegroundColor = ConsoleColor.Blue;

            storage.Clear();

            var scriptFilePath = Path.Combine(TestDataPath, file);

            // Attempt to compile this. If there are errors, we do not expect an
            // exception to be thrown.
            CompilationJob compilationJob = CompilationJob.CreateFromFiles(scriptFilePath);
            compilationJob.Library = dialogue.Library;

            compilationJob.AllowPreviewFeatures = true;

            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);

            result.Program.Nodes.Should().NotBeEmpty();

            foreach (var (nodeName, node) in result.Program.Nodes)
            {
                var debugInfo = result.ProjectDebugInfo.GetNodeDebugInfo(nodeName);
                debugInfo.Should().NotBeNull();
                var blocks = node.GetBasicBlocks(debugInfo);
                blocks.Should().NotBeEmpty();

                foreach (var block in blocks)
                {
                    block.Instructions.Should().NotBeEmpty();
                    block.Node.Should().Be(node);
                    block.ToString().Should().NotBeNullOrWhiteSpace();
                }
            }
        }

        [Fact]
        public void TestBasicBlockDetours()
        {
            // Given
            var source = @"
title: NodeA
---
Line 1
<<detour NodeB>>
Line 3
===
title: NodeB
---
Line 2
===
";
            CompilationJob compilationJob = CompilationJob.CreateFromString("input", source);
            compilationJob.Library = dialogue.Library;

            compilationJob.AllowPreviewFeatures = true;

            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);

            // When
            var blocks = result.Program!.Nodes["NodeA"].GetBasicBlocks(result.ProjectDebugInfo.GetNodeDebugInfo("NodeA"));

            // Then
            blocks.Should().HaveCount(2);

            var firstBlock = blocks.ElementAt(0);
            var destination = firstBlock.Destinations.Should().ContainSingle().Which.Should().BeOfType<BasicBlock.NodeDestination>().Subject;

            destination.NodeName.Should().Be("NodeB", "the first block detours to NodeB");
            destination.ReturnTo.Should().NotBeNull("the first block is a detour");
            destination.ReturnTo.NodeName.Should().Be("NodeA", "after detouring, the first block resumes in NodeA");
        }

        [Fact]
        public void TestPreviewFeaturesUnavailable()
        {
            // Given
            var node = CreateTestNode(new[] {
                "<<enum Woo>>",
                "<<case One>>",
                "<<case Two>>",
                "<<case Three>>",
                "<<endenum>>",
                "<<declare $smart_var = 1 + 2>>",
                "=> Line group item 1",
                "=> Line group item 2",
            });


            // When
            var jobWithNoPreviewFeatures = CompilationJob.CreateFromString("input", node);
            var jobWithPreviewFeatures = CompilationJob.CreateFromString("input", node);

            jobWithNoPreviewFeatures.AllowPreviewFeatures = false;
            jobWithPreviewFeatures.AllowPreviewFeatures = true;

            var resultWithNoPreviewFeatures = Compiler.Compile(jobWithNoPreviewFeatures);
            var resultWithPreviewFeatures = Compiler.Compile(jobWithPreviewFeatures);

            // Then
            resultWithNoPreviewFeatures.Diagnostics.Should().ContainEquivalentOf(new
            {
                Severity = Diagnostic.DiagnosticSeverity.Error,
                Message = "Language feature \"enums\" is only available when preview features are enabled"
            }, "enums are a preview feature");

            resultWithNoPreviewFeatures.Diagnostics.Should().ContainEquivalentOf(new
            {
                Severity = Diagnostic.DiagnosticSeverity.Error,
                Message = "Language feature \"smart variables\" is only available when preview features are enabled"
            }, "smart variables are a preview feature");

            resultWithNoPreviewFeatures.Diagnostics.Should().ContainEquivalentOf(new
            {
                Severity = Diagnostic.DiagnosticSeverity.Error,
                Message = "Language feature \"line groups\" is only available when preview features are enabled"
            }, "line groups are a preview feature");

            resultWithPreviewFeatures.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error, "preview features are allowed, so no errors are produced");
        }

        [Fact]
        public void TestTestPlanThrowsErrorOnFailure()
        {
            var source = CreateTestNode(@"
title: Start
---
Line 1
Line 2
-> Opt 1
    you chose opt 1
-> Opt 2
-> Opt 3
<<command>>
            ");


            void RunTest(string source, TestPlan plan)
            {
                var job = CompilationJob.CreateFromString("input", source, this.dialogue.Library);
                var result = Compiler.Compile(job);
                RunTestPlan(result, plan);
            }

            // When
            // Create a plan that expects the first line to be "Line 2" (which
            // it won't get)
            var failingPlan = TestPlan.FromString(@"line: `Line 2`");

            // Then
            Action act = () => RunTest(source, failingPlan);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void TestWhenClauseValuesParseAsExpressions()
        {
            // Given
            var source = @"title: Start
when: $a
some_other_header1: $a
when: $b + func_call(42, ""wow"")
some_other_header2: $b + func_call(42, ""wow"")
---
===";

            var diagnostics = new List<Diagnostic>();

            // When
            var parseResult = Compiler.ParseSyntaxTree("input", source, ref diagnostics);

            // Then
            diagnostics.Should().NotContain(e => e.Severity == Diagnostic.DiagnosticSeverity.Error, "the parse should contain no errors");

            var dialogue = parseResult.Tree.Should().BeOfType<YarnSpinnerParser.DialogueContext>().Subject;

            YarnSpinnerParser.NodeContext node = dialogue.GetChild<YarnSpinnerParser.NodeContext>(0);
            node.Should().NotBeNull("a node is declared");

            node.GetHeaders().Should().HaveCount(3, "three total non-'when' headers are declared in the node");

            var textHeader1 = node.GetHeader("some_other_header1");
            textHeader1.Should().NotBeNull();
            textHeader1.header_value.Should().NotBeNull();
            textHeader1.header_value.Text.Should().Be("$a");

            var textHeader2 = node.GetHeader("some_other_header2");
            textHeader2.Should().NotBeNull();
            textHeader2.header_value.Should().NotBeNull();
            textHeader2.header_value.Text.Should().Be("$b + func_call(42, \"wow\")");

            var whenHeaders = node.GetWhenHeaders();
            whenHeaders.Should().HaveCount(2);

            whenHeaders.Should().AllSatisfy(x =>
            {
                x.header_expression.Should().NotBeNull();
                x.header_expression.expression().Should().NotBeNull();
            }, "all 'when' headers should have an expression");

            whenHeaders.ElementAt(0).header_when_expression().expression().ToStringTree(YarnSpinnerParser.ruleNames).Should().Be("(expression (value (variable $a)))", "the first expression is a simple variable declaration");
            whenHeaders.ElementAt(1).header_when_expression().expression().ToStringTree(YarnSpinnerParser.ruleNames).Should().Be("(expression (expression (value (variable $b))) + (expression (value (function_call func_call ( (expression (value 42)) , (expression (value \"wow\")) )))))", "the second expression is a compound expression");
        }

        [Fact]
        public void TestParsingStucturedCommands()
        {
            // Given
            var validCommandText = "walk mae $var 2.3 \"string\" false true SomeArbitraryID function_call(2,\"three\") EnumA.Member .Member";
            var invalidCommandText = "walk mae {$myVar}"; // an old-style 'plain text' command

            // When
            var parsedValidCommand = StructuredCommandParser.ParseStructuredCommand(validCommandText);
            var parsedInvalidCommand = StructuredCommandParser.ParseStructuredCommand(invalidCommandText);

            // Then
            parsedValidCommand.context.command_id.Should().NotBeNull();
            parsedValidCommand.context.command_id.Text.Should().Be("walk");
            parsedValidCommand.context.structured_command_value().Should().HaveCount(10, "the command has this many parameters");
            parsedValidCommand.diagnostics.Should().BeEmpty("a valid structured command has no errors");

            parsedInvalidCommand.diagnostics.Should().NotBeEmpty("an invalid structured command has errors");

            // Even if a structured command fails to parse, we may be able to
            // extract some data from it. This helps us decide whether the parse
            // error is something we should report (i.e. this is meant to be a
            // structured command, so errors should be surfaced), or not (i.e.
            // this is not meant to be a structured command, so errors should be
            // ignored).
            parsedInvalidCommand.context.command_id.Text.Should().Be("walk");
        }
    }
}

