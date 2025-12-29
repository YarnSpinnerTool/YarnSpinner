using CLDRPlurals;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        public void TestIdentifiersMayContainValidCharacters()
        {
            var source = @"
title: Start
---
<<declare $demo = 1 as number>>
<<declare $实验 = 1 as number>>
<<declare $эксперимент = 1 as number>>
<<declare $mục = 1 as number>>
<<declare $🧶 = 1 as number>>
===
";

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));
            result.ContainsErrors.Should().BeFalse();
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

            var invariantParseResult = Utility.ParseSourceText(source);

            var invariantCompilationJob = CompilationJob.CreateFromString("input", source);
            invariantCompilationJob.LanguageVersion = Project.CurrentProjectFileVersion;
            invariantCompilationJob.Library = dialogue.Library;

            var invariantResult = Compiler.Compile(invariantCompilationJob);

            var invariantDiagnostics = invariantResult.Diagnostics.Select(d => d.ToString());
            var invariantProgram = invariantResult.Program;
            var invariantStringTable = invariantResult.StringTable.Values.Select(s => s.ToString());
            var invariantParseTree = FormatParseTreeAsText(invariantParseResult.Tree);

            foreach (var cultureName in targetCultures)
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);

                var targetParseResult = Utility.ParseSourceText(source);

                var targetCompilationJob = CompilationJob.CreateFromString("input", source);
                invariantCompilationJob.LanguageVersion = Project.CurrentProjectFileVersion;
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

            compilationJob.LanguageVersion = Project.CurrentProjectFileVersion;

            var testPlanFilePath = Path.ChangeExtension(scriptFilePath, ".testplan");

            bool testPlanExists = File.Exists(testPlanFilePath);

            if (testPlanExists == false)
            {
                // No test plan for this file exists, which indicates that
                // the file is not expected to compile. We'll actually make
                // it a test failure if it _does_ compile.

                var result = Compiler.Compile(compilationJob);
                result.Diagnostics.Where(d => d.Severity == Diagnostic.DiagnosticSeverity.Error).Should().NotBeEmpty("{0} is expected to have compile errors", file);
                result.Diagnostics.Should().AllSatisfy(d => d.Range.IsValid.Should().BeTrue($"{d} should have a valid range"), "all diagnostics should have a valid position");
            }
            else
            {
                // Compile the job, and expect it to succeed.
                var resultFromSource = Compiler.Compile(compilationJob);

                var jobFromInputs = CompilationJob.CreateFromInputs(resultFromSource.ParseResults.OfType<ISourceInput>(), compilationJob.Library, compilationJob.LanguageVersion);
                var result = Compiler.Compile(jobFromInputs);

                // Re-use the parsed tree from the compile; it should produce an
                // identical result. (We exclude Declarations from the
                // comparison and instead compare them by ToString because
                // Declarations gets very nested, which appears to trip up
                // BeEquivalentTo.)
                result.Should().BeEquivalentTo(resultFromSource, c => c.Excluding(ctx => ctx.Path == "Declarations"));
                result.Declarations.Select(d => d.ToString())
                    .Should().ContainInOrder(resultFromSource.Declarations.Select(d => d.ToString()));

                result.Diagnostics.Should().BeEmpty("{0} is expected to have no diagnostics", file);

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

            compilationJob.LanguageVersion = Project.CurrentProjectFileVersion;

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

            compilationJob.LanguageVersion = Project.CurrentProjectFileVersion;

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

            jobWithNoPreviewFeatures.LanguageVersion = Project.YarnSpinnerProjectVersion2;
            jobWithPreviewFeatures.LanguageVersion = Project.YarnSpinnerProjectVersion3;

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
            var parseResult = Utility.ParseSourceText(source);

            // Then
            diagnostics.Should().NotContain(e => e.Severity == Diagnostic.DiagnosticSeverity.Error, "the parse should contain no errors");

            var dialogue = parseResult.Tree.Should().BeOfType<YarnSpinnerParser.DialogueContext>().Subject;

            YarnSpinnerParser.NodeContext node = dialogue.GetChild<YarnSpinnerParser.NodeContext>(0);
            node.Should().NotBeNull("a node is declared");

            node.GetHeaders().Should().HaveCount(2, "three total non-'when', non-'title' headers are declared in the node");

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

        [Fact]
        public void TestCompilationCanBeCancelled()
        {
            string GenerateExampleSource(int expressions)
            {
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("title: Start");
                sb.AppendLine("---");

                for (int i = 0; i < expressions; i++)
                {
                    sb.AppendLine($"<<declare $a{i} = 0>>");
                }

                sb.AppendLine("===");

                return sb.ToString();
            }

            var expressionCount = 10_000;

            // Given
            var source = GenerateExampleSource(expressionCount);

            // When

            // Run the compilation without cancelling it
            Stopwatch withoutCancelling = Stopwatch.StartNew();

            {
                var job = CompilationJob.CreateFromString("input", source);

                var result = Compiler.Compile(job);

                result.ContainsErrors.Should().BeFalse();
                result.Declarations.Where(d => d.IsVariable).Should().HaveCount(expressionCount);
                withoutCancelling.Stop();
            }

            Stopwatch withCancelling = Stopwatch.StartNew();

            // Run the compilation and cancel it 500 milliseconds after starting
            var cancellationSource = new System.Threading.CancellationTokenSource();
            cancellationSource.CancelAfter(500);

            var withCancellingTask = System.Threading.Tasks.Task.Run(() =>
            {
                var job = CompilationJob.CreateFromString("input", source);
                job.CancellationToken = cancellationSource.Token;

                var result = Compiler.Compile(job);

                result.ContainsErrors.Should().BeFalse();
                result.Declarations.Where(d => d.IsVariable).Should().HaveCount(expressionCount);
                withCancelling.Stop();
            });

            new Action(withCancellingTask.Wait)
                .Should().Throw<OperationCanceledException>("the compilation should be cancelled");

            // Then
            withCancelling.Elapsed.Should().BeLessThan(withoutCancelling.Elapsed,
                "cancelling a compilation should complete sooner than letting the compilation run to completion");
        }

        [Fact]
        public void TestNodeDebugInfoContainsInfo()
        {
            // Given
            var source = @"title: NodeA
---
Line 1
Line 2
<<declare $smartVar = 1+1>>
===
title: NodeB
---
Line 3
===
title: NodeGroup
when: always
---
Line in a node group
===
";


            // When
            var compilationJob = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(compilationJob);

            // Then
            result.ProjectDebugInfo.Nodes.Should().HaveCount(6);

            var nodeAInfo = result.ProjectDebugInfo.GetNodeDebugInfo("NodeA");
            var nodeBInfo = result.ProjectDebugInfo.GetNodeDebugInfo("NodeB");
            var smartVarInfo = result.ProjectDebugInfo.GetNodeDebugInfo("$smartVar");
            var nodeGroupHub = result.ProjectDebugInfo.GetNodeDebugInfo("NodeGroup");
            var nodeGroupItem = result.ProjectDebugInfo.Nodes.FirstOrDefault(n => n.NodeName.StartsWith("NodeGroup."));
            var nodeGroupItemCondition = result.ProjectDebugInfo.Nodes.FirstOrDefault(n => n.NodeName.Contains(".Condition."));

            nodeAInfo.Should().NotBeNull();
            nodeBInfo.Should().NotBeNull();
            smartVarInfo.Should().NotBeNull();
            nodeGroupHub.Should().NotBeNull();
            nodeGroupItem.Should().NotBeNull();
            nodeGroupItemCondition.Should().NotBeNull();

            new[] { nodeAInfo, nodeBInfo, smartVarInfo }.Should().AllSatisfy(i => i.FileName.Should().Be("input"));
            nodeAInfo.Range.Should().BeEquivalentTo<Yarn.Compiler.Range>(new(0, 0, 5, 2));
            nodeBInfo.Range.Should().BeEquivalentTo<Yarn.Compiler.Range>(new(6, 0, 9, 2));
            smartVarInfo.Range.Should().BeEquivalentTo<Yarn.Compiler.Range>(new(4, 22, 4, 25),
                "the position of the smart variable 'node' is defined by the expression");

            nodeAInfo.IsImplicit.Should().BeFalse();
            nodeBInfo.IsImplicit.Should().BeFalse();
            smartVarInfo.IsImplicit.Should().BeTrue();

            nodeGroupHub.IsImplicit.Should().BeTrue("node group hubs are created by the compiler and do not appear in the input source code");
            nodeGroupItem.IsImplicit.Should().BeFalse("nodes in a node group are present in the input source code");
            nodeGroupItemCondition.IsImplicit.Should().BeTrue("node group condition smart variables are created by the compiler");
        }

        [Theory]
        [InlineData([CompilationJob.Type.TypeCheck])]
        [InlineData([CompilationJob.Type.FullCompilation])]
        [InlineData([CompilationJob.Type.StringsOnly])]
        public void TestCompilingFromExistingParseTree(CompilationJob.Type type)
        {
            // Given
            var source = @"title: NodeA
---
Line 1
Line 2
<<declare $smartVar = 1+1>>
===
title: NodeB
---
Line 3
===
title: NodeGroup
when: always
---
Line in a node group
===
";

            var firstJob = CompilationJob.CreateFromString("input", source);
            firstJob.CompilationType = type;

            // When
            var firstResult = Compiler.Compile(firstJob);

            // Then
            firstResult.ParseResults.Should().NotBeNull();
            firstResult.ParseResults.Should().NotBeEmpty();

            var secondJob = CompilationJob.CreateFromInputs(firstResult.ParseResults.OfType<ISourceInput>(), null);
            secondJob.CompilationType = type;
            var secondResult = Compiler.Compile(secondJob);

            secondResult.Should().BeEquivalentTo(firstResult, config => config.WithTracing());

        }


        // Test every file in Tests/TestCases
        [Theory(Timeout = 1000)]
        [MemberData(nameof(FileSources), "TestCases")]
        [MemberData(nameof(FileSources), "TestCases/ParseFailures")]
        [MemberData(nameof(FileSources), "Issues")]
        public void TestFastParser(string file)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"INFO: Loading file {file}");

            storage.Clear();

            var scriptFilePath = Path.Combine(TestDataPath, file);

            // Attempt to compile this. If there are errors, we do not expect an
            // exception to be thrown.
            CompilationJob compilationJob = CompilationJob.CreateFromFiles(scriptFilePath);

            compilationJob.Library = dialogue.Library;

            compilationJob.LanguageVersion = Project.CurrentProjectFileVersion;

            var testPlanFilePath = Path.ChangeExtension(scriptFilePath, ".testplan");

            bool testPlanExists = File.Exists(testPlanFilePath);

            if (testPlanExists == false)
            {
                // No test plan for this file exists, which indicates that
                // the file is not expected to compile. We'll actually make
                // it a test failure if it _does_ compile.

                var result = Compiler.Compile(compilationJob);
                result.Diagnostics.Where(d => d.Severity == Diagnostic.DiagnosticSeverity.Error).Should().NotBeEmpty("{0} is expected to have compile errors", file);
                result.Diagnostics.Should().AllSatisfy(d => d.Range.IsValid.Should().BeTrue($"{d} should have a valid range"), "all diagnostics should have a valid position");
                return;
            }

            // Compile the job, and expect it to succeed.
            var fullCompilationResult = Compiler.Compile(compilationJob);

            fullCompilationResult.ContainsErrors.Should().BeFalse();

            // Perform a fast parse on the content.
            var fileContents = File.ReadAllText(scriptFilePath);
            var nodeInfos = BasicNodeParser.GetBasicNodeInfos(fileContents, scriptFilePath).ToList();

            nodeInfos.Should().AllSatisfy(n => fullCompilationResult.Program.Nodes.Keys.Should().Contain(n.Title), "every node found by the fast parse should exist in the full parse");
        }

        [Fact]
        public void TestFastParserFindingLinks()
        {
            // Given
            var source = @"title: Start
---
<<jump DemoJump>>
<<detour DemoDetour>>
<<jump {""expression""}>>
===
";

            // When
            var nodeInfos = BasicNodeParser.GetBasicNodeInfos(source, "input");

            // Then
            var node = nodeInfos.Single();
            node.Links.Should().HaveCount(2);
            node.Links.Should().Contain(l => l.LinkType == BasicNodeInfo.Link.Type.Jump && l.Destination == "DemoJump");
            node.Links.Should().Contain(l => l.LinkType == BasicNodeInfo.Link.Type.Detour && l.Destination == "DemoDetour");
        }

        [Fact]
        public void TestCustomLineTags()
        {
            var source = @"title: Node1
---
Carlton: immediately although unlike
Carlton: schematise enfranchise enthusiastically now
Shelley: a grizzled fold
===
title: Node2
---
Shelley: to comfortable mare
Carlton: ew pain boo joyfully so
Shelley: amidst forewarn notwithstanding aha yum
-> of potentially intent
-> district incommode soon
===
title: NodeGroup
when: always
---
Shelley: vacantly failing yowza an
===
title: NodeGroup
when: always
---
Shelley: hmph apropos hmph
===
";

            var (taggedSource, updatedLines) = Utility.TagLines(new CompilationJob.File
            {
                FileName = "TestLineIDs.yarn",
                Source = source,
            },
                existingLineTags: null,
                lineTagGenerator: new DescriptiveLineTagGenerator()
            );

            var job = CompilationJob.CreateFromString("TestLineIDs.yarn", taggedSource);

            var result = Compiler.Compile(job);



        }
    }
    // Copyright (c) Microsoft Corporation.
    // Licensed under the MIT License.

    public static class TextCoordinateConverter
    {
        /// <summary>
        /// Gets the indices at which lines start in <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The text to get line starts for.</param>
        /// <returns>A collection of indices indicating where a new line
        /// starts.</returns>
        public static ImmutableArray<int> GetLineStarts(string text)
        {
            var lineStarts = new List<int> { 0 };

            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];

                if (character == '\r')
                {
                    if (i < text.Length - 1 && text[i + 1] == '\n')
                    {
                        continue;
                    }

                    lineStarts.Add(i + 1);
                }

                if (text[i] == '\n')
                {
                    lineStarts.Add(i + 1);
                }
            }

            return lineStarts.ToImmutableArray();
        }

        public static (int line, int character) GetPosition(IReadOnlyList<int> lineStarts, int offset)
        {
            if (lineStarts.Count == 0)
            {
                throw new ArgumentException($"{nameof(lineStarts)} must not be empty.");
            }

            if (lineStarts[0] != 0)
            {
                throw new ArgumentException($"The first element of {nameof(lineStarts)} must be 0, but got {lineStarts[0]}.");
            }

            if (offset < 0)
            {
                throw new ArgumentException($"{nameof(offset)} must not be a negative number.");
            }

            int line = BinarySearch(lineStarts, offset);

            if (line < 0)
            {
                // If the actual line start was not found,
                // the binary search returns the 2's-complement of the next line start, so substracting 1.
                line = ~line - 1;
            }

            return (line, offset - lineStarts[line]);
        }

        public static int GetOffset(IReadOnlyList<int> lineStarts, int line, int character)
        {
            if (line < 0 || line >= lineStarts.Count)
            {
                throw new ArgumentException("The specified line number is not valid.");
            }

            return lineStarts[line] + character;
        }

        private static int BinarySearch(IReadOnlyList<int> values, int target)
        {
            int start = 0;
            int end = values.Count - 1;

            while (start <= end)
            {
                int mid = start + ((end - start) / 2);

                if (values[mid] == target)
                {
                    return mid;
                }
                else if (values[mid] < target)
                {
                    start = mid + 1;
                }
                else
                {
                    end = mid - 1;
                }
            }

            return ~start;
        }
    }



    public class BasicNodeParser
    {
        public static IEnumerable<BasicNodeInfo> GetBasicNodeInfos(string source, string inputName)
        {

            var lineStarts = TextCoordinateConverter.GetLineStarts(source);

            var nodeMatchRegex = new Regex(
                @"\s*           # any leading whitespace before the node
                (?<headers>.*?) # headers (i.e. everything up to the ---)
                ^\s*---\s*\r?\n # the start-of-node marker, on its own line, being lenient around whitespace
                (?<body>.*?)    # the body (i.e. everything up to the ===)
                ^\s*===         # the end-of-node marker, on its own line
                ", RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            var jumpOrDetourRegex = new Regex(@"
                <<                   # start of command
                (?<type>jump|detour) # the command keyword itself
                \s+                  # gap between keyword and destination
                (?<destination>\w+)  # destination
                >>                   # end of command
                ",
            RegexOptions.IgnorePatternWhitespace);

            var headerRegex = new Regex(@"
                (?<key>\w+)          # the header key
                :\s*                 # key/value delimiter and trailing space
                (?<value>[^\r\n]*)   # the header body: everything up to the end of the line
                \r?\n                # end of line
                ", RegexOptions.IgnorePatternWhitespace);


            foreach (Match nodeMatch in nodeMatchRegex.Matches(source))
            {
                var startPosition = TextCoordinateConverter.GetPosition(lineStarts, nodeMatch.Index);
                var endPosition = TextCoordinateConverter.GetPosition(lineStarts, nodeMatch.Index + nodeMatch.Length);

                // thought: TextCoordinateConverter.GetPosition does a binary
                // search across the entire source, which is overkill here when
                // we know that the line number is between startPosition and
                // endPosition, so it might be better to do a linear sweep
                // through lineStarts starting at startPosition.line
                var headerGroup = nodeMatch.Groups["headers"];
                var headerStartPosition = TextCoordinateConverter.GetPosition(lineStarts, headerGroup.Index);
                var headerEndPosition = TextCoordinateConverter.GetPosition(lineStarts, headerGroup.Index + headerGroup.Length);

                var bodyGroup = nodeMatch.Groups["body"];
                var bodyStartPosition = TextCoordinateConverter.GetPosition(lineStarts, bodyGroup.Index);
                var bodyEndPosition = TextCoordinateConverter.GetPosition(lineStarts, bodyGroup.Index + bodyGroup.Length);

                var headersRegion = source.Substring(headerGroup.Index, headerGroup.Length);

                var headerMatches = headerRegex.Matches(headersRegion);

                var headers = headerMatches.Select(m =>
                {
                    return new BasicNodeInfo.Header
                    {
                        Key = m.Groups["key"].Value,
                        Value = m.Groups["value"].Value,
                    };
                }).ToArray();

                var bodyRegion = source.Substring(bodyGroup.Index, bodyGroup.Length);

                var linkMatches = jumpOrDetourRegex.Matches(bodyRegion);

                var links = linkMatches.Select(l =>
                {
                    return new BasicNodeInfo.Link
                    {
                        LinkType = l.Groups["type"].Value switch
                        {
                            "jump" => BasicNodeInfo.Link.Type.Jump,
                            "detour" => BasicNodeInfo.Link.Type.Detour,
                            _ => throw new InvalidOperationException("unexpected link type " + l.Groups["type"].Value)
                        },
                        Destination = l.Groups["destination"].Value
                    };
                }).ToArray();

                var title = headers.FirstOrDefault(h => h.Key == "title").Value;
                var subtitle = headers.FirstOrDefault(h => h.Key == "subtitle").Value;
                var hasWhenHeaders = headers.Any(h => h.Key == "when");

                var uniqueTitle = hasWhenHeaders ? Utility.GetNodeUniqueName(inputName, title, subtitle, headerStartPosition.line + 1) : title;

                var node = new BasicNodeInfo
                {
                    InputName = inputName,
                    NodeRange = new Yarn.Compiler.Range(startPosition.line, startPosition.character, endPosition.line, endPosition.character),
                    BodyRange = new Yarn.Compiler.Range(bodyStartPosition.line, bodyStartPosition.character, bodyEndPosition.line, bodyEndPosition.character),
                    HeadersRange = new Yarn.Compiler.Range(headerStartPosition.line, headerStartPosition.character, headerEndPosition.line, headerEndPosition.character),

                    Title = uniqueTitle,
                    Body = bodyRegion,
                    Headers = headers,
                    Links = links,
                };

                yield return node;
            }
        }
    }

    public struct BasicNodeInfo
    {
        public Yarn.Compiler.Range? NodeRange { get; set; }
        public Yarn.Compiler.Range? TitleHeaderRange { get; set; }
        public Yarn.Compiler.Range? HeadersRange { get; set; }
        public Yarn.Compiler.Range? BodyRange { get; set; }


        public string? Title { get; set; }
        public Header[]? Headers { get; set; }
        public Link[]? Links { get; set; }
        public string? Body { get; set; }

        public string? InputName { get; set; }

        public struct Link
        {
            public enum Type { Jump, Detour }
            public string? Destination { get; set; }
            public Type LinkType { get; set; }
        }

        public struct Header
        {
            public string? Key { get; set; }
            public string? Value { get; set; }
        }
    }
}


