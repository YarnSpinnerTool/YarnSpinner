using NUnit.Framework;
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
		[SetUp]
		public new void Init()
		{
			base.Init();
		}


        [Test]
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

        [Test]
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

            Assert.AreEqual (2, diagnoses.Count);

            dialogue.UnloadAll ();

            context = new Yarn.Analysis.Context (typeof(Yarn.Analysis.UnusedVariableChecker));

            dialogue.LoadFile (Path.Combine(UnityDemoScriptsPath, "Ship.yarn.txt"));
            dialogue.LoadFile (Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt"));
            dialogue.Analyse (context);
            diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

            // This script should contain no unused variables
            Assert.IsEmpty (diagnoses);
        }

        [Test]
        public void TestDumpingCode()
        {

            var path = Path.Combine(TestDataPath, "Example.yarn.txt");
            dialogue.LoadFile (path);

            var byteCode = dialogue.GetByteCode ();
            Assert.NotNull (byteCode);

        }

        [Test]
        public void TestMissingNode()
        {
            var path = Path.Combine (TestDataPath, "TestCases", "Smileys.node");
            dialogue.LoadFile (path);

            errorsCauseFailures = false;

            foreach (var result in dialogue.Run("THIS NODE DOES NOT EXIST")) {

            }
        }

        [Test]
        public void TestGettingCurrentNodeName()  {

            dialogue.LoadFile (Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt"));

            // dialogue should not be running yet
            Assert.IsNull (dialogue.currentNode);

            foreach (var result in dialogue.Run("Sally")) {
                // Should now be in the node we requested
                Assert.AreEqual (dialogue.currentNode, "Sally");
                // Stop immediately
                dialogue.Stop ();
            }

            // Current node should now be null
            Assert.IsNull (dialogue.currentNode);
        }

        [Test]
        public void TestGettingRawSource() {

            dialogue.LoadFile (Path.Combine(TestDataPath, "Example.yarn.txt"));

            var source = dialogue.GetTextForNode ("LearnMore");

            Assert.IsNotNull (source);

            Assert.AreEqual (source, "A: HAHAHA");
        }
		[Test]
		public void TestGettingTags() {

			dialogue.LoadFile (Path.Combine(TestDataPath, "Example.yarn.txt"));

			var source = dialogue.GetTagsForNode ("LearnMore");

			Assert.IsNotNull (source);

			Assert.IsNotEmpty (source);

			Assert.AreEqual (source.First(), "rawText");
		}

        [Test]
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

