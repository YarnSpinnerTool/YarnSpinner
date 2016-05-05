using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;

namespace YarnSpinner.Tests
{
	
	public class TestBase
	{

		string nextExpectedLine = null;
		int nextExpectedOptionCount = -1;
		int nextOptionToSelect = -1;
		string nextExpectedCommand = null;


		protected VariableStorage storage = new MemoryVariableStore();
		protected Dialogue dialogue;

		protected bool errorsCauseFailures = true;

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


		protected void RunStandardTestcase() {
			foreach (var result in dialogue.Run()) {
				HandleResult (result);
			}
		}

		protected void HandleResult(Yarn.Dialogue.RunnerResult result) {

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

