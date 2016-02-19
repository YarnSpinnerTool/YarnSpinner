using NUnit.Framework;
using System;

namespace YarnSpinnerTests
{
	[TestFixture ()]
	public class Test
	{

		Yarn.MemoryVariableStore storage = new Yarn.MemoryVariableStore();
		Yarn.Dialogue dialogue;

		[SetUp()]
		public void Init()
		{
			var newWorkingDir = 
				System.IO.Path.Combine (Environment.CurrentDirectory, "Tests");
			Environment.CurrentDirectory = newWorkingDir;

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
		}

		[Test ()]
		public void TestNodeExists ()
		{
			
			dialogue.LoadFile ("Ship.json");

			dialogue.Compile ();

			Assert.True (dialogue.NodeExists ("Sally"));

			// Test clearing everything
			dialogue.UnloadAll ();

			// Load an empty node
			dialogue.LoadString("// Test, this is empty");
			dialogue.Compile ();

			Assert.False (dialogue.NodeExists ("Sally"));


		}
	}

}

