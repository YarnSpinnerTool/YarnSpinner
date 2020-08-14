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

            Compiler.CompileFile(path, out var program, out stringTable);

            dialogue.SetProgram (program);

            Assert.True (dialogue.NodeExists ("Sally"));

            // Test clearing everything
            dialogue.UnloadAll ();

            Assert.False (dialogue.NodeExists ("Sally"));

        }

        [Fact]
        public void TestOptionDestinations() {
            var path = Path.Combine(TestDataPath, "Options.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);

            dialogue.SetProgram (program);

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

            Compiler.CompileFile(path, out var program, out stringTable);

            dialogue.SetProgram(program);
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            Assert.Equal (2, diagnoses.Count);

            dialogue.UnloadAll ();

            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));

            string shipPath = Path.Combine(SpaceDemoScriptsPath, "Ship.yarn");
            Compiler.CompileFile(shipPath, out var shipProgram, out var shipStringTable);

            string sallyPath = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");
            Compiler.CompileFile(sallyPath, out var sallyProgram, out var sallyStringTable);

            stringTable = shipStringTable.Union(sallyStringTable).ToDictionary(k => k.Key, v => v.Value);            

            var combinedProgram = Program.Combine(shipProgram, sallyProgram);

            dialogue.SetProgram (combinedProgram);
            
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            // This script should contain no unused variables
            Assert.Empty (diagnoses);
        }

        [Fact]
        public void TestDumpingCode()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);

            dialogue.SetProgram (program);

            var byteCode = dialogue.GetByteCode ();
            Assert.NotNull (byteCode);

        }

        [Fact]
        public void TestMissingNode()
        {
            var path = Path.Combine (TestDataPath, "TestCases", "Smileys.yarn");

            Compiler.CompileFile(path, out var program, out stringTable);
            
            dialogue.SetProgram (program);

            errorsCauseFailures = false;

            Assert.Throws<DialogueException>( () => dialogue.SetNode("THIS NODE DOES NOT EXIST"));            
        }

        [Fact]
        public void TestGettingCurrentNodeName()  {

            string path = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");
            Compiler.CompileFile(path, out var program, out stringTable);
            
            dialogue.SetProgram (program);

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

            Compiler.CompileFile(path, out var program, out stringTable);
            dialogue.SetProgram (program);

            var sourceID = dialogue.GetStringIDForNode ("LearnMore");
            var source = stringTable[sourceID].text;

            Assert.NotNull (source);

            Assert.Equal ("A: HAHAHA\n", source);
        }
		[Fact]
		public void TestGettingTags() {

            var path = Path.Combine(TestDataPath, "Example.yarn");
            Compiler.CompileFile(path, out var program, out stringTable);
			dialogue.SetProgram (program);

			var source = dialogue.GetTagsForNode ("LearnMore");

			Assert.NotNull (source);

			Assert.NotEmpty (source);

			Assert.Equal ("rawText", source.First());
		}

        [Fact]
        public void TestPrepareForLine() {
            var path = Path.Combine(TestDataPath, "TaggedLines.yarn");
            Compiler.CompileFile(path, out var program, out stringTable);

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

			dialogue.SetProgram (program);
            dialogue.SetNode("Start");

            Assert.True(prepareForLinesWasCalled);
        }
    }
}

