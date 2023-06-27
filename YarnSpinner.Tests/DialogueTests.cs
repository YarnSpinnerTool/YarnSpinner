using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

using Yarn.Compiler;

using FluentAssertions;

namespace YarnSpinner.Tests
{


    public class DialogueTests : TestBase
    {
        [Fact]
        public void TestNodeExists ()
        {
            var path = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");

            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;

            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram (result.Program);

            dialogue.NodeExists ("Sally").Should().BeTrue();

            // Test clearing everything
            dialogue.UnloadAll ();

            dialogue.NodeExists ("Sally").Should().BeFalse();

        }

        [Fact]
        public void TestAnalysis() 
        {

            ICollection<Yarn.Analysis.Diagnosis> diagnoses;
            Yarn.Analysis.Context context;

            // this script has the following variables:
            // $foo is read from and written to
            // $bar is written to but never read
            // this means that there should be one diagnosis result
            context = new Yarn.Analysis.Context(typeof(Yarn.Analysis.UnusedVariableChecker));

            var path = Path.Combine(TestDataPath, "AnalysisTest.yarn");

            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;
            
            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            diagnoses.Count.Should().Be(1);
            diagnoses.First().message.Should().Contain("Variable $bar is assigned, but never read from");

            dialogue.UnloadAll ();

            context = new Yarn.Analysis.Context(typeof(Yarn.Analysis.UnusedVariableChecker));
        
            result = Compiler.Compile(CompilationJob.CreateFromFiles(new[] {
                Path.Combine(SpaceDemoScriptsPath, "Ship.yarn"),
                Path.Combine(SpaceDemoScriptsPath, "Sally.yarn"),
            }, dialogue.Library));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram (result.Program);
            
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            // This script should contain no unused variables
            diagnoses.Should().BeEmpty();
        }

        [Fact]
        public void TestDumpingCode()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn");
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram (result.Program);

            var byteCode = dialogue.GetByteCode ();
            byteCode.Should().NotBeNull();

        }

        [Fact]
        public void TestMissingNode()
        {
            var path = Path.Combine (TestDataPath, "TestCases", "Smileys.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();
            
            dialogue.SetProgram (result.Program);

            runtimeErrorsCauseFailures = false;

            var settingInvalidNode = new Action(() => dialogue.SetNode("THIS NODE DOES NOT EXIST"));
            settingInvalidNode.Should().Throw<DialogueException>();
        }

        [Fact]
        public void TestGettingCurrentNodeName()  {

            string path = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");
            
            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;
            
            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Should().BeEmpty();
            
            dialogue.SetProgram (result.Program);

            // dialogue should not be running yet
            dialogue.CurrentNode.Should().BeNull();

            dialogue.SetNode("Sally");
            dialogue.CurrentNode.Should().Be("Sally");

            dialogue.Stop();
            // Current node should now be null
            dialogue.CurrentNode.Should().BeNull();
        }

        [Fact]
        public void TestGettingRawSource() {

            var path = Path.Combine(TestDataPath, "Example.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram (result.Program);

            stringTable = result.StringTable;

            var sourceID = dialogue.GetStringIDForNode ("LearnMore");
            var source = stringTable[sourceID].text;

            source.Should().NotBeNull();

            source.Should().Be("A: HAHAHA\n");
        }
		[Fact]
		public void TestGettingTags() {

            var path = Path.Combine(TestDataPath, "Example.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            dialogue.SetProgram (result.Program);

			var source = dialogue.GetTagsForNode ("LearnMore");

			source.Should().NotBeNull();

			source.Should().NotBeEmpty();

			source.First().Should().Be("rawText");
		}

        [Fact]
        public void TestPrepareForLine() {
            var path = Path.Combine(TestDataPath, "TaggedLines.yarn");
            
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();

            stringTable = result.StringTable;
            
            bool prepareForLinesWasCalled = false;

            dialogue.PrepareForLinesHandler = (lines) => {
                // When the Dialogue realises it's about to run the Start
                // node, it will tell us that it's about to run these two
                // line IDs
                lines.Should().HaveCount(2);
                lines.Should().Contain("line:test1");
                lines.Should().Contain("line:test2");

                // Ensure that these asserts were actually called
                prepareForLinesWasCalled = true;
            };

			dialogue.SetProgram (result.Program);
            dialogue.SetNode("Start");

            prepareForLinesWasCalled.Should().BeTrue();
        }


        [Fact]
        public void TestFunctionArgumentTypeInference() {

            // Register some functions
            dialogue.Library.RegisterFunction("ConcatString", (string a, string b) => a+b);
            dialogue.Library.RegisterFunction("AddInt", (int a, int b) => a+b);
            dialogue.Library.RegisterFunction("AddFloat", (float a, float b) => a+b);
            dialogue.Library.RegisterFunction("NegateBool", (bool a) => !a);

            // Run some code to exercise these functions
            var source = CreateTestNode(@"
            <<declare $str = """">>
            <<declare $int = 0>>
            <<declare $float = 0.0>>
            <<declare $bool = false>>

            <<set $str = ConcatString(""a"", ""b"")>>
            <<set $int = AddInt(1,2)>>
            <<set $float = AddFloat(1,2)>>
            <<set $bool = NegateBool(true)>>
            ");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source, dialogue.Library));

            result.Diagnostics.Should().BeEmpty();

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.SetNode("Start");

            do {
                dialogue.Continue();
            } while (dialogue.IsActive);

            // The values should be of the right type and value
            
            this.storage.TryGetValue<string>("$str", out var strValue);
            strValue.Should().Be("ab");

            this.storage.TryGetValue<float>("$int", out var intValue);
            intValue.Should().Be(3);

            this.storage.TryGetValue<float>("$float", out var floatValue);
            floatValue.Should().Be(3);

            this.storage.TryGetValue<bool>("$bool", out var boolValue);
            boolValue.Should().BeFalse();
        }

        [Fact]
        public void TestSelectingOptionFromInsideOptionCallback() {
            var testCase = new TestPlanBuilder()
                .AddOption("option 1")
                .AddOption("option 2")
                .AddSelect(0)
                .AddLine("final line")
                .GetPlan();
            
            dialogue.LineHandler = (line) => {
                var lineText = stringTable[line.ID];
                var parsedText = dialogue.ParseMarkup(lineText.text).Text;
                testCase.Next();

                testCase.nextExpectedType.Should().Be(TestPlan.Step.Type.Line);
                parsedText.Should().Be(testCase.nextExpectedValue);

                dialogue.Continue();
            };

            dialogue.OptionsHandler = (optionSet) => {
                testCase.Next();

                int optionCount = optionSet.Options.Count();

                testCase.nextExpectedType.Should().Be(TestPlan.Step.Type.Select);
                
                // Assert that the list of options we were given is
                // identical to the list of options we expect
                var actualOptionList = optionSet.Options
                    .Select(o => (GetComposedTextForLine(o.Line), o.IsAvailable))
                    .ToList();

                actualOptionList.Should().Contain(testCase.nextExpectedOptions);

                var expectedOptionCount = testCase.nextExpectedOptions.Count();

                optionCount.Should().Be(expectedOptionCount);

                dialogue.SetSelectedOption(0);
            };

            dialogue.CommandHandler = (command) => {
                testCase.Next();
                testCase.nextExpectedType.Should().Be(TestPlan.Step.Type.Command);
                dialogue.Continue();
            };

            dialogue.DialogueCompleteHandler = () => {
                testCase.Next();
                testCase.nextExpectedType.Should().Be(TestPlan.Step.Type.Stop);
                dialogue.Continue();
            };

            var code = CreateTestNode("-> option 1\n->option 2\nfinal line\n");

            var job = CompilationJob.CreateFromString("input", code);

            var result = Compiler.Compile(job);

            result.Diagnostics.Should().BeEmpty();

            this.stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.SetNode("Start");

            dialogue.Continue();
            

        }

    }
}

