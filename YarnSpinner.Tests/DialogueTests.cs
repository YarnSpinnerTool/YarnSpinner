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
            var path = Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt");

            var program = Compiler.CompileFile(path);

            dialogue.LoadProgram (program);

            Assert.True (dialogue.NodeExists ("Sally"));

            // Test clearing everything
            dialogue.UnloadAll ();

            Assert.False (dialogue.NodeExists ("Sally"));

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

            var program = Compiler.CompileFile(Path.Combine(TestDataPath, "AnalysisTest.yarn.txt"));

            dialogue.LoadProgram(program);
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            Assert.Equal (2, diagnoses.Count);

            dialogue.UnloadAll ();

            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));

            var shipProgram = Compiler.CompileFile(Path.Combine(UnityDemoScriptsPath, "Ship.yarn.txt"));
            var sallyProgram = Compiler.CompileFile(Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt"));

            var combinedProgram = Program.Combine(shipProgram, sallyProgram);

            dialogue.LoadProgram (combinedProgram);
            
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            // This script should contain no unused variables
            Assert.Empty (diagnoses);
        }

        [Fact]
        public void TestDumpingCode()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn.txt");

            var program = Compiler.CompileFile(path);

            dialogue.LoadProgram (program);

            var byteCode = dialogue.GetByteCode ();
            Assert.NotNull (byteCode);

        }

        [Fact]
        public void TestMissingNode()
        {
            var path = Path.Combine (TestDataPath, "TestCases", "Smileys.yarn.txt");

            var program = Compiler.CompileFile(path);

            dialogue.LoadProgram (program);

            errorsCauseFailures = false;

            foreach (var result in dialogue.Run("THIS NODE DOES NOT EXIST")) {

            }
        }

        [Fact]
        public void TestGettingCurrentNodeName()  {

            var program = Compiler.CompileFile(Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt"));

            dialogue.LoadProgram (program);

            // dialogue should not be running yet
            Assert.Null (dialogue.currentNode);

            foreach (var result in dialogue.Run("Sally")) {
                // Should now be in the node we requested
                Assert.Equal ("Sally", dialogue.currentNode);
                // Stop immediately
                dialogue.Stop ();
            }

            // Current node should now be null
            Assert.Null (dialogue.currentNode);
        }

        [Fact]
        public void TestGettingRawSource() {

            var path = Path.Combine(TestDataPath, "Example.yarn.txt");

            Program program = Compiler.CompileFile(path);
            dialogue.LoadProgram (program);

            var source = dialogue.GetTextForNode ("LearnMore");

            Assert.NotNull (source);

            Assert.Equal ("A: HAHAHA", source);
        }
		[Fact]
		public void TestGettingTags() {

            var path = Path.Combine(TestDataPath, "Example.yarn.txt");
			dialogue.LoadProgram (Compiler.CompileFile(path));

			var source = dialogue.GetTagsForNode ("LearnMore");

			Assert.NotNull (source);

			Assert.NotEmpty (source);

			Assert.Equal ("rawText", source.First());
		}

        [Fact]
        public void TestNodeVistation() {

            string fileName = Path.Combine(TestDataPath, "Example.yarn.txt");

            dialogue.LoadProgram(Compiler.CompileFile(fileName));

            foreach (var result in dialogue.Run("Leave")) {
                HandleResult (result);
            }

            Assert.Contains("Leave", dialogue.visitedNodes.ToList());

            // Override the visitedNodes list
            dialogue.visitedNodes = new string[]{ "LearnMore" };

            Assert.Contains("LearnMore", dialogue.visitedNodes.ToList());

        }


    }
}

