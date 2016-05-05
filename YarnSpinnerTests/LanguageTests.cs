using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;

namespace YarnSpinner.Tests
{


	[TestFixture ()]
	public class LanguageTests : TestBase
	{
		
			
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
		public void TestEndOfNotesWithOptionsNotAdded() {
			dialogue.LoadFile ("SkippedOptions.node");

			foreach (var result in dialogue.Run()) {
				Assert.IsNotInstanceOf<Yarn.Dialogue.OptionSetResult> (result);
			}

		}



	}

}

