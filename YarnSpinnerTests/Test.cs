using NUnit.Framework;
using System;

namespace YarnSpinner.Tests
{


	[TestFixture ()]
	public class Test
	{



		Yarn.MemoryVariableStore storage = new Yarn.MemoryVariableStore();
		Yarn.Dialogue dialogue;

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
				Console.WriteLine (message);
				Console.ResetColor ();
			};

			dialogue.library.RegisterFunction ("assert", 1, delegate(Yarn.Value[] parameters) {
				if (parameters[0].AsBool == false)
					Assert.Fail ("Assertion failed");
			});

			dialogue.library.RegisterFunction ("prepare_for_options", -1, delegate(Yarn.Value[] parameters) {
				
			});
		}

		[Test ()]
		public void TestNodeExists ()
		{
		

			dialogue.LoadFile ("Space.json");

			dialogue.Compile ();

			Assert.True (dialogue.NodeExists ("Sally"));

			// Test clearing everything
			dialogue.UnloadAll ();

			// Load an empty node
			dialogue.LoadString("// Test, this is empty");
			dialogue.Compile ();

			Assert.False (dialogue.NodeExists ("Sally"));


		}

		[Test()]
		public void TestParsingSmileys()
		{
			var path = System.IO.Path.Combine ("TestCases", "Smileys.node");
			dialogue.LoadFile (path);
			dialogue.Compile ();
		}

		[Test()]
		public void TestCommands()
		{
			var path = System.IO.Path.Combine ("TestCases", "Commands.node");
			dialogue.LoadFile (path);
			dialogue.Compile ();

			foreach (var result in dialogue.Run()) {
				Console.WriteLine (result);
			}
		}
	}

}

