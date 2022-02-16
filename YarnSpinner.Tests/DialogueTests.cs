using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

using Yarn.Compiler;

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

            Assert.Empty(result.Diagnostics);

            dialogue.SetProgram (result.Program);

            Assert.True (dialogue.NodeExists ("Sally"));

            // Test clearing everything
            dialogue.UnloadAll ();

            Assert.False (dialogue.NodeExists ("Sally"));

        }

        [Fact]
        public void QuickTest()
        {
            Assert.Equal(1, 1);
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
            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));

            var path = Path.Combine(TestDataPath, "AnalysisTest.yarn");

            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;
            
            var result = Compiler.Compile(compilationJob);

            Assert.Empty(result.Diagnostics);

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            Assert.Equal (1, diagnoses.Count);
            Assert.Contains("Variable $bar is assigned, but never read from", diagnoses.First().message);

            dialogue.UnloadAll ();

            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));
        
            result = Compiler.Compile(CompilationJob.CreateFromFiles(new[] {
                Path.Combine(SpaceDemoScriptsPath, "Ship.yarn"),
                Path.Combine(SpaceDemoScriptsPath, "Sally.yarn"),
            }, dialogue.Library));

            Assert.Empty(result.Diagnostics);

            dialogue.SetProgram (result.Program);
            
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            // This script should contain no unused variables
            Assert.Empty (diagnoses);
        }

        [Fact]
        public void TestDumpingCode()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn");
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);

            dialogue.SetProgram (result.Program);

            var byteCode = dialogue.GetByteCode ();
            Assert.NotNull (byteCode);

        }

        [Fact]
        public void TestMissingNode()
        {
            var path = Path.Combine (TestDataPath, "TestCases", "Smileys.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);
            
            dialogue.SetProgram (result.Program);

            runtimeErrorsCauseFailures = false;

            Assert.Throws<DialogueException>( () => dialogue.SetNode("THIS NODE DOES NOT EXIST"));            
        }

        [Fact]
        public void TestGettingCurrentNodeName()  {

            string path = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");
            
            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;
            
            var result = Compiler.Compile(compilationJob);

            Assert.Empty(result.Diagnostics);
            
            dialogue.SetProgram (result.Program);

            // dialogue should not be running yet
            Assert.Null (dialogue.CurrentNode);

            dialogue.SetNode("Sally");
            Assert.Equal ("Sally", dialogue.CurrentNode);

            dialogue.Stop();
            // Current node should now be null
            Assert.Null (dialogue.CurrentNode);
        }

        [Fact]
        public void TestGettingRawSource() {

            var path = Path.Combine(TestDataPath, "Example.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);

            dialogue.SetProgram (result.Program);

            stringTable = result.StringTable;

            var sourceID = dialogue.GetStringIDForNode ("LearnMore");
            var source = stringTable[sourceID].text;

            Assert.NotNull (source);

            Assert.Equal ("A: HAHAHA\n", source);
        }
		[Fact]
		public void TestGettingTags() {

            var path = Path.Combine(TestDataPath, "Example.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);

            dialogue.SetProgram (result.Program);

			var source = dialogue.GetTagsForNode ("LearnMore");

			Assert.NotNull (source);

			Assert.NotEmpty (source);

			Assert.Equal ("rawText", source.First());
		}

        [Fact]
        public void TestPrepareForLine() {
            var path = Path.Combine(TestDataPath, "TaggedLines.yarn");
            
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);

            stringTable = result.StringTable;
            
            bool prepareForLinesWasCalled = false;

            dialogue.PrepareForLinesHandler = (lines) => {
                // When the Dialogue realises it's about to run the Start
                // node, it will tell us that it's about to run these two
                // line IDs
                Assert.Equal(2, lines.Count());
                Assert.Contains("line:test1", lines);
                Assert.Contains("line:test2", lines);

                // Ensure that these asserts were actually called
                prepareForLinesWasCalled = true;
            };

			dialogue.SetProgram (result.Program);
            dialogue.SetNode("Start");

            Assert.True(prepareForLinesWasCalled);
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

            Assert.Empty(result.Diagnostics);

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.SetNode("Start");

            do {
                dialogue.Continue();
            } while (dialogue.IsActive);

            // The values should be of the right type and value
            
            this.storage.TryGetValue<string>("$str", out var strValue);
            Assert.Equal("ab", strValue);

            this.storage.TryGetValue<float>("$int", out var intValue);
            Assert.Equal(3, intValue);

            this.storage.TryGetValue<float>("$float", out var floatValue);
            Assert.Equal(3, floatValue);

            this.storage.TryGetValue<bool>("$bool", out var boolValue);
            Assert.False(boolValue);
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

                Assert.Equal(TestPlan.Step.Type.Line, testCase.nextExpectedType);
                Assert.Equal(testCase.nextExpectedValue, parsedText);

                dialogue.Continue();
            };

            dialogue.OptionsHandler = (optionSet) => {
                testCase.Next();

                int optionCount = optionSet.Options.Count();

                Assert.Equal(TestPlan.Step.Type.Select, testCase.nextExpectedType);
                
                // Assert that the list of options we were given is
                // identical to the list of options we expect
                var actualOptionList = optionSet.Options
                    .Select(o => (GetComposedTextForLine(o.Line), o.IsAvailable))
                    .ToList();
                Assert.Equal(testCase.nextExpectedOptions, actualOptionList);

                var expectedOptionCount = testCase.nextExpectedOptions.Count();

                Assert.Equal (expectedOptionCount, optionCount);

                dialogue.SetSelectedOption(0);
            };

            dialogue.CommandHandler = (command) => {
                testCase.Next();
                Assert.Equal(TestPlan.Step.Type.Command, testCase.nextExpectedType);
                dialogue.Continue();
            };

            dialogue.DialogueCompleteHandler = () => {
                testCase.Next();
                Assert.Equal(TestPlan.Step.Type.Stop, testCase.nextExpectedType);
                dialogue.Continue();
            };

            var code = CreateTestNode("-> option 1\n->option 2\nfinal line\n");

            var job = CompilationJob.CreateFromString("input", code);

            var result = Compiler.Compile(job);

            Assert.Empty(result.Diagnostics);

            this.stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.SetNode("Start");

            dialogue.Continue();
            

        }

    }
}

