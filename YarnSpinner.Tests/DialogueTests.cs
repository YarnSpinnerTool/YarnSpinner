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

            dialogue.SetProgram (result.Program);

            Assert.True (dialogue.NodeExists ("Sally"));

            // Test clearing everything
            dialogue.UnloadAll ();

            Assert.False (dialogue.NodeExists ("Sally"));

        }

        [Fact]
        public void TestOptionDestinations() {
            var path = Path.Combine(TestDataPath, "Options.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            stringTable = result.StringTable;

            dialogue.SetProgram (result.Program);

            dialogue.OptionsHandler = delegate (OptionSet optionSet) {
                Assert.Equal(2, optionSet.Options.Length);
                Assert.Equal("B", optionSet.Options[0].DestinationNode);
                Assert.Equal("C", optionSet.Options[1].DestinationNode);
            };

            dialogue.SetNode("A");

            dialogue.Continue();
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

            dialogue.SetProgram (result.Program);

            var byteCode = dialogue.GetByteCode ();
            Assert.NotNull (byteCode);

        }

        [Fact]
        public void TestMissingNode()
        {
            var path = Path.Combine (TestDataPath, "TestCases", "Smileys.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));
            
            dialogue.SetProgram (result.Program);

            errorsCauseFailures = false;

            Assert.Throws<DialogueException>( () => dialogue.SetNode("THIS NODE DOES NOT EXIST"));            
        }

        [Fact]
        public void TestGettingCurrentNodeName()  {

            string path = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");
            
            CompilationJob compilationJob = CompilationJob.CreateFromFiles(path);
            compilationJob.Library = dialogue.Library;
            
            var result = Compiler.Compile(compilationJob);
            
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
            Assert.Equal(false, boolValue);
        }

    }
}

