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

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

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

            dialogue.optionsHandler = delegate (OptionSet optionSet) {
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
            // $bas is read from but never written to
            // this means that there should be two diagnosis results
            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));

            var path = Path.Combine(TestDataPath, "AnalysisTest.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            Assert.Equal (2, diagnoses.Count);

            dialogue.UnloadAll ();

            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));

        
            result = Compiler.Compile(CompilationJob.CreateFromFiles(new[] {
                Path.Combine(SpaceDemoScriptsPath, "Ship.yarn"),
                Path.Combine(SpaceDemoScriptsPath, "Sally.yarn"),
            }));

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
            
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));
            
            dialogue.SetProgram (result.Program);

            // dialogue should not be running yet
            Assert.Null (dialogue.currentNode);

            dialogue.SetNode("Sally");
            Assert.Equal ("Sally", dialogue.currentNode);

            dialogue.Stop();
            // Current node should now be null
            Assert.Null (dialogue.currentNode);
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

            dialogue.prepareForLinesHandler = (lines) => {
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
            dialogue.library.RegisterFunction("ConcatString", (string a, string b) => a+b);
            dialogue.library.RegisterFunction("AddInt", (int a, int b) => a+b);
            dialogue.library.RegisterFunction("AddFloat", (float a, float b) => a+b);
            dialogue.library.RegisterFunction("NegateBool", (bool a) => !a);

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

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            stringTable = result.StringTable;

            dialogue.SetProgram(result.Program);
            dialogue.SetNode("Start");

            do {
                dialogue.Continue();
            } while (dialogue.IsActive);

            // The values should be of the right type and value
            Assert.Equal(Value.Type.String, this.storage.GetValue("$str").type);
            Assert.Equal("ab", this.storage.GetValue("$str").AsString);

            Assert.Equal(Value.Type.Number, this.storage.GetValue("$int").type);
            Assert.Equal(3, this.storage.GetValue("$int").AsNumber);

            Assert.Equal(Value.Type.Number, this.storage.GetValue("$float").type);
            Assert.Equal(3, this.storage.GetValue("$float").AsNumber);

            Assert.Equal(Value.Type.Bool, this.storage.GetValue("$bool").type);
            Assert.Equal(false, this.storage.GetValue("$bool").AsBool);
        }

        [Fact]
        public void TestFunctionArgumentCount() {

            // Register a function with a given number of arguments
            dialogue.library.RegisterFunction("the_func", (int a, int b, int c) => 0);

            // Run code that calls it with the wrong number of arguments
            var source = CreateTestNode("<<declare $var = 0>> <<set $var = the_func(1,2)>>");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            stringTable = result.StringTable;
            dialogue.SetProgram(result.Program);

            dialogue.SetNode("Start");

            // It should throw an InvalidOperationException because the
            // wrong number of arguments were supplied
            Assert.Throws<InvalidOperationException>(delegate {
                do {
                    dialogue.Continue();
                } while (dialogue.IsActive);
            });
        }

        [Fact]
        public void TestFunctionArgumentTypeChecking() {
            // Register a function that expects a parameter of a type that
            // Yarn Spinner can't represent
            dialogue.library.RegisterFunction("the_func", (Dialogue d) => 0);

            // Run code that calls it
            var source = CreateTestNode("<<declare $var = 0>> <<set $var = the_func(1)>>");

            var result = Compiler.Compile(CompilationJob.CreateFromString("input", source));

            stringTable = result.StringTable;
            dialogue.SetProgram(result.Program);

            dialogue.SetNode("Start");

            // It should throw an InvalidCastException because a Yarn.Value
            // can't be converted to a Dialogue
            Assert.Throws<InvalidCastException>(delegate {
                do {
                    dialogue.Continue();
                } while (dialogue.IsActive);
            });

        }
    }
}

