using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;

namespace YarnSpinner.Tests
{


	[TestFixture ()]
	public class DialogueTests : TestBase {
		
		[Test()]
		public void TestNodeExists ()
		{


			dialogue.LoadFile ("../Unity/Assets/Yarn Spinner/Examples/Demo Assets/Space/Sally.json");

			Assert.True (dialogue.NodeExists ("Sally"));

			// Test clearing everything
			dialogue.UnloadAll ();

			// Load an empty node
			dialogue.LoadString("// Test, this is empty");

			Assert.False (dialogue.NodeExists ("Sally"));

		}

		[Test()]
		public void TestAnalysis() {

			ICollection<Yarn.Analysis.Diagnosis> diagnoses;
			Yarn.Analysis.Context context;


			// this script has the following variables:
			// $foo is read from and written to
			// $bar is written to but never read
			// $bas is read from but never written to
			// this means that there should be two diagnosis results
			var script = "// testing\n<<set $foo to 1>><<set $bar to $foo>><<set $bar to $bas>>";

			context = new Yarn.Analysis.Context ();
			dialogue.LoadString (script);
			dialogue.Analyse (context);
			diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

			Assert.IsTrue (diagnoses.Count == 2);

			dialogue.UnloadAll ();

			context = new Yarn.Analysis.Context ();
			dialogue.LoadFile ("../Unity/Assets/Yarn Spinner/Examples/Demo Assets/Space/Ship.json");
			dialogue.LoadFile ("../Unity/Assets/Yarn Spinner/Examples/Demo Assets/Space/Sally.json");
			dialogue.Analyse (context);
			diagnoses = new List<Yarn.Analysis.Diagnosis>(context.FinishAnalysis ());

			// This script should contain no unused variables
			Assert.IsEmpty (diagnoses);


		}

		[Test()]
		public void TestDumpingCode()
		{
			var path = "Example.json";
			dialogue.LoadFile (path);

			var byteCode = dialogue.GetByteCode ();
			Assert.NotNull (byteCode);

		}

		[Test()]
		public void TestMissingNode() 
		{
			var path = System.IO.Path.Combine ("TestCases", "Smileys.node");
			dialogue.LoadFile (path);

			errorsCauseFailures = false;

			foreach (var result in dialogue.Run("THIS NODE DOES NOT EXIST")) {

			}
		}

		[Test()]
		public void TestGettingCurrentNodeName()  {
			dialogue.LoadFile ("../Unity/Assets/Yarn Spinner/Examples/Demo Assets/Space/Sally.json");

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

		[Test()]
		public void TestGettingRawSource() {
			dialogue.LoadFile ("Example.json");

			var source = dialogue.GetTextForNode ("LearnMore");

			Assert.IsNotNull (source);

			Assert.AreEqual (source, "A: HAHAHA");
		}

		[Test()]
		public void TestNodeVistation() {
			dialogue.LoadFile ("Example.json");

			foreach (var result in dialogue.Run("Leave")) {
				HandleResult (result);
			}

			bool found;

			found = false;
			foreach (var name in dialogue.visitedNodes) {
				if (name == "Leave")
					found = true;
			}
			Assert.IsTrue (found);

			dialogue.visitedNodes = new string[]{ "LearnMore" };

			found = false;
			foreach (var name in dialogue.visitedNodes) {
				if (name == "Leave")
					found = true;
			}
			Assert.IsTrue (found);

				

		}


	}
}

