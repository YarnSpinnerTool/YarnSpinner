using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;

namespace YarnSpinner.Tests
{


	[TestFixture]
	public class LanguageTests : TestBase
	{

		[Test]
		public void TestTags()
		{
			var path = Path.Combine(TestDataPath, "TestCases", "Localisation.node");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}

		[Test]
		public void TestIndentation()
		{
			var path = Path.Combine(TestDataPath, "TestCases", "Indentation.node");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}

		[Test]
		public void TestVariableStorage()
		{
			storage.Clear();

			var path = Path.Combine(TestDataPath, "TestCases", "VariableStorage.node");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}

		[Test]
		public void TestOptions()
		{
			var path = Path.Combine(TestDataPath, "TestCases", "Options.node");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}

		[Test]
		public void TestParsingSmileys()
		{
			var path = Path.Combine(TestDataPath, "TestCases", "Smileys.node");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}


		[Test]
		public void TestExampleScript()
		{

			errorsCauseFailures = false;
			var path = Path.Combine(TestDataPath, "Example.json");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}

		[Test]
		public void TestCommands()
		{
			var path = Path.Combine(TestDataPath, "TestCases", "Commands.node");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}



		[Test]
		public void TestTypes()
		{
			var path = Path.Combine(TestDataPath, "TestCases", "Types.node");
			dialogue.LoadFile(path);
			RunStandardTestcase();
		}

		[Test]
		public void TestMergingNodes()
		{
			var sallyPath = Path.Combine(UnityDemoScriptsPath, "Sally.json");
			var examplePath = Path.Combine(TestDataPath, "Example.json");

			dialogue.LoadFile(sallyPath);
			dialogue.LoadFile(examplePath);

			// Loading code with the same contents should throw
			Assert.Throws<InvalidOperationException>(delegate ()
			{
				var path = Path.Combine(TestDataPath, "Example.json");
				dialogue.LoadFile(path);
				return;
			});
		}



		[Test]
		public void TestEndOfNotesWithOptionsNotAdded()
		{
			var path = Path.Combine(TestDataPath, "SkippedOptions.node");
			dialogue.LoadFile(path);

			foreach (var result in dialogue.Run())
			{
				Assert.IsNotInstanceOf<Dialogue.OptionSetResult>(result);
			}

		}



	}

}

