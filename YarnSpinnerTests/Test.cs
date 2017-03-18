using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;

namespace YarnSpinner.Tests
{


	[TestFixture ()]
	public class Test
	{
		string nextExpectedLine = null;
		int nextExpectedOptionCount = -1;
		int nextOptionToSelect = -1;
		string nextExpectedCommand = null;


		VariableStorage storage = new MemoryVariableStore();
		Dialogue dialogue;

		bool errorsCauseFailures = true;

		[SetUp()]
		public void Init()
		{

			if (System.IO.Path.GetFileName(Environment.CurrentDirectory) != "Tests") {
				if (TestContext.CurrentContext.TestDirectory == Environment.CurrentDirectory) {
					// Hop up to the folder that contains the Tests folder
					var topLevelPath = System.IO.Path.Combine(Environment.CurrentDirectory, "..", "..", "..");
					Environment.CurrentDirectory = topLevelPath;
				}

				var newWorkingDir = 
					System.IO.Path.Combine (Environment.CurrentDirectory, "Tests");
				Environment.CurrentDirectory = newWorkingDir;
			}


			dialogue = new Yarn.Dialogue (storage);

			dialogue.LogDebugMessage = delegate(string message) {
				
				Console.WriteLine (message);

			};

			dialogue.LogErrorMessage = delegate(string message) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine ("ERROR: " + message);
				Console.ResetColor ();

				if (errorsCauseFailures == true)
					Assert.Fail();
			};

			dialogue.library.RegisterFunction ("assert", -1, delegate(Yarn.Value[] parameters) {
				if (parameters[0].AsBool == false) {
					if( parameters.Length > 1 && parameters[1].AsBool ) {
						Assert.Fail ("Assertion failed: " + parameters[1].AsString);
					} else {
						Assert.Fail ("Assertion failed");
					}
				}
			});

			dialogue.library.RegisterFunction ("prepare_for_options", 2, delegate(Yarn.Value[] parameters) {
				nextExpectedOptionCount = (int)parameters[0].AsNumber;
				nextOptionToSelect = (int)parameters[1].AsNumber;
			});

			dialogue.library.RegisterFunction ("expect_line", -1, delegate(Yarn.Value[] parameters) {
				nextExpectedLine = parameters[0].AsString;
			});

			dialogue.library.RegisterFunction ("expect_command", -1, delegate(Yarn.Value[] parameters) {
				nextExpectedCommand = parameters[0].AsString;
			});
		}

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
		public void TestIndentation()
		{
			var path = System.IO.Path.Combine ("TestCases", "Indentation.node");
			dialogue.LoadFile (path);
			RunStandardTestcase ();
		}

		[Test()]
		public void TestVariableStorage()
		{
			storage.Clear ();

			var path = System.IO.Path.Combine ("TestCases", "VariableStorage.node");
			dialogue.LoadFile (path);
			RunStandardTestcase ();
		}

		[Test()]
		public void TestOptions()
		{
			var path = System.IO.Path.Combine ("TestCases", "Options.node");
			dialogue.LoadFile (path);
			RunStandardTestcase ();
		}

		[Test()]
		public void TestParsingSmileys()
		{
			var path = System.IO.Path.Combine ("TestCases", "Smileys.node");
			dialogue.LoadFile (path);
			RunStandardTestcase ();
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
		public void TestExampleScript()
		{

			errorsCauseFailures = false;
			var path = "Example.json";
			dialogue.LoadFile (path);
			RunStandardTestcase ();
		}

		[Test()]
		public void TestCommands()
		{
			var path = System.IO.Path.Combine ("TestCases", "Commands.node");
			dialogue.LoadFile (path);
			RunStandardTestcase ();
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
		public void TestTypes() 
		{
			var path = System.IO.Path.Combine ("TestCases", "Types.node");
			dialogue.LoadFile (path);
			RunStandardTestcase ();
		}

		[Test()]
		public void TestMergingNodes()
		{
			dialogue.LoadFile ("../Unity/Assets/Yarn Spinner/Examples/Demo Assets/Space/Sally.json");

			dialogue.LoadFile ("Example.json");

			// Loading code with the same contents should throw
			Assert.Throws <InvalidOperationException> (delegate () {
				dialogue.LoadFile ("Example.json");
				return;
			});
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
		public void TestSaveAndLoad()
		{
			Console.WriteLine("entering");
			var path = System.IO.Path.Combine("TestCases", "SaveTest.json");
			dialogue.LoadFile(path);
			var runner = dialogue.Run().GetEnumerator();
			runner.MoveNext();
			Assert.AreEqual((runner.Current as Yarn.Dialogue.LineResult).line.text, "This is the first line.");
			string saveState = dialogue.SerializeVM();//save
			runner.MoveNext();
			Assert.AreEqual((runner.Current as Yarn.Dialogue.LineResult).line.text, "This is the second line.");
			runner.MoveNext();
			Assert.AreEqual((runner.Current as Yarn.Dialogue.LineResult).line.text, "This is the third line.");//make sure leftover Current does not create a false positive
			dialogue.DeserializeVM(saveState);//load
			runner.MoveNext();
			Assert.AreEqual((runner.Current as Yarn.Dialogue.LineResult).line.text, "This is the second line.");
			
		}


		[Test()]
		public void TestGettingRawSource() {
			dialogue.LoadFile ("Example.json");

			var source = dialogue.GetTextForNode ("LearnMore");

			Assert.IsNotNull (source);

			Assert.AreEqual (source, "A: HAHAHA");
		}

		[Test()]
		public void TestEndOfNotesWithOptionsNotAdded() {
			dialogue.LoadFile ("SkippedOptions.node");

			foreach (var result in dialogue.Run()) {
				Assert.IsNotInstanceOf<Yarn.Dialogue.OptionSetResult> (result);
			}

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

		private void RunStandardTestcase() {
			foreach (var result in dialogue.Run()) {
				HandleResult (result);
			}
		}

		private void HandleResult(Yarn.Dialogue.RunnerResult result) {

			Console.WriteLine (result);

			if (result is Yarn.Dialogue.LineResult) {
				var text = (result as Yarn.Dialogue.LineResult).line.text;

				if (nextExpectedLine != null) {
					Assert.AreEqual (text, nextExpectedLine);
				}
			} else if (result is Yarn.Dialogue.OptionSetResult) {
				var optionCount = (result as Yarn.Dialogue.OptionSetResult).options.options.Count;
				var resultDelegate = (result as Yarn.Dialogue.OptionSetResult).setSelectedOptionDelegate;

				if (nextExpectedOptionCount != -1) {
					Assert.AreEqual (nextExpectedOptionCount, optionCount);
				}

				if (nextOptionToSelect != -1) {
					resultDelegate (nextOptionToSelect);
				}
			} else if (result is Yarn.Dialogue.CommandResult) {
				var commandText = (result as Yarn.Dialogue.CommandResult).command.text;

				if (nextExpectedCommand != null) {
					Assert.AreEqual (nextExpectedCommand, commandText);
				}
			}

			Console.WriteLine (result.ToString ());

			nextExpectedLine = null;
			nextExpectedCommand = null;
			nextExpectedOptionCount = -1;
			nextOptionToSelect = -1;

		}
	}

}

