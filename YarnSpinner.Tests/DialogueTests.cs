using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

namespace YarnSpinner.Tests
{


    public class DialogueTests : TestBase
    {
        [Fact]
        public void TestNodeExists ()
        {
            var path = Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt");

            dialogue.LoadFile (path);

            Assert.True (dialogue.NodeExists ("Sally"));

            // Test clearing everything
            dialogue.UnloadAll ();

            // Load an empty node
            dialogue.LoadString("// Test, this is empty");

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
            var script = "// testing\n<<set $foo to 1>><<set $bar to $foo>><<set $bar to $bas>>";

            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));
            dialogue.LoadString (script);
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            Assert.Equal (2, diagnoses.Count);

            dialogue.UnloadAll ();

            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));

            dialogue.LoadFile (Path.Combine(UnityDemoScriptsPath, "Ship.yarn.txt"));
            dialogue.LoadFile (Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt"));
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            // This script should contain no unused variables
            Assert.Empty (diagnoses);
        }

        [Fact]
        public void TestDumpingCode()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn.txt");
            dialogue.LoadFile (path);

            var byteCode = dialogue.GetByteCode ();
            Assert.NotNull (byteCode);

        }

        [Fact]
        public void TestMissingNode()
        {
            var path = Path.Combine (TestDataPath, "TestCases", "Smileys.node");
            dialogue.LoadFile (path);

            errorsCauseFailures = false;

            foreach (var result in dialogue.Run("THIS NODE DOES NOT EXIST")) {

            }
        }

        [Fact]
        public void TestGettingCurrentNodeName()  {

            dialogue.LoadFile (Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt"));

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

            dialogue.LoadFile (Path.Combine(TestDataPath, "Example.yarn.txt"));

            var source = dialogue.GetTextForNode ("LearnMore");

            Assert.NotNull (source);

            Assert.Equal ("A: HAHAHA", source);
        }
		[Fact]
		public void TestGettingTags() {

			dialogue.LoadFile (Path.Combine(TestDataPath, "Example.yarn.txt"));

			var source = dialogue.GetTagsForNode ("LearnMore");

			Assert.NotNull (source);

			Assert.NotEmpty (source);

			Assert.Equal ("rawText", source.First());
		}

        [Fact]
        public void TestNodeVistation() {

            dialogue.LoadFile(Path.Combine(TestDataPath, "Example.yarn.txt"));

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

